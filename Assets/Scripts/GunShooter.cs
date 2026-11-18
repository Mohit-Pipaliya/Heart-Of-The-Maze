using UnityEngine;
using UnityEngine.InputSystem;

public class GunShooter : MonoBehaviour
{
    public static GunShooter Instance { get; private set; }

    [Header("── Ammo & Reload Settings ──")]
    [Tooltip("Bullets currently inside active gun magazine (40 bullets per round).")]
    public int currentMagAmmo = 40;
    [Tooltip("Max capacity of one magazine round (40 bullets).")]
    public int maxMagCapacity = 40;
    [Tooltip("Current reserve bullets carried by player.")]
    public int reserveAmmo = 120;
    [Tooltip("Maximum reserve ammo player can carry (120 bullets).")]
    public int maxReserveAmmo = 120;
    [Tooltip("Reload duration in seconds when pressing R.")]
    public float reloadDuration = 1.35f;

    [Header("── 3D Magazine System (M Key & Sockets) ──")]
    [Tooltip("Whether the player has collected a spare magazine via E.")]
    public bool hasSpareMagazine = false;
    [Tooltip("Empty socket GameObject on Player body where carried spare magazine is placed.")]
    public Transform magazineHolsterSocket;
    [Tooltip("Socket/Position on Gun where active magazine sits.")]
    public Transform gunMagazineSocket;
    [Tooltip("Socket or bone on Player Left Hand where magazine is held during reload.")]
    public Transform leftHandSocket;
    [Tooltip("Prefab or 3D model of the M4 Magazine.")]
    public GameObject magazinePrefab;
    [Tooltip("Duration of full Magazine Change animation sequence.")]
    public float magChangeDuration = 1.85f;

    [Header("── Ammo HUD Settings ──")]
    public bool showAmmoHUD = true;

    // Reload & Magazine State
    public bool isReloading = false;
    public bool isChangingMagazine = false;
    private GameObject _carriedMagObj;
    private GameObject _gunMagObj;
    private Vector3 _userMagScale = Vector3.one;
    private Vector3 _userMagWorldScale = Vector3.zero;
    private GameObject _userMagTemplate;
    private string _hudMessage = null;
    private float _hudMessageTimer = 0f;

    [Header("── Gun Barrel & Ballistics ──")]
    public Transform barrelPoint;
    public GameObject bulletPrefab;
    [Tooltip("Cinematic AAA visible projectile velocity (120 - 150 m/s ensures glowing tracers are clearly visible).")]
    public float bulletSpeed = 135f;
    public float bulletLifetime = 4f;
    public float fireRate = 0.13f;
    [Tooltip("Damage dealt per bullet hit. Recommended: 75 - 100 (Spider HP: 60 [1 shot kill], Zombie HP: 1000 [10-12 shots kill]).")]
    public float bulletDamage = 85f;

    [Header("── Audio SFX ──")]
    public AudioClip gunshotSound;
    public AudioClip fleshHitSound;
    public AudioClip groundHitSound;
    [Range(0.8f, 1.2f)] public float minPitch = 0.94f;
    [Range(0.8f, 1.2f)] public float maxPitch = 1.06f;

    [Header("── Weapon Stability on Fire ──")]
    [Tooltip("If true, the gun stays 100% fixed in place when firing (zero kickback, zero hand/camera shake).")]
    public bool lockGunPositionOnFire = true;

    [Header("── Camera Recoil & Shake ──")]
    public float recoilPitchKick = 0f;        // 0 = gun and camera stay completely fixed
    public float recoilYawKick = 0f;          // 0 = no horizontal sway
    public float recoilShakeAmount = 0f;      // 0 = no camera shake

    [Header("── Procedural Weapon Kickback ──")]
    [Tooltip("Enable realistic weapon kickback and recovery on fire.")]
    public bool disableWeaponKick = true;    // True = gun does not kick or move on fire!
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

        // Ensure visible cinematic tracer speed
        if (bulletSpeed > 160f) bulletSpeed = 135f;

        _gunSystem = GetComponentInParent<GunEquipSystem>();
        if (_gunSystem == null) _gunSystem = GetComponent<GunEquipSystem>();

        _playerController = GetComponentInParent<PlayerController>();
        if (_playerController == null) _playerController = GetComponent<PlayerController>();
        GetLeftHandSocket();

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

