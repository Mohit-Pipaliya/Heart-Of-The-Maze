using UnityEditor;
using UnityEngine;

namespace CelestialSystem.Editor
{
    /// <summary>
    /// Master One-Click Scene Setup Tool.
    /// Provides menu items to:
    ///   • Automatically instantiate or locate CelestialSystem in the scene
    ///   • Automatically detect all Terrains and fit the BoxCollider to cover them
    ///   • Automatically wire the Main Camera
    /// </summary>
    public static class CelestialSceneSetupTool
    {
        private const string PrefabPath = "Assets/CelestialSystem/Prefabs/CelestialSystem.prefab";

        [MenuItem("Window/Celestial System/🌟 One-Click Setup (Fit to Scene Terrains)", priority = 1)]
        [MenuItem("GameObject/3D Object/Celestial System (Auto-Fit Terrains)", priority = 10)]
        public static void OneClickSetup()
        {
            // 1. Locate or create CelestialSystem in current scene
            CelestialVolume volume = Object.FindFirstObjectByType<CelestialVolume>();
            GameObject celestialObj = null;

            if (volume == null)
            {
                // Ensure prefab exists
                if (!System.IO.File.Exists(PrefabPath))
                {
                    Debug.Log("[CelestialSystem] Prefab missing. Building prefab first...");
                    CelestialPrefabBuilder.BuildPrefab();
                }

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (prefab == null)
                {
                    EditorUtility.DisplayDialog(
                        "Celestial System Error",
                        "Could not find or build CelestialSystem.prefab. Please check console for details.",
                        "OK"
                    );
                    return;
                }

                celestialObj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(celestialObj, "One-Click Celestial Setup");
                volume = celestialObj.GetComponent<CelestialVolume>();
            }
            else
            {
                celestialObj = volume.gameObject;
                Undo.RecordObject(celestialObj, "One-Click Celestial Setup");
            }

            if (volume == null)
            {
                Debug.LogError("[CelestialSystem] Failed to find CelestialVolume component on root object.");
                return;
            }

            // 2. Auto-fit to all Terrains in the scene
            Undo.RecordObject(volume, "One-Click Fit Terrains");
            if (volume.volumeCollider != null)
                Undo.RecordObject(volume.volumeCollider, "One-Click Fit Terrains");

            bool success = volume.FitToAllTerrains(horizontalPadding: 40f, heightPadding: 300f);

            // 3. Ensure camera assigned
            if (volume.playerCamera == null)
            {
                volume.playerCamera = Camera.main;
                EditorUtility.SetDirty(volume);
            }

            // 4. Mark scene dirty & select object
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene()
            );

            Selection.activeGameObject = celestialObj;
            SceneView.lastActiveSceneView?.FrameSelected();

            int terrainCount = Terrain.activeTerrains != null ? Terrain.activeTerrains.Length : 0;
            string message = success
                ? $"Celestial System is ready!\n\nSuccessfully covered {terrainCount} Terrain(s) with BoxCollider size: {volume.volumeCollider.size}\n\nCamera: {(volume.playerCamera != null ? volume.playerCamera.name : "Camera.main")}"
                : "Celestial System was added to the scene, but no Terrains were detected. Default bounds have been applied.";

            EditorUtility.DisplayDialog("One-Click Celestial Setup", message, "Done");
        }
    }
}

