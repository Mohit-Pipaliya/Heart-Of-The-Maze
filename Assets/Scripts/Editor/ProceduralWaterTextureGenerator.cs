#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

public static class ProceduralWaterTextureGenerator
{
    [MenuItem("Window/Water/Generate Procedural Fallback Textures")]
    public static void GenerateTextures()
    {
        string dir = "Assets/Textures/Water";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        // 1. Procedural Noise & Foam Map
        Texture2D foamTex = new Texture2D(512, 512, TextureFormat.RGBA32, true);
        for (int y = 0; y < 512; y++)
        {
            for (int x = 0; x < 512; x++)
            {
                float n1 = Mathf.PerlinNoise(x * 0.03f, y * 0.03f);
                float n2 = Mathf.PerlinNoise(x * 0.08f + 12f, y * 0.08f + 45f) * 0.5f;
                float val = Mathf.Clamp01(n1 + n2);
                foamTex.SetPixel(x, y, new Color(val, val, val, 1f));
            }
        }
        foamTex.Apply();
        File.WriteAllBytes($"{dir}/Procedural_Foam_Noise.png", foamTex.EncodeToPNG());

        // 2. Procedural Water Normal Map
        Texture2D normTex = new Texture2D(512, 512, TextureFormat.RGBA32, true, true);
        for (int y = 0; y < 512; y++)
        {
            for (int x = 0; x < 512; x++)
            {
                float hL = Mathf.PerlinNoise((x - 1) * 0.05f, y * 0.05f);
                float hR = Mathf.PerlinNoise((x + 1) * 0.05f, y * 0.05f);
                float hD = Mathf.PerlinNoise(x * 0.05f, (y - 1) * 0.05f);
                float hU = Mathf.PerlinNoise(x * 0.05f, (y + 1) * 0.05f);

                Vector3 normal = new Vector3((hL - hR) * 2.5f, (hD - hU) * 2.5f, 1f).normalized;
                Color col = new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.z * 0.5f + 0.5f, 1f);
                normTex.SetPixel(x, y, col);
            }
        }
        normTex.Apply();
        File.WriteAllBytes($"{dir}/Procedural_Water_Normal.png", normTex.EncodeToPNG());

        AssetDatabase.Refresh();
        Debug.Log("<color=cyan>[WaterGen]</color> Procedural water textures successfully generated in Assets/Textures/Water!");
    }
}
#endif

