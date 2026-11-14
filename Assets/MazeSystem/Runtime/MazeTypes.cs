using System;
using System.Collections.Generic;
using UnityEngine;

namespace MazeSystem
{
    public enum MazeSlotPoseMode
    {
        UseSlotPoseAsIs,
        PivotCorrected
    }

    /// <summary>
    /// Travel mode used when relocating blocks outside the Glade.
    /// </summary>
    public enum MazeTravelMode
    {
        /// <summary>Sinks straight down below ground, glides horizontally outside/below, rises into slot. (Default)</summary>
        UndergroundGlide,
        /// <summary>Follows author-defined/generated waypoints along the outer perimeter ring away from Glade.</summary>
        OuterRingRoute,
        /// <summary>Lifts straight up, arcs horizontally over outer perimeter walls, descends into target slot.</summary>
        LiftAndArcOverOuterRing
    }

    /// <summary>
    /// Fixed world pose of one maze slot. Captured once from the original layout.
    /// </summary>
    [Serializable]
    public sealed class MazeSlot
    {
        public int index;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public Vector3 scale = Vector3.one;

        /// <summary>World-space center of the block footprint that belongs to this slot.</summary>
        public Vector3 footprintCenter;
        public Vector3 footprintSize;

        public MazeSlot() { }

        public MazeSlot(int index, Vector3 position, Quaternion rotation, Vector3 scale,
                        Vector3 footprintCenter, Vector3 footprintSize)
        {
            this.index = index;
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
            this.footprintCenter = footprintCenter;
            this.footprintSize = footprintSize;
        }
    }

    /// <summary>
    /// Single waypoint along an outside-the-glade travel path.
    /// </summary>
    [Serializable]
    public struct TravelWaypoint
    {
        public Vector3 position;
        public Quaternion rotation;
        public float progress; // 0..1

        public TravelWaypoint(Vector3 position, Quaternion rotation, float progress)
        {
            this.position = position;
            this.rotation = rotation;
            this.progress = progress;
        }
    }

    /// <summary>
    /// Full waypoint trajectory for a block during a swap.
    /// </summary>
    [Serializable]
    public sealed class TravelPath
    {
        public int blockNumber;
        public int sourceSlotIndex;
        public int targetSlotIndex;
        public MazeTravelMode mode;
        public List<TravelWaypoint> waypoints = new List<TravelWaypoint>();
        public float totalDistance;
        public int depthLane; // 0 = primary lane, 1 = alternate lane (avoids block-to-block collision)

        public Vector3 EvaluatePosition(float t)
        {
            if (waypoints == null || waypoints.Count == 0)
                return Vector3.zero;
            if (waypoints.Count == 1 || t <= 0f)
                return waypoints[0].position;
            if (t >= 1f)
                return waypoints[waypoints.Count - 1].position;

            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                if (t >= waypoints[i].progress && t <= waypoints[i + 1].progress)
                {
                    float range = waypoints[i + 1].progress - waypoints[i].progress;
                    float localT = range > 0.0001f ? (t - waypoints[i].progress) / range : 0f;
                    return Vector3.Lerp(waypoints[i].position, waypoints[i + 1].position, localT);
                }
            }
            return waypoints[waypoints.Count - 1].position;
        }

