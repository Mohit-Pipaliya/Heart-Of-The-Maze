using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace CelestialSystem
{
    /// <summary>
    /// URP ScriptableRendererFeature providing radial-blur volumetric sun shafts (God Rays).
    /// Compatible with URP 17+ / Unity 6 RenderGraph (RecordRenderGraph API)
    /// with backward-compatible fallback for classic Execute mode.
    ///
    /// Driven per-frame by SunController. Zero allocation in render graph loop.
    /// Fully disabled when sun is below horizon or visibility is 0.
    /// </summary>
    public sealed class GodRaysRendererFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public sealed class GodRaysSettings
        {
            [Tooltip("When in the frame pipeline the god rays are calculated and blended.")]
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

            [Tooltip("Custom shader for radial blur and additive composite.")]
            public Shader godRaysShader;

            [Header("Ray Quality")]
            [Tooltip("Number of radial blur steps (8-32). Higher = smoother rays.")]
            [Range(8, 32)] public int sampleCount = 18;

            [Tooltip("Ray density / length multiplier.")]
            [Range(0.2f, 2.5f)] public float density = 1.0f;

            [Tooltip("Exponential decay per sample along the ray.")]
            [Range(0.80f, 0.99f)] public float decay = 0.94f;

            [Tooltip("Weight contribution per sample.")]
            [Range(0.1f, 1.0f)] public float weight = 0.5f;

            [Tooltip("Overall god ray exposure/intensity multiplier.")]
            [Range(0.0f, 4.0f)] public float intensity = 1.2f;

            [Header("Downsampling")]
            [Tooltip("Downsample ray texture (2 = half res, 4 = quarter res).")]
            [Range(1, 4)] public int downsample = 2;
        }

        [SerializeField] private GodRaysSettings settings = new GodRaysSettings();

        // Material instance
        private Material _godRaysMaterial;
        private GodRaysPass _pass;

        // Static data bridge for zero-alloc feed from SunController
        public static Vector4 SharedSunScreenPos; // xy = uv, z = onScreen (0..1), w = alt/vis (0..1)
        public static Color   SharedRayTint = Color.white;
        public static float   SharedGlobalVisibility = 1.0f;

        public override void Create()
        {
            if (settings.godRaysShader == null)
                settings.godRaysShader = Shader.Find("CelestialSystem/GodRays");

            if (settings.godRaysShader != null && _godRaysMaterial == null)
                _godRaysMaterial = CoreUtils.CreateEngineMaterial(settings.godRaysShader);

            _pass = new GodRaysPass(settings, _godRaysMaterial)
            {
                renderPassEvent = settings.renderPassEvent
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // Do not enqueue pass if disabled or invisible
            if (SharedGlobalVisibility <= 0.001f || SharedSunScreenPos.w <= 0.001f || SharedSunScreenPos.z <= 0.001f)
                return;

            if (_godRaysMaterial == null)
            {
                if (settings.godRaysShader != null)
                    _godRaysMaterial = CoreUtils.CreateEngineMaterial(settings.godRaysShader);
                if (_godRaysMaterial == null) return;
                _pass.UpdateMaterial(_godRaysMaterial);
            }

            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_godRaysMaterial);
            _godRaysMaterial = null;
        }

        // ─────────────────────────────────────────────────────────────
        //  Render Pass
        // ─────────────────────────────────────────────────────────────
        private sealed class GodRaysPass : ScriptableRenderPass
        {
            private readonly GodRaysSettings _settings;
            private Material _material;

            // Shader Property IDs cached
            private static readonly int SunScreenPosId = Shader.PropertyToID("_SunScreenPos");
            private static readonly int GodRayParamsId = Shader.PropertyToID("_GodRayParams");
            private static readonly int SampleCountId  = Shader.PropertyToID("_SampleCount");
            private static readonly int RayTintId      = Shader.PropertyToID("_RayTint");

            // PassData container for RenderGraph
            private sealed class PassData
            {
                public TextureHandle source;
                public Material material;
                public int passIndex;
            }

            public GodRaysPass(GodRaysSettings settings, Material material)
            {
                _settings = settings;
                _material = material;
                profilingSampler = new ProfilingSampler("CelestialSystem_GodRays");
            }

            public void UpdateMaterial(Material mat) => _material = mat;

            // ── RenderGraph API (Unity 6 / URP 17+) ───────────────────
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_material == null) return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData     = frameData.Get<UniversalCameraData>();

                if (!resourceData.activeColorTexture.IsValid()) return;

                // Push uniforms to material
                _material.SetVector(SunScreenPosId, SharedSunScreenPos);
                _material.SetVector(GodRayParamsId, new Vector4(
                    _settings.density,
                    _settings.weight,
                    _settings.decay,
                    _settings.intensity * SharedGlobalVisibility
                ));
                _material.SetInteger(SampleCountId, _settings.sampleCount);
                _material.SetColor(RayTintId, SharedRayTint);

                // Create downsampled temporary texture for rays
                TextureDesc sourceDesc = resourceData.activeColorTexture.GetDescriptor(renderGraph);
                int divisor = Mathf.Max(1, _settings.downsample);
                sourceDesc.width  = Mathf.Max(1, sourceDesc.width / divisor);
                sourceDesc.height = Mathf.Max(1, sourceDesc.height / divisor);
                sourceDesc.name   = "_GodRaysBuffer";
                sourceDesc.clearBuffer = true;
                sourceDesc.clearColor  = Color.black;

                TextureHandle rayTexture = renderGraph.CreateTexture(sourceDesc);

                // Pass 0: Radial Blur from sun center into rayTexture
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("GodRays_Blur", out var passData, profilingSampler))
                {
                    passData.source = resourceData.activeColorTexture;
                    passData.material = _material;
                    passData.passIndex = 0;

                    builder.UseTexture(passData.source, AccessFlags.Read);
                    builder.SetRenderAttachment(rayTexture, 0, AccessFlags.Write);

                    builder.SetRenderFunc((PassData data, RasterGraphContext rgContext) =>
                    {
                        Blitter.BlitTexture(rgContext.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, data.passIndex);
                    });
                }

                // Pass 1: Additive Composite rayTexture back onto activeColorTexture
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("GodRays_Composite", out var passData, profilingSampler))
                {
                    passData.source = rayTexture;
                    passData.material = _material;
                    passData.passIndex = 1;

                    builder.UseTexture(passData.source, AccessFlags.Read);
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);

                    builder.SetRenderFunc((PassData data, RasterGraphContext rgContext) =>
                    {
                        Blitter.BlitTexture(rgContext.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, data.passIndex);
                    });
                }
            }

            // ── Classic Execute Fallback (Compatibility mode) ─────────
            [Obsolete]
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                // In Unity 6 / URP 17+, RenderGraph is active by default.
                // This fallback is provided if the user explicitly disables RenderGraph in ProjectSettings.
                if (_material == null) return;

                CommandBuffer cmd = CommandBufferPool.Get("CelestialSystem_GodRays_Fallback");
                _material.SetVector(SunScreenPosId, SharedSunScreenPos);
                _material.SetVector(GodRayParamsId, new Vector4(_settings.density, _settings.weight, _settings.decay, _settings.intensity));
                _material.SetInteger(SampleCountId, _settings.sampleCount);
                _material.SetColor(RayTintId, SharedRayTint);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }
    }
}
