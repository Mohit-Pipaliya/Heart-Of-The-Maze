Shader "CelestialSystem/Clouds"
{
    Properties
    {
        [Header(Textures and Scrolling)]
        _CloudTex ("Seamless Cloud Noise Texture", 2D) = "white" {}
        _Tiling ("Cloud Texture Tiling", Float) = 3.5
        _WindSpeed ("Wind Velocity", Range(0.0, 1.0)) = 0.03
        _WindDirection ("Wind Direction (X, Z)", Vector) = (1.0, 0.0, 0.4, 0.0)

        [Header(Cloud Morphology)]
        _Coverage ("Cloud Coverage (0 = Clear, 1 = Overcast)", Range(0.0, 1.0)) = 0.48
        _Density ("Cloud Opacity Density", Range(0.2, 4.0)) = 1.8
        _Softness ("Cloud Boundary Softness", Range(0.02, 0.5)) = 0.20

        [Header(Color and Shading)]
        _BaseColor ("Sunlit Cloud Color", Color) = (0.95, 0.96, 1.0, 1.0)
        _ShadowColor ("Cloud Underside Shadow", Color) = (0.32, 0.36, 0.48, 1.0)
        _NightCloudColor ("Night Ambient Cloud Tint", Color) = (0.04, 0.06, 0.12, 1.0)

        [Header(Silver Lining Rim Lighting)]
        _SilverLiningIntensity ("Silver Lining Rim Brightness", Range(0.0, 8.0)) = 3.5
        _SilverLiningExponent ("Silver Lining Concentration", Range(2.0, 48.0)) = 16.0

        [Header(Horizon Fade)]
        _HorizonFadeAngle ("Horizon Fade Altitude Deg", Range(1.0, 30.0)) = 10.0

        [Header(Driven By Scripts)]
        _SunDirection ("Sun Direction (World)", Vector) = (0.0, 1.0, 0.0, 0.0)
        _SunColor ("Sun Light Color", Color) = (1.0, 0.95, 0.85, 1.0)
        _SunIntensity ("Sun Intensity Factor", Float) = 1.0
        _MoonDirection ("Moon Direction (World)", Vector) = (0.0, -1.0, 0.0, 0.0)
        _MoonColor ("Moon Light Color", Color) = (0.70, 0.82, 1.0, 1.0)
        _MoonIntensity ("Moon Intensity Factor", Float) = 0.2
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
            "Queue" = "Background+20"
            "RenderPipeline" = "UniversalPipeline"
        }

        Stencil
        {
            Ref [_StencilRef]
            Comp [_StencilComp]
            Pass Keep
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "CloudsForward"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 localPos   : TEXCOORD0;
                float2 uv         : TEXCOORD1;
            };

            TEXTURE2D(_CloudTex);
            SAMPLER(sampler_CloudTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _ShadowColor;
                half4 _NightCloudColor;
                half4 _SunColor;
                half4 _MoonColor;
                float4 _SunDirection;
                float4 _MoonDirection;
                float4 _WindDirection;
                float _Tiling;
                float _WindSpeed;
                float _Coverage;
                float _Density;
                float _Softness;
                float _SilverLiningIntensity;
                float _SilverLiningExponent;
                float _HorizonFadeAngle;
                float _SunIntensity;
                float _MoonIntensity;
                float _Visibility;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.localPos   = input.positionOS.xyz;
                output.uv         = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                if (_Visibility <= 0.0001)
                    discard;

                float3 dir = normalize(input.localPos);

                // Horizon elevation in degrees
                float elevationDeg = asin(clamp(dir.y, -1.0, 1.0)) * (180.0 / PI);
                if (elevationDeg <= 0.5)
                    discard;

                // Smooth horizon fade to avoid clipping at the ground
                float horizonFade = saturate(elevationDeg / max(0.5, _HorizonFadeAngle));

                // ── 1. Planar / Spherical UV Mapping with Dual-Octave Wind Scrolling ──
                // Project local horizontal direction onto planar UV space
                float2 baseUV = (dir.xz / max(0.05, dir.y + 0.35)) * _Tiling;

                float2 windDir = normalize(_WindDirection.xz + float2(0.0001, 0.0001));
                float timeOffset = _Time.y * _WindSpeed;

                // Octave 1: primary wind drift
                float2 uv1 = baseUV + windDir * timeOffset;
                // Octave 2: secondary micro-drift at rotated angle and higher speed
                float2 rotatedWind = float2(-windDir.y, windDir.x);
                float2 uv2 = baseUV * 1.85 + (windDir * 0.7 + rotatedWind * 0.7) * (timeOffset * 1.35);

                half sample1 = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, uv1).r;
                half sample2 = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, uv2).r;

                // Combined multi-scale noise
                half cloudNoise = sample1 * 0.65 + sample2 * 0.35;

                // ── 2. Cloud Coverage & Density ──
                // Coverage modulates threshold: low coverage = isolated cumulus, high = overcast
                float threshold = 1.0 - _Coverage;
                float halfSoft = max(0.01, _Softness * 0.5);
                float cloudAlpha = smoothstep(threshold - halfSoft, threshold + halfSoft, cloudNoise);
                cloudAlpha = saturate(cloudAlpha * _Density) * horizonFade * _Visibility;

                if (cloudAlpha <= 0.002)
                    discard;

                // ── 3. Solar & Lunar Lighting ──
                float3 V = normalize(_WorldSpaceCameraPos - input.localPos);
                float3 sunDir  = normalize(_SunDirection.xyz);
                float3 moonDir = normalize(_MoonDirection.xyz);

                // Underside shading factor based on cloud thickness (dense clouds have dark bellies)
                float thicknessShadow = saturate((cloudNoise - (threshold - halfSoft)) * 1.6);
                half3 dayCloudColor = lerp(_ShadowColor.rgb, _BaseColor.rgb * _SunColor.rgb, thicknessShadow);

                // Blend between day sunlight and nighttime moonlight
                float sunWeight = saturate(_SunIntensity);
                float moonWeight = saturate(_MoonIntensity);
                half3 ambientCloud = lerp(_NightCloudColor.rgb, dayCloudColor, sunWeight);

                // ── 4. Silver Lining Forward-Scattering Rim Light ──
                // When looking toward the Sun/Moon, cloud edges glow with a brilliant silver/golden lining
                float cosSun = saturate(dot(-V, sunDir));
                float silverLiningSun = pow(cosSun, _SilverLiningExponent) * _SilverLiningIntensity * sunWeight;
                half3 sunRim = _SunColor.rgb * silverLiningSun;

                float cosMoon = saturate(dot(-V, moonDir));
                float silverLiningMoon = pow(cosMoon, _SilverLiningExponent) * (_SilverLiningIntensity * 0.8) * moonWeight;
                half3 moonRim = _MoonColor.rgb * silverLiningMoon;

                // Edge mask: silver lining appears strongest on the thin borders of clouds
                float edgeMask = 1.0 - abs(cloudAlpha - 0.5) * 2.0;
                edgeMask = saturate(edgeMask * 1.5);

                half3 finalRGB = ambientCloud + (sunRim + moonRim) * edgeMask;

                return half4(finalRGB, cloudAlpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

