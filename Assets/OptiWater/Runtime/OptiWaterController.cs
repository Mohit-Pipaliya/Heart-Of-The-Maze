using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Smartomano.OptiWater
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class OptiWaterController : MonoBehaviour
    {
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private MeshFilter meshFilter;

        [Header("Runtime Multipliers")]
        [SerializeField, Range(0f, 5f)] private float foamStrength = 1.0f;

        [Header("Water Level (design height, drives early-return clip)")]
        [SerializeField] private float waterSurfaceHeight = 95f;

        [Header("Height Auto-Sync (unify all OptiWater water to this surface's Y)")]
        [SerializeField] private Transform waterHeightReference;

        [Header("Shore Wave")]
        [SerializeField] private bool shoreWaveEnabled = true;

        [Header("Mesh")]
        [SerializeField, Range(2, 512)] private int meshSegments = 50;

        [Header("Quality Switches")]
        [SerializeField] private bool gerstnerWaveEnabled = true;
        [SerializeField] private bool normalPerturbEnabled = true;
        [SerializeField] private bool causticsEnabled = true;
        [SerializeField] private bool foamEnabled = true;
        [SerializeField] private bool crestGlowEnabled = true;
        [SerializeField] private bool planarReflectionEnabled = true;
        [SerializeField] private bool microNormalEnabled = false;
        [SerializeField] private bool shoreWaveNormalEnabled = true;
        [SerializeField] private bool bottomDistortEnabled = false;

        private static readonly int WaterColorID = Shader.PropertyToID("_WaterColor");
        private static readonly int NormalMapID = Shader.PropertyToID("_NormalMap");
        private static readonly int NormalStrengthID = Shader.PropertyToID("_NormalStrength");
        private static readonly int NormalBlendID = Shader.PropertyToID("_NormalBlend");
        private static readonly int CausticsTexID = Shader.PropertyToID("_CausticsTex");
        private static readonly int CausticsStrengthID = Shader.PropertyToID("_CausticsStrength");
        private static readonly int CausticsSpeedID = Shader.PropertyToID("_CausticsSpeed");
        private static readonly int FoamTexID = Shader.PropertyToID("_FoamTex");
        private static readonly int FoamIntensityID = Shader.PropertyToID("_FoamIntensity");
        private static readonly int FoamDepthThresholdID = Shader.PropertyToID("_FoamDepthThreshold");
        private static readonly int FoamShorelineBoostID = Shader.PropertyToID("_FoamShorelineBoost");
        private static readonly int FoamPulseSpeedID = Shader.PropertyToID("_FoamPulseSpeed");
        private static readonly int ShorelineAlphaFalloffID = Shader.PropertyToID("_ShorelineAlphaFalloff");
        private static readonly int ShorelineDepthFadeID = Shader.PropertyToID("_ShorelineDepthFade");
        private static readonly int PlanarReflectionID = Shader.PropertyToID("_PlanarReflection");
        private static readonly int ReflectionTexID = Shader.PropertyToID("_ReflectionTex");
        private static readonly int ReflectionIntensityID = Shader.PropertyToID("_ReflectionIntensity");
        private static readonly int FresnelPowerID = Shader.PropertyToID("_FresnelPower");
        private static readonly int FresnelBiasID = Shader.PropertyToID("_FresnelBias");
        private static readonly int SmoothnessID = Shader.PropertyToID("_Smoothness");
        private static readonly int SpecularIntensityID = Shader.PropertyToID("_SpecularIntensity");
        private static readonly int WaterSurfaceHeightID = Shader.PropertyToID("_WaterSurfaceHeight");
        private static readonly int WaterClipThresholdID = Shader.PropertyToID("_WaterClipThreshold");
        private static readonly int UnderwaterWaterLevelID = Shader.PropertyToID("_UnderwaterWaterLevel");
        private static readonly int UnderwaterColorID = Shader.PropertyToID("_UnderwaterColor");
        private static readonly int DeepWaterColorID = Shader.PropertyToID("_DeepWaterColor");
        private static readonly int ShoreWaveFrequencyID = Shader.PropertyToID("_ShoreWaveFrequency");
        private static readonly int ShoreWaveSpeedID = Shader.PropertyToID("_ShoreWaveSpeed");
        private static readonly int ShoreWaveMixID = Shader.PropertyToID("_ShoreWaveMix");
        private static readonly int ShoreWaveFoamStrengthID = Shader.PropertyToID("_ShoreWaveFoamStrength");
        private static readonly int ShoreWaveNormalStrengthID = Shader.PropertyToID("_ShoreWaveNormalStrength");

        private Material waterMaterial;
        private MaterialPropertyBlock propertyBlock;
        private Mesh runtimeWaterMesh;

        private OptiWaterPlanarReflectionRenderer planarRenderer;

        [Header("Debug Capture (render water to RenderTexture)")]
        [SerializeField] private bool debugCaptureEnabled = false;
        [SerializeField, Tooltip("Capture RT resolution")] private int captureWidth = 1920;
        [SerializeField] private int captureHeight = 1080;
        [SerializeField] private string captureSavePath = "WaterCapture.png";

        private RenderTexture captureRT;
        private Camera captureCamera;
        private GameObject captureCamGO;
        private Camera mainCamera;
        private Coroutine captureCoroutine;

        private static readonly HashSet<OptiWaterController> s_ActiveControllers = new HashSet<OptiWaterController>();
        public static bool HasActiveWater => s_ActiveControllers.Count > 0;

        private void OnEnable()
        {
            s_ActiveControllers.Add(this);
        }

        private void OnDisable()
        {
            s_ActiveControllers.Remove(this);
        }

        private void Awake()
        {
            InitializeWater();
        }

        private void Start()
        {
            Debug.Log("[OptiWaterController] Start: activeSelf=" + gameObject.activeSelf + " enabled=" + enabled);
            if (waterMaterial != null) Debug.Log("[OptiWaterController] material=" + waterMaterial.name + " shader=" + waterMaterial.shader?.name);
            else Debug.LogWarning("[OptiWaterController] waterMaterial is null!");
        }

        private void Update()
        {
            UnifyWaterHeight();
            Shader.SetGlobalFloat(UnderwaterWaterLevelID, waterSurfaceHeight);
            SyncFromMaterial();
            SyncCapture();
        }

        private void UnifyWaterHeight()
        {
            float targetY = (waterHeightReference != null) ? waterHeightReference.position.y : transform.position.y;
            if (Mathf.Abs(transform.position.y - targetY) > 1e-4f)
            {
                Vector3 surfacePos = transform.position;
                surfacePos.y = targetY;
                transform.position = surfacePos;
            }
            waterSurfaceHeight = targetY;
        }

        private void OnDestroy() => CleanupWater();

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying || !gameObject.scene.isLoaded) return;
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                InitializeWater();
                UnifyWaterHeight();
                SyncFromMaterial();
                UpdateShaderKeyword();
                SyncCapture();
            };
        }
