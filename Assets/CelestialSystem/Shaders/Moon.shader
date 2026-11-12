Shader "CelestialSystem/Moon"
{
    Properties
    {
        [Header(Textures)]
        _BaseMap ("Moon Albedo Map", 2D) = "white" {}
        [Normal] _BumpMap ("Moon Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Range(0.0, 2.0)) = 1.0

        [Header(Color and Lighting)]
        _MoonColor ("Moon Daylight/Zenith Color", Color) = (0.94, 0.96, 1.0, 1.0)
        _HorizonMoonColor ("Moon Horizon Color (Illusion)", Color) = (1.0, 0.70, 0.38, 1.0)
        _Roughness ("Oren-Nayar Roughness", Range(0.0, 1.0)) = 0.85
        _EmissionMultiplier ("Lit Emission Brightness", Range(0.2, 5.0)) = 1.0

        [Header(Phase and Earthshine)]
        _TerminatorSoftness ("Terminator Softness", Range(0.01, 0.5)) = 0.12
        _EarthshineColor ("Earthshine Color (Dark Side)", Color) = (0.10, 0.14, 0.25, 1.0)
        _EarthshineIntensity ("Earthshine Intensity", Range(0.0, 0.8)) = 0.15

        [Header(Atmospheric Halo and Bloom)]
        [HDR] _HaloColor ("Atmospheric Lunar Halo Color", Color) = (0.78, 0.88, 1.0, 1.0)
        _HaloIntensity ("Halo Brightness", Range(0.0, 5.0)) = 0.6
        _HaloSize ("Halo Radius Falloff", Range(0.05, 1.0)) = 0.25

        [Header(Horizon Dynamics)]
        _HorizonExpansion ("Horizon Size Expansion Factor", Range(1.0, 2.0)) = 1.35

        [Header(Driven By Scripts)]
        _SunDirection ("Sun World Direction", Vector) = (0.0, 1.0, 0.0, 0.0)
        _MoonAltitude ("Moon Altitude Deg", Float) = 45.0
        _PhaseAngle ("Moon Phase Angle Deg", Float) = 180.0
        _Visibility ("Visibility (CelestialVolume)", Range(0.0, 1.0)) = 1.0

        [Header(Stencil)]
        _StencilRef ("Stencil Ref", Int) = 0
        _StencilComp ("Stencil Comp", Int) = 8 /* Always */
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+490"
            "RenderPipeline" = "UniversalPipeline"
        }

        Stencil
        {
            Ref [_StencilRef]
            Comp [_StencilComp]
            Pass Keep
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Back
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "MoonForward"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 worldPos   : TEXCOORD1;
                float3 worldNormal: NORMAL;
                float3 worldTangent: TANGENT;
                float3 worldBinormal: TEXCOORD3;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _MoonColor;
                half4 _HorizonMoonColor;
                half4 _EarthshineColor;
                half4 _HaloColor;
                float4 _SunDirection;
                float _BumpScale;
                float _Roughness;
                float _EmissionMultiplier;
                float _TerminatorSoftness;
                float _EarthshineIntensity;
                float _HaloIntensity;
                float _HaloSize;
                float _HorizonExpansion;
                float _MoonAltitude;
                float _PhaseAngle;
                float _Visibility;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.worldPos   = TransformObjectToWorld(input.positionOS.xyz);
                output.uv         = TRANSFORM_TEX(input.uv, _BaseMap);

                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.worldNormal   = normalInput.normalWS;
                output.worldTangent  = normalInput.tangentWS;
                output.worldBinormal = normalInput.bitangentWS;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Visibility and horizon cutoff
                float horizonFade = saturate((_MoonAltitude + 3.0) / 3.0);
                float totalVis = _Visibility * horizonFade;
                if (totalVis <= 0.0001)
                    discard;

                // ── 1. Surface Normals & Tangent Space ──
                float3 geomNormal = normalize(input.worldNormal);
                half4 normalSample = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv);
                half3 unpackedNormal = UnpackNormalScale(normalSample, _BumpScale);

                float3x3 tbn = float3x3(
                    normalize(input.worldTangent),
                    normalize(input.worldBinormal),
                    geomNormal
                );
                float3 N = normalize(mul(unpackedNormal, tbn));

                // ── 2. View Direction & Fresnel Rim ──
                float3 V = normalize(_WorldSpaceCameraPos - input.worldPos);
                float NdotV = saturate(dot(N, V));

                // ── 3. Astronomical Solar Illumination (Phase) ──
                // The Sun shines on the Moon from _SunDirection
                float3 L = normalize(_SunDirection.xyz);
                float NdotL = dot(N, L);

                // Soft terminator transition between lit and unlit hemispheres
                float halfTerm = max(0.005, _TerminatorSoftness * 0.5);
                float litFactor = smoothstep(-halfTerm, halfTerm, NdotL);

                // Lunar photometric roughness (Oren-Nayar style retro-reflection / opposition surge)
                // The moon's regolith reflects light back towards the sun with flat limb behavior
                float cosAngle = dot(V, L);
                float retroSurge = saturate(cosAngle);
                float retroBoost = 1.0 + pow(retroSurge, 6.0) * 0.45;

                // ── 4. Albedo & Color Shift ──
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

                // Horizon reddening & moon illusion color shift
                float horizonFactor = saturate((16.0 - _MoonAltitude) / 16.0);
                half3 surfaceColor = lerp(_MoonColor.rgb, _HorizonMoonColor.rgb, horizonFactor);

                // ── 5. Earthshine on Dark Side ──
                // Earthshine is strongest during crescent phases (phase angle near 0° to 90° or 270° to 360°)
                // when the Earth as seen from the moon is nearly "full"
                float radPhase = _PhaseAngle * (PI / 180.0);
                float earthIllumFraction = (1.0 + cos(radPhase)) * 0.5; // Inverse of moon phase!
                float darkSideFactor = 1.0 - litFactor;
                half3 earthshine = _EarthshineColor.rgb * (_EarthshineIntensity * earthIllumFraction * darkSideFactor);

                // ── 6. Combine Surface Lighting ──
                half3 litColor = surfaceColor * (albedo.rgb * (NdotL * litFactor * retroBoost * _EmissionMultiplier));
                half3 darkColor = albedo.rgb * earthshine;
                half3 finalSurface = litColor + darkColor;

                // ── 7. Atmospheric Lunar Halo & Limb Glow ──
                float limbGlow = pow(1.0 - NdotV, 3.5) * _HaloIntensity;
                half3 haloEmission = _HaloColor.rgb * (limbGlow * (litFactor * 0.85 + 0.15));

                half3 finalColor = (finalSurface + haloEmission) * totalVis;

                return half4(finalColor, totalVis);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

