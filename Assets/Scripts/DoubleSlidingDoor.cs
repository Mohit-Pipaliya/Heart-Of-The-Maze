using System;
using UnityEngine;
using MazeSystem;

/// <summary>
/// Normal Double Sliding Door Mechanism.
/// Jab gate khulta hai toh Left Door ek taraf aur Right Door uski opposite taraf slide hota hai.
/// Inspector me Axis (X, Y, Z) aur Slide Distance set karne ka aasan option hai.
/// Implements IMazeDoor for full seamless Shifting Maze integration.
/// </summary>
[SelectionBase]
public class DoubleSlidingDoor : MonoBehaviour, IMazeDoor
{
    public enum DoorAxis
    {
        Z_Axis,  // Maze Gate Z-axis par lined up hai
        X_Axis,  // Normal sideways sliding
        Y_Axis   // Up / Down sliding
    }

    [Header("─── Door References ─────────────────────────────────────")]
    [Tooltip("Left door ka Transform (empty chhodne par auto-find kar lega)")]
    public Transform leftDoor;

    [Tooltip("Right door ka Transform (empty chhodne par auto-find kar lega)")]
    public Transform rightDoor;

    [Header("─── Sliding Controls ───────────────────────────────────")]
    [Tooltip("Kis axis par doors slide honge")]
    public DoorAxis slideAxis = DoorAxis.Z_Axis;

    [Tooltip("Doors kitna door slide honge (dono opposite side me slide honge)")]
    public float slideDistance = 1.5f;

    [Tooltip("Agar doors ulti direction me khul rahe ho toh tick karein")]
    public bool invertDirection = false;

    [Tooltip("Agar Left aur Right dono ke liye alag distance chahiye toh ise tick karein")]
    public bool separateDistances = false;

    [Tooltip("Left door ka alag slide distance (sirf tab jab separateDistances tick ho)")]
    public float leftSlideDistance = 1.5f;

    [Tooltip("Right door ka alag slide distance (sirf tab jab separateDistances tick ho)")]
    public float rightSlideDistance = 1.5f;

    [Header("─── Speed & Animation ──────────────────────────────────")]
    [Tooltip("Door open / close hone ki speed")]
    public float openSpeed = 2.5f;

    [Tooltip("Door open hai ya closed (Inspector me live toggle karke test kar sakte hain)")]
    public bool isOpen = false;

    [Header("─── Player Auto Trigger ────────────────────────────────")]
    [Tooltip("Player paas aane par automatically khule")]
    public bool openOnApproach = true;

    [Tooltip("Player kitne distance me aane par door khule (meters)")]
    public float triggerDistance = 4.0f;

    [Tooltip("Player ke nikalne par automatically bandh ho")]
    public bool autoClose = true;

    [Tooltip("Player nikalne ke kitne second baad bandh ho")]
    public float autoCloseDelay = 1.0f;

    [Header("─── Audio (Optional) ───────────────────────────────────")]
    public AudioClip openSound;
    public AudioClip closeSound;
    [Range(0f, 1f)] public float soundVolume = 0.8f;

    // ─── IMazeDoor Implementation ───
    public bool IsOpen => isOpen && _currentProgress >= 0.99f;
    public bool IsClosed => !isOpen && _currentProgress <= 0.01f;
    public bool IsMoving => Mathf.Abs(_currentProgress - (isOpen ? 1f : 0f)) > 0.01f;
    public bool IsOccupied
    {
        get
        {
            if (_playerTransform == null) FindPlayer();
            if (_playerTransform != null)
                return Vector3.Distance(transform.position, _playerTransform.position) <= triggerDistance;
            return false;
        }
    }

    public event Action OnOpened;
    public event Action OnClosed;

    public void Open() => OpenDoor();
    public void Close() => CloseDoor();
    public void SetStateInstant(bool open)
    {
        Initialize();
        isOpen = open;
        _currentProgress = open ? 1f : 0f;
        ApplyProgress(_currentProgress);
        if (open) OnOpened?.Invoke();
        else OnClosed?.Invoke();
    }

    // ─── Private State ───
    private Vector3 _leftClosedPos;
    private Vector3 _rightClosedPos;
    private Vector3 _leftOpenPos;
    private Vector3 _rightOpenPos;

