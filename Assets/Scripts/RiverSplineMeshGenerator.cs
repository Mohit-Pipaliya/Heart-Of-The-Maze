using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// RiverSplineMeshGenerator generates a subdivided river ribbon along Unity Splines.
/// UV.x: Cross riverbank (0 to 1).
/// UV.y: Longitudinal flow direction (metrically proportional).
/// VertexColor.r: Edge proximity (1 at banks, 0 at center) for shoreline foam.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RiverSplineMeshGenerator : MonoBehaviour
{
    [Header("Spline Binding")]
    [SerializeField] private SplineContainer splineContainer;
    [SerializeField] private int splineIndex = 0;

    [Header("River Geometry Dimensions")]
    [Tooltip("Total river width from bank to bank (meters).")]
    [Range(2f, 60f)] public float riverWidth = 14f;
    [Tooltip("Distance along spline between vertex rings (meters). Lower = denser mesh.")]
    [Range(0.4f, 4f)] public float segmentLength = 1.0f;
    [Tooltip("Subdivisions across river width for Gerstner wave deformation.")]
    [Range(2, 32)] public int crossSubdivisions = 8;
    [Tooltip("Texture tiling along river flow.")]
    public float textureTilingAlongFlow = 0.25f;

    private MeshFilter _filter;

    private void OnEnable()
    {
        _filter = GetComponent<MeshFilter>();
        GenerateRiverMesh();
    }

    private void OnValidate()
    {
        GenerateRiverMesh();
    }

    [ContextMenu("Rebuild River Mesh")]
    public void GenerateRiverMesh()
    {
        if (splineContainer == null) splineContainer = GetComponent<SplineContainer>();
        if (splineContainer == null || splineContainer.Splines == null || splineContainer.Splines.Count <= splineIndex) return;

        Spline spline = splineContainer.Splines[splineIndex];
        float splineLength = spline.GetLength();
        if (splineLength < 1f) return;

        int numRings = Mathf.Max(2, Mathf.CeilToInt(splineLength / segmentLength));
        int vertsPerRing = crossSubdivisions + 1;
        int totalVerts = numRings * vertsPerRing;

        List<Vector3> vertices = new List<Vector3>(totalVerts);
        List<Vector3> normals = new List<Vector3>(totalVerts);
        List<Vector4> tangents = new List<Vector4>(totalVerts);
        List<Vector2> uvs = new List<Vector2>(totalVerts);
        List<Color> vertexColors = new List<Color>(totalVerts);
        List<int> triangles = new List<int>((numRings - 1) * crossSubdivisions * 6);

        float halfWidth = riverWidth * 0.5f;

        for (int r = 0; r < numRings; r++)
        {
            float t = (float)r / (numRings - 1);
            spline.Evaluate(t, out Unity.Mathematics.float3 pos, out Unity.Mathematics.float3 tan, out Unity.Mathematics.float3 up);

            Vector3 worldCenter = splineContainer.transform.TransformPoint(pos);
            Vector3 worldTan = splineContainer.transform.TransformDirection(tan).normalized;
            Vector3 worldUp = splineContainer.transform.TransformDirection(up).normalized;
            Vector3 worldRight = Vector3.Cross(worldUp, worldTan).normalized;

            Vector3 localCenter = transform.InverseTransformPoint(worldCenter);
            Vector3 localRight = transform.InverseTransformDirection(worldRight);
            Vector3 localTan = transform.InverseTransformDirection(worldTan);
            Vector3 localUp = transform.InverseTransformDirection(worldUp);

            float accumulatedDistance = t * splineLength;

            for (int c = 0; c <= crossSubdivisions; c++)
            {
                float lateralPct = (float)c / crossSubdivisions; // 0 to 1
                float offsetFromCenter = Mathf.Lerp(-halfWidth, halfWidth, lateralPct);
                Vector3 vertPos = localCenter + localRight * offsetFromCenter;

                vertices.Add(vertPos);
                normals.Add(localUp);
                tangents.Add(new Vector4(localTan.x, localTan.y, localTan.z, 1f));

                // UV: x = cross width (0..1), y = longitudinal flow distance
                uvs.Add(new Vector2(lateralPct, accumulatedDistance * textureTilingAlongFlow));

                // Shore edge proximity in R channel: 1 at banks, 0 in river center
                float bankDistance = Mathf.Abs(lateralPct - 0.5f) * 2.0f; // 0 at center, 1 at banks
                vertexColors.Add(new Color(bankDistance, 0f, 0f, 1f));
            }
        }

        for (int r = 0; r < numRings - 1; r++)
        {
            int ringStart = r * vertsPerRing;
            int nextRingStart = (r + 1) * vertsPerRing;

            for (int c = 0; c < crossSubdivisions; c++)
            {
                int current = ringStart + c;
                int next = current + 1;
                int currentAbove = nextRingStart + c;
                int nextAbove = currentAbove + 1;

                // Quad formed by two triangles
                triangles.Add(current);
                triangles.Add(currentAbove);
                triangles.Add(next);

                triangles.Add(next);
                triangles.Add(currentAbove);
                triangles.Add(nextAbove);
            }
        }

        Mesh mesh = _filter.sharedMesh;
        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = "RiverSpline_ProceduralMesh";
            _filter.sharedMesh = mesh;
        }
        else
        {
            mesh.Clear();
        }

        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTangents(tangents);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(vertexColors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
    }
}
