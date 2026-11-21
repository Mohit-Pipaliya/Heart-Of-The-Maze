Shader "OptiWater/Water Surface"
{
    Properties
    {
        _WaterColor("Water Color", Color) = (0.2, 0.5, 0.8, 0.6)
        _DeepWaterColor("Deep Water Color", Color) = (0.05, 0.2, 0.4, 1)
        _ShallowDeepBlendDepth("Shallow to Deep Blend Depth (m)", Range(0.5, 50.0)) = 8.0

        _AlphaClearDepth("Alpha Clear Depth (fully transparent, m)", Range(0.0, 50.0)) = 0.0
        _AlphaFullDepth("Alpha Full Depth (fully water color, m)", Range(0.0, 1.0)) = 8.0
        _AlphaFalloffPower("Alpha Edge Falloff Power (low=sharp edge)", Range(0.00, 8.0)) = 0.35
        _AlphaEdgeWidth("Alpha Cliff Edge Width (horizontal, m)", Range(0.0, 20.0)) = 3.0
        _AlphaEdgeMaxDepth("Alpha Edge Max Depth (deep water cutoff, m)", Range(0.5, 100.0)) = 8.0

        [Toggle] _GerstnerWave("Enable Gerstner Wave", Float) = 1
        _WaveFrequency("Wave Frequency (global mult)", Float) = 1.0
        _WaveAmplitude("Wave Amplitude (global mult)", Float) = 1.0
        _WaveSpeed("Wave Speed", Float) = 1.0
        _WaveSteepness("Wave Steepness (Q)", Range(0, 1)) = 0.5
        _A_Direction("Wave A Direction", Vector) = (1, 0.6, 0, 0)
        _A_Amplitude("Wave A Amplitude", Float) = 0.4
        _A_Frequency("Wave A Frequency", Float) = 0.08
        _B_Direction("Wave B Direction", Vector) = (-0.7, 1, 0, 0)
        _B_Amplitude("Wave B Amplitude", Float) = 0.25
        _B_Frequency("Wave B Frequency", Float) = 0.14
        _C_Direction("Wave C Direction", Vector) = (0.4, -1, 0, 0)
        _C_Amplitude("Wave C Amplitude", Float) = 0.15
        _C_Frequency("Wave C Frequency", Float) = 0.22
        _D_Direction("Wave D Direction", Vector) = (-1, -0.3, 0, 0)
        _D_Amplitude("Wave D Amplitude", Float) = 0.1
        _D_Frequency("Wave D Frequency", Float) = 0.35
        _WaveDirSpeed("Wave Direction Drift Speed", Float) = 0.15
        _WaveFreqMod("Wave Frequency Mod Depth", Float) = 0.35
        _WaveFreqSpeed("Wave Frequency Mod Speed", Float) = 0.2

        [Toggle] _NormalPerturb("Enable Normal Map Detail", Float) = 1
        _NormalMap("Normal Map", 2D) = "bump" { }
        _NormalStrength("Normal Strength", Range(0, 2)) = 0.5
        _NormalBlend("Normal Blend (0=Gerstner only, 1=Detail full)", Range(0, 1)) = 0.6
        _NormalWorldScale("Normal World Scale (world pos divisor)", Float) = 10.0

        _ShorelineAlphaFalloff("Foam Shoreline Decay (low=soft, high=sharp)", Range(0.05, 40.0)) = 0.5
        _ShorelineDepthFade("Shoreline Depth Fade (world units)", Range(0.1, 20.0)) = 2.0

        _CausticsTex("Caustics Texture", 2D) = "white" { }
        _CausticsStrength("Caustics Strength", Float) = 1.0
        _CausticsSpeed("Caustics Speed", Float) = 0.1
        [Toggle] _Caustics("Enable Caustics", Float) = 1

        _FoamTex("Foam Texture", 2D) = "white" { }
        _FoamIntensity("Foam Intensity", Float) = 1.0
        _FoamDepthThreshold("Foam Depth Threshold", Float) = 0.3
        _FoamShorelineBoost("Foam Shoreline Boost", Range(0.01, 2)) = 0.5
        _FoamPulseSpeed("Foam Pulse Speed", Float) = 1.5
        _FoamScalePulse("Foam Scale Pulse Strength", Range(0, 1)) = 0.2
        _WaveFoamIntensity("Wave Foam Intensity", Range(0, 10)) = 1.0
        _WaveFoamThreshold("Wave Foam Threshold", Range(0.01, 10)) = 3.0
        [Toggle] _Foam("Enable Foam", Float) = 1

        [Toggle] _CrestGlow("Enable Crest Glow", Float) = 1
        _CrestGlowColor("Crest Glow Color", Color) = (1, 1, 1, 1)
        _CrestGlowThreshold("Crest Glow Threshold (normal.y below = glow)", Range(0.5, 1.0)) = 0.95
        _CrestGlowIntensity("Crest Glow Intensity", Range(0, 12)) = 1.0
        _CrestGlowPower("Crest Glow Falloff Power", Range(0.5, 8)) = 2.0

        [Toggle] _PlanarReflection("Enable Planar Reflection", Float) = 1
        _ReflectionTex("Reflection RT (Planar)", 2D) = "black" { }
        _ReflectionIntensity("Reflection Intensity", Float) = 0.6
        _DistortionStrength("Reflection Distortion", Range(0, 1)) = 0.15
        _FresnelPower("Fresnel Power", Float) = 3.0
        _FresnelBias("Fresnel Bias", Float) = 0.05

        _Smoothness("Smoothness (0=rough,1=mirror)", Range(0, 1)) = 0.8
        _SpecularIntensity("Specular Intensity", Range(0, 12)) = 1.0
        _SunGlitterRoughness("Sun Glitter Width (high=wide/soft)", Range(0.06, 1.0)) = 0.35
        _SunGlitterStrength("Sun Glitter Strength", Range(0, 8)) = 1.5
        _SunGlitterSparkle("Sun Glitter Sparkle (0=smooth,1=broken)", Range(0, 1)) = 1.0

        [Toggle] _MicroNormal("Enable Micro Normal (smooth grid lines)", Float) = 0

        [Toggle] _ShoreWave("Enable Shore Wave", Float) = 1
        _ShoreWaveFrequency("Shore Wave Frequency (higher = tighter spacing)", Float) = 0.5
        _ShoreWaveSpeed("Shore Wave Speed", Float) = 0.3
        _ShoreWaveMix("Shore Wave Mix (0=ElevDiff, 1=DistField)", Range(0, 1)) = 0.5
        _ShoreWaveFoamStrength("Shore Wave Foam Strength", Range(0, 5000)) = 1.0
        _ShoreWaveNormalStrength("Shore Wave Normal Strength", Range(0, 2)) = 0.5
        [Toggle] _ShoreWaveNormal("Enable Shore Wave Normal Peak", Float) = 1
        _ShoreWaveWidth("Shore Wave Line Width (low=thin, high=thick)", Range(0.02, 1.0)) = 0.3
        _ShoreWaveStart("Shore Wave Start (waves appear this far offshore, m)", Range(0.0, 20.0)) = 0.0
        _ShoreWaveRange("Shore Wave Range (offshore reach, m)", Range(0.5, 50.0)) = 6.67
        _ShoreWaveFalloff("Shore Wave Falloff (higher = more shore-weighted)", Range(0.1, 4.0)) = 1.5
        _ShoreWaveSlopeReach("Shore Wave Slope Reach (far on gentle / near on steep, 0=off)", Range(0, 10)) = 0
        _ShoreWaveSlopeRef("Shore Wave Slope Ref (slope normalization reference)", Float) = 1.0
        _ShoreWaveFoamTexTiling("Shore Wave Foam Tiling (1=dense, 10000=sparse)", Range(1, 10000)) = 1000
        _ShoreWaveFoamMaskSpeed("Shore Wave Foam Mask Speed (0=static)", Range(0, 5)) = 0.15
        _ShoreWaveFoamMaskFloor("Shore Wave Foam Mask Floor (0=break,1=solid)", Range(0, 1)) = 0.25
        _ShoreWaveFoamMaskPower("Shore Wave Foam Mask Contrast Power", Range(0.1, 8)) = 1.0

        _DeepFoamStart("Deep Foam Start Depth (m)", Range(0.0, 50.0)) = 4.0
        _DeepFoamFade("Deep Foam Fade Range (m)", Range(0.1, 50.0)) = 6.0
        _DeepFoamIntensity("Deep Foam Intensity (0=off)", Range(0.0, 5.0)) = 1.0

        [Toggle] _BottomDistort("Enable Shallow Bottom Distortion", Float) = 0
        _BottomDistortStrength("Bottom Distort Strength (screen UV)", Range(0, 0.05)) = 0.012
        _BottomDistortDepth("Bottom Distort Depth (m)", Range(0.1, 20.0)) = 3.0
        _BottomDistortSpeed("Bottom Distort Speed (0=static)", Range(0, 3)) = 0.8
        _BottomDistortTint("Bottom Distort Water Tint (0=dry copy,1=strong)", Range(0, 1)) = 0.25

        _WaterSurfaceHeight("Water Surface Height", Float) = 95.0
        _WaterClipThreshold("Water Clip Threshold (Elevation Bias, m)", Range(-50, 200)) = 5.0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            Stencil
            {
                Ref 1
                ReadMask 1
                WriteMask 1
                Comp Always
                Pass Replace
                ZFail Replace
            }

            HLSLPROGRAM
            #pragma vertex WaterVertex
            #pragma fragment WaterFragment

            #if UNITY_EDITOR
            #pragma shader_feature_local _ _DEBUG_CLIP
            #pragma shader_feature_local _ _DEBUG_SHOREMASK
            #endif
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            #define PI 3.14159265359

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv : TEXCOORD0;
                float4 worldPos : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 tangentWS : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
                float4 screenPos : TEXCOORD5;
                float3 viewDirWS : TEXCOORD6;
            };

            CBUFFER_START(UnityPerMaterial)
            float4 _WaterColor;
            float4 _DeepWaterColor;
            float _ShallowDeepBlendDepth;
            float _AlphaClearDepth, _AlphaFullDepth, _AlphaFalloffPower;
            float _AlphaEdgeWidth;
            float _AlphaEdgeMaxDepth;
            float _NormalStrength, _NormalBlend, _NormalWorldScale;
            float _WaveFrequency, _WaveAmplitude, _WaveSpeed, _WaveSteepness, _WaveDirSpeed, _WaveFreqMod, _WaveFreqSpeed;
            float _A_Amplitude, _A_Frequency, _B_Amplitude, _B_Frequency, _C_Amplitude, _C_Frequency, _D_Amplitude, _D_Frequency;
            float _ShorelineAlphaFalloff, _ShorelineDepthFade;
            float _CausticsStrength, _CausticsSpeed;
            float _GerstnerWave, _NormalPerturb, _Caustics, _Foam, _CrestGlow, _PlanarReflection, _MicroNormal, _ShoreWave, _ShoreWaveNormal, _BottomDistort;
            float _FoamIntensity, _FoamDepthThreshold, _FoamShorelineBoost, _FoamPulseSpeed, _FoamScalePulse;
            float _WaveFoamIntensity, _WaveFoamThreshold;
            float4 _CrestGlowColor;
            float _CrestGlowThreshold, _CrestGlowIntensity, _CrestGlowPower;
            float _ReflectionIntensity, _FresnelPower, _FresnelBias, _DistortionStrength;
            float _Smoothness;
            float _SpecularIntensity;
            float _SunGlitterRoughness, _SunGlitterStrength, _SunGlitterSparkle;
            float _WaterSurfaceHeight;
            float _WaterClipThreshold;
            float _BottomDistortStrength, _BottomDistortDepth, _BottomDistortSpeed, _BottomDistortTint;
            float4 _CausticsTex_ST, _FoamTex_ST, _NormalMap_ST;
            float4 _A_Direction, _B_Direction, _C_Direction, _D_Direction;
            float _ShoreWaveFrequency, _ShoreWaveSpeed, _ShoreWaveMix, _ShoreWaveFoamStrength, _ShoreWaveNormalStrength, _ShoreWaveWidth, _ShoreWaveStart, _ShoreWaveRange, _ShoreWaveFalloff, _ShoreWaveSlopeReach, _ShoreWaveSlopeRef, _ShoreWaveFoamTexTiling, _ShoreWaveFoamMaskSpeed, _ShoreWaveFoamMaskFloor, _ShoreWaveFoamMaskPower;
            float _DeepFoamStart, _DeepFoamFade, _DeepFoamIntensity;
            CBUFFER_END

            TEXTURE2D(_CausticsTex);  SAMPLER(sampler_CausticsTex);
            TEXTURE2D(_NormalMap);    SAMPLER(sampler_NormalMap);
            TEXTURE2D(_FoamTex);      SAMPLER(sampler_FoamTex);
            TEXTURE2D(_ReflectionTex); SAMPLER(sampler_ReflectionTex);

            float4x4 _OptiWaterMirrorVP;
            float3 _OptiWaterPlanePosWS;
            float3 _OptiWaterPlaneNormalWS;

            void GerstnerWave(float2 pos, float t, float2 dir, float freq, float amplitude, float steepness, inout float3 nAcc, inout float2 flowAcc)
            {
                dir = normalize(dir);
                float k = 6.28318530718 * freq;
                float w = k * _WaveSpeed;
                float phase = k * dot(dir, pos) - w * t;
                float c = cos(phase);
                float s = sin(phase);
                float ka = k * amplitude;
                nAcc.x += -dir.x * ka * c;
                nAcc.z += -dir.y * ka * c;
                nAcc.y += -steepness * ka * s;
                flowAcc += steepness * amplitude * dir * c;
            }

            float2 Rot2(float2 d, float a)
            {
                float s = sin(a), c = cos(a);
                return float2(d.x * c - d.y * s, d.x * s + d.y * c);
            }

            float3 ReconstructSceneWorldPos(float2 screenUV, float rawDepth, float3 fragWorldPos, float waterEyeDepth, out float sceneEye)
            {
                sceneEye = LinearEyeDepth(rawDepth, _ZBufferParams);
                float4 viewPosFrag = mul(UNITY_MATRIX_V, float4(fragWorldPos, 1.0));
                float scale = sceneEye / max(viewPosFrag.z * -1.0, 1e-5);
                float3 viewPosScene = float3(viewPosFrag.xy * scale, -sceneEye);
                float3 world = mul(UNITY_MATRIX_I_V, float4(viewPosScene, 1.0)).xyz;
                return world;
            }

            float SampleSceneHeight(float2 screenUV, float3 fragWorldPos, float waterEyeDepth)
            {
                float raw = SampleSceneDepth(screenUV);
                float unused;
                float3 w = ReconstructSceneWorldPos(screenUV, raw, fragWorldPos, waterEyeDepth, unused);
                return w.y;
            }

            float3 BlendNormalsWhiteout(float3 n1, float3 n2)
            {
                n1 = normalize(n1);
                n2 = normalize(n2);
                return normalize(float3(n1.xy + n2.xy, n1.z * n2.z));
            }

            void ComputeShoreAdvancingWave(float shoreCoord, float t, float shoreStart, out float foam, out float2 normalPerturb)
            {
                foam = 0.0;
                normalPerturb = float2(0.0, 0.0);
                float phase = shoreCoord * _ShoreWaveFrequency + t * _ShoreWaveSpeed;
                float band = sin(phase);
                float peak = saturate(band * 0.5 + 0.5);
                float edge = smoothstep(1.0 - _ShoreWaveWidth, 1.0, peak);
                float gate = smoothstep(shoreStart, shoreStart + 0.5, shoreCoord);
                float distFromStart = max(shoreCoord - shoreStart, 0.0);
                float envelope = exp(-distFromStart / _ShoreWaveRange) * gate;
                envelope = pow(envelope, _ShoreWaveFalloff);
                foam = edge * envelope * _ShoreWaveFoamStrength;
                float slope = cos(phase);
                normalPerturb = float2(slope, slope) * edge * envelope * _ShoreWaveNormalStrength * 0.5;
            }

            float3 ComputeWaterWorksSpecular(float3 N, float3 V, float3 L, float smoothness, float3 lightColor, float intensity)
            {
                float3 H = normalize(L + V);
                float vlLen = length(L + V);
                if (vlLen < 1e-3) return float3(0.0, 0.0, 0.0);
                float NdotL = max(dot(N, L), 1e-4);
                float NdotV = max(dot(N, V), 1e-4);
                float NdotH = max(dot(N, H), 0.0);
                float HdotV = max(dot(H, V), 1e-4);

                float roughness = clamp(1.0 - smoothness, 0.06, 1.0);
                float a = roughness * roughness;
                float a2 = a * a;

                float d = NdotH * NdotH * (a2 - 1.0) + 1.0;
                float D = a2 / max(PI * d * d, 1e-7);

                float Gv = NdotV / (NdotV * (1.0 - a) + a);
                float Gl = NdotL / (NdotL * (1.0 - a) + a);
                float G = Gv * Gl;

                float F0 = 0.02;
                float F = F0 + (1.0 - F0) * pow(1.0 - HdotV, 5.0);

                float spec = (D * G * F) / max(4.0 * NdotV * NdotL, 1e-4);

                return spec * lightColor * intensity;
            }

            Varyings WaterVertex(Attributes IN)
            {
                Varyings OUT;
                float3 positionOS = IN.positionOS.xyz;
                float3 normalOS = IN.normalOS;
                VertexNormalInputs normalInput = GetVertexNormalInputs(normalOS, IN.tangentOS);
                OUT.worldPos = mul(UNITY_MATRIX_M, float4(positionOS, 1.0));
                OUT.positionCS = TransformWorldToHClip(OUT.worldPos.xyz);
                OUT.normalWS = normalInput.normalWS;
                OUT.tangentWS = normalInput.tangentWS;
                OUT.bitangentWS = normalInput.bitangentWS;
                float2 uvPan = _Time.y * _CausticsSpeed * 0.01;
                OUT.uv.xy = TRANSFORM_TEX(IN.uv, _NormalMap);
                OUT.uv.zw = TRANSFORM_TEX(IN.uv, _FoamTex) * 2.0 + uvPan;
                OUT.screenPos = ComputeScreenPos(OUT.positionCS);
                OUT.viewDirWS = GetWorldSpaceNormalizeViewDir(OUT.worldPos.xyz);
                return OUT;
            }

            float4 WaterFragment(Varyings IN) : SV_Target
            {
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                float sceneRawDepth = SampleSceneDepth(screenUV);

                float waterEyeDepth = -mul(UNITY_MATRIX_V, float4(IN.worldPos.xyz, 1.0)).z;
                float sceneEye;
                float3 sceneWorldPos = ReconstructSceneWorldPos(screenUV, sceneRawDepth, IN.worldPos.xyz, waterEyeDepth, sceneEye);
                float hWorld = sceneWorldPos.y;

                float clipLine = _WaterSurfaceHeight + _WaterClipThreshold;

#if _DEBUG_CLIP
                if (sceneRawDepth >= 0.99999 || sceneRawDepth <= 0.00001)
                    return float4(0.0, 0.6, 0.8, 1.0);
                if (hWorld > clipLine)
                    return float4(1.0, 0.0, 0.0, 1.0);
                float dbgD = saturate((clipLine - hWorld) / max(_ShorelineDepthFade, 0.001));
                return float4(0.0, dbgD, 0.0, 1.0);
#else
                if (sceneRawDepth >= 0.99999 || sceneRawDepth <= 0.00001)
                {
                    hWorld = -10000.0;
                }
                else if (hWorld > clipLine)
                {
                    return float4(1, 1, 1, 1);
                }
#endif

                float pathLen = max(sceneEye - waterEyeDepth, 0.0);

                float surfaceDepth = max(_WaterSurfaceHeight - hWorld, 0.0);
                float surfaceDepthMask = saturate(surfaceDepth / max(_ShorelineDepthFade, 0.001));

                float3 viewDirWS = normalize(_WorldSpaceCameraPos - IN.worldPos.xyz);
                float3 normalWS = normalize(IN.normalWS);

                float3 finalNormal = normalWS;
                float3 litNormal = normalWS;
                float3 specNormal = litNormal;
                float3 glitterNormal = litNormal;
                float2 flowXY = float2(0.0, 0.0);
                float2 foamFlowXY = float2(0.0, 0.0);
if (_GerstnerWave > 0.5) {
                float3 gNAcc = float3(0.0, 0.0, 0.0);
                float2 gFlow = float2(0.0, 0.0);
                float gt = _Time.y;
                float animSpeed = _WaveSpeed;
                float2 dirA = Rot2(_A_Direction.xy, gt * _WaveDirSpeed *  1.0 * animSpeed);
                float2 dirB = Rot2(_B_Direction.xy, gt * _WaveDirSpeed * -0.6 * animSpeed);
                float2 dirC = Rot2(_C_Direction.xy, gt * _WaveDirSpeed *  1.7 * animSpeed);
                float2 dirD = Rot2(_D_Direction.xy, gt * _WaveDirSpeed * -1.3 * animSpeed);
                float fA = _A_Frequency * _WaveFrequency * (1.0 + _WaveFreqMod * sin(gt * _WaveFreqSpeed *  1.0 * animSpeed));
                float fB = _B_Frequency * _WaveFrequency * (1.0 + _WaveFreqMod * sin(gt * _WaveFreqSpeed * -0.7 * animSpeed));
                float fC = _C_Frequency * _WaveFrequency * (1.0 + _WaveFreqMod * sin(gt * _WaveFreqSpeed *  1.5 * animSpeed));
                float fD = _D_Frequency * _WaveFrequency * (1.0 + _WaveFreqMod * sin(gt * _WaveFreqSpeed * -1.1 * animSpeed));
                GerstnerWave(IN.worldPos.xz, gt, dirA, fA, _A_Amplitude * _WaveAmplitude, _WaveSteepness, gNAcc, gFlow);
                GerstnerWave(IN.worldPos.xz, gt, dirB, fB, _B_Amplitude * _WaveAmplitude, _WaveSteepness, gNAcc, gFlow);
                GerstnerWave(IN.worldPos.xz, gt, dirC, fC, _C_Amplitude * _WaveAmplitude, _WaveSteepness, gNAcc, gFlow);
                GerstnerWave(IN.worldPos.xz, gt, dirD, fD, _D_Amplitude * _WaveAmplitude, _WaveSteepness, gNAcc, gFlow);
                finalNormal = normalize(float3(gNAcc.x, 1.0 + gNAcc.y, gNAcc.z));
                flowXY = gFlow;
                foamFlowXY = gFlow;
}

                float3 normalWS_fresnel = normalize(litNormal);
                float NdotV = saturate(dot(normalWS_fresnel, viewDirWS));
                float fresnel = _FresnelBias + (1.0 - _FresnelBias) * pow(1.0 - NdotV, _FresnelPower);

if (_NormalPerturb > 0.5) {
                float2 worldUV = IN.worldPos.xz / _NormalWorldScale;
                float nt = _Time.y;
                float2 nuvA = worldUV + float2(nt * 0.008, nt * 0.003);
                float2 nuvB = worldUV + float2(nt * -0.005, nt * 0.006);
                float3 nA = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, nuvA));
                float2 nuvB_rot = float2(-nuvB.y, nuvB.x);
                float3 nB_raw = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, nuvB_rot));
                float2 nB = float2(-nB_raw.y, nB_raw.x);
                float2 detailFlow = (nA.xy + nB) * _NormalStrength;
                float3 normalDetail = normalize(float3(detailFlow.x, sqrt(max(0.0, 1.0 - dot(detailFlow, detailFlow) * 0.25)), detailFlow.y));
                litNormal = BlendNormalsWhiteout(litNormal, normalDetail * _NormalBlend);
                flowXY += detailFlow;

                float2 fineUV = IN.worldPos.xz / max(_NormalWorldScale * 0.2, 0.05);
                float2 fnuvA = fineUV + float2(nt * 0.021, nt * 0.014);
                float2 fnuvB = fineUV + float2(nt * -0.017, nt * 0.023);
                float3 fnA = UnpackNormal(SAMPLE_TEXTURE2D_LOD(_NormalMap, sampler_NormalMap, fnuvA, 0));
                float2 fnuvB_rot = float2(-fnuvB.y, fnuvB.x);
                float3 fnB_raw = UnpackNormal(SAMPLE_TEXTURE2D_LOD(_NormalMap, sampler_NormalMap, fnuvB_rot, 0));
                float2 fnB = float2(-fnB_raw.y, fnB_raw.x);
                float2 fineFlow = (fnA.xy + fnB) * _NormalStrength * 1.8;
                float3 fineNormal = normalize(float3(fineFlow.x, sqrt(max(0.0, 1.0 - dot(fineFlow, fineFlow) * 0.5)), fineFlow.y));
                specNormal = BlendNormalsWhiteout(litNormal, fineNormal * _NormalBlend);
                glitterNormal = fineNormal;
}

