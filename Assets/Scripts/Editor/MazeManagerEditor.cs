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
            "• Day 1 starts with Block 8 directly facing the Glade entrance.\n" +
            "• Each block swap automatically rotates the block so its main entrance door ALWAYS faces the Glade!\n" +
            "• When Night begins, ground smoke billows up from the floor, door closes, and blocks swap.\n" +
            "• When Day begins, smoke smoothly fades out and the Glade-facing door opens!", 
            MessageType.Info);

        EditorGUILayout.Space(4);

        // One-Click Setup Button
        GUI.backgroundColor = new Color(0.2f, 0.85f, 0.4f);
        if (GUILayout.Button("⚡ ONE-CLICK SETUP (Auto-Find Blocks & Fog)", GUILayout.Height(36)))
        {
            Undo.RecordObject(mgr, "One-Click Setup Maze Manager");
            mgr.OneClickSetup();
            mgr.EnsureEntranceFogVisuals(true);
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
        if (GUILayout.Button("☀️ Morning (Day)", GUILayout.Height(30)))
        {
            mgr.TestOpenDoor();
        }

        GUI.backgroundColor = new Color(0.35f, 0.55f, 0.95f);
        if (GUILayout.Button("🌙 Night (Smoke & Swap)", GUILayout.Height(30)))
        {
            mgr.TestNightSwap();
        }

        EditorGUILayout.EndHorizontal();

        GUI.backgroundColor = new Color(0.75f, 0.75f, 0.85f);
        if (GUILayout.Button("💨 Toggle Entrance Smoke (Fade In / Out)", GUILayout.Height(28)))
        {
            mgr.TestToggleFog();
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(6);
    }
}
