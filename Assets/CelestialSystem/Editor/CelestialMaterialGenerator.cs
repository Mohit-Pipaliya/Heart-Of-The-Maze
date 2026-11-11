using System.IO;
using UnityEditor;
using UnityEngine;

namespace CelestialSystem.Editor
{
    /// <summary>
    /// Utility to generate and ensure default materials for the CelestialSystem.
    /// Runs on load or via menu item: Window > Celestial System > Generate Default Materials.
    /// </summary>
    public static class CelestialMaterialGenerator
    {
        private const string MaterialsPath = "Assets/CelestialSystem/Materials";

        [MenuItem("Window/Celestial System/Generate Default Materials", priority = 100)]
        public static void GenerateDefaultMaterials()
        {
            if (!AssetDatabase.IsValidFolder(MaterialsPath))
            {
                Directory.CreateDirectory(MaterialsPath);
                AssetDatabase.Refresh();
            }

            EnsureMaterial("M_SunDisk.mat", "CelestialSystem/SunDisk");
            EnsureMaterial("M_SkyGradient.mat", "CelestialSystem/SkyGradient");
            EnsureMaterial("M_GodRays.mat", "CelestialSystem/GodRays");
            EnsureMaterial("M_StencilWrite.mat", "CelestialSystem/StencilWrite");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CelestialSystem] Default materials generated/verified in " + MaterialsPath);
        }

        private static void EnsureMaterial(string fileName, string shaderName)
        {
            string fullPath = Path.Combine(MaterialsPath, fileName);
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[CelestialSystem] Could not find shader: {shaderName}");
                return;
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(fullPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, fullPath);
                Debug.Log($"[CelestialSystem] Created material: {fullPath}");
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
                EditorUtility.SetDirty(mat);
            }
        }
    }
}
