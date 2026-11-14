using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class DayNightAutoCleanInitializer
{
    static DayNightAutoCleanInitializer()
    {
        EditorApplication.delayCall += () =>
        {
            var controller = Object.FindFirstObjectByType<DayNightController>();
            if (controller != null)
            {
                controller.CleanUpLegacyArtifacts();
                controller.EnsureCloudsAndStars();
                controller.UpdateCelestialState();
                SceneView.RepaintAll();
            }
        };
    }
}

[CustomEditor(typeof(DayNightController))]
public class DayNightControllerEditor : Editor
{
    [MenuItem("Tools/Day & Night System/Create or Setup Controller in Scene", false, 1)]
    public static void CreateOrSetupInScene()
    {
        var existing = Object.FindFirstObjectByType<DayNightController>();
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject("DayNight Controller");
            Undo.RegisterCreatedObjectUndo(go, "Create DayNight Controller");
            existing = go.AddComponent<DayNightController>();
        }

        existing.CleanUpLegacyArtifacts();
        existing.AutoFindSceneReferences();
        existing.EnsureCloudsAndStars();
        existing.UpdateCelestialState();

        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
        Debug.Log("[DayNightController] Day & Night Controller created and configured in current scene!");
    }

    [MenuItem("Tools/Day & Night System/🧹 Purge Legacy Cloud & Ground Particles", false, 2)]
    public static void PurgeLegacyParticlesMenu()
    {
        var controller = Object.FindFirstObjectByType<DayNightController>();
        if (controller != null)
        {
            controller.CleanUpLegacyArtifacts();
            controller.EnsureCloudsAndStars();
            controller.UpdateCelestialState();
            SceneView.RepaintAll();
            Debug.Log("[DayNightController] All legacy particle clouds and ground particles eradicated!");
        }
        else
        {
            // Destroy any stray scene particle clouds
            var allPs = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
            int count = 0;
            foreach (var ps in allPs)
            {
                if (ps == null) continue;
                string n = ps.gameObject.name;
                if (n.Contains("Cumulus") || n.Contains("Cirrus") || n == "Clouds_Layer")
                {
                    DayNightController.SafeDestroy(ps.gameObject);
                    count++;
                }
            }
            Debug.Log($"[DayNightController] Purged {count} stray legacy cloud particle systems from scene.");
        }
    }

    public override void OnInspectorGUI()
    {
        var controller = (DayNightController)target;

        // Custom Title Header
        EditorGUILayout.Space(8);
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 15,
            alignment = TextAnchor.MiddleCenter
        };
        GUIStyle subTitleStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter
        };

        EditorGUILayout.LabelField("☀️ REALISTIC DAY & NIGHT CONTROLLER 🌙", titleStyle);
        EditorGUILayout.LabelField("Sun (East → West) • Moon (East → West) • Warm Sunrise/Sunset • Stars & Clouds", subTitleStyle);
        EditorGUILayout.Space(6);

        // Status Card
        Color bg = controller.IsDay ? new Color(0.95f, 0.65f, 0.25f, 0.25f) : new Color(0.2f, 0.4f, 0.8f, 0.25f);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUI.backgroundColor = bg;

        string phaseText = controller.IsDay ? "☀️ DAYTIME" : "🌙 NIGHTTIME";
        string progressText = $"Progress: {(controller.TimeNormalized * 100f):F0}%";
        EditorGUILayout.LabelField($"{phaseText}  |  {progressText}", EditorStyles.boldLabel);

        // Reference checks
        bool hasEast = controller.EastPoint != null;
        bool hasWest = controller.WestPoint != null;

        EditorGUILayout.LabelField($"East Reference Point: {(hasEast ? "✓ " + controller.EastPoint.name : "⚠ MISSING (Click Auto-Setup below)")}");
        EditorGUILayout.LabelField($"West Reference Point: {(hasWest ? "✓ " + controller.WestPoint.name : "⚠ MISSING (Click Auto-Setup below)")}");

        EditorGUILayout.EndVertical();
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(6);

        // Quick Jump & Test Controls
        EditorGUILayout.LabelField("Quick Jump Time Controls", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🌅 Sunrise", GUILayout.Height(28)))
        {
            Undo.RecordObject(controller, "Jump To Sunrise");
            controller.JumpToSunrise();
            EditorUtility.SetDirty(controller);
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("☀️ High Noon", GUILayout.Height(28)))
        {
            Undo.RecordObject(controller, "Jump To Noon");
            controller.JumpToNoon();
            EditorUtility.SetDirty(controller);
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("🌇 Sunset", GUILayout.Height(28)))
        {
            Undo.RecordObject(controller, "Jump To Sunset");
            controller.JumpToSunset();
            EditorUtility.SetDirty(controller);
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("🌙 Midnight", GUILayout.Height(28)))
        {
            Undo.RecordObject(controller, "Jump To Midnight");
            controller.JumpToMidnight();
            EditorUtility.SetDirty(controller);
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);

        // Purge & Setup Buttons
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🧹 Purge Ground Particles & Clouds", GUILayout.Height(30)))
        {
            Undo.RecordObject(controller, "Purge Legacy Artifacts");
            controller.CleanUpLegacyArtifacts();
            controller.EnsureCloudsAndStars();
            controller.UpdateCelestialState();
            EditorUtility.SetDirty(controller);
            SceneView.RepaintAll();
            Debug.Log("[DayNightController] Legacy particle systems purged!");
        }
        if (GUILayout.Button("🔍 Auto-Setup Visuals & Points", GUILayout.Height(30)))
        {
            Undo.RecordObject(controller, "Auto Setup DayNight");
            controller.CleanUpLegacyArtifacts();
            controller.AutoFindSceneReferences();
            controller.EnsureCloudsAndStars();
            controller.UpdateCelestialState();
            EditorUtility.SetDirty(controller);
            SceneView.RepaintAll();
            Debug.Log("[DayNightController] Auto-setup complete! Visuals, East/West points, and Lighting verified.");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // Default Inspector fields
        DrawDefaultInspector();
    }
}