if (_MicroNormal > 0.5) {
                float3 normalDeriv = abs(ddx(litNormal)) + abs(ddy(litNormal));
                float3 microNormal = normalize(litNormal + normalDeriv * 0.08);
                litNormal = normalize(lerp(litNormal, microNormal, 0.3));
                float3 normalDerivG = abs(ddx(finalNormal)) + abs(ddy(finalNormal));
                float3 microNormalG = normalize(finalNormal + normalDerivG * 0.08);
                finalNormal = normalize(lerp(finalNormal, microNormalG, 0.3));
}

                float shoreWaveFoam = 0.0;
                float2 shoreWaveNormal = float2(0.0, 0.0);
                float shoreFoamMask = 1.0;
if (_ShoreWave > 0.5) {
                float elevCoord = max(surfaceDepth, 0.0);
                float2 px = _ScreenParams.zw * 2.0;
                float hC = hWorld;
                float hR = SampleSceneHeight(screenUV + float2(px.x, 0), IN.worldPos.xyz, waterEyeDepth);
                float hL = SampleSceneHeight(screenUV - float2(px.x, 0), IN.worldPos.xyz, waterEyeDepth);
                float hU = SampleSceneHeight(screenUV + float2(0, px.y), IN.worldPos.xyz, waterEyeDepth);
                float hD = SampleSceneHeight(screenUV - float2(0, px.y), IN.worldPos.xyz, waterEyeDepth);
                float distX = (hR - hL) * 0.5;
                float distZ = (hU - hD) * 0.5;
                float slope = length(float2(distX, distZ)) / max(2.0 * length(px) * sceneEye * 0.001 + 1e-3, 1e-3);
                float horizDist = elevCoord / max(slope, 1e-3);
                float distCoord = lerp(elevCoord, horizDist, 0.5);
                float shoreCoord = lerp(elevCoord, distCoord, _ShoreWaveMix);
                float slopeNorm = saturate(slope / max(_ShoreWaveSlopeRef, 1e-3));
                float effStart = _ShoreWaveStart + _ShoreWaveSlopeReach * (1.0 - slopeNorm);
                ComputeShoreAdvancingWave(shoreCoord, _Time.y, effStart, shoreWaveFoam, shoreWaveNormal);
                float2 sfUV = IN.worldPos.xz / _ShoreWaveFoamTexTiling;
                float sfT = _Time.y * _ShoreWaveFoamMaskSpeed;
                float2 sfUV1 = sfUV * float2(1.0, 0.3) + float2(sfT * 0.6, sfT * 0.2);
                float2 sfUV2 = sfUV * float2(-0.7, 1.0) - float2(sfT * 0.4, sfT * 0.7);
                float sfA = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, sfUV1).r;
                float sfB = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, sfUV2).r;
                float fm = (sfA + sfB) * 0.5;
                shoreFoamMask = lerp(_ShoreWaveFoamMaskFloor, 1.0, pow(smoothstep(0.0, 0.8, fm), _ShoreWaveFoamMaskPower));
                shoreWaveFoam *= shoreFoamMask;
                shoreWaveNormal *= shoreFoamMask;
                shoreWaveFoam *= saturate(surfaceDepthMask * 8.0);
                shoreWaveNormal *= saturate(surfaceDepthMask * 8.0);
