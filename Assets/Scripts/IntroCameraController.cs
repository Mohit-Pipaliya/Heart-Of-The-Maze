using System.Collections;
using UnityEngine;

public class IntroCameraController : MonoBehaviour
{
    private enum CameraMode
    {
        Gameplay,
        EyePoint,
        Returning,
        FollowPlayerOffset
    }

    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private Transform playerRoot;
    [SerializeField] private Behaviour gameplayCameraController;

    private CameraMode _mode = CameraMode.Gameplay;
    private Vector3 _playerLocalCameraPosition;
    private Quaternion _playerLocalCameraRotation;
    private Vector3 _returnStartPosition;
    private Quaternion _returnStartRotation;
    private float _returnDuration;
    private float _returnElapsed;
    private bool _cameraControllerWasEnabled;
    private bool _brainWasEnabled;
    private Behaviour _cinemachineBrain;

    private void Awake()
    {
        ResolveReferences();
    }

    public void Configure(Camera camera, Transform player, Behaviour cameraController)
    {
        gameplayCamera = camera;
        playerRoot = player;
        gameplayCameraController = cameraController;
        ResolveReferences();
    }

    public void BeginIntro()
    {
        ResolveReferences();
        if (gameplayCamera == null)
        {
            Debug.LogError($"[{nameof(IntroCameraController)}] No gameplay Camera was found.", this);
            return;
        }

        if (playerRoot != null)
        {
            _playerLocalCameraPosition = playerRoot.InverseTransformPoint(gameplayCamera.transform.position);
            _playerLocalCameraRotation = Quaternion.Inverse(playerRoot.rotation) * gameplayCamera.transform.rotation;
        }

        _cameraControllerWasEnabled = gameplayCameraController != null && gameplayCameraController.enabled;
        if (gameplayCameraController != null)
            gameplayCameraController.enabled = false;

        _brainWasEnabled = _cinemachineBrain != null && _cinemachineBrain.enabled;
        if (_cinemachineBrain != null)
            _cinemachineBrain.enabled = false;

        _mode = CameraMode.EyePoint;
    }

    public IEnumerator BlendBackToNormal(float duration)
    {
        if (gameplayCamera == null)
            yield break;

        _returnStartPosition = gameplayCamera.transform.position;
        _returnStartRotation = gameplayCamera.transform.rotation;
        _returnElapsed = 0f;
        _returnDuration = Mathf.Max(0.01f, duration);
        _mode = CameraMode.Returning;

        while (_mode == CameraMode.Returning)
            yield return null;
    }

    public void CompleteIntro()
    {
        _mode = CameraMode.Gameplay;
        if (_cinemachineBrain != null && _brainWasEnabled)
            _cinemachineBrain.enabled = true;
        if (gameplayCameraController != null && _cameraControllerWasEnabled)
            gameplayCameraController.enabled = true;
    }

    private void LateUpdate()
    {
        if (gameplayCamera == null)
            return;

        switch (_mode)
        {
            case CameraMode.EyePoint:
                gameplayCamera.transform.SetPositionAndRotation(transform.position, transform.rotation);
                break;

            case CameraMode.Returning:
                _returnElapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_returnElapsed / _returnDuration));
                GetPlayerOffsetPose(out Vector3 targetPosition, out Quaternion targetRotation);
                gameplayCamera.transform.SetPositionAndRotation(
                    Vector3.Lerp(_returnStartPosition, targetPosition, t),
                    Quaternion.Slerp(_returnStartRotation, targetRotation, t));

                if (t >= 1f)
                    _mode = CameraMode.FollowPlayerOffset;
                break;

            case CameraMode.FollowPlayerOffset:
                GetPlayerOffsetPose(out Vector3 followPosition, out Quaternion followRotation);
                gameplayCamera.transform.SetPositionAndRotation(followPosition, followRotation);
                break;
        }
    }

    private void ResolveReferences()
    {
        if (gameplayCamera == null)
            gameplayCamera = Camera.main;

        if (playerRoot == null)
        {
            PlayerController player = GetComponentInParent<PlayerController>();
            if (player != null)
                playerRoot = player.transform;
        }

        if (gameplayCameraController == null)
        {
            CameraController cameraController = CameraController.Instance;
            if (cameraController == null)
                cameraController = FindFirstObjectByType<CameraController>();
            gameplayCameraController = cameraController;
        }

        if (_cinemachineBrain == null && gameplayCamera != null)
        {
            System.Type brainType = System.Type.GetType("Unity.Cinemachine.CinemachineBrain, Unity.Cinemachine");
            if (brainType != null)
                _cinemachineBrain = gameplayCamera.GetComponent(brainType) as Behaviour;
        }
    }

    private void GetPlayerOffsetPose(out Vector3 position, out Quaternion rotation)
    {
        if (playerRoot == null)
        {
            position = gameplayCamera.transform.position;
            rotation = gameplayCamera.transform.rotation;
            return;
        }

        position = playerRoot.TransformPoint(_playerLocalCameraPosition);
        rotation = playerRoot.rotation * _playerLocalCameraRotation;
    }
}
