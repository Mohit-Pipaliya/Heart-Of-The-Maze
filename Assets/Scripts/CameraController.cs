using UnityEngine;

[ExecuteAlways]
public class CameraController : MonoBehaviour
{
    [Header("Target & Positioning")]
    public Transform target;
    public float distance = 4.0f;
    public Vector3 targetOffset = new Vector3(0f, 1.5f, 0f); 

    [Header("AAA Aim Settings (Over Shoulder)")]
    public float aimDistance = 1.5f;
    public Vector3 aimOffset = new Vector3(0.7f, 1.5f, 0f); // Thoda right side me
    public float zoomSpeed = 10f;

    [Header("Camera Settings")]
    public float positionSmoothTime = 0.05f; 
    public float mouseSensitivity = 2.0f;

    [Header("Angle Limits")]
    public float yMinLimit = -20f;
    public float yMaxLimit = 70f;

    [Header("Collision")]
    public LayerMask collisionMask;
    public float minDistance = 0.5f;

    [Header("Editor Preview (Only for tuning)")]
    [Range(-20f, 70f)]
    public float editorPreviewPitch = 15f;

    [Header("AAA Gun Recoil & Shake")]
    public float recoilRecoverySpeed = 10f;
    private Vector2 _recoilOffset = Vector2.zero;
    private Vector3 _shakeOffset = Vector3.zero;
    private float _recoilRoll = 0f;
    private float _kickbackDistance = 0f;

    public static CameraController Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    public void AddRecoil(float pitchKick, float yawKick, float shakeAmount)
    {
        _recoilOffset.x -= pitchKick;
        _recoilOffset.y += yawKick;
        _recoilRoll += Random.Range(-0.85f, 0.85f) * Mathf.Max(0.5f, pitchKick * 0.4f);
        _kickbackDistance += Mathf.Clamp(shakeAmount * 1.6f, 0.06f, 0.22f);
        _shakeOffset += (Vector3)(Random.insideUnitCircle * shakeAmount);
        GameFeel.FovPunch(Mathf.Clamp(Mathf.Abs(pitchKick) * 1.8f, 2.5f, 5.0f));
    }

    private float mouseY = 0.0f;
    private float mouseX = 0.0f;
    private Vector3 currentPosVelocity;

    public float Yaw => mouseX;

    public Vector3 PlanarForward
    {
        get
        {
            Vector3 f = Quaternion.Euler(0f, mouseX, 0f) * Vector3.forward;
            f.y = 0f;
            return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
        }
    }

    public Vector3 PlanarRight
    {
        get
        {
            Vector3 r = Quaternion.Euler(0f, mouseX, 0f) * Vector3.right;
            r.y = 0f;
            return r.sqrMagnitude > 0.0001f ? r.normalized : Vector3.right;
        }
    }
    
    // Zoom ke liye variables
    private float _currentDistance;
    private Vector3 _currentOffset;
    private GunEquipSystem _gunSystem;

    // Cinematic variables
    private bool _isCinematic = false;
    private float _cinematicDistance;
    private Vector3 _cinematicOffset;
    private float _cinematicYaw;
    private float _cinematicPitch;
    private float _cinematicBlend = 0f;

    // ─── Dedicated Boat Camera Mode ─────────────────────────────────────────
    [Header("Boat Camera")]
    private bool _isBoatMode = false;
    private Transform _boatTransform;
    private System.Func<Vector3> _boatForwardFunc;
    private float _boatDistance = 4.8f;
    private Vector3 _boatOffset = new Vector3(0f, 1.8f, 0f);
    private float _boatPitch = 13.0f;
    private float _currentBoatYaw = 0f;
    private bool _hasBoatYaw = false;
    private Transform _savedPlayerTarget;
    private bool _hasBoatPose = false;
    private bool _snapBoatPose = false;
    private Vector3 _boatCamPos;
    private Quaternion _boatCamRot;

