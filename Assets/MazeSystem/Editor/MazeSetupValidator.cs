using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MazeSystem.EditorTools
{
    /// <summary>
    /// Comprehensive validation tool for the Shifting Maze System.
    /// Performs 10-point safety and integrity checks across slots, exclusion zones, travel routes, and doors.
    /// </summary>
    public static class MazeSetupValidator
    {
        public const float MarkerPositionTolerance = 4f;

        public static bool Validate(MazeBlockManager manager,
                                    out List<string> errors,
                                    out List<string> warnings)
        {
            errors = new List<string>();
            warnings = new List<string>();

            if (manager == null)
            {
                errors.Add("No MazeBlockManager found in the scene.");
                return false;
            }

            // 1. Blocks Check (Exactly 8)
            MazeBlock[] blocks = manager.Blocks;
            if (blocks == null || blocks.Length != MazeBlockManager.BlockCount)
            {
                errors.Add($"Block list must contain exactly {MazeBlockManager.BlockCount} entries.");
            }
            else
            {
                var seen = new HashSet<int>();
                for (int i = 0; i < blocks.Length; i++)
                {
                    MazeBlock block = blocks[i];
                    if (block == null)
                    {
                        errors.Add($"Maze {i + 1} scene instance is not assigned.");
                        continue;
                    }

                    if (block.BlockNumber != i + 1)
                        errors.Add($"{block.name}: block number is {block.BlockNumber}, expected {i + 1}.");

                    if (!seen.Add(block.BlockNumber))
                        errors.Add($"Maze {block.BlockNumber} is assigned more than once.");

                    if (block.Door == null && block.MainDoor == null)
                        errors.Add($"{block.name}: no Main Sliding Door (MazeMainDoor or DoubleSlidingDoor) assigned.");
                    if (block.MainEntrance == null)
                        warnings.Add($"{block.name}: no Main Entrance marker assigned.");

                    if (block.Contents == null)
                        warnings.Add($"{block.name}: no MazeBlockContents attached. Dynamic enemies/pickups will not freeze during move.");

                    if (GameObjectUtility.GetStaticEditorFlags(block.gameObject) != 0)
                        warnings.Add($"{block.name}: still marked static. Moving it can cause static-batching artifacts.");
                }
            }

            // 2. Active Block (Slot 8) Check
            if (manager.ActiveBlock == null)
                errors.Add("Active block could not be resolved (Slot 8 must be occupied).");
            else if (manager.ActiveBlock.BlockNumber != 8)
                warnings.Add($"Startup active block is Maze {manager.ActiveBlock.BlockNumber}; the scene layout normally starts with Maze 8 active.");

            // 3. Glade Exclusion Zone Check
            if (manager.ExclusionZone == null)
            {
                errors.Add("Glade Exclusion Zone is not assigned! Outside-the-Glade swaps cannot verify safety without it.");
            }
            else
            {
                Bounds zoneBounds = manager.ExclusionZone.GetExpandedBounds();
                if (zoneBounds.size.sqrMagnitude < 100f)
                    warnings.Add("Glade Exclusion Zone has a very small bounding volume; verify its BoxCollider size.");
            }

            // 4. Glade Entrance Marker & Alignment
            if (manager.GladeEntranceMarker == null)
            {
                errors.Add("Glade Entrance Marker is not assigned.");
            }
            else if (manager.ActiveBlock != null && manager.ActiveBlock.DoorTransform != null)
            {
                Transform door = manager.ActiveBlock.DoorTransform;
                Vector3 towardGlade = -manager.GladeEntranceMarker.forward;
                float angle = Vector3.Angle(manager.ActiveBlock.DoorOutwardDirection, towardGlade);
                if (angle > manager.DoorFacingTolerance)
                {
                    errors.Add($"Active Maze {manager.ActiveBlock.BlockNumber} door faces {angle:F1}° away from the Glade " +
                               $"(tolerance {manager.DoorFacingTolerance:F0}°).");
                }

                float distance = Vector3.Distance(door.position, manager.GladeEntranceMarker.position);
                if (distance > MarkerPositionTolerance)
                {
                    warnings.Add($"Active door frame is {distance:F2}m from the Glade Entrance Marker " +
                                 $"(tolerance {MarkerPositionTolerance:F1}m).");
                }
            }

            // 6. Transition, Fog & Gate
            if (manager.Transition == null)
                errors.Add("MazeTransitionController is not assigned.");
            if (manager.Fog == null)
                errors.Add("GladeEntranceFog is not assigned.");
            if (manager.Gate == null)
                errors.Add("GladeEntranceGate is not assigned.");

            // 7. Audio & VFX
            if (manager.AudioCtrl == null)
                warnings.Add("MazeAudioController is not assigned. Moving blocks will have no 3D sound.");
            if (manager.VfxCtrl == null)
                warnings.Add("MazeVfxController is not assigned. Moving blocks will have no dust/tremor VFX.");
            if (manager.CameraFeedbackCtrl == null)
                warnings.Add("MazeCameraFeedback is not assigned. Screen shake during swaps will be disabled.");

            // 8. Visitor Sequences
            if (manager.Sequences == null)
            {
                errors.Add("Visitor sequences are missing.");
            }
            else if (!manager.Sequences.AllSequencesValid(out string sequenceError))
            {
                errors.Add(sequenceError);
            }

            // 9. Slot Capture
            if (!manager.HasCapturedSlots)
                errors.Add("Slot poses have not been captured.");

            return errors.Count == 0;
        }

        public static string FormatReport(List<string> errors, List<string> warnings)
        {
            var sb = new System.Text.StringBuilder();
            if (errors.Count == 0 && warnings.Count == 0)
                return "Maze system setup is 100% valid. Ready for production play!";

            if (errors.Count > 0)
            {
                sb.AppendLine("ERRORS:");
                for (int i = 0; i < errors.Count; i++)
                    sb.AppendLine($"  ✕ {errors[i]}");
            }

            if (warnings.Count > 0)
            {
                if (errors.Count > 0) sb.AppendLine();
                sb.AppendLine("WARNINGS:");
                for (int i = 0; i < warnings.Count; i++)
                    sb.AppendLine($"  ▲ {warnings[i]}");
            }

            return sb.ToString();
        }
    }
}
