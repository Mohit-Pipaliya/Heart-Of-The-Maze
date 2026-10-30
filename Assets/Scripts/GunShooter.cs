using UnityEngine;

public class GunShooter : MonoBehaviour
{
    public static GunShooter Instance { get; private set; }

    [Header("── Gun Barrel & Ballistics ──")]
    public Transform barrelPoint;
    public GameObject bulletPrefab;
    public float bulletSpeed = 320f;
    public float bulletLifetime = 4f;
    public float fireRate = 0.13f;

    [Header("── Audio SFX ──")]
    public AudioClip gunshotSound;
    public AudioClip fleshHitSound;
    public AudioClip groundHitSound;
    [Range(0.8f, 1.2f)] public float minPitch = 0.94f;
    [Range(0.8f, 1.2f)] public float maxPitch = 1.06f;

    [Header("── Camera Recoil & Shake ──")]
    public float recoilPitchKick = 0.6f;      // Camera pitch up (was 1.15 — too strong)
    public float recoilYawKick = 0.15f;       // Camera sway (was 0.35)
    public float recoilShakeAmount = 0.03f;   // Screen shake (was 0.08)

    [Header("── Procedural Weapon Kickback ──")]
    [Tooltip("Completely disable ALL weapon position/rotation kick on fire. Gun stays locked to hand socket.")]
    public bool disableWeaponKick = true;    // Gun haath mein fixed rahegi — koi jhatka nahi
    public float weaponKickZ = 0f;           // No push back
    public float weaponKickUp = 0f;          // No vertical shift
    public float weaponPitchKick = 0f;       // No rotation kick
    public float weaponYawKick = 0f;         // No yaw sway
    public float weaponKickRecoverSpeed = 30f;

    [Header("── Shell Casing Ejection ──")]
    public bool ejectShellCasings = true;

    [Header("── AAA Crosshair Settings ──")]
    public float crosshairBaseSize = 75f;
    public float aimBaseSize = 55f;
    public float expandOnFire = 22f;
    public float recoilRecoverSpeed = 14f;
    public Color crosshairColor = new Color(0.9f, 0.9f, 0.9f, 0.92f);

    [Header("── Crosshair Shapes ──")]
    public float circleRadius = 26f;
    public float lineGap = 7f;
    public float lineLength = 36f;
    public float lineThickness = 1.6f;

    [Header("── Crosshair Position ──")]
    [Tooltip("Keep ON for true screen-center AAA reticle.")]
    public bool centerOnScreen = true;
    public float crosshairDistance = 50f;

    [Header("── Hit Marker Settings ──")]
    public float hitMarkerDuration = 0.22f;
    public float hitMarkerSize = 13f;
    public float hitMarkerThickness = 2f;

    // Component References
    private GunEquipSystem _gunSystem;
    private PlayerController _playerController;
    private CameraController _cameraController;
    private Camera _mainCam;
    private AudioSource _audioSource;
    private Collider _playerCollider;

    // Firing & State
    private float _nextFireTime = 0f;
    private float _currentSize;
    public bool isAiming = false;

    // Hit-Marker State
    private float _hitMarkerTimer = 0f;
    private bool _hitMarkerIsKill = false;
    private Texture2D _whitePixelTex;

    // Weapon Kickback Tracking
    private Vector3 _weaponKickPos = Vector3.zero;
    private Vector3 _weaponKickRot = Vector3.zero;
    private Vector3 _baseGunLocalPos = Vector3.zero;
    private Quaternion _baseGunLocalRot = Quaternion.identity;
    private bool _hasRecordedBaseTransform = false;
    private Transform _recordedParent = null;

    // Textures & Rendering
    private Texture2D _crosshairTex;
    private Texture2D _particleTex;
    private float _lastRadius, _lastGap, _lastLength, _lastThickness;

    private void Awake()
    {
        Instance = this;

        _gunSystem = GetComponentInParent<GunEquipSystem>();
        if (_gunSystem == null) _gunSystem = GetComponent<GunEquipSystem>();

        _playerController = GetComponentInParent<PlayerController>();
        if (_playerController == null) _playerController = GetComponent<PlayerController>();

        _playerCollider = GetComponentInChildren<Collider>();
        _mainCam = Camera.main;

        if (_mainCam != null)
        {
            _cameraController = _mainCam.GetComponent<CameraController>();
        }
        if (_cameraController == null)
        {
            _cameraController = CameraController.Instance ?? FindFirstObjectByType<CameraController>();
        }

        // Setup dedicated 2D/3D AudioSource for punchy gunshots
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0.2f; // Punchy in ears with subtle spatial cues
            _audioSource.volume = 1.0f;
        }

