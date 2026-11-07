using UnityEngine;

/// <summary>
/// Unified wave & swell mathematical model shared between C# physics and Shader Graph HLSL.
/// Includes 4-octave Gerstner waves + large downhill rapid swell ridges.
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

    // 4-octave Gerstner river waves
    public static readonly WaveData[] DefaultRiverWaves = new WaveData[4]
    {
        new WaveData(new Vector2(0.0f, 1.0f),    0.16f,  9.5f, 0.40f, 2.2f),
        new WaveData(new Vector2(0.25f, 0.97f),  0.10f,  5.2f, 0.45f, 2.7f),
        new WaveData(new Vector2(-0.22f, 0.98f), 0.06f,  2.8f, 0.35f, 3.4f),
        new WaveData(new Vector2(0.08f, 1.0f),   0.03f,  1.4f, 0.30f, 4.1f)
    };

    // Large rolling swell ridge parameters (Temple Run / Whitewater rapids swell)
    public static float SwellAmplitude = 0.28f;
    public static float SwellWavelength = 18.0f;
    public static float SwellSpeed = 3.2f;

    public static Vector3 EvaluateWaterDisplacement(Vector3 worldPos, float time, WaveData[] waves = null)
    {
        waves ??= DefaultRiverWaves;
        Vector3 disp = Vector3.zero;
        Vector2 xz = new Vector2(worldPos.x, worldPos.z);

        // 1. 4-wave Gerstner sum
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

        // 2. Large rolling swell ridges along downstream flow
        float kSwell = 2f * Mathf.PI / Mathf.Max(1f, SwellWavelength);
        float swellPhase = kSwell * worldPos.z - (kSwell * SwellSpeed) * time;
        disp.y += Mathf.Sin(swellPhase) * SwellAmplitude;

        return disp;
    }

    public static float SampleWaterHeight(Vector3 worldPos, float time, float baseWaterLevel = 0f, WaveData[] waves = null)
    {
        // If TempleRunRiver is in scene, sample its slope height as the base level
        if (TempleRunRiver.Instance != null && TempleRunRiver.Instance.enabled)
        {
            return TempleRunRiver.Instance.GetWaterHeight(worldPos);
        }

        return baseWaterLevel + EvaluateWaterDisplacement(worldPos, time, waves).y;
    }

    public static Vector3 SampleWaterNormal(Vector3 worldPos, float time, WaveData[] waves = null)
    {
        if (TempleRunRiver.Instance != null && TempleRunRiver.Instance.enabled)
        {
            return TempleRunRiver.Instance.GetSurfaceNormal(worldPos);
        }

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
