Shader "Universal Render Pipeline/DayNight/RealisticClouds"
{
    Properties
    {
        _MainTex ("Cloud Detail Texture", 2D) = "white" {}
        _CloudColor ("Cloud Sunlit Color", Color) = (1.0, 1.0, 1.0, 0.95)
        _CloudShadowColor ("Cloud Ambient Shadow Color", Color) = (0.38, 0.42, 0.52, 1.0)
        _SunHighlightColor ("Sun Silver Lining Color", Color) = (1.0, 0.88, 0.65, 1.0)
        
        _CloudCoverage ("Cloud Coverage", Range(0.1, 0.9)) = 0.50
        _CloudDensity ("Cloud Density", Range(0.5, 3.5)) = 1.3
        _CloudSoftness ("Cloud Edge Softness", Range(0.05, 0.5)) = 0.28
        
        _WindSpeedX ("Wind Speed X", Float) = 0.003
        _WindSpeedY ("Wind Speed Y", Float) = 0.0015
        _Tiling ("Cloud Scale", Float) = 1.4
        
        _SilverLiningPower ("Silver Lining Power", Range(1.0, 16.0)) = 4.0
        _SilverLiningIntensity ("Silver Lining Intensity", Range(0.0, 4.0)) = 1.8
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

            // High-quality hash for cellular & gradient noise
            float2 Hash2D(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            // Worley cellular noise (forms rounded cumulus billow volume inside main cloud banks)
            float WorleyNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float minDist = 1.0;

                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 neighbor = float2(float(x), float(y));
                        float2 pointPos = Hash2D(i + neighbor);
                        float2 diff = neighbor + pointPos - f;
                        float dist = length(diff);
                        minDist = min(minDist, dist);
                    }
                }
                return 1.0 - minDist;
            }

            // Perlin gradient noise (forms grand, continuous, wispy atmospheric cloud banks)
            float PerlinNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

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

                // High-frequency detail from noise texture
                float texNoise = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv * 2.5).r;

                // Domain warping simulates natural aerodynamic airflow curls
                float2 warp = float2(
                    PerlinNoise(uv * 0.95 + float2(0.0, 0.0)),
                    PerlinNoise(uv * 0.95 + float2(4.3, 2.7))
                ) * 0.35;

                // Grand continuous atmospheric cloud mass (Perlin FBM)
                // Produces vast, cohesive, connected cloud banks across the celestial dome
                float macro1 = PerlinNoise((uv + warp) * 0.75);
                float macro2 = PerlinNoise((uv + warp) * 1.5 + float2(1.7, 3.2)) * 0.5;
                float macro3 = PerlinNoise((uv + warp) * 3.0 + float2(2.5, 1.4)) * 0.25;
                float macroFBM = (macro1 + macro2 + macro3) / 1.75;

                // Threshold based on coverage
                float threshold = 1.0 - _CloudCoverage;

                // STRICT FILTER: Eradicate all small isolated clouds!
                // If macro density is below threshold, clear blue sky - zero floating puffs!
                if (macroFBM < threshold - _CloudSoftness)
                    discard;

                // How deep inside the continuous cloud formation are we?
                // Outside the cloud banks, bankDepth is 0. Inside, it approaches 1.
                float bankDepth = saturate((macroFBM - (threshold - _CloudSoftness * 0.5)) / (_CloudSoftness * 1.5));

                // Worley cellular noise ONLY carves billows inside the main cloud banks
                // Because bankDepth is 0 in clear sky, Worley noise can NEVER create small isolated puffs!
                float billow1 = WorleyNoise((uv + warp) * 1.4);
                float billow2 = WorleyNoise((uv + warp) * 2.8 + float2(3.1, 1.4)) * 0.5;
                float billowFBM = (billow1 + billow2) / 1.5;

                // Fluffy internal cumulus billow volume inside the main cloud body
                float cloudDensity = macroFBM - (1.0 - billowFBM) * (0.18 * bankDepth);

                // Wispy edge modulation from texture
                cloudDensity += (texNoise - 0.5) * (0.10 * bankDepth);

                // Silky smooth alpha transition at cloud perimeter
                float alpha = smoothstep(threshold - _CloudSoftness * 0.5, threshold + _CloudSoftness, cloudDensity);
                alpha = saturate(pow(alpha, 1.25) * _CloudDensity);

                if (alpha < 0.005)
                    discard;

                // Directional Light Shading (Sunlit cloud tops vs Ambient shaded undersides)
                float lightGradient = smoothstep(threshold - 0.02, threshold + _CloudSoftness * 1.4, cloudDensity);
                half3 shadedCloud = lerp(_CloudShadowColor.rgb, _CloudColor.rgb, lightGradient);

                // Mie forward scattering silver lining around Sun / Moon
                float3 sunDir = normalize(_SunDirection);
                float sunDotView = saturate(dot(input.viewDir, sunDir));
                float silverLining = pow(sunDotView, _SilverLiningPower) * _SilverLiningIntensity;

                // Silver lining is most radiant on the soft, translucent edges of the clouds
                float rimGlow = (1.0 - lightGradient) * alpha;
                half3 finalRGB = shadedCloud + _SunHighlightColor.rgb * (silverLining * rimGlow);

                // Seamless Horizon Haze Fade: Clouds gently dissolve into atmospheric distance haze
                // With planar UV mapping, (0.5, 0.5) is directly overhead at zenith
                float distFromZenith = length(input.uv - float2(0.5, 0.5)) * 2.0;
                float horizonHaze = saturate((1.0 - distFromZenith) * 3.5);

                return half4(finalRGB, alpha * horizonHaze * _CloudColor.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}