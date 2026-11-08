using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MountainRapidsRiver — Fast-flowing dangerous mountain river rapids with downhill slope.
/// Generates a clean, sloped river mesh flowing downhill from the mountain (no skirts, no spikes).
/// Provides exact downhill water elevation, surface normal, and raging current velocity for boat physics.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MountainRapidsRiver : MonoBehaviour
{
    public static MountainRapidsRiver Instance { get; private set; }

    [Header("Mountain Rapids Dimensions")]
    [Tooltip("Total river width from left bank to right bank (meters)")]
    public float riverWidth = 118f;

    [Tooltip("Total river length along flow direction (meters)")]
    public float riverLength = 300f;

    [Tooltip("Start elevation at top of mountain rapids (World Y)")]
    public float topElevation = -4.5f;

    [Tooltip("Bottom elevation at base of rapids valley (World Y)")]
    public float bottomElevation = -22.0f;

    [Header("Rapids Current & Turbulence")]
    [Tooltip("Fast downstream river current speed (m/s) — dangerous rushing rapids")]
    public float currentSpeed = 6.0f;

    [Tooltip("Whitewater rapid wave amplitude (meters)")]
    public float rapidWaveHeight = 0.28f;

    [Tooltip("Whitewater wave frequency")]
    public float rapidWaveFrequency = 2.4f;

    [Tooltip("How fast waves rush downhill through the rapids")]
    public float waveScrollSpeed = 7.0f;

    [Header("Mesh Density")]
    [Range(8, 64)] public int subdivisionsX = 32;
    [Range(16, 160)] public int subdivisionsZ = 80;
    public float textureTilingX = 1.0f;
    public float textureTilingZ = 0.12f;

    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private Mesh _runtimeMesh;

    private void Awake()
    {
        Instance = this;
        GenerateRapidsMesh();
    }

    private void OnEnable()
    {
        Instance = this;
        GenerateRapidsMesh();
    }

    private void OnValidate()
    {
        GenerateRapidsMesh();
    }

    private void Start()
    {
        GenerateRapidsMesh();
    }

    private void Update()
    {
        if (Instance == null) Instance = this;
    }

    [ContextMenu("Rebuild Mountain Rapids Mesh")]
    public void GenerateRapidsMesh()
    {
        if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
        if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();
        if (_meshFilter == null) return;

        int nx = Mathf.Clamp(subdivisionsX, 4, 64);
        int nz = Mathf.Clamp(subdivisionsZ, 8, 160);

        int totalVerts = (nx + 1) * (nz + 1);
        List<Vector3> verts = new List<Vector3>(totalVerts);
        List<Vector3> norms = new List<Vector3>(totalVerts);
        List<Vector4> tans = new List<Vector4>(totalVerts);
        List<Vector2> uvs = new List<Vector2>(totalVerts);
        List<Color> colors = new List<Color>(totalVerts);
        List<int> tris = new List<int>(nx * nz * 6);

        float totalDrop = topElevation - bottomElevation;
        float dropPerMeter = totalDrop / Mathf.Max(1f, riverLength);

        // Slope tangent: points downstream along the downhill gradient
        Vector3 slopeTangent = new Vector3(0f, -dropPerMeter, 1f).normalized;
        // Slope normal: perpendicular to slope pointing upward
        Vector3 slopeNormal = new Vector3(0f, 1f, dropPerMeter).normalized;

        float halfWidth = riverWidth * 0.5f;

        for (int iz = 0; iz <= nz; iz++)
        {
            float tz = (float)iz / nz; // 0 = mountain top, 1 = bottom valley
            float localZ = (tz - 0.5f) * riverLength;

            // Compute world Y along downhill slope, convert to local Y
            float worldY = Mathf.Lerp(topElevation, bottomElevation, tz);
            float localY = worldY - transform.position.y;

            for (int ix = 0; ix <= nx; ix++)
            {
                float tx = (float)ix / nx; // 0 = left bank, 1 = right bank
                float localX = (tx - 0.5f) * riverWidth;

                verts.Add(new Vector3(localX, localY, localZ));
                norms.Add(slopeNormal);
                tans.Add(new Vector4(slopeTangent.x, slopeTangent.y, slopeTangent.z, 1f));
                uvs.Add(new Vector2(tx * textureTilingX, tz * riverLength * textureTilingZ));

                // Bank foam (R) & rapid current whitewater intensity (G)
                float bankDist = Mathf.Abs(tx - 0.5f) * 2f; // 0 at river center, 1 at banks
                float bankFoam = Mathf.Pow(bankDist, 2.0f);
                float rapidWhitewater = 0.75f + 0.25f * Mathf.Sin(tz * 24f);
                colors.Add(new Color(bankFoam, rapidWhitewater, 0f, 1f));
            }
        }

        int vertsPerRow = nx + 1;
        for (int iz = 0; iz < nz; iz++)
        {
            int rowStart = iz * vertsPerRow;
            int nextRowStart = (iz + 1) * vertsPerRow;

            for (int ix = 0; ix < nx; ix++)
            {
                int current = rowStart + ix;
                int next = current + 1;
                int currentAbove = nextRowStart + ix;
                int nextAbove = currentAbove + 1;

                tris.Add(current);
                tris.Add(currentAbove);
                tris.Add(next);

                tris.Add(next);
                tris.Add(currentAbove);
                tris.Add(nextAbove);
            }
        }

        if (_runtimeMesh == null)
        {
            _runtimeMesh = new Mesh();
            _runtimeMesh.name = "MountainRapids_SlopedRiver_Mesh";
        }
        else
        {
            _runtimeMesh.Clear();
        }

        _runtimeMesh.SetVertices(verts);
        _runtimeMesh.SetNormals(norms);
        _runtimeMesh.SetTangents(tans);
        _runtimeMesh.SetUVs(0, uvs);
        _runtimeMesh.SetColors(colors);
        _runtimeMesh.SetTriangles(tris, 0);
        _runtimeMesh.RecalculateBounds();

        _meshFilter.sharedMesh = _runtimeMesh;

        // Configure trigger collider for water volume
        BoxCollider col = GetComponent<BoxCollider>();
        if (col != null)
        {
            col.isTrigger = true;
            col.size = new Vector3(riverWidth, Mathf.Abs(totalDrop) + 8f, riverLength);
            col.center = Vector3.zero;
        }
    }

    // ─── Surface Queries For Boat Buoyancy & Physics ────────────────────────────

    /// <summary>
    /// Returns the exact downhill water surface elevation at any world coordinate,
    /// including dynamic rushing whitewater rapids and fast-moving wave peaks.
    /// </summary>
    public float GetWaterHeight(Vector3 worldPos)
    {
        Vector3 local = transform.InverseTransformPoint(worldPos);
        float halfLen = riverLength * 0.5f;
        float tz = Mathf.Clamp01((local.z + halfLen) / Mathf.Max(1f, riverLength));

        float baseSlopeWorldY = Mathf.Lerp(topElevation, bottomElevation, tz);

        // Rapid whitewater waves rushing downhill
        float time = Application.isPlaying ? Time.time : 0f;
        float wave1 = Mathf.Sin(worldPos.z * rapidWaveFrequency - time * waveScrollSpeed) * rapidWaveHeight;
        float wave2 = Mathf.Cos(worldPos.x * 0.8f + worldPos.z * 1.5f - time * (waveScrollSpeed * 0.75f)) * (rapidWaveHeight * 0.5f);
        float wave3 = Mathf.Sin(worldPos.x * 1.8f - time * 6.5f) * (rapidWaveHeight * 0.25f);

        return baseSlopeWorldY + wave1 + wave2 + wave3;
    }

    /// <summary>
    /// Returns the downhill surface normal at a position for realistic boat tilt.
    /// </summary>
    public Vector3 GetSurfaceNormal(Vector3 worldPos)
    {
        float totalDrop = topElevation - bottomElevation;
        float dropPerMeter = totalDrop / Mathf.Max(1f, riverLength);
        Vector3 localNorm = new Vector3(0f, 1f, dropPerMeter).normalized;
        return transform.TransformDirection(localNorm).normalized;
    }

    /// <summary>
    /// Returns the rushing downstream current velocity vector (meters/second).
    /// </summary>
    public Vector3 GetCurrentVelocity(Vector3 worldPos)
    {
        float totalDrop = topElevation - bottomElevation;
        float dropPerMeter = totalDrop / Mathf.Max(1f, riverLength);
        Vector3 localDownstream = new Vector3(0f, -dropPerMeter, 1f).normalized;
        Vector3 worldDirection = transform.TransformDirection(localDownstream).normalized;
        return worldDirection * currentSpeed;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.45f);
        Gizmos.matrix = transform.localToWorldMatrix;
        float totalDrop = Mathf.Abs(topElevation - bottomElevation);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(riverWidth, totalDrop + 4f, riverLength));
    }
}
