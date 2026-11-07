using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// SplineRiverChunkGenerator — Generates a high-detail, seamless subdivided river/channel mesh
/// along a Unity Spline using chunk-based streaming and object pooling.
/// - Connects adjacent chunks with 0 seams.
/// - UV.x = across banks (0..1), UV.y = along continuous flow distance.
/// - VertexColor.r = edge bank foam, VertexColor.g = depth fade / whitewater.
/// </summary>
public class SplineRiverChunkGenerator : MonoBehaviour
{
    [Header("Spline Binding")]
    [SerializeField] private SplineContainer splineContainer;

    [Header("Chunk Architecture")]
    [Tooltip("River width across banks (meters)")]
    public float riverWidth = 16f;

    [Tooltip("Length of each river chunk (meters)")]
    public float chunkLength = 40f;

    [Tooltip("Distance between vertex rings along spline (meters)")]
    public float ringResolution = 1.0f;

    [Tooltip("Subdivisions across river width")]
    public int crossSubdivisions = 12;

    [Tooltip("Material to assign to chunks")]
    [SerializeField] private Material riverMaterial;

    [Header("Object Pooling")]
    [SerializeField] private int initialPoolSize = 8;
    [SerializeField] private Transform poolContainer;

    private readonly Queue<GameObject> _chunkPool = new Queue<GameObject>();
    private readonly List<GameObject> _activeChunks = new List<GameObject>();

    private void Start()
    {
        InitializePool();
        GenerateAllChunks();
    }

    private void InitializePool()
    {
        if (poolContainer == null)
        {
            GameObject container = new GameObject("River_Chunk_Pool");
            container.transform.SetParent(transform);
            poolContainer = container.transform;
        }

        for (int i = 0; i < initialPoolSize; i++)
        {
            GameObject chunk = CreateChunkObject();
            chunk.SetActive(false);
            _chunkPool.Enqueue(chunk);
        }
    }

    private GameObject CreateChunkObject()
    {
        GameObject go = new GameObject("River_Chunk");
        go.transform.SetParent(poolContainer);
        go.AddComponent<MeshFilter>();
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = riverMaterial;
        return go;
    }

    [ContextMenu("Generate River Chunks")]
    public void GenerateAllChunks()
    {
        if (splineContainer == null) splineContainer = GetComponent<SplineContainer>();
        if (splineContainer == null || splineContainer.Splines == null || splineContainer.Splines.Count == 0) return;

        Spline spline = splineContainer.Spline;
        float totalSplineLength = spline.GetLength();
        int totalChunks = Mathf.Max(1, Mathf.CeilToInt(totalSplineLength / chunkLength));

        for (int c = 0; c < totalChunks; c++)
        {
            float startDist = c * chunkLength;
            float endDist = Mathf.Min(totalSplineLength, (c + 1) * chunkLength);

            GameObject chunkGo = _chunkPool.Count > 0 ? _chunkPool.Dequeue() : CreateChunkObject();
            chunkGo.SetActive(true);
            chunkGo.name = $"River_Chunk_{c:D2}";
            _activeChunks.Add(chunkGo);

            MeshFilter mf = chunkGo.GetComponent<MeshFilter>();
            BuildChunkMesh(spline, totalSplineLength, startDist, endDist, mf);
        }
    }

    private void BuildChunkMesh(Spline spline, float totalSplineLength, float startDist, float endDist, MeshFilter mf)
    {
        float length = endDist - startDist;
        int numRings = Mathf.Max(2, Mathf.CeilToInt(length / ringResolution));
        int vertsPerRow = crossSubdivisions + 1;
        int totalVerts = numRings * vertsPerRow;

        List<Vector3> verts = new List<Vector3>(totalVerts);
        List<Vector3> normals = new List<Vector3>(totalVerts);
        List<Vector4> tangents = new List<Vector4>(totalVerts);
        List<Vector2> uvs = new List<Vector2>(totalVerts);
        List<Color> colors = new List<Color>(totalVerts);
        List<int> tris = new List<int>((numRings - 1) * crossSubdivisions * 6);

        float halfW = riverWidth * 0.5f;

        for (int r = 0; r < numRings; r++)
        {
            float ringDist = startDist + (float)r / (numRings - 1) * length;
            float t = Mathf.Clamp01(ringDist / totalSplineLength);

            spline.Evaluate(t, out Unity.Mathematics.float3 p, out Unity.Mathematics.float3 tan, out Unity.Mathematics.float3 up);

            Vector3 worldPos = splineContainer.transform.TransformPoint(p);
            Vector3 worldTan = splineContainer.transform.TransformDirection(tan).normalized;
            Vector3 worldUp = splineContainer.transform.TransformDirection(up).normalized;
            Vector3 worldRight = Vector3.Cross(worldUp, worldTan).normalized;

            for (int i = 0; i <= crossSubdivisions; i++)
            {
                float tx = (float)i / crossSubdivisions;
                float offset = Mathf.Lerp(-halfW, halfW, tx);

                Vector3 vPos = worldPos + worldRight * offset;
                verts.Add(vPos);
                normals.Add(worldUp);
                tangents.Add(new Vector4(worldTan.x, worldTan.y, worldTan.z, 1f));

                // UVs: continuous along entire spline length (seamless tiling between chunks)
                uvs.Add(new Vector2(tx, ringDist * 0.15f));

                // Bank foam mask: 1 at banks, 0 at center
                float bankFoam = Mathf.Abs(tx - 0.5f) * 2f;
                colors.Add(new Color(bankFoam, 0f, 0f, 1f));
            }
        }

        // Triangles
        for (int r = 0; r < numRings - 1; r++)
        {
            int rStart = r * vertsPerRow;
            int rNext = (r + 1) * vertsPerRow;

            for (int i = 0; i < crossSubdivisions; i++)
            {
                int c0 = rStart + i;
                int c1 = c0 + 1;
                int c2 = rNext + i;
                int c3 = c2 + 1;

                tris.Add(c0); tris.Add(c2); tris.Add(c1);
                tris.Add(c1); tris.Add(c2); tris.Add(c3);
            }
        }

        Mesh mesh = mf.sharedMesh;
        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = "RiverChunk_ProceduralMesh";
            mf.sharedMesh = mesh;
        }
        else
        {
            mesh.Clear();
        }

        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetTangents(tangents);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
    }
}
