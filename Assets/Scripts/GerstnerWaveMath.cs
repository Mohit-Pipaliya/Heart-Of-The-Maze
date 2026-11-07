using UnityEngine;

/// <summary>
/// GerstnerWaveMath provides unified wave evaluation matching GerstnerWaves.hlsl exactly.
/// This guarantees that boat buoyancy and visual water displacement never drift apart.
/// </summary>
public static class GerstnerWaveMath
{
    public struct WaveData
    {
        public Vector2 direction;
        public float amplitude;
        public float wavelength;
        public float steepness;
        public float speed;

        public WaveData(Vector2 dir, float amp, float len, float steep, float spd)
        {
            direction = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector2.up;
            amplitude = amp;
            wavelength = Mathf.Max(0.1f, len);
            steepness = Mathf.Clamp01(steep);
            speed = spd;
        }
    }

    public static readonly WaveData[] DefaultRiverWaves = new WaveData[4]
    {
        new WaveData(new Vector2(0.0f, 1.0f),   0.18f,  8.0f, 0.45f, 1.6f),
        new WaveData(new Vector2(0.3f, 0.95f),  0.11f,  4.5f, 0.50f, 2.1f),
        new WaveData(new Vector2(-0.25f, 0.96f),0.07f,  2.2f, 0.40f, 2.8f),
        new WaveData(new Vector2(0.1f, 1.0f),   0.035f, 1.1f, 0.35f, 3.4f)
    };

    public static Vector3 EvaluateWaterDisplacement(Vector3 worldPos, float time, WaveData[] waves = null)
    {
        waves ??= DefaultRiverWaves;
        Vector3 disp = Vector3.zero;
        Vector2 xz = new Vector2(worldPos.x, worldPos.z);

        for (int i = 0; i < waves.Length; i++)
        {
            WaveData w = waves[i];
            float k = 2f * Mathf.PI / w.wavelength;
            float c = Mathf.Sqrt(9.81f / k) * w.speed;
            float q = w.steepness / (k * Mathf.Max(w.amplitude, 0.0001f) * waves.Length);

            float theta = k * Vector2.Dot(w.direction, xz) - c * time;
            float sinTheta = Mathf.Sin(theta);
            float cosTheta = Mathf.Cos(theta);

            disp.x += q * w.amplitude * w.direction.x * cosTheta;
            disp.y += w.amplitude * sinTheta;
            disp.z += q * w.amplitude * w.direction.y * cosTheta;
        }

        return disp;
    }

    public static float SampleWaterHeight(Vector3 worldPos, float time, float baseWaterLevel = -4.7f, WaveData[] waves = null)
    {
        return baseWaterLevel + EvaluateWaterDisplacement(worldPos, time, waves).y;
    }

    public static Vector3 SampleWaterNormal(Vector3 worldPos, float time, WaveData[] waves = null)
    {
        waves ??= DefaultRiverWaves;
        Vector3 normal = Vector3.up;
        Vector2 xz = new Vector2(worldPos.x, worldPos.z);

        for (int i = 0; i < waves.Length; i++)
        {
            WaveData w = waves[i];
            float k = 2f * Mathf.PI / w.wavelength;
            float c = Mathf.Sqrt(9.81f / k) * w.speed;
            float q = w.steepness / (k * Mathf.Max(w.amplitude, 0.0001f) * waves.Length);

            float theta = k * Vector2.Dot(w.direction, xz) - c * time;
            float sinTheta = Mathf.Sin(theta);
            float cosTheta = Mathf.Cos(theta);

            float wa = k * w.amplitude;
            normal.x -= w.direction.x * wa * cosTheta;
            normal.y -= q * wa * sinTheta;
            normal.z -= w.direction.y * wa * cosTheta;
        }

        return normal.normalized;
    }
}
