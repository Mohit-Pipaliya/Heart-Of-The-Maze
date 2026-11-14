using System;
using System.Collections;
using UnityEngine;

namespace MazeSystem
{
    /// <summary>
    /// Master Manager for the Shifting Maze System.
    /// Controls:
    ///   • 8 fixed slot poses (Slot 8 = Active Block Position beside the Glade)
    ///   • Runtime atomic Slot Occupancy Map
    ///   • Outside-the-Glade Travel System (Underground Glide / Outer Ring Route)
    ///   • Glade Exclusion Zone boundary verification
    ///   • Contents freeze and restore (Enemies, NavMeshAgents, Pickups, Puzzle objects)
    ///   • 8 visitor sequences and Return Night cycle
    ///   • 3D audio, VFX dust/tremors, and cinematic camera trauma shake
    ///   • Built-in Day/Night cycle timer & manual swap trigger hooks
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    public sealed class MazeBlockManager : MonoBehaviour
    {
        public const int BlockCount = 8;
        public const int SlotCount = 8;
        public const int ActiveSlotIndex = 7; // Slot 8 = the fixed Active Block Position

        [Header("── 1. Blocks (Maze 1 → Maze 8) ──────────────")]
        [SerializeField] private MazeBlock[] blocks = new MazeBlock[BlockCount];

        [Header("── 2. Slots & Glade ─────────────────────────")]
        [Tooltip("Slot 1..8 fixed world poses. Slot 8 is the Active Block Position.")]
        [SerializeField] private MazeSlot[] slots = new MazeSlot[SlotCount];
        [SerializeField] private Transform activeBlockPosition;
        [Tooltip("Boundary pose between the Glade and Slot 8. Forward points from the Glade into the block.")]
        [SerializeField] private Transform gladeEntranceMarker;
        [SerializeField] private Transform gladeCenter;
        [SerializeField] private GladeExclusionZone gladeExclusionZone;
        [SerializeField] private MazeSlotPoseMode slotPoseMode = MazeSlotPoseMode.UseSlotPoseAsIs;
        [Range(0f, 0.5f)] [SerializeField] private float slotFitTolerance = 0.08f;

        [Header("── 3. Travel System (Outside-the-Glade) ─────")]
        [SerializeField] private MazeTravelPlanner travelPlanner = new MazeTravelPlanner();
        [Tooltip("Easing curve for block travel along outside/underground waypoint paths.")]
        [SerializeField] private AnimationCurve travelEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("── 4. Presentation & Feedback ───────────────")]
        [SerializeField] private MazeAudioController audioController;
        [SerializeField] private MazeVfxController vfxController;
        [SerializeField] private MazeCameraFeedback cameraFeedback;

        [Header("── 5. System References ─────────────────────")]
        [SerializeField] private MazeTransitionController transition;
        [SerializeField] private GladeEntranceFog fog;
        [SerializeField] private GladeEntranceGate gate;

        [Header("── 5b. Night / Day Auto Cycle ──────────────")]
        [Tooltip("Automatically advance between Day and Night using the durations below.")]
        [SerializeField] private bool autoAdvanceDayNight = false;
        [Min(5f)] [SerializeField] private float dayDuration = 180f;
        [Min(5f)] [SerializeField] private float nightDuration = 60f;

        public bool AutoAdvanceDayNight
        {
            get => autoAdvanceDayNight;
            set => autoAdvanceDayNight = value;
        }

        [Header("── 6. Visitor Sequences ─────────────────────")]
        [SerializeField] private MazeSequenceData sequences = new MazeSequenceData();
        [SerializeField] private bool useSingleSequenceForEveryCycle;

        [Header("── 7. Swap Rules ────────────────────────────")]
        [SerializeField] private bool restoreOriginalLayoutAfterEachCycle;
        [Tooltip("Wait for the player to leave the active block before closing its door. Prevents trapping the player.")]
        [SerializeField] private bool delayNightWhilePlayerInside = true;

