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
    public float recoilRecoverySpeed = 14f;
    private Vector2 _recoilOffset = Vector2.zero;
    private Vector3 _shakeOffset = Vector3.zero;

    public static CameraController Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    public void AddRecoil(float pitchKick, float yawKick, float shakeAmount)
    {
        _recoilOffset.x -= pitchKick; // Kick up
        _recoilOffset.y += yawKick;   // Random horizontal sway
        _shakeOffset += Random.insideUnitSphere * shakeAmount;
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
            _shakeOffset = Vector3.Lerp(_shakeOffset, Vector3.zero, Time.deltaTime * 20f);

            // Check if player is aiming with gun
            if (_gunSystem != null && _gunSystem.IsGunEquipped && Input.GetMouseButton(1))
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

        // Camera ka rotation
        Quaternion rotation = Quaternion.Euler(finalPitch, finalYaw, 0);
        
        float scale = target.lossyScale.y;

        // Apply offset (right/up/forward relative to player rotation)
        Vector3 actualTargetOffset = target.position 
                                     + (Vector3.up * _currentOffset.y * scale) 
                                     + (rotation * new Vector3(_currentOffset.x * scale, 0, _currentOffset.z * scale));
        
        float scaledDistance = _currentDistance * scale;
        Vector3 desiredPosition = actualTargetOffset - (rotation * Vector3.forward * scaledDistance);

        if (Application.isPlaying)
        {
            float finalDistance = scaledDistance;
            Vector3 directionToCamera = (desiredPosition - actualTargetOffset).normalized;
            
            // Wall Collision Check
            if (Physics.SphereCast(actualTargetOffset, 0.2f, directionToCamera, out RaycastHit hit, scaledDistance, collisionMask))
            {
                finalDistance = Mathf.Clamp(hit.distance, minDistance, scaledDistance);
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
}
