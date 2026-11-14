using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MazeSystem.EditorTools
{
    [CustomEditor(typeof(MazeBlockManager))]
    public sealed class MazeBlockManagerEditor : UnityEditor.Editor
    {
        private bool _showBlocks = true;
        private bool _showSlots = true;
        private bool _showTravel = true;
        private bool _showPresentation = true;
        private bool _showSequences = true;
        private bool _showRules = true;
        private bool _showDebug = true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var manager = (MazeBlockManager)target;

            // ── Big 1-Click Action Header ──────────────────────────
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 13,
                    alignment = TextAnchor.MiddleCenter
                };
                EditorGUILayout.LabelField("⚡ SHIFTING MAZE SYSTEM (AAA PRODUCTION)", titleStyle);
                EditorGUILayout.LabelField("Central Glade · 8 Surrounding Maze Blocks · Outside-The-Glade Swaps", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.Space(4f);

                GUI.backgroundColor = new Color(0.25f, 0.85f, 0.45f);
                if (GUILayout.Button("⚡ 1-Click Auto Setup & Fill All Inspector Fields", GUILayout.Height(34f)))
                {
                    MazeOneClickSetup.OneClickSetup();
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.Space(2f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Validate Setup", GUILayout.Height(22f)))
                    {
                        MazeSetupValidator.Validate(manager, out List<string> errors, out List<string> warnings);
                        string report = MazeSetupValidator.FormatReport(errors, warnings);
                        if (errors.Count > 0)
                            Debug.LogError("[MazeSystem] " + report, manager);
                        else if (warnings.Count > 0)
                            Debug.LogWarning("[MazeSystem] " + report, manager);
                        else
                            Debug.Log("[MazeSystem] " + report, manager);

                        EditorUtility.DisplayDialog("Maze System Validation", report, "OK");
                    }

                    if (GUILayout.Button("Recapture Slots", GUILayout.Height(22f)))
                    {
                        MazeOneClickSetup.RecaptureSlots();
                    }

                    if (GUILayout.Button("Reset Layout", GUILayout.Height(22f)))
                    {
                        Undo.RecordObject(manager, "Reset Maze Layout");
                        manager.EditorResetLayoutNow();
                        EditorUtility.SetDirty(manager);
                    }
                }
            }

            // Quick Status Health Check
            DrawStatusBadge(manager);

            EditorGUILayout.Space(6f);

            // ── Grouped Serialized Properties ──────────────────────
            _showBlocks = EditorGUILayout.Foldout(_showBlocks, "1. Maze Blocks (1 → 8)", true, EditorStyles.foldoutHeader);
            if (_showBlocks)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("blocks"), true);
                EditorGUI.indentLevel--;
            }

            _showSlots = EditorGUILayout.Foldout(_showSlots, "2. Slots & Glade Environment", true, EditorStyles.foldoutHeader);
            if (_showSlots)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("activeBlockPosition"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("gladeEntranceMarker"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("gladeCenter"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("gladeExclusionZone"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("slotPoseMode"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("slotFitTolerance"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("slots"), true);
                EditorGUI.indentLevel--;
            }

            _showTravel = EditorGUILayout.Foldout(_showTravel, "3. Outside-The-Glade Travel System", true, EditorStyles.foldoutHeader);
            if (_showTravel)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("travelPlanner"), true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("travelEase"));
                EditorGUI.indentLevel--;
            }

            _showPresentation = EditorGUILayout.Foldout(_showPresentation, "4. Audio, VFX & Camera Feedback", true, EditorStyles.foldoutHeader);
            if (_showPresentation)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("audioController"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("vfxController"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("cameraFeedback"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("fog"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("gate"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("autoAdvanceDayNight"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("dayDuration"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("nightDuration"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("transition"));
                EditorGUI.indentLevel--;
            }

            _showSequences = EditorGUILayout.Foldout(_showSequences, "5. Visitor Sequences", true, EditorStyles.foldoutHeader);
            if (_showSequences)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("useSingleSequenceForEveryCycle"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("sequences"), true);
                EditorGUI.indentLevel--;
            }

            _showRules = EditorGUILayout.Foldout(_showRules, "6. Swap Rules & Timing", true, EditorStyles.foldoutHeader);
            if (_showRules)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("restoreOriginalLayoutAfterEachCycle"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("delayNightWhilePlayerInside"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("revealSwapToPlayer"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("swapRevealFogCoverage"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("swapRevealSlideDuration"));
                EditorGUI.indentLevel--;
            }

            _showDebug = EditorGUILayout.Foldout(_showDebug, "7. Live Runtime State & Controls", true, EditorStyles.foldoutHeader);
            if (_showDebug)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("verboseLogging"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("drawGizmos"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("doorFacingTolerance"));

                DrawRuntimeDebug(manager);
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();

            if (Application.isPlaying)
                Repaint();
        }

        private static void DrawStatusBadge(MazeBlockManager manager)
        {
            bool hasSlots = manager.HasCapturedSlots;
            bool hasExclusion = manager.ExclusionZone != null;
            bool hasBlocks = manager.Blocks != null && manager.Blocks.Length == 8 && manager.Blocks[7] != null;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawIndicator("Schedule / Cycle", manager.IsNight ? "Night Mode" : "Day Mode", true);
                DrawIndicator("8 Maze Blocks", hasBlocks ? "All 8 Blocks Assigned" : "Blocks Incomplete", hasBlocks);
                DrawIndicator("Slot Poses", hasSlots ? "Captured & Valid" : "Not Captured Yet", hasSlots);
                DrawIndicator("Glade Exclusion Zone", hasExclusion ? "Active (Underground/Outer paths safe)" : "Missing Exclusion Zone", hasExclusion);
            }
        }

        private static void DrawIndicator(string label, string status, bool ok)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.color = ok ? new Color(0.2f, 0.9f, 0.3f) : new Color(0.95f, 0.35f, 0.2f);
                GUILayout.Label(ok ? "●" : "▲", GUILayout.Width(14f));
                GUI.color = Color.white;
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel, GUILayout.Width(150f));
                EditorGUILayout.LabelField(status, EditorStyles.miniLabel);
            }
        }

        private static void DrawRuntimeDebug(MazeBlockManager manager)
        {
            EditorGUILayout.Space(4f);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Cycle Index", manager.CurrentCycleIndex + 1);
                EditorGUILayout.IntField("Step Index", manager.CurrentStepIndex + 1);

                MazeBlock active = manager.ActiveBlock;
                MazeBlock incoming = manager.IncomingBlock;

                EditorGUILayout.TextField("Active Block", active != null ? $"Maze {active.BlockNumber}" : "—");
                EditorGUILayout.TextField("Incoming Block", incoming != null ? $"Maze {incoming.BlockNumber}" : "—");
                EditorGUILayout.TextField("Active Door", DescribeDoor(active));
                EditorGUILayout.TextField("Night Doors Closed", manager.NightDoorsClosed ? "Yes" : "No");

                if (manager.Transition != null)
                {
                    MazeTransitionController t = manager.Transition;
                    EditorGUILayout.TextField("Transition State", t.State.ToString());
                    EditorGUILayout.TextField("Queued Signals",
                        $"{(t.HasQueuedDay ? "Day" : "—")} / {(t.HasQueuedNight ? "Night" : "—")}");
                }

                if (manager.Gate != null)
                    EditorGUILayout.TextField("Glade Blocker Gate", manager.Gate.IsBlocked ? "BLOCKED (Glade Sealed)" : "Open");

                if (manager.Fog != null)
                    EditorGUILayout.TextField("Entrance Fog Coverage", $"{manager.Fog.Coverage:P0}");
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Slot Occupancy Map (Live)", EditorStyles.boldLabel);

            int[] occupancy = manager.Occupancy;
            if (occupancy != null)
            {
                for (int i = 0; i < occupancy.Length; i++)
                {
                    string suffix = i == MazeBlockManager.ActiveSlotIndex ? "  ★ ACTIVE (Glade Entrance)" : string.Empty;
                    EditorGUILayout.LabelField($"Slot {i + 1}", $"Maze {occupancy[i]}{suffix}");
                }
            }

            if (!Application.isPlaying)
                return;

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Force Next Night", GUILayout.Height(24f)))
                    manager.ForceNextNight();
                if (GUILayout.Button("Force Day Start", GUILayout.Height(24f)))
                    manager.ForceDayStart();
                if (GUILayout.Button("Reset Layout", GUILayout.Height(24f)))
                    manager.ForceResetLayout();
            }
        }

        private static string DescribeDoor(MazeBlock block)
        {
            if (block == null || block.MainDoor == null)
                return "—";

            MazeMainDoor door = block.MainDoor;
            string pending = door.HasPendingClose ? " (close held for player)" : string.Empty;
            return $"{door.State} · {door.Progress:P0}{pending}";
        }
    }
}