    private float _currentProgress = 0f; // 0 = Closed, 1 = Open
    private AudioSource _audioSource;
    private Transform _playerTransform;
    private float _lastPlayerNearTime = -999f;
    private bool _initialized = false;

    private void Reset()
    {
        AutoFindDoors();
    }

    [ContextMenu("Auto Find Left & Right Doors")]
    public void AutoFindDoors()
    {
        if (leftDoor == null)
        {
            leftDoor = transform.Find("Left Door") ?? transform.Find("LeftDoor") ?? transform.Find("Left");
            if (leftDoor == null && transform.childCount > 0) leftDoor = transform.GetChild(0);
        }

        if (rightDoor == null)
        {
            rightDoor = transform.Find("Right Door") ?? transform.Find("RightDoor") ?? transform.Find("Right");
            if (rightDoor == null && transform.childCount > 1) rightDoor = transform.GetChild(1);
        }
    }

    private void Awake()
    {
        Initialize();

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null && (openSound != null || closeSound != null))
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 1f;
        }

        FindPlayer();
    }

    private void Start()
    {
        if (_playerTransform == null) FindPlayer();

        // Initial state set
        _currentProgress = isOpen ? 1f : 0f;
        ApplyProgress(_currentProgress);
    }

    public void Initialize()
    {
        if (_initialized) return;
        AutoFindDoors();

        if (leftDoor != null) _leftClosedPos = leftDoor.localPosition;
        if (rightDoor != null) _rightClosedPos = rightDoor.localPosition;

        CalculateOpenPositions();
        _initialized = true;
    }

    public void CalculateOpenPositions()
    {
        Vector3 baseDir;
        switch (slideAxis)
        {
            case DoorAxis.X_Axis: baseDir = Vector3.right; break;
            case DoorAxis.Y_Axis: baseDir = Vector3.up; break;
            case DoorAxis.Z_Axis:
            default: baseDir = Vector3.forward; break;
        }

        if (invertDirection) baseDir = -baseDir;

        float distLeft = separateDistances ? leftSlideDistance : slideDistance;
        float distRight = separateDistances ? rightSlideDistance : slideDistance;

        // Normal double door: Left door goes in -direction, Right door goes in +direction
        Vector3 leftDir = -baseDir;
        Vector3 rightDir = baseDir;

        _leftOpenPos = _leftClosedPos + (leftDir * distLeft);
        _rightOpenPos = _rightClosedPos + (rightDir * distRight);
    }

    private void FindPlayer()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            _playerTransform = p.transform;
            return;
        }

        PlayerController pc = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        if (pc != null) _playerTransform = pc.transform;
    }

    private void Update()
    {
        if (openOnApproach)
        {
            if (_playerTransform == null) FindPlayer();

            if (_playerTransform != null)
            {
                float dist = Vector3.Distance(transform.position, _playerTransform.position);
                bool isNear = dist <= triggerDistance;

                if (isNear)
                {
                    _lastPlayerNearTime = Time.time;
                    if (!isOpen)
                    {
                        OpenDoor();
                    }
                }
                else if (autoClose && isOpen)
                {
                    if (Time.time >= _lastPlayerNearTime + autoCloseDelay)
                    {
                        CloseDoor();
                    }
                }
            }
        }

        // 2. Smooth Sliding Animation
        float targetProgress = isOpen ? 1f : 0f;
        if (Mathf.Abs(_currentProgress - targetProgress) > 0.0001f)
        {
            _currentProgress = Mathf.MoveTowards(_currentProgress, targetProgress, Time.deltaTime * openSpeed);
            // Smooth damping
            float smooth = Mathf.SmoothStep(0f, 1f, _currentProgress);
            ApplyProgress(smooth);
        }
    }

    private void ApplyProgress(float t)
    {
        if (leftDoor != null)
        {
            leftDoor.localPosition = Vector3.Lerp(_leftClosedPos, _leftOpenPos, t);
        }

        if (rightDoor != null)
        {
            rightDoor.localPosition = Vector3.Lerp(_rightClosedPos, _rightOpenPos, t);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  PUBLIC METHODS (Call via code, buttons, or UnityEvents)
    // ═══════════════════════════════════════════════════════════════════════════

    [ContextMenu("Open Door")]
    public void OpenDoor()
    {
        if (isOpen && _currentProgress >= 0.99f) return;
        Initialize();
        CalculateOpenPositions();
        isOpen = true;
        PlayAudio(openSound);
        if (!Application.isPlaying)
        {
            _currentProgress = 1f;
            ApplyProgress(1f);
        }
        OnOpened?.Invoke();
    }

    [ContextMenu("Close Door")]
    public void CloseDoor()
    {
        if (!isOpen && _currentProgress <= 0.01f) return;
        Initialize();
        CalculateOpenPositions();
        isOpen = false;
        PlayAudio(closeSound);
        if (!Application.isPlaying)
        {
            _currentProgress = 0f;
            ApplyProgress(0f);
        }
        OnClosed?.Invoke();
    }

    [ContextMenu("Toggle Door")]
    public void ToggleDoor()
    {
        if (isOpen) CloseDoor();
        else OpenDoor();
    }

    private void PlayAudio(AudioClip clip)
    {
        if (clip == null) return;
        if (_audioSource != null) _audioSource.PlayOneShot(clip, soundVolume);
        else AudioSource.PlayClipAtPoint(clip, transform.position, soundVolume);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  PHYSICS TRIGGER SUPPORT (Optional: BoxCollider with IsTrigger)
    // ═══════════════════════════════════════════════════════════════════════════

    private void OnTriggerEnter(Collider other)
    {
        if (!openOnApproach) return;

        if (other.CompareTag("Player") || other.GetComponent<PlayerController>() != null)
        {
            _lastPlayerNearTime = Time.time;
            OpenDoor();
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!openOnApproach) return;
        if (other.CompareTag("Player") || other.GetComponent<PlayerController>() != null)
        {
            _lastPlayerNearTime = Time.time;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!openOnApproach || !autoClose) return;
        if (other.CompareTag("Player") || other.GetComponent<PlayerController>() != null)
        {
            _lastPlayerNearTime = Time.time;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  INSPECTOR VALIDATE & SCENE GIZMOS
    // ═══════════════════════════════════════════════════════════════════════════

    private void OnValidate()
    {
        // Negative (minus) numbers allow kiye gaye hain taaki opposite side slide kar sakein
        triggerDistance = Mathf.Max(0.5f, triggerDistance);
        openSpeed = Mathf.Max(0.1f, openSpeed);

        if (!Application.isPlaying && (leftDoor != null || rightDoor != null))
        {
            AutoFindDoors();
            if (leftDoor != null) _leftClosedPos = leftDoor.localPosition;
            if (rightDoor != null) _rightClosedPos = rightDoor.localPosition;
            CalculateOpenPositions();
            ApplyProgress(isOpen ? 1f : 0f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        AutoFindDoors();

        // 1. Trigger Area Sphere (Green)
        if (openOnApproach)
        {
            Gizmos.color = new Color(0.1f, 1f, 0.4f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, triggerDistance);
        }

        Vector3 baseDir;
        switch (slideAxis)
        {
            case DoorAxis.X_Axis: baseDir = Vector3.right; break;
            case DoorAxis.Y_Axis: baseDir = Vector3.up; break;
            case DoorAxis.Z_Axis:
            default: baseDir = Vector3.forward; break;
        }
        if (invertDirection) baseDir = -baseDir;

        float distLeft = separateDistances ? leftSlideDistance : slideDistance;
        float distRight = separateDistances ? rightSlideDistance : slideDistance;

        // Left Door open direction (Yellow)
        if (leftDoor != null)
        {
            Vector3 worldLeft = transform.TransformPoint(leftDoor.localPosition);
            Vector3 worldLeftTarget = transform.TransformPoint(leftDoor.localPosition - baseDir * distLeft);

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(worldLeft, worldLeftTarget);
            Gizmos.DrawWireSphere(worldLeftTarget, 0.2f);
        }

        // Right Door open direction (Cyan)
        if (rightDoor != null)
        {
            Vector3 worldRight = transform.TransformPoint(rightDoor.localPosition);
            Vector3 worldRightTarget = transform.TransformPoint(rightDoor.localPosition + baseDir * distRight);

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(worldRight, worldRightTarget);
            Gizmos.DrawWireSphere(worldRightTarget, 0.2f);
        }
    }
}
