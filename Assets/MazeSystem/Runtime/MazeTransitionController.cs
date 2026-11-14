using System.Collections;
using UnityEngine;

namespace MazeSystem
{
    /// <summary>
    /// Sequences the nightly transition (door close → fog in → swap → fog out)
    /// and the day-open sequence. All state changes are event-driven; nothing here polls.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MazeTransitionController : MonoBehaviour
    {
        public enum TransitionState
        {
            Idle,
            ClosingDoor,
            FogIn,
            Swapping,
            FogOut,
            OpeningDoor,
            Failed
        }

        [Header("── References ───────────────────────────────")]
        [SerializeField] private MazeBlockManager manager;
        [SerializeField] private GladeEntranceFog fog;
        [SerializeField] private GladeEntranceGate gate;

        [Header("── Night Timing (seconds) ───────────────────")]
        [Min(0.1f)] [SerializeField] private float nightSafetyTimeout = 16f;
        [Min(0.05f)] [SerializeField] private float doorRetryInterval = 0.35f;
        [Min(0f)] [SerializeField] private float fogFadeIn = 1.6f;
        [Min(0f)] [SerializeField] private float fogHoldAfterSwap = 1.2f;
        [Min(0f)] [SerializeField] private float swapSettleDelay = 0.35f;
        [Min(0f)] [SerializeField] private float fogFadeOut = 1.6f;
        [Min(0f)] [SerializeField] private float blockSlideDuration = 0.85f;

        [Header("── Day Timing (seconds) ────────────────────")]
        [Min(0f)] [SerializeField] private float dayDoorOpenDelay = 0.8f;
        [Min(0.1f)] [SerializeField] private float dayDoorTimeout = 6f;

        public TransitionState State { get; private set; } = TransitionState.Idle;
        public bool IsBusy => State != TransitionState.Idle && State != TransitionState.Failed;
        public bool HasQueuedDay { get; private set; }
        public bool HasQueuedNight { get; private set; }

        private Coroutine _nightRoutine;
        private Coroutine _dayRoutine;

        private void Awake()
        {
            if (manager == null)
                manager = GetComponent<MazeBlockManager>();
            if (manager == null)
                manager = GetComponentInParent<MazeBlockManager>();
        }

        /// <summary>Editor setup hook.</summary>
        public void EditorAssign(MazeBlockManager blockManager, GladeEntranceFog fogWall, GladeEntranceGate entranceGate)
        {
            manager = blockManager;
            fog = fogWall;
            gate = entranceGate;
        }

        public void HandleNightStart()
        {
            if (_nightRoutine != null)
            {
                HasQueuedNight = true;
                return;
            }

            if (_dayRoutine != null)
            {
                StopCoroutine(_dayRoutine);
                _dayRoutine = null;
            }

            HasQueuedDay = false;
            HasQueuedNight = false;
            _nightRoutine = StartCoroutine(NightRoutine());
        }

        public void HandleDayStart()
        {
            if (_nightRoutine != null)
            {
                HasQueuedDay = true;
                return;
            }

            if (_dayRoutine != null)
                return;

            _dayRoutine = StartCoroutine(DayRoutine());
        }

        /// <summary>Scene load / save restore: doors snap to the correct state, no animation.</summary>
        public void ApplyInitialState(bool night)
        {
            StopAllCoroutines();
            _nightRoutine = null;
            _dayRoutine = null;
            HasQueuedDay = false;
            HasQueuedNight = false;
            State = TransitionState.Idle;

            if (fog != null)
                fog.SetCoverage(0f);

            if (gate != null)
                gate.SetBlocked(night);

            if (manager != null)
                manager.SetActiveDoorInstant(!night);
        }

        public void ForceNight()
        {
            if (manager != null)
                manager.HandleNightStart();
        }

        public void ForceDay()
        {
            if (manager != null)
                manager.HandleDayStart();
        }

        private IEnumerator NightRoutine()
        {
            if (manager == null)
            {
                State = TransitionState.Failed;
                _nightRoutine = null;
                yield break;
            }

            State = TransitionState.ClosingDoor;
            if (gate != null)
                gate.SetBlocked(true);

            bool revealSwap = manager.RevealSwapToPlayer &&
                              manager.ActiveDoorFullyOpen &&
                              !manager.IsPlayerInsideActiveBlock();

            if (revealSwap)
                yield return RevealNightSequence();
            else
                yield return HiddenNightSequence();

            if (State == TransitionState.Failed)
            {
                _nightRoutine = null;
                if (HasQueuedDay)
                {
                    HasQueuedDay = false;
                    HandleDayStart();
                }
                yield break;
            }

            State = TransitionState.Idle;
            _nightRoutine = null;

            if (HasQueuedNight)
            {
                HasQueuedNight = false;
                HandleNightStart();
            }
            else if (HasQueuedDay)
            {
                HasQueuedDay = false;
                HandleDayStart();
            }
        }

        /// <summary>
        /// Visible swap: the entrance stays open (the gate already seals the Glade) so the
        /// player watches the old maze slide out and the new maze slide in. Doors close only
        /// after the blocks have settled.
        /// </summary>
        private IEnumerator RevealNightSequence()
        {
            MazeBlock incoming = manager.IncomingBlock;
            if (incoming != null && incoming.MainDoor != null)
                incoming.MainDoor.Open();

            State = TransitionState.FogIn;
            if (fog != null)
                yield return fog.FadeTo(manager.SwapRevealFogCoverage, fogFadeIn);

            State = TransitionState.Swapping;
            yield return manager.PerformNightSwapRoutine(manager.SwapRevealSlideDuration);
            yield return FinishSwapTail(manager.SwapRevealSlideDuration);

            State = TransitionState.ClosingDoor;
            yield return CloseDoorsForNight();

            if (!manager.NightDoorsClosed)
            {
                FailNight("doors did not close after the visible swap");
                yield break;
            }

            if (fogHoldAfterSwap > 0f)
                yield return new WaitForSeconds(fogHoldAfterSwap);

            State = TransitionState.FogOut;
            if (fog != null)
                yield return fog.FadeTo(0f, fogFadeOut);
        }

        /// <summary>Hidden swap: doors close first and the fog wall fully covers the move.</summary>
        private IEnumerator HiddenNightSequence()
        {
            yield return CloseDoorsForNight();

            if (!manager.NightDoorsClosed)
            {
                FailNight("doors did not close safely");
                yield break;
            }

            State = TransitionState.FogIn;
            if (fog != null)
                yield return fog.FadeTo(1f, fogFadeIn);

            State = TransitionState.Swapping;
            yield return manager.PerformNightSwapRoutine(blockSlideDuration);
            yield return FinishSwapTail(blockSlideDuration);

            if (fogHoldAfterSwap > 0f)
                yield return new WaitForSeconds(fogHoldAfterSwap);

            State = TransitionState.FogOut;
            if (fog != null)
                yield return fog.FadeTo(0f, fogFadeOut);
        }

        private IEnumerator FinishSwapTail(float slideDuration)
        {
            if (swapSettleDelay > 0f)
                yield return new WaitForSeconds(swapSettleDelay);

            if (manager.RestoreOriginalLayoutAfterEachCycle && manager.ReturnNightJustCompleted)
                yield return manager.RestoreOriginalLayoutRoutine(slideDuration);
        }

        private void FailNight(string reason)
        {
            State = TransitionState.Failed;
            Debug.LogError($"[MazeSystem] Night transition aborted: {reason}. " +
                           "Entrance stays locked until the next day start.", this);
        }

        private IEnumerator CloseDoorsForNight()
        {
            float elapsed = 0f;
            bool playerInsideLogged = false;

            while (elapsed < nightSafetyTimeout)
            {
                if (manager.DelayNightWhilePlayerInside && manager.IsPlayerInsideActiveBlock())
                {
                    if (!playerInsideLogged)
                    {
                        playerInsideLogged = true;
                        Debug.LogWarning("[MazeSystem] Player is still inside the active maze block. " +
                                         "Night transition waits for a safe moment before closing the main door.", this);
                    }
                }
                else
                {
                    manager.RequestCloseNightDoors();
                }

                if (manager.NightDoorsClosed)
                    yield break;

                elapsed += doorRetryInterval;
                yield return new WaitForSeconds(doorRetryInterval);
            }

            Debug.LogError("[MazeSystem] Night safety timeout reached while closing doors. Transition failed.", this);
        }

        private IEnumerator DayRoutine()
        {
            if (manager == null)
            {
                _dayRoutine = null;
                yield break;
            }

            if (dayDoorOpenDelay > 0f)
                yield return new WaitForSeconds(dayDoorOpenDelay);

            State = TransitionState.OpeningDoor;
            manager.OpenActiveDoor();

            float elapsed = 0f;
            while (elapsed < dayDoorTimeout && !manager.ActiveDoorFullyOpen)
            {
                elapsed += 0.1f;
                yield return new WaitForSeconds(0.1f);
            }

            if (gate != null)
                gate.SetBlocked(false);

            State = TransitionState.Idle;
            _dayRoutine = null;

            if (HasQueuedNight)
            {
                HasQueuedNight = false;
                HandleNightStart();
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            _nightRoutine = null;
            _dayRoutine = null;
            if (gate != null)
                gate.SetBlocked(false);
        }
    }
}
