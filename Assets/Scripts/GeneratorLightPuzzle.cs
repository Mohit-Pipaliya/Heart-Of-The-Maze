using UnityEngine;
using System;

[RequireComponent(typeof(Collider))]
public class GeneratorLightPuzzle : MonoBehaviour
{
    private enum PuzzleState
    {
        Idle,
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
    [SerializeField] private ParticleSystem wheelStandBeacon;
    [Tooltip("House trigger volumes powered by this generator. Each trigger controls its own house lights.")]
    [SerializeField] private HouseLightTrigger[] houseLightTriggers;

    [Header("Interaction")]
    [Tooltip("Player manually walks this close to the stand point; movement then locks for the wheel puzzle.")]
    [SerializeField, Min(0.2f)] private float wheelInteractionRadius = 1.8f;
    [SerializeField, Min(1f)] private float wheelRotationSpeed = 75f;
    [SerializeField, Min(0.1f)] private float alignmentAngleThreshold = 8f;
    [SerializeField] private Vector3 wheelLocalRotationAxis = Vector3.forward;

    private PuzzleState _state;
    private PlayerController _player;
    private CharacterController _characterController;
    private Animator _animator;
    private CameraController _cameraController;
    private float _currentPuzzleAngle;
    private float _signedPuzzleAngle;
    private float _lastPuzzleAngle;
    private bool _isMovingCloser;
    private bool _isRotatingKeyPressed;
    private bool _lastPowerAvailability;

    private bool IsNight => dayNightController != null && dayNightController.IsNight;
    public bool IsPowerAvailable => _state == PuzzleState.Completed && IsNight;
    public event Action<bool> OnPowerAvailabilityChanged;

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

        if (houseLightTriggers != null)
            foreach (HouseLightTrigger houseTrigger in houseLightTriggers)
                if (houseTrigger != null) houseTrigger.SetPowerSource(this);

        CreateWheelStandBeaconIfNeeded();
    }

    private void Start()
    {
        if (dayNightController == null)
            Debug.LogError($"[{nameof(GeneratorLightPuzzle)}] Assign a DayNightController or add one to the scene.", this);

        if (wheelStandPoint == null || wheelPivot == null || wheelMarker == null || targetWheelMarker == null)
            Debug.LogError($"[{nameof(GeneratorLightPuzzle)}] Assign the wheel stand, wheel pivot, moving marker, and target marker.", this);

        if (houseLightTriggers == null || houseLightTriggers.Length == 0)
            Debug.LogWarning($"[{nameof(GeneratorLightPuzzle)}] No HouseLightTrigger components are assigned; solving the wheel will not power house lights.", this);

        RefreshPowerAvailability();
        UpdateBeaconVisibility();

        if (wheelLocalRotationAxis.sqrMagnitude < 0.0001f)
            Debug.LogError($"[{nameof(GeneratorLightPuzzle)}] Wheel rotation axis must not be zero.", this);
    }

    private void Update()
    {
        RefreshPowerAvailability();

        if (_state == PuzzleState.RotatingWheel)
        {
            // Keep locomotion idle throughout the wheel interaction. The player is
            // frozen, but a blend tree can otherwise retain its previous walk value.
            if (_animator != null)
                _animator.SetFloat("Speed", 0f);
            RotateWheel();
            return;
        }

        if (_state == PuzzleState.Idle && _player != null && IsNight && wheelStandPoint != null)
        {
            Vector3 toStand = wheelStandPoint.position - _player.transform.position;
            toStand.y = 0f;
            if (toStand.sqrMagnitude <= wheelInteractionRadius * wheelInteractionRadius)
                BeginWheelInteraction();
        }

        UpdateBeaconVisibility();
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

        if (_player == null || _characterController == null || !_characterController.enabled || wheelPivot == null)
            return;

        Vector3 facing = wheelPivot.position - _player.transform.position;
        facing.y = 0f;
        if (facing.sqrMagnitude > 0.001f)
            _player.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);

        // The player walked here manually. Freeze their current position and enter
        // the wheel view immediately; there is no scripted/autowalk phase.
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
        UpdateBeaconVisibility();
        Debug.Log($"[{nameof(GeneratorLightPuzzle)}] Player reached the wheel. Press A/D to align it.", this);
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

        _state = PuzzleState.Completed;
        RefreshPowerAvailability();
        UpdateBeaconVisibility();

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

    private bool HasWheelSetup()
    {
        bool configured = wheelStandPoint != null && wheelPivot != null &&
                          wheelMarker != null && targetWheelMarker != null &&
                          wheelLocalRotationAxis.sqrMagnitude >= 0.0001f;
        if (!configured)
            Debug.LogError($"[{nameof(GeneratorLightPuzzle)}] Cannot start: one or more wheel transforms are not assigned.", this);

        return configured;
    }

    private void RefreshPowerAvailability()
    {
        bool available = IsPowerAvailable;
        if (available == _lastPowerAvailability) return;
        _lastPowerAvailability = available;
        OnPowerAvailabilityChanged?.Invoke(available);
    }

    private void OnGUI()
    {
        if (_state == PuzzleState.Completed || !IsNight)
            return;

        if (_state == PuzzleState.RotatingWheel)
        {
            GameHUDPrompt.DrawAlignmentHUD(
                _currentPuzzleAngle,
                _signedPuzzleAngle,
                alignmentAngleThreshold,
                _currentPuzzleAngle <= alignmentAngleThreshold,
                _isMovingCloser,
                false,
                "GENERATOR WHEEL — A / D TO ALIGN",
                new Color(1f, 0.78f, 0.18f));
            return;
        }

        if (_player != null)
            GameHUDPrompt.DrawPromptBox("Follow the glowing beacon to the generator wheel");
        else
            GameHUDPrompt.DrawPromptBox("Night has fallen. Turn on the generator to restore the lights.");
    }

    private void CreateWheelStandBeaconIfNeeded()
    {
        if (wheelStandPoint == null || wheelStandBeacon != null)
            return;

        GameObject beaconObject = new GameObject("Generator Wheel Stand Beacon");
        beaconObject.transform.SetParent(wheelStandPoint, false);
        beaconObject.transform.localPosition = Vector3.up * 0.15f;
        wheelStandBeacon = beaconObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = wheelStandBeacon.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.7f, 1.35f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.25f, 0.82f, 1f, 0.95f), new Color(0.9f, 1f, 1f, 0.85f));
        main.maxParticles = 100;

        ParticleSystem.EmissionModule emission = wheelStandBeacon.emission;
        emission.rateOverTime = 24f;

        ParticleSystem.ShapeModule shape = wheelStandBeacon.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 5f;
        shape.radius = 0.32f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        ParticleSystem.ColorOverLifetimeModule color = wheelStandBeacon.colorOverLifetime;
        color.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(new Color(0.3f, 0.85f, 1f), 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.18f), new GradientAlphaKey(0f, 1f) });
        color.color = fade;

        ParticleSystem.SizeOverLifetimeModule size = wheelStandBeacon.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.15f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0.45f)));
    }

    private void UpdateBeaconVisibility()
    {
        if (wheelStandBeacon == null)
            return;

        bool shouldShow = IsNight && _state != PuzzleState.Completed && _state != PuzzleState.RotatingWheel;
        if (shouldShow && !wheelStandBeacon.isPlaying)
            wheelStandBeacon.Play();
        else if (!shouldShow && wheelStandBeacon.isPlaying)
            wheelStandBeacon.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
