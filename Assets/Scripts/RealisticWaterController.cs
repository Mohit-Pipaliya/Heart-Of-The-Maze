using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// RealisticWaterController — Production-ready realistic water system for Unity URP.
/// Automatically transforms low-poly / flat cube water objects into finely subdivided,
/// 3D Gerstner wave water volumes with physical wave crests, realistic rolling waves,
/// dynamic shader synchronization, and multi-octave water buoyancy for boats and physics objects.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RealisticWaterController : MonoBehaviour
{
    public static RealisticWaterController Instance { get; private set; }

    public enum WaterPreset
    {
        RealisticRiverOcean,
        CalmLake,
        RoughStormy,
        Custom
    }

    [Header("Preset & Profile")]
    [Tooltip("Predefined water styles")]
    public WaterPreset preset = WaterPreset.RealisticRiverOcean;
    private WaterPreset _lastPreset = WaterPreset.RealisticRiverOcean;

    [Header("Mesh Subdivision (Cube to Water Surface)")]
    [Tooltip("X axis subdivisions on the top surface")]
    [Range(16, 160)]
    public int resolutionX = 64;

    [Tooltip("Z axis subdivisions on the top surface")]
    [Range(16, 200)]
    public int resolutionZ = 128;

    [Tooltip("Include a short lip around the river so the edge has depth without giant side walls")]
    public bool generateVolumeSkirts = true;

    [Tooltip("How far the side lip drops, in world meters")]
    [Range(0.3f, 4f)]
    public float skirtWorldDepth = 1.35f;

    [Header("Wave Dynamics (Gerstner Waves)")]
    [Tooltip("Physical height of waves in meters (Crest to Trough amplitude)")]
    [Range(0.01f, 3.0f)]
    public float waveHeight = 0.45f;

    [Tooltip("Distance between wave crests in world meters")]
    [Range(2.0f, 60.0f)]
    public float waveLength = 22.0f;

    [Tooltip("Speed of wave motion across the water body")]
    [Range(0.0f, 6.0f)]
    public float waveSpeed = 1.35f;

    [Tooltip("Wave steepness / choppiness (0 = round smooth swells, 1 = sharp crests)")]
    [Range(0.0f, 1.0f)]
    public float waveSteepness = 0.45f;

    [Tooltip("Wind & wave flow direction in degrees (0 = East, 90 = North, 180 = West, 270 = South)")]
    [Range(0f, 360f)]
    public float windDirection = 45.0f;

    [Header("Multi-Octave Harmonics")]
    [Tooltip("Secondary cross-wave amplitude multiplier for natural chaotic interference")]
    [Range(0.0f, 1.0f)]
    public float secondaryWaveScale = 0.45f;

    [Tooltip("Secondary wave angle offset in degrees")]
    [Range(-90f, 90f)]
    public float secondaryAngleOffset = 38.0f;

    [Tooltip("Wind ripple / capillary wave scale")]
    [Range(0.0f, 0.5f)]
    public float rippleScale = 0.18f;

    [Header("Simulation & Performance")]
    [Tooltip("Physically undulate mesh vertices in 3D on CPU so water truly rolls in front of player")]
    public bool enableCPUVertexWaves = true;

    [Tooltip("Recalculate mesh normals for realistic sun glints and lighting highlights")]
    public bool recalculateMeshNormals = true;

    [Tooltip("Keep shader parameters (speed, frequency, scale, scrolling) synchronized with material")]
    public bool syncShaderProperties = true;

    [Tooltip("Normal map / ripple drift speed")]
    [Range(0.0f, 3.0f)]
    public float normalScrollSpeed = 1.0f;

    [Tooltip("Automatically set BoxCollider to Trigger so boats & players enter water naturally")]
    public bool makeColliderTrigger = true;

    [Header("Water Height Offset")]
    [Tooltip("Fine-tune base water level offset in world space")]
    public float baseWaterLevelOffset = 0.0f;

    // ─── Private State ────────────────────────────────────────────────────────
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private Material _waterMaterial;
    private Mesh _runtimeMesh;

    private Vector3[] _baseVertices;
    private Vector3[] _displacedVertices;
    private Color[] _colors;
    private Vector3[] _baseNormals;
    private int _topVertexCount;
    private int[] _skirtTopIndices;
    private int[] _skirtCorrespondingTop;

    private int _generatedResX = -1;
    private int _generatedResZ = -1;
    private bool _generatedSkirts = false;

    private Vector2 _currentNormalOffset = Vector2.zero;

    // Shader property IDs (Bitgem Stylised Water & standard URP water)
    private static readonly int PropWaveSpeed = Shader.PropertyToID("_WaveSpeed");
    private static readonly int PropWaveScale = Shader.PropertyToID("_WaveScale");
    private static readonly int PropWaveFreq = Shader.PropertyToID("_WaveFrequency");
    private static readonly int PropScrollSpeed = Shader.PropertyToID("_ScrollSpeed");
    private static readonly int PropBumpStrength = Shader.PropertyToID("_BumpStrength");
    private static readonly int PropFoamWidth = Shader.PropertyToID("_FoamWidth");
    private static readonly int PropNormalMap = Shader.PropertyToID("_NormalMap");

    // Default reference property IDs (used by Unity ShaderGraph)
    private static readonly int RefWaveSpeed = Shader.PropertyToID("Vector1_DF445019");
    private static readonly int RefWaveScale = Shader.PropertyToID("Vector1_E03AB928");
    private static readonly int RefWaveFreq = Shader.PropertyToID("Vector1_34CF798A");
    private static readonly int RefScrollSpeed = Shader.PropertyToID("Vector1_244B0600");
    private static readonly int RefFoamWidth = Shader.PropertyToID("Vector1_36E8227");

    // ─── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;
        InitComponents();
        ApplyPreset(preset, false);
        EnsureSubdividedMesh();
        ConfigureCollider();
        SyncMaterialProperties(true);
    }

    private void OnEnable()
    {
        Instance = this;
        InitComponents();
        EnsureSubdividedMesh();
        ConfigureCollider();
        SyncMaterialProperties(true);
    }

    private void Start()
    {
        EnsureSubdividedMesh();
        ConfigureCollider();
        SyncMaterialProperties(true);
    }

    private void OnValidate()
    {
        if (preset != _lastPreset && preset != WaterPreset.Custom)
        {
            ApplyPreset(preset, true);
            _lastPreset = preset;
        }

        if (resolutionX < 8) resolutionX = 8;
        if (resolutionZ < 8) resolutionZ = 8;

        if (_runtimeMesh == null || _generatedResX != resolutionX || _generatedResZ != resolutionZ || _generatedSkirts != generateVolumeSkirts)
        {
            EnsureSubdividedMesh();
        }

        ConfigureCollider();
        SyncMaterialProperties(true);
    }

    private void InitComponents()
    {
        if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
        if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();
        if (_meshRenderer != null && _waterMaterial == null)
        {
            _waterMaterial = _meshRenderer.sharedMaterial;
        }
    }

    private void ConfigureCollider()
    {
        if (!makeColliderTrigger) return;
        BoxCollider col = GetComponent<BoxCollider>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
        }
    }

    public void ApplyPreset(WaterPreset p, bool overwrite)
    {
        preset = p;
        if (p == WaterPreset.CalmLake)
        {
            waveHeight = 0.16f;
            waveLength = 16.0f;
            waveSpeed = 0.85f;
            waveSteepness = 0.25f;
            secondaryWaveScale = 0.3f;
            rippleScale = 0.12f;
            normalScrollSpeed = 0.6f;
        }
        else if (p == WaterPreset.RealisticRiverOcean)
        {
            waveHeight = 0.95f;
            waveLength = 13.0f;
            waveSpeed = 1.65f;
            waveSteepness = 0.62f;
            secondaryWaveScale = 0.55f;
            rippleScale = 0.22f;
            normalScrollSpeed = 0.85f;
        }
        else if (p == WaterPreset.RoughStormy)
        {
            waveHeight = 1.15f;
            waveLength = 32.0f;
            waveSpeed = 2.4f;
            waveSteepness = 0.75f;
            secondaryWaveScale = 0.6f;
            rippleScale = 0.28f;
            normalScrollSpeed = 1.8f;
        }
    }

    // ─── Mesh Subdivision & Generation ─────────────────────────────────────────

    public void EnsureSubdividedMesh()
    {
        InitComponents();
        if (_meshFilter == null) return;

        Mesh current = _meshFilter.sharedMesh;
        // Check if mesh is primitive cube (24 verts) or needs rebuild
        bool needsRebuild = (current == null) ||
                            (current.vertexCount <= 24) ||
                            (_runtimeMesh == null) ||
                            (_generatedResX != resolutionX) ||
                            (_generatedResZ != resolutionZ) ||
                            (_generatedSkirts != generateVolumeSkirts);

        if (needsRebuild)
        {
            RebuildSubdividedMesh();
        }
    }

    [ContextMenu("Rebuild Water Mesh")]
    public void RebuildSubdividedMesh()
    {
        InitComponents();
        if (_meshFilter == null) return;

        int rx = Mathf.Clamp(resolutionX, 8, 200);
        int rz = Mathf.Clamp(resolutionZ, 8, 240);

        float scaleY = Mathf.Max(0.001f, transform.lossyScale.y);
        float skirtDrop = Mathf.Clamp(skirtWorldDepth, 0.2f, scaleY) / scaleY;
        float bottomY = 0.5f - skirtDrop;

        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector4> tangents = new List<Vector4>();
        List<Color> colors = new List<Color>();
        List<int> tris = new List<int>();

        // 1. Top Surface Grid
        for (int ix = 0; ix <= rx; ix++)
        {
            float u = (float)ix / rx;
            float x = u - 0.5f;

            for (int iz = 0; iz <= rz; iz++)
            {
                float v = (float)iz / rz;
                float z = v - 0.5f;

                verts.Add(new Vector3(x, 0.5f, z));
                uvs.Add(new Vector2(u * 8f, v * 8f));
                normals.Add(Vector3.up);
                tangents.Add(new Vector4(1f, 0f, 0f, 1f));
                colors.Add(new Color(0f, 0f, 0f, 0f));
            }
        }

        _topVertexCount = verts.Count;

        // Top surface triangles (clockwise from above)
        for (int ix = 0; ix < rx; ix++)
        {
            for (int iz = 0; iz < rz; iz++)
            {
                int i00 = ix * (rz + 1) + iz;
                int i01 = ix * (rz + 1) + (iz + 1);
                int i10 = (ix + 1) * (rz + 1) + iz;
                int i11 = (ix + 1) * (rz + 1) + (iz + 1);

                tris.Add(i00); tris.Add(i01); tris.Add(i10);
                tris.Add(i10); tris.Add(i01); tris.Add(i11);
            }
        }

        // 2. Volume Skirts & Bottom (if enabled)
        List<int> skirtTopIdx = new List<int>();
        List<int> skirtRefTop = new List<int>();

        if (generateVolumeSkirts)
        {
            // South Skirt (Z = -0.5, iz = 0, outward normal -Z)
            for (int ix = 0; ix < rx; ix++)
            {
                int topRefA = ix * (rz + 1) + 0;
                int topRefB = (ix + 1) * (rz + 1) + 0;

                int vTopA = verts.Count;
                verts.Add(verts[topRefA]);
                uvs.Add(uvs[topRefA]);
                normals.Add(Vector3.back);
                tangents.Add(new Vector4(1f, 0f, 0f, 1f));
                colors.Add(new Color(0f, 0f, 0f, 0f));
                skirtTopIdx.Add(vTopA);
                skirtRefTop.Add(topRefA);

                int vTopB = verts.Count;
                verts.Add(verts[topRefB]);
                uvs.Add(uvs[topRefB]);
                normals.Add(Vector3.back);
                tangents.Add(new Vector4(1f, 0f, 0f, 1f));
                colors.Add(new Color(0f, 0f, 0f, 0f));
                skirtTopIdx.Add(vTopB);
                skirtRefTop.Add(topRefB);

                int vBotA = verts.Count;
                verts.Add(new Vector3(verts[topRefA].x, bottomY, -0.5f));
                uvs.Add(new Vector2(uvs[topRefA].x, 0f));
                normals.Add(Vector3.back);
                tangents.Add(new Vector4(1f, 0f, 0f, 1f));
                colors.Add(new Color(0f, 0f, 0f, 0f));

                int vBotB = verts.Count;
                verts.Add(new Vector3(verts[topRefB].x, bottomY, -0.5f));
                uvs.Add(new Vector2(uvs[topRefB].x, 0f));
                normals.Add(Vector3.back);
                tangents.Add(new Vector4(1f, 0f, 0f, 1f));
                colors.Add(new Color(0f, 0f, 0f, 0f));

                tris.Add(vTopA); tris.Add(vTopB); tris.Add(vBotA);
                tris.Add(vTopB); tris.Add(vBotB); tris.Add(vBotA);
            }

            // North Skirt (Z = +0.5, iz = rz, outward normal +Z)
            for (int ix = 0; ix < rx; ix++)
            {
                int topRefA = ix * (rz + 1) + rz;
                int topRefB = (ix + 1) * (rz + 1) + rz;

                int vTopA = verts.Count;
                verts.Add(verts[topRefA]);
                uvs.Add(uvs[topRefA]);
                normals.Add(Vector3.forward);
                tangents.Add(new Vector4(-1f, 0f, 0f, 1f));
                colors.Add(new Color(0f, 0f, 0f, 0f));
                skirtTopIdx.Add(vTopA);
                skirtRefTop.Add(topRefA);

                int vTopB = verts.Count;
                verts.Add(verts[topRefB]);
                uvs.Add(uvs[topRefB]);
                normals.Add(Vector3.forward);
                tangents.Add(new Vector4(-1f, 0f, 0f, 1f));
                colors.Add(new Color(0f, 0f, 0f, 0f));
                skirtTopIdx.Add(vTopB);
                skirtRefTop.Add(topRefB);

                int vBotA = verts.Count;
                verts.Add(new Vector3(verts[topRefA].x, bottomY, 0.5f));
                uvs.Add(new Vector2(uvs[topRefA].x, 0f));
                normals.Add(Vector3.forward);
                tangents.Add(new Vector4(-1f, 0f, 0f, 1f));
                colors.Add(new Color(0f, 0f, 0f, 0f));

                int vBotB = verts.Count;
                verts.Add(new Vector3(verts[topRefB].x, bottomY, 0.5f));
                uvs.Add(new Vector2(uvs[topRefB].x, 0f));
                normals.Add(Vector3.forward);
                tangents.Add(new Vector4(-1f, 0f, 0f, 1f));
                colors.Add(new Color(0f, 0f, 0f, 0f));

                tris.Add(vTopB); tris.Add(vTopA); tris.Add(vBotB);
                tris.Add(vTopA); tris.Add(vBotA); tris.Add(vBotB);
            }

            // West Skirt (X = -0.5, ix = 0, outward normal -X)
            for (int iz = 0; iz < rz; iz++)
            {
                int topRefA = 0 * (rz + 1) + iz;
                int topRefB = 0 * (rz + 1) + (iz + 1);

                int vTopA = verts.Count;
                verts.Add(verts[topRefA]);
                uvs.Add(uvs[topRefA]);
                normals.Add(Vector3.left);
                tangents.Add(new Vector4(0f, 0f, 1f, 1f));
                colors.Add(new Color(0f, 0f, 0f, 0f));
                skirtTopIdx.Add(vTopA);
                skirtRefTop.Add(topRefA);

                int vTopB = verts.Count;
                verts.Add(verts[topRefB]);
                uvs.Add(uvs[topRefB]);
                normals.Add(Vector3.left);
                tangents.Add(new Vector4(0f, 0f, 1f, 1f));
                colors.Add(new Color(0f, 0f, 0f, 0f));
                skirtTopIdx.Add(vTopB);
                skirtRefTop.Add(topRefB);

                int vBotA = verts.Count;
                verts.Add(new Vector3(-0.5f, bottomY, verts[topRefA].z));
                uvs.Add(new Vector2(uvs[topRefA].y, 0f));
                normals.Add(Vector3.left);
                tangents.Add(new Vector4(0f, 0f, 1f, 1f));
                colors.Add(new Color(0f, 0f, 0f, 0f));

                int vBotB = verts.Count;
                verts.Add(new Vector3(-0.5f, bottomY, verts[topRefB].z));
                uvs.Add(new Vector2(uvs[topRefB].y, 0f));
                normals.Add(Vector3.left);
                tangents.Add(new Vector4(0f, 0f, 1f, 1f));
                colors.Add(new Color(0f, 0f, 0f, 0f));

                tris.Add(vTopB); tris.Add(vTopA); tris.Add(vBotB);
                tris.Add(vTopA); tris.Add(vBotA); tris.Add(vBotB);
            }

            // East Skirt (X = +0.5, ix = rx, outward normal +X)
            for (int iz = 0; iz < rz; iz++)
            {
                int topRefA = rx * (rz + 1) + iz;
                int topRefB = rx * (rz + 1) + (iz + 1);

                int vTopA = verts.Count;
                verts.Add(verts[topRefA]);
                uvs.Add(uvs[topRefA]);
                normals.Add(Vector3.right);
                tangents.Add(new Vector4(0f, 0f, -1f, 1f));
                colors.Add(new Color(0f, 0f, 0f, 0f));
                skirtTopIdx.Add(vTopA);
                skirtRefTop.Add(topRefA);

                int vTopB = verts.Count;
                verts.Add(verts[topRefB]);
                uvs.Add(uvs[topRefB]);
                normals.Add(Vector3.right);
                tangents.Add(new Vector4(0f, 0f, -1f, 1f));
                colors.Add(new Color(0f, 0f, 0f, 0f));
                skirtTopIdx.Add(vTopB);
                skirtRefTop.Add(topRefB);

                int vBotA = verts.Count;
                verts.Add(new Vector3(0.5f, bottomY, verts[topRefA].z));
                uvs.Add(new Vector2(uvs[topRefA].y, 0f));
                normals.Add(Vector3.right);
                tangents.Add(new Vector4(0f, 0f, -1f, 1f));
                colors.Add(new Color(0f, 0f, 0f, 0f));

                int vBotB = verts.Count;
                verts.Add(new Vector3(0.5f, bottomY, verts[topRefB].z));
                uvs.Add(new Vector2(uvs[topRefB].y, 0f));
                normals.Add(Vector3.right);
                tangents.Add(new Vector4(0f, 0f, -1f, 1f));
                colors.Add(new Color(0f, 0f, 0f, 0f));

                tris.Add(vTopA); tris.Add(vTopB); tris.Add(vBotA);
                tris.Add(vTopB); tris.Add(vBotB); tris.Add(vBotA);
            }

            int b00 = verts.Count;
            verts.Add(new Vector3(-0.5f, bottomY, -0.5f));
            uvs.Add(new Vector2(0f, 0f));
            normals.Add(Vector3.down);
            tangents.Add(new Vector4(1f, 0f, 0f, 1f));
            colors.Add(new Color(0f, 0f, 0f, 0f));

            int b10 = verts.Count;
            verts.Add(new Vector3(0.5f, bottomY, -0.5f));
            uvs.Add(new Vector2(1f, 0f));
            normals.Add(Vector3.down);
            tangents.Add(new Vector4(1f, 0f, 0f, 1f));
            colors.Add(new Color(0f, 0f, 0f, 0f));

            int b01 = verts.Count;
            verts.Add(new Vector3(-0.5f, bottomY, 0.5f));
            uvs.Add(new Vector2(0f, 1f));
            normals.Add(Vector3.down);
            tangents.Add(new Vector4(1f, 0f, 0f, 1f));
            colors.Add(new Color(0f, 0f, 0f, 0f));

            int b11 = verts.Count;
            verts.Add(new Vector3(0.5f, bottomY, 0.5f));
            uvs.Add(new Vector2(1f, 1f));
            normals.Add(Vector3.down);
            tangents.Add(new Vector4(1f, 0f, 0f, 1f));
            colors.Add(new Color(0f, 0f, 0f, 0f));

            tris.Add(b00); tris.Add(b10); tris.Add(b01);
            tris.Add(b10); tris.Add(b11); tris.Add(b01);
        }

        _runtimeMesh = new Mesh();
        _runtimeMesh.name = "RealisticWater_Mesh";
        _runtimeMesh.MarkDynamic();
        _runtimeMesh.SetVertices(verts);
        _runtimeMesh.SetUVs(0, uvs);
        _runtimeMesh.SetNormals(normals);
        _runtimeMesh.SetTangents(tangents);
        _runtimeMesh.SetColors(colors);
        _runtimeMesh.SetTriangles(tris, 0);

        _baseVertices = verts.ToArray();
        _displacedVertices = new Vector3[_baseVertices.Length];
        Array.Copy(_baseVertices, _displacedVertices, _baseVertices.Length);
        _colors = colors.ToArray();
        _baseNormals = normals.ToArray();

        _skirtTopIndices = skirtTopIdx.ToArray();
        _skirtCorrespondingTop = skirtRefTop.ToArray();

        _meshFilter.sharedMesh = _runtimeMesh;
        _generatedResX = rx;
        _generatedResZ = rz;
        _generatedSkirts = generateVolumeSkirts;
    }

    // ─── Update Loop (Waves & Animation) ───────────────────────────────────────

    private void Update()
    {
        if (Instance == null) Instance = this;

        float time = Application.isPlaying ? Time.time : (float)(DateTime.Now.TimeOfDay.TotalSeconds % 10000.0);

        if (enableCPUVertexWaves)
        {
            UpdateVertexWaves(time);
        }

        if (syncShaderProperties)
        {
            UpdateShaderProperties(time);
        }
    }

    private void UpdateVertexWaves(float time)
    {
        if (_runtimeMesh == null || _baseVertices == null || _displacedVertices == null)
        {
            EnsureSubdividedMesh();
            if (_runtimeMesh == null) return;
        }

        Vector3 scale = transform.lossyScale;
        Vector3 position = transform.position;
        float invScaleX = scale.x != 0f ? 1f / scale.x : 1f;
        float invScaleY = scale.y != 0f ? 1f / scale.y : 1f;
        float invScaleZ = scale.z != 0f ? 1f / scale.z : 1f;

        // Animate Top Surface Vertices
        for (int i = 0; i < _topVertexCount; i++)
        {
            Vector3 baseLocal = _baseVertices[i];

            // Convert to world horizontal position for true world-meter wavelength
            float worldX = position.x + baseLocal.x * scale.x;
            float worldZ = position.z + baseLocal.z * scale.z;

            Vector3 waveDispWorld = SampleGerstnerWave(worldX, worldZ, time);

            _displacedVertices[i] = new Vector3(
                baseLocal.x + waveDispWorld.x * invScaleX,
                baseLocal.y + waveDispWorld.y * invScaleY,
                baseLocal.z + waveDispWorld.z * invScaleZ
            );

            if (_colors != null && i < _colors.Length)
            {
                float crest = Mathf.InverseLerp(waveHeight * 0.28f, waveHeight * 0.82f, waveDispWorld.y);
                _colors[i] = new Color(0f, 0f, 0f, crest);
            }
        }

        // Animate Skirt Top Edges to seal seamlessly with top wavy vertices
        if (generateVolumeSkirts && _skirtTopIndices != null)
        {
            for (int k = 0; k < _skirtTopIndices.Length; k++)
            {
                int skirtIdx = _skirtTopIndices[k];
                int topIdx = _skirtCorrespondingTop[k];
                _displacedVertices[skirtIdx] = _displacedVertices[topIdx];
            }
        }

        _runtimeMesh.vertices = _displacedVertices;
        if (_colors != null && _colors.Length == _displacedVertices.Length)
            _runtimeMesh.colors = _colors;

        if (recalculateMeshNormals)
        {
            _runtimeMesh.RecalculateNormals();
        }
    }

    public void SyncMaterialProperties(bool forceUpdate)
    {
        InitComponents();
        if (_waterMaterial == null && _meshRenderer != null)
        {
            _waterMaterial = _meshRenderer.sharedMaterial;
        }
        if (_waterMaterial == null) return;

        // Compute balanced wave speed and frequency for shader
        float speedVal = waveSpeed;
        float freqVal = Mathf.Clamp(40f / Mathf.Max(1f, waveLength), 0.5f, 5.0f);
        float scaleVal = Mathf.Clamp(waveHeight * 0.15f, 0.02f, 0.5f);

        // Standard properties
        if (_waterMaterial.HasProperty(PropWaveSpeed)) _waterMaterial.SetFloat(PropWaveSpeed, speedVal);
        if (_waterMaterial.HasProperty(PropWaveScale)) _waterMaterial.SetFloat(PropWaveScale, scaleVal);
        if (_waterMaterial.HasProperty(PropWaveFreq)) _waterMaterial.SetFloat(PropWaveFreq, freqVal);
        if (_waterMaterial.HasProperty(PropScrollSpeed)) _waterMaterial.SetFloat(PropScrollSpeed, normalScrollSpeed);
        if (_waterMaterial.HasProperty(PropFoamWidth)) _waterMaterial.SetFloat(PropFoamWidth, 0.08f);

        // ShaderGraph reference properties
        if (_waterMaterial.HasProperty(RefWaveSpeed)) _waterMaterial.SetFloat(RefWaveSpeed, speedVal);
        if (_waterMaterial.HasProperty(RefWaveScale)) _waterMaterial.SetFloat(RefWaveScale, scaleVal);
        if (_waterMaterial.HasProperty(RefWaveFreq)) _waterMaterial.SetFloat(RefWaveFreq, freqVal);
        if (_waterMaterial.HasProperty(RefScrollSpeed)) _waterMaterial.SetFloat(RefScrollSpeed, normalScrollSpeed);
        if (_waterMaterial.HasProperty(RefFoamWidth)) _waterMaterial.SetFloat(RefFoamWidth, 0.08f);
    }

    private void UpdateShaderProperties(float time)
    {
        if (_waterMaterial == null) return;

        // Drift normal map smoothly in wind direction
        float rad = windDirection * Mathf.Deg2Rad;
        Vector2 flowDir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        float dt = Application.isPlaying ? Time.deltaTime : 0.016f;
        _currentNormalOffset += flowDir * (normalScrollSpeed * 0.035f * dt);
        _currentNormalOffset.x = _currentNormalOffset.x % 1f;
        _currentNormalOffset.y = _currentNormalOffset.y % 1f;

        if (_waterMaterial.HasProperty(PropNormalMap))
        {
            _waterMaterial.SetTextureOffset(PropNormalMap, _currentNormalOffset);
        }
    }

    // ─── Gerstner Wave Core Algorithm ──────────────────────────────────────────

    /// <summary>
    /// Computes multi-octave Gerstner wave displacement (World X, Y, Z) at a world coordinate.
    /// </summary>
    public Vector3 SampleGerstnerWave(float worldX, float worldZ, float time)
    {
        Vector3 totalDisp = Vector3.zero;

        // Octave 1: Primary Ocean Swell
        float angle1 = windDirection * Mathf.Deg2Rad;
        Vector2 dir1 = new Vector2(Mathf.Cos(angle1), Mathf.Sin(angle1)).normalized;
        float len1 = waveLength;
        float amp1 = waveHeight * 0.58f;
        float spd1 = waveSpeed;
        float steep1 = waveSteepness * 0.5f;
        totalDisp += EvaluateGerstnerOctave(worldX, worldZ, time, dir1, len1, amp1, spd1, steep1);

        // Octave 2: Secondary Cross Wave (adds natural chaotic interference)
        if (secondaryWaveScale > 0.01f)
        {
            float angle2 = (windDirection + secondaryAngleOffset) * Mathf.Deg2Rad;
            Vector2 dir2 = new Vector2(Mathf.Cos(angle2), Mathf.Sin(angle2)).normalized;
            float len2 = waveLength * 0.48f;
            float amp2 = waveHeight * secondaryWaveScale * 0.32f;
            float spd2 = waveSpeed * 1.25f;
            float steep2 = waveSteepness * 0.4f;
            totalDisp += EvaluateGerstnerOctave(worldX, worldZ, time, dir2, len2, amp2, spd2, steep2);
        }

        // Octave 3: Surface Wind Ripples
        if (rippleScale > 0.01f)
        {
            float angle3 = (windDirection - 28f) * Mathf.Deg2Rad;
            Vector2 dir3 = new Vector2(Mathf.Cos(angle3), Mathf.Sin(angle3)).normalized;
            float len3 = waveLength * 0.22f;
            float amp3 = waveHeight * rippleScale * 0.15f;
            float spd3 = waveSpeed * 1.6f;
            float steep3 = waveSteepness * 0.25f;
            totalDisp += EvaluateGerstnerOctave(worldX, worldZ, time, dir3, len3, amp3, spd3, steep3);
        }

        return totalDisp;
    }

    private static Vector3 EvaluateGerstnerOctave(float x, float z, float t, Vector2 dir, float length, float amp, float speed, float steepness)
    {
        if (length <= 0.01f || amp <= 0.0001f) return Vector3.zero;

        float k = 2.0f * Mathf.PI / length;
        float w = k * speed;
        float phi = k * (dir.x * x + dir.y * z) - w * t;

        float cosPhi = Mathf.Cos(phi);
        float sinPhi = Mathf.Sin(phi);

        float q = Mathf.Clamp01(steepness);
        float dispX = q * amp * dir.x * cosPhi;
        float dispZ = q * amp * dir.y * cosPhi;
        float dispY = amp * sinPhi;

        return new Vector3(dispX, dispY, dispZ);
    }

    // ─── Buoyancy & Floating Physics API ───────────────────────────────────────

    /// <summary>
    /// Returns the exact current water surface elevation (World Y) at any World (X, Z) coordinate.
    /// Perfectly matches real-time visual waves for boat navigation and floating objects.
    /// </summary>
    public static float GetWaterHeight(Vector3 worldPos)
    {
        if (Instance == null)
        {
            // Auto-discover if available in scene
            Instance = FindFirstObjectByType<RealisticWaterController>();
        }

        if (Instance == null)
        {
            return -4.7f; // Default project water level fallback
        }

        return Instance.GetWaterHeightInternal(worldPos);
    }

    public float GetWaterHeightInternal(Vector3 worldPos)
    {
        float time = Application.isPlaying ? Time.time : (float)(DateTime.Now.TimeOfDay.TotalSeconds % 10000.0);
        float baseSurfaceY = transform.position.y + (transform.lossyScale.y * 0.5f) + baseWaterLevelOffset;

        Vector3 waveDisp = SampleGerstnerWave(worldPos.x, worldPos.z, time);
        return baseSurfaceY + waveDisp.y;
    }

    /// <summary>
    /// Wave height only, around zero. Use this to bob a boat without moving it to the river surface.
    /// </summary>
    public float GetWaveOffset(Vector3 worldPos)
    {
        float time = Application.isPlaying ? Time.time : (float)(DateTime.Now.TimeOfDay.TotalSeconds % 10000.0);
        return SampleGerstnerWave(worldPos.x, worldPos.z, time).y;
    }

    /// <summary>
    /// Returns the 3D surface tilt normal of the water at any World (X, Z) point.
    /// Use to orient floating boat hulls and rafts cleanly with wave crests.
    /// </summary>
    public static Vector3 GetWaterNormal(Vector3 worldPos)
    {
        if (Instance == null)
        {
            Instance = FindFirstObjectByType<RealisticWaterController>();
        }

        if (Instance == null) return Vector3.up;

        // Sample finite difference around point for robust, fast surface normal
        const float delta = 0.5f;
        float hL = Instance.GetWaterHeightInternal(new Vector3(worldPos.x - delta, 0f, worldPos.z));
        float hR = Instance.GetWaterHeightInternal(new Vector3(worldPos.x + delta, 0f, worldPos.z));
        float hB = Instance.GetWaterHeightInternal(new Vector3(worldPos.x, 0f, worldPos.z - delta));
        float hF = Instance.GetWaterHeightInternal(new Vector3(worldPos.x, 0f, worldPos.z + delta));

        Vector3 tangentX = new Vector3(delta * 2f, hR - hL, 0f).normalized;
        Vector3 tangentZ = new Vector3(0f, hF - hB, delta * 2f).normalized;

        return Vector3.Cross(tangentZ, tangentX).normalized;
    }

    /// <summary>
    /// Checks if a world position is inside or below the water surface.
    /// </summary>
    public static bool IsInWater(Vector3 worldPos, out float submersionDepth)
    {
        float waterY = GetWaterHeight(worldPos);
        submersionDepth = waterY - worldPos.y;
        return submersionDepth > 0f;
    }

    // ─── Scene Gizmos ──────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.4f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);

        // Wind direction arrow
        Gizmos.matrix = Matrix4x4.identity;
        float rad = windDirection * Mathf.Deg2Rad;
        Vector3 windDir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
        Vector3 center = transform.position + Vector3.up * (transform.lossyScale.y * 0.5f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(center, windDir * 15f);
        Gizmos.DrawSphere(center + windDir * 15f, 0.8f);
    }
}

