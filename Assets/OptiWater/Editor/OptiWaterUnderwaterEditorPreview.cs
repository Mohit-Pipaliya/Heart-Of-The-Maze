#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System;
using Smartomano.OptiWater;
using Smartomano.OptiWater.Underwater;

namespace Smartomano.OptiWater.Editor
{
    [InitializeOnLoad]
    public static class OptiWaterUnderwaterEditorPreview
    {
        private const string FeatureName = "OptiWaterUnderwaterFeature (editor-preview)";
        private static readonly int UnderwaterWaterLevelID = Shader.PropertyToID("_UnderwaterWaterLevel");

        private static int s_InjectLogCount = 0;
        private static bool s_LoggedNoAsset = false;

        static OptiWaterUnderwaterEditorPreview()
        {
            EditorApplication.update += OnUpdate;
        }

        private static void OnUpdate()
        {
            if (Application.isPlaying)
                return;

            EnsureFeatureInjected();
            EnsureWaterLevel();
        }

        private static void EnsureFeatureInjected()
        {
            try
            {
                var asset = UniversalRenderPipeline.asset as UniversalRenderPipelineAsset;
                if (asset == null)
                {
                    if (!s_LoggedNoAsset)
                    {
                        Debug.LogWarning("[OptiWaterUnderwaterEditorPreview] UniversalRenderPipeline.asset is null, cannot inject (check GraphicsSettings URP).");
                        s_LoggedNoAsset = true;
                    }
                    return;
                }
                s_LoggedNoAsset = false;

                bool changed = false;
                var dataList = asset.rendererDataList;
                bool verbose = s_InjectLogCount < 5;
                if (verbose)
                    Debug.Log($"[OptiWaterUnderwaterEditorPreview] inject attempt #{s_InjectLogCount}: rendererDataList.Length={dataList.Length}");

                for (int i = 0; i < dataList.Length; i++)
                {
                    var data = dataList[i];
                    if (data == null)
                        continue;

                    for (int j = data.rendererFeatures.Count - 1; j >= 0; j--)
                    {
                        if (data.rendererFeatures[j] == null)
                        {
                            data.rendererFeatures.RemoveAt(j);
                            EditorUtility.SetDirty(data);
                            changed = true;
                        }
                    }

                    bool already = data.TryGetRendererFeature<OptiWaterUnderwaterFeature>(out _);
                    if (verbose)
                        Debug.Log($"[OptiWaterUnderwaterEditorPreview]   data[{i}] name={data.name} hasUnderwater={already} featureCount={data.rendererFeatures.Count}");

                    if (already)
                        continue;

                    var feature = ScriptableObject.CreateInstance<OptiWaterUnderwaterFeature>();
                    feature.name = FeatureName;
                    feature.Create();

                    AssetDatabase.AddObjectToAsset(feature, data);
                    feature.hideFlags = HideFlags.HideInHierarchy;

                    data.rendererFeatures.Add(feature);
                    EditorUtility.SetDirty(data);
                    changed = true;
                    if (verbose)
                        Debug.Log($"[OptiWaterUnderwaterEditorPreview]   -> injected OptiWaterUnderwaterFeature into data[{i}] and AddObjectToAsset+SaveAssets.");
                }

                if (changed)
                {
                    AssetDatabase.SaveAssets();
                    string urpPath = AssetDatabase.GetAssetPath(asset);
                    if (!string.IsNullOrEmpty(urpPath))
                    {
                        AssetDatabase.ImportAsset(urpPath, ImportAssetOptions.ForceUpdate);
                        if (verbose)
                            Debug.Log($"[OptiWaterUnderwaterEditorPreview] forced reimport of URP asset: {urpPath}");
                    }
                    Debug.Log("[OptiWaterUnderwaterEditorPreview] OptiWaterUnderwaterFeature injected into current URP Renderer, editor preview enabled.");
                }
                s_InjectLogCount++;
            }
            catch (System.Exception e)
            {
                Debug.LogError("[OptiWaterUnderwaterEditorPreview] inject OptiWaterUnderwaterFeature failed: " + e);
            }
        }

        private static void EnsureWaterLevel()
        {
            var awc = UnityEngine.Object.FindFirstObjectByType<OptiWaterController>();
            if (awc != null)
                return;

            Shader.SetGlobalFloat(UnderwaterWaterLevelID, 0f);
        }

