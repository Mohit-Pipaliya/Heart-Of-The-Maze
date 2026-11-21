#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

namespace Smartomano.OptiWater.Editor
{
    [InitializeOnLoad]
    public static class OptiWaterAssetSetup
    {
        private const string RuntimeFolder = "Assets/OptiWater/Runtime";
        private const string ResourcesFolder = RuntimeFolder + "/Resources";
        private const string UnderwaterMatPath = ResourcesFolder + "/OptiWaterUnderwater.mat";
        private const string SurfaceMatPath = RuntimeFolder + "/OptiWaterSurface.mat";

        static OptiWaterAssetSetup()
        {
            EnsureUnderwaterMaterial();
            EnsureSurfaceMaterial();
        }

        private static void EnsureUnderwaterMaterial()
        {
            if (File.Exists(UnderwaterMatPath)) return;

            Shader s = Shader.Find("Hidden/OptiWaterUnderwater");
            if (s == null)
            {
                Debug.LogWarning("[OptiWaterAssetSetup] shader Hidden/OptiWaterUnderwater not found yet, skipping underwater material creation.");
                return;
            }
            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            {
                string parent = Path.GetDirectoryName(ResourcesFolder);
                string folderName = Path.GetFileName(ResourcesFolder);
                AssetDatabase.CreateFolder(parent, folderName);
            }
            var mat = new Material(s) { name = "OptiWaterUnderwater" };
            AssetDatabase.CreateAsset(mat, UnderwaterMatPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[OptiWaterAssetSetup] created " + UnderwaterMatPath);
        }

        private static void EnsureSurfaceMaterial()
        {
            if (File.Exists(SurfaceMatPath)) return;

            Shader s = Shader.Find("OptiWater/Water Surface");
            if (s == null)
            {
                Debug.LogWarning("[OptiWaterAssetSetup] shader OptiWater/Water Surface not found yet, skipping surface material creation.");
                return;
            }
            var mat = new Material(s) { name = "OptiWaterSurface" };
            AssetDatabase.CreateAsset(mat, SurfaceMatPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[OptiWaterAssetSetup] created " + SurfaceMatPath);
        }
    }
}
#endif
