using System.Collections;
using UnityEngine;

/// <summary>
/// BoatSystem — Poora boat enter/exit, paddling aur movement system.
///
/// ── FLOW ─────────────────────────────────────────────────────────────────────
///  EnterBoat:
///    1. Player walk karke BoatNear (empty object) tak jaata hai
///    2. BoatClimb animation chalta hai
///    3. Player BoatSeat (empty object) pe land karta hai — Idle 2 sec
///    4. SitDown animation chalta hai
///    5. Player boat chala sakta hai (WASD = paddle, Mouse/Trackpad = steer)
///
///  ExitBoat (G press):
///    1. SitUp animation chalta hai
///    2. Idle 2 sec
///    3. BoatUnclimb animation chalta hai
///    4. Player BoatNear pe land karta hai
///    5. Player walk karke BoatTrigger se bahar jaata hai
///
/// ── Scene Setup ─────────────────────────────────────────────────────────────
///  Boat GameObject:
///    • BoatSystem script lagao
///    • Inspector mein assign karo:
///        - boatNearPoint     → Boat ke side pe Empty Object "BoatNear"
///        - boatSeatPoint     → Boat ke baithne ki jagah Empty Object "BoatSeat"
///        - boatTrigger       → BoatTrigger script
///        - boatRigidbody     → Boat ka Rigidbody (ya khud boat pe dhoondh lega)
///  Player Animator mein yeh states chahiye:
///    • "BoatClimb"   — climb up animation
///    • "BoatIdle"    — boat pe baithe baithe idle (ya normal idle bhi chalega)
///    • "SitDown"     — sit down animation
///    • "Paddle"      — paddling loop animation (Bool "IsPaddling" se control)
///    • "SitUp"       — sit up animation
///    • "BoatUnclimb" — climb down/exit animation
///  Player tag "Player" hona chahiye.
/// </summary>
public class BoatSystem : MonoBehaviour
{
    // ─── Inspector ─────────────────────────────────────────────────────────────
    [Header("Boat Mount Points")]
    [Tooltip("Boat ke side pe Empty Object — player yahan walk karke aata hai (BoatNear)")]
    public Transform boatNearPoint;

    [Tooltip("Boat se utarte waqt player kahan land karega (unclimb ke liye)")]
    public Transform boatNearDownPoint;

    [Tooltip("Boat ki seating — player yahan land karta hai (BoatSeat)")]
    public Transform boatSeatPoint;

    [Header("Boat Movement")]
    [Tooltip("Boat ka Rigidbody — null hone par script khud dhoondh legi")]
    public Rigidbody boatRigidbody;

    [Tooltip("Paddle karte time forward speed (m/s)")]
    public float boatForwardSpeed = 3f;

    [Tooltip("Boat steering sensitivity (mouse/trackpad)")]
    public float boatTurnSpeed = 80f;

    [Tooltip("Boat kitni smoothly rukti hai (0=instant, 1=slow)")]
    [Range(0f, 0.99f)]
    public float boatDecelerationSmoothing = 0.85f;

    [Header("AAA Polish (Physics & Feel)")]
    [Tooltip("Smooth Acceleration Rate")]
    public float accelerationRate = 2f;
    [Tooltip("Turn Smoothing Momentum")]
    public float turnSmoothing = 5f;
    [Tooltip("Water Bobbing Intensity (Up/Down)")]
    public float waterBobAmount = 0.5f;
    [Tooltip("Water Bobbing Speed")]
    public float waterBobSpeed = 2f;

    public enum AxisDirection { Z_Forward, Y_Up, X_Right, Negative_Z, Negative_Y, Negative_X }
    
    [Tooltip("Boat model ka aage badhne wala axis konsa hai? (Blender models ke liye Y_Up select karo)")]
    public AxisDirection forwardMovementAxis = AxisDirection.Y_Up; // Default to Y_Up as user requested

    [Tooltip("Boat me baithne ke baad boat local axes pe kitna ghiske (e.g. X=-0.5 means left)")]
    public Vector3 boatEnterShiftOffset = new Vector3(-0.5f, 0f, 0f);

    [Header("References")]
    [Tooltip("BoatTrigger script — exit ke baad player ko wapas bahar walk karana ke liye")]
    public BoatTrigger boatTrigger;

