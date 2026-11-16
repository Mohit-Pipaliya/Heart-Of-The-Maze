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
        EditorGUILayout.LabelField("🔥 REALISTIC FIRE CONTROLLER", headerStyle);
        EditorGUILayout.HelpBox(
            "• Light & Flame Controls: Change Light Intensity, Range, Colors, Flame Size and Sparks directly from Inspector.\n" +
            "• Live Updates: Changes update in real-time in both Scene View and Game View!",
            MessageType.Info
        );

        EditorGUILayout.Space(4);

        // ── Preset Row 1: Fire Sizes ──
        EditorGUILayout.LabelField("⚡ Quick Size Presets:", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🔥 Torch / Mashal"))
        {
            Undo.RecordObject(fire, "Set Torch Preset");
            fire.fireScale = 1.0f;
            fire.flameHeight = 1.0f;
            fire.flameWidth = 1.0f;
            fire.flameSpeed = 1.0f;
            fire.fireIntensity = 1.0f;
            fire.lightIntensity = 3.5f;
            fire.lightRange = 8.5f;
            fire.enableEmbers = true;
            fire.enableSmoke = true;
            fire.ApplyVisualSettings();
            EditorUtility.SetDirty(fire);
        }
        if (GUILayout.Button("🏕️ Campfire / Bonfire"))
        {
            Undo.RecordObject(fire, "Set Campfire Preset");
            fire.fireScale = 2.2f;
            fire.flameHeight = 1.4f;
            fire.flameWidth = 1.5f;
            fire.flameSpeed = 1.3f;
            fire.fireIntensity = 1.6f;
            fire.lightIntensity = 7.5f;
            fire.lightRange = 16f;
            fire.enableEmbers = true;
            fire.enableSmoke = true;
            fire.ApplyVisualSettings();
            EditorUtility.SetDirty(fire);
        }
        if (GUILayout.Button("🕯️ Small Flame"))
        {
            Undo.RecordObject(fire, "Set Small Flame Preset");
            fire.fireScale = 0.4f;
            fire.flameHeight = 0.7f;
            fire.flameWidth = 0.6f;
            fire.flameSpeed = 0.7f;
            fire.fireIntensity = 0.8f;
            fire.lightIntensity = 1.5f;
            fire.lightRange = 4.5f;
            fire.enableEmbers = false;
            fire.enableSmoke = false;
            fire.ApplyVisualSettings();
            EditorUtility.SetDirty(fire);
        }
        EditorGUILayout.EndHorizontal();

        // ── Preset Row 2: Light Brightness ──
        EditorGUILayout.Space(3);
        EditorGUILayout.LabelField("💡 Quick Light Brightness:", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🌙 Dim (1.5)"))
        {
            Undo.RecordObject(fire, "Set Dim Light");
            fire.lightIntensity = 1.5f;
            fire.ApplyVisualSettings();
            EditorUtility.SetDirty(fire);
        }
        if (GUILayout.Button("🔥 Normal (3.5)"))
        {
            Undo.RecordObject(fire, "Set Normal Light");
            fire.lightIntensity = 3.5f;
            fire.ApplyVisualSettings();
            EditorUtility.SetDirty(fire);
        }
        if (GUILayout.Button("☀️ Bright (8.0)"))
        {
            Undo.RecordObject(fire, "Set Bright Light");
            fire.lightIntensity = 8.0f;
            fire.ApplyVisualSettings();
            EditorUtility.SetDirty(fire);
        }
        if (GUILayout.Button("💥 Ultra (16.0)"))
        {
            Undo.RecordObject(fire, "Set Ultra Bright Light");
            fire.lightIntensity = 16.0f;
            fire.ApplyVisualSettings();
            EditorUtility.SetDirty(fire);
        }
        EditorGUILayout.EndHorizontal();

        // ── Preset Row 3: Colors / Themes ──
        EditorGUILayout.Space(3);
        EditorGUILayout.LabelField("🎨 Quick Color Themes:", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🟠 Classic Orange"))
        {
            Undo.RecordObject(fire, "Set Classic Orange Theme");
            fire.flameCoreColor = new Color(1.0f, 1.0f, 0.95f, 1.0f);
            fire.flameMidColor = new Color(1.0f, 0.62f, 0.12f, 1.0f);
            fire.flameTipColor = new Color(1.0f, 0.22f, 0.02f, 1.0f);
            fire.lightBaseColor = new Color(1.0f, 0.55f, 0.15f);
            fire.lightHotColor = new Color(1.0f, 0.75f, 0.32f);
            fire.embersColor = new Color(1.0f, 0.85f, 0.35f, 1.0f);
            fire.ApplyVisualSettings();
            EditorUtility.SetDirty(fire);
        }
        if (GUILayout.Button("🔵 Blue Magic Flame"))
        {
            Undo.RecordObject(fire, "Set Blue Magic Theme");
            fire.flameCoreColor = new Color(0.85f, 0.95f, 1.0f, 1.0f);
            fire.flameMidColor = new Color(0.12f, 0.55f, 1.0f, 1.0f);
            fire.flameTipColor = new Color(0.02f, 0.12f, 0.85f, 1.0f);
            fire.lightBaseColor = new Color(0.15f, 0.45f, 1.0f);
            fire.lightHotColor = new Color(0.4f, 0.75f, 1.0f);
            fire.embersColor = new Color(0.35f, 0.75f, 1.0f, 1.0f);
            fire.ApplyVisualSettings();
            EditorUtility.SetDirty(fire);
        }
        if (GUILayout.Button("🟢 Green Toxic Flame"))
        {
            Undo.RecordObject(fire, "Set Green Toxic Theme");
            fire.flameCoreColor = new Color(0.9f, 1.0f, 0.9f, 1.0f);
            fire.flameMidColor = new Color(0.2f, 0.95f, 0.15f, 1.0f);
            fire.flameTipColor = new Color(0.04f, 0.45f, 0.05f, 1.0f);
            fire.lightBaseColor = new Color(0.15f, 0.85f, 0.2f);
            fire.lightHotColor = new Color(0.4f, 1.0f, 0.5f);
            fire.embersColor = new Color(0.45f, 1.0f, 0.35f, 1.0f);
            fire.ApplyVisualSettings();
            EditorUtility.SetDirty(fire);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);

        // Standard Inspector properties with live auto-refresh
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        if (EditorGUI.EndChangeCheck())
        {
            fire.ApplyVisualSettings();
            EditorUtility.SetDirty(fire);
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("🎮 Live Controls & Maintenance", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(1f, 0.55f, 0.15f);
        if (GUILayout.Button("🔥 Ignite Fire", GUILayout.Height(30)))
        {
            Undo.RecordObject(fire, "Ignite Fire");
            fire.Ignite();
            EditorUtility.SetDirty(fire);
        }

        GUI.backgroundColor = new Color(0.7f, 0.7f, 0.7f);
        if (GUILayout.Button("🛑 Extinguish", GUILayout.Height(30)))
        {
            Undo.RecordObject(fire, "Extinguish Fire");
            fire.Extinguish();
            EditorUtility.SetDirty(fire);
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
