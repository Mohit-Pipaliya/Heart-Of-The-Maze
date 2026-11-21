using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
using Smartomano.OptiWater;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Smartomano.OptiWater.Underwater
{
    public class OptiWaterUnderwaterFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader  m_UnderwaterShader;
        [SerializeField] private Material m_UnderwaterMaterial;

        [SerializeField] private RenderPassEvent m_EffectInjectionPoint = RenderPassEvent.AfterRenderingTransparents;

        [SerializeField] private bool m_EnableDistortion = true;
        [SerializeField, Range(0, 9)] private int m_DebugMode = 0;

        private bool m_SafeEnabled = true;

        private OptiWaterUnderwaterPass m_EffectPass;

        public static int DebugModeOverride = -1;

        public static bool GlobalEnabled = true;

        public static void ApplyConfigAll(bool enableDistortion, int debugMode)
        {
            var asset = UniversalRenderPipeline.asset;
            if (asset == null) return;
            var dataList = asset.rendererDataList;
            for (int i = 0; i < dataList.Length; i++)
            {
                var data = dataList[i];
                if (data == null) continue;
                if (data.TryGetRendererFeature<OptiWaterUnderwaterFeature>(out var feature) && feature != null)
                {
                    feature.SetDistortion(enableDistortion);
                    feature.SetDebugMode(debugMode);
                }
            }
        }

        public void SetDistortion(bool value) => m_EnableDistortion = value;

        public void SetDebugMode(int value) => m_DebugMode = value;

        private static bool s_LoggedSceneView = false;
        private static bool s_LoggedScenePass = false;

        public override void Create()
        {
            try
            {
                if (m_UnderwaterMaterial == null)
                    m_UnderwaterMaterial = Resources.Load<Material>("OptiWaterUnderwater");
                if (m_UnderwaterMaterial == null && m_UnderwaterShader == null)
                    m_UnderwaterShader = Shader.Find("Hidden/OptiWaterUnderwater");
                if (m_UnderwaterMaterial == null && m_UnderwaterShader != null)
                    m_UnderwaterMaterial = new Material(m_UnderwaterShader) { name = "OptiWaterUnderwater (auto)" };

                m_SafeEnabled = m_UnderwaterMaterial != null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[OptiWaterUnderwaterFeature] init failed, underwater disabled: " + e);
                m_SafeEnabled = false;
            }

            m_EffectPass = new OptiWaterUnderwaterPass(name + " Effect", m_SafeEnabled);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!GlobalEnabled || !m_SafeEnabled || m_UnderwaterMaterial == null)
                return;

            if (!OptiWaterController.HasActiveWater)
                return;

            if (renderingData.cameraData.cameraType == CameraType.Preview ||
                renderingData.cameraData.cameraType == CameraType.Reflection ||
                UniversalRenderer.IsOffscreenDepthTexture(ref renderingData.cameraData))
                return;

            if (OptiWaterPlanarReflectionRenderer.IsRenderingReflection)
                return;

#if UNITY_EDITOR
            if (!s_LoggedSceneView && renderingData.cameraData.cameraType == CameraType.SceneView)
            {
                bool hasRT = renderingData.cameraData.camera.targetTexture != null;
                Debug.Log($"[OptiWaterUnderwaterFeature] SceneView camera entered AddRenderPasses: targetTexture={hasRT} -> passed.");
                s_LoggedSceneView = true;
            }
#endif

            int stencilComp = (DebugModeOverride == 8 || m_DebugMode == 8) ? 8 : 3;

            int debugMode = DebugModeOverride >= 0 ? DebugModeOverride : m_DebugMode;

            m_EffectPass.renderPassEvent = m_EffectInjectionPoint;
            m_EffectPass.Setup(m_UnderwaterMaterial, debugMode, stencilComp, m_EnableDistortion);
            renderer.EnqueuePass(m_EffectPass);
        }

        internal class OptiWaterUnderwaterPass : ScriptableRenderPass
        {
            private Material m_Material;
            private bool m_SafeEnabled;
            private int m_DebugMode;
            private int m_StencilComp;
            private bool m_EnableDistortion;
            private static MaterialPropertyBlock s_Block = new MaterialPropertyBlock();
            private static readonly int s_DebugModeID = Shader.PropertyToID("_UnderwaterDebugMode");
            private static readonly int s_StencilCompID = Shader.PropertyToID("_UWStencilComp");
            private static readonly int s_DistortOnID = Shader.PropertyToID("_UnderwaterDistortOn");
            private static readonly int s_SceneColorID = Shader.PropertyToID("_UnderwaterSceneColor");

            private class PassData
            {
                internal Material material;
                internal int debugMode;
                internal int stencilComp;
                internal bool distortOn;
                internal TextureHandle sceneColorCopy;
            }

            public OptiWaterUnderwaterPass(string passName, bool safeEnabled)
            {
                profilingSampler = new ProfilingSampler(passName);
                m_SafeEnabled = safeEnabled;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public void Setup(Material material, int debugMode, int stencilComp, bool enableDistortion)
            {
                m_Material = material;
                m_DebugMode = debugMode;
                m_StencilComp = stencilComp;
                m_EnableDistortion = enableDistortion;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                try
                {
                    if (!m_SafeEnabled || m_Material == null)
                        return;

                    UniversalResourceData resourcesData = frameData.Get<UniversalResourceData>();
                    if (!resourcesData.activeColorTexture.IsValid() || !resourcesData.activeDepthTexture.IsValid())
                        return;

#if UNITY_EDITOR
                    if (!s_LoggedScenePass)
                    {
                        var camData = frameData.Get<UniversalCameraData>();
                        if (camData.cameraType == CameraType.SceneView)
                        {
                            bool cd = resourcesData.cameraDepthTexture.IsValid();
                            bool ad = resourcesData.activeDepthTexture.IsValid();
                            Debug.Log($"[OptiWaterUnderwaterFeature] SceneView post-process executed: debugMode={m_DebugMode} stencilComp={m_StencilComp} cameraDepthTexture={cd} activeDepthTexture={ad}");
                            s_LoggedScenePass = true;
                        }
                    }
#endif

                    TextureHandle sceneColorCopy = TextureHandle.nullHandle;
                    if (m_EnableDistortion)
                    {
                        var copyDesc = renderGraph.GetTextureDesc(resourcesData.activeColorTexture);
                        copyDesc.name = "_UnderwaterSceneColor";
                        copyDesc.msaaSamples = MSAASamples.None;
                        copyDesc.depthBufferBits = DepthBits.None;
                        copyDesc.clearBuffer = false;
                        copyDesc.bindTextureMS = false;
                        sceneColorCopy = renderGraph.CreateTexture(copyDesc);
                        renderGraph.AddBlitPass(resourcesData.activeColorTexture, sceneColorCopy,
                            Vector2.one, Vector2.zero, passName: "OptiWater SceneColor Copy");
                    }

                    using (var builder = renderGraph.AddRasterRenderPass<PassData>("OptiWater Effect Pass", out var passData, profilingSampler))
                    {
                        passData.material = m_Material;
                        passData.debugMode = m_DebugMode;
                        passData.stencilComp = m_StencilComp;
                        passData.distortOn = m_EnableDistortion;
                        passData.sceneColorCopy = sceneColorCopy;

                        if (m_EnableDistortion)
                            builder.UseTexture(sceneColorCopy, AccessFlags.Read);

                        if (resourcesData.cameraDepthTexture.IsValid())
                            builder.UseTexture(resourcesData.cameraDepthTexture, AccessFlags.Read);

                        builder.SetRenderAttachment(resourcesData.activeColorTexture, 0, AccessFlags.ReadWrite);
                        builder.SetRenderAttachmentDepth(resourcesData.activeDepthTexture, AccessFlags.Read);

                        builder.SetRenderFunc(static (PassData data, RasterGraphContext rgContext) =>
                        {
                            s_Block.Clear();
                            s_Block.SetFloat(s_DebugModeID, data.debugMode);
                            s_Block.SetInt(s_StencilCompID, data.stencilComp);
                            s_Block.SetFloat(s_DistortOnID, data.distortOn ? 1f : 0f);
                            if (data.distortOn && data.sceneColorCopy.IsValid())
                                s_Block.SetTexture(s_SceneColorID, data.sceneColorCopy);
                            rgContext.cmd.DrawProcedural(Matrix4x4.identity, data.material, 0, MeshTopology.Triangles, 3, 1, s_Block);
                        });
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[OptiWaterUnderwaterFeature] OptiWaterUnderwaterPass error, skipped: " + e);
                }
            }
        }

#if UNITY_EDITOR
        private static void SetDebug(int v) { DebugModeOverride = v; Debug.Log("[OptiWaterUnderwaterFeature] DebugMode forced to " + v); }
        [MenuItem("Tools/OptiWater/Debug/0 Normal")]                    private static void Dbg0() => SetDebug(0);
        [MenuItem("Tools/OptiWater/Debug/2 Underwater Magenta")]        private static void Dbg2() => SetDebug(2);
        [MenuItem("Tools/OptiWater/Debug/5 TerrainY Heatmap")]          private static void Dbg5() => SetDebug(5);
        [MenuItem("Tools/OptiWater/Debug/8 IgnoreStencil")]             private static void Dbg8() => SetDebug(8);
        [MenuItem("Tools/OptiWater/Debug/9 NonWater PureRed")]          private static void Dbg9() => SetDebug(9);
        [MenuItem("Tools/OptiWater/Debug/-1 Use Instance Field")]       private static void DbgReset() => SetDebug(-1);
#endif
    }
}
