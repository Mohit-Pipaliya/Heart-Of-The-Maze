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
            EnsureMaterial("M_Moon.mat", "CelestialSystem/Moon", AssignMoonTextures);
            EnsureMaterial("M_StarDome.mat", "CelestialSystem/StarDome", AssignStarDomeTextures);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CelestialSystem] Default materials generated/verified in " + MaterialsPath);
        }

        private static void EnsureMaterial(string fileName, string shaderName, System.Action<Material> configure = null)
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
                configure?.Invoke(mat);
                AssetDatabase.CreateAsset(mat, fullPath);
                Debug.Log($"[CelestialSystem] Created material: {fullPath}");
            }
            else
            {
                if (mat.shader != shader)
                    mat.shader = shader;
                configure?.Invoke(mat);
                EditorUtility.SetDirty(mat);
            }
        }

        private static void AssignMoonTextures(Material mat)
        {
            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/CelestialSystem/Textures_Placeholders/Moon_Albedo.png");
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/CelestialSystem/Textures_Placeholders/Moon_Normal.png");
            if (albedo != null) mat.SetTexture("_BaseMap", albedo);
            if (normal != null) mat.SetTexture("_BumpMap", normal);
        }

        private static void AssignStarDomeTextures(Material mat)
        {
            Texture2D mw = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/CelestialSystem/Textures_Placeholders/MilkyWay_Panorama.png");
            if (mw != null) mat.SetTexture("_MilkyWayTex", mw);
        }
    }
}