    private void OnDestroy()
    {
        if (_muzzleFlashRig != null)
        {
            Destroy(_muzzleFlashRig);
        }
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

        // Reload Input (R Key)
        bool rPressed = (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            || Input.GetKeyDown(KeyCode.R);
        if (rPressed && !isReloading && !isChangingMagazine)
        {
            if (currentMagAmmo < maxMagCapacity && reserveAmmo > 0)
            {
                StartCoroutine(ReloadRoutine());
            }
        }

        // Magazine Change Input (M Key)
        bool mPressed = (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
            || Input.GetKeyDown(KeyCode.M);
        if (mPressed && !isReloading && !isChangingMagazine)
        {
            if (hasSpareMagazine)
            {
                StartCoroutine(ChangeMagazineRoutine());
            }
            else
            {
                _hudMessage = "NO SPARE MAGAZINE! Pick one up with [E]";
                _hudMessageTimer = 2.5f;
            }
        }

        bool holdingFire = Input.GetMouseButton(0);
        bool clickedFire = Input.GetMouseButtonDown(0);

        if ((holdingFire || clickedFire) && Time.time >= _nextFireTime)
        {
            if (isReloading || isChangingMagazine)
            {
                return; // Cannot shoot while reloading or changing magazine
            }

            if (currentMagAmmo <= 0)
            {
                _nextFireTime = Time.time + 0.25f;
                Vector3 muzzlePos = GetEffectiveBarrelPosition();
                GameAudio.PlayDryFire(muzzlePos, _audioSource);
                _hudMessage = reserveAmmo > 0 ? "EMPTY! Press [R] to Reload" : "NO AMMO! Press [M] if you have a Spare Mag";
                _hudMessageTimer = 2.0f;
                return;
            }

            _nextFireTime = Time.time + fireRate;
            currentMagAmmo--;
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

            // Gun ko jaha hai wahi bilkul strictly fixed lock rakho — fire karne par bhi bilkul nahi hilegi
            if (lockGunPositionOnFire || disableWeaponKick)
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
        // 1. Gun hierarchy in GunEquipSystem (Active held gun takes absolute priority!)
        if (_gunSystem != null && _gunSystem.gun != null)
        {
            if (barrelPoint != null && barrelPoint.IsChildOf(_gunSystem.gun.transform))
                return barrelPoint;

            Transform b = _gunSystem.gun.transform.Find("BulletPoint")
                ?? _gunSystem.gun.transform.Find("BarrelPoint")
                ?? _gunSystem.gun.transform.Find("Muzzle");
            if (b == null)
            {
                foreach (Transform t in _gunSystem.gun.GetComponentsInChildren<Transform>(true))
                {
                    string n = t.name.ToLower().Replace(" ", "").Replace("_", "");
                    if (n.Contains("bulletpoint") || n.Contains("barrel") || n.Contains("muzzle") || n.Contains("firepoint"))
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

        // 2. Inspector assigned barrelPoint fallback
        if (barrelPoint != null)
            return barrelPoint;

        return null;
    }

    public Vector3 GetEffectiveBarrelPosition()
    {
        Transform b = GetBarrelTransform();
        bool onHeldGun = b != null && _gunSystem != null && _gunSystem.gun != null && b.IsChildOf(_gunSystem.gun.transform);
        if (b != null && (onHeldGun || Vector3.Distance(b.position, transform.position) < 8f))
        {
            Vector3 muzzle = b.position;
            if (_gunSystem != null && _gunSystem.gun != null)
            {
                Vector3 alongBarrel = muzzle - _gunSystem.gun.transform.position;
                if (alongBarrel.sqrMagnitude > 0.0001f)
                    muzzle += alongBarrel.normalized * 0.04f;
            }
            return muzzle;
        }

        Vector3 handPos = transform.position + Vector3.up * 1.35f + transform.right * 0.25f;
        if (_gunSystem != null && _gunSystem.gunHandSocket != null)
            handPos = _gunSystem.gunHandSocket.position;

        Vector3 aimDir = transform.forward;
        if (_mainCam != null)
            aimDir = _mainCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)).direction;

        return handPos + aimDir * 0.75f;
    }

    private void Fire()
    {
        Vector3 muzzlePos = GetEffectiveBarrelPosition();
        ResolveAim(out Vector3 impactPoint, out SpiderEnemy aimedSpider, out ZombieAI aimedZombie);
        Vector3 fireDir = impactPoint - muzzlePos;
        if (fireDir.sqrMagnitude < 0.001f)
            fireDir = _mainCam != null ? _mainCam.transform.forward : transform.forward;
        fireDir.Normalize();

        if (bulletPrefab != null)
        {
            GameObject bulletObj = Instantiate(bulletPrefab, muzzlePos, Quaternion.LookRotation(fireDir));
            Bullet bulletScript = bulletObj.GetComponent<Bullet>();
            if (bulletScript != null)
            {
                bulletScript.damage = bulletDamage;
                if (_pendingSnakeTarget != null)
                    bulletScript.LaunchTo(impactPoint, aimedSpider, aimedZombie, _pendingSnakeTarget, bulletLifetime, fleshHitSound, groundHitSound);
                else
                    bulletScript.LaunchTo(impactPoint, aimedSpider, aimedZombie, bulletLifetime, fleshHitSound, groundHitSound);
            }
        }

        // 2. Play Punchy Gunshot SFX with pitch variation
        GameAudio.PlayGunshot(muzzlePos, _audioSource, 1.0f);
        ZombieSense.Report(muzzlePos, ZombieSense.Kind.Gunshot);

        if (!lockGunPositionOnFire)
        {
            GameFeel.FovPunch(2.2f);
        }

        // 3. Pooled AAA Muzzle Flash (Core, Plume, Cross Vents, Sparks, Smoke, Light)
        TriggerMuzzleFlash(muzzlePos, fireDir);

        // 4. Authentic 5.56mm Brass Shell Casing Ejection from receiver
        EjectShellCasing();

        // 5. Procedural Weapon Kickback Impulse (disabled when locked)
        if (!disableWeaponKick && !lockGunPositionOnFire)
        {
            _weaponKickPos += new Vector3(0f, 0f, -weaponKickZ);
            _weaponKickRot += new Vector3(-weaponPitchKick, Random.Range(-weaponYawKick, weaponYawKick), 0f);
        }
        else
        {
            _weaponKickPos = Vector3.zero;
            _weaponKickRot = Vector3.zero;
        }

        // 6. Camera Recoil & Screen Shake (disabled when locked)
        if (!lockGunPositionOnFire && recoilPitchKick > 0.001f)
        {
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
        }

        // 7. Synchronized Player Animation Trigger
        if (_playerController != null)
        {
            _playerController.TriggerFireAnimation();
        }
    }

    private void ResolveAim(out Vector3 impactPoint, out SpiderEnemy spider, out ZombieAI zombie)
    {
        spider = null;
        zombie = null;
        Camera cam = _mainCam != null ? _mainCam : Camera.main;
        if (cam == null)
        {
            impactPoint = transform.position + transform.forward * 40f;
            return;
        }

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        float best = 500f;
        impactPoint = ray.GetPoint(80f);

        RaycastHit[] hits = Physics.RaycastAll(ray, 500f, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        HumanSnakeEnemy aimedSnake = null;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col == null) continue;
            if (col.transform.IsChildOf(transform)) continue;
            if (col.GetComponentInParent<PlayerController>() != null) continue;
            if (col.GetComponentInParent<GunShooter>() != null) continue;
            if (col.isTrigger &&
                col.GetComponentInParent<SpiderEnemy>() == null &&
                col.GetComponentInParent<ZombieAI>() == null &&
                col.GetComponentInParent<HumanSnakeEnemy>() == null)
                continue;

            best = hits[i].distance;
            impactPoint = hits[i].point;
            spider = col.GetComponentInParent<SpiderEnemy>() ?? col.GetComponentInChildren<SpiderEnemy>();
            zombie = col.GetComponentInParent<ZombieAI>() ?? col.GetComponentInChildren<ZombieAI>();
            aimedSnake = col.GetComponentInParent<HumanSnakeEnemy>() ?? col.GetComponentInChildren<HumanSnakeEnemy>();
            break;
        }

        ConsiderEnemyBoundsWithSnake(ray, ref best, ref impactPoint, ref spider, ref zombie, ref aimedSnake);

        if (spider != null && !spider.IsDead)
            impactPoint = BodyCenter(spider.transform);
        else if (zombie != null && !zombie.IsDead)
            impactPoint = BodyCenter(zombie.transform);
        else if (aimedSnake != null && !aimedSnake.IsDead)
            impactPoint = BodyCenter(aimedSnake.transform);

        // Pass snake to bullet via LaunchTo (via Fire())
        // Store for Fire() to use
        _pendingSnakeTarget = aimedSnake;
    }

    private HumanSnakeEnemy _pendingSnakeTarget;

    private static void ConsiderEnemyBoundsWithSnake(Ray ray, ref float best, ref Vector3 impactPoint, ref SpiderEnemy spider, ref ZombieAI zombie, ref HumanSnakeEnemy snake)
    {
        ZombieAI[] zombies = Object.FindObjectsByType<ZombieAI>(FindObjectsSortMode.None);
        for (int i = 0; i < zombies.Length; i++)
        {
            ZombieAI z = zombies[i];
            if (z == null || z.IsDead) continue;
            if (!BodyBounds(z.transform, out Bounds bounds)) continue;
            if (!bounds.IntersectRay(ray, out float dist)) continue;
            if (dist < 0.2f || dist >= best) continue;
            best = dist;
            zombie = z;
            spider = null;
            snake = null;
            impactPoint = bounds.center;
        }

        SpiderEnemy[] spiders = Object.FindObjectsByType<SpiderEnemy>(FindObjectsSortMode.None);
        for (int i = 0; i < spiders.Length; i++)
        {
            SpiderEnemy s = spiders[i];
            if (s == null || s.IsDead) continue;
            if (!BodyBounds(s.transform, out Bounds bounds)) continue;
            if (!bounds.IntersectRay(ray, out float dist)) continue;
            if (dist < 0.2f || dist >= best) continue;
            best = dist;
            spider = s;
            zombie = null;
            snake = null;
            impactPoint = bounds.center;
        }

        HumanSnakeEnemy[] snakes = Object.FindObjectsByType<HumanSnakeEnemy>(FindObjectsSortMode.None);
        for (int i = 0; i < snakes.Length; i++)
        {
            HumanSnakeEnemy sn = snakes[i];
            if (sn == null || sn.IsDead) continue;
            if (!BodyBounds(sn.transform, out Bounds bounds)) continue;
            if (!bounds.IntersectRay(ray, out float dist)) continue;
            if (dist < 0.2f || dist >= best) continue;
            best = dist;
            snake = sn;
            spider = null;
            zombie = null;
            impactPoint = bounds.center;
        }
    }

    private static void ConsiderEnemyBounds(Ray ray, ref float best, ref Vector3 impactPoint, ref SpiderEnemy spider, ref ZombieAI zombie)
    {
        ZombieAI[] zombies = Object.FindObjectsByType<ZombieAI>(FindObjectsSortMode.None);
        for (int i = 0; i < zombies.Length; i++)
        {
            ZombieAI z = zombies[i];
            if (z == null || z.IsDead) continue;
            if (!BodyBounds(z.transform, out Bounds bounds)) continue;
            if (!bounds.IntersectRay(ray, out float dist)) continue;
            if (dist < 0.2f || dist >= best) continue;
            best = dist;
            zombie = z;
            spider = null;
            impactPoint = bounds.center;
        }

        SpiderEnemy[] spiders = Object.FindObjectsByType<SpiderEnemy>(FindObjectsSortMode.None);
        for (int i = 0; i < spiders.Length; i++)
        {
            SpiderEnemy s = spiders[i];
            if (s == null || s.IsDead) continue;
            if (!BodyBounds(s.transform, out Bounds bounds)) continue;
            if (!bounds.IntersectRay(ray, out float dist)) continue;
            if (dist < 0.2f || dist >= best) continue;
            best = dist;
            spider = s;
            zombie = null;
            impactPoint = bounds.center;
        }
    }

    private static Vector3 BodyCenter(Transform body)
    {
        if (BodyBounds(body, out Bounds bounds))
            return bounds.center;
        return body.position + Vector3.up * 1f;
    }

    private static bool BodyBounds(Transform body, out Bounds bounds)
    {
        bounds = default;
        Renderer[] renders = body.GetComponentsInChildren<Renderer>();
        bool found = false;
        for (int i = 0; i < renders.Length; i++)
        {
            if (renders[i] == null) continue;
            if (!found)
            {
                bounds = renders[i].bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renders[i].bounds);
            }
        }
        return found;
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
        _muzzleFlashRig.transform.SetParent(null); // World space: independent of character motion & rig scales
        DontDestroyOnLoad(_muzzleFlashRig);

        // 1. Dynamic Point Light (warm incandescent illumination)
        GameObject lightObj = new GameObject("FlashLight");
        lightObj.transform.SetParent(_muzzleFlashRig.transform, false);
        _flashLight = lightObj.AddComponent<Light>();
        _flashLight.type = LightType.Point;
        _flashLight.color = new Color(1f, 0.88f, 0.55f);
        _flashLight.range = 22f;
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
        main1.startLifetime = new ParticleSystem.MinMaxCurve(0.065f, 0.095f);
        main1.startSpeed = 0f;
        main1.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
        main1.startColor = new Color(8.0f, 6.8f, 4.0f, 1f);
        main1.simulationSpace = ParticleSystemSimulationSpace.World;
        main1.maxParticles = 16;
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
        mainPlume.startLifetime = new ParticleSystem.MinMaxCurve(0.06f, 0.09f);
        mainPlume.startSpeed = new ParticleSystem.MinMaxCurve(28f, 42f);
        mainPlume.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.75f);
        mainPlume.startColor = new Color(6.5f, 4.0f, 1.2f, 1f);
        mainPlume.simulationSpace = ParticleSystemSimulationSpace.Local;
        mainPlume.maxParticles = 24;
        var emPlume = _flashPlumePS.emission;
        emPlume.rateOverTime = 0;
        var shapePlume = _flashPlumePS.shape;
        shapePlume.shapeType = ParticleSystemShapeType.Cone;
        shapePlume.angle = 8f;
        shapePlume.radius = 0.01f;
        var rendPlume = plumeObj.GetComponent<ParticleSystemRenderer>();
        rendPlume.material = muzzleMat;
        rendPlume.renderMode = ParticleSystemRenderMode.Stretch;
        rendPlume.lengthScale = 3.8f;
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
        mainCross.startLifetime = new ParticleSystem.MinMaxCurve(0.05f, 0.08f);
        mainCross.startSpeed = new ParticleSystem.MinMaxCurve(20f, 32f);
        mainCross.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
        mainCross.startColor = new Color(5.5f, 2.8f, 0.6f, 1f);
        mainCross.simulationSpace = ParticleSystemSimulationSpace.Local;
        mainCross.maxParticles = 24;
        var emCross = _flashCrossPS.emission;
        emCross.rateOverTime = 0;
        var shapeCross = _flashCrossPS.shape;
        shapeCross.shapeType = ParticleSystemShapeType.Circle;
        shapeCross.radius = 0.02f;
        shapeCross.arc = 360f;
        var rendCross = crossObj.GetComponent<ParticleSystemRenderer>();
        rendCross.material = muzzleMat;
        rendCross.renderMode = ParticleSystemRenderMode.Stretch;
        rendCross.lengthScale = 2.8f;
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
        main2.startLifetime = new ParticleSystem.MinMaxCurve(0.14f, 0.32f);
        main2.startSpeed = new ParticleSystem.MinMaxCurve(30f, 55f);
        main2.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.12f);
        main2.startColor = new Color(7.0f, 4.5f, 1.5f, 1f);
        main2.gravityModifier = 2.2f;
        main2.simulationSpace = ParticleSystemSimulationSpace.World;
        main2.maxParticles = 120;
        var em2 = _flashSparksPS.emission;
        em2.rateOverTime = 0;
        var shape2 = _flashSparksPS.shape;
        shape2.shapeType = ParticleSystemShapeType.Cone;
        shape2.angle = 22f;
        shape2.radius = 0.02f;
        var rend2 = sparksObj.GetComponent<ParticleSystemRenderer>();
        rend2.material = sparkMat;
        rend2.renderMode = ParticleSystemRenderMode.Stretch;
        rend2.lengthScale = 4.5f;
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
        main3.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.8f);
        main3.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6.0f);
        main3.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
        main3.startColor = new Color(0.9f, 0.9f, 0.92f, 0.40f);
        main3.gravityModifier = -0.08f;
        main3.simulationSpace = ParticleSystemSimulationSpace.World;
        main3.maxParticles = 48;
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
            new GradientAlphaKey[] { new GradientAlphaKey(0.35f, 0f), new GradientAlphaKey(0f, 1f) }
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
        mainFire.startLifetime = new ParticleSystem.MinMaxCurve(0.14f, 0.25f);
        mainFire.startSpeed = new ParticleSystem.MinMaxCurve(18f, 35f);
        mainFire.startSize = new ParticleSystem.MinMaxCurve(0.55f, 1.1f);
        mainFire.startColor = new Color(7.5f, 3.5f, 0.4f, 1f);
        mainFire.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        mainFire.gravityModifier = -0.15f;
        mainFire.simulationSpace = ParticleSystemSimulationSpace.World;
        mainFire.maxParticles = 80;
        var emFire = _flashFirePS.emission;
        emFire.rateOverTime = 0;
        var shapeFire = _flashFirePS.shape;
        shapeFire.shapeType = ParticleSystemShapeType.Cone;
        shapeFire.angle = 18f;
        shapeFire.radius = 0.02f;

        var colFire = _flashFirePS.colorOverLifetime;
        colFire.enabled = true;
        Gradient fireGrad = new Gradient();
        fireGrad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(7.5f, 5.5f, 1.5f), 0.0f),
                new GradientColorKey(new Color(6.0f, 2.5f, 0.2f), 0.28f),
                new GradientColorKey(new Color(4.0f, 0.8f, 0.05f), 0.65f),
                new GradientColorKey(new Color(0.3f, 0.05f, 0.01f), 1.0f)
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
        fireSizeCurve.AddKey(0.35f, 1.35f);
        fireSizeCurve.AddKey(1.0f, 0.9f);
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
        mainTongue.startLifetime = new ParticleSystem.MinMaxCurve(0.07f, 0.11f);
        mainTongue.startSpeed = new ParticleSystem.MinMaxCurve(32f, 48f);
        mainTongue.startSize = new ParticleSystem.MinMaxCurve(0.40f, 0.65f);
        mainTongue.startColor = new Color(6.5f, 3.2f, 0.4f, 1f);
        mainTongue.simulationSpace = ParticleSystemSimulationSpace.Local;
        mainTongue.maxParticles = 24;
        var emTongue = _flashFireTonguePS.emission;
        emTongue.rateOverTime = 0;
        var shapeTongue = _flashFireTonguePS.shape;
        shapeTongue.shapeType = ParticleSystemShapeType.Cone;
        shapeTongue.angle = 6f;
        shapeTongue.radius = 0.01f;
        var rendTongue = tongueObj.GetComponent<ParticleSystemRenderer>();
        rendTongue.material = muzzleMat;
        rendTongue.renderMode = ParticleSystemRenderMode.Stretch;
        rendTongue.lengthScale = 3.8f;
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

        if (_flashCorePS != null)
        {
            var m = _flashCorePS.main;
            m.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
            m.startRotation = Random.Range(0f, Mathf.PI * 2f);
            _flashCorePS.Emit(1);
        }

        if (_flashFireTonguePS != null)
            _flashFireTonguePS.Emit(2);

        if (_flashLight != null)
        {
            if (_flashLightRoutine != null) StopCoroutine(_flashLightRoutine);
            _flashLightRoutine = StartCoroutine(FadeLightRoutine(6f, 0.05f));
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

        if (_gunSystem != null && _gunSystem.IsGunEquipped)
        {
            DrawAmmoHUD();
        }
    }

    private void DrawAmmoHUD()
    {
        if (!showAmmoHUD) return;

        float hudWidth = 240f;
        float hudHeight = 78f;
        float x = Screen.width - hudWidth - 25f;
        float y = Screen.height - hudHeight - 25f;

        if (_whitePixelTex == null)
        {
            _whitePixelTex = new Texture2D(1, 1);
            _whitePixelTex.SetPixel(0, 0, Color.white);
            _whitePixelTex.Apply();
        }

        // Background Box
        GUI.color = new Color(0.04f, 0.06f, 0.09f, 0.85f);
        GUI.DrawTexture(new Rect(x, y, hudWidth, hudHeight), _whitePixelTex);

        // Border outline
        GUI.color = new Color(0.35f, 0.42f, 0.55f, 0.6f);
        GUI.DrawTexture(new Rect(x, y, hudWidth, 1.5f), _whitePixelTex);
        GUI.DrawTexture(new Rect(x, y, 1.5f, hudHeight), _whitePixelTex);
        GUI.DrawTexture(new Rect(x + hudWidth - 1.5f, y, 1.5f, hudHeight), _whitePixelTex);
        GUI.DrawTexture(new Rect(x, y + hudHeight - 1.5f, hudWidth, 1.5f), _whitePixelTex);

        // Current Ammo Number
        GUIStyle bigStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 32,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };

        if (currentMagAmmo == 0)
        {
            bigStyle.normal.textColor = ((int)(Time.time * 4f) % 2 == 0) ? new Color(1f, 0.25f, 0.25f) : Color.white;
        }
        else if (currentMagAmmo <= 10)
        {
            bigStyle.normal.textColor = new Color(1f, 0.75f, 0.2f);
        }
        else
        {
            bigStyle.normal.textColor = Color.white;
        }

        GUI.color = Color.white;
        GUI.Label(new Rect(x + 16f, y + 6f, 75f, 38f), currentMagAmmo.ToString(), bigStyle);

        GUIStyle slashStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleLeft
        };
        slashStyle.normal.textColor = new Color(0.75f, 0.78f, 0.85f, 0.9f);
        GUI.Label(new Rect(x + 70f, y + 17f, 85f, 25f), $"/ {reserveAmmo}", slashStyle);

        GUIStyle subStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleRight
        };
        subStyle.normal.textColor = new Color(0.6f, 0.65f, 0.72f);
        GUI.Label(new Rect(x + 130f, y + 8f, 95f, 20f), "5.56 NATO", subStyle);

        // Status Line / Magazine Indicator
        GUIStyle statusStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };

        if (isChangingMagazine)
        {
            statusStyle.normal.textColor = new Color(0.3f, 0.85f, 1f);
            GUI.Label(new Rect(x + 16f, y + 48f, 215f, 22f), "CHANGING MAGAZINE... [M]", statusStyle);
        }
        else if (isReloading)
        {
            statusStyle.normal.textColor = new Color(1f, 0.85f, 0.25f);
            GUI.Label(new Rect(x + 16f, y + 48f, 215f, 22f), "RELOADING... [R]", statusStyle);
        }
        else if (hasSpareMagazine)
        {
            statusStyle.normal.textColor = new Color(0.35f, 0.95f, 0.45f);
            GUI.Label(new Rect(x + 16f, y + 48f, 215f, 22f), "● SPARE MAG READY [Press M]", statusStyle);
        }
        else if (currentMagAmmo == 0)
        {
            statusStyle.normal.textColor = new Color(1f, 0.35f, 0.35f);
            GUI.Label(new Rect(x + 16f, y + 48f, 215f, 22f), reserveAmmo > 0 ? "EMPTY! Press [R] to Reload" : "NO AMMO!", statusStyle);
        }
        else
        {
            statusStyle.normal.textColor = new Color(0.55f, 0.58f, 0.65f);
            GUI.Label(new Rect(x + 16f, y + 48f, 215f, 22f), "○ [R] Reload  •  [M] Change Mag", statusStyle);
        }

        // Notification Banner
        if (_hudMessageTimer > 0f && !string.IsNullOrEmpty(_hudMessage))
        {
            _hudMessageTimer -= Time.deltaTime;
            float notifW = 420f;
            float notifH = 36f;
            float nx = (Screen.width - notifW) * 0.5f;
            float ny = Screen.height * 0.74f;

            GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.90f);
            GUI.DrawTexture(new Rect(nx, ny, notifW, notifH), _whitePixelTex);

            GUI.color = new Color(0.4f, 0.5f, 0.7f, 0.7f);
            GUI.DrawTexture(new Rect(nx, ny, notifW, 1.5f), _whitePixelTex);
            GUI.DrawTexture(new Rect(nx, ny + notifH - 1.5f, notifW, 1.5f), _whitePixelTex);

            GUI.color = Color.white;
            GUIStyle notifStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            notifStyle.normal.textColor = new Color(1f, 0.88f, 0.35f);
            GUI.Label(new Rect(nx, ny, notifW, notifH), _hudMessage, notifStyle);
        }
    }

    /// <summary>
    /// R Key Reload Routine: Refills active magazine (up to 40) from reserve ammo.
    /// E.g. 0 / 120 -> 40 / 80.
    /// </summary>
    public System.Collections.IEnumerator ReloadRoutine()
    {
        isReloading = true;
        _hudMessage = "RELOADING...";
        _hudMessageTimer = reloadDuration;

        Vector3 muzzlePos = GetEffectiveBarrelPosition();

        // 1. Mag Out sound
        GameAudio.PlayMagOut(muzzlePos, _audioSource);
        yield return new WaitForSeconds(reloadDuration * 0.40f);

        // 2. Mag In sound
        GameAudio.PlayMagIn(muzzlePos, _audioSource);
        yield return new WaitForSeconds(reloadDuration * 0.35f);

        // 3. Bolt pull/cock
        GameAudio.PlayGunCock(muzzlePos, _audioSource);
        yield return new WaitForSeconds(reloadDuration * 0.25f);

        int needed = maxMagCapacity - currentMagAmmo;
        int toLoad = Mathf.Min(needed, reserveAmmo);
        reserveAmmo -= toLoad;
        currentMagAmmo += toLoad;

        _hudMessage = null;
        isReloading = false;
    }

    /// <summary>
    /// Gets the Transform representing the Player's Left Hand for magazine reload.
    /// </summary>
    public Transform GetLeftHandSocket()
    {
        if (leftHandSocket != null) return leftHandSocket;

        if (_playerController != null)
        {
            Animator anim = _playerController.GetComponent<Animator>();
            if (anim != null && anim.isHuman)
            {
                Transform bone = anim.GetBoneTransform(HumanBodyBones.LeftHand);
                if (bone != null)
                {
                    leftHandSocket = bone;
                    return bone;
                }
            }

            Transform[] all = _playerController.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == "LeftHandTorchPoint")
                {
                    leftHandSocket = all[i];
                    return all[i];
                }
                if (all[i].name == "LeftHand" || all[i].name == "mixamorig:LeftHand")
                {
                    leftHandSocket = all[i];
                    return all[i];
                }
            }
        }
        return null;
    }

    /// <summary>
    /// M Key Magazine Change Routine: Plays character reload animation,
    /// moves magazine to player's left hand during animation, and when inserted
    /// into the gun it disappears (receiver eats it), then fully restocks 120 bullets.
    /// </summary>
    public System.Collections.IEnumerator ChangeMagazineRoutine()
    {
        isChangingMagazine = true;
        _hudMessage = "CHANGING MAGAZINE...";
        _hudMessageTimer = magChangeDuration;

        Vector3 muzzlePos = GetEffectiveBarrelPosition();

        // Trigger character magazine change animation
        if (_playerController != null)
        {
            _playerController.TriggerMagazineChangeAnimation();
        }

        // If carried magazine doesn't exist yet but player has spare mag, ensure it exists
        if (_carriedMagObj == null && hasSpareMagazine)
        {
            EnsureHolsterMagazine();
        }

        // Phase 1: Hand pulls out old magazine (~0.40s)
        yield return new WaitForSeconds(0.40f);
        GameAudio.PlayMagOut(muzzlePos, _audioSource);

        if (_gunMagObj != null)
        {
            SpawnDroppedMagazine(_gunMagObj.transform.position, _gunMagObj.transform.rotation);
            _gunMagObj.SetActive(false);
            Destroy(_gunMagObj, 0.1f);
            _gunMagObj = null;
        }

        // Phase 2: Magazine player ke left hand mein attach hoti hai!
        Transform handT = GetLeftHandSocket();
        if (_carriedMagObj != null && handT != null)
        {
            _carriedMagObj.transform.SetParent(handT, false);
            _carriedMagObj.transform.localPosition = new Vector3(0.01f, 0.05f, 0.02f);
            _carriedMagObj.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            Vector3 worldScale = _userMagWorldScale != Vector3.zero ? _userMagWorldScale : _carriedMagObj.transform.lossyScale;
            Vector3 parentLossy = handT.lossyScale;
            _carriedMagObj.transform.localScale = new Vector3(
                parentLossy.x > 0.0001f ? worldScale.x / parentLossy.x : _userMagScale.x,
                parentLossy.y > 0.0001f ? worldScale.y / parentLossy.y : _userMagScale.y,
                parentLossy.z > 0.0001f ? worldScale.z / parentLossy.z : _userMagScale.z
            );
            _carriedMagObj.SetActive(true);
        }

        // Hand travels with magazine towards gun magwell (~0.65s)
        yield return new WaitForSeconds(0.65f);

        // Magazine gun mein insert hoti hai
        GameAudio.PlayMagIn(muzzlePos, _audioSource);

        // User requirement: "gun jane ke baad gayab ho jani chaiye"
        // Gun mein jaane ke baad magazine completely gayab (deactivate & destroy) ho jaati hai!
        if (_carriedMagObj != null)
        {
            _carriedMagObj.SetActive(false);
            Destroy(_carriedMagObj, 0.05f);
            _carriedMagObj = null;
        }

        if (_gunMagObj != null)
        {
            _gunMagObj.SetActive(false);
            Destroy(_gunMagObj, 0.05f);
            _gunMagObj = null;
        }

        // Phase 3: Cock bolt / charging handle (~0.45s)
        yield return new WaitForSeconds(0.45f);
        GameAudio.PlayGunCock(muzzlePos, _audioSource);

        // Reset ammo: 40 bullets active in gun + 120 bullets in reserve!
        currentMagAmmo = maxMagCapacity;
        reserveAmmo = maxReserveAmmo;
        hasSpareMagazine = false;

        _hudMessage = "MAGAZINE CHANGED! 120 BULLETS RESTOCKED";
        _hudMessageTimer = 2.5f;

        yield return new WaitForSeconds(0.30f);
        isChangingMagazine = false;
    }

    /// <summary>
    /// Gets the exact local scale from the assigned magazine prefab or the user's picked up object.
    /// </summary>
    public Vector3 GetPrefabScale()
    {
        if (magazinePrefab != null)
        {
            return magazinePrefab.transform.localScale;
        }
        if (_userMagScale != Vector3.zero)
        {
            return _userMagScale;
        }
        return Vector3.one;
    }

    /// <summary>
    /// Called when player picks up a 3D magazine in the world with 'E'.
    /// The magazine is placed on the empty object socket with the user's exact prefab size.
    /// </summary>
    public void PickUpSpareMagazine(GameObject pickupObj = null)
    {
        hasSpareMagazine = true;

        EnsureHolsterSocketExists();

        if (pickupObj != null)
        {
            // Store the user's exact prefab scale and template
            _userMagScale = pickupObj.transform.localScale;
            _userMagWorldScale = pickupObj.transform.lossyScale;
            _userMagTemplate = pickupObj;

            if (_carriedMagObj != null && _carriedMagObj != pickupObj)
            {
                Destroy(_carriedMagObj);
            }

            _carriedMagObj = pickupObj;
            _carriedMagObj.name = "Carried_M4Magazine";

            if (magazineHolsterSocket != null)
            {
                _carriedMagObj.transform.SetParent(magazineHolsterSocket, false);
                _carriedMagObj.transform.localPosition = Vector3.zero;
                _carriedMagObj.transform.localRotation = Quaternion.identity;

                // User requirement: Prefab mein jo visual size hai wahi size rehni chahiye!
                Vector3 parentLossy = magazineHolsterSocket.lossyScale;
                _carriedMagObj.transform.localScale = new Vector3(
                    parentLossy.x > 0.0001f ? _userMagWorldScale.x / parentLossy.x : _userMagScale.x,
                    parentLossy.y > 0.0001f ? _userMagWorldScale.y / parentLossy.y : _userMagScale.y,
                    parentLossy.z > 0.0001f ? _userMagWorldScale.z / parentLossy.z : _userMagScale.z
                );
            }

            _carriedMagObj.SetActive(true);
        }
        else
        {
            EnsureHolsterMagazine();
            if (_carriedMagObj != null)
            {
                _carriedMagObj.transform.localScale = GetPrefabScale();
                _carriedMagObj.SetActive(true);
            }
        }

        GameAudio.PlayEquip(transform.position);
        _hudMessage = "SPARE MAGAZINE COLLECTED! Press [M] to Change";
        _hudMessageTimer = 3.5f;
    }

    public void EnsureHolsterSocketExists()
    {
        if (magazineHolsterSocket == null && _gunSystem != null && _gunSystem.magazineHolsterSocket != null)
        {
            magazineHolsterSocket = _gunSystem.magazineHolsterSocket;
        }

        if (magazineHolsterSocket == null)
        {
            Transform playerT = transform;
            if (_playerController != null) playerT = _playerController.transform;

            Transform found = playerT.Find("MagazineHolsterPoint") 
                ?? playerT.Find("MagazineHolster")
                ?? playerT.Find("MagazineSocket");

            if (found == null)
            {
                Transform[] allChildren = playerT.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < allChildren.Length; i++)
                {
                    if (allChildren[i].name == "MagazineHolsterPoint" || allChildren[i].name == "MagazineHolster")
                    {
                        found = allChildren[i];
                        break;
                    }
                }
            }

            if (found == null)
            {
                GameObject socketObj = new GameObject("MagazineHolsterPoint");
                socketObj.transform.SetParent(playerT, false);
                socketObj.transform.localPosition = new Vector3(-0.1109f, 0.6418f, -0.0068f);
                socketObj.transform.localEulerAngles = new Vector3(15f, -10f, 0f);
                found = socketObj.transform;
            }
            magazineHolsterSocket = found;
            if (_gunSystem != null) _gunSystem.magazineHolsterSocket = found;
        }
    }

    public void EnsureHolsterMagazine()
    {
        EnsureHolsterSocketExists();

        Vector3 targetScale = GetPrefabScale();

        if (_carriedMagObj == null && magazineHolsterSocket != null)
        {
            _carriedMagObj = CreateMagazineModel("Carried_M4Magazine");
            _carriedMagObj.transform.SetParent(magazineHolsterSocket, false);
            _carriedMagObj.transform.localPosition = Vector3.zero;
            _carriedMagObj.transform.localRotation = Quaternion.identity;
            _carriedMagObj.transform.localScale = targetScale;
        }
        else if (_carriedMagObj != null)
        {
            _carriedMagObj.transform.localScale = targetScale;
        }
    }

    public void EnsureGunMagazineSocket()
    {
        if (gunMagazineSocket == null)
        {
            if (_gunSystem != null && _gunSystem.gun != null)
            {
                Transform gunT = _gunSystem.gun.transform;
                Transform found = gunT.Find("GunMagazineSocket") ?? gunT.Find("MagSocket") ?? gunT.Find("Magazine");
                if (found == null)
                {
                    Transform[] allChildren = gunT.GetComponentsInChildren<Transform>(true);
                    for (int i = 0; i < allChildren.Length; i++)
                    {
                        if (allChildren[i].name == "GunMagazineSocket" || allChildren[i].name == "MagSocket")
                        {
                            found = allChildren[i];
                            break;
                        }
                    }
                }

                if (found == null)
                {
                    GameObject magSock = new GameObject("GunMagazineSocket");
                    magSock.transform.SetParent(gunT, false);
                    magSock.transform.localPosition = new Vector3(0f, -0.05f, 0.12f);
                    magSock.transform.localEulerAngles = Vector3.zero;
                    found = magSock.transform;
                }
                gunMagazineSocket = found;
            }
        }
    }

    public void EnsureGunMagazine()
    {
        if (_gunSystem == null || _gunSystem.gun == null) return;

        EnsureGunMagazineSocket();

        Vector3 targetScale = GetPrefabScale();

        if (_gunMagObj == null && gunMagazineSocket != null)
        {
            _gunMagObj = CreateMagazineModel("Gun_ActiveMagazine");
            _gunMagObj.transform.SetParent(gunMagazineSocket, false);
            _gunMagObj.transform.localPosition = Vector3.zero;
            _gunMagObj.transform.localRotation = Quaternion.identity;
            _gunMagObj.transform.localScale = targetScale;
        }
        else if (_gunMagObj != null)
        {
            _gunMagObj.transform.localScale = targetScale;
        }
    }

    private GameObject CreateMagazineModel(string objName = "M4_Magazine")
    {
        // 1. User assigned 3D model / prefab in Inspector
        if (magazinePrefab != null)
        {
            GameObject inst = Instantiate(magazinePrefab);
            inst.name = objName;
            return inst;
        }

        // 2. Clone from the user's picked up magazine object
        if (_userMagTemplate != null)
        {
            GameObject inst = Instantiate(_userMagTemplate);
            inst.name = objName;
            return inst;
        }

        // 3. Fallback to 3D model FBX if present in project
#if UNITY_EDITOR
        GameObject res = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model/M4 Magazine/source/M4Mag.fbx");
        if (res != null)
        {
            GameObject inst = Instantiate(res);
            inst.name = objName;
            return inst;
        }
#endif

        // Procedural authentic magazine geometry (fallback only)
        GameObject magObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        magObj.name = objName;
        Collider c = magObj.GetComponent<Collider>();
        if (c != null) Destroy(c);

        Material mat = Resources.Load<Material>("Materials/M_M4Magazine");
        if (mat == null) mat = GetCasingMaterial();
        Renderer r = magObj.GetComponent<Renderer>();
        if (r != null && mat != null) r.sharedMaterial = mat;

        return magObj;
    }

    private void SpawnDroppedMagazine(Vector3 pos, Quaternion rot)
    {
        GameObject dropped = CreateMagazineModel("Dropped_EmptyMagazine");
        dropped.transform.SetParent(null);
        dropped.transform.position = pos;
        dropped.transform.rotation = rot;
        dropped.transform.localScale = GetPrefabScale();

        // Clean up any MagazinePickup scripts on dropped instance
        MagazinePickup[] pickups = dropped.GetComponentsInChildren<MagazinePickup>(true);
        for (int i = 0; i < pickups.Length; i++)
        {
            if (pickups[i] != null) Destroy(pickups[i]);
        }

        BoxCollider col = dropped.GetComponent<BoxCollider>();
        if (col == null) col = dropped.AddComponent<BoxCollider>();
        col.size = new Vector3(0.04f, 0.20f, 0.09f);
        col.isTrigger = false;
        col.enabled = true;

        Rigidbody rb = dropped.AddComponent<Rigidbody>();
        if (rb == null) rb = dropped.AddComponent<Rigidbody>();
        rb.mass = 0.45f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        if (_playerCollider != null)
            Physics.IgnoreCollision(col, _playerCollider, true);

        Vector3 dropForce = -transform.up * 1.5f + Random.insideUnitSphere * 0.35f;
        rb.linearVelocity = dropForce;
        rb.angularVelocity = new Vector3(Random.Range(-5f, 5f), Random.Range(-5f, 5f), Random.Range(-5f, 5f));

        Destroy(dropped, 4.0f);
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