        [Header("── 8. Visible Night Swap ────────────────────")]
        [Tooltip("Keep the entrance open and let the player watch the maze slide at night. Falls back to hidden swap while player is inside.")]
        [SerializeField] private bool revealSwapToPlayer = false;
        [Tooltip("Fog coverage used while the visible swap plays. 0 = clear view, 1 = fully hidden.")]
        [Range(0f, 1f)] [SerializeField] private float swapRevealFogCoverage = 0.18f;
        [Tooltip("Slide duration for the visible night swap. Longer values read as a smoother change.")]
        [Min(0.1f)] [SerializeField] private float swapRevealSlideDuration = 3.2f;

        [Header("── 9. Debug & Gizmos ────────────────────────")]
        [SerializeField] private bool verboseLogging;
        [SerializeField] private bool drawGizmos = true;
        [Range(0f, 90f)] [SerializeField] private float doorFacingTolerance = 15f;

        private MazeCycleState _cycleState = new MazeCycleState();
        private MazeBlock _activeBlock;
        private Transform _player;
        private bool _isCurrentlyNight;
        private float _phaseTimer;
        private bool _receivedDayNightSignal;
        private int _lastNightFrame = -1;
        private int _lastDayFrame = -1;

        // Visual gizmo preview paths
        private TravelPath _lastActivePath;
        private TravelPath _lastIncomingPath;

        public MazeBlock[] Blocks => blocks;
        public MazeSlot[] Slots => slots;
        public MazeSequenceData Sequences => sequences;
        public MazeCycleState CycleState => _cycleState;
        public MazeBlock ActiveBlock => _activeBlock;
        public bool IsNight => _isCurrentlyNight;
        public MazeTransitionController Transition => transition;
        public GladeEntranceFog Fog => fog;
        public GladeEntranceGate Gate => gate;
        public GladeExclusionZone ExclusionZone => gladeExclusionZone;
        public MazeTravelPlanner TravelPlanner => travelPlanner;
        public MazeAudioController AudioCtrl => audioController;
        public MazeVfxController VfxCtrl => vfxController;
        public MazeCameraFeedback CameraFeedbackCtrl => cameraFeedback;
        public Transform GladeEntranceMarker => gladeEntranceMarker;
        public Transform GladeCenter => gladeCenter;
        public MazeSlotPoseMode SlotPoseMode => slotPoseMode;
        public float SlotFitTolerance => slotFitTolerance;
        public float DoorFacingTolerance => doorFacingTolerance;
        public bool VerboseLogging => verboseLogging;
        public bool RestoreOriginalLayoutAfterEachCycle => restoreOriginalLayoutAfterEachCycle;
        public bool DelayNightWhilePlayerInside => delayNightWhilePlayerInside;
        public bool RevealSwapToPlayer => revealSwapToPlayer;
        public float SwapRevealFogCoverage => swapRevealFogCoverage;
        public float SwapRevealSlideDuration => swapRevealSlideDuration;
        public bool UseSingleSequence => useSingleSequenceForEveryCycle;
        public int CurrentCycleIndex => _cycleState.cycleIndex;
        public int CurrentStepIndex => _cycleState.stepIndex;
        public int[] Occupancy => _cycleState.occupancy;
        public bool ReturnNightJustCompleted { get; private set; }

        public MazeBlock IncomingBlock
        {
            get
            {
                int step = _cycleState.stepIndex;
                if (step >= MazeSequenceData.StepsPerSequence)
                    return GetBlock(8);

                int sequenceIndex = useSingleSequenceForEveryCycle
                    ? 0
                    : _cycleState.cycleIndex % MazeSequenceData.SequenceCount;

                if (sequences == null || !sequences.IsSequenceValid(sequenceIndex, out _))
                    return GetBlock(step + 1);

                return GetBlock(sequences.Get(sequenceIndex, step));
            }
        }

        public bool NightDoorsClosed
        {
            get
            {
                MazeBlock incoming = IncomingBlock;
                return IsDoorClosed(_activeBlock) && IsDoorClosed(incoming);
            }
        }

        // ───────────────────────────────────────────────────────────────
        //  Lifecycle
        // ───────────────────────────────────────────────────────────────