    [Header("Animation State Names")]
    [Tooltip("Animator state name — boat par chadhne ki animation")]
    public string climbStateName   = "BoatClimb";

    [Tooltip("Animator state name — boat par baith ke paddling")]
    public string paddleStateName  = "Paddle";

    [Tooltip("Animator state name — boat pe chadh ke pehla idle (SeatIdle)")]
    public string seatIdleStateName = "SeatIdle";

    [Tooltip("Animator state name — baithe baithe idle")]
    public string sitDownStateName = "SitDown";

    [Tooltip("Animator state name — uthne ki animation (StandUp)")]
    public string sitUpStateName   = "StandUp";

    [Tooltip("Animator state name — boat se utarne ki animation")]
    public string unclimbStateName = "BoatUnclimb";

    [Header("Animation Trigger/Bool Names")]
    [Tooltip("Animator trigger — BoatClimb start karne ke liye")]
    public string climbTriggerName   = "BoatClimb";

    [Tooltip("Animator trigger — SitDown start karne ke liye")]
    public string sitDownTriggerName = "SitDown";

    [Tooltip("Animator trigger — StandUp start karne ke liye (screenshot mein StandUp hai)")]
    public string sitUpTriggerName   = "StandUp";

    [Tooltip("Animator trigger — BoatUnclimb start karne ke liye")]
    public string unclimbTriggerName = "BoatUnclimb";

    [Tooltip("Animator Bool — Paddle animation loop ke liye")]
    public string paddlingBoolName = "IsPaddling";

    [Header("Timings")]
    [Tooltip("Climb animation ke baad idle me kitne seconds ruko")]
    public float idleAfterClimbDuration = 2f;

    [Tooltip("SitUp ke baad BoatUnclimb se pehle idle me kitne seconds ruko")]
    public float idleAfterSitUpDuration = 2f;

    [Tooltip("Player kitni speed se BoatNear tak walk kare (NavMesh nahi — direct lerp)")]
    public float walkToBoatSpeed = 2.5f;

    [Tooltip("Player kitni speed se boat trigger bahar tak walk kare")]
    public float walkAwaySpeed = 2.5f;

    [Header("Exit Walk Target")]
    [Tooltip("Player boat se utarne ke baad is point tak walk karega (BoatTrigger ke bahar)")]
    public Transform exitWalkTarget;

    [Header("Camera Settings (Boat Cinematic)")]
    [Tooltip("Boat me baithne par camera kitna door rahega (kam karoge to aur paas aayega)")]
    public float camDistance = 3.5f;
    [Tooltip("Camera ki position offset (XYZ)")]
    public Vector3 camOffset = new Vector3(0f, 1.5f, 0f);
    [Tooltip("Camera ka up/down tilt (Pitch)")]
    public float camPitch = 15f;
    [Tooltip("Camera ka left/right rotation (Yaw)")]
    public float camYaw = 0f;

    // ─── Private State ─────────────────────────────────────────────────────────
    private bool _isOccupied        = false;
    private bool _isPaddling        = false;
    private bool _canPaddle         = false;
    private bool _isEntering        = false;
    private bool _isExiting         = false;

    private PlayerController _playerController;
    private Animator         _playerAnimator;
    private CharacterController _playerCC;
    private Transform        _playerTransform;
    private Transform        _runtimeMount; // Unscaled mount to fix 3000x scale issues
    private Vector3          _currentBoatVelocity;

    // AAA State
    private float _currentSpeed = 0f;
    private float _currentTurn = 0f;

    // Animator hashes
    private int _hashClimb;
    private int _hashSitDown;
    private int _hashSitUp;
    private int _hashUnclimb;
    private int _hashIsPaddling;

    // ─── Public Properties ─────────────────────────────────────────────────────
    public bool IsOccupied  => _isOccupied;
    public bool IsPaddling  => _isPaddling;

    // ─── Unity Lifecycle ───────────────────────────────────────────────────────
    private void Awake()
    {
        if (boatRigidbody == null)
            boatRigidbody = GetComponent<Rigidbody>();

        // Boat ka scale (3000x) player ki animation ko bigad deta hai, isliye hum ek 
        // 1x scale wala alag 'Mount' object bana rahe hain jo boat ke sath chalega.
        GameObject mountObj = new GameObject("PlayerBoatMount_" + gameObject.name);
        _runtimeMount = mountObj.transform;
    }