    public void EnterBoatMode(Transform boat, System.Func<Vector3> getForwardFunc, float dist = 4.8f, Vector3 offset = default, float pitch = 13f)
    {
        _isBoatMode = true;
        _boatTransform = boat;
        _boatForwardFunc = getForwardFunc;
        _boatDistance = dist > 0.1f ? dist : 4.8f;
        _boatOffset = offset == default ? new Vector3(0f, 1.8f, 0f) : offset;
        _boatPitch = pitch;
        if (target != null && target != boat)
        {
            _savedPlayerTarget = target;
        }

        // Initialize boat yaw immediately to the boat's forward heading
        if (_boatTransform != null)
        {
            Vector3 fwd = _boatForwardFunc != null ? _boatForwardFunc() : _boatTransform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.001f)
            {
                _currentBoatYaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
                _hasBoatYaw = true;
            }
        }
    }

    public void UpdateBoatCamera(float dynamicDist, Vector3 offset, float pitch)
    {
        if (!_isBoatMode) return;
        _boatDistance = dynamicDist;
        if (offset != default) _boatOffset = offset;
        _boatPitch = pitch;
    }

    /// <summary>
    /// Places the seated camera. snap = true jumps there on the same frame the player sits.
    /// </summary>
    public void SetBoatFraming(Vector3 worldPos, Quaternion worldRot, bool snap)
    {
        _isBoatMode = true;
        _hasBoatPose = true;
        _boatCamPos = worldPos;
        _boatCamRot = worldRot;
        if (snap) _snapBoatPose = true;

        Vector3 look = worldRot * Vector3.forward;
        look.y = 0f;
        if (look.sqrMagnitude > 0.0001f)
            mouseX = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
        float pitch = worldRot.eulerAngles.x;
        if (pitch > 180f) pitch -= 360f;
        mouseY = pitch;
    }

    public void ExitBoatMode(Transform playerTransform = null)
    {
        _isBoatMode = false;
        _boatTransform = null;
        _boatForwardFunc = null;
        _hasBoatYaw = false;
        _hasBoatPose = false;
        _snapBoatPose = false;

        Transform newTarget = playerTransform != null ? playerTransform : _savedPlayerTarget;
        if (newTarget != null)
        {
            target = newTarget;
            // Orient camera directly behind player's back
            mouseX = newTarget.eulerAngles.y;
            mouseY = 12f;
        }
    }

    public void StartCinematic(float dist, Vector3 offset, float yaw, float pitch)
    {
        _isCinematic = true;
        _cinematicDistance = dist;
        _cinematicOffset = offset;
        _cinematicYaw = yaw;
        _cinematicPitch = pitch;
    }

    public void StopCinematic()
    {
        _isCinematic = false;
    }

    private void Start()
    {
        _currentDistance = distance;
        _currentOffset = targetOffset;

        if (Application.isPlaying && target != null)
        {
            _gunSystem = target.GetComponent<GunEquipSystem>();

            float startPitch = transform.eulerAngles.x;
            if (startPitch > 180f) startPitch -= 360f;
            mouseY = startPitch;
            mouseX = target.eulerAngles.y;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void LateUpdate()
    {
        // ─── BOAT MODE: dedicated 3rd-person behind-boat camera ───────────────
        if (_isBoatMode && Application.isPlaying && (_hasBoatPose || _boatTransform != null))
        {
            RunBoatCamera();
            return;
        }

        // ─── LADDER MODE: camera locked behind player while climbing ──────────
        if (_isLadderMode && Application.isPlaying)
        {
            RunLadderCamera();
            return;
        }

        // ─── STAND PUZZLE MODE: dedicated side-profile camera ─────────────────
        if (_isStandMode && Application.isPlaying)
        {
            RunStandCamera();
            return;
        }

        if (target == null) return;

        bool isAiming = false;

        if (Application.isPlaying)
        {
            mouseX += Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseYInput = Input.GetAxis("Mouse Y") * mouseSensitivity;
            mouseY -= mouseYInput;
            mouseY = Mathf.Clamp(mouseY, yMinLimit, yMaxLimit);

            // Procedural recoil & screen shake recovery
            _recoilOffset = Vector2.Lerp(_recoilOffset, Vector2.zero, Time.deltaTime * recoilRecoverySpeed);
            _shakeOffset = Vector3.Lerp(_shakeOffset, Vector3.zero, Time.deltaTime * 16f);
            _recoilRoll = Mathf.Lerp(_recoilRoll, 0f, Time.deltaTime * recoilRecoverySpeed);
            _kickbackDistance = Mathf.Lerp(_kickbackDistance, 0f, Time.deltaTime * 10f);

            if (_gunSystem != null && _gunSystem.IsGunEquipped &&
                (Input.GetMouseButton(1) || Input.GetMouseButton(0)))
            {
                isAiming = true;
            }
        }
        else
        {
            mouseY = Mathf.Clamp(editorPreviewPitch, yMinLimit, yMaxLimit);
        }

        // Smoothly transition between normal and aim states
        float targetDist = isAiming ? aimDistance : distance;
        Vector3 targetOff = isAiming ? aimOffset : targetOffset;

        if (Application.isPlaying)
        {
            _currentDistance = Mathf.Lerp(_currentDistance, targetDist, Time.deltaTime * zoomSpeed);
            _currentOffset = Vector3.Lerp(_currentOffset, targetOff, Time.deltaTime * zoomSpeed);
            _cinematicBlend = Mathf.Lerp(_cinematicBlend, _isCinematic ? 1f : 0f, Time.deltaTime * zoomSpeed);
        }
        else
        {
            _currentDistance = targetDist;
            _currentOffset = targetOff;
            _cinematicBlend = 0f;
        }

        // Blend yaw and pitch for cinematic mode + recoil
        float orbitYaw = Application.isPlaying ? mouseX : target.eulerAngles.y;
        float finalYaw = orbitYaw + Mathf.Lerp(0f, _cinematicYaw, _cinematicBlend) + _recoilOffset.y;
        float finalPitch = Mathf.Clamp(Mathf.Lerp(mouseY, _cinematicPitch, _cinematicBlend) + _recoilOffset.x, yMinLimit, yMaxLimit);

        if (_isCinematic)
        {
            // Override distance and offset with cinematic blend
            _currentDistance = Mathf.Lerp(_currentDistance, _cinematicDistance, _cinematicBlend);
            _currentOffset = Vector3.Lerp(_currentOffset, _cinematicOffset, _cinematicBlend);
        }

        // Camera ka rotation (with recoil roll)
        Quaternion rotation = Quaternion.Euler(finalPitch, finalYaw, _recoilRoll);
        
        float scale = target.lossyScale.y;

        // Apply offset (right/up/forward relative to player rotation)
        Vector3 actualTargetOffset = target.position 
                                     + (Vector3.up * _currentOffset.y * scale) 
                                     + (rotation * new Vector3(_currentOffset.x * scale, 0, _currentOffset.z * scale));
        
        float scaledDistance = _currentDistance * scale;
        Vector3 desiredPosition = actualTargetOffset - (rotation * Vector3.forward * (scaledDistance + _kickbackDistance));

        if (Application.isPlaying)
        {
            float finalDistance = scaledDistance;
            Vector3 directionToCamera = (desiredPosition - actualTargetOffset).normalized;
            
            int mask = collisionMask.value != 0 ? collisionMask.value : ~LayerMask.GetMask("Player", "Ignore Raycast");
            if (Physics.SphereCast(actualTargetOffset, 0.18f, directionToCamera, out RaycastHit hit, scaledDistance, mask, QueryTriggerInteraction.Ignore))
            {
                finalDistance = Mathf.Clamp(hit.distance - 0.12f, minDistance, scaledDistance);
                desiredPosition = actualTargetOffset - (rotation * Vector3.forward * finalDistance);
            }

            // Smooth movement + camera shake impulse
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentPosVelocity, positionSmoothTime) + _shakeOffset;
        }
        else
        {
            transform.position = desiredPosition;
        }

        if (_exitStandBlendTimer > 0f)
        {
            _exitStandBlendTimer -= Time.deltaTime;
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.deltaTime * 8f);
        }
        else
        {
            transform.rotation = rotation;
        }
    }

    // ─── Ladder Camera Mode ───────────────────────────────────────────────────
    // Jab player ladder chadh raha ho tab camera player ke peeche close distance pe lock ho jata hai.
    // Mouse input block ho jata hai, aur camera smoothly player ke peeche aa jata hai.

    [Header("── Ladder Camera Position Settings (Climb Mode) ──")]
    [Tooltip("Camera Distance (Pichhe / Door-i): Player se camera kitna peeche rahega. Default: 1.6f")]
    [SerializeField] private float ladderDistance = 1.6f;

    [Tooltip("Daye (+) / Baye (-) Offset: Right (+) ya Left (-) camera position shift. Default: 0f")]
    [SerializeField] private float ladderDayeBayeOffset = 0f;

    [Tooltip("Upar (+) / Neeche (-) Offset: Camera ki height adjustment. Default: 0.75f")]
    [SerializeField] private float ladderUparNeecheOffset = 0.75f;

    [Tooltip("Aage (+) / Pichhe (-) Offset: Forward (+) ya Backward (-) camera position shift. Default: 0f")]
    [SerializeField] private float ladderAagePichheOffset = 0f;

    [Tooltip("Optional combined 3D Vector position offset (X: Daye/Baye, Y: Upar/Neeche, Z: Aage/Pichhe)")]
    [SerializeField] private Vector3 ladderOffset = new Vector3(0f, 0f, 0f);

    [Header("── Ladder Camera Rotation Settings (Axes) ──")]
    [Tooltip("X Axis (Pitch Angle): Upar / Neeche dekhne ka angle (degrees). Default: 14f")]
    [SerializeField] private float ladderRotationX = 14f;

    [Tooltip("Y Axis (Yaw Angle Offset): Daye / Baye ghoomne ka angle offset (degrees). 0 = seedha peeth ke peeche, +/- angle = side angled view. Default: 0f")]
    [SerializeField] private float ladderRotationY = 0f;

    [Tooltip("Z Axis (Roll Angle): Camera ko tircha / tilt karne ka angle (degrees). Default: 0f")]
    [SerializeField] private float ladderRotationZ = 0f;

    [Tooltip("Optional combined 3D Vector axis rotation (X: Pitch, Y: Yaw, Z: Roll)")]
    [SerializeField] private Vector3 ladderRotation = Vector3.zero;

    private bool _isLadderMode = false;

    public float LadderDistance { get => ladderDistance; set => ladderDistance = value; }
    public float LadderDayeBayeOffset { get => ladderDayeBayeOffset; set => ladderDayeBayeOffset = value; }
    public float LadderUparNeecheOffset { get => ladderUparNeecheOffset; set => ladderUparNeecheOffset = value; }
    public float LadderAagePichheOffset { get => ladderAagePichheOffset; set => ladderAagePichheOffset = value; }
    public float LadderPitch { get => ladderRotationX; set => ladderRotationX = value; }
    public float LadderRotationX { get => ladderRotationX; set => ladderRotationX = value; }
    public float LadderRotationY { get => ladderRotationY; set => ladderRotationY = value; }
    public float LadderRotationZ { get => ladderRotationZ; set => ladderRotationZ = value; }
    public Vector3 LadderOffset { get => ladderOffset; set => ladderOffset = value; }
    public Vector3 LadderRotation { get => ladderRotation; set => ladderRotation = value; }

    /// <summary>
    /// Ladder chadhna shuru → camera player ke PEECHE lock ho jata hai.
    /// playerTransform: player ka transform (facing ladder direction already set hai).
    /// </summary>
    public void EnterLadderMode(Transform playerTransform)
    {
        _isLadderMode = true;
        if (playerTransform != null)
        {
            // Player ladder face kar raha hai.
            // Camera ko player ki PEETH (back) se dikhana hai.
            //
            // Camera position formula:  desiredPos = target - (Euler(pitch,yaw,0)*forward) * dist
            // Matlab camera wahan hota hai jo yaw OPPOSITE direction mein ho player ke relative.
            //
            // Player facing yaw = playerYaw  (e.g. 270° = west)
            // Camera peeche banana ke liye:  camera yaw = playerYaw + 180°
            // Phir desiredPos = target - forward(playerYaw+180) * dist
            //                 = target - (-forward(playerYaw)) * dist
            //                 = target + forward(playerYaw) * dist
            //                 = player ke peeche (same direction player face kar raha hai) ✓
            float playerYaw = playerTransform.eulerAngles.y;
            float targetYaw = playerYaw + 180f + ladderRotationY + ladderRotation.y;
            mouseX = targetYaw;
            mouseY = ladderRotationX + ladderRotation.x;
        }
    }

    /// <summary>
    /// Ladder se utarna → normal free-look camera wapas.
    /// </summary>
    public void ExitLadderMode()
    {
        _isLadderMode = false;
        // mouseX / mouseY wahi rehenge jahan the — seamless transition
    }

    private void RunLadderCamera()
    {
        if (target == null) return;

        // Ladder mode mein mouse input bilkul block — camera lock rehega peeche
        // (koi mouse X/Y delta nahi lete)
        // Recoil aur shake bhi apply nahi karte ladder pe

        // Smooth pitch recovery to desired ladder pitch (X-axis rotation)
        float targetPitch = ladderRotationX + ladderRotation.x;
        mouseY = Mathf.Lerp(mouseY, targetPitch, Time.deltaTime * 5f);

        // Yaw (Y-axis rotation): player ki PEETH + Y axis rotation offset
        float desiredYaw = target.eulerAngles.y + 180f + ladderRotationY + ladderRotation.y;
        mouseX = Mathf.LerpAngle(mouseX, desiredYaw, Time.deltaTime * 6f);

        // Roll (Z-axis rotation): camera horizon tilt
        float currentRoll = ladderRotationZ + ladderRotation.z;

        Quaternion rotation = Quaternion.Euler(mouseY, mouseX, currentRoll);

        float scale = target.lossyScale.y;

        // Calculate offsets relative to camera orientation:
        // X = Daye (+) / Baye (-)
        // Y = Upar (+) / Neeche (-)
        // Z = Aage (+) / Pichhe (-)
        float sideX   = (ladderDayeBayeOffset   + ladderOffset.x) * scale;
        float heightY = (ladderUparNeecheOffset + ladderOffset.y) * scale;
        float fwdZ    = (ladderAagePichheOffset + ladderOffset.z) * scale;

        Vector3 actualTargetOffset = target.position 
                                     + (Vector3.up * heightY) 
                                     + (rotation * new Vector3(sideX, 0f, fwdZ));

        float scaledDist = ladderDistance * scale;
        Vector3 desiredPos = actualTargetOffset - (rotation * Vector3.forward * scaledDist);

        // Smooth camera move + collision
        int mask = collisionMask.value != 0 ? collisionMask.value : ~LayerMask.GetMask("Player", "Ignore Raycast");
        Vector3 dir = (desiredPos - actualTargetOffset).normalized;
        if (Physics.SphereCast(actualTargetOffset, 0.18f, dir, out RaycastHit hit, scaledDist, mask, QueryTriggerInteraction.Ignore))
        {
            float clampedDist = Mathf.Clamp(hit.distance - 0.12f, minDistance, scaledDist);
            desiredPos = actualTargetOffset - (rotation * Vector3.forward * clampedDist);
        }

        transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref currentPosVelocity, positionSmoothTime);
        if (_exitStandBlendTimer > 0f)
        {
            _exitStandBlendTimer -= Time.deltaTime;
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.deltaTime * 8f);
        }
        else
        {
            transform.rotation = rotation;
        }
    }

    // ─── Boat Camera Core ────────────────────────────────────────────────────
    private void RunBoatCamera()
    {
        if (_hasBoatPose)
        {
            if (_snapBoatPose)
            {
                currentPosVelocity = Vector3.zero;
                transform.SetPositionAndRotation(_boatCamPos, _boatCamRot);
                _snapBoatPose = false;
                return;
            }

            transform.position = Vector3.SmoothDamp(transform.position, _boatCamPos, ref currentPosVelocity, 0.045f);
            transform.rotation = Quaternion.Slerp(transform.rotation, _boatCamRot, Time.deltaTime * 16f);
            return;
        }

        if (_boatTransform == null) return;

        Vector3 boatFwd = _boatForwardFunc != null ? _boatForwardFunc() : _boatTransform.forward;
        boatFwd.y = 0f;
        if (boatFwd.sqrMagnitude < 0.001f) boatFwd = Vector3.forward;
        boatFwd.Normalize();

        float boatHeading = Mathf.Atan2(boatFwd.x, boatFwd.z) * Mathf.Rad2Deg;
        if (!_hasBoatYaw)
        {
            _currentBoatYaw = boatHeading;
            _hasBoatYaw = true;
        }

        _currentBoatYaw = Mathf.LerpAngle(_currentBoatYaw, boatHeading, Time.deltaTime * 6f);
        Quaternion rotation = Quaternion.Euler(_boatPitch, _currentBoatYaw, 0f);
        Vector3 pivotPos = _boatTransform.position - boatFwd * 1.2f + Vector3.up * _boatOffset.y;
        Vector3 desiredPos = pivotPos - (rotation * Vector3.forward * _boatDistance);

        transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref currentPosVelocity, 0.05f);
        transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.deltaTime * 10f);
        mouseX = _currentBoatYaw;
        mouseY = _boatPitch;
    }

    // ─── Stand Interaction Camera (Side View & Puzzle View) ──────────────────
    [Header("── Stand Interaction Camera Settings ──")]
    [Tooltip("Horizontal distance back from the stand socket (meters) to view the FULL plate")]
    [SerializeField] public float standCamDistance = 3.5f;

    [Tooltip("Vertical height above the stand socket (meters) to view the FULL plate")]
    [SerializeField] public float standCamHeight = 3.8f;

    [Tooltip("Target look-at height offset above stand socket (meters)")]
    [SerializeField] public float standCamFocusOffset = 0.05f;

    [Tooltip("Side distance from the stand focus point (meters)")]
    [SerializeField] private float standCamSideOffset = 2.1f;

    [Tooltip("Camera height above ground (meters) - shows upper body of player + stand")]
    [SerializeField] private float standCamHeightOffset = 1.35f;

    [Tooltip("Forward/back offset relative to player facing the stand")]
    [SerializeField] private float standCamForwardOffset = -0.2f;

    private bool _isStandMode = false;
    private Transform _standSocketTransform;
    private Transform _standPlayerTransform;
    private Transform _cameraStandPoint;
    private Vector3 _standCamVelocity = Vector3.zero;

    private float _exitStandBlendTimer = 0f;

    public void EnterStandMode(Transform standSocket, Transform playerTransform, Transform cameraStandPoint = null)
    {
        _isStandMode = true;
        _standSocketTransform = standSocket;
        _standPlayerTransform = playerTransform != null ? playerTransform : target;
        _cameraStandPoint = cameraStandPoint;
        _standCamVelocity = Vector3.zero;
        _exitStandBlendTimer = 0f;
    }

    public void ExitStandMode()
    {
        _isStandMode = false;
        _standSocketTransform = null;
        _standPlayerTransform = null;
        _cameraStandPoint = null;
        _exitStandBlendTimer = 0.8f;

        if (target == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) target = p.transform;
            else
            {
                var pc = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                if (pc != null) target = pc.transform;
            }
        }

        if (target != null)
        {
            mouseX = target.eulerAngles.y;
            mouseY = 15f;
        }
    }

    private void RunStandCamera()
    {
        Vector3 standSocketPos = (_standSocketTransform != null) ? _standSocketTransform.position : transform.position;

        // Player direction relative to socket (direction towards player standpoint)
        Vector3 awayDir = (_standPlayerTransform != null) ? (_standPlayerTransform.position - standSocketPos) : Vector3.forward;
        awayDir.y = 0f;
        if (awayDir.sqrMagnitude < 0.01f) awayDir = Vector3.forward;
        awayDir.Normalize();

        // Optimal camera position so the FULL circular plate, rim, and arrows are completely visible!
        // ("plate puri dikhe vese kardo")
        Vector3 idealCamPos = standSocketPos + awayDir * standCamDistance + Vector3.up * standCamHeight;
        Vector3 targetCamPos = idealCamPos;

        if (_cameraStandPoint != null)
        {
            Vector3 rawStandPointPos = _cameraStandPoint.position;
            float rawDistAbove = rawStandPointPos.y - standSocketPos.y;

            // Agar scale 500x ki wajah se standpoint extreme height par ho (> 6m ya < 1.2m),
            // toh idealCamPos use karein taaki plate poori frame mein crisp aur complete dikhe:
            if (rawDistAbove > 5.5f || rawDistAbove < 1.2f)
            {
                targetCamPos = idealCamPos;
            }
            else
            {
                // Ensure horizontal distance from socket is sufficient taaki camera plate ke bohot paas zoom-in hoke cut na ho
                Vector2 horizDiff = new Vector2(rawStandPointPos.x - standSocketPos.x, rawStandPointPos.z - standSocketPos.z);
                if (horizDiff.magnitude < standCamDistance * 0.75f)
                {
                    targetCamPos = idealCamPos;
                }
                else
                {
                    targetCamPos = rawStandPointPos;
                }
            }
        }

        // Look directly at the center of the stand socket / plate dial
        Vector3 focusPoint = standSocketPos + Vector3.up * standCamFocusOffset;
        Vector3 standLookDir = focusPoint - targetCamPos;
        Quaternion standTargetRot = (standLookDir.sqrMagnitude > 0.01f)
            ? Quaternion.LookRotation(standLookDir.normalized, Vector3.up)
            : Quaternion.Euler(48f, (_standPlayerTransform != null ? _standPlayerTransform.eulerAngles.y + 180f : 0f), 0f);

        transform.position = Vector3.SmoothDamp(transform.position, targetCamPos, ref _standCamVelocity, 0.2f);
        transform.rotation = Quaternion.Slerp(transform.rotation, standTargetRot, Time.deltaTime * 10f);

        // Sync mouse yaw aur pitch taaki exit hone par camera smooth return ho
        if (target != null)
        {
            mouseX = target.eulerAngles.y;
            mouseY = 15f;
        }
    }
}
