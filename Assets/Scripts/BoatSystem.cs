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
        if (Input.GetKeyDown(KeyCode.G) && !_isExiting)
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

        // ── Step 1: Player ko freeze karke BoatNear tak walk karo ───────────
        player.SetFrozen(true, lockWorldPosition: false);
        
        // Boat ke collider se takra ke player stuck na ho uske liye CC band karo
        if (_playerCC != null) _playerCC.enabled = false;

        yield return StartCoroutine(WalkPlayerToPoint(_playerTransform, boatNearPoint.position));

        // BoatNear ke paas pohonch ke boat ki taraf rotate karo
        yield return StartCoroutine(SmoothRotateToward(_playerTransform, boatNearPoint.forward, 0.3f));

        // ── Step 2: BoatClimb animation ─────────────────────────────────────
        if (_playerAnimator != null)
        {
            _playerAnimator.ResetTrigger(_hashClimb);
            _playerAnimator.SetTrigger(_hashClimb);
        }

        yield return null; // Ek frame — animation start ho

        // Climb animation start hone ka wait
        int climbStateHash = Animator.StringToHash(climbStateName);
        yield return WaitForStateStart(_playerAnimator, climbStateHash, 5f);

        // ── Step 3: Realistic Climb Motion ──────────────────────────────────
        // Animation ke dauran player ko seat pe smoothly arc ke sath le jao
        yield return StartCoroutine(SyncClimbMotion(_playerAnimator, climbStateHash, _playerTransform, boatSeatPoint));

        // Player ko runtime mount ka child banao taaki Boat ka 3000x scale player ko hawa me na phenke
        _runtimeMount.position = boatSeatPoint.position;
        _runtimeMount.rotation = boatSeatPoint.rotation;
        _playerTransform.SetParent(_runtimeMount);

        // Boat ko thoda left (ya jo bhi offset ho) shift karo
        if (boatEnterShiftOffset != Vector3.zero)
        {
            yield return StartCoroutine(SmoothShiftBoat(boatEnterShiftOffset, 0.5f));
        }

        // ── Step 4: SitDown animation ───────────────────────────────────────
        if (_playerAnimator != null)
        {
            _playerAnimator.ResetTrigger(_hashSitDown);
            _playerAnimator.SetTrigger(_hashSitDown);
        }

        int sitDownHash = Animator.StringToHash(sitDownStateName);
        yield return WaitForStateStart(_playerAnimator, sitDownHash, 5f);
        yield return WaitForStateNormalizedTime(_playerAnimator, sitDownHash, 0.95f, 10f);

        // ── Step 5: Paddling ready (Ab SeatIdle default chalega) ─────────────
        _canPaddle  = true;
        _isEntering = false;

        // Camera ko Boat mode me dalo
        if (Camera.main != null)
        {
            CameraController cam = Camera.main.GetComponent<CameraController>();
            if (cam != null) cam.StartCinematic(camDistance, camOffset, camYaw, camPitch);
        }

        Debug.Log("[BoatSystem] Player boat mein aa gaya!");
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
        }

        int sitUpHash = Animator.StringToHash(sitUpStateName);
        yield return WaitForStateStart(_playerAnimator, sitUpHash, 5f);
        yield return WaitForStateNormalizedTime(_playerAnimator, sitUpHash, 0.95f, 10f);

        // ── Step 2: Normal idle mein 2 seconds ─────────────────────────────
        yield return new WaitForSeconds(idleAfterSitUpDuration);

        // ── Step 3: BoatUnclimb animation ──────────────────────────────────
        if (_playerAnimator != null)
        {
            _playerAnimator.ResetTrigger(_hashUnclimb);
            _playerAnimator.SetTrigger(_hashUnclimb);
        }

        int unclimbHash = Animator.StringToHash(unclimbStateName);
        yield return WaitForStateStart(_playerAnimator, unclimbHash, 5f);

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
            _playerController.SetFrozen(false);

        // Boat trigger se bahar gaya — oar drop ho jayega (OnTriggerExit se auto)
        if (boatTrigger != null)
            boatTrigger.OnPlayerExitedBoat();

        _isOccupied = false;
        _isExiting  = false;

        Debug.Log("[BoatSystem] Player boat se utar gaya!");
    }

    // ─── Helper: Player ko point tak walk karao (direct lerp) ─────────────────
    private IEnumerator WalkPlayerToPoint(Transform player, Vector3 targetPos)
    {
        // Player speed ko walk pe set karo (0.5f)
        int hashSpeed = Animator.StringToHash("Speed");
        if (_playerAnimator != null) _playerAnimator.SetFloat(hashSpeed, 0.5f);

        // Sirf XZ mein move karo — Y height maintain raho
        Vector3 flatTarget = new Vector3(targetPos.x, player.position.y, targetPos.z);

        while (Vector3.Distance(new Vector3(player.position.x, 0, player.position.z),
                                new Vector3(flatTarget.x, 0, flatTarget.z)) > 0.15f)
        {
            // Direction towards target
            Vector3 dir = (flatTarget - player.position).normalized;
            dir.y = 0f;

            // Rotate towards target
            if (dir != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                player.rotation = Quaternion.Slerp(player.rotation, targetRot, Time.deltaTime * 8f);
            }

            // Move
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

        float nt = 0f;
        while (nt < 0.95f) // Jab tak animation 95% nahi ho jati
        {
            if (anim != null)
            {
                for (int i = 0; i < anim.layerCount; i++)
                {
                    var info = anim.GetCurrentAnimatorStateInfo(i);
                    if (info.shortNameHash == stateHash)
                    {
                        nt = info.normalizedTime;
                        break;
                    }
                }
            }
            else
            {
                nt += Time.deltaTime * 0.5f; // Fallback agar animator na ho
            }
            
            // Animation ka 10% se 90% wala hissa motion ke liye use karo
            float t = Mathf.Clamp01((nt - 0.1f) / 0.8f);
            t = Mathf.SmoothStep(0f, 1f, t);

            // Ek halka sa Arc (jump/lift) add karo climb realistic lagne ke liye
            Vector3 currentPos = Vector3.Lerp(startPos, targetPoint.position, t);
            float arc = Mathf.Sin(t * Mathf.PI) * 0.4f; // 0.4m height lift
            currentPos.y += arc;

            player.position = currentPos;
            player.rotation = Quaternion.Slerp(startRot, targetPoint.rotation, t);
            
            yield return null;
        }

        // Final snap
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
