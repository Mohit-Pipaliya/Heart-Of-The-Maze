using System;
using UnityEngine;
using UnityEngine.Events;

namespace MazeSystem
{
    /// <summary>
    /// Sliding main-entrance door for a maze block.
    /// Only the door of the block occupying the Active Block Position is ever driven by the maze system.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MazeMainDoor : MonoBehaviour, IMazeDoor
    {
        public enum DoorState
        {
            Closed,
            Opening,
            Open,
            Closing
        }

        [Serializable]
        public sealed class SlidingPanel
        {
            public Transform panel;
            [Tooltip("Slide direction expressed in this door root's local space.")]
            public Vector3 slideDirection = Vector3.right;
            [Min(0f)] public float slideDistance = 1.5f;
        }

        [Header("── Panels ───────────────────────────────────")]
        [SerializeField] private SlidingPanel[] panels = Array.Empty<SlidingPanel>();

        [Header("── Timing ───────────────────────────────────")]
        [Min(0f)] [SerializeField] private float openDuration = 1.2f;
        [Min(0f)] [SerializeField] private float closeDuration = 1.2f;
        [Min(0f)] [SerializeField] private float delayBeforeOpening = 0f;
        [SerializeField] private AnimationCurve openEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve closeEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("── Collision ────────────────────────────────")]
        [Tooltip("Colliders enabled while the door is closed.")]
        [SerializeField] private Collider[] blockers = Array.Empty<Collider>();

        [Tooltip("Trigger volume used to verify the doorway is clear before closing. Auto-created by the setup tool.")]
        [SerializeField] private Collider doorwayVolume;

        [Tooltip("Layers that can hold the door open. Static walls are ignored automatically.")]
        [SerializeField] private LayerMask occupantMask = ~0;

        [Min(0.05f)] [SerializeField] private float occupiedRetryInterval = 0.4f;

        [Header("── Audio (optional) ─────────────────────────")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip openClip;
        [SerializeField] private AudioClip closeClip;
        [Range(0f, 1f)] [SerializeField] private float audioVolume = 0.75f;

        [Header("── Events ───────────────────────────────────")]
        [SerializeField] private UnityEvent onOpened = new UnityEvent();
        [SerializeField] private UnityEvent onClosed = new UnityEvent();

        public event Action OnOpened;
        public event Action OnClosed;

        public DoorState State { get; private set; } = DoorState.Closed;
        public float Progress { get; private set; }
        public bool IsOpen => Progress >= 0.9999f;
        public bool IsClosed => Progress <= 0.0001f;
        public bool IsMoving => State == DoorState.Opening || State == DoorState.Closing;
        public bool IsOccupied => CheckOccupied();
        public bool HasPendingClose => _pendingClose;

        private Vector3[] _panelClosedPositions = Array.Empty<Vector3>();
        private Vector3[] _panelOpenPositions = Array.Empty<Vector3>();
        private readonly Collider[] _overlapBuffer = new Collider[16];
        private bool _initialized;
        private bool _pendingClose;
        private float _delayRemaining;
        private float _retryTimer;
        private bool _occupiedLogged;

        private void Awake()
        {
            Initialize();
            SetBlockersClosed(IsClosed);
            ApplyProgress(Progress);
        }

        private void Reset()
        {
            CacheClosedPositions();
        }

        private void OnEnable()
        {
            if (!_initialized)
                Initialize();
        }

        private void OnValidate()
        {
            openDuration = Mathf.Max(0f, openDuration);
            closeDuration = Mathf.Max(0f, closeDuration);
            delayBeforeOpening = Mathf.Max(0f, delayBeforeOpening);

            if (!Application.isPlaying)
            {
                CacheClosedPositions();
                ApplyProgress(IsOpen ? 1f : 0f);
                SetBlockersClosed(!IsOpen);
            }
        }

        private void Update()
        {
            if (!_initialized)
                Initialize();

            if (State == DoorState.Opening)
            {
                if (_delayRemaining > 0f)
                {
                    _delayRemaining -= Time.deltaTime;
                    return;
                }

                float duration = Mathf.Max(0.0001f, openDuration);
                Progress = Mathf.MoveTowards(Progress, 1f, Time.deltaTime / duration);
                ApplyProgress(EvaluateOpen(Progress));
                if (Progress >= 1f)
                    CompleteOpen();
            }
            else if (State == DoorState.Closing)
            {
                float duration = Mathf.Max(0.0001f, closeDuration);
                Progress = Mathf.MoveTowards(Progress, 0f, Time.deltaTime / duration);
                ApplyProgress(EvaluateClose(Progress));
                if (Progress <= 0f)
                    CompleteClose();
            }
            else if (_pendingClose)
            {
                _retryTimer -= Time.deltaTime;
                if (_retryTimer <= 0f)
                {
                    _retryTimer = occupiedRetryInterval;
                    TryStartClosing();
                }
            }
        }

        public void Open()
        {
            Initialize();
            _pendingClose = false;
            _occupiedLogged = false;

            if (State == DoorState.Open && IsOpen)
                return;

            SetBlockersClosed(false);
            State = DoorState.Opening;
            _delayRemaining = delayBeforeOpening;
            PlayAudio(openClip);

            if (!Application.isPlaying)
            {
                SetStateInstant(true);
                return;
            }

            if (Progress >= 1f)
            {
                CompleteOpen();
                return;
            }
        }

        public void Close()
        {
            Initialize();
            if (State == DoorState.Closed && IsClosed)
                return;

            if (!Application.isPlaying)
            {
                SetStateInstant(false);
                return;
            }

            _pendingClose = true;
            _retryTimer = 0f;
            TryStartClosing();
        }

        public void SetStateInstant(bool open)
        {
            Initialize();
            _pendingClose = false;
            _delayRemaining = 0f;
            _occupiedLogged = false;

            Progress = open ? 1f : 0f;
            State = open ? DoorState.Open : DoorState.Closed;
            ApplyProgress(Progress);
            SetBlockersClosed(!open);
        }

        /// <summary>Editor/setup hook: replaces the panel list and recaches closed poses.</summary>
        public void EditorConfigure(SlidingPanel[] newPanels, Collider[] newBlockers, Collider checkVolume)
        {
            panels = newPanels ?? Array.Empty<SlidingPanel>();
            blockers = newBlockers ?? Array.Empty<Collider>();
            doorwayVolume = checkVolume;
            CacheClosedPositions();
            ApplyProgress(IsOpen ? 1f : 0f);
            SetBlockersClosed(!IsOpen);
        }

        /// <summary>Editor/setup hook: identity of blockers can change when the door is rebuilt.</summary>
        public void EditorSetBlockers(Collider[] newBlockers)
        {
            blockers = newBlockers ?? Array.Empty<Collider>();
            SetBlockersClosed(!IsOpen);
        }

        public Transform GetDoorTransform() => transform;

        private void Initialize()
        {
            if (_initialized)
                return;

            CacheClosedPositions();
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
            _initialized = true;
        }

        private void CacheClosedPositions()
        {
            int count = panels != null ? panels.Length : 0;
            _panelClosedPositions = new Vector3[count];
            _panelOpenPositions = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                SlidingPanel p = panels[i];
                if (p == null || p.panel == null)
                    continue;

                _panelClosedPositions[i] = p.panel.localPosition;
                Vector3 dir = p.slideDirection.sqrMagnitude > 0.0001f
                    ? p.slideDirection.normalized
                    : Vector3.right;
                _panelOpenPositions[i] = _panelClosedPositions[i] + dir * p.slideDistance;
            }
        }

