using System.Collections;
using UnityEngine;

/// <summary>
/// BoatSystem — Complete self-contained boat boarding, driving and exiting system.
/// No separate trigger object required — proximity is calculated directly from boatNearPoint.
///
/// ── FLOW ─────────────────────────────────────────────────────────────────────
///  EnterBoat:
///    1. Player approaches boatNearPoint (within enterDistance).
///    2. Screen shows "Press B to Board Boat" prompt.
///    3. On pressing 'B', player moves directly to boatSeatPoint.
///    4. SitDown animation plays, then WASD paddling is enabled.
///
///  ExitBoat (G press):
///    1. StandUp animation plays.
///    2. Player moves directly to boatNearDownPoint and regains control.
/// </summary>
public class BoatSystem : MonoBehaviour
{
    public static BoatSystem Instance { get; private set; }

    // ─── Inspector: Mount Points ───────────────────────────────────────────────
    [Header("Boat Mount Points")]
    [Tooltip("Boat ke side pe Empty Object — player is point ke paas aane par Press B dekhega (BoatNear)")]
    public Transform boatNearPoint;

    [Tooltip("Boat se utarte waqt player kahan land karega (BoatNearDown)")]
    public Transform boatNearDownPoint;

    [Tooltip("Boat ki seating — player yahan baithta hai (BoatSeat)")]
    public Transform boatSeatPoint;

    [Tooltip("Boat Camera Point — Jab player boat me baithega to camera is point par aa jayega (Inspector me assign karein)")]
    public Transform boatCameraPoint;

    [Header("Hull Size")]
    [Tooltip("Boat ke visual model ka parent (prefab me Collada_visual_scene_group).")]
    public Transform boatModelRoot;

    [Tooltip("Hull size multiplier: X = width, Y = height, Z = length.")]
    public Vector3 boatModelScale = Vector3.one;

    // ─── Inspector: Proximity & UI ─────────────────────────────────────────────
    [Header("Proximity & Interaction")]
    [Tooltip("Kitni doori (meters) ke andar player aane par Press B prompt dikhega")]
    public float enterDistance = 9.0f;

    [Tooltip("Optional: Scene ka OarPickup script (agar oar collect karwana ho)")]
    public OarPickup oarPickup;

    [Tooltip("Agar true ho to oar pickup zaroori hoga, false hone par direct boat chala sakta hai")]
    public bool requireOar = true;

    [Tooltip("Optional Canvas UI: 'Press B to Drive Boat'")]
    public GameObject driveBoatUI;

    [Tooltip("Optional Canvas UI: 'Collect the Oar First'")]
    public GameObject collectOarUI;

    // ─── Inspector: Boat Movement & Physics ─────────────────────────────────────
    [Header("Boat Movement")]
    [Tooltip("Boat ka Rigidbody — null hone par script khud dhoondh legi")]
    public Rigidbody boatRigidbody;

    [Tooltip("Paddle karte time forward speed (m/s)")]
    public float boatForwardSpeed = 15f;

    [Tooltip("Boat steering sensitivity (mouse/trackpad/keys)")]
    public float boatTurnSpeed = 110f;

    [Tooltip("Boat kitni smoothly rukti hai (0=instant, 1=slow)")]
    [Range(0f, 0.99f)]
    public float boatDecelerationSmoothing = 0.85f;

    [Header("AAA Polish (Physics & Feel)")]
    public float accelerationRate = 2f;
    public float turnSmoothing = 5f;

    [Header("Realistic Water Wave Physics")]
    [Tooltip("Laher ki unchai / wave amplitude (meters) — jitna zyada, utna bada wave bobbing")]
    [Range(0.05f, 1.5f)]
    public float waveHeight = 0.55f;

    [Tooltip("Laher ki raftaar / wave travel speed")]
    [Range(0.2f, 5.0f)]
    public float waveSpeed = 1.6f;

    [Tooltip("Laher ki frequency / density")]
    [Range(0.2f, 3.0f)]
    public float waveFrequency = 1.0f;

    [Tooltip("Boat aage-peeche kitni jhukegi laher ke sath (pitch strength)")]
    [Range(0.2f, 3.0f)]
    public float wavePitchStrength = 0.75f;

    [Tooltip("Boat daayein-baayein kitni dolega laher ke sath (roll strength)")]
    [Range(0.2f, 3.0f)]
    public float waveRollStrength = 0.85f;

    [Tooltip("Fast speed me boat ka bow (aage ka hissa) kitna upar uthega (planing angle in degrees)")]
    [Range(0f, 8.0f)]
    public float bowPlaningRise = 3.0f;

    [Tooltip("Bow trim: boat ka aage ka hissa naturally thoda upar rahega taaki laherein andar na ghusein")]
    [Range(0f, 6.0f)]
    public float bowTrimAngle = 2.0f;

    [Tooltip("Turning ke time boat kitni lean / bank karegi (degrees)")]
    [Range(0f, 15.0f)]
    public float turnBankAngle = 5.5f;

    [Tooltip("Hull buoyancy responsiveness damping (0.05=instant floating on waves, 0.2=sluggish)")]
    [Range(0.04f, 0.35f)]
    public float buoyancyDamping = 0.08f;

    [Tooltip("Clearance in meters; the lowest point of the hull stays above the highest sampled wave.")]
    [Min(0f)]
    public float boatWaterlineOffset = 0.2f;