        [MenuItem("Tools/OptiWater/Remove Editor Preview")]
        private static void RemoveInjection()
        {
            var asset = UniversalRenderPipeline.asset as UniversalRenderPipelineAsset;
            if (asset == null)
                return;

            bool changed = false;
            var dataList = asset.rendererDataList;
            for (int i = 0; i < dataList.Length; i++)
            {
                var data = dataList[i];
                if (data == null)
                    continue;
                for (int j = data.rendererFeatures.Count - 1; j >= 0; j--)
                {
                    if (data.rendererFeatures[j] is OptiWaterUnderwaterFeature f)
                    {
                        data.rendererFeatures.RemoveAt(j);
                        UnityEngine.Object.DestroyImmediate(f, true);
                        EditorUtility.SetDirty(data);
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                AssetDatabase.SaveAssets();
                string urpPath = AssetDatabase.GetAssetPath(asset);
                if (!string.IsNullOrEmpty(urpPath))
                    AssetDatabase.ImportAsset(urpPath, ImportAssetOptions.ForceUpdate);
                Debug.Log("[OptiWaterUnderwaterEditorPreview] removed editor-injected OptiWaterUnderwaterFeature.");
            }
            else
            {
                Debug.Log("[OptiWaterUnderwaterEditorPreview] current Renderer has no OptiWaterUnderwaterFeature, nothing to remove.");
            }
        }

        [MenuItem("Tools/OptiWater/Diagnose Editor Injection")]
        private static void Diagnose()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[OptiWater Diagnose]");
            sb.AppendLine($"  Application.isPlaying = {Application.isPlaying}");

            var asset = UniversalRenderPipeline.asset as UniversalRenderPipelineAsset;
            sb.AppendLine($"  UniversalRenderPipeline.asset null? {asset == null}");
            if (asset == null) { Debug.Log(sb.ToString()); return; }

            var dataList = asset.rendererDataList;
            sb.AppendLine($"  rendererDataList.Length = {dataList.Length}");
            for (int i = 0; i < dataList.Length; i++)
            {
                var data = dataList[i];
                if (data == null) { sb.AppendLine($"  data[{i}] = null"); continue; }
                bool has = data.TryGetRendererFeature<OptiWaterUnderwaterFeature>(out _);
                sb.AppendLine($"  data[{i}] name={data.name} hasUnderwater(data)={has} featureCount={data.rendererFeatures.Count}");
            }

            var pipeline = RenderPipelineManager.currentPipeline as UniversalRenderPipeline;
            sb.AppendLine($"  RenderPipelineManager.currentPipeline is URP? {pipeline != null}");
            if (pipeline != null)
            {
                var fi = typeof(UniversalRenderPipeline).GetField("m_Renderers",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var renderers = fi?.GetValue(pipeline) as ScriptableRenderer[];
                if (renderers != null)
                {
                    for (int i = 0; i < renderers.Length; i++)
                    {
                        var r = renderers[i];
                        if (r == null) { sb.AppendLine($"  live renderer[{i}] = null"); continue; }
                        var ff = typeof(ScriptableRenderer).GetField("m_RendererFeatures",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var list = ff?.GetValue(r) as System.Collections.Generic.List<ScriptableRendererFeature>;
                        int underwaterCount = 0;
                        if (list != null)
                            foreach (var f in list)
                                if (f is OptiWaterUnderwaterFeature) underwaterCount++;
                        sb.AppendLine($"  live renderer[{i}] hasUnderwater(instance)={underwaterCount} featureCount={(list?.Count ?? -1)}");
                    }
                }
                else
                {
                    sb.AppendLine("  cannot reflect m_Renderers");
                }
            }

            Debug.Log(sb.ToString());
        }

        [MenuItem("Tools/OptiWater/Force Inject (Diagnose)")]
        private static void ForceInjectAndDiagnose()
        {
            Debug.Log("[OptiWaterUnderwaterEditorPreview] forcing inject...");
            s_InjectLogCount = 0;
            s_LoggedNoAsset = false;
            EnsureFeatureInjected();
            Diagnose();
        }
    }
}
#endif
