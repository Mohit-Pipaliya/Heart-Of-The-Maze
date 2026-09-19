using UnityEngine;

[ExecuteAlways]
public class CameraController : MonoBehaviour
{
    [Header("Target & Positioning")]
    public Transform target;
    public float distance = 4.0f;
    public Vector3 targetOffset = new Vector3(0f, 1.5f, 0f); 

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

    private float mouseY = 0.0f;
    private Vector3 currentPosVelocity;

    private void Start()
    {
        if (Application.isPlaying && target != null)
        {
            float startPitch = transform.eulerAngles.x;
            if (startPitch > 180f) startPitch -= 360f;
            mouseY = startPitch;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        if (Application.isPlaying)
        {
            // Mouse Y (Pitch up/down)
            float mouseYInput = Input.GetAxis("Mouse Y") * mouseSensitivity;
            mouseY -= mouseYInput;
            mouseY = Mathf.Clamp(mouseY, yMinLimit, yMaxLimit);
        }
        else
        {
            mouseY = Mathf.Clamp(editorPreviewPitch, yMinLimit, yMaxLimit);
        }

        // Camera ka Y (Left/Right) rotation hamesha Target (Player) ke Y rotation ke barabar rahega!
        // Isse camera hamesha player ke pichhe 100% locked rahega.
        Quaternion rotation = Quaternion.Euler(mouseY, target.eulerAngles.y, 0);
        
        float scale = target.lossyScale.y;

        Vector3 actualTargetOffset = target.position + (Vector3.up * targetOffset.y * scale) + (rotation * new Vector3(targetOffset.x * scale, 0, targetOffset.z * scale));
        float scaledDistance = distance * scale;
        Vector3 desiredPosition = actualTargetOffset - (rotation * Vector3.forward * scaledDistance);

        if (Application.isPlaying)
        {
            float currentDistance = scaledDistance;
            Vector3 directionToCamera = (desiredPosition - actualTargetOffset).normalized;
            
            if (Physics.SphereCast(actualTargetOffset, 0.2f, directionToCamera, out RaycastHit hit, scaledDistance, collisionMask))
            {
                currentDistance = Mathf.Clamp(hit.distance, minDistance, scaledDistance);
                desiredPosition = actualTargetOffset - (rotation * Vector3.forward * currentDistance);
            }

            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentPosVelocity, positionSmoothTime);
        }
        else
        {
            transform.position = desiredPosition;
        }

        transform.rotation = rotation;
    }
}
