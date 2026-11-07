using UnityEngine;

/// <summary>
/// AAA Standalone Third-Person Boat Camera.
/// - Placed behind (6m) and above (3m) looking forward along the boat.
/// - Isolates boat pitch/roll jitter; smoothly follows yaw heading only.
/// - Smooth vertical dampening preventing dizzy wave bobbing.
/// - Mouse orbit with auto-recenter behind boat.
/// - Collision avoidance (SphereCast) & water surface clamping.
/// - Dynamic speed FOV and subtle impact shake.
/// </summary>
public class BoatFollowCamera : MonoBehaviour
{
    [Header("Boat Target")]
    [SerializeField] private Transform boatTransform;
    [SerializeField] private RealisticBoatBuoyancy boatBuoyancy;

    [Header("Framing Settings (Exposed Defaults)")]
    [Tooltip("Distance behind the boat (meters)")]
    public float distanceBehind = 6.0f;

    [Tooltip("Height above the boat (meters)")]
    public float heightAbove = 3.0f;

    [Tooltip("Look-at point height offset above boat (meters)")]
    public float lookAtHeight = 1.2f;

    [Tooltip("Look-at forward lead distance ahead of boat (meters)")]
    public float lookAtForwardLead = 3.0f;

    [Tooltip("Base Field of View")]
    public float baseFOV = 60f;

    [Tooltip("Maximum FOV increase at top speed")]
    public float speedFOVBoost = 8f;

    [Header("Smoothing & Stabilization")]
    [Tooltip("Yaw heading follow smoothness")]
    public float yawDamping = 4.5f;

    [Tooltip("Vertical height follow smoothness (slow to filter wave bobbing)")]
    public float heightDamping = 2.2f;

    [Tooltip("Position translation smoothness")]
    public float positionDamping = 8.0f;

    [Header("Mouse Orbit & Auto-Recentering")]
    public bool enableMouseOrbit = true;
    public float mouseSensitivity = 2.2f;
    public float autoRecenterDelay = 1.8f;
    public float autoRecenterSpeed = 3.5f;

    [Header("Collision & Water Clamping")]
    public LayerMask collisionLayers = ~0; // Everything except triggers
    public float cameraCollisionRadius = 0.28f;
    public float minDistance = 2.0f;
    public float waterSurfaceClearance = 0.45f;

    private Camera _cam;
    private float _currentYaw;
    private float _currentHeight;
    private float _orbitYawOffset;
    private float _orbitPitchOffset;
    private float _lastMouseInputTime;
    private float _shakeIntensity;

    public void SetTarget(Transform boat, RealisticBoatBuoyancy buoyancy = null)
    {
        boatTransform = boat;
        boatBuoyancy = buoyancy ?? boat.GetComponent<RealisticBoatBuoyancy>();
        if (boatTransform != null)
        {
            _currentYaw = boatTransform.eulerAngles.y;
            _currentHeight = boatTransform.position.y + heightAbove;
        }
    }

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        if (_cam == null) _cam = Camera.main;
    }

    private void Start()
    {
        if (boatTransform != null)
        {
            _currentYaw = boatTransform.eulerAngles.y;
            _currentHeight = boatTransform.position.y + heightAbove;
        }
    }

    private void LateUpdate()
    {
        if (boatTransform == null) return;

        HandleMouseOrbit();

        // 1. Follow only yaw heading — ignore boat's rapid pitch and roll
        float targetYaw = boatTransform.eulerAngles.y + _orbitYawOffset;
        _currentYaw = Mathf.LerpAngle(_currentYaw, targetYaw, Time.deltaTime * yawDamping);

        // 2. Smooth height separately and slowly (completely eliminates wave heave jitter)
        float targetHeight = boatTransform.position.y + heightAbove;
        _currentHeight = Mathf.Lerp(_currentHeight, targetHeight, Time.deltaTime * heightDamping);

        // 3. Compute desired camera position behind boat
        Quaternion flatRotation = Quaternion.Euler(0f, _currentYaw, 0f);
        Vector3 boatPos = boatTransform.position;
        Vector3 desiredCamPos = boatPos - (flatRotation * Vector3.forward * distanceBehind);
        desiredCamPos.y = _currentHeight + _orbitPitchOffset;

        // 4. Look-at point: 1.2m above boat, 3m ahead of boat
        Vector3 boatFwd = flatRotation * Vector3.forward;
        Vector3 lookTarget = boatPos + Vector3.up * lookAtHeight + boatFwd * lookAtForwardLead;

        // 5. Camera Collision (SphereCast from lookTarget to desiredCamPos)
        Vector3 fromLookToCam = desiredCamPos - lookTarget;
        float currentDist = fromLookToCam.magnitude;
        Vector3 rayDir = fromLookToCam.normalized;

        if (Physics.SphereCast(lookTarget, cameraCollisionRadius, rayDir, out RaycastHit hit, currentDist, collisionLayers, QueryTriggerInteraction.Ignore))
        {
            float hitDistance = Mathf.Max(hit.distance - 0.1f, minDistance);
            desiredCamPos = lookTarget + rayDir * hitDistance;
        }

        // 6. Water Surface Clamping: never dip below water surface
        float waterHeightAtCam = GerstnerWaveMath.SampleWaterHeight(desiredCamPos, Time.time);
        if (desiredCamPos.y < waterHeightAtCam + waterSurfaceClearance)
        {
            desiredCamPos.y = waterHeightAtCam + waterSurfaceClearance;
        }

        // 7. Apply subtle splash shake if active
        if (_shakeIntensity > 0.001f)
        {
            desiredCamPos += Random.insideUnitSphere * _shakeIntensity;
            _shakeIntensity = Mathf.Lerp(_shakeIntensity, 0f, Time.deltaTime * 6f);
        }

        transform.position = Vector3.Lerp(transform.position, desiredCamPos, Time.deltaTime * positionDamping);
        transform.rotation = Quaternion.LookRotation((lookTarget - transform.position).normalized, Vector3.up);

        // 8. Dynamic Speed FOV
        if (_cam != null && boatBuoyancy != null)
        {
            float speed01 = Mathf.Clamp01(boatBuoyancy.CurrentSpeed / 12f);
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, baseFOV + (speed01 * speedFOVBoost), Time.deltaTime * 4f);
        }
    }

    private void HandleMouseOrbit()
    {
        if (!enableMouseOrbit) return;

        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        if (Mathf.Abs(mouseX) > 0.01f || Mathf.Abs(mouseY) > 0.01f)
        {
            _orbitYawOffset += mouseX * mouseSensitivity;
            _orbitPitchOffset = Mathf.Clamp(_orbitPitchOffset - mouseY * mouseSensitivity * 0.5f, -1.8f, 2.5f);
            _lastMouseInputTime = Time.time;
        }
        else if (Time.time - _lastMouseInputTime > autoRecenterDelay)
        {
            // Auto recenter behind boat
            _orbitYawOffset = Mathf.Lerp(_orbitYawOffset, 0f, Time.deltaTime * autoRecenterSpeed);
            _orbitPitchOffset = Mathf.Lerp(_orbitPitchOffset, 0f, Time.deltaTime * autoRecenterSpeed);
        }
    }

    public void TriggerSplashShake(float intensity = 0.25f)
    {
        _shakeIntensity = Mathf.Max(_shakeIntensity, intensity);
    }
}
