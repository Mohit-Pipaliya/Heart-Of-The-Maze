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

        transform.rotation = rotation;
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
}
