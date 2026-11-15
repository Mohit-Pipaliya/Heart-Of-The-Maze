using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MazeManager))]
public class MazeManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        MazeManager mgr = (MazeManager)target;

        // Custom Header
        EditorGUILayout.Space(8);
        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUILayout.LabelField("🏛️ HEART OF THE MAZE - MANAGER", headerStyle);
        EditorGUILayout.HelpBox(
            "• 100% REALISTIC & LOGICAL (No Magic, No Slabs, No Cartoon Smoke):\n" +
            "• Day 1 starts with Block 8 directly facing the Glade entrance.\n" +
            "• Real Mechanical Sliding Door (DoubleSlidingDoor):\n" +
            "  - Night: Glade entrance sliding doors slide shut and latch with mechanical sound. Behind the closed door, the maze stone walls shift in darkness.\n" +
            "  - Day: Glade entrance sliding doors slide open, revealing the newly rearranged maze!", 
            MessageType.Info);

        EditorGUILayout.Space(4);

        // One-Click Setup Button
        GUI.backgroundColor = new Color(0.2f, 0.85f, 0.4f);
        if (GUILayout.Button("⚡ ONE-CLICK SETUP (Auto-Find Blocks & Door)", GUILayout.Height(36)))
        {
            Undo.RecordObject(mgr, "One-Click Setup Maze Manager");
            mgr.OneClickSetup();
            EditorUtility.SetDirty(mgr);
            if (!Application.isPlaying)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(mgr.gameObject.scene);
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(6);

        // Draw Standard Inspector
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("🎮 Live Simulation & Testing", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(1f, 0.85f, 0.3f);
        if (GUILayout.Button("☀️ Morning (Open Door)", GUILayout.Height(30)))
        {
            mgr.TestDayStart();
        }

        GUI.backgroundColor = new Color(0.35f, 0.55f, 0.95f);
        if (GUILayout.Button("🌙 Night (Close Door & Swap)", GUILayout.Height(30)))
        {
            mgr.TestNightSwap();
        }

        EditorGUILayout.EndHorizontal();

        GUI.backgroundColor = new Color(0.75f, 0.75f, 0.85f);
        if (GUILayout.Button("🚪 Toggle Entrance Door (Open / Close)", GUILayout.Height(28)))
        {
            mgr.TestToggleDoor();
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(6);
    }
}