        private void Awake()
        {
            EnsureArrays();
            EnsureHelpers();

            GameObject playerObject = GameObject.FindWithTag("Player");
            if (playerObject != null)
                _player = playerObject.transform;

            _cycleState.EnsureOccupancy();
            _activeBlock = GetBlock(_cycleState.activeBlockNumber);
            if (_activeBlock == null && blocks != null && blocks.Length > 0)
                _activeBlock = blocks[blocks.Length - 1]; // Safe fallback to Maze 8

            if (transition == null)
                transition = GetComponent<MazeTransitionController>();

            if (gladeExclusionZone == null)
                gladeExclusionZone = GetComponentInChildren<GladeExclusionZone>();

            if (gladeExclusionZone == null && gladeCenter != null)
            {
                var zoneGo = new GameObject("Glade Exclusion Zone");
                zoneGo.transform.SetParent(gladeCenter, false);
                var col = zoneGo.AddComponent<BoxCollider>();
                col.size = new Vector3(80f, 150f, 80f);
                col.isTrigger = true;
                gladeExclusionZone = zoneGo.AddComponent<GladeExclusionZone>();
                gladeExclusionZone.zoneCollider = col;
            }
        }

        private void EnsureHelpers()
        {
            if (audioController == null)
                audioController = GetComponentInChildren<MazeAudioController>() ?? gameObject.AddComponent<MazeAudioController>();
            if (vfxController == null)
                vfxController = GetComponentInChildren<MazeVfxController>() ?? gameObject.AddComponent<MazeVfxController>();
            if (cameraFeedback == null)
                cameraFeedback = GetComponentInChildren<MazeCameraFeedback>() ?? gameObject.AddComponent<MazeCameraFeedback>();
        }

        private void Start()
        {
            if (transition == null)
            {
                transition = GetComponent<MazeTransitionController>();
            }

            AlignGladeAnchorsToActiveDoor();

            if (!_receivedDayNightSignal && transition != null)
            {
                transition.ApplyInitialState(_isCurrentlyNight);
            }
        }

        private void Update()
        {
            if (autoAdvanceDayNight && Application.isPlaying)
            {
                _phaseTimer += Time.deltaTime;
                float currentDuration = _isCurrentlyNight ? nightDuration : dayDuration;
                if (_phaseTimer >= currentDuration)
                {
                    _phaseTimer = 0f;
                    if (!_isCurrentlyNight)
                    {
                        _isCurrentlyNight = true;
                        TriggerNight();
                    }
                    else
                    {
                        _isCurrentlyNight = false;
                        TriggerDay();
                    }
                }
            }
        }

        [ContextMenu("Trigger Night Swap")]
        public void TriggerNight()
        {
            autoAdvanceDayNight = false;
            OnNightStartReceived();
        }

        [ContextMenu("Trigger Day Start")]
        public void TriggerDay()
        {
            autoAdvanceDayNight = false;
            OnDayStartReceived();
        }

        private void OnNightStartReceived()
        {
            if (Time.frameCount == _lastNightFrame) return;
            _lastNightFrame = Time.frameCount;
            _receivedDayNightSignal = true;
            _isCurrentlyNight = true;

            // Immediately command main door to close for night
            CloseActiveDoor();
            if (gate != null) gate.SetBlocked(true);

            if (transition != null)
                transition.HandleNightStart();
            else
                Debug.LogError("[MazeSystem] Night signal received but no MazeTransitionController is assigned.", this);
        }

        private void OnDayStartReceived()
        {
            if (Time.frameCount == _lastDayFrame) return;
            _lastDayFrame = Time.frameCount;
            _receivedDayNightSignal = true;
            _isCurrentlyNight = false;

            // Immediately command main door to open for day
            OpenActiveDoor();
            if (gate != null) gate.SetBlocked(false);

            if (transition != null)
                transition.HandleDayStart();
            else
                Debug.LogError("[MazeSystem] Day signal received but no MazeTransitionController is assigned.", this);
        }

        // ───────────────────────────────────────────────────────────────
        //  Night Swap Execution (Outside-The-Glade Travel Choreography)
        // ───────────────────────────────────────────────────────────────

