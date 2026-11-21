using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Smartomano.OptiWater.Underwater
{
    public static class OptiWaterUnderwaterBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            try
            {
                var asset = ResolveAsset();
                if (asset == null)
                {
                    Debug.LogWarning("[OptiWaterUnderwaterBootstrap] UniversalRenderPipelineAsset not found, skipping underwater registration.");
                    return;
                }

                var dataList = asset.rendererDataList;
                for (int i = 0; i < dataList.Length; i++)
                {
                    var data = dataList[i];
                    if (data == null)
                        continue;

                    if (data.TryGetRendererFeature<OptiWaterUnderwaterFeature>(out _))
                        continue;

                    var feature = ScriptableObject.CreateInstance<OptiWaterUnderwaterFeature>();
                    feature.name = "OptiWaterUnderwaterFeature (runtime)";
                    feature.Create();

                    data.rendererFeatures.Add(feature);
                    data.SetDirty();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[OptiWaterUnderwaterBootstrap] underwater registration failed, skipped safely: " + e);
            }
        }

        private static UniversalRenderPipelineAsset ResolveAsset()
        {
            var asset = UniversalRenderPipeline.asset;
            if (asset != null)
                return asset;
            return GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        }
    }
}