    private void LateUpdate()
    {
        // Player ko boat ke sath chipkaye rakhne ke liye unscaled mount update karo
        if (_isOccupied && _runtimeMount != null && boatSeatPoint != null)
        {
            _runtimeMount.position = boatSeatPoint.position;
            _runtimeMount.rotation = boatSeatPoint.rotation;

            // Animation ya kisi root motion ki wajah se player khisak na jaye, 
            // isliye hum isko mathematically lock kar denge zero par.
            if (_playerTransform != null && _playerTransform.parent == _runtimeMount)
            {
                _playerTransform.localPosition = Vector3.zero;
                _playerTransform.localRotation = Quaternion.identity;
            }
        }
    }

    private void Start()
    {
        _hashClimb       = Animator.StringToHash(climbTriggerName);
        _hashSitDown     = Animator.StringToHash(sitDownTriggerName);
        _hashSitUp       = Animator.StringToHash(sitUpTriggerName);
        _hashUnclimb     = Animator.StringToHash(unclimbTriggerName);
        _hashIsPaddling  = Animator.StringToHash(paddlingBoolName);
    }

    private void Update()
    {
        if (!_isOccupied || !_canPaddle) return;

        HandleBoatMovement();
        HandleExitInput();
    }

    // ─── Boat Movement ─────────────────────────────────────────────────────────
    private void HandleBoatMovement()
    {
        float v = Input.GetAxis("Vertical");   // W/S
        float h = Input.GetAxis("Horizontal"); // A/D
        float mouseX = Input.GetAxis("Mouse X");

        bool pressing = Mathf.Abs(v) > 0.05f;

        // Paddling animation toggle
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

        // ─── AAA Steering (Smooth Momentum) ───
        float targetTurn = mouseX + (h * 2f); 
        _currentTurn = Mathf.Lerp(_currentTurn, targetTurn, Time.deltaTime * turnSmoothing);

        if (Mathf.Abs(_currentTurn) > 0.01f)
        {
            float turn = _currentTurn * boatTurnSpeed * Time.deltaTime;
            // Space.World ensure karta hai ki boat hamesha left/right ghoome
            transform.Rotate(Vector3.up, turn, Space.World);
            
            if (boatRigidbody != null)
            {
                boatRigidbody.rotation = transform.rotation;
            }
        }

        // ─── AAA Acceleration (Smooth Speed) ───
        float targetSpeed = pressing ? (v * boatForwardSpeed) : 0f;
        float accRate = pressing ? accelerationRate : (1f - boatDecelerationSmoothing) * 10f;
        _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed, Time.deltaTime * accRate);

        _currentBoatVelocity = GetBoatForward() * _currentSpeed;

        // Apply Movement
        Vector3 deltaMove = _currentBoatVelocity * Time.deltaTime;
        transform.position += deltaMove;
        
        // ─── AAA Water Bobbing (Sine Wave) ───
        // Boat paani pe halke se upar-neeche hogi
        float bobOffset = Mathf.Sin(Time.time * waterBobSpeed) * waterBobAmount * Time.deltaTime;
        transform.position += new Vector3(0, bobOffset, 0);

        if (boatRigidbody != null)
        {
            boatRigidbody.position = transform.position;
        }