        public IEnumerator PerformNightSwapRoutine(float slideDuration)
        {
            ReturnNightJustCompleted = false;

            MazeBlock active = _activeBlock;
            MazeBlock incoming = IncomingBlock;

            if (active == null || incoming == null)
            {
                Debug.LogError("[MazeSystem] Swap aborted: active or incoming block is missing.", this);
                yield break;
            }

            int activeNumber = active.BlockNumber;
            int incomingNumber = incoming.BlockNumber;

            if (activeNumber == incomingNumber)
            {
                Debug.LogWarning($"[MazeSystem] Incoming Maze {incomingNumber} is already active. Skipping safely.", this);
                AdvanceCycleStep();
                yield break;
            }

            int activeSlot = FindSlotOfBlock(activeNumber);
            int incomingSlot = FindSlotOfBlock(incomingNumber);

            if (activeSlot != ActiveSlotIndex || incomingSlot < 0 || incomingSlot == ActiveSlotIndex)
            {
                Debug.LogError($"[MazeSystem] Swap aborted: occupancy map is inconsistent " +
                               $"(active Maze {activeNumber} at slot {activeSlot + 1}, " +
                               $"incoming Maze {incomingNumber} at slot {incomingSlot + 1}).", this);
                yield break;
            }

            ComputeSlotPose(incoming, ActiveSlotIndex, out Vector3 incomingTargetPos, out Quaternion incomingTargetRot);
            ComputeSlotPose(active, incomingSlot, out Vector3 activeTargetPos, out Quaternion activeTargetRot);

            Vector3 activeStartPos = active.transform.position;
            Quaternion activeStartRot = active.transform.rotation;
            Vector3 incomingStartPos = incoming.transform.position;
            Quaternion incomingStartRot = incoming.transform.rotation;

            Vector3 gladePos = gladeCenter != null ? gladeCenter.position : transform.position;

            // 1. Plan Outside-The-Glade Trajectories
            travelPlanner.PlanSwapPaths(
                active, activeStartPos, activeStartRot, activeTargetPos, activeTargetRot, activeSlot, incomingSlot,
                incoming, incomingStartPos, incomingStartRot, incomingTargetPos, incomingTargetRot, incomingSlot, ActiveSlotIndex,
                gladePos,
                out TravelPath activePath,
                out TravelPath incomingPath);

            _lastActivePath = activePath;
            _lastIncomingPath = incomingPath;

            // 2. Validate Paths against Glade Exclusion Zone and Collisions
            if (gladeExclusionZone != null)
            {
                Bounds boundsA = active.GetFootprintWorldBounds();
                Bounds boundsB = incoming.GetFootprintWorldBounds();

                SweptValidationResult val = travelPlanner.ValidatePaths(
                    activePath, boundsA, incomingPath, boundsB, gladeExclusionZone, blocks, _player);

                if (!val.isValid)
                {
                    Debug.LogError($"[MazeSystem] Swap REJECTED by Outside-The-Glade Safety Validator: {val.errorMessage} (Segment {val.failedSegmentIndex})", this);
                    yield break;
                }
            }

            // 3. Freeze Contents (Enemies, NavMeshAgents, Pickups, Rigidbodies)
            active.Contents.FreezeBeforeMove();
            incoming.Contents.FreezeBeforeMove();

            // 4. Anticipation Tremor Feedback
            EnsureHelpers();
            Vector3 swapMidpoint = (activeStartPos + incomingStartPos) * 0.5f;
            if (audioController != null) audioController.StartTravelAudio(swapMidpoint);
            if (vfxController != null) vfxController.StartTravelVfx(swapMidpoint);
            if (cameraFeedback != null) cameraFeedback.AddTraumaFromEpicenter(swapMidpoint, 0.45f);

            // 5. Travel Along Outside-the-Glade Waypoint Trajectories
            float duration = Mathf.Max(0.5f, slideDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float rawT = Mathf.Clamp01(elapsed / duration);
                float curvedT = travelEase != null ? travelEase.Evaluate(rawT) : Mathf.SmoothStep(0f, 1f, rawT);

                Vector3 posA = activePath.EvaluatePosition(curvedT);
                Quaternion rotA = activePath.EvaluateRotation(curvedT);

                Vector3 posB = incomingPath.EvaluatePosition(curvedT);
                Quaternion rotB = incomingPath.EvaluateRotation(curvedT);

                active.SetTransformPose(posA, rotA);
                incoming.SetTransformPose(posB, rotB);

                Vector3 currentMid = (posA + posB) * 0.5f;
                if (audioController != null) audioController.UpdateAudioPosition(currentMid);
                if (vfxController != null) vfxController.UpdateVfxPosition(currentMid);
                if (cameraFeedback != null && rawT < 0.9f) cameraFeedback.AddTraumaFromEpicenter(currentMid, 0.08f * Time.deltaTime);

                yield return null;
            }

            // Snap final poses
            active.SetTransformPose(activeTargetPos, activeTargetRot);
            incoming.SetTransformPose(incomingTargetPos, incomingTargetRot);

            // 6. Arrival & Lock-In Impact
            if (audioController != null) audioController.StopTravelAudioAndPlayLock(incomingTargetPos);
            if (vfxController != null) vfxController.StopTravelVfxAndEmitLock(incomingTargetPos);
            if (cameraFeedback != null) cameraFeedback.AddTraumaFromEpicenter(incomingTargetPos, 0.65f);

            // 7. Unfreeze Contents & Warp NavMeshAgents
            active.Contents.UnfreezeAfterArrival();
            incoming.Contents.UnfreezeAfterArrival();

            // 8. Atomic Occupancy Map Update
            _cycleState.EnsureOccupancy();
            _cycleState.occupancy[incomingSlot] = activeNumber;
            _cycleState.occupancy[ActiveSlotIndex] = incomingNumber;
            _cycleState.activeBlockNumber = incomingNumber;
            _activeBlock = incoming;

            bool wasReturnNight = _cycleState.stepIndex >= MazeSequenceData.StepsPerSequence;
            AdvanceCycleStep();
            ReturnNightJustCompleted = wasReturnNight;

            AlignGladeAnchorsToActiveDoor();

            if (verboseLogging)
            {
                Debug.Log($"[MazeSystem] Night swap successfully completed outside Glade: Maze {activeNumber} → Slot {incomingSlot + 1}, " +
                          $"Maze {incomingNumber} → Active. Cycle {_cycleState.cycleIndex + 1}, step {_cycleState.stepIndex + 1}.", this);
            }
        }