if (_ShoreWaveNormal > 0.5) {
                float3 shoreWaveNormalWS = normalize(float3(shoreWaveNormal.x, 1.0, shoreWaveNormal.y));
                litNormal = BlendNormalsWhiteout(litNormal, shoreWaveNormalWS);
                specNormal = BlendNormalsWhiteout(specNormal, shoreWaveNormalWS);
                NdotV = saturate(dot(normalize(litNormal), viewDirWS));
                fresnel = _FresnelBias + (1.0 - _FresnelBias) * pow(1.0 - NdotV, _FresnelPower);
}
}

                float foam = 0.0;
if (_Foam > 0.5) {
                float minVal = 0.8;
                float maxVal = 0.9;
                float timeCycle = fmod(_Time.y * _FoamPulseSpeed, 10.0);
                float timePulse = minVal + (sin(timeCycle * 0.6283) * 0.5 + 0.5) * (maxVal - minVal);
                float2 foamWorldUV = IN.worldPos.xz * _FoamTex_ST.xy;
                float2 timeSinCos = float2(sin(timeCycle * 0.6283), cos(timeCycle * 0.6283));
                float baseScale = 1.0 + (timePulse - minVal) * _FoamScalePulse;
                float scale1 = baseScale + sin(timeCycle * 1.5) * 0.05;
                float scale2 = baseScale + cos(timeCycle * 1.7) * 0.05;
                float scale3 = baseScale + sin(timeCycle * 1.9) * 0.05;
                float scale4 = baseScale + cos(timeCycle * 2.1) * 0.05;
                float2 foamFlow = foamFlowXY * 0.15;
                float2 foamUV1 = (foamWorldUV - 0.5) * scale1 + 0.5 + foamFlow * 0.5 + timeSinCos * 0.08;
                float2 foamUV2 = (foamWorldUV - 0.5) * scale2 + 0.5 + foamFlow * 0.5 + timeSinCos.yx * -0.08;
                float2 foamUV3 = (foamWorldUV - 0.5) * scale3 + 0.5 + foamFlow * 0.5 + timeSinCos.xy * -0.08;
                float2 foamUV4 = (foamWorldUV - 0.5) * scale4 + 0.5 + foamFlow * 0.5 + timeSinCos.yx * 0.08;
                float foamNoise1 = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, foamUV1).r;
                float foamNoise2 = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, foamUV2).r;
                float foamNoise3 = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, foamUV3).r;
                float foamNoise4 = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, foamUV4).r;

                float edge = saturate(surfaceDepthMask / max(0.001, _FoamDepthThreshold));
                float shoreMaskDepth = saturate(1.0 - surfaceDepthMask / max(_FoamShorelineBoost, 0.001));
                float shoreMask = pow(shoreMaskDepth, _ShorelineAlphaFalloff);
                float foamShoreFade = smoothstep(0.0, 0.05, surfaceDepthMask);
                float foamMask = shoreMask * foamShoreFade;

                float waveSteepness = saturate(1.0 - finalNormal.y);
                float crestMask = pow(waveSteepness, _WaveFoamThreshold) * _WaveFoamIntensity;

                float noiseAverage = (foamNoise1 + foamNoise2 + foamNoise3 + foamNoise4) * 0.25;
                float shorelineFoam = foamMask * noiseAverage * _FoamIntensity * timePulse;
                float crestFoam = crestMask * noiseAverage * timePulse;
                foam = saturate(shorelineFoam + crestFoam);
                foam = saturate(foam + shoreWaveFoam * noiseAverage * timePulse);