#endif

        public void InitializeWater()
        {
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
            if (meshRenderer == null) { Debug.LogError("[OptiWaterController] MeshRenderer not found!", this); return; }

            if (meshRenderer.sharedMaterial != null)
            {
                waterMaterial = meshRenderer.sharedMaterial;
                Debug.Log("[OptiWaterController] using material: " + waterMaterial.name);
            }
            else
            {
                Debug.LogWarning("[OptiWaterController] material missing, creating default material");
                Shader s = Shader.Find("OptiWater/Water Surface");
                if (s == null)
                {
                    Debug.LogError("[OptiWaterController] OptiWater shader not found!");
                    s = Shader.Find("Universal Render Pipeline/Lit");
                    if (s == null)
                    {
                        Debug.LogError("[OptiWaterController] URP/Lit also not found!");
                        return;
                    }
                }
                waterMaterial = new Material(s);
                waterMaterial.name = "OptiWaterSurface";
                meshRenderer.sharedMaterial = waterMaterial;
                Debug.Log("[OptiWaterController] created material: " + waterMaterial.name);
            }

            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();

            if (meshFilter.sharedMesh == null)
            {
                Debug.Log("[OptiWaterController] sharedMesh is null, rebuilding mesh");
                RebuildWaterMesh();
            }

            UpdateShaderKeyword();

            Shader.SetGlobalFloat(UnderwaterWaterLevelID, waterSurfaceHeight);
        }

        private float ReadFloat(int id) => waterMaterial.HasProperty(id) ? waterMaterial.GetFloat(id) : 0f;
        private Color ReadColor(int id) => waterMaterial.HasProperty(id) ? waterMaterial.GetColor(id) : Color.white;
        private Texture ReadTexture(int id) => waterMaterial.HasProperty(id) ? waterMaterial.GetTexture(id) : null;

        public void SyncFromMaterial()
        {
            if (propertyBlock == null || meshRenderer == null || waterMaterial == null) return;

            meshRenderer.GetPropertyBlock(propertyBlock);

            propertyBlock.SetColor(WaterColorID, ReadColor(WaterColorID));
            var normalMap = ReadTexture(NormalMapID);
            if (normalMap != null) propertyBlock.SetTexture(NormalMapID, normalMap);

            propertyBlock.SetFloat(NormalStrengthID, ReadFloat(NormalStrengthID));
            propertyBlock.SetFloat(NormalBlendID, ReadFloat(NormalBlendID));

            var causticsTex = ReadTexture(CausticsTexID);
            if (causticsTex != null) propertyBlock.SetTexture(CausticsTexID, causticsTex);
            propertyBlock.SetFloat(CausticsStrengthID, ReadFloat(CausticsStrengthID));
            propertyBlock.SetFloat(CausticsSpeedID, ReadFloat(CausticsSpeedID));

            var foamTex = ReadTexture(FoamTexID);
            if (foamTex != null) propertyBlock.SetTexture(FoamTexID, foamTex);
            propertyBlock.SetFloat(FoamIntensityID, ReadFloat(FoamIntensityID) * foamStrength);
            propertyBlock.SetFloat(FoamDepthThresholdID, ReadFloat(FoamDepthThresholdID));

            propertyBlock.SetFloat(FoamShorelineBoostID, ReadFloat(FoamShorelineBoostID));
            propertyBlock.SetFloat(FoamPulseSpeedID, ReadFloat(FoamPulseSpeedID));
            propertyBlock.SetFloat(ShorelineAlphaFalloffID, ReadFloat(ShorelineAlphaFalloffID));
            propertyBlock.SetFloat(ShorelineDepthFadeID, ReadFloat(ShorelineDepthFadeID));

            propertyBlock.SetFloat(ReflectionIntensityID, ReadFloat(ReflectionIntensityID));
            propertyBlock.SetFloat(FresnelPowerID, ReadFloat(FresnelPowerID));
            propertyBlock.SetFloat(FresnelBiasID, ReadFloat(FresnelBiasID));
            propertyBlock.SetFloat(SmoothnessID, ReadFloat(SmoothnessID));
            propertyBlock.SetFloat(SpecularIntensityID, ReadFloat(SpecularIntensityID));

            Shader.SetGlobalColor(UnderwaterColorID, ReadColor(DeepWaterColorID));
            Shader.SetGlobalFloat(UnderwaterWaterLevelID, waterSurfaceHeight);

            propertyBlock.SetFloat(WaterSurfaceHeightID, waterSurfaceHeight);
            propertyBlock.SetFloat(WaterClipThresholdID, ReadFloat(WaterClipThresholdID));

            propertyBlock.SetFloat(ShoreWaveFrequencyID, ReadFloat(ShoreWaveFrequencyID));
            propertyBlock.SetFloat(ShoreWaveSpeedID, ReadFloat(ShoreWaveSpeedID));
            propertyBlock.SetFloat(ShoreWaveMixID, ReadFloat(ShoreWaveMixID));
            propertyBlock.SetFloat(ShoreWaveFoamStrengthID, ReadFloat(ShoreWaveFoamStrengthID));
            propertyBlock.SetFloat(ShoreWaveNormalStrengthID, ReadFloat(ShoreWaveNormalStrengthID));

            var globalRT = Shader.GetGlobalTexture(OptiWaterPlanarReflectionRenderer.OptiWaterReflectionTexName);
            if (globalRT != null)
                propertyBlock.SetTexture(ReflectionTexID, globalRT);
            else
            {
                var reflectionTex = ReadTexture(ReflectionTexID);
                if (reflectionTex != null) propertyBlock.SetTexture(ReflectionTexID, reflectionTex);
            }

            meshRenderer.SetPropertyBlock(propertyBlock);
        }

        [ContextMenu("RebuildWaterMesh")]
        public void RebuildWaterMesh()
        {
            if (runtimeWaterMesh != null)
            {
                if (Application.isPlaying) Destroy(runtimeWaterMesh);
                else DestroyImmediate(runtimeWaterMesh);
                runtimeWaterMesh = null;
            }
            Mesh mesh = GenerateWaterMesh(meshSegments, meshSegments);
            mesh.name = "OptiWaterMesh_" + meshSegments + "x" + meshSegments;
            runtimeWaterMesh = mesh;
            if (meshFilter != null) meshFilter.sharedMesh = mesh;
        }

        public void CleanupWater()
        {
            TeardownCapture();
            if (meshRenderer != null)
                meshRenderer.SetPropertyBlock(null);
            propertyBlock = null;
            if (runtimeWaterMesh != null)
            {
                if (Application.isPlaying) Destroy(runtimeWaterMesh);
                else DestroyImmediate(runtimeWaterMesh);
                runtimeWaterMesh = null;
            }
        }

        private void SyncCapture()
        {
            if (debugCaptureEnabled) EnsureCaptureSetup();
            else if (captureRT != null) TeardownCapture();
        }

        private void EnsureCaptureSetup()
        {
            if (captureRT != null) return;

            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                var cams = Object.FindObjectsOfType<Camera>();
                foreach (var c in cams)
                {
                    if (c != captureCamera && c.enabled) { mainCamera = c; break; }
                }
            }
            if (mainCamera == null)
            {
                Debug.LogWarning("[OptiWaterController] no main camera found, cannot start Debug Capture");
                return;
            }

            captureRT = new RenderTexture(captureWidth, captureHeight, 24, RenderTextureFormat.ARGB32);
            captureRT.name = "OptiWaterCaptureRT";
            if (!captureRT.Create())
                Debug.LogWarning("[OptiWaterController] CaptureRT creation failed");

            captureCamGO = new GameObject("OptiWaterCaptureCam");
            captureCamGO.hideFlags = HideFlags.HideAndDontSave;
            captureCamera = captureCamGO.AddComponent<Camera>();
            captureCamera.CopyFrom(mainCamera);
            captureCamera.targetTexture = captureRT;
            captureCamera.enabled = false;
            captureCamera.aspect = (float)captureWidth / captureHeight;
            captureCamera.transform.SetParent(mainCamera.transform, false);
            captureCamera.transform.localPosition = Vector3.zero;
            captureCamera.transform.localRotation = Quaternion.identity;

            Debug.Log("[OptiWaterController] Debug Capture enabled, RT=" + captureRT.name +
                      " (" + captureWidth + "x" + captureHeight + ")");

            if (captureCoroutine == null)
                captureCoroutine = StartCoroutine(CaptureLoop());
        }

        private IEnumerator CaptureLoop()
        {
            var yld = new WaitForEndOfFrame();
            while (debugCaptureEnabled && captureCamera != null && captureRT != null && mainCamera != null)
            {
                yield return yld;
                if (captureCamera == null || captureRT == null || mainCamera == null) break;
                captureCamera.Render();
            }
            captureCoroutine = null;
        }

        private void TeardownCapture()
        {
            if (captureCoroutine != null) { StopCoroutine(captureCoroutine); captureCoroutine = null; }
            if (captureCamGO != null)
            {
                if (Application.isPlaying) Destroy(captureCamGO);
                else DestroyImmediate(captureCamGO);
            }
            captureCamera = null;
            captureCamGO = null;
            if (captureRT != null)
            {
                captureRT.Release();
                if (Application.isPlaying) Destroy(captureRT);
                else DestroyImmediate(captureRT);
            }
            captureRT = null;
            mainCamera = null;
        }

        [ContextMenu("Render Capture Once")]
        private void RenderCaptureOnce()
        {
            EnsureCaptureSetup();
            if (captureCamera != null) captureCamera.Render();
        }

        [ContextMenu("Save Capture RT to PNG")]
        private void SaveCapturePNG()
        {
            if (captureRT == null)
            {
                Debug.LogWarning("[OptiWaterController] no CaptureRT to save, enable Debug Capture first");
                return;
            }
            if (!Application.isPlaying) captureCamera?.Render();

            int w = captureRT.width, h = captureRT.height;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var prev = RenderTexture.active;
            RenderTexture.active = captureRT;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            byte[] png = tex.EncodeToPNG();
            string path = Path.Combine(Application.dataPath, captureSavePath);
            File.WriteAllBytes(path, png);
            Debug.Log("[OptiWaterController] Capture saved: " + path);

            if (Application.isPlaying) Destroy(tex); else DestroyImmediate(tex);
#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
        }

        public bool GerstnerWaveEnabled     { get => gerstnerWaveEnabled;     set { gerstnerWaveEnabled = value;     UpdateShaderKeyword(); } }
        public bool NormalPerturbEnabled    { get => normalPerturbEnabled;    set { normalPerturbEnabled = value;    UpdateShaderKeyword(); } }
        public bool CausticsEnabled         { get => causticsEnabled;         set { causticsEnabled = value;         UpdateShaderKeyword(); } }
        public bool FoamEnabled             { get => foamEnabled;             set { foamEnabled = value;             UpdateShaderKeyword(); } }
        public bool CrestGlowEnabled        { get => crestGlowEnabled;        set { crestGlowEnabled = value;        UpdateShaderKeyword(); } }
        public bool PlanarReflectionEnabled { get => planarReflectionEnabled; set { planarReflectionEnabled = value; UpdateShaderKeyword(); } }
        public bool MicroNormalEnabled      { get => microNormalEnabled;      set { microNormalEnabled = value;      UpdateShaderKeyword(); } }
        public bool ShoreWaveEnabled        { get => shoreWaveEnabled;        set { shoreWaveEnabled = value;        UpdateShaderKeyword(); } }
        public bool ShoreWaveNormalEnabled  { get => shoreWaveNormalEnabled;  set { shoreWaveNormalEnabled = value;  UpdateShaderKeyword(); } }
        public bool BottomDistortEnabled    { get => bottomDistortEnabled;    set { bottomDistortEnabled = value;    UpdateShaderKeyword(); } }

        public int QualityMeshSegments
        {
            get => meshSegments;
            set
            {
                int target = Mathf.Clamp(value, 2, 512);
                if (target != meshSegments)
                {
                    meshSegments = target;
                    RebuildWaterMesh();
                }
            }
        }

        private void UpdateShaderKeyword()
        {
            if (waterMaterial == null) return;

            SetFeature("_GerstnerWave", gerstnerWaveEnabled);
            SetFeature("_NormalPerturb", normalPerturbEnabled);
            SetFeature("_Caustics", causticsEnabled);
            SetFeature("_Foam", foamEnabled);
            SetFeature("_CrestGlow", crestGlowEnabled);
            SetFeature("_PlanarReflection", planarReflectionEnabled);
            SetFeature("_MicroNormal", microNormalEnabled);
            SetFeature("_ShoreWave", shoreWaveEnabled);
            SetFeature("_ShoreWaveNormal", shoreWaveNormalEnabled);
            SetFeature("_BottomDistort", bottomDistortEnabled);
        }

        private void SetFeature(string name, bool enabled)
        {
            if (waterMaterial.HasProperty(name))
                waterMaterial.SetFloat(name, enabled ? 1f : 0f);

            if (name == "_PlanarReflection")
            {
                if (planarRenderer == null)
                {
                    var renderers = Object.FindObjectsByType<OptiWaterPlanarReflectionRenderer>(FindObjectsSortMode.None);
                    if (renderers.Length > 0) planarRenderer = renderers[0];
                }
                if (planarRenderer != null)
                    planarRenderer.reflectionEnabled = enabled;
            }
        }

        private static Mesh GenerateWaterMesh(int segmentsX, int segmentsZ)
        {
            Vector3[] verts = new Vector3[4];
            Vector2[] uv = new Vector2[4];
            Vector3[] normals = new Vector3[4];
            Vector4[] tangents = new Vector4[4];
            int[] tris = new int[6];

            verts[0] = new Vector3(-0.5f, 0, -0.5f);
            verts[1] = new Vector3( 0.5f, 0, -0.5f);
            verts[2] = new Vector3(-0.5f, 0,  0.5f);
            verts[3] = new Vector3( 0.5f, 0,  0.5f);

            uv[0] = new Vector2(0, 0);
            uv[1] = new Vector2(1, 0);
            uv[2] = new Vector2(0, 1);
            uv[3] = new Vector2(1, 1);

            for (int i = 0; i < 4; i++)
            {
                normals[i] = Vector3.up;
                tangents[i] = new Vector4(1, 0, 0, 1);
            }

            tris[0] = 0; tris[1] = 2; tris[2] = 1;
            tris[3] = 1; tris[4] = 2; tris[5] = 3;

            Mesh mesh = new Mesh();
            mesh.vertices = verts;
            mesh.uv = uv;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
