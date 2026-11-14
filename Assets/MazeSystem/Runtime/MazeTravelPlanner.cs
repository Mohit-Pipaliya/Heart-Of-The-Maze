using System;
using System.Collections.Generic;
using UnityEngine;

namespace MazeSystem
{
    /// <summary>
    /// Outside-The-Glade Travel System Planner.
    /// Computes and validates trajectory paths that move blocks strictly outside/below the Glade.
    /// Guarantees that no moving block ever enters the Glade Exclusion Zone or collides with the other moving block.
    /// </summary>
    [Serializable]
    public sealed class MazeTravelPlanner
    {
        [Header("── Travel Configuration ───────────────────────")]
        [Tooltip("Active travel mode for nightly swaps.")]
        public MazeTravelMode travelMode = MazeTravelMode.UndergroundGlide;

        [Header("── Underground Glide Settings ─────────────────")]
        [Tooltip("Depth to sink during Underground Glide (meters below ground plane).")]
        [Range(20f, 150f)] public float sinkDepthPrimary = 65f;

        [Tooltip("Alternate depth for second moving block to guarantee zero mid-air/subterranean collision.")]
        [Range(20f, 150f)] public float sinkDepthSecondary = 90f;

        [Header("── Lift & Arc Settings ────────────────────────")]
        [Tooltip("Height to lift above walls during Lift & Arc mode (meters).")]
        [Range(30f, 200f)] public float liftHeightPrimary = 70f;
        [Range(30f, 200f)] public float liftHeightSecondary = 95f;

        [Header("── Outer Ring Route Settings ──────────────────")]
        [Tooltip("Radial clearance beyond the outer maze boundary for Outer Ring Route (meters).")]
        [Range(20f, 250f)] public float outerRingRadialPadding = 60f;

        [Header("── Validation Settings ────────────────────────")]
        [Tooltip("Number of sampling steps used to sweep bounds along the path.")]
        [Range(10, 100)] public int sweptValidationSteps = 30;

        /// <summary>
        /// Generates collision-free outside-the-glade travel paths for both moving blocks.
        /// </summary>
        public void PlanSwapPaths(
            MazeBlock activeBlock, Vector3 activeStartPos, Quaternion activeStartRot, Vector3 activeTargetPos, Quaternion activeTargetRot, int activeSourceSlot, int activeTargetSlot,
            MazeBlock incomingBlock, Vector3 incomingStartPos, Quaternion incomingStartRot, Vector3 incomingTargetPos, Quaternion incomingTargetRot, int incomingSourceSlot, int incomingTargetSlot,
            Vector3 gladeCenter,
            out TravelPath activePath,
            out TravelPath incomingPath)
        {
            activePath = BuildPath(
                activeBlock != null ? activeBlock.BlockNumber : 8,
                activeSourceSlot, activeTargetSlot,
                activeStartPos, activeStartRot,
                activeTargetPos, activeTargetRot,
                gladeCenter,
                depthLane: 0);

            incomingPath = BuildPath(
                incomingBlock != null ? incomingBlock.BlockNumber : 1,
                incomingSourceSlot, incomingTargetSlot,
                incomingStartPos, incomingStartRot,
                incomingTargetPos, incomingTargetRot,
                gladeCenter,
                depthLane: 1);
        }

        private TravelPath BuildPath(
            int blockNumber, int srcSlot, int dstSlot,
            Vector3 startPos, Quaternion startRot,
            Vector3 targetPos, Quaternion targetRot,
            Vector3 gladeCenter, int depthLane)
        {
            var path = new TravelPath
            {
                blockNumber = blockNumber,
                sourceSlotIndex = srcSlot,
                targetSlotIndex = dstSlot,
                mode = travelMode,
                depthLane = depthLane
            };

            switch (travelMode)
            {
                case MazeTravelMode.UndergroundGlide:
                    BuildUndergroundPath(path, startPos, startRot, targetPos, targetRot, gladeCenter, depthLane);
                    break;

                case MazeTravelMode.LiftAndArcOverOuterRing:
                    BuildLiftArcPath(path, startPos, startRot, targetPos, targetRot, gladeCenter, depthLane);
                    break;

                case MazeTravelMode.OuterRingRoute:
                default:
                    BuildOuterRingPath(path, startPos, startRot, targetPos, targetRot, gladeCenter, depthLane);
                    break;
            }

            ComputeDistancesAndProgress(path);
            return path;
        }