        public Quaternion EvaluateRotation(float t)
        {
            if (waypoints == null || waypoints.Count == 0)
                return Quaternion.identity;
            if (waypoints.Count == 1 || t <= 0f)
                return waypoints[0].rotation;
            if (t >= 1f)
                return waypoints[waypoints.Count - 1].rotation;

            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                if (t >= waypoints[i].progress && t <= waypoints[i + 1].progress)
                {
                    float range = waypoints[i + 1].progress - waypoints[i].progress;
                    float localT = range > 0.0001f ? (t - waypoints[i].progress) / range : 0f;
                    return Quaternion.Slerp(waypoints[i].rotation, waypoints[i + 1].rotation, localT);
                }
            }
            return waypoints[waypoints.Count - 1].rotation;
        }
    }

    /// <summary>
    /// Result of swept-bounds collision check along a travel path.
    /// </summary>
    public struct SweptValidationResult
    {
        public bool isValid;
        public string errorMessage;
        public int failedSegmentIndex;
        public Vector3 failurePoint;

        public static SweptValidationResult Success() => new SweptValidationResult { isValid = true };
        public static SweptValidationResult Fail(string error, int segment, Vector3 pt) =>
            new SweptValidationResult { isValid = false, errorMessage = error, failedSegmentIndex = segment, failurePoint = pt };
    }

    /// <summary>
    /// Runtime cycle bookkeeping. Serialized so the debug inspector can show the exact state.
    /// </summary>
    [Serializable]
    public sealed class MazeCycleState
    {
        public int cycleIndex;
        public int stepIndex;          // 0..6 visitor nights, 7 = Return Night
        public int activeBlockNumber = 8;

        /// <summary>slot index (0..7) -> block number (1..8). 0 = empty.</summary>
        public int[] occupancy = new int[MazeBlockManager.SlotCount];

        public void ResetToOriginalLayout()
        {
            EnsureOccupancy();
            for (int i = 0; i < MazeBlockManager.SlotCount; i++)
                occupancy[i] = i + 1;
            activeBlockNumber = 8;
        }

        public void EnsureOccupancy()
        {
            if (occupancy == null || occupancy.Length != MazeBlockManager.SlotCount)
            {
                occupancy = new int[MazeBlockManager.SlotCount];
                for (int i = 0; i < MazeBlockManager.SlotCount; i++)
                    occupancy[i] = i + 1;
            }
        }
    }

    /// <summary>
    /// Eight visitor sequences, each a distinct permutation of Maze 1..7.
    /// Stored as one flat array so Unity serializes it reliably.
    /// </summary>
    [Serializable]
    public sealed class MazeSequenceData
    {
        public const int SequenceCount = 8;
        public const int StepsPerSequence = 7;
        public const int EntryCount = SequenceCount * StepsPerSequence;

        [SerializeField] private int[] entries = new int[EntryCount];

        // 8 curated distinct permutations of Maze 1..7
        private static readonly int[][] DefaultSequences = new int[][]
        {
            new int[] { 1, 2, 3, 4, 5, 6, 7 },
            new int[] { 2, 4, 6, 1, 3, 5, 7 },
            new int[] { 7, 6, 5, 4, 3, 2, 1 },
            new int[] { 3, 1, 5, 7, 2, 4, 6 },
            new int[] { 4, 7, 2, 5, 1, 6, 3 },
            new int[] { 5, 3, 6, 2, 7, 1, 4 },
            new int[] { 6, 5, 4, 3, 7, 2, 1 },
            new int[] { 1, 3, 7, 5, 2, 6, 4 }
        };

        public MazeSequenceData()
        {
            ResetToDefault();
        }

        public int Get(int sequenceIndex, int stepIndex)
        {
            EnsureSize();
            sequenceIndex = Mathf.Clamp(sequenceIndex, 0, SequenceCount - 1);
            stepIndex = Mathf.Clamp(stepIndex, 0, StepsPerSequence - 1);
            return entries[sequenceIndex * StepsPerSequence + stepIndex];
        }

        public void Set(int sequenceIndex, int stepIndex, int blockNumber)
        {
            EnsureSize();
            sequenceIndex = Mathf.Clamp(sequenceIndex, 0, SequenceCount - 1);
            stepIndex = Mathf.Clamp(stepIndex, 0, StepsPerSequence - 1);
            entries[sequenceIndex * StepsPerSequence + stepIndex] = blockNumber;
        }

        public void ResetToDefault()
        {
            entries = new int[EntryCount];
            for (int s = 0; s < SequenceCount; s++)
            {
                int[] seq = DefaultSequences[s % DefaultSequences.Length];
                for (int step = 0; step < StepsPerSequence; step++)
                    entries[s * StepsPerSequence + step] = seq[step];
            }
        }

        public bool IsSequenceValid(int sequenceIndex, out string error)
        {
            EnsureSize();
            var seen = new bool[StepsPerSequence + 1];
            for (int step = 0; step < StepsPerSequence; step++)
            {
                int block = Get(sequenceIndex, step);
                if (block < 1 || block > StepsPerSequence)
                {
                    error = $"Sequence {sequenceIndex + 1}, step {step + 1}: Maze {block} is invalid (expected 1..7).";
                    return false;
                }
                if (seen[block])
                {
                    error = $"Sequence {sequenceIndex + 1}: Maze {block} appears more than once.";
                    return false;
                }
                seen[block] = true;
            }

            error = null;
            return true;
        }

        public bool AllSequencesValid(out string error)
        {
            for (int s = 0; s < SequenceCount; s++)
                if (!IsSequenceValid(s, out error))
                    return false;

            error = null;
            return true;
        }

        public void EnsureSize()
        {
            if (entries == null || entries.Length != EntryCount)
            {
                ResetToDefault();
            }
        }
    }
}
