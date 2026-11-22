using UnityEngine;

[RequireComponent(typeof(Collider))]
public class GeneratorLightPuzzle : MonoBehaviour
{
    private enum PuzzleState
    {
        Idle,
        WalkingToWheel,
        RotatingWheel,
        Completed
    }

    [Header("Scene References")]
    [SerializeField] private DayNightController dayNightController;
    [SerializeField] private Transform wheelStandPoint;
    [SerializeField] private Transform cameraStandPoint;
    [SerializeField] private Transform wheelPivot;
    [SerializeField] private Transform wheelMarker;
    [SerializeField] private Transform targetWheelMarker;
    [SerializeField] private Light[] poweredLights;

    [Header("Interaction")]
    [SerializeField, Min(0.1f)] private float walkSpeed = 4.2f;
    [SerializeField, Min(1f)] private float walkTimeout = 20f;
    [SerializeField, Min(1f)] private float wheelRotationSpeed = 75f;
    [SerializeField, Min(0.1f)] private float alignmentAngleThreshold = 8f;
    [SerializeField] private Vector3 wheelLocalRotationAxis = Vector3.forward;

    private PuzzleState _state;
    private PlayerController _player;
    private CharacterController _characterController;
    private Animator _animator;
    private CameraController _cameraController;
    private float _walkElapsed;
    private float _currentPuzzleAngle;
    private float _signedPuzzleAngle;
    private float _lastPuzzleAngle;
    private bool _isMovingCloser;
    private bool _isRotatingKeyPressed;

    private bool IsNight => dayNightController != null && dayNightController.IsNight;

    private void Awake()
    {
        Collider trigger = GetComponent<Collider>();
        if (!trigger.isTrigger)
        {
            Debug.LogError($"[{nameof(GeneratorLightPuzzle)}] The generator interaction collider must have Is Trigger enabled.", this);
            enabled = false;
            return;
        }

        if (dayNightController == null)
            dayNightController = FindFirstObjectByType<DayNightController>();

        SetPoweredLights(false);
    }

    private void Start()
    {
        if (dayNightController == null)
            Debug.LogError($"[{nameof(GeneratorLightPuzzle)}] Assign a DayNightController or add one to the scene.", this);

        if (wheelStandPoint == null || wheelPivot == null || wheelMarker == null || targetWheelMarker == null)
            Debug.LogError($"[{nameof(GeneratorLightPuzzle)}] Assign the wheel stand, wheel pivot, moving marker, and target marker.", this);

        if (poweredLights == null || poweredLights.Length == 0)
            Debug.LogError($"[{nameof(GeneratorLightPuzzle)}] Assign at least one Light to power when the wheel is aligned.", this);

        if (wheelLocalRotationAxis.sqrMagnitude < 0.0001f)
            Debug.LogError($"[{nameof(GeneratorLightPuzzle)}] Wheel rotation axis must not be zero.", this);
    }

    private void Update()
    {
        if (_state == PuzzleState.WalkingToWheel)
        {
            WalkPlayerToWheel();
            return;
        }

        if (_state == PuzzleState.RotatingWheel)
        {
            RotateWheel();
            return;
        }

        if (_state == PuzzleState.Idle && _player != null && IsNight && Input.GetKeyDown(KeyCode.L))
            BeginWheelInteraction();
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || _state == PuzzleState.Completed)
            return;

