using System.Collections;
using UnityEngine;

/// <summary>
/// Double Sliding Door / Gate Controller.
/// Inspector me dono doors ke liye alag-alag Axis (X, Y, Z, Custom)
/// aur alag-alag Slide Distance set karne ki suvidha deta hai.
/// </summary>
[SelectionBase]
public class DoubleSlidingDoor : MonoBehaviour
{
    public enum SlideAxis
    {
        [InspectorName("X Axis (+Right)")]
        X_Positive,
        [InspectorName("X Axis (-Left)")]
        X_Negative,
        [InspectorName("Y Axis (+Up)")]
        Y_Positive,
        [InspectorName("Y Axis (-Down)")]
        Y_Negative,
        [InspectorName("Z Axis (+Forward)")]
        Z_Positive,
        [InspectorName("Z Axis (-Back)")]
        Z_Negative,
        [InspectorName("Custom Vector")]
        Custom
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  DOOR REFERENCES & AXIS / DISTANCE CONTROLS
    // ═══════════════════════════════════════════════════════════════════════════

    [Header("─── Door 1 (e.g. Left Door) ───────────────────────────")]
    [Tooltip("Pehle door ka Transform")]
    public Transform door1;

    [Tooltip("Door 1 kis axis par slide karega")]
    public SlideAxis door1Axis = SlideAxis.Z_Negative;

    [Tooltip("Door 1 kitne distance tak slide hoga (units/meters)")]
    public float door1SlideDistance = 1.3f;

    [Tooltip("Agar door1Axis 'Custom' chuna ho, toh yeh direction vector use hoga")]
    public Vector3 door1CustomAxis = Vector3.back;

    [Header("─── Door 2 (e.g. Right Door) ──────────────────────────")]
    [Tooltip("Dusre door ka Transform")]
    public Transform door2;

    [Tooltip("Door 2 kis axis par slide karega")]
    public SlideAxis door2Axis = SlideAxis.Z_Positive;

    [Tooltip("Door 2 kitne distance tak slide hoga (units/meters)")]
    public float door2SlideDistance = 1.3f;

    [Tooltip("Agar door2Axis 'Custom' chuna ho, toh yeh direction vector use hoga")]
    public Vector3 door2CustomAxis = Vector3.forward;

    [Header("─── Motion & Animation ─────────────────────────────────")]
    [Tooltip("Door khulne / bandh hone me kitna time lagega (seconds)")]
    [Range(0.2f, 10f)]
    public float moveDuration = 1.5f;

    [Tooltip("Smooth sliding motion ke liye animation easing curve")]
    public AnimationCurve motionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Local coordinate space use kare (gate rotate hone par bhi doors gate ke hisaab se slide honge)")]
    public bool useLocalSpace = true;

    [Tooltip("Parent scale ko compensate kare taaki Inspector ki distance actual world meters me rahe")]
    public bool compensateParentScale = true;

    [Header("─── Trigger & Interaction ──────────────────────────────")]
    [SerializeField, Tooltip("Door ka current status (Inspector me live toggle karke test kar sakte hain)")]
    private bool isOpen = false;

    [Tooltip("Player paas aane par automatically door open ho jaye")]
    public bool openOnProximity = true;

    [Tooltip("Player kitne meter door ho tab door open trigger ho (meters)")]
    public float proximityRadius = 6.0f;

    [Tooltip("Player ke nikal jane ke baad automatically door bandh ho jaye")]
    public bool autoClose = true;

    [Tooltip("Player ke door se nikalne ke kitne second baad close shuru ho")]
    public float autoCloseDelay = 1.5f;

    [Tooltip("Key press interaction chahiye (e.g. Press 'E' to open)")]
    public bool requireKeyPress = false;

    public KeyCode interactKey = KeyCode.E;

    [Header("─── Audio ──────────────────────────────────────────────")]
    public AudioClip openSound;
    public AudioClip closeSound;
    [Range(0f, 1f)]
    public float soundVolume = 0.8f;