if (_ShoreWave > 0.5) {
                float deepWeight = saturate((surfaceDepth - _DeepFoamStart) / max(_DeepFoamFade, 0.001));
                float deepFoam = shoreFoamMask * noiseAverage * timePulse * _DeepFoamIntensity * deepWeight;
                foam = saturate(foam + deepFoam);
}
}

                float caustic = 0.0;
if (_Caustics > 0.5) {
                float time = _Time.y;
                float2 causticsWorldUV = IN.worldPos.xz * _CausticsTex_ST.xy;
                float2 uv1 = causticsWorldUV + float2(time * _CausticsSpeed * 0.008, time * _CausticsSpeed * 0.006);
                float2 uv2 = causticsWorldUV + float2(time * _CausticsSpeed * -0.006, time * _CausticsSpeed * 0.01);
                float caustic1 = SAMPLE_TEXTURE2D(_CausticsTex, sampler_CausticsTex, uv1).r;
                float caustic2 = SAMPLE_TEXTURE2D(_CausticsTex, sampler_CausticsTex, uv2).r;
                caustic = (caustic1 * caustic2) * _CausticsStrength;
                caustic *= saturate(1.0 - pathLen / max(_WaterSurfaceHeight, 0.001));
}

                Light mainLight = GetMainLight();
                float3 lightDirWS = normalize(mainLight.direction);
                float NdotL = saturate(dot(litNormal, lightDirWS));

                float3 envN = float3(0.0, 1.0, 0.0);
                float3 glitterN = normalize(lerp(envN, glitterNormal, _SunGlitterSparkle));
                float3 specColor = ComputeWaterWorksSpecular(glitterN, viewDirWS, lightDirWS, 1.0 - _SunGlitterRoughness, mainLight.color, _SpecularIntensity) * _SunGlitterStrength;

                float3 reflectDir = reflect(-viewDirWS, litNormal);
                float3 reflectionColor = 0;

