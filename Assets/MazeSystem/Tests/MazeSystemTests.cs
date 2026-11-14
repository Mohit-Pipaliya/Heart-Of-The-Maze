using NUnit.Framework;
using UnityEngine;

namespace MazeSystem.Tests
{
    [TestFixture]
    public sealed class MazeSystemTests
    {
        // ───────────────────────────────────────────────────────────────
        //  1. Sequences Test
        // ───────────────────────────────────────────────────────────────
        [Test]
        public void Sequences_AllEightPermutations_AreValidPermutationsOf1To7()
        {
            var sequences = new MazeSequenceData();
            Assert.IsTrue(sequences.AllSequencesValid(out string error), $"Sequence validation failed: {error}");

            for (int s = 0; s < MazeSequenceData.SequenceCount; s++)
            {
                var seen = new bool[MazeSequenceData.StepsPerSequence + 1];
                for (int step = 0; step < MazeSequenceData.StepsPerSequence; step++)
                {
                    int block = sequences.Get(s, step);
                    Assert.GreaterOrEqual(block, 1, $"Sequence {s + 1} step {step + 1} is less than 1");
                    Assert.LessOrEqual(block, 7, $"Sequence {s + 1} step {step + 1} is greater than 7 (Maze 8 must never be in visitor sequence)");
                    Assert.IsFalse(seen[block], $"Sequence {s + 1} has duplicate Maze {block}");
                    seen[block] = true;
                }
            }
        }

        // ───────────────────────────────────────────────────────────────
        //  2. Slot Occupancy Map & Startup State
        // ───────────────────────────────────────────────────────────────
        [Test]
        public void OccupancyMap_InitializesWithSlot8AsActive()
        {
            var cycle = new MazeCycleState();
            cycle.ResetToOriginalLayout();

            Assert.AreEqual(8, cycle.occupancy.Length, "Must have exactly 8 slots.");
            Assert.AreEqual(8, cycle.occupancy[MazeBlockManager.ActiveSlotIndex], "Slot 8 must be occupied by Maze 8 at startup.");
            Assert.AreEqual(8, cycle.activeBlockNumber, "Active block number must be 8 at startup.");

            for (int i = 0; i < 8; i++)
            {
                Assert.AreEqual(i + 1, cycle.occupancy[i], $"Slot {i + 1} must start with Maze {i + 1}");
            }
        }

        // ───────────────────────────────────────────────────────────────
        //  3. Outside-The-Glade Travel System (Underground Glide)
        // ───────────────────────────────────────────────────────────────
        [Test]
        public void UndergroundGlide_NeverIntersectsGladeExclusionZone()
        {
            var planner = new MazeTravelPlanner
            {
                travelMode = MazeTravelMode.UndergroundGlide,
                sinkDepthPrimary = 65f,
                sinkDepthSecondary = 90f
            };

            Vector3 gladeCenter = new Vector3(4338f, 0f, 1000f);

            var exObj = new GameObject("Test_GladeExclusionZone");
            var col = exObj.AddComponent<BoxCollider>();
            col.center = Vector3.zero;
            col.size = new Vector3(150f, 60f, 150f); // spans Y: -30 to +30
            exObj.transform.position = gladeCenter;

            var exclusion = exObj.AddComponent<GladeExclusionZone>();
            exclusion.zoneCollider = col;
            exclusion.paddingMargin = 10f;

            // Slot A (West of Glade) to Slot B (East of Glade)
            Vector3 startA = gladeCenter + new Vector3(-200f, 0f, 0f);
            Vector3 targetA = gladeCenter + new Vector3(200f, 0f, 0f);

            Vector3 startB = targetA;
            Vector3 targetB = startA;

            planner.PlanSwapPaths(
                null, startA, Quaternion.identity, targetA, Quaternion.identity, 0, 1,
                null, startB, Quaternion.identity, targetB, Quaternion.identity, 1, 0,
                gladeCenter,
                out TravelPath pathA, out TravelPath pathB);

            Bounds blockBounds = new Bounds(Vector3.zero, new Vector3(80f, 40f, 80f));
            SweptValidationResult res = planner.ValidatePaths(pathA, blockBounds, pathB, blockBounds, exclusion, null, null);

            Object.DestroyImmediate(exObj);

            Assert.IsTrue(res.isValid, $"Underground travel must be safe and outside Glade! Error: {res.errorMessage}");
        }

        // ───────────────────────────────────────────────────────────────
        //  4. Separate Depth Lanes Prevent Mutual Collision
        // ───────────────────────────────────────────────────────────────
        [Test]
        public void MovingBlocks_HaveSeparateDepthLanes_NeverCollide()
        {
            var planner = new MazeTravelPlanner
            {
                travelMode = MazeTravelMode.UndergroundGlide,
                sinkDepthPrimary = 50f,
                sinkDepthSecondary = 110f // 60m vertical separation
            };

            Vector3 center = Vector3.zero;
            Vector3 startA = new Vector3(-100f, 0f, 0f);
            Vector3 targetA = new Vector3(100f, 0f, 0f);

            planner.PlanSwapPaths(
                null, startA, Quaternion.identity, targetA, Quaternion.identity, 0, 1,
                null, targetA, Quaternion.identity, startA, Quaternion.identity, 1, 0,
                center,
                out TravelPath pathA, out TravelPath pathB);

            Assert.AreEqual(0, pathA.depthLane);
            Assert.AreEqual(1, pathB.depthLane);

            // Test midpoints: vertical separation must be >= 50m
            Vector3 midA = pathA.EvaluatePosition(0.5f);
            Vector3 midB = pathB.EvaluatePosition(0.5f);
            float verticalSeparation = Mathf.Abs(midA.y - midB.y);

            Assert.GreaterOrEqual(verticalSeparation, 50f, "Moving blocks must have at least 50m vertical clearance during swap.");
        }