        public IEnumerator RestoreOriginalLayoutRoutine(float slideDuration)
        {
            Vector3[] startPos = new Vector3[blocks.Length];
            Quaternion[] startRot = new Quaternion[blocks.Length];

            for (int i = 0; i < blocks.Length; i++)
            {
                if (blocks[i] == null) continue;
                blocks[i].Contents.FreezeBeforeMove();
                startPos[i] = blocks[i].transform.position;
                startRot[i] = blocks[i].transform.rotation;
            }

            float duration = Mathf.Max(0.5f, slideDuration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                for (int i = 0; i < blocks.Length; i++)
                {
                    MazeBlock b = blocks[i];
                    if (b == null) continue;
                    b.SetTransformPose(
                        Vector3.LerpUnclamped(startPos[i], b.OriginalPosition, t),
                        Quaternion.SlerpUnclamped(startRot[i], b.OriginalRotation, t));
                }
                yield return null;
            }

            for (int i = 0; i < blocks.Length; i++)
            {
                MazeBlock b = blocks[i];
                if (b == null) continue;
                b.SetTransformPose(b.OriginalPosition, b.OriginalRotation);
                b.Contents.UnfreezeAfterArrival();
            }

            _cycleState.ResetToOriginalLayout();
            _activeBlock = GetBlock(8);
            AlignGladeAnchorsToActiveDoor();

            if (verboseLogging)
                Debug.Log("[MazeSystem] Layout fully restored to original positions.", this);
        }

        // ───────────────────────────────────────────────────────────────
        //  Door and State Helpers
        // ───────────────────────────────────────────────────────────────

        public bool ActiveDoorFullyOpen
        {
            get
            {
                if (_activeBlock == null) return false;
                IMazeDoor door = _activeBlock.Door;
                return door != null && door.IsOpen;
            }
        }

