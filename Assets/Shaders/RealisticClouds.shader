Shader "Universal Render Pipeline/DayNight/RealisticClouds"
{
    Properties
    {
        _MainTex ("Cloud Detail Texture", 2D) = "white" {}
        _CloudColor ("Cloud Sunlit Color", Color) = (1.0, 1.0, 1.0, 0.95)
        _CloudShadowColor ("Cloud Ambient Shadow Color", Color) = (0.42, 0.46, 0.58, 1.0)
        _SunHighlightColor ("Sun Silver Lining Color", Color) = (1.0, 0.90, 0.70, 1.0)
        
        _CloudCoverage ("Cloud Coverage", Range(0.1, 0.9)) = 0.52
        _CloudDensity ("Cloud Density", Range(0.5, 3.5)) = 1.35
        _CloudSoftness ("Cloud Edge Softness", Range(0.05, 0.6)) = 0.35
        
        _WindSpeedX ("Wind Speed X", Float) = 0.002
        _WindSpeedY ("Wind Speed Y", Float) = 0.001
        _Tiling ("Cloud Scale", Float) = 0.65
        
        _SilverLiningPower ("Silver Lining Power", Range(1.0, 24.0)) = 6.0
        _SilverLiningIntensity ("Silver Lining Intensity", Range(0.0, 5.0)) = 2.2
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent-20"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "RealisticCloudsPass"
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 viewDir : TEXCOORD3;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _CloudColor;
                float4 _CloudShadowColor;
                float4 _SunHighlightColor;
                float _CloudCoverage;
                float _CloudDensity;
                float _CloudSoftness;
                float _WindSpeedX;
                float _WindSpeedY;
                float _Tiling;
                float _SilverLiningPower;
                float _SilverLiningIntensity;
                float3 _SunDirection;
            CBUFFER_END

            // Smooth hash for multi-octave gradient noise
            float2 Hash2D(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            // High-quality smooth Perlin gradient noise
            float PerlinNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                // Quintic hermite curve for silky smooth C2 continuity
                float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);

                float2 g00 = Hash2D(i) * 2.0 - 1.0;
                float2 g10 = Hash2D(i + float2(1, 0)) * 2.0 - 1.0;
                float2 g01 = Hash2D(i + float2(0, 1)) * 2.0 - 1.0;
                float2 g11 = Hash2D(i + float2(1, 1)) * 2.0 - 1.0;

                float n00 = dot(g00, f);
                float n10 = dot(g10, f - float2(1, 0));
                float n01 = dot(g01, f - float2(0, 1));
                float n11 = dot(g11, f - float2(1, 1));

                return lerp(lerp(n00, n10, u.x), lerp(n01, n11, u.x), u.y) * 0.5 + 0.5;
            }

            // 4-Octave Fractional Brownian Motion (fBm)
            float CloudFBM(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.52;
                float2x2 rot = float2x2(0.80, 0.60, -0.60, 0.80);

                value += amplitude * PerlinNoise(p);
                p = mul(rot, p * 2.02);
                amplitude *= 0.48;

                value += amplitude * PerlinNoise(p);
                p = mul(rot, p * 2.04);
                amplitude *= 0.48;

                value += amplitude * PerlinNoise(p);
                p = mul(rot, p * 2.03);
                amplitude *= 0.48;

                value += amplitude * PerlinNoise(p);
                return value;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.worldPos = posInputs.positionWS;
                output.uv = input.uv;
                output.viewDir = normalize(posInputs.positionWS - _WorldSpaceCameraPos);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float time = _Time.y;
                float2 wind = float2(_WindSpeedX, _WindSpeedY) * time;
                float2 uv = input.uv * _Tiling + wind;

                // 1. Aerodynamic Domain Warping: Simulates natural atmospheric curl currents
                float2 warp = float2(
                    PerlinNoise(uv * 0.70 + float2(0.0, 0.0)),
                    PerlinNoise(uv * 0.70 + float2(4.7, 2.3))
                ) * 0.45;

                // 2. Continental Cloud Macro Formation:
                // Determines vast, cohesive cloud fronts crossing the sky.
                // Eradicates isolated small pancake clouds by requiring a grand continuous front!
                float macroFront = PerlinNoise((uv + warp * 0.5) * 0.55);
                float macroMask = smoothstep(0.36, 0.64, macroFront);

                // If macro bank is zero, clear open sky - zero small floating discs!
                if (macroMask < 0.01)
                    discard;

                // 3. Multi-Octave Volumetric Cloud Billows
                float billowFBM = CloudFBM(uv + warp);

                // High-frequency wispy edge detail from texture
                float texNoise = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, (uv + warp) * 3.5).r;

                // Combined Cloud Shape
                float cloudRaw = billowFBM + (texNoise - 0.5) * 0.12;
                cloudRaw *= macroMask;

                // 4. Threshold & Smooth Feathery Alpha
                float threshold = 1.0 - _CloudCoverage;
                float softness = _CloudSoftness;
                
                // Smooth feathery edge fade - never a sharp cookie-cutter cutoff!
                float density = smoothstep(threshold - softness * 0.6, threshold + softness * 1.4, cloudRaw);
                density = pow(density, 1.35) * _CloudDensity;

                if (density < 0.005)
                    discard;

                // 5. 3D Atmospheric Volumetric Shading
                // Direct Sunlit tops vs. Soft Ambient Skylight underside
                float lightDepth = smoothstep(threshold, threshold + softness * 1.8, cloudRaw);
                half3 ambientShade = _CloudShadowColor.rgb;
                half3 sunlitTop = _CloudColor.rgb;
                half3 shadedCloud = lerp(ambientShade, sunlitTop, lightDepth);

                // 6. Radiant Forward Scattering Silver Lining (Mie Scattering around Sun / Moon)
                float3 sunDir = normalize(_SunDirection);
                float3 viewDir = normalize(input.viewDir);
                float sunDotView = saturate(dot(viewDir, sunDir));
                
                // Strong forward scatter core + softer wide glow halo
                float silverLining = pow(sunDotView, _SilverLiningPower) * 1.8 + pow(sunDotView, 3.0) * 0.4;
                silverLining *= _SilverLiningIntensity;

                // Silver lining is radiant on the soft translucent wispy edges of the cloud
                float translucentRim = (1.0 - lightDepth) * saturate(density * 1.8);
                half3 finalRGB = shadedCloud + _SunHighlightColor.rgb * (silverLining * translucentRim);

                // 7. Atmospheric Horizon Haze Blend: Clouds naturally dissolve at the horizon
                float distFromZenith = length(input.uv - float2(0.5, 0.5)) * 2.0;
                float horizonHaze = saturate((1.0 - distFromZenith) * 3.2);
                horizonHaze = smoothstep(0.0, 1.0, horizonHaze);

                float finalAlpha = saturate(density * horizonHaze * _CloudColor.a);
                if (finalAlpha < 0.003)
                    discard;

                return half4(finalRGB, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