    [Header("No Water Inside Boat (Hull Depth Mask)")]
    [Tooltip("Invisible depth mask jo boat ke andar paani dikhne se rokta hai")]
    public bool enableHullDepthMask = true;
    [Tooltip("Exact hull depth-mask size (Width, Height, Length) in meters; independent of boat scale")]
    public Vector3 hullMaskSize = new Vector3(1.60f, 1.70f, 4.30f);
    [Tooltip("Interior cavity position offset (X, Y, Z)")]
    public Vector3 hullMaskOffset = new Vector3(0f, -0.15f, 0.20f);
    [Tooltip("Depth-only material used to hide water rendered inside the hull.")]
    public Material hullDepthMaskMaterial;

    public enum AxisDirection { Z_Forward, Y_Up, X_Right, Negative_Z, Negative_Y, Negative_X }
    
    [Tooltip("Boat model ka forward axis (Blender models ke liye Negative_Z ya Y_Up)")]
    public AxisDirection forwardMovementAxis = AxisDirection.Negative_Z;

    [Tooltip("Boat me baithne ke baad boat local axes pe kitna ghiske")]
    public Vector3 boatEnterShiftOffset = new Vector3(-0.5f, 0f, 0f);

    // ─── Inspector: Animation State Names ──────────────────────────────────────
    [Header("Animation State Names")]
    public string paddleStateName   = "Paddle";
    public string sitDownStateName  = "SitDown";
    public string sitUpStateName    = "StandUp";

    [Header("Animation Trigger/Bool Names")]
    public string sitDownTriggerName = "SitDown";
    public string sitUpTriggerName   = "StandUp";
    public string paddlingBoolName   = "IsPaddling";
    public string seatIdleBoolName   = "SeatIdle";

    [Header("Timings")]
    public float idleAfterSitUpDuration = 0.35f;

    [Header("Camera Settings (Boat Cinematic)")]
    public float camDistance = 4.0f;
    public Vector3 camOffset = new Vector3(0f, 1.6f, 0f);
    public float camPitch    = 18f;
    public float camYaw      = 0f;

    [Tooltip("Camera kitna peeche stern ke, jaise boat ke andar se aage dekh rahe ho")]
    public float camBehindStern = 1.35f;
    [Tooltip("Deck ke center se camera kitna upar")]
    public float camHeightAboveDeck = 1.5f;
    [Tooltip("Nazar boat ke bow ki taraf kitni aage")]
    [Range(0.2f, 1f)]
    public float camLookAhead = 0.72f;
    public float camLookHeight = 0.2f;

    // ─── Private State ─────────────────────────────────────────────────────────
    private bool _isOccupied        = false;
    private bool _isPaddling        = false;
    private bool _canPaddle         = false;
    private bool _isEntering        = false;
    private bool _isExiting         = false;
    private bool _playerNear        = false;

    private PlayerController _cachedPlayer;
    private Transform        _playerTransform;
    private CharacterController _playerCC;
    private Animator         _playerAnimator;
    private Transform        _runtimeMount;

    private float _currentSpeed = 0f;
    private float _currentTurn  = 0f;
    private float _waterRestY;
    private bool  _hasWaterRest = false;
    private float _nextSplashTime;
    private float _nextWaterLapTime;
    private float _boatYaw;
    private Quaternion _axisFix = Quaternion.identity;
    private bool _poseReady;
    private float _heaveVel;
    private float _smoothedPitch;
    private float _pitchVel;
    private float _smoothedRoll;
    private float _rollVel;
    private bool _snapBoatCam;
    private bool _cameraOnBoat;
    private bool _hasSnappedWaterline;
    private Renderer[] _hullRenderers;
    private GameObject _hullMaskObj;
    private Mesh _hullMaskMesh;

    private static readonly Vector2[] HullMaskOutline =
    {
        new Vector2(0f, 0.5f),
        new Vector2(0.28f, 0.30f),
        new Vector2(0.5f, 0.02f),
        new Vector2(0.40f, -0.32f),
        new Vector2(0.22f, -0.5f),
        new Vector2(-0.22f, -0.5f),
        new Vector2(-0.40f, -0.32f),
        new Vector2(-0.5f, 0.02f),
        new Vector2(-0.28f, 0.30f)
    };

    private int _hashSitDown;
    private int _hashSitUp;
    private int _hashIsPaddling;
    private int _hashSeatIdle;

    public bool IsOccupied => _isOccupied;
    public bool IsPaddling => _isPaddling;

    // ─── Unity Lifecycle ───────────────────────────────────────────────────────
    private void Awake()
    {
        Instance = this;
        if (boatRigidbody == null)
            boatRigidbody = GetComponent<Rigidbody>();

        // Boat ka 3000x scale player ki animation ko affect na kare, isliye 1x unscaled mount
        GameObject mountObj = new GameObject("PlayerBoatMount_" + gameObject.name);
        _runtimeMount = mountObj.transform;

        _hashSitDown     = Animator.StringToHash(sitDownTriggerName);
        _hashSitUp       = Animator.StringToHash(sitUpTriggerName);
        _hashIsPaddling  = Animator.StringToHash(paddlingBoolName);
        _hashSeatIdle    = Animator.StringToHash(seatIdleBoolName);
        ApplyBoatModelScale();
        _hullRenderers = GetComponentsInChildren<Renderer>(true);
        CaptureBoatAxes();
    }