    [Header("─── Debug Visuals ──────────────────────────────────────")]
    public bool showGizmos = true;

    // ═══════════════════════════════════════════════════════════════════════════
    //  INTERNAL STATE
    // ═══════════════════════════════════════════════════════════════════════════

    private Vector3 _door1ClosedPos;
    private Vector3 _door2ClosedPos;
    private Vector3 _door1OpenPos;
    private Vector3 _door2OpenPos;

    private Coroutine _motionRoutine;
    private float _currentProgress = 0f; // 0 = Closed, 1 = Fully Open
    private AudioSource _audioSource;
    private Transform _playerTransform;
    private float _lastPlayerNearTime = -999f;
    private bool _playerWasNear = false;
    private bool _initialized = false;

    public bool IsOpen => isOpen;
    public float CurrentProgress => _currentProgress;

    private void Reset()
    {
        AutoAssignDoors();
    }

    [ContextMenu("Auto-Assign Left & Right Doors")]
    public void AutoAssignDoors()
    {
        if (door1 == null)
        {
            Transform left = transform.Find("Left Door") ?? transform.Find("LeftDoor") ?? transform.Find("Door1");
            if (left != null) door1 = left;
            else if (transform.childCount > 0) door1 = transform.GetChild(0);
        }

        if (door2 == null)
        {
            Transform right = transform.Find("Right Door") ?? transform.Find("RightDoor") ?? transform.Find("Door2");
            if (right != null) door2 = right;
            else if (transform.childCount > 1) door2 = transform.GetChild(1);
        }
    }

    private void Awake()
    {
        InitializePositions();

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null && (openSound != null || closeSound != null))
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 1f; // 3D sound
            _audioSource.minDistance = 2f;
            _audioSource.maxDistance = 25f;
        }

