using System.IO;
using UnityEditor;
using UnityEngine;

namespace CelestialSystem.Editor
{
    /// <summary>
    /// Procedurally generates high-quality placeholder textures for the CelestialSystem:
    ///   • Moon_Albedo.png (maria basins + craters)
    ///   • Moon_Normal.png (surface bump/crater relief)
    ///   • Star_Particle.png (soft radial star sprite)
    ///   • MilkyWay_Panorama.png (galactic plane + dust lanes)
    ///
    /// Runs via Window > Celestial System > Generate Placeholder Textures
    /// or automatically on asset initialization.
    /// </summary>
    public static class CelestialTextureGenerator
    {
        private const string TextureDir = "Assets/CelestialSystem/Textures_Placeholders";

        [MenuItem("Window/Celestial System/Generate Placeholder Textures", priority = 101)]
        public static void GenerateAllTextures()
        {
            if (!AssetDatabase.IsValidFolder(TextureDir))
            {
                Directory.CreateDirectory(TextureDir);
                AssetDatabase.Refresh();
            }

            GenerateMoonAlbedoAndNormal();
            GenerateStarParticle();
            GenerateMilkyWayPanorama();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CelestialSystem] All placeholder textures generated successfully in " + TextureDir);
        }

        // ─────────────────────────────────────────────────────────────
        //  1. Moon Albedo & Normal
        // ─────────────────────────────────────────────────────────────
        private static void GenerateMoonAlbedoAndNormal()
        {
            string albedoPath = Path.Combine(TextureDir, "Moon_Albedo.png");
            string normalPath = Path.Combine(TextureDir, "Moon_Normal.png");

            int width = 512;
            int height = 512;

            float[,] heightMap = new float[width, height];
            Texture2D albedoTex = new Texture2D(width, height, TextureFormat.RGBA32, true);
            Texture2D normalTex = new Texture2D(width, height, TextureFormat.RGBA32, true);

            // Pre-seed crater centers
            int craterCount = 35;
            Vector3[] craters = new Vector3[craterCount]; // x, y, radius
            var rand = new System.Random(42);
            for (int i = 0; i < craterCount; i++)
            {
                craters[i] = new Vector3(
                    (float)rand.NextDouble() * width,
                    (float)rand.NextDouble() * height,
                    10f + (float)rand.NextDouble() * 45f
                );
            }

            // Build height & albedo
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = (float)x / width;
                    float v = (float)y / height;

                    // Multi-octave lunar maria & highlands
                    float n1 = Mathf.PerlinNoise(u * 4f, v * 4f);
                    float n2 = Mathf.PerlinNoise(u * 12f + 5f, v * 12f + 5f) * 0.5f;
                    float n3 = Mathf.PerlinNoise(u * 32f + 10f, v * 32f + 10f) * 0.25f;
                    float baseHeight = (n1 + n2 + n3) / 1.75f;

                    // Crater depression & rim elevation
                    float craterMod = 0f;
                    for (int c = 0; c < craterCount; c++)
                    {
                        float dx = Mathf.Abs(x - craters[c].x);
                        if (dx > width * 0.5f) dx = width - dx;
                        float dy = Mathf.Abs(y - craters[c].y);
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        float r = craters[c].z;

                        if (dist < r * 1.3f)
                        {
                            float normDist = dist / r;
                            if (normDist < 0.85f)
                            {
                                // Crater bowl depression
                                craterMod -= (1f - normDist) * 0.35f;
                            }
                            else if (normDist <= 1.25f)
                            {
                                // Raised crater rim
                                float rimFactor = 1f - Mathf.Abs(normDist - 1.0f) / 0.25f;
                                craterMod += rimFactor * 0.25f;
                            }
                        }
                    }

                    float finalH = Mathf.Clamp01(baseHeight + craterMod);
                    heightMap[x, y] = finalH;

                    // Albedo tone: maria (low elevation) is dark, highlands are bright
                    float tone = Mathf.Lerp(0.38f, 0.88f, finalH);
                    // Subtle color variation
                    Color col = new Color(tone * 0.98f, tone * 0.98f, tone, 1f);
                    albedoTex.SetPixel(x, y, col);
                }
            }

            // Generate Sobel normal map from heightMap
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int xL = (x - 1 + width) % width;
                    int xR = (x + 1) % width;
                    int yD = (y - 1 + height) % height;
                    int yU = (y + 1) % height;

                    float dX = (heightMap[xR, y] - heightMap[xL, y]) * 4.0f;
                    float dY = (heightMap[x, yU] - heightMap[x, yD]) * 4.0f;

                    Vector3 normal = new Vector3(-dX, -dY, 1f).normalized;
                    Color normCol = new Color(
                        normal.x * 0.5f + 0.5f,
                        normal.y * 0.5f + 0.5f,
                        normal.z * 0.5f + 0.5f,
                        1f
                    );
                    normalTex.SetPixel(x, y, normCol);
                }
            }

            albedoTex.Apply();
            normalTex.Apply();

            File.WriteAllBytes(albedoPath, albedoTex.EncodeToPNG());
            File.WriteAllBytes(normalPath, normalTex.EncodeToPNG());

            Object.DestroyImmediate(albedoTex);
            Object.DestroyImmediate(normalTex);

            AssetDatabase.ImportAsset(albedoPath);
            AssetDatabase.ImportAsset(normalPath);

            // Mark normal map in importer
            TextureImporter normalImporter = AssetImporter.GetAtPath(normalPath) as TextureImporter;
            if (normalImporter != null)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.SaveAndReimport();
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  2. Star Particle Sprite
        // ─────────────────────────────────────────────────────────────
        private static void GenerateStarParticle()
        {
            string path = Path.Combine(TextureDir, "Star_Particle.png");
            int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            float center = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    // Soft radial glow
                    float radial = Mathf.Exp(-dist * 4.5f);

                    // Subtle 4-point cross diffraction spike
                    float spikeX = Mathf.Exp(-Mathf.Abs(dx) * 20f) * Mathf.Exp(-Mathf.Abs(dy) * 2f);
                    float spikeY = Mathf.Exp(-Mathf.Abs(dy) * 20f) * Mathf.Exp(-Mathf.Abs(dx) * 2f);
                    float spike = (spikeX + spikeY) * 0.35f;

                    float alpha = Mathf.Clamp01(radial + spike);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
        }

        // ─────────────────────────────────────────────────────────────
        //  3. Milky Way Panorama
        // ─────────────────────────────────────────────────────────────
        private static void GenerateMilkyWayPanorama()
        {
            string path = Path.Combine(TextureDir, "MilkyWay_Panorama.png");
            int width = 1024;
            int height = 512;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, true);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = (float)x / width;
                    float v = (float)y / height;

                    // Galactic plane is an inclined sinusoidal band
                    float galacticCenterY = 0.5f + Mathf.Sin(u * Mathf.PI * 2f) * 0.22f;
                    float distToGalacticPlane = Mathf.Abs(v - galacticCenterY);

                    // Galactic band intensity falloff
                    float band = Mathf.Exp(-distToGalacticPlane * 12f);

                    // Galactic bulge (bright core around u = 0.5)
                    float distToCore = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, galacticCenterY));
                    float core = Mathf.Exp(-distToCore * 18f) * 1.5f;

                    // Clumpy star clouds + dark dust rift lane
                    float noiseClumps = Mathf.PerlinNoise(u * 20f, v * 20f);
                    float noiseDust   = Mathf.PerlinNoise(u * 35f + 17f, v * 35f + 17f);
                    float dustRift = 1f - Mathf.Exp(-Mathf.Abs(distToGalacticPlane - 0.02f) * 30f) * 0.65f * noiseDust;

                    float intensity = (band * 0.7f + core) * (noiseClumps * 0.8f + 0.3f) * dustRift;
                    intensity = Mathf.Clamp01(intensity);

                    // Celestial dust tint: pale silver with subtle violet/gold hints
                    Color mwColor = new Color(
                        intensity * 0.95f,
                        intensity * 0.92f,
                        intensity * 1.05f,
                        intensity
                    );

                    tex.SetPixel(x, y, mwColor);
                }
            }

            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
        }
    }
}