        // ─── AAA Dynamic Camera (Speed pe depend karega) ───
        if (Camera.main != null)
        {
            CameraController cam = Camera.main.GetComponent<CameraController>();
            if (cam != null)
            {
                // Jaise speed badhegi, camera thoda peeche (zoom out) jayega speed feel ke liye
                float speedFactor = Mathf.Abs(_currentSpeed) / boatForwardSpeed;
                float dynamicDist = camDistance + (speedFactor * 1.5f);
                cam.StartCinematic(dynamicDist, camOffset, camYaw, camPitch);
            }
        }
    }

    // ─── Exit Input ────────────────────────────────────────────────────────────
    private void HandleExitInput()
    {
        if ((Input.GetKeyDown(KeyCode.G) || Input.GetKeyDown(KeyCode.B)) && !_isExiting && !_isEntering)
        {
            StartCoroutine(ExitBoatSequence());
        }
    }

    // ─── ENTER BOAT ────────────────────────────────────────────────────────────
    /// <summary>
    /// BoatTrigger is method ko call karta hai jab player B dabaye.
    /// </summary>
    public void EnterBoat(PlayerController player)
    {
        if (_isOccupied || _isEntering) return;
        StartCoroutine(EnterBoatSequence(player));
    }

    private IEnumerator EnterBoatSequence(PlayerController player)
    {
        _isEntering = true;
        _isOccupied = true;

        // References store karo
        _playerController = player;
        _playerTransform  = player.transform;
        _playerCC         = player.GetComponent<CharacterController>();
        _playerAnimator   = player.GetComponentInChildren<Animator>();

        // Step 0: Agar player ke hath mein koi weapon hai, pehle smoothly unequip karo
        if (_playerController != null && _playerController.IsHoldingWeapon)
        {
            yield return _playerController.UnequipCurrentWeaponForPickup();
            yield return new WaitForSeconds(0.1f);
        }

        // ── Step 1: Player ko freeze karke Boat boarding point tak smoothly align karo ──
        player.SetFrozen(true, lockWorldPosition: false);
        if (_playerCC != null) _playerCC.enabled = false;

        Vector3 boardPos = (boatNearPoint != null) ? boatNearPoint.position : transform.position - transform.right * 1.5f;
        Vector3 boardFwd = (boatNearPoint != null) ? boatNearPoint.forward : transform.forward;

        // Walk animation band karo, clean align karo
        if (_playerAnimator != null)
            _playerAnimator.SetFloat(Animator.StringToHash("Speed"), 0f);

        yield return StartCoroutine(SmoothAlignTo(_playerTransform, boardPos, boardFwd, 0.35f));

        // ── Step 2: BoatClimb animation ─────────────────────────────────────
        if (_playerAnimator != null)
        {
            _playerAnimator.ResetTrigger(_hashClimb);
            _playerAnimator.SetTrigger(_hashClimb);
            _playerAnimator.CrossFadeInFixedTime(climbStateName, 0.1f, 0, 0f);
        }

        yield return null;

        int climbStateHash = Animator.StringToHash(climbStateName);

        // ── Step 3: Realistic Climb Motion (Seat tak smooth arc lift) ────────
        yield return StartCoroutine(SyncClimbMotion(_playerAnimator, climbStateHash, _playerTransform, boatSeatPoint));

        // Player ko runtime mount ka child banao taaki boat ke saath lock rahe
        _runtimeMount.position = boatSeatPoint.position;
        _runtimeMount.rotation = boatSeatPoint.rotation;
        _playerTransform.SetParent(_runtimeMount);
        _playerTransform.localPosition = Vector3.zero;
        _playerTransform.localRotation = Quaternion.identity;

        // Boat ko thoda shift karo agar offset ho
        if (boatEnterShiftOffset != Vector3.zero)
        {
            yield return StartCoroutine(SmoothShiftBoat(boatEnterShiftOffset, 0.4f));
        }

        // ── Step 4: SitDown animation ───────────────────────────────────────
        if (_playerAnimator != null)
        {
            _playerAnimator.ResetTrigger(_hashSitDown);
            _playerAnimator.SetTrigger(_hashSitDown);
            _playerAnimator.CrossFadeInFixedTime(sitDownStateName, 0.15f, 0, 0f);
        }

        yield return new WaitForSeconds(0.9f);

        // ── Step 5: Paddling ready ──────────────────────────────────────────
        _canPaddle  = true;
        _isEntering = false;

        // Camera ko Boat mode me dalo
        if (Camera.main != null)
        {
            CameraController cam = Camera.main.GetComponent<CameraController>();
            if (cam != null) cam.StartCinematic(camDistance, camOffset, camYaw, camPitch);
        }

        Debug.Log("[BoatSystem] Player boat mein successfully baith gaya!");
    }

    // ─── EXIT BOAT ─────────────────────────────────────────────────────────────
    private IEnumerator ExitBoatSequence()
    {
        _isExiting  = true;
        _canPaddle  = false;

        // Camera ko wapas Normal mode me bhejo
        if (Camera.main != null)
        {
            CameraController cam = Camera.main.GetComponent<CameraController>();
            if (cam != null) cam.StopCinematic();
        }

        // Boat roko
        if (boatRigidbody != null)
            boatRigidbody.linearVelocity = Vector3.zero;

        // Paddle animation band karo
        if (_isPaddling)
        {
            _isPaddling = false;
            if (_playerAnimator != null)
                _playerAnimator.SetBool(_hashIsPaddling, false);
        }

        // ── Step 1: SitUp animation ─────────────────────────────────────────
        if (_playerAnimator != null)
        {
            _playerAnimator.ResetTrigger(_hashSitUp);
            _playerAnimator.SetTrigger(_hashSitUp);
            _playerAnimator.CrossFadeInFixedTime(sitUpStateName, 0.15f, 0, 0f);
        }

        int sitUpHash = Animator.StringToHash(sitUpStateName);
        yield return WaitForStateStart(_playerAnimator, sitUpHash, 2f);
        yield return WaitForStateNormalizedTime(_playerAnimator, sitUpHash, 0.9f, 3f);

        // ── Step 2: Normal idle mein thoda rest ─────────────────────────────
        yield return new WaitForSeconds(Mathf.Min(idleAfterSitUpDuration, 0.8f));

        // ── Step 3: BoatUnclimb animation ──────────────────────────────────
        if (_playerAnimator != null)
        {
            _playerAnimator.ResetTrigger(_hashUnclimb);
            _playerAnimator.SetTrigger(_hashUnclimb);
            _playerAnimator.CrossFadeInFixedTime(unclimbStateName, 0.15f, 0, 0f);
        }

        int unclimbHash = Animator.StringToHash(unclimbStateName);
        yield return WaitForStateStart(_playerAnimator, unclimbHash, 2f);

        // ── Step 4: Player ko BoatNear pe utaro ────────────────────────────
        _playerTransform.SetParent(null); // Boat se alag karo
        
        // Agar boatNearDownPoint assign kiya hai toh wahan utrega, nahi toh purane boatNearPoint pe hi utrega
        Transform unclimbTarget = boatNearDownPoint != null ? boatNearDownPoint : boatNearPoint;

        // Realistic Unclimb motion
        yield return StartCoroutine(SyncClimbMotion(_playerAnimator, unclimbHash, _playerTransform, unclimbTarget));

        // Player walk karke exit target tak jaye
        if (exitWalkTarget != null)
            yield return StartCoroutine(WalkPlayerToPoint(_playerTransform, exitWalkTarget.position));

        // ── Step 5: Player ko unfreeze karo (Control wapas do) ──────────────
        if (_playerCC != null) _playerCC.enabled = true;
        
        if (_playerController != null)
        {
            _playerController.SetFrozen(false);
            _playerController.SnapToGround();
        }

        if (_playerAnimator != null)
        {
            _playerAnimator.CrossFadeInFixedTime("UnarmedLocomotion", 0.2f, 0, 0f);
        }

        // Boat trigger se bahar gaya — oar drop ho jayega (OnTriggerExit se auto)
        if (boatTrigger != null)
            boatTrigger.OnPlayerExitedBoat();

        _isOccupied = false;
        _isExiting  = false;

        Debug.Log("[BoatSystem] Player boat se utar gaya!");
    }

    // ─── Helper: Player ko smoothly align karo position & rotation ───────
    private IEnumerator SmoothAlignTo(Transform target, Vector3 targetPos, Vector3 targetForward, float duration)
    {
        if (target == null) yield break;
        Vector3 startPos = target.position;
        Quaternion startRot = target.rotation;
        Quaternion destRot = targetForward != Vector3.zero 
            ? Quaternion.LookRotation(new Vector3(targetForward.x, 0f, targetForward.z)) 
            : startRot;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            target.position = Vector3.Lerp(startPos, targetPos, t);
            target.rotation = Quaternion.Slerp(startRot, destRot, t);
            yield return null;
        }

        target.position = targetPos;
        target.rotation = destRot;
    }

    // ─── Helper: Player ko point tak walk karao (direct lerp with timeout) ─
    private IEnumerator WalkPlayerToPoint(Transform player, Vector3 targetPos)
    {
        int hashSpeed = Animator.StringToHash("Speed");
        if (_playerAnimator != null) _playerAnimator.SetFloat(hashSpeed, 0.5f);

        Vector3 flatTarget = new Vector3(targetPos.x, player.position.y, targetPos.z);
        float timer = 0f;
        const float maxWalkTime = 1.8f; // Safety timeout prevents infinite walking

        while (timer < maxWalkTime &&
               Vector3.Distance(new Vector3(player.position.x, 0, player.position.z),
                                new Vector3(flatTarget.x, 0, flatTarget.z)) > 0.25f)
        {
            timer += Time.deltaTime;
            Vector3 dir = (flatTarget - player.position).normalized;
            dir.y = 0f;

            if (dir != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                player.rotation = Quaternion.Slerp(player.rotation, targetRot, Time.deltaTime * 10f);
            }

            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null && cc.enabled)
                cc.Move(dir * walkToBoatSpeed * Time.deltaTime + Vector3.down * 2f * Time.deltaTime);
            else
                player.position = Vector3.MoveTowards(player.position, flatTarget, walkToBoatSpeed * Time.deltaTime);

            yield return null;
        }

        // Walk khatam, speed wapas 0
        if (_playerAnimator != null) _playerAnimator.SetFloat(hashSpeed, 0f);
    }

    // ─── Helper: Realistic Sync Climb Motion ───────────────────────────────────
    private IEnumerator SyncClimbMotion(Animator anim, int stateHash, Transform player, Transform targetPoint)
    {
        Vector3 startPos = player.position;
        Quaternion startRot = player.rotation;
        
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        float timer = 0f;
        const float motionDuration = 1.8f;

        while (timer < motionDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / motionDuration);
            t = Mathf.SmoothStep(0f, 1f, t);

            Vector3 currentPos = Vector3.Lerp(startPos, targetPoint.position, t);
            float arc = Mathf.Sin(t * Mathf.PI) * 0.4f; // 0.4m arc lift
            currentPos.y += arc;

            player.position = currentPos;
            player.rotation = Quaternion.Slerp(startRot, targetPoint.rotation, t);
            yield return null;
        }

        player.position = targetPoint.position;
        player.rotation = targetPoint.rotation;
    }

    // ─── Helper: Boat ko smoothly shift karo ──────────────────────────────────
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
            {
                boatRigidbody.position = transform.position;
            }
                
            yield return null;
        }
    }

    // ─── Helper: Forward Direction mapping ────────────────────────────────────
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

    // ─── Helper: Player ko smoothly rotate karo ───────────────────────────────
    private IEnumerator SmoothRotateToward(Transform player, Vector3 direction, float duration)
    {
        if (direction == Vector3.zero) yield break;
        Quaternion targetRot = Quaternion.LookRotation(direction);
        Quaternion startRot  = player.rotation;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            player.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }
        player.rotation = targetRot;
    }

    // ─── Helper: Animator state start hone ka wait ────────────────────────────
    private static IEnumerator WaitForStateStart(Animator anim, int stateHash, float timeout)
    {
        if (anim == null) yield break;
        float timer = 0f;
        while (timer < timeout)
        {
            timer += Time.deltaTime;
            for (int i = 0; i < anim.layerCount; i++)
            {
                if (anim.GetCurrentAnimatorStateInfo(i).shortNameHash == stateHash ||
                    anim.GetNextAnimatorStateInfo(i).shortNameHash == stateHash)
                    yield break;
            }
            yield return null;
        }
    }

    // ─── Helper: Animator state normalized time tak wait ──────────────────────
    private static IEnumerator WaitForStateNormalizedTime(Animator anim, int stateHash, float targetNT, float timeout)
    {
        if (anim == null) yield break;
        float timer = 0f;
        while (timer < timeout)
        {
            timer += Time.deltaTime;
            for (int i = 0; i < anim.layerCount; i++)
            {
                var info = anim.GetCurrentAnimatorStateInfo(i);
                if (info.shortNameHash == stateHash && info.normalizedTime >= targetNT)
                    yield break;
            }
            yield return null;
        }
    }
}