        private void BuildUndergroundPath(TravelPath path, Vector3 startPos, Quaternion startRot, Vector3 targetPos, Quaternion targetRot, Vector3 gladeCenter, int lane)
        {
            float depth = lane == 0 ? sinkDepthPrimary : sinkDepthSecondary;

            Vector3 undergroundStart = new Vector3(startPos.x, startPos.y - depth, startPos.z);
            Vector3 undergroundTarget = new Vector3(targetPos.x, targetPos.y - depth, targetPos.z);

            path.waypoints.Add(new TravelWaypoint(startPos, startRot, 0f));
            path.waypoints.Add(new TravelWaypoint(undergroundStart, startRot, 0.2f));

            // Curve slightly away from Glade center in XZ even underground for extra safety
            Vector3 midPoint = (undergroundStart + undergroundTarget) * 0.5f;
            Vector3 awayFromGlade = (midPoint - gladeCenter);
            awayFromGlade.y = 0f;
            if (awayFromGlade.sqrMagnitude > 1f)
            {
                midPoint += awayFromGlade.normalized * 30f;
                path.waypoints.Add(new TravelWaypoint(midPoint, Quaternion.Slerp(startRot, targetRot, 0.5f), 0.5f));
            }

            path.waypoints.Add(new TravelWaypoint(undergroundTarget, targetRot, 0.8f));
            path.waypoints.Add(new TravelWaypoint(targetPos, targetRot, 1f));
        }

        private void BuildLiftArcPath(TravelPath path, Vector3 startPos, Quaternion startRot, Vector3 targetPos, Quaternion targetRot, Vector3 gladeCenter, int lane)
        {
            float height = lane == 0 ? liftHeightPrimary : liftHeightSecondary;

            Vector3 liftedStart = new Vector3(startPos.x, startPos.y + height, startPos.z);
            Vector3 liftedTarget = new Vector3(targetPos.x, targetPos.y + height, targetPos.z);

            path.waypoints.Add(new TravelWaypoint(startPos, startRot, 0f));
            path.waypoints.Add(new TravelWaypoint(liftedStart, startRot, 0.2f));

            Vector3 midPoint = (liftedStart + liftedTarget) * 0.5f;
            Vector3 awayFromGlade = (midPoint - gladeCenter);
            awayFromGlade.y = 0f;
            if (awayFromGlade.sqrMagnitude > 1f)
            {
                midPoint += awayFromGlade.normalized * 50f;
                path.waypoints.Add(new TravelWaypoint(midPoint, Quaternion.Slerp(startRot, targetRot, 0.5f), 0.5f));
            }

            path.waypoints.Add(new TravelWaypoint(liftedTarget, targetRot, 0.8f));
            path.waypoints.Add(new TravelWaypoint(targetPos, targetRot, 1f));
        }

        private void BuildOuterRingPath(TravelPath path, Vector3 startPos, Quaternion startRot, Vector3 targetPos, Quaternion targetRot, Vector3 gladeCenter, int lane)
        {
            float radialOffset = outerRingRadialPadding + (lane * 35f);

            Vector3 startDir = (startPos - gladeCenter);
            startDir.y = 0f;
            Vector3 outerStart = startPos + startDir.normalized * radialOffset;

            Vector3 targetDir = (targetPos - gladeCenter);
            targetDir.y = 0f;
            Vector3 outerTarget = targetPos + targetDir.normalized * radialOffset;

            path.waypoints.Add(new TravelWaypoint(startPos, startRot, 0f));
            path.waypoints.Add(new TravelWaypoint(outerStart, startRot, 0.2f));

            // Arc along outer perimeter
            Vector3 midDir = (startDir.normalized + targetDir.normalized) * 0.5f;
            if (midDir.sqrMagnitude < 0.05f)
            {
                // Opposite sides: arc around exterior tangent
                midDir = Vector3.Cross(Vector3.up, startDir.normalized);
            }
            Vector3 outerMid = gladeCenter + midDir.normalized * (Mathf.Max(startDir.magnitude, targetDir.magnitude) + radialOffset);
            outerMid.y = (startPos.y + targetPos.y) * 0.5f;

            path.waypoints.Add(new TravelWaypoint(outerMid, Quaternion.Slerp(startRot, targetRot, 0.5f), 0.5f));
            path.waypoints.Add(new TravelWaypoint(outerTarget, targetRot, 0.8f));
            path.waypoints.Add(new TravelWaypoint(targetPos, targetRot, 1f));
        }