if (_PlanarReflection > 0.5) {
                float3 p0 = IN.worldPos.xyz;
                float3 pp = _OptiWaterPlanePosWS;
                float3 np = normalize(_OptiWaterPlaneNormalWS);
                float denom = dot(reflectDir, np);
                float3 hit = p0;
                if (abs(denom) > 1e-4)
                {
                    float t = dot(pp - p0, np) / denom;
                    if (t > 0.0) hit = p0 + reflectDir * t;
                }
                float4 clip = mul(_OptiWaterMirrorVP, float4(hit, 1.0));
                float2 planarUV = clip.xy / max(clip.w, 1e-5);
                planarUV = planarUV * 0.5 + 0.5;
                float2 planarDistort = litNormal.xz * _DistortionStrength;
                planarUV += planarDistort;
                reflectionColor = SAMPLE_TEXTURE2D(_ReflectionTex, sampler_ReflectionTex, planarUV).rgb;
}
                reflectionColor *= (_ReflectionIntensity * fresnel);

                float waterDepthMeters = max(0.0, _WaterSurfaceHeight - hWorld);
                float depthFactor = saturate(pathLen / max(_ShallowDeepBlendDepth, 0.001));
                float3 waterBaseColor = lerp(_WaterColor.rgb, _DeepWaterColor.rgb, depthFactor);

                float depthT_depth = saturate((pathLen - _AlphaClearDepth) / max(_AlphaFullDepth - _AlphaClearDepth, 0.001));
                float2 upx = _ScreenParams.zw * 2.0;
                float sR = SampleSceneHeight(screenUV + float2(upx.x, 0), IN.worldPos.xyz, waterEyeDepth);
                float sL = SampleSceneHeight(screenUV - float2(upx.x, 0), IN.worldPos.xyz, waterEyeDepth);
                float sU = SampleSceneHeight(screenUV + float2(0, upx.y), IN.worldPos.xyz, waterEyeDepth);
                float sD = SampleSceneHeight(screenUV - float2(0, upx.y), IN.worldPos.xyz, waterEyeDepth);
                float edgeSlopeX = (sR - sL) / (2.0 * max(2.0 * upx.x * sceneEye * 0.001, 1e-3));
                float edgeSlopeZ = (sU - sD) / (2.0 * max(2.0 * upx.y * sceneEye * 0.001, 1e-3));
                float edgeSlope = length(float2(edgeSlopeX, edgeSlopeZ));
                float horizDist2 = waterDepthMeters / max(edgeSlope, 1e-3);
                float depthT_horiz = (_AlphaEdgeWidth > 1e-4) ? saturate(horizDist2 / _AlphaEdgeWidth) : 1.0;
                float deepFade = saturate((waterDepthMeters - _AlphaEdgeMaxDepth) / max(_AlphaEdgeMaxDepth, 0.001));
                depthT_horiz = lerp(depthT_horiz, 1.0, deepFade);
                float depthT = min(depthT_depth, max(depthT_horiz, saturate(pathLen / max(_WaterSurfaceHeight, 0.001))));
                float depthAlpha = pow(depthT, _AlphaFalloffPower);
                float aaWidth = max(fwidth(depthT), 1e-5);
                depthAlpha *= smoothstep(0.0, aaWidth, depthT);
                float alpha = depthAlpha * _WaterColor.a;

                float3 ambientSH = SampleSH(litNormal);
                float3 diffuseColor = waterBaseColor * (ambientSH + mainLight.color * (0.35 + 0.65 * NdotL));
                diffuseColor += caustic * float3(1.0, 0.8, 0.4);
                float3 finalColor = diffuseColor + reflectionColor + specColor;