        // ───────────────────────────────────────────────────────────────
        //  5. Return Night Cycle Progression
        // ───────────────────────────────────────────────────────────────
        [Test]
        public void CycleProgression_WrapsCorrectly_Step8IsReturnNight()
        {
            var managerGo = new GameObject("Test_Manager");
            var manager = managerGo.AddComponent<MazeBlockManager>();

            // Step 0 through 6 are visitor nights (7 steps)
            for (int i = 0; i < 7; i++)
            {
                Assert.AreEqual(i, manager.CurrentStepIndex, $"Step index should be {i}");
                manager.AdvanceCycleStep();
            }

            // Step 7 is Return Night
            Assert.AreEqual(7, manager.CurrentStepIndex, "Step index 7 must be Return Night.");

            // After step 7, wraps to step 0 of next cycle
            manager.AdvanceCycleStep();
            Assert.AreEqual(0, manager.CurrentStepIndex, "Step should wrap to 0.");
            Assert.AreEqual(1, manager.CurrentCycleIndex, "Cycle should advance to 1.");

            Object.DestroyImmediate(managerGo);
        }

        // ───────────────────────────────────────────────────────────────
        //  6. Full 7+ Night Swap Order & Return Night Validation
        // ───────────────────────────────────────────────────────────────
        [Test]
        public void SwapOrder_SevenNights_FollowsActiveBlockVisitorSequence_AndReturnNightReturnsMaze8()
        {
            var sequences = new MazeSequenceData();
            var cycle = new MazeCycleState();
            cycle.ResetToOriginalLayout();

            Assert.AreEqual(8, cycle.occupancy[MazeBlockManager.ActiveSlotIndex], "Start: Maze 8 must occupy Active Slot.");

            // Night 1 through 7: Each visitor visits the Active Slot and swaps with current occupant
            for (int night = 0; night < 7; night++)
            {
                int currentActive = cycle.occupancy[MazeBlockManager.ActiveSlotIndex];
                int visitor = sequences.Get(0, night);

                // Find where visitor is currently sitting
                int visitorSlot = -1;
                for (int s = 0; s < 8; s++)
                {
                    if (cycle.occupancy[s] == visitor)
                    {
                        visitorSlot = s;
                        break;
                    }
                }

                Assert.AreNotEqual(-1, visitorSlot, $"Visitor Maze {visitor} must exist in occupancy map.");

                // Perform swap
                cycle.occupancy[visitorSlot] = currentActive;
                cycle.occupancy[MazeBlockManager.ActiveSlotIndex] = visitor;
                cycle.activeBlockNumber = visitor;

                Assert.AreEqual(visitor, cycle.occupancy[MazeBlockManager.ActiveSlotIndex],
                    $"Night {night + 1}: Visitor Maze {visitor} must now occupy the Active Slot.");
            }

            // Night 8: Return Night — Maze 8 returns to Active Slot
            int lastVisitor = cycle.occupancy[MazeBlockManager.ActiveSlotIndex];
            int slotOfMaze8 = -1;
            for (int s = 0; s < 8; s++)
            {
                if (cycle.occupancy[s] == 8)
                {
                    slotOfMaze8 = s;
                    break;
                }
            }

            Assert.AreNotEqual(-1, slotOfMaze8, "Maze 8 must exist in occupancy map before Return Night.");

            // Swap Maze 8 back to active
            cycle.occupancy[slotOfMaze8] = lastVisitor;
            cycle.occupancy[MazeBlockManager.ActiveSlotIndex] = 8;
            cycle.activeBlockNumber = 8;

            Assert.AreEqual(8, cycle.occupancy[MazeBlockManager.ActiveSlotIndex],
                "Night 8 (Return Night): Maze 8 must be back at the Active Block Position (Slot 8)!");
        }

        // ───────────────────────────────────────────────────────────────
        //  7. Main Entrance Door Open / Close State Transitions
        // ───────────────────────────────────────────────────────────────
        [Test]
        public void MazeDoor_OpenClose_StateTransitions()
        {
            var doorGo = new GameObject("Test_Door");
            var door = doorGo.AddComponent<MazeMainDoor>();

            // Initial State: Closed
            door.SetStateInstant(false);
            Assert.IsTrue(door.IsClosed, "Door should be closed initially.");
            Assert.IsFalse(door.IsOpen, "Door should not be open initially.");

            // Open State
            door.SetStateInstant(true);
            Assert.IsTrue(door.IsOpen, "Door should be open after SetStateInstant(true).");
            Assert.IsFalse(door.IsClosed, "Door should not be closed after opening.");

            // Close State
            door.SetStateInstant(false);
            Assert.IsTrue(door.IsClosed, "Door should be closed after SetStateInstant(false).");
            Assert.IsFalse(door.IsOpen, "Door should not be open after closing.");

            Object.DestroyImmediate(doorGo);
        }
    }
}