        FindPlayer();
    }

    private void Start()
    {
        if (_playerTransform == null)
        {
            FindPlayer();
        }

        // Agar inspector me default open set kiya ho
        if (isOpen)
        {
            _currentProgress = 1f;
            ApplyProgress(1f);
        }
        else
        {
            _currentProgress = 0f;
            ApplyProgress(0f);
        }
    }

    public void InitializePositions()
    {
        if (_initialized) return;
        AutoAssignDoors();

        if (door1 != null)
        {
            _door1ClosedPos = useLocalSpace ? door1.localPosition : door1.position;
        }

        if (door2 != null)
        {
            _door2ClosedPos = useLocalSpace ? door2.localPosition : door2.position;
        }

        RecalculateTargetPositions();
        _initialized = true;
    }

    /// <summary>
    /// Calculate karta hai ki slide hone ke baad dono doors ka target position kya hoga.
    /// </summary>
    public void RecalculateTargetPositions()
    {
        if (door1 != null)
        {
            Vector3 dir1 = GetDirectionVector(door1Axis, door1CustomAxis);
            float dist1 = door1SlideDistance;

            if (useLocalSpace && compensateParentScale && transform.localScale.sqrMagnitude > 0.001f)
            {
                // Compensate parent scale along the axis
                Vector3 localScale = transform.lossyScale;
                float scaleOnAxis = Mathf.Abs(Vector3.Dot(dir1.normalized, localScale));
                if (scaleOnAxis > 0.001f)
                {
                    dist1 = door1SlideDistance / scaleOnAxis;
                }
            }

            Vector3 offset1 = dir1.normalized * dist1;
            _door1OpenPos = _door1ClosedPos + offset1;
        }

        if (door2 != null)
        {
            Vector3 dir2 = GetDirectionVector(door2Axis, door2CustomAxis);
            float dist2 = door2SlideDistance;

            if (useLocalSpace && compensateParentScale && transform.localScale.sqrMagnitude > 0.001f)
            {
                Vector3 localScale = transform.lossyScale;
                float scaleOnAxis = Mathf.Abs(Vector3.Dot(dir2.normalized, localScale));
                if (scaleOnAxis > 0.001f)
                {
                    dist2 = door2SlideDistance / scaleOnAxis;
                }
            }

            Vector3 offset2 = dir2.normalized * dist2;
            _door2OpenPos = _door2ClosedPos + offset2;
        }
    }

    public Vector3 GetDirectionVector(SlideAxis axis, Vector3 custom)
    {
        switch (axis)
        {
            case SlideAxis.X_Positive: return Vector3.right;
            case SlideAxis.X_Negative: return Vector3.left;
            case SlideAxis.Y_Positive: return Vector3.up;
            case SlideAxis.Y_Negative: return Vector3.down;
            case SlideAxis.Z_Positive: return Vector3.forward;
            case SlideAxis.Z_Negative: return Vector3.back;
            case SlideAxis.Custom: return custom.sqrMagnitude > 0.001f ? custom.normalized : Vector3.forward;
            default: return Vector3.forward;
        }
    }

    private void FindPlayer()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            _playerTransform = p.transform;
            return;
        }

        PlayerController pc = Object.FindFirstObjectByType<PlayerController>();
        if (pc != null)
        {
            _playerTransform = pc.transform;
        }
    }

    private void Update()
    {
        if (!openOnProximity) return;

        if (_playerTransform == null)
        {
            FindPlayer();
            if (_playerTransform == null) return;
        }

        float dist = Vector3.Distance(transform.position, _playerTransform.position);
        bool isNear = dist <= proximityRadius;

        if (requireKeyPress)
        {
            if (isNear && Input.GetKeyDown(interactKey))
            {
                Toggle();
            }
        }
        else
        {
            // Automatic approach and leave
            if (isNear)
            {
                _lastPlayerNearTime = Time.time;
                _playerWasNear = true;

                if (!isOpen)
                {
                    Open();
                }
            }
            else if (_playerWasNear && autoClose)
            {
                if (Time.time >= _lastPlayerNearTime + autoCloseDelay)
                {
                    _playerWasNear = false;
                    if (isOpen)
                    {
                        Close();
                    }
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  PUBLIC DOOR CONTROL METHODS
    // ═══════════════════════════════════════════════════════════════════════════

    [ContextMenu("Open Doors")]
    public void Open()
    {
        SetState(true);
    }

    [ContextMenu("Close Doors")]
    public void Close()
    {
        SetState(false);
    }

    [ContextMenu("Toggle Doors")]
    public void Toggle()
    {
        SetState(!isOpen);
    }

    public void SetState(bool open)
    {
        if (!_initialized)
        {
            InitializePositions();
        }

        isOpen = open;
        RecalculateTargetPositions();

        PlayDoorSound(open);

        if (_motionRoutine != null)
        {
            StopCoroutine(_motionRoutine);
        }
        _motionRoutine = StartCoroutine(AnimateMotion(open ? 1f : 0f));
    }

    private IEnumerator AnimateMotion(float targetProgress)
    {
        float startProgress = _currentProgress;
        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, moveDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Apply curve
            float curveT = motionCurve != null ? motionCurve.Evaluate(t) : t;
            _currentProgress = Mathf.Lerp(startProgress, targetProgress, curveT);

            ApplyProgress(_currentProgress);
            yield return null;
        }

        _currentProgress = targetProgress;
        ApplyProgress(_currentProgress);
        _motionRoutine = null;
    }

    private void ApplyProgress(float progress)
    {
        if (door1 != null)
        {
            Vector3 pos1 = Vector3.Lerp(_door1ClosedPos, _door1OpenPos, progress);
            if (useLocalSpace) door1.localPosition = pos1;
            else door1.position = pos1;
        }

        if (door2 != null)
        {
            Vector3 pos2 = Vector3.Lerp(_door2ClosedPos, _door2OpenPos, progress);
            if (useLocalSpace) door2.localPosition = pos2;
            else door2.position = pos2;
        }
    }

    private void PlayDoorSound(bool opening)
    {
        AudioClip clip = opening ? openSound : closeSound;
        if (clip == null) return;

        if (_audioSource != null)
        {
            _audioSource.PlayOneShot(clip, soundVolume);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clip, transform.position, soundVolume);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  TRIGGER COLLIDER SUPPORT
    // ═══════════════════════════════════════════════════════════════════════════

    private void OnTriggerEnter(Collider other)
    {
        if (!openOnProximity || requireKeyPress) return;
        if (IsPlayerCollider(other))
        {
            _lastPlayerNearTime = Time.time;
            _playerWasNear = true;
            if (!isOpen) Open();
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!openOnProximity || requireKeyPress) return;
        if (IsPlayerCollider(other))
        {
            _lastPlayerNearTime = Time.time;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!openOnProximity || requireKeyPress) return;
        if (IsPlayerCollider(other) && autoClose)
        {
            _lastPlayerNearTime = Time.time;
        }
    }

    private bool IsPlayerCollider(Collider col)
    {
        if (col == null) return false;
        if (col.CompareTag("Player")) return true;
        if (col.GetComponent<PlayerController>() != null || col.GetComponentInParent<PlayerController>() != null) return true;
        return false;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  EDITOR & SCENE VIEW GIZMOS
    // ═══════════════════════════════════════════════════════════════════════════

    private void OnValidate()
    {
        door1SlideDistance = Mathf.Max(0f, door1SlideDistance);
        door2SlideDistance = Mathf.Max(0f, door2SlideDistance);
        proximityRadius = Mathf.Max(0.5f, proximityRadius);
        moveDuration = Mathf.Max(0.05f, moveDuration);

        // Edit Mode preview: agar play mode nahi hai aur user ne isOpen checkbox toggle kiya ho
        if (!Application.isPlaying && (door1 != null || door2 != null))
        {
            AutoAssignDoors();
            if (door1 != null) _door1ClosedPos = useLocalSpace ? door1.localPosition : door1.position;
            if (door2 != null) _door2ClosedPos = useLocalSpace ? door2.localPosition : door2.position;
            RecalculateTargetPositions();
            ApplyProgress(isOpen ? 1f : 0f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        AutoAssignDoors();

        Vector3 c1 = door1 != null ? (useLocalSpace ? transform.TransformPoint(door1.localPosition) : door1.position) : transform.position;
        Vector3 c2 = door2 != null ? (useLocalSpace ? transform.TransformPoint(door2.localPosition) : door2.position) : transform.position;

        // Proximity Radius wire sphere
        if (openOnProximity)
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, proximityRadius);
        }

        // Door 1 Open Vector (Green Line & Arrow)
        if (door1 != null)
        {
            Vector3 dir1 = GetDirectionVector(door1Axis, door1CustomAxis);
            Vector3 worldDir1 = useLocalSpace ? transform.TransformDirection(dir1.normalized) : dir1.normalized;
            Vector3 target1 = c1 + worldDir1 * door1SlideDistance;

            Gizmos.color = new Color(0.1f, 1f, 0.2f, 0.9f);
            Gizmos.DrawLine(c1, target1);
            Gizmos.DrawWireSphere(target1, 0.25f);
        }

        // Door 2 Open Vector (Cyan Line & Arrow)
        if (door2 != null)
        {
            Vector3 dir2 = GetDirectionVector(door2Axis, door2CustomAxis);
            Vector3 worldDir2 = useLocalSpace ? transform.TransformDirection(dir2.normalized) : dir2.normalized;
            Vector3 target2 = c2 + worldDir2 * door2SlideDistance;

            Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.9f);
            Gizmos.DrawLine(c2, target2);
            Gizmos.DrawWireSphere(target2, 0.25f);
        }
    }
}