        private float EvaluateOpen(float t)
        {
            if (openEase == null || openEase.length == 0)
                return Mathf.SmoothStep(0f, 1f, t);
            return openEase.Evaluate(Mathf.Clamp01(t));
        }

        private float EvaluateClose(float t)
        {
            // closeEase describes 0 -> 1 across the whole close motion; invert it so the
            // returned slide progress stays 1 (open) -> 0 (closed).
            float e = closeEase == null || closeEase.length == 0
                ? Mathf.SmoothStep(0f, 1f, 1f - t)
                : closeEase.Evaluate(Mathf.Clamp01(1f - t));
            return 1f - e;
        }

        private void ApplyProgress(float slideProgress)
        {
            slideProgress = Mathf.Clamp01(slideProgress);
            int count = panels != null ? panels.Length : 0;
            for (int i = 0; i < count; i++)
            {
                SlidingPanel p = panels[i];
                if (p == null || p.panel == null)
                    continue;
                if (i >= _panelClosedPositions.Length || i >= _panelOpenPositions.Length)
                    CacheClosedPositions();

                p.panel.localPosition = Vector3.LerpUnclamped(
                    _panelClosedPositions[i], _panelOpenPositions[i], slideProgress);
            }
        }

        private void CompleteOpen()
        {
            Progress = 1f;
            State = DoorState.Open;
            ApplyProgress(1f);
            SetBlockersClosed(false);
            OnOpened?.Invoke();
            onOpened.Invoke();
        }

        private void CompleteClose()
        {
            Progress = 0f;
            State = DoorState.Closed;
            _pendingClose = false;
            ApplyProgress(0f);
            SetBlockersClosed(true);
            OnClosed?.Invoke();
            onClosed.Invoke();
            PlayAudio(closeClip);
        }

        private void TryStartClosing()
        {
            if (IsClosed)
            {
                _pendingClose = false;
                return;
            }

            if (CheckOccupied())
            {
                if (!_occupiedLogged)
                {
                    _occupiedLogged = true;
                    Debug.LogWarning($"[MazeSystem] {name}: doorway is occupied — close is held and will retry.", this);
                }
                _retryTimer = occupiedRetryInterval;
                return;
            }

            _occupiedLogged = false;
            _pendingClose = false;
            State = DoorState.Closing;
            SetBlockersClosed(true);
        }

        private bool CheckOccupied()
        {
            if (doorwayVolume == null)
                return false;

            Bounds b = doorwayVolume.bounds;
            int hits = Physics.OverlapBoxNonAlloc(
                b.center, b.extents, _overlapBuffer, Quaternion.identity, occupantMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hits; i++)
            {
                Collider c = _overlapBuffer[i];
                if (c == null)
                    continue;
                if (c.transform.IsChildOf(transform))
                    continue;
                if (c is TerrainCollider)
                    continue;

                if (c.GetComponentInParent<CharacterController>() != null)
                    return true;
                if (c.CompareTag("Player"))
                    return true;

                Rigidbody rb = c.attachedRigidbody;
                if (rb != null && !rb.isKinematic)
                    return true;
            }

            return false;
        }

        private void SetBlockersClosed(bool closed)
        {
            if (blockers == null)
                return;

            for (int i = 0; i < blockers.Length; i++)
            {
                Collider c = blockers[i];
                if (c != null)
                    c.enabled = closed;
            }
        }

        private void PlayAudio(AudioClip clip)
        {
            if (clip == null)
                return;
            if (audioSource != null)
                audioSource.PlayOneShot(clip, audioVolume);
            else
                AudioSource.PlayClipAtPoint(clip, transform.position, audioVolume);
        }
    }
}