    private void OnValidate()
    {
        boatModelScale.x = Mathf.Max(0.01f, boatModelScale.x);
        boatModelScale.y = Mathf.Max(0.01f, boatModelScale.y);
        boatModelScale.z = Mathf.Max(0.01f, boatModelScale.z);
        ApplyBoatModelScale();
    }

    private void ApplyBoatModelScale()
    {
        if (boatModelRoot != null)
            boatModelRoot.localScale = new Vector3(
                boatModelScale.x,
                boatModelScale.z,
                boatModelScale.y);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_hullMaskObj != null)
        {
            Destroy(_hullMaskObj);
            _hullMaskObj = null;
        }
        if (_hullMaskMesh != null)
        {
            Destroy(_hullMaskMesh);
            _hullMaskMesh = null;
        }
    }

    private void OnDisable()
    {
        if (_hullMaskObj != null)
            _hullMaskObj.SetActive(false);
    }

    private void OnEnable()
    {
        if (_hullMaskObj != null && enableHullDepthMask)
            _hullMaskObj.SetActive(true);
    }

    private void Start()
    {
        if (oarPickup == null)
            oarPickup = OarPickup.Instance ?? FindFirstObjectByType<OarPickup>();

        if (boatCameraPoint == null)
        {
            Transform found = transform.Find("BoatCameraPoint") ?? transform.Find("boatCameraPoint");
            if (found != null) boatCameraPoint = found;
        }

        EnsureHullDepthMask();
        HideUI();
    }

    private void LateUpdate()
    {
        if (_isOccupied && _runtimeMount != null && boatSeatPoint != null)
        {
            _runtimeMount.position = boatSeatPoint.position;
            Vector3 bFwd = HeadingForward();
            if (bFwd.sqrMagnitude > 0.001f)
                _runtimeMount.rotation = Quaternion.LookRotation(bFwd, Vector3.up);

            if (_cameraOnBoat && !_isExiting)
                UpdateSeatedCamera(false);

            if (_playerTransform != null && _playerTransform.parent == _runtimeMount)
            {
                _playerTransform.localPosition = Vector3.zero;
                _playerTransform.localRotation = Quaternion.identity;
            }
        }
    }

    private void Update()
    {
        if (_isOccupied)
        {
            if (_isExiting)
            {
                ApplyWaterPose(0f);
                return;
            }

            if (_canPaddle)
            {
                HandleBoatMovement();
                HandleExitInput();
            }

            float speed01 = _canPaddle
                ? Mathf.Clamp01(Mathf.Abs(_currentSpeed) / Mathf.Max(0.1f, boatForwardSpeed))
                : 0f;
            ApplyWaterPose(speed01);

            if (_cameraOnBoat && _snapBoatCam)
            {
                UpdateSeatedCamera(true);
                _snapBoatCam = false;
            }
            return;
        }

        if (!_isOccupied && !_isEntering && !_isExiting)
        {
            ApplyWaterPose(0f);
            HandleProximityCheck();
        }
    }

    // ─── Proximity Detection & UI ──────────────────────────────────────────────
    private float _oarWarningTimer = 0f;
    private bool _triggerReportedNear = false;

    public void SetPlayerNear(bool near)
    {
        _triggerReportedNear = near;
    }

    private bool IsPlayerNearBoat()
    {
        if (_cachedPlayer == null) return false;
        if (_triggerReportedNear) return true;

        Vector3 playerPos = _cachedPlayer.transform.position;
        Transform nearPoint = boatNearPoint != null ? boatNearPoint : transform;

        // 1. Check Collider trigger bounds on boatNearPoint if present
        Collider nearCol = nearPoint.GetComponent<Collider>();
        if (nearCol != null)
        {
            if (nearCol.bounds.Contains(playerPos) || nearCol.bounds.SqrDistance(playerPos) < 25f)
                return true;
        }

        // 2. Horizontal (XZ) distance check strictly ignoring Y height differences
        Vector3 targetPos = nearPoint.position;
        float dx = playerPos.x - targetPos.x;
        float dz = playerPos.z - targetPos.z;
        float horizontalDist = Mathf.Sqrt(dx * dx + dz * dz);
        float verticalDist = Mathf.Abs(playerPos.y - targetPos.y);

        if (horizontalDist <= enterDistance && verticalDist < 12.0f)
            return true;

        // 3. Fallback check relative to Boat root transform
        float rootDx = playerPos.x - transform.position.x;
        float rootDz = playerPos.z - transform.position.z;
        float rootHorizDist = Mathf.Sqrt(rootDx * rootDx + rootDz * rootDz);
        if (rootHorizDist <= (enterDistance + 4.0f) && verticalDist < 12.0f)
            return true;

        return false;
    }

    private IEnumerator FlashOarWarning()
    {
        _oarWarningTimer = 2.5f;
        while (_oarWarningTimer > 0f)
        {
            _oarWarningTimer -= Time.deltaTime;
            yield return null;
        }
    }

    private void HandleProximityCheck()
    {
        if (_cachedPlayer == null)
        {
            _cachedPlayer = PlayerController.Instance ?? FindFirstObjectByType<PlayerController>();
            if (_cachedPlayer == null)
            {
                GameObject pObj = GameObject.FindGameObjectWithTag("Player");
                if (pObj != null) _cachedPlayer = pObj.GetComponent<PlayerController>();
            }
        }

        if (_cachedPlayer == null) return;

        // Auto-find oarPickup if missing
        if (oarPickup == null)
            oarPickup = OarPickup.Instance ?? FindFirstObjectByType<OarPickup>();

        bool inRange = IsPlayerNearBoat();

        // Only allow boarding if oar has been collected
        bool hasOar = !requireOar || (oarPickup != null && oarPickup.IsPickedUp);

        if (inRange && !_cachedPlayer.IsDead && !_cachedPlayer.isFrozen && !_cachedPlayer.IsSequenceRunning)
        {
            _playerNear = true;

            // Keep old raw canvas UI inactive so golden-yellow HUD box is used
            if (driveBoatUI != null)  driveBoatUI.SetActive(false);
            if (collectOarUI != null) collectOarUI.SetActive(false);

            if (Input.GetKeyDown(KeyCode.B))
            {
                if (!hasOar)
                {
                    Debug.Log("<color=#FFAA00><b>[BoatSystem]</b></color> Please collect the oar first!");
                    StopCoroutine(nameof(FlashOarWarning));
                    StartCoroutine(nameof(FlashOarWarning));
                    return;
                }

                HideUI();
                _playerNear = false;
                EnterBoat(_cachedPlayer);
            }
        }
        else
        {
            if (_playerNear)
            {
                _playerNear = false;
                HideUI();
            }
        }
    }

    private void HideUI()
    {
        if (driveBoatUI != null)  driveBoatUI.SetActive(false);
        if (collectOarUI != null) collectOarUI.SetActive(false);
    }

    private void OnGUI()
    {
        if (_isEntering || _isExiting) return;

        if (_isOccupied)
        {
            if (!_isPaddling)
                GameHUDPrompt.DrawPromptBox("[W/S] Paddle Forward / Back   |   [A/D] Steer   |   [G] Exit Boat");
            return;
        }

        if (!_playerNear) return;

        bool hasOar = !requireOar || (oarPickup != null && oarPickup.IsPickedUp);

        string prompt;
        if (!hasOar)
        {
            prompt = _oarWarningTimer > 0f 
                ? "⚠️ OAR REQUIRED! Please Collect The Oar First" 
                : "Collect The Oar First";
        }
        else
        {
            prompt = "[ B ] Board Boat";
        }

        GameHUDPrompt.DrawPromptBox(prompt);
    }

    // ─── Boat Movement (Drive / Steer) ─────────────────────────────────────────
    private void HandleBoatMovement()
    {
        float v = Input.GetAxis("Vertical");   // W/S
        float h = Input.GetAxis("Horizontal"); // A/D
        float mouseX = Input.GetAxis("Mouse X");

        bool pressing = Mathf.Abs(v) > 0.05f || Mathf.Abs(h) > 0.05f;

        // Paddling animation toggle on WASD input
        if (pressing && !_isPaddling)
        {
            _isPaddling = true;
            if (_playerAnimator != null)
                _playerAnimator.SetBool(_hashIsPaddling, true);
        }
        else if (!pressing && _isPaddling)
        {
            _isPaddling = false;
            if (_playerAnimator != null)
                _playerAnimator.SetBool(_hashIsPaddling, false);
        }

        // Steering (Smooth Turn) — yaw is applied in ApplyWaterPose so the hull axis stays upright
        float targetTurn = mouseX + (h * 2f);
        _currentTurn = Mathf.Lerp(_currentTurn, targetTurn, Time.deltaTime * turnSmoothing);
        if (Mathf.Abs(_currentTurn) > 0.01f)
            _boatYaw += _currentTurn * boatTurnSpeed * Time.deltaTime;

        // Acceleration
        float targetSpeed = pressing ? (v * boatForwardSpeed) : 0f;
        float accRate = pressing ? accelerationRate : (1f - boatDecelerationSmoothing) * 10f;
        _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed, Time.deltaTime * accRate);

        Vector3 boatFwd = HeadingForward();
        Vector3 deltaMove = boatFwd * _currentSpeed * Time.deltaTime;
        transform.position += deltaMove;

        if (pressing && Time.time >= _nextSplashTime)
        {
            _nextSplashTime = Time.time + 0.45f;
            VFXUtil.WaterSplash(transform.position + boatFwd * 0.8f, 0.7f);
            GameAudio.PlayPaddle(transform.position);
        }

        // Ambient boat hull water wave & lap sound while moving across the river
        if (Mathf.Abs(_currentSpeed) > 0.8f && Time.time >= _nextWaterLapTime)
        {
            _nextWaterLapTime = Time.time + 0.95f;
            GameAudio.PlayBoatWaterLap(transform.position, null, 0.35f);
        }

        if (boatRigidbody != null)
        {
            boatRigidbody.position = transform.position;
            boatRigidbody.rotation = transform.rotation;
        }
    }

    private void HandleExitInput()
    {
        if (Input.GetKeyDown(KeyCode.G) && !_isExiting)
        {
            StartCoroutine(ExitBoatSequence());
        }
    }

    // ─── Enter Boat Sequence ───────────────────────────────────────────────────
    public void EnterBoat(PlayerController player)
    {
        if (_isOccupied || _isEntering) return;
        StartCoroutine(EnterBoatSequence(player));
    }

    private IEnumerator EnterBoatSequence(PlayerController player)
    {
        _isEntering = true;
        _isOccupied = true;
        HideUI();

        _cachedPlayer    = player;
        _playerTransform = player.transform;
        _playerCC        = player.GetComponent<CharacterController>();
        _playerAnimator  = player.GetComponentInChildren<Animator>();

        // Freeze player locomotion and disable the CharacterController before teleporting.
        player.SetFrozen(true, lockWorldPosition: false);
        if (_playerCC != null) _playerCC.enabled = false;

        Transform seatTarget = boatSeatPoint != null ? boatSeatPoint : transform;
        Vector3 boatFwd = HeadingForward();
        if (boatFwd.sqrMagnitude < 0.001f) boatFwd = Vector3.forward;
        Quaternion uprightSeatRot = Quaternion.LookRotation(boatFwd, Vector3.up);

        _runtimeMount.position = seatTarget.position;
        _runtimeMount.rotation = uprightSeatRot;
        _playerTransform.SetParent(_runtimeMount);
        _playerTransform.localPosition = Vector3.zero;
        _playerTransform.localRotation = Quaternion.identity;

        _cameraOnBoat = true;
        _snapBoatCam = true;
        UpdateSeatedCamera(true);

        if (_playerAnimator != null)
        {
            _playerAnimator.ResetTrigger(_hashSitDown);
            _playerAnimator.SetTrigger(_hashSitDown);
            _playerAnimator.CrossFadeInFixedTime(sitDownStateName, 0.15f, 0, 0f);
        }

        yield return new WaitForSeconds(0.6f);

        if (_playerAnimator != null)
            _playerAnimator.SetBool(_hashSeatIdle, true);

        _canPaddle  = true;
        _isEntering = false;
        UpdateSeatedCamera(true);
    }

    // ─── Exit Boat Sequence ────────────────────────────────────────────────────
    private IEnumerator ExitBoatSequence()
    {
        _isExiting  = true;
        _canPaddle  = false;
        _cameraOnBoat = false;

        if (boatRigidbody != null)
            boatRigidbody.linearVelocity = Vector3.zero;

        if (_isPaddling)
        {
            _isPaddling = false;
            if (_playerAnimator != null)
                _playerAnimator.SetBool(_hashIsPaddling, false);
        }

        if (_playerAnimator != null)
        {
            _playerAnimator.SetBool(_hashSeatIdle, false);
            _playerAnimator.ResetTrigger(_hashSitUp);
            _playerAnimator.SetTrigger(_hashSitUp);
            _playerAnimator.CrossFadeInFixedTime(sitUpStateName, 0.15f, 0, 0f);
        }

        yield return new WaitForSeconds(Mathf.Max(0.5f, idleAfterSitUpDuration));

        Transform exitTarget = boatNearDownPoint != null ? boatNearDownPoint : (boatNearPoint != null ? boatNearPoint : transform);
        _playerTransform.SetParent(null);

        Vector3 exitFwd = exitTarget.forward;
        exitFwd.y = 0f;
        Quaternion exitRot = (exitFwd.sqrMagnitude > 0.001f)
            ? Quaternion.LookRotation(exitFwd.normalized, Vector3.up)
            : _playerTransform.rotation;
        _playerTransform.SetPositionAndRotation(exitTarget.position, exitRot);

        if (Camera.main != null)
        {
            CameraController cam = Camera.main.GetComponent<CameraController>();
            if (cam != null)
                cam.ExitBoatMode(_playerTransform);
        }

        if (oarPickup == null)
            oarPickup = OarPickup.Instance ?? FindFirstObjectByType<OarPickup>();
        if (oarPickup != null && oarPickup.IsPickedUp)
        {
            Vector3 dropPos = exitTarget.position + exitTarget.right * 0.45f;
            oarPickup.DropOar(dropPos);
        }

        if (_playerCC != null) _playerCC.enabled = true;
        if (_cachedPlayer != null) _cachedPlayer.SetFrozen(false);

        _isOccupied = false;
        _isExiting  = false;
    }

    // ─── Motion Helpers ────────────────────────────────────────────────────────
    private IEnumerator SmoothShiftBoat(Vector3 localOffset, float duration)
    {
        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + transform.TransformDirection(localOffset);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            if (boatRigidbody != null)
                boatRigidbody.position = transform.position;
            yield return null;
        }
    }

    private void CaptureBoatAxes()
    {
        Vector3 fwd = GetBoatForward();
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        fwd.Normalize();
        _boatYaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
        Quaternion flat = Quaternion.LookRotation(fwd, Vector3.up);
        _axisFix = Quaternion.Inverse(flat) * transform.rotation;
        _poseReady = true;
        _waterRestY = GetWaterSurfaceY(transform.position);
        _hasWaterRest = true;
    }

    private Vector3 HeadingForward()
    {
        float rad = _boatYaw * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
    }

    private Bounds CalculateHullBounds()
    {
        bool any = false;
        Bounds b = new Bounds(transform.position, new Vector3(2.2f, 1.2f, 6f));
        if (_hullRenderers == null || _hullRenderers.Length == 0)
            _hullRenderers = GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < _hullRenderers.Length; i++)
        {
            Renderer r = _hullRenderers[i];
            if (r == null || !r.enabled) continue;
            if (r.bounds.size.sqrMagnitude > 2500f) continue;
            if (!any)
            {
                b = r.bounds;
                any = true;
            }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    private static float Support(Vector3 extents, Vector3 dir)
    {
        return Mathf.Abs(extents.x * dir.x) + Mathf.Abs(extents.y * dir.y) + Mathf.Abs(extents.z * dir.z);
    }

    /// <summary>
    /// Evaluates rich multi-harmonic procedural ocean/river wave elevation at world coordinates (x, z).
    /// Combining deep directional swell, diagonal chop, and surface ripples for a completely realistic feel.
    /// </summary>
    public float SampleWaveHeight(float x, float z, float t)
    {
        float freq = Mathf.Max(0.1f, waveFrequency);
        float spd = waveSpeed;
        float h = waveHeight;

        // Wave 1: Primary deep directional swell (major rolling wave)
        float phase1 = (x * 0.09f + z * 0.42f) * freq - t * (spd * 1.8f);
        float w1 = Mathf.Sin(phase1) * (h * 0.45f);

        // Wave 2: Secondary diagonal chop (creates natural undulating wave peaks & troughs)
        float phase2 = (x * 0.32f - z * 0.26f) * freq - t * (spd * 2.5f);
        float w2 = Mathf.Cos(phase2) * (h * 0.30f);

        // Wave 3: Tertiary wind ripples (fine fluid surface detail)
        float phase3 = (x * 0.65f + z * 0.55f) * freq - t * (spd * 3.6f);
        float w3 = Mathf.Sin(phase3) * (h * 0.15f);

        // Wave 4: Dynamic cross-ripple (prevents any repetitive loop)
        float phase4 = (x * 0.18f + z * 0.85f) * freq - t * (spd * 1.2f);
        float w4 = Mathf.Cos(phase4) * (h * 0.10f);

        return w1 + w2 + w3 + w4;
    }

    private Renderer _cachedWaterRenderer;

    private float GetWaterSurfaceY(Vector3 worldPos)
    {
        // 1. Raycast downwards & upwards to find water surface under the boat (handles slopes, rivers, rapids)
        Ray ray = new Ray(worldPos + Vector3.up * 25f, Vector3.down);
        RaycastHit[] hits = Physics.RaycastAll(ray, 50f, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider != null && hits[i].collider.gameObject.name.ToLower().Contains("water"))
            {
                return hits[i].point.y;
            }
        }

        // 2. Fallback to water renderer bounds.max.y
        if (_cachedWaterRenderer == null)
        {
            GameObject waterGo = GameObject.Find("water") ?? GameObject.Find("Water");
            if (waterGo != null) _cachedWaterRenderer = waterGo.GetComponent<Renderer>();
        }

        if (_cachedWaterRenderer != null)
        {
            return _cachedWaterRenderer.bounds.max.y;
        }

        return _waterRestY != 0f ? _waterRestY : -4.70f;
    }

    private void ApplyWaterPose(float speed01)
    {
        if (!_poseReady) CaptureBoatAxes();

        if (!_hasWaterRest)
        {
            _waterRestY = GetWaterSurfaceY(transform.position);
            _hasWaterRest = true;
        }

        Vector3 p = transform.position;
        Vector3 boatFwd = HeadingForward();
        Vector3 boatRight = new Vector3(boatFwd.z, 0f, -boatFwd.x);

        float t = Time.time;

        // Sample at the visual hull edges so buoyancy follows inspector hull scaling.
        Bounds hullBounds = CalculateHullBounds();
        float halfLength = Mathf.Max(0.2f, Support(hullBounds.extents, boatFwd));
        float halfWidth = Mathf.Max(0.2f, Support(hullBounds.extents, boatRight));

        Vector3 bowPos = p + boatFwd * halfLength;
        Vector3 sternPos = p - boatFwd * halfLength;
        Vector3 portPos = p - boatRight * halfWidth;
        Vector3 starPos = p + boatRight * halfWidth;

        float bowWave = SampleWaveHeight(bowPos.x, bowPos.z, t);
        float sternWave = SampleWaveHeight(sternPos.x, sternPos.z, t);
        float portWave = SampleWaveHeight(portPos.x, portPos.z, t);
        float starWave = SampleWaveHeight(starPos.x, starPos.z, t);

        // The hull heaves with the wave at its center; edge samples control pitch and roll.
        float centerWave = SampleWaveHeight(p.x, p.z, t);

        // Dynamic speed effects: hydro-planing lift + speed chop
        float hydroLift = speed01 * 0.14f;
        float speedChop = speed01 > 0.05f ? Mathf.Sin(t * 14f) * (0.04f * speed01) : 0f;

        float baseSurfaceY = GetWaterSurfaceY(p);

        // 2. Pitch (Front-to-back wave rocking + bow planing rise + natural bow trim)
        float wavePitch = Mathf.Atan2(bowWave - sternWave, halfLength * 2f) * Mathf.Rad2Deg * wavePitchStrength;
        wavePitch = Mathf.Clamp(wavePitch, -4.5f, 5.5f); // Prevent nose from plunging underwater
        float bowRise = speed01 * bowPlaningRise; // Lift nose up when moving fast
        float targetPitch = wavePitch + bowRise + bowTrimAngle;
        _smoothedPitch = Mathf.SmoothDampAngle(_smoothedPitch, targetPitch, ref _pitchVel, 0.10f);

        // 3. Roll (Left-to-right wave rocking + centrifugal turn banking)
        float waveRoll = Mathf.Atan2(starWave - portWave, halfWidth * 2f) * Mathf.Rad2Deg * waveRollStrength;
        waveRoll = Mathf.Clamp(waveRoll, -6.0f, 6.0f);
        float bankLean = -_currentTurn * turnBankAngle * speed01; // Lean into turns
        float targetRoll = waveRoll + bankLean;
        _smoothedRoll = Mathf.SmoothDampAngle(_smoothedRoll, targetRoll, ref _rollVel, 0.12f);

        // 4. Orientation: Yaw heading + tilt + model axis protection
        Quaternion yawRot = Quaternion.Euler(0f, _boatYaw, 0f);
        Quaternion tiltRot = Quaternion.Euler(_smoothedPitch, 0f, -_smoothedRoll);
        Quaternion targetRot = yawRot * tiltRot * _axisFix;

        float tiltFollow = Mathf.Lerp(6.0f, 14.0f, speed01);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-tiltFollow * Time.deltaTime));

        float hullBottomOffset = CalculateHullBounds().min.y - transform.position.y;
        float targetY = baseSurfaceY + centerWave + hydroLift + speedChop +
                        boatWaterlineOffset - hullBottomOffset;

        float damp = Mathf.Lerp(buoyancyDamping, buoyancyDamping * 0.55f, speed01);
        Vector3 floatingPosition = transform.position;
        if (!_hasSnappedWaterline)
        {
            floatingPosition.y = targetY;
            _heaveVel = 0f;
            _hasSnappedWaterline = true;
        }
        else
        {
            floatingPosition.y = Mathf.SmoothDamp(floatingPosition.y, targetY, ref _heaveVel, damp);
        }
        transform.position = floatingPosition;

        // 5. Update Hull Depth Mask so zero water renders inside the boat
        UpdateHullDepthMask(baseSurfaceY + centerWave);

        if (boatRigidbody != null)
        {
            boatRigidbody.position = transform.position;
            boatRigidbody.rotation = transform.rotation;
        }
    }

    private void UpdateSeatedCamera(bool snap)
    {
        Camera camGo = Camera.main;
        if (camGo == null) return;
        CameraController cam = camGo.GetComponent<CameraController>();
        if (cam == null) return;

        Vector3 camPos;
        Quaternion camRot;

        if (boatCameraPoint != null)
        {
            camPos = boatCameraPoint.position;
            // Agar boatCameraPoint rotated hai to uski rotation le, otherwise boat heading look
            if (boatCameraPoint.rotation != Quaternion.identity)
            {
                camRot = boatCameraPoint.rotation;
            }
            else
            {
                Vector3 fwd = HeadingForward();
                camRot = Quaternion.LookRotation(fwd + Vector3.down * 0.1f, Vector3.up);
            }
        }
        else
        {
            Vector3 fwd = HeadingForward();
            Bounds hull = CalculateHullBounds();
            Vector3 seat = boatSeatPoint != null ? boatSeatPoint.position : transform.position;
            bool hullTrustworthy = (hull.center - seat).sqrMagnitude < 36f;

            Vector3 look;
            if (hullTrustworthy)
            {
                float reach = Mathf.Clamp(Support(hull.extents, fwd), 1.4f, 8f);
                Vector3 stern = hull.center - fwd * reach;
                Vector3 bow = hull.center + fwd * reach;
                camPos = stern - fwd * camBehindStern;
                camPos.y = hull.center.y + camHeightAboveDeck;
                look = Vector3.Lerp(hull.center, bow, camLookAhead);
                look.y = hull.center.y + camLookHeight;
            }
            else
            {
                camPos = seat - fwd * (camBehindStern + 2.4f);
                camPos.y = seat.y + camHeightAboveDeck;
                look = seat + fwd * 4.6f;
                look.y = seat.y + camLookHeight;
            }

            Vector3 dir = look - camPos;
            if (dir.sqrMagnitude < 0.01f) dir = fwd + Vector3.down * 0.15f;
            camRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        cam.SetBoatFraming(camPos, camRot, snap);
    }

    private Vector3 GetBoatForward()
    {
        switch (forwardMovementAxis)
        {
            case AxisDirection.Y_Up: return transform.up;
            case AxisDirection.X_Right: return transform.right;
            case AxisDirection.Negative_Z: return -transform.forward;
            case AxisDirection.Negative_Y: return -transform.up;
            case AxisDirection.Negative_X: return -transform.right;
            default: return transform.forward;
        }
    }

    // ─── Hull Depth Mask (Dry Boat Interior) ──────────────────────────────────

    private void EnsureHullDepthMask()
    {
        if (_hullMaskObj != null) return;

        _hullMaskObj = new GameObject("Boat_HullDepthMask");
        MeshFilter mf = _hullMaskObj.AddComponent<MeshFilter>();
        MeshRenderer mr = _hullMaskObj.AddComponent<MeshRenderer>();
        _hullMaskMesh = CreateHullMaskMesh();
        mf.sharedMesh = _hullMaskMesh;

        _hullMaskObj.transform.localScale = hullMaskSize;

        Material maskMat = hullDepthMaskMaterial;
#if UNITY_EDITOR
        if (maskMat == null)
            maskMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/HullDepthMask.mat");
#endif

        if (maskMat == null)
        {
            Shader maskShader = Shader.Find("Custom/Water/HullDepthMask");
            if (maskShader != null)
            {
                maskMat = new Material(maskShader);
                maskMat.name = "Runtime_HullDepthMask";
                maskMat.renderQueue = 2450;
            }
        }

        if (maskMat == null)
            Debug.LogError("[BoatSystem] Hull depth-mask material or shader is missing; water may render inside the boat.");

        if (maskMat != null) mr.sharedMaterial = maskMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    private static Mesh CreateHullMaskMesh()
    {
        int outlineCount = HullMaskOutline.Length;
        Vector3[] vertices = new Vector3[outlineCount * 2];
        for (int i = 0; i < outlineCount; i++)
        {
            Vector2 point = HullMaskOutline[i];
            vertices[i] = new Vector3(point.x, 0.5f, point.y);
            vertices[i + outlineCount] = new Vector3(point.x, -0.5f, point.y);
        }

        int[] triangles = new int[(outlineCount - 2) * 6 + outlineCount * 6];
        int triangleIndex = 0;
        for (int i = 1; i < outlineCount - 1; i++)
        {
            triangles[triangleIndex++] = 0;
            triangles[triangleIndex++] = i;
            triangles[triangleIndex++] = i + 1;
            triangles[triangleIndex++] = outlineCount;
            triangles[triangleIndex++] = outlineCount + i + 1;
            triangles[triangleIndex++] = outlineCount + i;
        }

        for (int i = 0; i < outlineCount; i++)
        {
            int next = (i + 1) % outlineCount;
            triangles[triangleIndex++] = i;
            triangles[triangleIndex++] = i + outlineCount;
            triangles[triangleIndex++] = next + outlineCount;
            triangles[triangleIndex++] = i;
            triangles[triangleIndex++] = next + outlineCount;
            triangles[triangleIndex++] = next;
        }

        Mesh mesh = new Mesh { name = "BoatHullDepthMaskMesh" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private Bounds CalculateHullDepthMaskBounds(float waterSurfaceY, Quaternion maskRotation)
    {
        Bounds hullBounds = CalculateHullBounds();
        Vector3 center = hullBounds.center + maskRotation * hullMaskOffset;
        float bottomY = waterSurfaceY + hullMaskOffset.y - hullMaskSize.y * 0.5f;
        center.y = bottomY + hullMaskSize.y * 0.5f;
        return new Bounds(center, hullMaskSize);
    }

    private void UpdateHullDepthMask(float waterSurfaceY)
    {
        if (!enableHullDepthMask)
        {
            if (_hullMaskObj != null && _hullMaskObj.activeSelf) _hullMaskObj.SetActive(false);
            return;
        }

        EnsureHullDepthMask();
        if (_hullMaskObj == null) return;
        if (!_hullMaskObj.activeSelf) _hullMaskObj.SetActive(true);

        Quaternion yawRot = Quaternion.Euler(0f, _boatYaw, 0f);
        Quaternion tiltRot = Quaternion.Euler(_smoothedPitch, 0f, -_smoothedRoll);
        Quaternion maskRot = yawRot * tiltRot;

        Bounds maskBounds = CalculateHullDepthMaskBounds(waterSurfaceY, maskRot);
        _hullMaskObj.transform.position = maskBounds.center;
        _hullMaskObj.transform.rotation = maskRot;
        _hullMaskObj.transform.localScale = maskBounds.size;
    }

    private void OnDrawGizmosSelected()
    {
        if (boatCameraPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(boatCameraPoint.position, 0.25f);
            Gizmos.DrawRay(boatCameraPoint.position, boatCameraPoint.forward * 1.5f);
        }
        else if (boatSeatPoint != null)
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
            Vector3 approxCam = boatSeatPoint.position - HeadingForward() * 3.5f + Vector3.up * 1.8f;
            Gizmos.DrawWireSphere(approxCam, 0.2f);
        }

        Vector3 boatFwd = HeadingForward();
        Vector3 boatRight = new Vector3(boatFwd.z, 0f, -boatFwd.x);
        Vector3 p = transform.position;

        // Draw waterline and wave sampling points
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireSphere(p + boatFwd * 2.2f, 0.15f); // Bow
        Gizmos.DrawWireSphere(p - boatFwd * 2.2f, 0.15f); // Stern
        Gizmos.DrawWireSphere(p - boatRight * 0.85f, 0.12f); // Port
        Gizmos.DrawWireSphere(p + boatRight * 0.85f, 0.12f); // Starboard

        // Draw Hull Depth Mask (Dry interior volume)
        if (enableHullDepthMask)
        {
            Gizmos.color = new Color(0.1f, 1f, 0.4f, 0.45f);
            Matrix4x4 oldMat = Gizmos.matrix;
            Quaternion yawRot = Quaternion.Euler(0f, _boatYaw, 0f);
            Quaternion tiltRot = Quaternion.Euler(_smoothedPitch, 0f, -_smoothedRoll);
            Quaternion maskRot = yawRot * tiltRot;
            Bounds maskBounds = CalculateHullDepthMaskBounds(GetWaterSurfaceY(p), maskRot);
            if (_hullMaskMesh != null)
                Gizmos.DrawWireMesh(_hullMaskMesh, maskBounds.center, maskRot, maskBounds.size);
            else
            {
                Gizmos.matrix = Matrix4x4.TRS(maskBounds.center, maskRot, maskBounds.size);
                Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
                Gizmos.matrix = oldMat;
            }
        }
    }
}