        _player = player;
        _characterController = player.CharacterController;
        _animator = player.GetComponentInChildren<Animator>();
        _cameraController = CameraController.Instance;
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || player != _player || _state != PuzzleState.Idle)
            return;

        _player = null;
        _characterController = null;
        _animator = null;
    }

    private void BeginWheelInteraction()
    {
        if (!HasWheelSetup())
            return;

        if (_player == null || _characterController == null || !_characterController.enabled)
            return;

        _player.SetFrozen(true);
        if (_animator != null)
        {
            _animator.applyRootMotion = false;
            _animator.SetFloat("Speed", 0f);
        }

        _walkElapsed = 0f;
        _state = PuzzleState.WalkingToWheel;
    }

    private void WalkPlayerToWheel()
    {
        if (_player == null || _characterController == null || !_characterController.enabled)
        {
            CancelInteraction();
            return;
        }

        Vector3 destination = wheelStandPoint.position;
        Vector3 toDestination = destination - _player.transform.position;
        toDestination.y = 0f;

        _walkElapsed += Time.deltaTime;
        if (_walkElapsed >= walkTimeout)
        {
            Debug.LogWarning($"[{nameof(GeneratorLightPuzzle)}] Player could not reach the wheel stand point before the walk timed out.", this);
            CancelInteraction();
            return;
        }

        if (toDestination.sqrMagnitude > 0.01f)
        {
            Vector3 direction = toDestination.normalized;
            _characterController.Move(direction * (walkSpeed * Time.deltaTime));
            _player.transform.rotation = Quaternion.RotateTowards(
                _player.transform.rotation,
                Quaternion.LookRotation(direction, Vector3.up),
                540f * Time.deltaTime);

            if (_animator != null)
                _animator.SetFloat("Speed", 1f);

            return;
        }

        _characterController.Move(toDestination);
        Vector3 facing = wheelPivot.position - _player.transform.position;
        facing.y = 0f;
        if (facing.sqrMagnitude > 0.001f)
            _player.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);

        _player.SetFrozen(true, true);
        if (_animator != null)
        {
            _animator.applyRootMotion = false;
            _animator.SetFloat("Speed", 0f);
        }

        _cameraController = CameraController.Instance;
        if (_cameraController != null)
            _cameraController.EnterStandMode(wheelPivot, _player.transform, cameraStandPoint);
        else
            Debug.LogError($"[{nameof(GeneratorLightPuzzle)}] No CameraController was found; the wheel camera view cannot be enabled.", this);

        if (GetAlignmentError() <= alignmentAngleThreshold + 15f)
            wheelPivot.Rotate(wheelLocalRotationAxis.normalized, 115f, Space.Self);

        _currentPuzzleAngle = GetAlignmentError();
        _signedPuzzleAngle = GetSignedAlignmentAngle();
        _lastPuzzleAngle = _currentPuzzleAngle;
        _isMovingCloser = false;
        _state = PuzzleState.RotatingWheel;
    }

    private void RotateWheel()
    {
        bool rotateLeft = Input.GetKey(KeyCode.A);
        bool rotateRight = Input.GetKey(KeyCode.D);
        _isRotatingKeyPressed = rotateLeft || rotateRight;
        float direction = (rotateRight ? 1f : 0f) - (rotateLeft ? 1f : 0f);

        if (Mathf.Abs(direction) > 0f)
            wheelPivot.Rotate(wheelLocalRotationAxis.normalized, direction * wheelRotationSpeed * Time.deltaTime, Space.Self);

        _currentPuzzleAngle = GetAlignmentError();
        _signedPuzzleAngle = GetSignedAlignmentAngle();

        if (_isRotatingKeyPressed)
        {
            if (_currentPuzzleAngle < _lastPuzzleAngle - 0.02f)
                _isMovingCloser = true;
            else if (_currentPuzzleAngle > _lastPuzzleAngle + 0.02f)
                _isMovingCloser = false;
        }

        _lastPuzzleAngle = _currentPuzzleAngle;

        if (_currentPuzzleAngle <= alignmentAngleThreshold)
            CompletePuzzle();
    }

    private float GetAlignmentError()
    {
        Vector3 axis = wheelPivot.TransformDirection(wheelLocalRotationAxis).normalized;
        Vector3 markerDirection = Vector3.ProjectOnPlane(wheelMarker.position - wheelPivot.position, axis);
        Vector3 targetDirection = Vector3.ProjectOnPlane(targetWheelMarker.position - wheelPivot.position, axis);

        if (markerDirection.sqrMagnitude < 0.0001f || targetDirection.sqrMagnitude < 0.0001f)
            return 180f;

        return Vector3.Angle(markerDirection, targetDirection);
    }

    private float GetSignedAlignmentAngle()
    {
        Vector3 axis = wheelPivot.TransformDirection(wheelLocalRotationAxis).normalized;
        Vector3 markerDirection = Vector3.ProjectOnPlane(wheelMarker.position - wheelPivot.position, axis);
        Vector3 targetDirection = Vector3.ProjectOnPlane(targetWheelMarker.position - wheelPivot.position, axis);

        if (markerDirection.sqrMagnitude < 0.0001f || targetDirection.sqrMagnitude < 0.0001f)
            return 0f;

        return Vector3.SignedAngle(markerDirection, targetDirection, axis);
    }

    private void CompletePuzzle()
    {
        float finalAdjustment = GetSignedAlignmentAngle();
        if (Mathf.Abs(finalAdjustment) > 0.01f)
            wheelPivot.Rotate(wheelLocalRotationAxis.normalized, finalAdjustment, Space.Self);

        SetPoweredLights(true);
        _state = PuzzleState.Completed;

        if (_cameraController != null)
            _cameraController.ExitStandMode();

        if (_player != null)
            _player.SetFrozen(false);

        if (_animator != null)
        {
            _animator.applyRootMotion = false;
            _animator.SetFloat("Speed", 0f);
        }
    }

    private void CancelInteraction()
    {
        if (_cameraController != null)
            _cameraController.ExitStandMode();

        if (_player != null)
            _player.SetFrozen(false);

        if (_animator != null)
        {
            _animator.applyRootMotion = false;
            _animator.SetFloat("Speed", 0f);
        }

        _state = PuzzleState.Idle;
    }

    private bool HasWheelSetup()
    {
        bool configured = wheelStandPoint != null && wheelPivot != null &&
                          wheelMarker != null && targetWheelMarker != null &&
                          wheelLocalRotationAxis.sqrMagnitude >= 0.0001f;
        if (!configured)
            Debug.LogError($"[{nameof(GeneratorLightPuzzle)}] Cannot start: one or more wheel transforms are not assigned.", this);

        return configured;
    }

    private void SetPoweredLights(bool powered)
    {
        if (poweredLights == null)
            return;

        foreach (Light poweredLight in poweredLights)
        {
            if (poweredLight != null)
                poweredLight.enabled = powered;
        }
    }

    private void OnGUI()
    {
        if (_state == PuzzleState.Completed || !IsNight)
            return;

        if (_state == PuzzleState.RotatingWheel)
        {
            bool keyPressed = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.D);
            if (!keyPressed)
            {
                GameHUDPrompt.DrawAlignmentHUD(
                    _currentPuzzleAngle,
                    _signedPuzzleAngle,
                    alignmentAngleThreshold,
                    _currentPuzzleAngle <= alignmentAngleThreshold,
                    _isMovingCloser,
                    false,
                    "GENERATOR WHEEL",
                    new Color(1f, 0.78f, 0.18f));
            }
            return;
        }

        if (_state == PuzzleState.WalkingToWheel)
            return;

        if (_player != null)
            GameHUDPrompt.DrawPromptBox("[L] Walk to the generator wheel");
        else
            GameHUDPrompt.DrawPromptBox("Night has fallen. Turn on the generator to restore the lights.");
    }
}