if (_CrestGlow > 0.5) {
                float crestFromWave = saturate((_CrestGlowThreshold - finalNormal.y) / max(1.0 - _CrestGlowThreshold, 0.001));
                float crestFromDetail = saturate((_CrestGlowThreshold - specNormal.y) / max(1.0 - _CrestGlowThreshold, 0.001));
                float crestGlowT = max(crestFromWave, crestFromDetail);
                float crestGlow = pow(crestGlowT, _CrestGlowPower) * _CrestGlowIntensity;
                finalColor += _CrestGlowColor.rgb * crestGlow;
}

                finalColor = lerp(finalColor, float3(1, 1, 1), foam);

if (_BottomDistort > 0.5) {
                float bottomWeight = saturate(1.0 - surfaceDepth / max(_BottomDistortDepth, 0.001));
                bottomWeight *= (1.0 - depthT_horiz);
                bottomWeight = saturate(bottomWeight);
                if (bottomWeight > 0.001)
                {
                    float2 bnOffset = litNormal.xz * _BottomDistortStrength;
                    float bt = _Time.y * _BottomDistortSpeed;
                    bnOffset += float2(
                        sin(IN.worldPos.x * 0.7 + bt) + sin(IN.worldPos.z * 0.5 - bt * 0.8),
                        cos(IN.worldPos.z * 0.6 + bt * 1.1) + cos(IN.worldPos.x * 0.4 + bt * 0.9)
                    ) * 0.5 * _BottomDistortStrength;
                    float2 distortedUV = saturate(screenUV + bnOffset * bottomWeight);
                    float3 bottomColor = SampleSceneColor(distortedUV);
                    bottomColor = lerp(bottomColor, bottomColor * _WaterColor.rgb * 1.6, _BottomDistortTint);
                    finalColor = lerp(finalColor, bottomColor, bottomWeight * (1.0 - foam));
                    alpha = max(alpha, bottomWeight);
                }
}

#if _DEBUG_SHOREMASK
                return float4(shoreFoamMask, shoreFoamMask, shoreFoamMask, 1.0);
#endif
                return float4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    CustomEditor "Smartomano.OptiWater.Editor.OptiWaterShaderGUI"
}
