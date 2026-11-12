using UnityEditor;
using UnityEngine;

namespace CelestialSystem.Editor
{
    /// <summary>
    /// Custom Inspector for CelestialVolume.
    /// Provides one-click buttons to:
    ///   • Auto-fit the BoxCollider to all active scene Terrains
    ///   • Auto-assign the Main Camera
    ///   • Display live volume bounds and camera tracking status
    /// </summary>
    [CustomEditor(typeof(CelestialVolume))]
    public sealed class CelestialVolumeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            CelestialVolume volume = (CelestialVolume)target;

            // One-Click Actions at the top
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("── One-Click Scene Automation ───────────────", EditorStyles.boldLabel);

            int terrainCount = Terrain.activeTerrains != null ? Terrain.activeTerrains.Length : 0;
            EditorGUILayout.LabelField($"Active Terrains in Scene: {terrainCount}", EditorStyles.miniLabel);

            EditorGUILayout.Space(2);
            GUI.backgroundColor = new Color(0.4f, 0.85f, 0.45f);
            if (GUILayout.Button("🌲 Auto-Fit Box to Scene Terrains", GUILayout.Height(32)))
            {
                Undo.RecordObject(volume.gameObject, "Fit Box to Terrains");
                if (volume.volumeCollider != null)
                    Undo.RecordObject(volume.volumeCollider, "Fit Box to Terrains");

                bool success = volume.FitToAllTerrains();
                if (success)
                {
                    EditorUtility.SetDirty(volume);
                    if (volume.volumeCollider != null)
                        EditorUtility.SetDirty(volume.volumeCollider);
                    SceneView.RepaintAll();
                }
            }

            GUI.backgroundColor = Color.white;
            if (volume.playerCamera == null)
            {
                EditorGUILayout.Space(2);
                GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
                if (GUILayout.Button("📷 Auto-Assign Main Camera", GUILayout.Height(24)))
                {
                    Undo.RecordObject(volume, "Assign Main Camera");
                    volume.playerCamera = Camera.main;
                    EditorUtility.SetDirty(volume);
                }
                GUI.backgroundColor = Color.white;
            }

            // Live status readout
            if (volume.volumeCollider != null)
            {
                EditorGUILayout.Space(4);
                Vector3 size = volume.volumeCollider.size;
                EditorGUILayout.LabelField($"Box Size: ({size.x:F0}m x {size.y:F0}m x {size.z:F0}m)", EditorStyles.miniLabel);
                if (Application.isPlaying)
                {
                    EditorGUILayout.LabelField(
                        $"Status: {(volume.IsInside ? "INSIDE" : "OUTSIDE")} (Vis: {volume.Visibility:F2})",
                        EditorStyles.miniBoldLabel
                    );
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(8);

            // Default inspector fields
            DrawDefaultInspector();
        }
    }
}

