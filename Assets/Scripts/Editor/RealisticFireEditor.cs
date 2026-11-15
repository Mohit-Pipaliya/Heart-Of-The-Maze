#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RealisticFire))]
[CanEditMultipleObjects]
public class RealisticFireEditor : Editor
{
    public override void OnInspectorGUI()
    {
        RealisticFire fire = (RealisticFire)target;

        EditorGUILayout.Space(6);
        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUILayout.LabelField("🔥 REALISTIC AUTO-FIRE SYSTEM", headerStyle);
        EditorGUILayout.HelpBox(
            "• Auto-Ignite: Game play karte hi bina lighter ke direct jalne lagega.\n" +
            "• Any Object: Mashal, Campfire, Wall Torch, ya kisi bhi GameObject par laga sakte hain.\n" +
            "• Includes: Main flame, hot core, embers/sparks, smoke, flickering light & 3D sound.",
            MessageType.Info
        );

        EditorGUILayout.Space(4);

        // Preset Buttons
        EditorGUILayout.LabelField("⚡ Quick Presets:", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🔥 Torch / Mashal"))
        {
            Undo.RecordObject(fire, "Set Torch Preset");
            fire.fireScale = 1.0f;
            fire.fireIntensity = 1.0f;
            fire.lightRange = 8.5f;
            fire.minLightIntensity = 1.8f;
            fire.maxLightIntensity = 4.2f;
            fire.enableEmbers = true;
            fire.enableSmoke = true;
            fire.ApplyVisualSettings();
            EditorUtility.SetDirty(fire);
        }
        if (GUILayout.Button("🏕️ Campfire / Bonfire"))
        {
            Undo.RecordObject(fire, "Set Campfire Preset");
            fire.fireScale = 2.2f;
            fire.fireIntensity = 1.5f;
            fire.lightRange = 16f;
            fire.minLightIntensity = 3.5f;
            fire.maxLightIntensity = 7.5f;
            fire.enableEmbers = true;
            fire.enableSmoke = true;
            fire.ApplyVisualSettings();
            EditorUtility.SetDirty(fire);
        }
        if (GUILayout.Button("🕯️ Small Flame"))
        {
            Undo.RecordObject(fire, "Set Small Flame Preset");
            fire.fireScale = 0.4f;
            fire.fireIntensity = 0.8f;
            fire.lightRange = 4.5f;
            fire.minLightIntensity = 0.8f;
            fire.maxLightIntensity = 2.0f;
            fire.enableEmbers = false;
            fire.enableSmoke = false;
            fire.ApplyVisualSettings();
            EditorUtility.SetDirty(fire);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);

        // Standard Inspector properties
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("🎮 Live Controls", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(1f, 0.55f, 0.15f);
        if (GUILayout.Button("🔥 Ignite Fire", GUILayout.Height(30)))
        {
            fire.Ignite();
        }

        GUI.backgroundColor = new Color(0.7f, 0.7f, 0.7f);
        if (GUILayout.Button("🛑 Extinguish", GUILayout.Height(30)))
        {
            fire.Extinguish();
        }
        EditorGUILayout.EndHorizontal();

        GUI.backgroundColor = new Color(0.3f, 0.85f, 1f);
        if (GUILayout.Button("⚡ Rebuild VFX Rig", GUILayout.Height(26)))
        {
            Undo.RecordObject(fire, "Rebuild Fire VFX");
            fire.EnsureVFXRig();
            fire.ApplyVisualSettings();
            EditorUtility.SetDirty(fire);
        }
        GUI.backgroundColor = Color.white;
    }
}
#endif
