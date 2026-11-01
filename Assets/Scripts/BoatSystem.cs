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
///    3. On pressing 'B', player smoothly aligns to boatNearPoint in 0.15s.
///    4. BoatClimb animation plays immediately and moves player smoothly onto boatSeatPoint.
///    5. SitDown animation plays.
///    6. SeatIdle activates — player can drive boat (WASD = paddle, Mouse/A/D = steer).
///
///  ExitBoat (G or F press):
///    1. StandUp animation plays.
///    2. BoatUnclimb animation plays and moves player down to boatNearDownPoint.
///    3. CharacterController re-enabled, player unfreezes and regains full normal control.
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
    public float waterBobAmount = 0.4f;
    public float waterBobSpeed = 2f;

    public enum AxisDirection { Z_Forward, Y_Up, X_Right, Negative_Z, Negative_Y, Negative_X }
    
    [Tooltip("Boat model ka forward axis (Blender models ke liye Negative_Z ya Y_Up)")]
    public AxisDirection forwardMovementAxis = AxisDirection.Negative_Z;

    [Tooltip("Boat me baithne ke baad boat local axes pe kitna ghiske")]
    public Vector3 boatEnterShiftOffset = new Vector3(-0.5f, 0f, 0f);

    // ─── Inspector: Animation State Names ──────────────────────────────────────
    [Header("Animation State Names")]
    public string climbStateName    = "BoatClimb";
    public string paddleStateName   = "Paddle";
    public string sitDownStateName  = "SitDown";
    public string sitUpStateName    = "StandUp";
    public string unclimbStateName  = "BoatUnclimb";

    [Header("Animation Trigger/Bool Names")]
    public string climbTriggerName   = "BoatClimb";
    public string sitDownTriggerName = "SitDown";
    public string sitUpTriggerName   = "StandUp";
    public string unclimbTriggerName = "BoatUnclimb";
    public string paddlingBoolName   = "IsPaddling";
    public string seatIdleBoolName   = "SeatIdle";

    [Header("Timings")]
    public float idleAfterSitUpDuration = 0.35f;

    [Header("Camera Settings (Boat Cinematic)")]
    public float camDistance = 4.0f;
    public Vector3 camOffset = new Vector3(0f, 1.6f, 0f);
    public float camPitch    = 14f;
    public float camYaw      = 0f;

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

    private int _hashClimb;
    private int _hashSitDown;
    private int _hashSitUp;
    private int _hashUnclimb;
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

        _hashClimb       = Animator.StringToHash(climbTriggerName);
        _hashSitDown     = Animator.StringToHash(sitDownTriggerName);
        _hashSitUp       = Animator.StringToHash(sitUpTriggerName);
        _hashUnclimb     = Animator.StringToHash(unclimbTriggerName);
        _hashIsPaddling  = Animator.StringToHash(paddlingBoolName);
        _hashSeatIdle    = Animator.StringToHash(seatIdleBoolName);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (oarPickup == null)
            oarPickup = OarPickup.Instance ?? FindFirstObjectByType<OarPickup>();

        HideUI();
    }

    private void LateUpdate()
    {
        if (_isOccupied && _runtimeMount != null && boatSeatPoint != null)
        {
            _runtimeMount.position = boatSeatPoint.position;
            Vector3 bFwd = GetBoatForward();
            bFwd.y = 0f;
            if (bFwd.sqrMagnitude > 0.001f)
                _runtimeMount.rotation = Quaternion.LookRotation(bFwd.normalized, Vector3.up);

            if (_playerTransform != null && _playerTransform.parent == _runtimeMount)
            {
                _playerTransform.localPosition = Vector3.zero;
                _playerTransform.localRotation = Quaternion.identity;
            }
        }
    }

    private void Update()
    {
        if (_isOccupied && _canPaddle)
        {
            HandleBoatMovement();
            HandleExitInput();
            return;
        }

        if (!_isOccupied && !_isEntering && !_isExiting)
        {
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

            // Show canvas UI if assigned
            if (driveBoatUI != null)  driveBoatUI.SetActive(hasOar);
            if (collectOarUI != null) collectOarUI.SetActive(!hasOar);

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
        if (!_playerNear || _isOccupied || _isEntering || _isExiting) return;

        bool hasOar = !requireOar || (oarPickup != null && oarPickup.IsPickedUp);

        // Don't duplicate if canvas UI is active unless flashing warning
        if (driveBoatUI != null && driveBoatUI.activeSelf && hasOar) return;
        if (collectOarUI != null && collectOarUI.activeSelf && !hasOar && _oarWarningTimer <= 0f) return;

        string prompt;
        Color promptColor;

        if (!hasOar)
        {
            prompt = _oarWarningTimer > 0f 
                ? "⚠️ OAR REQUIRED! Please Collect The Oar First" 
                : "Collect The Oar First";
            promptColor = _oarWarningTimer > 0f ? new Color(1f, 0.3f, 0.2f) : new Color(1f, 0.8f, 0.25f);
        }
        else
        {
            prompt = "Press B to Board Boat";
            promptColor = Color.white;
        }

        GUIStyle style = new GUIStyle();
        style.fontSize = 24;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleCenter;

        float sw = Screen.width;
        float sh = Screen.height;
        Rect r = new Rect(0, sh * 0.82f, sw, 50);

        // Shadow
        style.normal.textColor = new Color(0, 0, 0, 0.9f);
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), prompt, style);

        // Foreground
        style.normal.textColor = promptColor;
        GUI.Label(r, prompt, style);
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
        {
            float turn = _currentTurn * boatTurnSpeed * Time.deltaTime;
            transform.Rotate(Vector3.up, turn, Space.World);

            if (boatRigidbody != null)
                boatRigidbody.rotation = transform.rotation;
        }

        // Acceleration
        float targetSpeed = pressing ? (v * boatForwardSpeed) : 0f;
        float accRate = pressing ? accelerationRate : (1f - boatDecelerationSmoothing) * 10f;
        _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed, Time.deltaTime * accRate);

        Vector3 boatFwd = GetBoatForward();
        Vector3 deltaMove = boatFwd * _currentSpeed * Time.deltaTime;
        transform.position += deltaMove;

        // Water Bobbing
        if (!_hasWaterRest)
        {
            _waterRestY = transform.position.y;
            _hasWaterRest = true;
        }

        float bob = Mathf.Sin(Time.time * waterBobSpeed) * waterBobAmount * 0.08f;
        Vector3 p = transform.position;
        p.y = _waterRestY + bob;
        transform.position = p;

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
            boatRigidbody.position = transform.position;

        // Dynamic Camera
        if (Camera.main != null)
        {
            CameraController cam = Camera.main.GetComponent<CameraController>();
            if (cam != null)
            {
                float speedFactor = Mathf.Abs(_currentSpeed) / Mathf.Max(0.1f, boatForwardSpeed);
                float dynamicDist = camDistance + (speedFactor * 1.5f);
                cam.StartCinematic(dynamicDist, camOffset, camYaw, camPitch);
            }
        }
    }

    private void HandleExitInput()
    {
        if ((Input.GetKeyDown(KeyCode.G) || Input.GetKeyDown(KeyCode.F)) && !_isExiting)
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

        // 1. Unequip any weapon before boarding
        if (player.WeaponIndex != 0)
        {
            yield return StartCoroutine(player.UnequipCurrentWeaponForPickup());
        }

        // 2. Freeze player locomotion and disable CC
        player.SetFrozen(true, lockWorldPosition: false);
        if (_playerCC != null) _playerCC.enabled = false;

        Transform seatTarget = boatSeatPoint != null ? boatSeatPoint : transform;

        // Guaranteed upright rotation for the boat seat (strictly zero pitch/roll)
        Vector3 boatFwd = GetBoatForward();
        boatFwd.y = 0f;
        if (boatFwd.sqrMagnitude < 0.001f) boatFwd = transform.forward;
        boatFwd.Normalize();
        Quaternion uprightSeatRot = Quaternion.LookRotation(boatFwd, Vector3.up);

        // 3. Direct snap to boat seat standing up (No climb animation!)
        _playerTransform.position = seatTarget.position;
        _playerTransform.rotation = uprightSeatRot;

        // 4. Parent player to unscaled mount at seat
        _runtimeMount.position = seatTarget.position;
        _runtimeMount.rotation = uprightSeatRot;
        _playerTransform.SetParent(_runtimeMount);
        _playerTransform.localPosition = Vector3.zero;
        _playerTransform.localRotation = Quaternion.identity;

        // 5. Play SitDown / Seat animation
        if (_playerAnimator != null)
        {
            _playerAnimator.ResetTrigger(_hashSitDown);
            _playerAnimator.SetTrigger(_hashSitDown);
            _playerAnimator.CrossFadeInFixedTime(sitDownStateName, 0.15f, 0, 0f);
        }

        yield return new WaitForSeconds(0.6f);

        // 6. Ready to paddle!
        if (_playerAnimator != null)
        {
            _playerAnimator.SetBool(_hashSeatIdle, true);
        }

        _canPaddle  = true;
        _isEntering = false;

        if (Camera.main != null)
        {
            CameraController cam = Camera.main.GetComponent<CameraController>();
            if (cam != null) cam.StartCinematic(camDistance, camOffset, camYaw, camPitch);
        }

        Debug.Log("<color=#00FF88><b>[BoatSystem]</b></color> Player boarded boat and seated directly!");
    }

    // ─── Exit Boat Sequence ────────────────────────────────────────────────────
    private IEnumerator ExitBoatSequence()
    {
        _isExiting  = true;
        _canPaddle  = false;

        if (Camera.main != null)
        {
            CameraController cam = Camera.main.GetComponent<CameraController>();
            if (cam != null) cam.StopCinematic();
        }

        if (boatRigidbody != null)
            boatRigidbody.linearVelocity = Vector3.zero;

        if (_isPaddling)
        {
            _isPaddling = false;
            if (_playerAnimator != null)
                _playerAnimator.SetBool(_hashIsPaddling, false);
        }

        // 1. Play StandUp animation
        if (_playerAnimator != null)
        {
            _playerAnimator.SetBool(_hashSeatIdle, false);
            _playerAnimator.ResetTrigger(_hashSitUp);
            _playerAnimator.SetTrigger(_hashSitUp);
            _playerAnimator.CrossFadeInFixedTime(sitUpStateName, 0.15f, 0, 0f);
        }

        yield return new WaitForSeconds(Mathf.Max(0.5f, idleAfterSitUpDuration));

        // 2. Unparent and direct snap player outside to exit point (No unclimb animation!)
        Transform exitTarget = boatNearDownPoint != null ? boatNearDownPoint : (boatNearPoint != null ? boatNearPoint : transform);
        _playerTransform.SetParent(null);
        _playerTransform.position = exitTarget.position;
        Vector3 exitFwd = exitTarget.forward;
        exitFwd.y = 0f;
        if (exitFwd.sqrMagnitude > 0.001f)
            _playerTransform.rotation = Quaternion.LookRotation(exitFwd.normalized, Vector3.up);

        // 3. Drop Oar immediately as player stands outside
        if (oarPickup == null)
            oarPickup = OarPickup.Instance ?? FindFirstObjectByType<OarPickup>();
        if (oarPickup != null && oarPickup.IsPickedUp)
        {
            Vector3 dropPos = exitTarget.position + exitTarget.right * 0.45f;
            oarPickup.DropOar(dropPos);
        }

        // 4. Restore player controls
        if (_playerCC != null) _playerCC.enabled = true;
        if (_cachedPlayer != null) _cachedPlayer.SetFrozen(false);

        _isOccupied = false;
        _isExiting  = false;

        Debug.Log("<color=#00FF88><b>[BoatSystem]</b></color> Player stood up, exited boat, and dropped oar!");
    }

    // ─── Motion Helpers ────────────────────────────────────────────────────────
    private IEnumerator SyncClimbMotion(Animator anim, Transform player, Transform targetPoint, float duration, float arcHeight, Quaternion targetRotation)
    {
        Vector3 startPos = player.position;
        Quaternion startRot = player.rotation;
        Vector3 targetPos = targetPoint.position;

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, smoothT);
            float arc = Mathf.Sin(smoothT * Mathf.PI) * arcHeight;
            currentPos.y += arc;
            player.position = currentPos;

            // Upright smooth rotation — guaranteed zero pitch or roll
            Quaternion slerpRot = Quaternion.Slerp(startRot, targetRotation, smoothT);
            Vector3 fwd = slerpRot * Vector3.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.001f)
            {
                player.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
            }

            yield return null;
        }

        player.position = targetPos;
        player.rotation = targetRotation;
    }

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
}
