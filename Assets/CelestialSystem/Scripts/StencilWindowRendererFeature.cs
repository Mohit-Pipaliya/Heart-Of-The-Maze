using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace CelestialSystem
{
    /// <summary>
    /// URP ScriptableRendererFeature supporting the "Stencil Window Mode" of CelestialVolume.
    /// Draws the assigned BoxCollider volume before sky rendering to write a stencil reference (default 1)
    /// into the depth-stencil buffer. Celestial objects (sky, sun, moon, stars) then only render
    /// where stencil == 1.
    ///
    /// Compatible with URP 17+ / Unity 6 RenderGraph and classic fallback.
    /// </summary>
    public sealed class StencilWindowRendererFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public sealed class StencilSettings
        {
            [Tooltip("When to write the stencil mask into the depth/stencil buffer.")]
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingSkybox;

            [Tooltip("Stencil write shader (defaults to CelestialSystem/StencilWrite).")]
            public Shader stencilWriteShader;

            [Tooltip("Stencil reference integer written to the stencil buffer.")]
            [Range(1, 255)] public int stencilRef = 1;
        }

        [SerializeField] private StencilSettings settings = new StencilSettings();

        private Material _stencilMaterial;
        private StencilPass _pass;

        // Static reference to the active CelestialVolume's box collider for zero-alloc drawing
        public static BoxCollider ActiveBoxCollider;
        public static bool StencilModeEnabled = false;

        public override void Create()
        {
            if (settings.stencilWriteShader == null)
                settings.stencilWriteShader = Shader.Find("CelestialSystem/StencilWrite");

            if (settings.stencilWriteShader != null && _stencilMaterial == null)
                _stencilMaterial = CoreUtils.CreateEngineMaterial(settings.stencilWriteShader);

            _pass = new StencilPass(settings, _stencilMaterial)
            {
                renderPassEvent = settings.renderPassEvent
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!StencilModeEnabled || ActiveBoxCollider == null)
                return;

            if (_stencilMaterial == null)
            {
                if (settings.stencilWriteShader != null)
                    _stencilMaterial = CoreUtils.CreateEngineMaterial(settings.stencilWriteShader);
                if (_stencilMaterial == null) return;
                _pass.UpdateMaterial(_stencilMaterial);
            }

            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_stencilMaterial);
            _stencilMaterial = null;
        }

        private sealed class StencilPass : ScriptableRenderPass
        {
            private readonly StencilSettings _settings;
            private Material _material;
            private static readonly int StencilRefId = Shader.PropertyToID("_StencilRef");
            private static Mesh _cubeMesh;

            private sealed class PassData
            {
                public Material material;
                public Matrix4x4 matrix;
                public Mesh mesh;
            }

            public StencilPass(StencilSettings settings, Material material)
            {
                _settings = settings;
                _material = material;
                profilingSampler = new ProfilingSampler("CelestialSystem_StencilWindow");
                EnsureCubeMesh();
            }

            public void UpdateMaterial(Material mat) => _material = mat;

            private static void EnsureCubeMesh()
            {
                if (_cubeMesh == null)
                {
                    GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    MeshFilter mf = tempCube.GetComponent<MeshFilter>();
                    _cubeMesh = mf != null ? mf.sharedMesh : null;
                    if (Application.isPlaying)
                        Destroy(tempCube);
                    else
                        DestroyImmediate(tempCube);
                }
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_material == null || ActiveBoxCollider == null) return;
                EnsureCubeMesh();
                if (_cubeMesh == null) return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (!resourceData.activeColorTexture.IsValid()) return;

                _material.SetInteger(StencilRefId, _settings.stencilRef);

                Transform t = ActiveBoxCollider.transform;
                Matrix4x4 localMatrix = Matrix4x4.TRS(
                    ActiveBoxCollider.center,
                    Quaternion.identity,
                    ActiveBoxCollider.size
                );
                Matrix4x4 worldMatrix = t.localToWorldMatrix * localMatrix;

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("StencilWindow_Write", out var passData, profilingSampler))
                {
                    passData.material = _material;
                    passData.matrix   = worldMatrix;
                    passData.mesh     = _cubeMesh;

                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
                    if (resourceData.activeDepthTexture.IsValid())
                        builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Write);

                    builder.SetRenderFunc((PassData data, RasterGraphContext rgContext) =>
                    {
                        rgContext.cmd.DrawMesh(data.mesh, data.matrix, data.material, 0, 0);
                    });
                }
            }

            [Obsolete]
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_material == null || ActiveBoxCollider == null) return;
                EnsureCubeMesh();
                if (_cubeMesh == null) return;

                _material.SetInteger(StencilRefId, _settings.stencilRef);

                Transform t = ActiveBoxCollider.transform;
                Matrix4x4 localMatrix = Matrix4x4.TRS(
                    ActiveBoxCollider.center,
                    Quaternion.identity,
                    ActiveBoxCollider.size
                );
                Matrix4x4 worldMatrix = t.localToWorldMatrix * localMatrix;

                CommandBuffer cmd = CommandBufferPool.Get("CelestialSystem_StencilWindow_Fallback");
                cmd.DrawMesh(_cubeMesh, worldMatrix, _material, 0, 0);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }
    }
}
