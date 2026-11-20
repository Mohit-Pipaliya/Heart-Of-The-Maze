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
    private float _nextPaddleTime;
    private float _boatYaw;
    private Quaternion _axisFix = Quaternion.identity;
    private bool _snapBoatCam;
    private bool _cameraOnBoat;
    private Renderer[] _hullRenderers;

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
                return;

            if (_canPaddle)
            {
                HandleBoatMovement();
                HandleExitInput();
            }

            if (_cameraOnBoat && _snapBoatCam)
            {
                UpdateSeatedCamera(true);
                _snapBoatCam = false;
            }
            return;
        }

        if (!_isOccupied && !_isEntering && !_isExiting)
            HandleProximityCheck();
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

        // Steering (Smooth Turn)
        float targetTurn = mouseX + (h * 2f);
        _currentTurn = Mathf.Lerp(_currentTurn, targetTurn, Time.deltaTime * turnSmoothing);
        if (Mathf.Abs(_currentTurn) > 0.01f)
            _boatYaw += _currentTurn * boatTurnSpeed * Time.deltaTime;

        // Acceleration
        float targetSpeed = pressing ? (v * boatForwardSpeed) : 0f;
        float accRate = pressing ? accelerationRate : (1f - boatDecelerationSmoothing) * 10f;
        _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed, Time.deltaTime * accRate);

        Vector3 boatFwd = HeadingForward();
        transform.rotation = Quaternion.Euler(0f, _boatYaw, 0f) * _axisFix;
        transform.position += boatFwd * _currentSpeed * Time.deltaTime;

        if (pressing && Time.time >= _nextPaddleTime)
        {
            _nextPaddleTime = Time.time + 0.45f;
            GameAudio.PlayPaddle(transform.position);
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

    }
}
