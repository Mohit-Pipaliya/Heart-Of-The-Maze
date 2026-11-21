using UnityEngine;
using UnityEditor;
using System.IO;
using Smartomano.OptiWater;

namespace Smartomano.OptiWater.Editor
{
    public static class OptiWaterConfigurator
    {
        private const string WATER_FOLDER = "Assets/OptiWater";
        private const string PREFAB_PATH = WATER_FOLDER + "/OptiWaterSurface.prefab";
        private const string SHADER_NAME = "OptiWater/Water Surface";

        [MenuItem("GameObject/Water/Create OptiWater Surface", false, 10)]
        private static void CreateOptiWaterSurfaceMenuItem(MenuCommand menuCommand)
        {
            CreateOptiWaterSurface(menuCommand?.context as GameObject);
        }

        [MenuItem("Assets/Create/Water/OptiWater Surface Prefab", false, 10)]
        private static void CreateOptiWaterSurfacePrefabFromMenu()
        {
            CreateOptiWaterSurfacePrefab();
        }

        public static OptiWaterController CreateOptiWaterSurface(GameObject parent = null)
        {
            GameObject waterGO = GameObject.CreatePrimitive(PrimitiveType.Plane);
            waterGO.name = "OptiWaterSurface";
            waterGO.transform.localScale = Vector3.one * 10f;

            Object.DestroyImmediate(waterGO.GetComponent<MeshCollider>());

            if (parent != null)
                waterGO.transform.SetParent(parent.transform);

            OptiWaterController controller = waterGO.AddComponent<OptiWaterController>();
            controller.RebuildWaterMesh();

            int waterLayer = LayerMask.NameToLayer("Water");
            if (waterLayer >= 0)
                waterGO.layer = waterLayer;

            Undo.RegisterCreatedObjectUndo(waterGO, "Create OptiWater Surface");
            Selection.activeGameObject = waterGO;

            Debug.Log("[OptiWaterConfigurator] OptiWaterSurface created successfully.", waterGO);
            return controller;
        }

        public static GameObject CreateOptiWaterSurfacePrefab()
        {
            if (!AssetDatabase.IsValidFolder(WATER_FOLDER))
            {
                string parent = Path.GetDirectoryName(WATER_FOLDER);
                string folderName = Path.GetFileName(WATER_FOLDER);
                AssetDatabase.CreateFolder(parent, folderName);
            }

            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            if (existingPrefab != null)
            {
                if (!EditorUtility.DisplayDialog("Overwrite Confirm",
                    $"OptiWaterSurface.prefab already exists, overwrite?", "Overwrite", "Cancel"))
                {
                    Debug.Log("[OptiWaterConfigurator] Prefab creation cancelled by user.");
                    return existingPrefab;
                }
            }

            GameObject tempGO = GameObject.CreatePrimitive(PrimitiveType.Plane);
            tempGO.name = "OptiWaterSurface";
            tempGO.transform.localScale = Vector3.one * 10f;
            Object.DestroyImmediate(tempGO.GetComponent<MeshCollider>());

            OptiWaterController controller = tempGO.AddComponent<OptiWaterController>();
            controller.RebuildWaterMesh();

            string matPath = WATER_FOLDER + "/OptiWaterSurface.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat != null)
            {
                var mr = tempGO.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = mat;
            }
            else
            {
                Debug.LogWarning("[OptiWaterConfigurator] OptiWaterSurface.mat not found, controller will create material automatically.");
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(tempGO, PREFAB_PATH);
            Object.DestroyImmediate(tempGO);

            if (prefab != null)
            {
                Debug.Log($"[OptiWaterConfigurator] Prefab created at: {PREFAB_PATH}", prefab);
                EditorGUIUtility.PingObject(prefab);
            }
            else
            {
                Debug.LogError("[OptiWaterConfigurator] Failed to create prefab!");
            }

            return prefab;
        }
    }
}
