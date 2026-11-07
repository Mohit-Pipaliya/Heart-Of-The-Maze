using UnityEngine;

/// <summary>
/// Smooths camera follow tracking by filtering high-frequency pitch and roll wave bobbing,
/// preventing motion sickness while keeping the camera firmly behind the boat heading.
/// </summary>
public class BoatCameraTarget : MonoBehaviour
{
    [Tooltip("Target boat transform to follow.")]
    [SerializeField] private Transform boatTransform;
    [Tooltip("Translation follow smoothness (lower = smoother).")]
    [SerializeField] private float translationDamping = 0.12f;
    [Tooltip("Heading yaw smoothness.")]
    [SerializeField] private float yawDamping = 0.18f;

    private Vector3 _currentVelocity;
    private float _currentYaw;
    private float _yawVelocity;

    public void SetBoat(Transform boat)
    {
        boatTransform = boat;
        if (boatTransform != null)
        {
            transform.position = boatTransform.position;
            _currentYaw = boatTransform.eulerAngles.y;
        }
    }

    private void Start()
    {
        if (boatTransform != null)
        {
            transform.position = boatTransform.position;
            _currentYaw = boatTransform.eulerAngles.y;
        }
    }

    private void LateUpdate()
    {
        if (boatTransform == null) return;

        // 1. Smooth position tracking (removes sudden vertical wave spikes)
        transform.position = Vector3.SmoothDamp(transform.position, boatTransform.position, ref _currentVelocity, translationDamping);

        // 2. Smooth heading yaw (zero out pitch and roll completely)
        float targetYaw = boatTransform.eulerAngles.y;
        _currentYaw = Mathf.SmoothDampAngle(_currentYaw, targetYaw, ref _yawVelocity, yawDamping);
        transform.rotation = Quaternion.Euler(0f, _currentYaw, 0f);
    }
}
