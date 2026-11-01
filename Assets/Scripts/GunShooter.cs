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
    public float recoilPitchKick = 2.2f;      // Snappy vertical camera impulse
    public float recoilYawKick = 0.65f;       // Subtle horizontal sway
    public float recoilShakeAmount = 0.10f;   // High-frequency kinetic screen shake

    [Header("── Procedural Weapon Kickback ──")]
    [Tooltip("Enable realistic weapon kickback and recovery on fire.")]
    public bool disableWeaponKick = false;   // Snappy weapon kick enabled for visceral AAA combat feel
    public float weaponKickZ = 0.038f;       // Punchy backward kick into shoulder
    public float weaponKickUp = 0.008f;      // Subtle vertical kick
    public float weaponPitchKick = 1.6f;     // Snappy muzzle climb
    public float weaponYawKick = 0.35f;      // Realistic horizontal weapon wobble
    public float weaponKickRecoverSpeed = 28f;

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

    // Pooled AAA Muzzle Flash Rig
    private GameObject _muzzleFlashRig;
    private ParticleSystem _flashCorePS;
    private ParticleSystem _flashPlumePS;
    private ParticleSystem _flashCrossPS;
    private ParticleSystem _flashSparksPS;
    private ParticleSystem _flashSmokePS;
    private ParticleSystem _flashFirePS;
    private ParticleSystem _flashFireTonguePS;
    private Light _flashLight;
    private Coroutine _flashLightRoutine;

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
        InitMuzzleFlashRig();
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

        isAiming = Input.GetMouseButton(1) || Input.GetMouseButton(0);
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

    public Transform GetBarrelTransform()
    {
        // 1. Inspector assigned barrelPoint
        if (barrelPoint != null)
            return barrelPoint;

        // 2. Gun hierarchy in GunEquipSystem
        if (_gunSystem != null && _gunSystem.gun != null)
        {
            Transform b = _gunSystem.gun.transform.Find("BarrelPoint") ?? _gunSystem.gun.transform.Find("Muzzle");
            if (b == null)
            {
                foreach (Transform t in _gunSystem.gun.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.ToLower().Contains("barrel") || t.name.ToLower().Contains("muzzle"))
                    {
                        b = t;
                        break;
                    }
                }
            }
            if (b != null)
            {
                barrelPoint = b;
                return b;
            }
        }

        return null;
    }

    public Vector3 GetEffectiveBarrelPosition()
    {
        // 1. If an actual barrel transform exists on the weapon, use its EXACT world position!
        Transform b = GetBarrelTransform();
        if (b != null)
        {
            return b.position;
        }

        // 2. Base position fallback if weapon model has no barrel point
        Vector3 handPos = transform.position + Vector3.up * 1.25f + transform.right * 0.22f;
        if (_gunSystem != null && _gunSystem.gunHandSocket != null)
        {
            handPos = _gunSystem.gunHandSocket.position;
        }

        Vector3 aimDir = transform.forward;
        if (_mainCam != null)
        {
            Ray ray = _mainCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            aimDir = ray.direction;
        }

        return handPos + aimDir * 0.85f;
    }

    private void Fire()
    {
        Vector3 muzzlePos = GetEffectiveBarrelPosition();
        Vector3 fireDir = CalculateAimDirection(muzzlePos);

        // 1. Spawn High-Velocity Continuous Bullet
        if (bulletPrefab != null)
        {
            GameObject bulletObj = Instantiate(bulletPrefab, muzzlePos, Quaternion.LookRotation(fireDir));
            Bullet bulletScript = bulletObj.GetComponent<Bullet>();
            if (bulletScript != null)
            {
                bulletScript.Launch(fireDir, bulletSpeed, bulletLifetime, fleshHitSound, groundHitSound);
            }
        }

        // 2. Play Punchy Gunshot SFX with pitch variation
        GameAudio.PlayGunshot(muzzlePos, _audioSource, 1.0f);
        GameFeel.FovPunch(2.2f);

        // 3. Pooled AAA Muzzle Flash (Core, Plume, Cross Vents, Sparks, Smoke, Light)
        TriggerMuzzleFlash(muzzlePos, fireDir);

        // 4. Authentic 5.56mm Brass Shell Casing Ejection from receiver
        EjectShellCasing();

        // 5. Procedural Weapon Kickback Impulse
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

    private Vector3 CalculateAimDirection(Vector3 muzzlePos)
    {
        if (_mainCam == null) return transform.forward;

        Ray ray = _mainCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        int aimHitMask = ~LayerMask.GetMask("Player", "Ignore Raycast");

        if (Physics.Raycast(ray, out RaycastHit hit, 500f, aimHitMask, QueryTriggerInteraction.Ignore))
        {
            Vector3 dir = (hit.point - muzzlePos).normalized;
            if (Vector3.Dot(dir, _mainCam.transform.forward) > 0.1f)
                return dir;
        }

        Vector3 targetPoint = ray.origin + ray.direction * 500f;
        return (targetPoint - muzzlePos).normalized;
    }

    private void InitMuzzleFlashRig()
    {
        if (_muzzleFlashRig != null) return;

        _muzzleFlashRig = new GameObject("AAA_MuzzleFlashRig");
        _muzzleFlashRig.transform.SetParent(transform, false);

        // 1. Dynamic Point Light
        GameObject lightObj = new GameObject("FlashLight");
        lightObj.transform.SetParent(_muzzleFlashRig.transform, false);
        _flashLight = lightObj.AddComponent<Light>();
        _flashLight.type = LightType.Point;
        _flashLight.color = new Color(1f, 0.92f, 0.65f);
        _flashLight.range = 18f;
        _flashLight.intensity = 0f;
        _flashLight.shadows = LightShadows.None;

        Material muzzleMat = VFXUtil.MuzzleFlashMat;
        Material sparkMat = VFXUtil.SparkMat;
        Material smokeMat = VFXUtil.SmokeMat;

        // 2. Core Flash (White-hot 4-point star billboard)
        GameObject coreObj = new GameObject("FlashCore");
        coreObj.transform.SetParent(_muzzleFlashRig.transform, false);
        _flashCorePS = coreObj.AddComponent<ParticleSystem>();
        var main1 = _flashCorePS.main;
        main1.loop = false;
        main1.playOnAwake = false;
        main1.startLifetime = new ParticleSystem.MinMaxCurve(0.035f, 0.05f);
        main1.startSpeed = 0f;
        main1.startSize = new ParticleSystem.MinMaxCurve(0.75f, 0.95f);
        main1.startColor = new Color(5.0f, 4.5f, 3.2f, 1f);
        main1.simulationSpace = ParticleSystemSimulationSpace.World;
        main1.maxParticles = 8;
        var em1 = _flashCorePS.emission;
        em1.rateOverTime = 0;
        var rend1 = coreObj.GetComponent<ParticleSystemRenderer>();
        rend1.material = muzzleMat;
        rend1.renderMode = ParticleSystemRenderMode.Billboard;
        rend1.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend1.receiveShadows = false;

        // 3. Bore Plume (Supersonic Flame Jet along barrel axis)
        GameObject plumeObj = new GameObject("FlashPlume");
        plumeObj.transform.SetParent(_muzzleFlashRig.transform, false);
        _flashPlumePS = plumeObj.AddComponent<ParticleSystem>();
        var mainPlume = _flashPlumePS.main;
        mainPlume.loop = false;
        mainPlume.playOnAwake = false;
        mainPlume.startLifetime = new ParticleSystem.MinMaxCurve(0.04f, 0.055f);
        mainPlume.startSpeed = new ParticleSystem.MinMaxCurve(28f, 38f);
        mainPlume.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.42f);
        mainPlume.startColor = new Color(3.8f, 2.4f, 0.8f, 1f);
        mainPlume.simulationSpace = ParticleSystemSimulationSpace.Local;
        mainPlume.maxParticles = 16;
        var emPlume = _flashPlumePS.emission;
        emPlume.rateOverTime = 0;
        var shapePlume = _flashPlumePS.shape;
        shapePlume.shapeType = ParticleSystemShapeType.Cone;
        shapePlume.angle = 8f;
        shapePlume.radius = 0.01f;
        var rendPlume = plumeObj.GetComponent<ParticleSystemRenderer>();
        rendPlume.material = muzzleMat;
        rendPlume.renderMode = ParticleSystemRenderMode.Stretch;
        rendPlume.lengthScale = 3.2f;
        rendPlume.velocityScale = 0.08f;
        rendPlume.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rendPlume.receiveShadows = false;

        // 4. M4 Flash Hider Cross Vents (4-prong radial gas jets)
        GameObject crossObj = new GameObject("FlashCrossVents");
        crossObj.transform.SetParent(_muzzleFlashRig.transform, false);
        _flashCrossPS = crossObj.AddComponent<ParticleSystem>();
        var mainCross = _flashCrossPS.main;
        mainCross.loop = false;
        mainCross.playOnAwake = false;
        mainCross.startLifetime = new ParticleSystem.MinMaxCurve(0.03f, 0.045f);
        mainCross.startSpeed = new ParticleSystem.MinMaxCurve(18f, 28f);
        mainCross.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.35f);
        mainCross.startColor = new Color(3.2f, 1.8f, 0.5f, 1f);
        mainCross.simulationSpace = ParticleSystemSimulationSpace.Local;
        mainCross.maxParticles = 16;
        var emCross = _flashCrossPS.emission;
        emCross.rateOverTime = 0;
        var shapeCross = _flashCrossPS.shape;
        shapeCross.shapeType = ParticleSystemShapeType.Circle;
        shapeCross.radius = 0.02f;
        shapeCross.arc = 360f;
        var rendCross = crossObj.GetComponent<ParticleSystemRenderer>();
        rendCross.material = muzzleMat;
        rendCross.renderMode = ParticleSystemRenderMode.Stretch;
        rendCross.lengthScale = 2.4f;
        rendCross.velocityScale = 0.06f;
        rendCross.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rendCross.receiveShadows = false;

        // 5. Sparks Shower (Incandescent needle embers using T_Spark)
        GameObject sparksObj = new GameObject("FlashSparks");
        sparksObj.transform.SetParent(_muzzleFlashRig.transform, false);
        _flashSparksPS = sparksObj.AddComponent<ParticleSystem>();
        var main2 = _flashSparksPS.main;
        main2.loop = false;
        main2.playOnAwake = false;
        main2.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
        main2.startSpeed = new ParticleSystem.MinMaxCurve(35f, 60f);
        main2.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.09f);
        main2.startColor = new Color(4.0f, 2.8f, 1.0f, 1f);
        main2.gravityModifier = 1.8f;
        main2.simulationSpace = ParticleSystemSimulationSpace.World;
        main2.maxParticles = 64;
        var em2 = _flashSparksPS.emission;
        em2.rateOverTime = 0;
        var shape2 = _flashSparksPS.shape;
        shape2.shapeType = ParticleSystemShapeType.Cone;
        shape2.angle = 20f;
        shape2.radius = 0.02f;
        var rend2 = sparksObj.GetComponent<ParticleSystemRenderer>();
        rend2.material = sparkMat;
        rend2.renderMode = ParticleSystemRenderMode.Stretch;
        rend2.lengthScale = 4.2f;
        rend2.velocityScale = 0.06f;
        rend2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend2.receiveShadows = false;

        // 6. Volumetric Gun Smoke (Soft propellant puff using T_Smoke)
        GameObject smokeObj = new GameObject("FlashSmoke");
        smokeObj.transform.SetParent(_muzzleFlashRig.transform, false);
        _flashSmokePS = smokeObj.AddComponent<ParticleSystem>();
        var main3 = _flashSmokePS.main;
        main3.loop = false;
        main3.playOnAwake = false;
        main3.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.85f);
        main3.startSpeed = new ParticleSystem.MinMaxCurve(2.0f, 5.0f);
        main3.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
        main3.startColor = new Color(0.85f, 0.85f, 0.85f, 0.22f);
        main3.gravityModifier = -0.06f;
        main3.simulationSpace = ParticleSystemSimulationSpace.World;
        main3.maxParticles = 32;
        var em3 = _flashSmokePS.emission;
        em3.rateOverTime = 0;
        var shape3 = _flashSmokePS.shape;
        shape3.shapeType = ParticleSystemShapeType.Cone;
        shape3.angle = 22f;
        shape3.radius = 0.03f;
        var colOverLife = _flashSmokePS.colorOverLifetime;
        colOverLife.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(0.88f, 0.88f, 0.88f), 0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.24f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        colOverLife.color = grad;
        var rend3 = smokeObj.GetComponent<ParticleSystemRenderer>();
        rend3.material = smokeMat;
        rend3.renderMode = ParticleSystemRenderMode.Billboard;
        rend3.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend3.receiveShadows = false;

        // 7. Volumetric Fiery Flame Burst (Expanding bright fire tongues)
        GameObject fireObj = new GameObject("FlashFireBurst");
        fireObj.transform.SetParent(_muzzleFlashRig.transform, false);
        _flashFirePS = fireObj.AddComponent<ParticleSystem>();
        var mainFire = _flashFirePS.main;
        mainFire.loop = false;
        mainFire.playOnAwake = false;
        mainFire.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
        mainFire.startSpeed = new ParticleSystem.MinMaxCurve(16f, 30f);
        mainFire.startSize = new ParticleSystem.MinMaxCurve(0.40f, 0.75f);
        mainFire.startColor = new Color(5.5f, 2.2f, 0.2f, 1f);
        mainFire.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        mainFire.gravityModifier = -0.15f;
        mainFire.simulationSpace = ParticleSystemSimulationSpace.World;
        mainFire.maxParticles = 64;
        var emFire = _flashFirePS.emission;
        emFire.rateOverTime = 0;
        var shapeFire = _flashFirePS.shape;
        shapeFire.shapeType = ParticleSystemShapeType.Cone;
        shapeFire.angle = 16f;
        shapeFire.radius = 0.02f;

        var colFire = _flashFirePS.colorOverLifetime;
        colFire.enabled = true;
        Gradient fireGrad = new Gradient();
        fireGrad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(6.0f, 4.5f, 1.2f), 0.0f),
                new GradientColorKey(new Color(5.2f, 2.0f, 0.15f), 0.28f),
                new GradientColorKey(new Color(3.8f, 0.5f, 0.04f), 0.65f),
                new GradientColorKey(new Color(0.2f, 0.04f, 0.01f), 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1.0f, 0.0f),
                new GradientAlphaKey(0.95f, 0.35f),
                new GradientAlphaKey(0.6f, 0.75f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        colFire.color = fireGrad;

        var sizeFire = _flashFirePS.sizeOverLifetime;
        sizeFire.enabled = true;
        AnimationCurve fireSizeCurve = new AnimationCurve();
        fireSizeCurve.AddKey(0.0f, 0.45f);
        fireSizeCurve.AddKey(0.35f, 1.25f);
        fireSizeCurve.AddKey(1.0f, 0.85f);
        sizeFire.size = new ParticleSystem.MinMaxCurve(1.0f, fireSizeCurve);

        var rotFire = _flashFirePS.rotationOverLifetime;
        rotFire.enabled = true;
        rotFire.z = new ParticleSystem.MinMaxCurve(-360f * Mathf.Deg2Rad, 360f * Mathf.Deg2Rad);

        var rendFire = fireObj.GetComponent<ParticleSystemRenderer>();
        rendFire.material = muzzleMat;
        rendFire.renderMode = ParticleSystemRenderMode.Billboard;
        rendFire.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rendFire.receiveShadows = false;

        // 8. Bore Fire Tongue (Stretched supersonic flame jet along barrel axis)
        GameObject tongueObj = new GameObject("FlashFireTongue");
        tongueObj.transform.SetParent(_muzzleFlashRig.transform, false);
        _flashFireTonguePS = tongueObj.AddComponent<ParticleSystem>();
        var mainTongue = _flashFireTonguePS.main;
        mainTongue.loop = false;
        mainTongue.playOnAwake = false;
        mainTongue.startLifetime = new ParticleSystem.MinMaxCurve(0.06f, 0.10f);
        mainTongue.startSpeed = new ParticleSystem.MinMaxCurve(28f, 42f);
        mainTongue.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
        mainTongue.startColor = new Color(5.2f, 2.6f, 0.3f, 1f);
        mainTongue.simulationSpace = ParticleSystemSimulationSpace.Local;
        mainTongue.maxParticles = 16;
        var emTongue = _flashFireTonguePS.emission;
        emTongue.rateOverTime = 0;
        var shapeTongue = _flashFireTonguePS.shape;
        shapeTongue.shapeType = ParticleSystemShapeType.Cone;
        shapeTongue.angle = 6f;
        shapeTongue.radius = 0.01f;
        var rendTongue = tongueObj.GetComponent<ParticleSystemRenderer>();
        rendTongue.material = muzzleMat;
        rendTongue.renderMode = ParticleSystemRenderMode.Stretch;
        rendTongue.lengthScale = 3.6f;
        rendTongue.velocityScale = 0.08f;
        rendTongue.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rendTongue.receiveShadows = false;
    }

    private void TriggerMuzzleFlash(Vector3 muzzlePos, Vector3 fireDir)
    {
        if (_muzzleFlashRig == null)
            InitMuzzleFlashRig();

        if (_muzzleFlashRig == null) return;

        _muzzleFlashRig.transform.position = muzzlePos;
        _muzzleFlashRig.transform.rotation = fireDir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(fireDir) : transform.rotation;

        // 1. Core billboard with randomized angle
        if (_flashCorePS != null)
        {
            var m = _flashCorePS.main;
            m.startRotation = Random.Range(0f, Mathf.PI * 2f);
            _flashCorePS.Emit(1);
        }

        // 2. Forward flame tongue
        if (_flashPlumePS != null)
            _flashPlumePS.Emit(4);

        // 3. Compensator radial cross vents
        if (_flashCrossPS != null)
            _flashCrossPS.Emit(6);

        // 4. Stretched incandescent needle sparks
        if (_flashSparksPS != null)
            _flashSparksPS.Emit(Random.Range(26, 36));

        // 5. Billowing soft smoke puff
        if (_flashSmokePS != null)
            _flashSmokePS.Emit(Random.Range(4, 7));

        // 6. Volumetric Fiery Flame Burst (Visible fire particles)
        if (_flashFirePS != null)
            _flashFirePS.Emit(Random.Range(12, 18));

        // 7. Supersonic Bore Flame Jet
        if (_flashFireTonguePS != null)
            _flashFireTonguePS.Emit(6);

        // 8. Fiery light & ember flash at muzzle
        VFXUtil.Burst(muzzlePos, fireDir, new Color(5.5f, 2.2f, 0.2f, 1f), 8, 8f, 20f, 0.08f, 0.2f, 0.12f, 0.25f, true, true, 0.6f, 30f);

        // 9. Rapid exponential light flash
        if (_flashLight != null)
        {
            if (_flashLightRoutine != null) StopCoroutine(_flashLightRoutine);
            _flashLightRoutine = StartCoroutine(FadeLightRoutine(15.0f, 0.05f));
        }
    }

    private System.Collections.IEnumerator FadeLightRoutine(float startIntensity, float duration)
    {
        if (_flashLight == null) yield break;
        _flashLight.intensity = startIntensity;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float n = Mathf.Clamp01(elapsed / duration);
            if (_flashLight != null)
                _flashLight.intensity = startIntensity * (1f - n * n);
            yield return null;
        }
        if (_flashLight != null) _flashLight.intensity = 0f;
    }

    private static Material _casingMat;

    private Material GetCasingMaterial()
    {
        if (_casingMat != null && _casingMat.shader != null && !_casingMat.shader.name.Contains("Error"))
            return _casingMat;

        // Guaranteed working shader directly from the active gun or scene renderer
        Shader s = null;
        if (_gunSystem != null && _gunSystem.gun != null)
        {
            Renderer gunRend = _gunSystem.gun.GetComponentInChildren<Renderer>();
            if (gunRend != null && gunRend.sharedMaterial != null && gunRend.sharedMaterial.shader != null)
            {
                string sn = gunRend.sharedMaterial.shader.name;
                if (!sn.Contains("Error")) s = gunRend.sharedMaterial.shader;
            }
        }

        if (s == null) s = VFXUtil.GetValidOpaqueShader();
        if (s == null) s = Shader.Find("Universal Render Pipeline/Lit");

        _casingMat = new Material(s);
        Color brassCol = new Color(0.92f, 0.74f, 0.25f);
        if (_casingMat.HasProperty("_BaseColor")) _casingMat.SetColor("_BaseColor", brassCol);
        if (_casingMat.HasProperty("_Color")) _casingMat.SetColor("_Color", brassCol);
        if (_casingMat.HasProperty("_Metallic")) _casingMat.SetFloat("_Metallic", 0.95f);
        if (_casingMat.HasProperty("_Smoothness")) _casingMat.SetFloat("_Smoothness", 0.85f);
        return _casingMat;
    }

    private void EjectShellCasing()
    {
        if (!ejectShellCasings) return;

        Vector3 handPos = transform.position + Vector3.up * 1.25f + transform.right * 0.22f;
        if (_gunSystem != null && _gunSystem.gunHandSocket != null)
            handPos = _gunSystem.gunHandSocket.position;

        Transform camTrans = _mainCam != null ? _mainCam.transform : transform;
        Vector3 spawnPos = handPos + camTrans.forward * 0.15f + camTrans.right * 0.08f + camTrans.up * 0.06f;

        GameObject casing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        casing.name = "ShellCasing";
        casing.transform.position = spawnPos;
        casing.transform.rotation = camTrans.rotation * Quaternion.Euler(Random.Range(-20f, 20f), Random.Range(70f, 110f), Random.Range(30f, 60f));
        casing.transform.localScale = new Vector3(0.014f, 0.038f, 0.014f); // Authentic 5.56mm NATO scale

        Collider col = casing.GetComponent<Collider>();
        if (_playerCollider != null && col != null)
            Physics.IgnoreCollision(col, _playerCollider, true);

        Renderer rend = casing.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.sharedMaterial = GetCasingMaterial();
        }

        Rigidbody rb = casing.AddComponent<Rigidbody>();
        rb.mass = 0.012f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        Vector3 ejectDir = (camTrans.right * 2.2f + camTrans.up * 1.4f - camTrans.forward * 0.5f).normalized;
        ejectDir += Random.insideUnitSphere * 0.25f;
        rb.linearVelocity = ejectDir * Random.Range(3.5f, 5.5f);
        rb.angularVelocity = new Vector3(Random.Range(20f, 40f), Random.Range(40f, 80f), Random.Range(20f, 40f));

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
            Transform b = GetBarrelTransform();
            Vector3 bPos = b != null ? b.position : transform.position + Vector3.up * 1.3f;
            Vector3 bFwd = b != null ? b.forward : transform.forward;
            Vector3 worldPoint = bPos + bFwd * crosshairDistance;
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