        public void SetActiveDoorInstant(bool open)
        {
            if (_activeBlock != null)
            {
                IMazeDoor door = _activeBlock.Door;
                if (door != null) door.SetStateInstant(open);
            }
        }

        public void OpenActiveDoor()
        {
            if (_activeBlock != null)
            {
                IMazeDoor door = _activeBlock.Door;
                if (door != null) door.Open();
            }
        }

        public void CloseActiveDoor()
        {
            if (_activeBlock != null)
            {
                IMazeDoor door = _activeBlock.Door;
                if (door != null) door.Close();
            }
        }

        public void RequestCloseNightDoors()
        {
            if (_activeBlock != null)
            {
                IMazeDoor door = _activeBlock.Door;
                if (door != null) door.Close();
            }

            MazeBlock incoming = IncomingBlock;
            if (incoming != null)
            {
                IMazeDoor incomingDoor = incoming.Door;
                if (incomingDoor != null) incomingDoor.Close();
            }
        }

        public void HandleNightStart()
        {
            OnNightStartReceived();
        }

        public void HandleDayStart()
        {
            OnDayStartReceived();
        }

        public bool IsDoorClosed(MazeBlock block)
        {
            if (block == null) return true;
            IMazeDoor door = block.Door;
            if (door == null) return true;
            return door.IsClosed && !door.IsMoving;
        }

        // ───────────────────────────────────────────────────────────────
        //  Cycle and Slot Calculations
        // ───────────────────────────────────────────────────────────────

        public void AdvanceCycleStep()
        {
            _cycleState.stepIndex++;
            if (_cycleState.stepIndex > MazeSequenceData.StepsPerSequence)
            {
                _cycleState.stepIndex = 0;
                _cycleState.cycleIndex++;
            }
        }

        public void ComputeSlotPose(MazeBlock block, int slotIndex, out Vector3 position, out Quaternion rotation)
        {
            EnsureArrays();
            slotIndex = Mathf.Clamp(slotIndex, 0, SlotCount - 1);
            MazeSlot slot = slots[slotIndex];

            if (slot == null || slot.scale == Vector3.zero)
            {
                if (block != null)
                {
                    position = block.OriginalPosition;
                    rotation = block.OriginalRotation;
                }
                else
                {
                    position = transform.position;
                    rotation = Quaternion.identity;
                }
                return;
            }

            position = slot.position;
            rotation = slot.rotation;
        }

        public int FindSlotOfBlock(int blockNumber)
        {
            _cycleState.EnsureOccupancy();
            for (int i = 0; i < _cycleState.occupancy.Length; i++)
                if (_cycleState.occupancy[i] == blockNumber)
                    return i;
            return -1;
        }

        public MazeBlock GetBlock(int blockNumber)
        {
            int index = blockNumber - 1;
            if (blocks == null || index < 0 || index >= blocks.Length)
                return null;
            return blocks[index];
        }

        public bool IsPlayerInsideActiveBlock()
        {
            if (_player == null || _activeBlock == null)
                return false;

            Bounds b = _activeBlock.GetFootprintWorldBounds();
            return b.Contains(_player.position);
        }

        public void AlignGladeAnchorsToActiveDoor()
        {
            if (_activeBlock == null || gladeEntranceMarker == null) return;

            Transform doorT = _activeBlock.DoorTransform;
            if (doorT != null)
            {
                Vector3 outward = _activeBlock.DoorOutwardDirection;
                gladeEntranceMarker.SetPositionAndRotation(
                    doorT.position,
                    Quaternion.LookRotation(-outward, Vector3.up));
            }
        }

        // ───────────────────────────────────────────────────────────────
        //  Editor Setup Helpers
        // ───────────────────────────────────────────────────────────────

        public void EditorSetBlocks(MazeBlock[] newBlocks)
        {
            blocks = newBlocks;
        }

        public void SnapBlocksToSlots()
        {
            EnsureArrays();
            for (int i = 0; i < SlotCount && i < blocks.Length; i++)
            {
                if (blocks[i] != null && slots[i] != null && slots[i].scale != Vector3.zero)
                {
                    blocks[i].SetTransformPose(slots[i].position, slots[i].rotation);
                }
            }
        }

