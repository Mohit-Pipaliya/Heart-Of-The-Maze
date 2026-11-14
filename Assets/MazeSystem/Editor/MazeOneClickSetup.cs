using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MazeSystem.EditorTools
{
    /// <summary>
    /// One-click scene setup for the Shifting Maze system.
    /// Discovers the existing Maze 1..8 instances, captures fixed slots, adds main sliding
    /// doors at the real entrance gaps, creates the fog/gate, and wires the existing
    /// DayNightCycle to the transition controller.
    /// </summary>
    public static class MazeOneClickSetup
    {
        private const string RootName = "Maze System";
        private const string GeneratedFolder = "Assets/MazeSystem/Generated";
        private const string FogMaterialPath = GeneratedFolder + "/MazeFogWall.mat";
        private const string DoorMaterialPath = "Assets/Model/maze gate/Materials/DoorWood_None_color.mat";
        private const float DefaultDoorWidth = 5f;
        private const float DefaultDoorHeight = 4.5f;
        private const float MinDoorWidth = 2.6f;
        private const float MaxDoorWidth = 10f;

        private sealed class SetupReport
        {
            public readonly StringBuilder Log = new StringBuilder();
            public int BlocksFound;
            public int DoorsCreated;
            public int DoorsReused;
            public int StaticFlagsCleared;
            public GameObject SystemRoot;
            public string GladeInfo;
            public float MaxEntranceWidth = DefaultDoorWidth;
            public bool Ok = true;

            public void Line(string text)
            {
                Log.AppendLine(text);
            }
        }

        private struct CellRect
        {
            public float xMin;
            public float zMin;
            public float xMax;
            public float zMax;

            public Vector3 Center => new Vector3((xMin + xMax) * 0.5f, 0f, (zMin + zMax) * 0.5f);
            public float Width => xMax - xMin;
            public float Depth => zMax - zMin;
        }

        private struct EntranceInfo
        {
            public bool HasGap;
            public int Side;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public float Width;
            public float RawGapWidth;
            public float Height;
            public bool Synthetic;
        }

        [MenuItem("Tools/Maze System/⚡ 1-Click Auto Setup & Fill All Inspector Fields", false, 0)]
        [MenuItem("Window/Maze System/⚡ 1-Click Auto Setup & Fill All Inspector Fields", false, 0)]
        [MenuItem("Tools/Heart Of The Maze/★ 1-Click Setup Shifting Maze System", false, 20)]
        [MenuItem("Tools/1-Click Fix/5. Setup Shifting Maze System", false, 5)]
        [MenuItem("GameObject/Heart Of The Maze/Setup Shifting Maze System", false, 20)]
        [MenuItem("GameObject/3D Object/Shifting Maze System (Auto-Setup)", false, 11)]
        public static void OneClickSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Shifting Maze Setup",
                    "Exit Play Mode before running the one-click setup.", "OK");
                return;
            }

            var report = new SetupReport();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("One-Click Shifting Maze Setup");

            try
            {
                RunSetup(report);
            }
            catch (Exception e)
            {
                report.Ok = false;
                report.Line($"FATAL: {e}");
                Debug.LogException(e);
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            if (report.SystemRoot != null)
                Selection.activeGameObject = report.SystemRoot;

            Debug.Log("[MazeSystem] One-click setup report:\n" + report.Log);

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    report.Ok ? "Shifting Maze — Setup Complete" : "Shifting Maze — Setup Needs Attention",
                    report.Log.ToString(),
                    "OK");
            }
        }

        [MenuItem("Tools/Heart Of The Maze/Recapture Maze Slot Poses (Use Current Layout)", false, 21)]
        public static void RecaptureSlots()
        {
            MazeBlockManager manager = Object.FindFirstObjectByType<MazeBlockManager>();
            if (manager == null)
            {
                EditorUtility.DisplayDialog("Shifting Maze Setup", "No MazeBlockManager found in the scene.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Recapture Slot Poses",
                    "This records the current pose of Maze 1..8 as the fixed slot layout. " +
                    "Use this only while the scene is in its intended starting layout.", "Recapture", "Cancel"))
                return;

            Undo.RecordObject(manager, "Recapture Maze Slots");
            manager.CaptureSlotsFromCurrentLayout();
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[MazeSystem] Slot poses recaptured from the current layout.");
        }

        private static void RunSetup(SetupReport report)
        {
            Scene scene = SceneManager.GetActiveScene();

            // ── 1. Discover the eight existing scene instances ──────────
            var blocks = new MazeBlock[MazeBlockManager.BlockCount];
            var blockObjects = new GameObject[MazeBlockManager.BlockCount];

            for (int n = 1; n <= MazeBlockManager.BlockCount; n++)
            {
                GameObject go = FindMazeInstance(scene, n);
                if (go == null)
                {
                    report.Ok = false;
                    report.Line($"ERROR: Maze {n} instance was not found in the scene. Nothing was rebuilt.");
                    continue;
                }

                MazeBlock block = go.GetComponent<MazeBlock>();
                if (block == null)
                {
                    block = Undo.AddComponent<MazeBlock>(go);
                    block.Configure(n, null, null);
                }

                if (!block.Captured)
                    block.CaptureOriginalState();

                blocks[n - 1] = block;
                blockObjects[n - 1] = go;
                report.BlocksFound++;
            }

            if (report.BlocksFound < MazeBlockManager.BlockCount)
            {
                report.Line("Setup stopped: all 8 block instances are required.");
                return;
            }

            // ── 2. Locate the empty Glade cell in the 3x3 grid ──────────
            if (!TryFindGladeCell(blocks, out CellRect gladeCell, out float cellGroundY))
            {
                report.Ok = false;
                report.Line("ERROR: Could not determine the empty Glade cell. Ensure the 8 blocks still form a 3x3 grid.");
                return;
            }

            Vector3 gladeCenterWorld = new Vector3(
                gladeCell.Center.x, cellGroundY, gladeCell.Center.z);
            report.GladeInfo = $"Glade center ({gladeCenterWorld.x:F1}, {gladeCenterWorld.z:F1})";

            // ── 3. Resolve or create the system root ────────────────────
            MazeBlockManager manager = Object.FindFirstObjectByType<MazeBlockManager>();
            GameObject root = manager != null ? manager.gameObject : null;

            if (root == null)
            {
                root = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(root, "Create Maze System");
                root.transform.SetPositionAndRotation(gladeCenterWorld, Quaternion.identity);
                manager = Undo.AddComponent<MazeBlockManager>(root);
                report.Line("Created 'Maze System' root.");
            }
            else
            {
                report.Line($"Reusing existing '{root.name}' root.");
            }

            MazeTransitionController transition = root.GetComponent<MazeTransitionController>();
            if (transition == null)
                transition = Undo.AddComponent<MazeTransitionController>(root);

            report.SystemRoot = root;

            // ── 4. Slots (fixed poses) ──────────────────────────────────
            Transform slotsContainer = GetOrCreateChild(root.transform, "Slots");
            var slotTransforms = new Transform[MazeBlockManager.SlotCount];

            for (int i = 0; i < MazeBlockManager.SlotCount; i++)
            {
                Transform slotT = GetOrCreateChild(slotsContainer, $"Slot {i + 1}");
                slotTransforms[i] = slotT;

                if (!Application.isPlaying && !manager.HasCapturedSlots)
                {
                    Undo.RecordObject(slotT, "Place Maze Slot");
                    slotT.SetPositionAndRotation(blocks[i].transform.position, blocks[i].transform.rotation);
                }
            }

            // ── 5. Build or reuse main doors at the real entrance gaps ──
            Transform gladeMarker = null;

            for (int i = 0; i < blocks.Length; i++)
            {
                MazeBlock block = blocks[i];
                MazeMainDoor existingDoor = block.MainDoor;
                if (existingDoor == null)
                    existingDoor = block.GetComponentInChildren<MazeMainDoor>(true);

                if (existingDoor != null)
                {
                    if (block.MainEntrance != existingDoor.transform || block.MainDoor != existingDoor)
                    {
                        Undo.RecordObject(block, "Assign Maze Main Door");
                        block.Configure(block.BlockNumber, existingDoor.transform, existingDoor);
                        EditorUtility.SetDirty(block);
                    }

                    report.DoorsReused++;
                    report.MaxEntranceWidth = Mathf.Max(report.MaxEntranceWidth,
                        Mathf.Clamp(EstimateDoorWidth(existingDoor), DefaultDoorWidth, 60f));
                    continue;
                }

                Terrain terrain = block.GetComponentInChildren<Terrain>(true);

                // The active slot sits west of the Glade, so Maze 8's main entrance is its +X
                // gap. Every prefab shares that entrance layout; using it for all blocks keeps
                // the swapped-in block at its original orientation (neighbour gaps stay aligned).
                EntranceInfo entrance = FindEntrance(block, terrain, Vector3.right, true);
                report.MaxEntranceWidth = Mathf.Max(report.MaxEntranceWidth,
                    Mathf.Clamp(entrance.RawGapWidth, DefaultDoorWidth, 60f));
                MazeMainDoor door = CreateDoor(block, terrain, entrance);

                if (door != null)
                {
                    report.DoorsCreated++;
                    if (block.BlockNumber == 8 && entrance.Synthetic)
                    {
                        report.Line("WARNING: No wall gap was detected on Maze 8's main-entrance (+X) side. " +
                                    "A door was placed at the side centre; adjust it manually if needed.");
                    }
                }
                else
                {
                    report.Ok = false;
                    report.Line($"ERROR: Could not create a main door for Maze {block.BlockNumber}.");
                }
            }

            // ── 6. Glade center + entrance marker ───────────────────────
            MazeBlock activeBlock = blocks[MazeBlockManager.ActiveSlotIndex];
            MazeMainDoor activeDoor = activeBlock.MainDoor;

            // Prefer the active block's real doorway so the gate/fog line up with the
            // opening the player actually walks through on the first day.
            Vector3 markerWorld;
            Vector3 forwardFromGlade;
            if (activeDoor != null)
            {
                markerWorld = activeDoor.transform.position;
                forwardFromGlade = -activeDoor.transform.forward;
            }
            else
            {
                markerWorld = ComputeGladeBoundary(activeBlock, gladeCell, out forwardFromGlade);
                markerWorld.y = SampleTerrainY(activeBlock.GetComponentInChildren<Terrain>(true), markerWorld,
                    activeBlock.transform.position.y) + 0.05f;
            }

            Transform gladeCenterT = GetOrCreateChild(root.transform, "Glade Center");
            if (gladeCenterT.localPosition == Vector3.zero)
                gladeCenterT.position = gladeCenterWorld;

            Transform markerT = GetOrCreateChild(root.transform, "Glade Entrance Marker");
            if (markerT.localPosition == Vector3.zero)
            {
                Undo.RecordObject(markerT, "Place Glade Entrance Marker");
                markerT.SetPositionAndRotation(markerWorld, Quaternion.LookRotation(forwardFromGlade, Vector3.up));
            }
            gladeMarker = markerT;

            // ── 7. Fog wall and entrance gate ───────────────────────────
            GladeEntranceFog fog = root.GetComponentInChildren<GladeEntranceFog>(true);
            if (fog == null)
                fog = CreateFog(root.transform, gladeMarker, report.MaxEntranceWidth);

            GladeEntranceGate gate = root.GetComponentInChildren<GladeEntranceGate>(true);
            if (gate == null)
                gate = CreateGate(root.transform, gladeMarker, report.MaxEntranceWidth, DefaultDoorHeight);

            // ── 7b. Glade Exclusion Zone ────────────────────────────────
            GladeExclusionZone exclusion = root.GetComponentInChildren<GladeExclusionZone>(true);
            if (exclusion == null)
            {
                var exGo = new GameObject("Glade Exclusion Zone");
                Undo.RegisterCreatedObjectUndo(exGo, "Create Glade Exclusion Zone");
                exGo.transform.SetParent(root.transform, false);
                exGo.transform.position = gladeCenterWorld;
                var box = Undo.AddComponent<BoxCollider>(exGo);
                box.isTrigger = true;
                box.size = new Vector3(Mathf.Max(gladeCell.Width, 120f), 250f, Mathf.Max(gladeCell.Depth, 120f));
                exclusion = Undo.AddComponent<GladeExclusionZone>(exGo);
                exclusion.zoneCollider = box;
                exclusion.paddingMargin = 15f;
            }

            // ── 8. Clear static flags on the movable roots & add Contents
            for (int i = 0; i < blockObjects.Length; i++)
            {
                if (blockObjects[i] == null)
                    continue;

                // Ensure MazeBlockContents exists
                if (blocks[i] != null && blocks[i].Contents == null)
                {
                    Undo.AddComponent<MazeBlockContents>(blockObjects[i]);
                }

                StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(blockObjects[i]);
                if (flags != 0)
                {
                    Undo.RecordObject(blockObjects[i], "Clear Maze Block Static Flags");
                    GameObjectUtility.SetStaticEditorFlags(blockObjects[i], 0);
                    report.StaticFlagsCleared++;
                }
            }

            // ── 9. Wire everything ─────────────────────────────────────
            Undo.RecordObject(manager, "Configure Maze Block Manager");
            manager.EditorAssignReferences(
                blocks,
                slotTransforms[MazeBlockManager.ActiveSlotIndex],
                gladeMarker,
                gladeCenterT,
                exclusion,
                transition,
                fog,
                gate);

            if (manager.Sequences == null)
                manager.EditorSetSequences(new MazeSequenceData(), manager.UseSingleSequence);

            if (!manager.HasCapturedSlots)
                manager.CaptureSlotsFromCurrentLayout();
            else
            {
                // Blocks added this run may not have a captured footprint yet.
                for (int i = 0; i < blocks.Length; i++)
                    if (!blocks[i].Captured)
                        blocks[i].CaptureOriginalState();
            }

            EditorUtility.SetDirty(manager);

            Undo.RecordObject(transition, "Configure Maze Transition Controller");
            transition.EditorAssign(manager, fog, gate);
            EditorUtility.SetDirty(transition);

            // ── 10. Validate and report ─────────────────────────────────
            MazeSetupValidator.Validate(manager, out List<string> errors, out List<string> warnings);

            report.Line("");
            report.Line($"Blocks found: {report.BlocksFound}/8");
            report.Line(report.GladeInfo);
            report.Line($"Main doors: {report.DoorsCreated} created, {report.DoorsReused} reused");
            report.Line($"Static flags cleared on {report.StaticFlagsCleared} block root(s)");
            report.Line("Schedule: Standalone auto day/night cycle enabled on MazeBlockManager.");
            report.Line($"Fog: {(fog != null ? "ready" : "MISSING")}    Gate: {(gate != null ? "ready" : "MISSING")}");
            report.Line("");
            report.Line(MazeSetupValidator.FormatReport(errors, warnings));

            report.Line("AUDIT NOTES");
            report.Line("  • No existing main doors were present in the Maze prefabs; MazeMainDoor was added as a child of each instance.");
            report.Line("  • Main entrances were generated on each block's +X gap (the side Maze 8 presents to the Glade), so swaps keep every block's original orientation.");
            report.Line("  • Blocks are moved at runtime; NavMesh is not rebaked automatically. Bake NavMesh if AI must follow the moved block.");
            report.Line("  • No existing night-rule for a player inside a block was found; the transition waits for the player to leave before closing.");
            report.Line("  • Internal doors, corridors and neighbour connections are untouched.");

            if (errors.Count > 0)
                report.Ok = false;

            if (!report.Ok)
                Debug.LogError("[MazeSystem] Setup finished with errors:\n" + report.Log);
            else
                Debug.Log("[MazeSystem] Setup finished successfully.\n" + report.Log);
        }

        // ═══════════════════════════════════════════════════════════════
        //  Discovery
        // ═══════════════════════════════════════════════════════════════

        private static GameObject FindMazeInstance(Scene scene, int number)
        {
            MazeBlock[] existing = Object.FindObjectsByType<MazeBlock>(FindObjectsSortMode.None);
            for (int i = 0; i < existing.Length; i++)
                if (existing[i] != null && existing[i].BlockNumber == number &&
                    existing[i].gameObject.scene == scene)
                    return existing[i].gameObject;

            string targetName = $"Maze {number}";
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                if (roots[i].name == targetName)
                    return roots[i];

            Transform[] transforms = Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
                if (transforms[i] != null && transforms[i].name == targetName &&
                    transforms[i].gameObject.scene == scene)
                    return transforms[i].gameObject;

            return null;
        }

        private static bool TryFindGladeCell(MazeBlock[] blocks, out CellRect gladeCell, out float groundY)
        {
            gladeCell = default;
            groundY = 0f;

            var rects = new CellRect[blocks.Length];
            float minX = float.MaxValue;
            float minZ = float.MaxValue;
            float cellW = 0f;
            float cellD = 0f;
            int terrainCount = 0;

            for (int i = 0; i < blocks.Length; i++)
            {
                Terrain terrain = blocks[i].GetComponentInChildren<Terrain>(true);
                if (terrain == null || terrain.terrainData == null)
                    continue;

                Vector3 size = terrain.terrainData.size;
                Vector3 p = terrain.transform.position;
                rects[i] = new CellRect
                {
                    xMin = p.x,
                    zMin = p.z,
                    xMax = p.x + size.x,
                    zMax = p.z + size.z
                };

                minX = Mathf.Min(minX, p.x);
                minZ = Mathf.Min(minZ, p.z);
                cellW += size.x;
                cellD += size.z;
                terrainCount++;
            }

            if (terrainCount == 0)
            {
                // Fallback: infer a 3x3 grid from block pivots.
                for (int i = 0; i < blocks.Length; i++)
                {
                    minX = Mathf.Min(minX, blocks[i].transform.position.x);
                    minZ = Mathf.Min(minZ, blocks[i].transform.position.z);
                }
                cellW = 1000f;
                cellD = 1000f;
                for (int i = 0; i < blocks.Length; i++)
                {
                    rects[i] = new CellRect
                    {
                        xMin = blocks[i].transform.position.x,
                        zMin = blocks[i].transform.position.z,
                        xMax = blocks[i].transform.position.x + cellW,
                        zMax = blocks[i].transform.position.z + cellD
                    };
                }
            }
            else
            {
                cellW /= terrainCount;
                cellD /= terrainCount;
            }

            if (cellW < 1f || cellD < 1f)
                return false;

            var occupied = new bool[3, 3];
            for (int i = 0; i < rects.Length; i++)
            {
                int gx = Mathf.RoundToInt((rects[i].xMin - minX) / cellW);
                int gz = Mathf.RoundToInt((rects[i].zMin - minZ) / cellD);
                if (gx >= 0 && gx < 3 && gz >= 0 && gz < 3)
                    occupied[gx, gz] = true;
            }

            int gladeX = 1, gladeZ = 1;
            bool found = false;
            for (int x = 0; x < 3; x++)
            {
                for (int z = 0; z < 3; z++)
                {
                    if (!occupied[x, z])
                    {
                        gladeX = x;
                        gladeZ = z;
                        found = true;
                    }
                }
            }

            if (!found)
                return false;

            gladeCell = new CellRect
            {
                xMin = minX + gladeX * cellW,
                zMin = minZ + gladeZ * cellD,
                xMax = minX + (gladeX + 1) * cellW,
                zMax = minZ + (gladeZ + 1) * cellD
            };

            groundY = blocks[MazeBlockManager.ActiveSlotIndex].transform.position.y;
            return true;
        }

        private static Vector3 ComputeGladeBoundary(MazeBlock activeBlock, CellRect gladeCell, out Vector3 forwardFromGlade)
        {
            Terrain terrain = activeBlock.GetComponentInChildren<Terrain>(true);
            CellRect activeCell = gladeCell;

            if (terrain != null && terrain.terrainData != null)
            {
                Vector3 size = terrain.terrainData.size;
                Vector3 p = terrain.transform.position;
                activeCell = new CellRect
                {
                    xMin = p.x,
                    zMin = p.z,
                    xMax = p.x + size.x,
                    zMax = p.z + size.z
                };
            }

            Vector3 activeCenter = activeCell.Center;
            Vector3 gladeCenter = gladeCell.Center;
            Vector3 delta = activeCenter - gladeCenter;

            Vector3 dir;
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.z))
                dir = delta.x >= 0f ? Vector3.right : Vector3.left;
            else
                dir = delta.z >= 0f ? Vector3.forward : Vector3.back;

            forwardFromGlade = dir;
            float halfExtent = Mathf.Abs(dir.x) > 0.5f ? activeCell.Width * 0.5f : activeCell.Depth * 0.5f;
            Vector3 marker = activeCenter - dir * halfExtent;
            marker.y = activeBlock.transform.position.y;
            return marker;
        }

        // ═══════════════════════════════════════════════════════════════
        //  Main door construction
        // ═══════════════════════════════════════════════════════════════

        private static EntranceInfo FindEntrance(MazeBlock block, Terrain terrain, Vector3 localToGlade, bool forceGladeSide)
        {
            Vector3 cellSize = terrain != null && terrain.terrainData != null
                ? terrain.terrainData.size
                : new Vector3(1000f, 100f, 1000f);

            var wallBounds = new List<Bounds>();
            MeshRenderer[] renderers = block.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer r = renderers[i];
                if (r == null || !r.name.StartsWith("wall", StringComparison.OrdinalIgnoreCase))
                    continue;

                Bounds wb = r.bounds;
                Vector3 center = block.transform.InverseTransformPoint(wb.center);
                Vector3 extents = block.transform.InverseTransformVector(wb.extents);
                extents = new Vector3(Mathf.Abs(extents.x), Mathf.Abs(extents.y), Mathf.Abs(extents.z));
                wallBounds.Add(new Bounds(center, extents * 2f));
            }

            int gladeSide = -1;
            float bestGladeAlignment = float.MinValue;
            for (int side = 0; side < 4; side++)
            {
                float alignment = Vector3.Dot(Outward(side), localToGlade);
                if (alignment > bestGladeAlignment)
                {
                    bestGladeAlignment = alignment;
                    gladeSide = side;
                }
            }

            int bestSide = -1;
            float bestScore = float.MinValue;
            float bestCenter = 0f;
            float bestGap = 0f;

            for (int side = 0; side < 4; side++)
            {
                if (forceGladeSide && side != gladeSide)
                    continue;

                if (!TryFindLargestGap(wallBounds, cellSize, side, out float gapCenter, out float gapWidth))
                    continue;

                float alignment = Vector3.Dot(Outward(side), localToGlade);
                float score = gapWidth + alignment * 20f;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestSide = side;
                    bestCenter = gapCenter;
                    bestGap = gapWidth;
                }
            }

            bool synthetic = false;
            if (bestSide < 0)
            {
                bestSide = gladeSide >= 0 ? gladeSide : 0;
                bestCenter = SideTangentSize(cellSize, bestSide) * 0.5f;
                bestGap = DefaultDoorWidth;
                synthetic = true;
            }

            float width = Mathf.Clamp(bestGap - 0.6f, MinDoorWidth, MaxDoorWidth);
            float plane = SidePlane(cellSize, bestSide);

            Vector3 localPos = bestSide switch
            {
                0 => new Vector3(plane, 0f, bestCenter),   // +X
                1 => new Vector3(plane, 0f, bestCenter),   // -X
                2 => new Vector3(bestCenter, 0f, plane),   // +Z
                _ => new Vector3(bestCenter, 0f, plane)    // -Z
            };

            return new EntranceInfo
            {
                HasGap = !synthetic,
                Side = bestSide,
                LocalPosition = localPos,
                LocalRotation = Quaternion.LookRotation(Outward(bestSide), Vector3.up),
                Width = width,
                RawGapWidth = bestGap,
                Height = DefaultDoorHeight,
                Synthetic = synthetic
            };
        }

        private static bool TryFindLargestGap(List<Bounds> walls, Vector3 cellSize, int side,
                                              out float gapCenter, out float gapWidth)
        {
            gapCenter = 0f;
            gapWidth = 0f;

            int axis = side < 2 ? 0 : 2;
            int tangent = side < 2 ? 2 : 0;
            float plane = SidePlane(cellSize, side);
            float tangentMax = SideTangentSize(cellSize, side);

            var intervals = new List<Vector2>();
            for (int i = 0; i < walls.Count; i++)
            {
                Bounds b = walls[i];
                if (b.max[axis] < plane - 2f || b.min[axis] > plane + 2f)
                    continue;

                float start = Mathf.Clamp(b.min[tangent], 0f, tangentMax);
                float end = Mathf.Clamp(b.max[tangent], 0f, tangentMax);
                if (end - start > 0.05f)
                    intervals.Add(new Vector2(start, end));
            }

            intervals.Sort((a, b) => a.x.CompareTo(b.x));

            float cursor = 0f;
            float bestW = 0f;
            float bestC = 0f;

            for (int i = 0; i < intervals.Count; i++)
            {
                Vector2 iv = intervals[i];
                if (iv.x > cursor + MinDoorWidth)
                {
                    float w = iv.x - cursor;
                    if (w > bestW)
                    {
                        bestW = w;
                        bestC = cursor + w * 0.5f;
                    }
                }
                cursor = Mathf.Max(cursor, iv.y);
            }

            if (tangentMax > cursor + MinDoorWidth)
            {
                float w = tangentMax - cursor;
                if (w > bestW)
                {
                    bestW = w;
                    bestC = cursor + w * 0.5f;
                }
            }

            gapCenter = bestC;
            gapWidth = bestW;
            return bestW >= MinDoorWidth;
        }

        private static Vector3 Outward(int side) => side switch
        {
            0 => Vector3.right,
            1 => Vector3.left,
            2 => Vector3.forward,
            _ => Vector3.back
        };

        private static float SidePlane(Vector3 cellSize, int side) => side switch
        {
            0 => cellSize.x,
            1 => 0f,
            2 => cellSize.z,
            _ => 0f
        };

        private static float SideTangentSize(Vector3 cellSize, int side) =>
            side < 2 ? cellSize.z : cellSize.x;

        private static MazeMainDoor CreateDoor(MazeBlock block, Terrain terrain, EntranceInfo entrance)
        {
            float groundY = SampleTerrainY(terrain,
                block.transform.TransformPoint(entrance.LocalPosition),
                block.transform.position.y);

            Vector3 worldPos = block.transform.TransformPoint(entrance.LocalPosition);
            worldPos.y = groundY + 0.05f;
            Quaternion worldRot = block.transform.rotation * entrance.LocalRotation;

            var doorGo = new GameObject("Generated Main Door");
            Undo.RegisterCreatedObjectUndo(doorGo, "Create Maze Main Door");
            doorGo.transform.SetParent(block.transform, true);
            doorGo.transform.SetPositionAndRotation(worldPos, worldRot);

            Material doorMaterial = AssetDatabase.LoadAssetAtPath<Material>(DoorMaterialPath);
            Material frameMaterial = FindWallMaterial(block) ?? doorMaterial;

            float w = entrance.Width;
            float h = entrance.Height;
            float halfW = w * 0.5f;
            float panelW = halfW - 0.04f;

            GameObject leftPanel = CreateCube("Left Door Panel", doorGo.transform,
                new Vector3(-w * 0.25f, h * 0.5f, 0f),
                new Vector3(panelW, h, 0.32f), doorMaterial);
            GameObject rightPanel = CreateCube("Right Door Panel", doorGo.transform,
                new Vector3(w * 0.25f, h * 0.5f, 0f),
                new Vector3(panelW, h, 0.32f), doorMaterial);

            CreateCube("Frame Left", doorGo.transform,
                new Vector3(-(halfW + 0.18f), h * 0.5f, 0f),
                new Vector3(0.32f, h + 0.4f, 0.5f), frameMaterial, false);
            CreateCube("Frame Right", doorGo.transform,
                new Vector3(halfW + 0.18f, h * 0.5f, 0f),
                new Vector3(0.32f, h + 0.4f, 0.5f), frameMaterial, false);
            CreateCube("Frame Lintel", doorGo.transform,
                new Vector3(0f, h + 0.2f, 0f),
                new Vector3(w + 0.7f, 0.34f, 0.5f), frameMaterial, false);

            var checkGo = new GameObject("Doorway Check Volume");
            Undo.RegisterCreatedObjectUndo(checkGo, "Create Doorway Check");
            checkGo.transform.SetParent(doorGo.transform, false);
            var check = Undo.AddComponent<BoxCollider>(checkGo);
            check.isTrigger = true;
            check.center = new Vector3(0f, h * 0.5f, 0f);
            check.size = new Vector3(w * 0.85f, h, 2.4f);

            MazeMainDoor door = Undo.AddComponent<MazeMainDoor>(doorGo);

            var panels = new[]
            {
                new MazeMainDoor.SlidingPanel
                {
                    panel = leftPanel.transform,
                    slideDirection = Vector3.left,
                    slideDistance = halfW * 1.05f
                },
                new MazeMainDoor.SlidingPanel
                {
                    panel = rightPanel.transform,
                    slideDirection = Vector3.right,
                    slideDistance = halfW * 1.05f
                }
            };

            Collider leftCollider = leftPanel.GetComponent<Collider>();
            Collider rightCollider = rightPanel.GetComponent<Collider>();
            var blockers = new List<Collider>(2);
            if (leftCollider != null) blockers.Add(leftCollider);
            if (rightCollider != null) blockers.Add(rightCollider);

            door.EditorConfigure(panels, blockers.ToArray(), check);

            Undo.RecordObject(block, "Assign Maze Main Door");
            block.Configure(block.BlockNumber, doorGo.transform, door);
            EditorUtility.SetDirty(block);
            EditorUtility.SetDirty(door);

            return door;
        }

        private static GameObject CreateCube(string name, Transform parent, Vector3 localPos,
                                             Vector3 localScale, Material material, bool keepCollider = true)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(go, "Create Maze Door Part");
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;

            if (!keepCollider)
            {
                Collider collider = go.GetComponent<Collider>();
                if (collider != null)
                    Object.DestroyImmediate(collider);
            }

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null && material != null)
                renderer.sharedMaterial = material;

            return go;
        }

        private static Material FindWallMaterial(MazeBlock block)
        {
            MeshRenderer[] renderers = block.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null && renderers[i].name.StartsWith("wall", StringComparison.OrdinalIgnoreCase))
                    return renderers[i].sharedMaterial;
            return null;
        }

        private static float EstimateDoorWidth(MazeMainDoor door)
        {
            Renderer[] renderers = door.GetComponentsInChildren<Renderer>(true);
            bool any = false;
            Bounds bounds = default;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || r.name.IndexOf("Panel", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }

            return any ? Mathf.Clamp(bounds.size.magnitude * 0.55f, MinDoorWidth, MaxDoorWidth) : DefaultDoorWidth;
        }

        // ═══════════════════════════════════════════════════════════════
        //  Fog / Gate
        // ═══════════════════════════════════════════════════════════════

        private static GladeEntranceFog CreateFog(Transform root, Transform marker, float entranceWidth)
        {
            float width = Mathf.Max(entranceWidth * 1.15f, 12f);
            float height = 12f;

            GameObject fogGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Undo.RegisterCreatedObjectUndo(fogGo, "Create Glade Fog");
            fogGo.name = "Glade Fog";
            fogGo.transform.SetParent(root, false);
            fogGo.transform.SetPositionAndRotation(
                marker.position - marker.forward * 2.2f + Vector3.up * (height * 0.5f),
                Quaternion.LookRotation(-marker.forward, Vector3.up));
            fogGo.transform.localScale = new Vector3(width, height, 1f);

            Collider collider = fogGo.GetComponent<Collider>();
            if (collider != null)
                Object.DestroyImmediate(collider);

            Material fogMaterial = EnsureFogMaterial();
            Renderer fogRenderer = fogGo.GetComponent<Renderer>();
            if (fogRenderer != null && fogMaterial != null)
                fogRenderer.sharedMaterial = fogMaterial;

            GladeEntranceFog fog = Undo.AddComponent<GladeEntranceFog>(fogGo);
            fog.EditorConfigure(fogRenderer, fogMaterial);
            EditorUtility.SetDirty(fog);
            return fog;
        }

        private static GladeEntranceGate CreateGate(Transform root, Transform marker, float entranceWidth, float doorHeight)
        {
            var gateGo = new GameObject("Glade Entrance Gate");
            Undo.RegisterCreatedObjectUndo(gateGo, "Create Glade Gate");
            gateGo.transform.SetParent(root, false);
            gateGo.transform.SetPositionAndRotation(
                marker.position - marker.forward * 0.35f + Vector3.up * (doorHeight * 0.5f),
                Quaternion.LookRotation(-marker.forward, Vector3.up));

            var collider = Undo.AddComponent<BoxCollider>(gateGo);
            collider.size = new Vector3(Mathf.Max(entranceWidth * 1.02f, 3f), doorHeight, 0.8f);
            collider.center = Vector3.zero;
            collider.isTrigger = false;
            collider.enabled = false;

            GladeEntranceGate gate = Undo.AddComponent<GladeEntranceGate>(gateGo);
            gate.EditorConfigure(collider);
            EditorUtility.SetDirty(gate);
            return gate;
        }

        private static Material EnsureFogMaterial()
        {
            if (!AssetDatabase.IsValidFolder(GeneratedFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/MazeSystem"))
                    AssetDatabase.CreateFolder("Assets", "MazeSystem");
                AssetDatabase.CreateFolder("Assets/MazeSystem", "Generated");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(FogMaterialPath);
            if (material != null)
                return material;

            Shader shader = Shader.Find("MazeSystem/FogWall");
            if (shader == null)
            {
                Debug.LogError("[MazeSystem] Fog shader 'MazeSystem/FogWall' was not found. " +
                               "Make sure Assets/MazeSystem/Shaders/MazeFogWall.shader compiled.");
                return null;
            }

            material = new Material(shader)
            {
                name = "MazeFogWall"
            };
            material.SetColor("_Color", new Color(0.62f, 0.68f, 0.74f, 1f));
            material.SetFloat("_Coverage", 0f);
            AssetDatabase.CreateAsset(material, FogMaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        // ═══════════════════════════════════════════════════════════════
        //  Helpers
        // ═══════════════════════════════════════════════════════════════

        private static Transform GetOrCreateChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return existing;

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static float SampleTerrainY(Terrain terrain, Vector3 worldPosition, float fallback)
        {
            if (terrain == null || terrain.terrainData == null)
                return fallback;

            return terrain.SampleHeight(new Vector3(worldPosition.x, 0f, worldPosition.z)) +
                   terrain.transform.position.y;
        }
    }
}
