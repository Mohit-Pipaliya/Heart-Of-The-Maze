#ifndef GERSTNER_WAVES_INCLUDED
#define GERSTNER_WAVES_INCLUDED

struct GerstnerWaveParams
{
    float2 direction;
    float amplitude;
    float wavelength;
    float steepness;
    float speed;
};

void EvaluateGerstnerWave(
    float2 worldXZ,
    float time,
    GerstnerWaveParams wave,
    inout float3 displacement,
    inout float3 normal,
    inout float crestFactor)
{
    float2 dir = normalize(wave.direction);
    float k = 6.28318530718 / max(wave.wavelength, 0.001);
    float c = sqrt(9.81 / k) * wave.speed;
    float q = wave.steepness / (k * max(wave.amplitude, 0.0001) * 4.0);
    
    float theta = k * dot(dir, worldXZ) - c * time;
    float sinTheta = sin(theta);
    float cosTheta = cos(theta);

    displacement.x += q * wave.amplitude * dir.x * cosTheta;
    displacement.y += wave.amplitude * sinTheta;
    displacement.z += q * wave.amplitude * dir.y * cosTheta;

    float wa = k * wave.amplitude;
    normal.x -= dir.x * wa * cosTheta;
    normal.y -= q * wa * sinTheta;
    normal.z -= dir.y * wa * cosTheta;

    crestFactor += sinTheta;
}

// Shader Graph Custom Function entry point
void CalculateGerstnerWaves_float(
    float3 WorldPos,
    float Time,
    float4 Wave0, // x: dirX, y: dirZ, z: amp, w: length
    float4 Wave1,
    float4 Wave2,
    float4 Wave3,
    float4 SpeedsSteepness, // x,y,z,w: speeds for waves 0-3
    float4 SteepnessAll,   // x,y,z,w: steepness for waves 0-3
    out float3 WorldDisplacement,
    out float3 WorldNormal,
    out float CrestIntensity)
{
    float3 disp = float3(0, 0, 0);
    float3 norm = float3(0, 1, 0);
    float crest = 0;

    GerstnerWaveParams waves[4];
    
    waves[0].direction  = Wave0.xy;
    waves[0].amplitude  = Wave0.z;
    waves[0].wavelength = Wave0.w;
    waves[0].speed      = SpeedsSteepness.x;
    waves[0].steepness  = SteepnessAll.x;

    waves[1].direction  = Wave1.xy;
    waves[1].amplitude  = Wave1.z;
    waves[1].wavelength = Wave1.w;
    waves[1].speed      = SpeedsSteepness.y;
    waves[1].steepness  = SteepnessAll.y;

    waves[2].direction  = Wave2.xy;
    waves[2].amplitude  = Wave2.z;
    waves[2].wavelength = Wave2.w;
    waves[2].speed      = SpeedsSteepness.z;
    waves[2].steepness  = SteepnessAll.z;

    waves[3].direction  = Wave3.xy;
    waves[3].amplitude  = Wave3.z;
    waves[3].wavelength = Wave3.w;
    waves[3].speed      = SpeedsSteepness.w;
    waves[3].steepness  = SteepnessAll.w;

    [unroll]
    for (int i = 0; i < 4; i++)
    {
        EvaluateGerstnerWave(WorldPos.xz, Time, waves[i], disp, norm, crest);
    }

    WorldDisplacement = disp;
    WorldNormal = normalize(norm);
    CrestIntensity = saturate((crest / 4.0) * 0.5 + 0.5);
}

#endif // GERSTNER_WAVES_INCLUDED

