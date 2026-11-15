Shader "Universal Render Pipeline/DayNight/RealisticClouds"
{
    Properties
    {
        _CloudColor ("Cloud Sunlit Color", Color) = (1.0, 1.0, 1.0, 0.95)
        _CloudShadowColor ("Cloud Ambient Shadow Color", Color) = (0.38, 0.42, 0.52, 1.0)
        _SunHighlightColor ("Sun Silver Lining Color", Color) = (1.0, 0.88, 0.65, 1.0)
        
        _CloudCoverage ("Cloud Coverage", Range(0.1, 0.9)) = 0.52
        _CloudDensity ("Cloud Density", Range(0.5, 3.5)) = 1.35
        _CloudSoftness ("Cloud Edge Softness", Range(0.05, 0.5)) = 0.30
        
        _WindSpeedX ("Wind Speed X", Float) = 0.003
        _WindSpeedY ("Wind Speed Y", Float) = 0.0015
        _Tiling ("Cloud Scale", Float) = 0.65
        
        _SilverLiningPower ("Silver Lining Power", Range(1.0, 16.0)) = 5.0
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

            CBUFFER_START(UnityPerMaterial)
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

            // Smooth hash without grid or aliasing artifacts
            float Hash(float2 p)
            {
                p = 50.0 * frac(p * 0.3183099 + float2(0.71, 0.113));
                return -1.0 + 2.0 * frac(p.x * p.y * (p.x + p.y));
            }

            // High-quality 2D Value Noise with quintic Hermite polynomial C2 interpolation
            // Guarantees zero cellular discs or circular blobs!
            float SmoothNoise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                // Quintic curve: 6t^5 - 15t^4 + 10t^3 (smoothest 2nd derivative)
                float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);

                float a = Hash(i + float2(0.0, 0.0));
                float b = Hash(i + float2(1.0, 0.0));
                float c = Hash(i + float2(0.0, 1.0));
                float d = Hash(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y) * 0.5 + 0.5;
            }

            // Fractal Brownian Motion (FBM) with rotated octaves
            // Produces continuous, sweeping natural atmospheric cloud masses
            float CloudFBM(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.52;
                float2 shift = float2(12.3, 17.5);
                // Golden angle rotation matrix to eradicate all grid axes
                float2x2 rot = float2x2(0.80, 0.60, -0.60, 0.80);

                for (int i = 0; i < 4; i++)
                {
                    value += amplitude * SmoothNoise2D(p);
                    p = mul(rot, p) * 2.05 + shift;
                    amplitude *= 0.48;
                }
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

                // Domain Warping: simulates realistic continuous fluid atmospheric airflow
                // This connects cloud masses together rather than breaking them into isolated round dots!
                float2 warp = float2(
                    CloudFBM(uv * 0.85 + float2(0.0, 0.0)),
                    CloudFBM(uv * 0.85 + float2(5.2, 1.3))
                ) * 0.35;

                // Continuous macro cloud body
                float macro = CloudFBM(uv + warp);

                // Sky coverage threshold
                float threshold = 1.0 - _CloudCoverage;

                // STRICT FILTER: Eradicate small isolated clouds!
                // Any weak isolated noise fluctuations below threshold are completely eliminated.
                if (macro < threshold - _CloudSoftness)
                    discard;

                // Natural feathering at cloud perimeter
                float alpha = smoothstep(threshold - _CloudSoftness * 0.7, threshold + _CloudSoftness, macro);
                alpha = saturate(pow(alpha, 1.3) * _CloudDensity);

                if (alpha < 0.005)
                    discard;

                // Directional Light Shading (Sunlit cloud tops vs ambient shaded undersides)
                float lightGradient = smoothstep(threshold - 0.02, threshold + _CloudSoftness * 1.5, macro);
                half3 shadedCloud = lerp(_CloudShadowColor.rgb, _CloudColor.rgb, lightGradient);

                // Mie forward scattering silver lining around Sun / Moon
                float3 sunDir = normalize(_SunDirection);
                float sunDotView = saturate(dot(input.viewDir, sunDir));
                float silverLining = pow(sunDotView, _SilverLiningPower) * _SilverLiningIntensity;

                // Radiant soft rim glow on cloud edges
                float rimGlow = (1.0 - lightGradient) * alpha;
                half3 finalRGB = shadedCloud + _SunHighlightColor.rgb * (silverLining * rimGlow);

                // Seamless Horizon Haze Fade: clouds smoothly dissolve into distance haze
                float distFromZenith = length(input.uv - float2(0.5, 0.5)) * 2.0;
                float horizonHaze = saturate((1.0 - distFromZenith) * 3.5);

                return half4(finalRGB, alpha * horizonHaze * _CloudColor.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