        public void EditorAssignReferences(
            MazeBlock[] blockArray,
            Transform activeSlot,
            Transform entranceMarker,
            Transform center,
            GladeExclusionZone exclusion,
            MazeTransitionController transitionCtrl,
            GladeEntranceFog fogWall,
            GladeEntranceGate entranceGate)
        {
            blocks = blockArray;
            activeBlockPosition = activeSlot;
            gladeEntranceMarker = entranceMarker;
            gladeCenter = center;
            gladeExclusionZone = exclusion;
            transition = transitionCtrl;
            fog = fogWall;
            gate = entranceGate;

            EnsureHelpers();
            _cycleState.ResetToOriginalLayout();
            _activeBlock = GetBlock(8);
        }

        public void EditorSetSequences(MazeSequenceData sequenceData, bool singleSequence)
        {
            sequences = sequenceData;
            useSingleSequenceForEveryCycle = singleSequence;
        }

        public void CaptureSlotsFromCurrentLayout()
        {
            EnsureArrays();
            for (int i = 0; i < SlotCount; i++)
            {
                MazeBlock b = (blocks != null && i < blocks.Length) ? blocks[i] : null;
                if (b != null)
                {
                    slots[i] = new MazeSlot(
                        i,
                        b.transform.position,
                        b.transform.rotation,
                        b.transform.lossyScale,
                        b.transform.TransformPoint(b.FootprintCenterLocal),
                        b.FootprintSize);
                }
            }
        }

        public void EditorResetLayoutNow()
        {
            if (blocks == null) return;
            for (int i = 0; i < blocks.Length; i++)
            {
                if (blocks[i] != null)
                    blocks[i].RestoreOriginalState();
            }
            _cycleState.ResetToOriginalLayout();
            _activeBlock = GetBlock(8);
            AlignGladeAnchorsToActiveDoor();
        }

        public void ForceNextNight()
        {
            if (transition != null)
                transition.HandleNightStart();
        }

        public void ForceDayStart()
        {
            if (transition != null)
                transition.HandleDayStart();
        }

        public void ForceResetLayout()
        {
            StartCoroutine(RestoreOriginalLayoutRoutine(1.2f));
        }

        private void EnsureArrays()
        {
            if (blocks == null || blocks.Length != BlockCount)
                blocks = new MazeBlock[BlockCount];
            if (slots == null || slots.Length != SlotCount)
                slots = new MazeSlot[SlotCount];
            for (int i = 0; i < SlotCount; i++)
            {
                if (slots[i] == null)
                    slots[i] = new MazeSlot { index = i, rotation = Quaternion.identity, scale = Vector3.one };
            }
        }

        public bool HasCapturedSlots
        {
            get
            {
                if (slots == null || slots.Length != SlotCount) return false;
                for (int i = 0; i < slots.Length; i++)
                    if (slots[i] != null && slots[i].footprintSize.sqrMagnitude > 0.0001f)
                        return true;
                return false;
            }
        }

        // ───────────────────────────────────────────────────────────────
        //  Gizmos
        // ───────────────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            // Draw Travel Path Waypoint trajectories
            if (_lastActivePath != null && _lastActivePath.waypoints.Count > 1)
            {
                Gizmos.color = Color.yellow;
                for (int i = 0; i < _lastActivePath.waypoints.Count - 1; i++)
                    Gizmos.DrawLine(_lastActivePath.waypoints[i].position, _lastActivePath.waypoints[i + 1].position);
            }

            if (_lastIncomingPath != null && _lastIncomingPath.waypoints.Count > 1)
            {
                Gizmos.color = Color.cyan;
                for (int i = 0; i < _lastIncomingPath.waypoints.Count - 1; i++)
                    Gizmos.DrawLine(_lastIncomingPath.waypoints[i].position, _lastIncomingPath.waypoints[i + 1].position);
            }

            // Draw Slots
            if (slots != null)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] == null) continue;
                    Gizmos.color = i == ActiveSlotIndex ? Color.green : new Color(0.2f, 0.7f, 1f, 0.4f);
                    Gizmos.DrawWireCube(slots[i].position, new Vector3(30f, 10f, 30f));
                }
            }
        }
    }
}
