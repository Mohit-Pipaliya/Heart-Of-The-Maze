using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TempleRunRiver — AAA Sloped Downhill River Rapids System (Temple Run / Uncharted style).
/// Generates a clean, smooth, downhill-sloping river surface mesh with flowing UVs,
/// bank foam vertex colors, and multi-octave water rapids.
/// Provides exact surface height, normal, and downstream current direction for boat navigation.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TempleRunRiver : MonoBehaviour
{
    public static TempleRunRiver Instance { get; private set; }

    [Header("River Dimensions & Slope (Temple Run Style)")]
    [Tooltip("Total river width from left bank to right bank (meters)")]
    public float riverWidth = 90f;

    [Tooltip("Total river length along flow direction (meters)")]
    public float riverLength = 320f;

    [Tooltip("Start elevation at top of the river (World Y)")]
    public float startElevation = 4.5f;

    [Tooltip("Total vertical drop from top to bottom of rapids (meters)")]
    public float totalDrop = 22f;

    [Tooltip("Subdivisions across river width")]
    [Range(8, 64)] public int subdivisionsX = 32;

    [Tooltip("Subdivisions along river length (slope)")]
    [Range(16, 160)] public int subdivisionsZ = 80;

    [Tooltip("Texture tiling along river flow")]
    public float textureTilingZ = 0.12f;

    [Header("River Rapids Animation")]
    [Tooltip("Downhill current flow speed (m/s)")]
    public float currentFlowSpeed = 3.2f;

    [Tooltip("Rapid wave ripple height (meters)")]
    public float rapidWaveHeight = 0.22f;

    [Tooltip("Rapid wave frequency")]
    public float rapidWaveFrequency = 1.8f;

    [Header("Collision & Physics")]
    public bool configureTriggerCollider = true;

    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private Mesh _runtimeMesh;

    // Cache for fast buoyancy sampling
    private float _riverStartZ;
    private float _riverEndZ;
    private float _riverStartX;
    private float _riverEndX;

    private void Awake()
    {
        Instance = this;
        InitAndBuild();
    }

    private void OnEnable()
    {
        Instance = this;
        InitAndBuild();
    }

    private void OnValidate()
    {
        InitAndBuild();
    }

    private void Start()
    {
        InitAndBuild();
    }

    private void Update()
    {
        if (Instance == null) Instance = this;
    }

    [ContextMenu("Rebuild Sloped River Mesh")]
    public void InitAndBuild()
    {
        if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
        if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();
        if (_meshFilter == null) return;

        BuildSlopedRiverMesh();
        ConfigureCollider();
    }

    private void BuildSlopedRiverMesh()
    {
        int nx = Mathf.Clamp(subdivisionsX, 4, 64);
        int nz = Mathf.Clamp(subdivisionsZ, 8, 160);

        int totalVerts = (nx + 1) * (nz + 1);
        List<Vector3> vertices = new List<Vector3>(totalVerts);
        List<Vector3> normals = new List<Vector3>(totalVerts);
        List<Vector4> tangents = new List<Vector4>(totalVerts);
        List<Vector2> uvs = new List<Vector2>(totalVerts);
        List<Color> vertexColors = new List<Color>(totalVerts);
        List<int> triangles = new List<int>(nx * nz * 6);

        float halfWidth = riverWidth * 0.5f;

        // Downward slope angle and tangent calculation
        float dropPerMeter = totalDrop / Mathf.Max(1f, riverLength);
        Vector3 slopeTangent = new Vector3(0f, -dropPerMeter, 1f).normalized;
        Vector3 slopeNormal = new Vector3(0f, 1f, dropPerMeter).normalized;

        for (int iz = 0; iz <= nz; iz++)
        {
            float tz = (float)iz / nz; // 0 (start/top) to 1 (end/bottom)
            float localZ = (tz - 0.5f) * riverLength;

            // Downward slope: smooth downhill curve
            // tz = 0 -> localY = 0.5f * totalDrop
            // tz = 1 -> localY = -0.5f * totalDrop
            float localY = Mathf.Lerp(totalDrop * 0.5f, -totalDrop * 0.5f, tz);

            for (int ix = 0; ix <= nx; ix++)
            {
                float tx = (float)ix / nx; // 0 (left) to 1 (right)
                float localX = (tx - 0.5f) * riverWidth;

                Vector3 pos = new Vector3(localX, localY, localZ);
                vertices.Add(pos);
                normals.Add(slopeNormal);
                tangents.Add(new Vector4(slopeTangent.x, slopeTangent.y, slopeTangent.z, 1f));

                // UVs: U = 0..1 across banks, V = along downhill flow
                uvs.Add(new Vector2(tx, tz * riverLength * textureTilingZ));

                // Bank foam: 1 near left/right edges, 0 in center
                float bankProximity = Mathf.Abs(tx - 0.5f) * 2f; // 0 at center, 1 at bank
                float whitewater = Mathf.Pow(bankProximity, 2f);
                vertexColors.Add(new Color(whitewater, tz, 0f, 1f));
            }
        }

        // Generate triangles (clockwise from above)
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

                triangles.Add(current);
                triangles.Add(currentAbove);
                triangles.Add(next);

                triangles.Add(next);
                triangles.Add(currentAbove);
                triangles.Add(nextAbove);
            }
        }

        if (_runtimeMesh == null)
        {
            _runtimeMesh = new Mesh();
            _runtimeMesh.name = "TempleRun_SlopedRiver_Mesh";
        }
        else
        {
            _runtimeMesh.Clear();
        }

        _runtimeMesh.SetVertices(vertices);
        _runtimeMesh.SetNormals(normals);
        _runtimeMesh.SetTangents(tangents);
        _runtimeMesh.SetUVs(0, uvs);
        _runtimeMesh.SetColors(vertexColors);
        _runtimeMesh.SetTriangles(triangles, 0);
        _runtimeMesh.RecalculateBounds();

        _meshFilter.sharedMesh = _runtimeMesh;
    }

    private void ConfigureCollider()
    {
        if (!configureTriggerCollider) return;
        BoxCollider col = GetComponent<BoxCollider>();
        if (col != null)
        {
            col.isTrigger = true;
            col.size = new Vector3(riverWidth, totalDrop + 4f, riverLength);
            col.center = Vector3.zero;
        }
    }

    // ─── Buoyancy & Physics Surface Queries ─────────────────────────────────────

    /// <summary>
    /// Returns the exact sloped water elevation at any world position, including dynamic rapids ripples.
    /// </summary>
    public float GetWaterHeight(Vector3 worldPos)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPos);
        float halfLen = riverLength * 0.5f;

        // Normalized position along river length (0 = top/start, 1 = bottom/end)
        float tz = Mathf.Clamp01((localPos.z + halfLen) / Mathf.Max(1f, riverLength));

        // Smooth downhill slope elevation
        float slopeLocalY = Mathf.Lerp(totalDrop * 0.5f, -totalDrop * 0.5f, tz);
        Vector3 surfaceLocal = new Vector3(localPos.x, slopeLocalY, localPos.z);
        Vector3 surfaceWorld = transform.TransformPoint(surfaceLocal);

        // Dynamic rapids surface ripples
        float time = Time.time;
        float ripple1 = Mathf.Sin(worldPos.z * rapidWaveFrequency - time * currentFlowSpeed * 1.5f) * rapidWaveHeight;
        float ripple2 = Mathf.Sin(worldPos.x * 0.8f + worldPos.z * 1.2f - time * 2.8f) * (rapidWaveHeight * 0.45f);

        return surfaceWorld.y + ripple1 + ripple2;
    }

    /// <summary>
    /// Returns the surface normal of the sloped river rapids.
    /// </summary>
    public Vector3 GetSurfaceNormal(Vector3 worldPos)
    {
        float dropPerMeter = totalDrop / Mathf.Max(1f, riverLength);
        Vector3 localNorm = new Vector3(0f, 1f, dropPerMeter).normalized;
        return transform.TransformDirection(localNorm).normalized;
    }

    /// <summary>
    /// Returns the downhill flow vector (direction water travels downstream).
    /// </summary>
    public Vector3 GetFlowDirection(Vector3 worldPos)
    {
        float dropPerMeter = totalDrop / Mathf.Max(1f, riverLength);
        Vector3 localFlow = new Vector3(0f, -dropPerMeter, 1f).normalized;
        return transform.TransformDirection(localFlow).normalized;
    }

    /// <summary>
    /// Returns true if a position is within the river canal boundaries.
    /// </summary>
    public bool IsInRiverBounds(Vector3 worldPos)
    {
        Vector3 local = transform.InverseTransformPoint(worldPos);
        return Mathf.Abs(local.x) <= riverWidth * 0.5f && Mathf.Abs(local.z) <= riverLength * 0.5f;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.1f, 0.7f, 1f, 0.4f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(riverWidth, totalDrop, riverLength));
    }
}
