using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MazeSystem.EditorTools
{
    /// <summary>
    /// Live Editor Dashboard for the Shifting Maze System.
    /// Provides visual monitoring of slot occupancy, cycle progress, and direct controls to step nights/days.
    /// </summary>
    public sealed class MazeDebugWindow : EditorWindow
    {
        [MenuItem("Window/Maze Shift System (Dashboard)", priority = 100)]
        [MenuItem("Tools/Heart Of The Maze/Maze Shift Dashboard", priority = 30)]
        public static void ShowWindow()
        {
            var win = GetWindow<MazeDebugWindow>("Maze Shift");
            win.minSize = new Vector2(380, 480);
            win.Show();
        }

        private Vector2 _scrollPos;

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("🌙 SHIFTING MAZE DASHBOARD", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Real-Time Monitor & Outside-the-Glade Swapping Controls", EditorStyles.miniLabel);
            }

            MazeBlockManager manager = FindFirstObjectByType<MazeBlockManager>();
            if (manager == null)
            {
                EditorGUILayout.HelpBox("No MazeBlockManager found in the active scene.\nClick below to run 1-Click Setup.", MessageType.Warning);
                if (GUILayout.Button("⚡ Run 1-Click Setup Now", GUILayout.Height(30f)))
                {
                    MazeOneClickSetup.OneClickSetup();
                }
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Live Shift State", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.IntField("Cycle Index", manager.CurrentCycleIndex + 1);
                    EditorGUILayout.IntField("Step Index", manager.CurrentStepIndex + 1);
                    EditorGUILayout.TextField("Active Block", manager.ActiveBlock != null ? $"Maze {manager.ActiveBlock.BlockNumber}" : "None");
                    EditorGUILayout.TextField("Incoming Block", manager.IncomingBlock != null ? $"Maze {manager.IncomingBlock.BlockNumber}" : "None");
                    EditorGUILayout.TextField("Travel Mode", manager.TravelPlanner != null ? manager.TravelPlanner.travelMode.ToString() : "UndergroundGlide");
                }
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Slot Occupancy Map", EditorStyles.boldLabel);
            int[] occupancy = manager.Occupancy;
            if (occupancy != null && occupancy.Length == 8)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    for (int i = 0; i < 8; i++)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            bool isActive = i == MazeBlockManager.ActiveSlotIndex;
                            if (isActive) GUI.color = Color.green;
                            EditorGUILayout.LabelField($"Slot {i + 1}:", GUILayout.Width(60f));
                            EditorGUILayout.LabelField($"Maze {occupancy[i]} {(isActive ? "★ (Active Beside Glade)" : "")}");
                            GUI.color = Color.white;
                        }
                    }
                }
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Actions & Diagnostics", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validate Setup", GUILayout.Height(24f)))
                {
                    MazeSetupValidator.Validate(manager, out List<string> errors, out List<string> warnings);
                    string report = MazeSetupValidator.FormatReport(errors, warnings);
                    EditorUtility.DisplayDialog("Maze System Validation", report, "OK");
                }

                if (GUILayout.Button("1-Click Auto Setup", GUILayout.Height(24f)))
                {
                    MazeOneClickSetup.OneClickSetup();
                }
            }

            if (Application.isPlaying)
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("Play Mode Night / Day Controls", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Force Night Swap", GUILayout.Height(28f)))
                        manager.ForceNextNight();
                    if (GUILayout.Button("Force Day Open", GUILayout.Height(28f)))
                        manager.ForceDayStart();
                    if (GUILayout.Button("Reset Layout", GUILayout.Height(28f)))
                        manager.ForceResetLayout();
                }
            }

            EditorGUILayout.EndScrollView();

            if (Application.isPlaying)
                Repaint();
        }
    }
}