        private void ComputeDistancesAndProgress(TravelPath path)
        {
            if (path.waypoints.Count <= 1) return;

            float total = 0f;
            var segLengths = new float[path.waypoints.Count - 1];
            for (int i = 0; i < path.waypoints.Count - 1; i++)
            {
                float d = Vector3.Distance(path.waypoints[i].position, path.waypoints[i + 1].position);
                segLengths[i] = d;
                total += d;
            }
            path.totalDistance = total;

            if (total > 0.001f)
            {
                float accum = 0f;
                for (int i = 0; i < path.waypoints.Count; i++)
                {
                    if (i == 0)
                    {
                        var wp = path.waypoints[0];
                        wp.progress = 0f;
                        path.waypoints[0] = wp;
                    }
                    else
                    {
                        accum += segLengths[i - 1];
                        var wp = path.waypoints[i];
                        wp.progress = Mathf.Clamp01(accum / total);
                        path.waypoints[i] = wp;
                    }
                }
            }
        }

        /// <summary>
        /// Validates that both paths are 100% outside the Glade Exclusion Zone and do not intersect each other.
        /// </summary>
        public SweptValidationResult ValidatePaths(
            TravelPath pathA, Bounds boundsA,
            TravelPath pathB, Bounds boundsB,
            GladeExclusionZone exclusionZone,
            MazeBlock[] allBlocks,
            Transform player)
        {
            if (exclusionZone == null)
                return SweptValidationResult.Fail("Glade Exclusion Zone is missing.", -1, Vector3.zero);

            int steps = Mathf.Max(10, sweptValidationSteps);
            Vector3 halfA = boundsA.extents;
            Vector3 halfB = boundsB.extents;

            // Shrink mutual collision bounds by 15% margin so adjacent resting blocks in the 3x3 grid
            // do not register a false-positive collision at start/end slots.
            Vector3 colHalfA = halfA * 0.85f;
            Vector3 colHalfB = halfB * 0.85f;

            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;

                // 1. Check Path A against Glade Exclusion Zone
                Vector3 posA = pathA.EvaluatePosition(t);
                Bounds currentBoundsA = new Bounds(posA, halfA * 2f);
                if (exclusionZone.Intersects(currentBoundsA))
                {
                    return SweptValidationResult.Fail(
                        $"Block {pathA.blockNumber} trajectory intersects Glade Exclusion Zone at progress {t:P0}.",
                        i, posA);
                }

                // 2. Check Path B against Glade Exclusion Zone
                Vector3 posB = pathB.EvaluatePosition(t);
                Bounds currentBoundsB = new Bounds(posB, halfB * 2f);
                if (exclusionZone.Intersects(currentBoundsB))
                {
                    return SweptValidationResult.Fail(
                        $"Block {pathB.blockNumber} trajectory intersects Glade Exclusion Zone at progress {t:P0}.",
                        i, posB);
                }

                // 3. Check mutual collision between Block A and Block B during active horizontal transit (t: 0.25 to 0.75)
                // (Before 0.25 they sink vertically in their initial slots; after 0.75 they rise in target slots)
                if (t >= 0.25f && t <= 0.75f)
                {
                    Bounds colBoundsA = new Bounds(posA, colHalfA * 2f);
                    Bounds colBoundsB = new Bounds(posB, colHalfB * 2f);
                    if (colBoundsA.Intersects(colBoundsB))
                    {
                        return SweptValidationResult.Fail(
                            $"Collision detected between moving blocks ({pathA.blockNumber} and {pathB.blockNumber}) at progress {t:P0}.",
                            i, (posA + posB) * 0.5f);
                    }
                }

                // 4. Check intersection with Player
                if (player != null)
                {
                    if (currentBoundsA.Contains(player.position))
                        return SweptValidationResult.Fail($"Block {pathA.blockNumber} bounds intersect player position at progress {t:P0}.", i, posA);
                    if (currentBoundsB.Contains(player.position))
                        return SweptValidationResult.Fail($"Block {pathB.blockNumber} bounds intersect player position at progress {t:P0}.", i, posB);
                }
            }

            return SweptValidationResult.Success();
        }
    }
}