        AutoLoadAudioClips();

        _currentSize = crosshairBaseSize;
        GenerateCrosshairTexture();
        GenerateParticleTexture();
    }

    private void AutoLoadAudioClips()
    {
        if (gunshotSound == null)
            gunshotSound = GameAudio.GetClip(GameAudio.SoundType.Gunshot);
        if (fleshHitSound == null)
            fleshHitSound = GameAudio.GetClip(GameAudio.SoundType.BulletFlesh);
        if (groundHitSound == null)
            groundHitSound = GameAudio.GetClip(GameAudio.SoundType.BulletGround);
    }

    private void GenerateParticleTexture()
    {
        _particleTex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float dx = (x - 16f) / 16f;
                float dy = (y - 16f) / 16f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(1f - dist);
                alpha = Mathf.Pow(alpha, 1.5f);
                _particleTex.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }
        _particleTex.Apply();
    }

    private void GenerateCrosshairTexture()
    {
        int texSize = 128;
        _crosshairTex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);

        Color transparent = new Color(0, 0, 0, 0);
        for (int y = 0; y < texSize; y++)
            for (int x = 0; x < texSize; x++)
                _crosshairTex.SetPixel(x, y, transparent);

        float center = texSize / 2f;

        for (int y = 0; y < texSize; y++)
        {
            for (int x = 0; x < texSize; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                bool draw = false;

                if (Mathf.Abs(dist - circleRadius) < lineThickness) draw = true;
                if (Mathf.Abs(dy) < lineThickness && Mathf.Abs(dx) >= lineGap && Mathf.Abs(dx) <= lineLength) draw = true;
                if (Mathf.Abs(dx) < lineThickness && Mathf.Abs(dy) >= lineGap && Mathf.Abs(dy) <= lineLength) draw = true;

                if (draw)
                    _crosshairTex.SetPixel(x, y, crosshairColor);
            }
        }

        _crosshairTex.Apply();

        _lastRadius = circleRadius;
        _lastGap = lineGap;
        _lastLength = lineLength;
        _lastThickness = lineThickness;
    }

    private void Update()
    {
        if (_lastRadius != circleRadius || _lastGap != lineGap || _lastLength != lineLength || _lastThickness != lineThickness)
        {
            GenerateCrosshairTexture();
        }

        if (_gunSystem == null || !_gunSystem.IsGunEquipped)
        {
            isAiming = false;
            return;
        }

        isAiming = Input.GetMouseButton(1);
        float targetSize = isAiming ? aimBaseSize : crosshairBaseSize;
        _currentSize = Mathf.Lerp(_currentSize, targetSize, Time.deltaTime * recoilRecoverSpeed);

        bool holdingFire = Input.GetMouseButton(0);
        bool clickedFire = Input.GetMouseButtonDown(0);

        if ((holdingFire || clickedFire) && Time.time >= _nextFireTime)
        {
            _nextFireTime = Time.time + fireRate;
            Fire();
            _currentSize += expandOnFire;
        }
    }

    private void LateUpdate()
    {
        // Gun ko haath mein BILKUL FIXED rakhna hai — koi bhi kickback position/rotation nahi
        if (_gunSystem == null || _gunSystem.gun == null) return;

        Transform gunTrans = _gunSystem.gun.transform;

        if (_gunSystem.IsGunEquipped && gunTrans.parent == _gunSystem.gunHandSocket)
        {
            // Base transform record karo (sirf ek baar jab parent match kare)
            if (!_hasRecordedBaseTransform || _recordedParent != gunTrans.parent)
            {
                _baseGunLocalPos = gunTrans.localPosition;
                _baseGunLocalRot = gunTrans.localRotation;
                _recordedParent = gunTrans.parent;
                _hasRecordedBaseTransform = true;
            }

            // Agar kick disabled hai — gun ko strictly original position pe lock karo
            if (disableWeaponKick)
            {
                gunTrans.localPosition = _baseGunLocalPos;
                gunTrans.localRotation = _baseGunLocalRot;
                _weaponKickPos = Vector3.zero;
                _weaponKickRot = Vector3.zero;
                return;
            }

            // Kick enabled hone par smoothly recover karo
            _weaponKickPos = Vector3.Lerp(_weaponKickPos, Vector3.zero, Time.deltaTime * weaponKickRecoverSpeed);
            _weaponKickRot = Vector3.Lerp(_weaponKickRot, Vector3.zero, Time.deltaTime * weaponKickRecoverSpeed);

            gunTrans.localPosition = _baseGunLocalPos + gunTrans.localRotation * _weaponKickPos;
            gunTrans.localRotation = _baseGunLocalRot * Quaternion.Euler(_weaponKickRot);
        }
        else
        {
            _hasRecordedBaseTransform = false;
            _recordedParent = null;
            _weaponKickPos = Vector3.zero;
            _weaponKickRot = Vector3.zero;
        }
    }

    private void Fire()
    {
        if (barrelPoint == null) return;

        Vector3 fireDir = CalculateAimDirection();

        // 1. Spawn High-Velocity Continuous Bullet
        if (bulletPrefab != null)
        {
            GameObject bulletObj = Instantiate(bulletPrefab, barrelPoint.position, Quaternion.LookRotation(fireDir));
            Bullet bulletScript = bulletObj.GetComponent<Bullet>();
            if (bulletScript != null)
            {
                bulletScript.Launch(fireDir, bulletSpeed, bulletLifetime, fleshHitSound, groundHitSound);
            }
        }

        // 2. Play Punchy Gunshot SFX with pitch variation
        GameAudio.PlayGunshot(barrelPoint.position, _audioSource, 1.0f);

        // 3. Scale-Adjusted 3-Stage Muzzle Flash & Dynamic Light
        SpawnRealisticMuzzleFlash();

        // 4. Brass Shell Casing Ejection
        EjectShellCasing();

        // 5. Procedural Weapon Kickback Impulse (only when kick is enabled)
        //    disableWeaponKick = true hone par gun strictly haath mein fixed rahegi
        if (!disableWeaponKick)
        {
            _weaponKickPos += new Vector3(0f, 0f, -weaponKickZ);
            _weaponKickRot += new Vector3(-weaponPitchKick, Random.Range(-weaponYawKick, weaponYawKick), 0f);
        }

        // 6. Camera Recoil & Screen Shake
        if (_cameraController == null)
        {
            _cameraController = CameraController.Instance ?? FindFirstObjectByType<CameraController>();
        }
        if (_cameraController != null)
        {
            _cameraController.AddRecoil(
                pitchKick: recoilPitchKick,
                yawKick: Random.Range(-recoilYawKick, recoilYawKick),
                shakeAmount: recoilShakeAmount
            );
        }

        // 7. Synchronized Player Animation Trigger
        if (_playerController != null)
        {
            _playerController.TriggerFireAnimation();
        }
    }

    private Vector3 CalculateAimDirection()
    {
        if (_mainCam == null) return barrelPoint.forward;

        Ray ray = _mainCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        int aimHitMask = ~LayerMask.GetMask("Player", "Ignore Raycast");

        if (Physics.Raycast(ray, out RaycastHit hit, 500f, aimHitMask, QueryTriggerInteraction.Ignore))
        {
            Vector3 dir = (hit.point - barrelPoint.position).normalized;
            if (Vector3.Dot(dir, _mainCam.transform.forward) > 0.1f)
                return dir;
        }

        Vector3 targetPoint = ray.origin + ray.direction * 500f;
        return (targetPoint - barrelPoint.position).normalized;
    }

    private static Material _sharedAdditiveMat;
    private static Material _sharedAlphaMat;

    /// <summary>
    /// Returns a particle material using URP shaders with proper asset loading.
    /// additive=true → URP Additive Glow
    /// additive=false → URP Alpha Blended Smoke
    /// </summary>
    private Material GetParticleMat(bool additive = true)
    {
        if (additive && _sharedAdditiveMat != null) return _sharedAdditiveMat;
        if (!additive && _sharedAlphaMat != null) return _sharedAlphaMat;

        Material loaded = Resources.Load<Material>(additive ? "VFX/M_ParticleAdditive" : "VFX/M_ParticleAlpha");

#if UNITY_EDITOR
        if (loaded == null)
        {
            string path = additive 
                ? "Assets/Materials/VFX/M_ParticleAdditive.mat" 
                : "Assets/Materials/VFX/M_ParticleAlpha.mat";
            loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
        }
#endif

        if (loaded == null)
        {
            loaded = Resources.Load<Material>("VFX/M_TorchFire");
#if UNITY_EDITOR
            if (loaded == null)
                loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/VFX/M_TorchFire.mat");
#endif
        }

        if (loaded != null)
        {
            Material cloned = new Material(loaded);
            if (additive) _sharedAdditiveMat = cloned;
            else _sharedAlphaMat = cloned;
            return cloned;
        }

        Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
        if (s == null) s = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (s == null) s = Shader.Find("Universal Render Pipeline/Lit");

        Material m = new Material(s);
        m.SetFloat("_Surface", 1);
        m.SetFloat("_Blend", additive ? 2 : 0);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        if (additive) m.EnableKeyword("_BLENDMODE_ADD");
        else m.EnableKeyword("_BLENDMODE_ALPHA");
        m.renderQueue = 3000;
        m.SetOverrideTag("RenderType", "Transparent");

        if (additive) _sharedAdditiveMat = m;
        else _sharedAlphaMat = m;
        return m;
    }

    private void SpawnRealisticMuzzleFlash()
    {
        GameObject flashObj = new GameObject("AAA_MuzzleFlash");
        flashObj.transform.position = barrelPoint.position;
        flashObj.transform.rotation = barrelPoint.rotation;

        // 1. Dynamic Point Light (Scaled for world)
        GameObject lightObj = new GameObject("MuzzleLight");
        lightObj.transform.SetParent(flashObj.transform, false);
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.78f, 0.3f);
        light.range = 35f;
        light.intensity = 5.5f;

        Material mat = GetParticleMat(additive: true);

        // 2. Core Flash (Bright Center Burst)
        GameObject coreObj = new GameObject("FlashCore");
        coreObj.transform.SetParent(flashObj.transform, false);
        ParticleSystem psCore = coreObj.AddComponent<ParticleSystem>();
        psCore.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main1 = psCore.main;
        main1.loop = false;
        main1.startLifetime = 0.04f;
        main1.startSpeed = 0f;
        main1.startSize = new ParticleSystem.MinMaxCurve(2.5f, 4.0f);
        main1.startColor = new Color(1.8f, 1.4f, 0.8f, 1f);
        main1.simulationSpace = ParticleSystemSimulationSpace.World;
        main1.playOnAwake = false;
        var em1 = psCore.emission;
        em1.rateOverTime = 0;
        em1.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0, 1) });
        var rend1 = coreObj.GetComponent<ParticleSystemRenderer>();
        rend1.material = mat;
        rend1.renderMode = ParticleSystemRenderMode.Billboard;

        // 3. Sparks Shower (Stretched high-speed fire sparks)
        GameObject sparksObj = new GameObject("Sparks");
        sparksObj.transform.SetParent(flashObj.transform, false);
        ParticleSystem psSparks = sparksObj.AddComponent<ParticleSystem>();
        psSparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main2 = psSparks.main;
        main2.loop = false;
        main2.startLifetime = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
        main2.startSpeed = new ParticleSystem.MinMaxCurve(60f, 100f);
        main2.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main2.startColor = new Color(1f, 0.82f, 0.25f, 1f);
        main2.simulationSpace = ParticleSystemSimulationSpace.World;
        main2.playOnAwake = false;
        var em2 = psSparks.emission;
        em2.rateOverTime = 0;
        em2.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0, 22, 35) });
        var shape2 = psSparks.shape;
        shape2.shapeType = ParticleSystemShapeType.Cone;
        shape2.angle = 20f;
        shape2.radius = 0.05f;
        var rend2 = sparksObj.GetComponent<ParticleSystemRenderer>();
        rend2.material = mat;
        rend2.renderMode = ParticleSystemRenderMode.Stretch;
        rend2.lengthScale = 2.8f;

        // 4. Volumetric Gun Smoke Puff
        GameObject smokeObj = new GameObject("SmokePuff");
        smokeObj.transform.SetParent(flashObj.transform, false);
        ParticleSystem psSmoke = smokeObj.AddComponent<ParticleSystem>();
        psSmoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main3 = psSmoke.main;
        main3.loop = false;
        main3.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main3.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
        main3.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3.2f);
        main3.startColor = new Color(0.6f, 0.6f, 0.6f, 0.35f);
        main3.simulationSpace = ParticleSystemSimulationSpace.World;
        main3.playOnAwake = false;
        var em3 = psSmoke.emission;
        em3.rateOverTime = 0;
        em3.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0, 6, 10) });
        var shape3 = psSmoke.shape;
        shape3.shapeType = ParticleSystemShapeType.Cone;
        shape3.angle = 15f;
        var colOverLife = psSmoke.colorOverLifetime;
        colOverLife.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(0.8f, 0.8f, 0.8f), 0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.4f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        colOverLife.color = grad;
        var rend3 = smokeObj.GetComponent<ParticleSystemRenderer>();
        rend3.material = GetParticleMat(additive: false); // Alpha-blend for semi-transparent smoke

        psCore.Play();
        psSparks.Play();
        psSmoke.Play();

        Destroy(flashObj, 0.6f);
    }

    private void EjectShellCasing()
    {
        if (!ejectShellCasings || barrelPoint == null) return;

        Vector3 spawnPos = barrelPoint.position - barrelPoint.forward * 0.4f + barrelPoint.up * 0.1f;
        GameObject casing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        casing.name = "ShellCasing";
        casing.transform.position = spawnPos;
        casing.transform.rotation = barrelPoint.rotation * Quaternion.Euler(0, 90f, 0);
        casing.transform.localScale = new Vector3(0.06f, 0.14f, 0.06f);

        Collider col = casing.GetComponent<Collider>();
        if (_playerCollider != null && col != null)
            Physics.IgnoreCollision(col, _playerCollider, true);

        Shader shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Diffuse");
        if (shader != null)
        {
            Material brassMat = new Material(shader);
            brassMat.color = new Color(0.92f, 0.72f, 0.25f);
            if (brassMat.HasProperty("_Metallic")) brassMat.SetFloat("_Metallic", 0.9f);
            if (brassMat.HasProperty("_Smoothness")) brassMat.SetFloat("_Smoothness", 0.8f);
            casing.GetComponent<Renderer>().material = brassMat;
        }

        Rigidbody rb = casing.AddComponent<Rigidbody>();
        rb.mass = 0.02f;

        Vector3 ejectDir = (barrelPoint.right * 1.5f + barrelPoint.up * 1.2f - barrelPoint.forward * 0.4f).normalized;
        ejectDir += Random.insideUnitSphere * 0.15f;
        rb.linearVelocity = ejectDir * Random.Range(4.5f, 7.0f);
        rb.angularVelocity = Random.insideUnitSphere * Random.Range(30f, 60f);

        Destroy(casing, 2.5f);
    }

    /// <summary>
    /// Called by Bullet when successfully hitting an enemy.
    /// </summary>
    public void RegisterHitMarker(bool isKill)
    {
        _hitMarkerTimer = hitMarkerDuration;
        _hitMarkerIsKill = isKill;
    }

    private void OnGUI()
    {
        if (!isAiming && (_gunSystem == null || !_gunSystem.IsGunEquipped)) return;

        float cx;
        float cy;

        if (centerOnScreen || _mainCam == null)
        {
            cx = Screen.width * 0.5f;
            cy = Screen.height * 0.5f;
        }
        else
        {
            Vector3 worldPoint = barrelPoint.position + barrelPoint.forward * crosshairDistance;
            Vector3 screenPos = _mainCam.WorldToScreenPoint(worldPoint);

            if (screenPos.z < 0) return;

            cx = screenPos.x;
            cy = Screen.height - screenPos.y;
        }

        // Draw dynamic reticle if aiming or gun equipped
        if (_crosshairTex != null && isAiming)
        {
            float s = _currentSize;
            float half = s * 0.5f;
            GUI.DrawTexture(new Rect(cx - half, cy - half, s, s), _crosshairTex);
        }

        // Draw Hit-Marker
        if (_hitMarkerTimer > 0f)
        {
            _hitMarkerTimer -= Time.deltaTime;
            float alpha = Mathf.Clamp01(_hitMarkerTimer / hitMarkerDuration);
            Color markerCol = _hitMarkerIsKill
                ? new Color(1f, 0.15f, 0.15f, alpha)
                : new Color(1f, 1f, 1f, alpha);

            DrawHitMarker(cx, cy, hitMarkerSize, hitMarkerThickness, markerCol);
        }
    }

    private void DrawHitMarker(float cx, float cy, float size, float thickness, Color color)
    {
        if (_whitePixelTex == null)
        {
            _whitePixelTex = new Texture2D(1, 1);
            _whitePixelTex.SetPixel(0, 0, Color.white);
            _whitePixelTex.Apply();
        }

        Color oldCol = GUI.color;
        GUI.color = color;
        Vector2 pivot = new Vector2(cx, cy);

        float[] angles = new float[] { 45f, 135f, 225f, 315f };
        float gap = 5f;
        float len = size;

        foreach (float angle in angles)
        {
            GUIUtility.RotateAroundPivot(angle, pivot);
            GUI.DrawTexture(new Rect(cx + gap, cy - thickness * 0.5f, len, thickness), _whitePixelTex);
            GUIUtility.RotateAroundPivot(-angle, pivot);
        }

        GUI.color = oldCol;
    }
}
