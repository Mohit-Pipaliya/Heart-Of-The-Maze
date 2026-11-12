Shader "CelestialSystem/Clouds"
{
    Properties
    {
        [Header(Textures and Scrolling)]
        _CloudTex ("Seamless Cloud Noise Texture", 2D) = "white" {}
        _Tiling ("Cumulus Cloud Tiling", Float) = 1.85
        _WindSpeed ("Wind Velocity", Range(0.0, 0.5)) = 0.04
        _WindDirection ("Wind Direction (X, Z)", Vector) = (1.0, 0.0, 0.35, 0.0)
        _TurbulenceSpeed ("Cloud Evolution & Boiling Speed", Range(0.0, 0.5)) = 0.06

        [Header(Cloud Morphology)]
        _Coverage ("Cloud Coverage (0 = Clear, 1 = Overcast)", Range(0.0, 1.0)) = 0.46
        _Density ("Cloud Opacity Density", Range(0.5, 5.0)) = 2.4
        _Softness ("Cloud Boundary Softness", Range(0.02, 0.5)) = 0.22

        [Header(Color and Shading)]
        _BaseColor ("Sunlit Cloud Highlights", Color) = (1.0, 0.98, 0.95, 1.0)
        _ShadowColor ("Cloud Underside Shadow", Color) = (0.28, 0.32, 0.45, 1.0)
        _SunsetColor ("Sunset Cloud Reddening", Color) = (1.0, 0.48, 0.25, 1.0)
        _NightCloudColor ("Night Ambient Cloud Tint", Color) = (0.05, 0.08, 0.16, 1.0)

        [Header(Silver Lining Rim Lighting)]
        _SilverLiningIntensity ("Silver Lining Rim Brightness", Range(0.0, 10.0)) = 4.5
        _SilverLiningExponent ("Silver Lining Concentration", Range(2.0, 32.0)) = 12.0

        [Header(High Altitude Cirrus Layer)]
        _CirrusTiling ("Cirrus Cloud Tiling", Float) = 4.2
        _CirrusOpacity ("Cirrus Cloud Visibility", Range(0.0, 1.0)) = 0.35

        [Header(Horizon Fade)]
        _HorizonFadeAngle ("Horizon Fade Altitude Deg", Range(1.0, 30.0)) = 10.0

        [Header(Driven By Scripts)]
        _SunDirection ("Sun Direction (World)", Vector) = (0.0, 1.0, 0.0, 0.0)
        _SunColor ("Sun Light Color", Color) = (1.0, 0.95, 0.85, 1.0)
        _SunAltitude ("Sun Altitude Deg", Float) = 45.0
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
            "Queue" = "Transparent-400"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
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
                half4 _SunsetColor;
                half4 _NightCloudColor;
                half4 _SunColor;
                half4 _MoonColor;
                float4 _SunDirection;
                float4 _MoonDirection;
                float4 _WindDirection;
                float _Tiling;
                float _WindSpeed;
                float _TurbulenceSpeed;
                float _Coverage;
                float _Density;
                float _Softness;
                float _SilverLiningIntensity;
                float _SilverLiningExponent;
                float _CirrusTiling;
                float _CirrusOpacity;
                float _HorizonFadeAngle;
                float _SunAltitude;
                float _SunIntensity;
                float _MoonIntensity;
                float _Visibility;
            CBUFFER_END

            // ───────────────────────────────────────────────────────────────
            //  AAA Procedural Fractal Brownian Motion (FBM) & Domain Warping
            // ───────────────────────────────────────────────────────────────
            float2 Hash22(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = dot(Hash22(i + float2(0.0, 0.0)) - 0.5, f - float2(0.0, 0.0));
                float b = dot(Hash22(i + float2(1.0, 0.0)) - 0.5, f - float2(1.0, 0.0));
                float c = dot(Hash22(i + float2(0.0, 1.0)) - 0.5, f - float2(0.0, 1.0));
                float d = dot(Hash22(i + float2(1.0, 1.0)) - 0.5, f - float2(1.0, 1.0));

                return 0.5 + 0.5 * lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float FBM4(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                float2x2 rot = float2x2(0.8775, 0.4794, -0.4794, 0.8775);
                for (int i = 0; i < 4; ++i)
                {
                    v += a * ValueNoise(p);
                    p = mul(rot, p) * 2.02 + float2(4.2, 1.7);
                    a *= 0.5;
                }
                return v;
            }

            // Domain warping for billowing cumulus edges
            float WarpedFBM(float2 p, float evolveTime)
            {
                float2 q = float2(FBM4(p), FBM4(p + float2(5.2, 1.3)));
                float2 r = float2(FBM4(p + 3.0 * q + float2(1.7, 9.2) + float2(evolveTime, 0.0)),
                                  FBM4(p + 3.0 * q + float2(8.3, 2.8) + float2(0.0, evolveTime * 0.85)));
                return FBM4(p + 2.5 * r);
            }

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

                // Horizon elevation check
                float elevationDeg = asin(clamp(dir.y, -1.0, 1.0)) * (180.0 / PI);
                if (elevationDeg <= 0.5)
                    discard;

                // Smooth horizon fade to seamlessly blend with atmospheric haze
                float horizonFade = saturate(elevationDeg / max(0.5, _HorizonFadeAngle));

                // ── 1. Planar UV Mapping with Wind Vector ──
                float2 skyPlaneUV = (dir.xz / max(0.06, dir.y + 0.28)) * _Tiling;
                float2 windDir = normalize(_WindDirection.xz + float2(0.0001, 0.0001));

                float windDist = _Time.y * _WindSpeed;
                float evolveTime = _Time.y * _TurbulenceSpeed;

                float2 windOffset = windDir * windDist;

                // Sample base texture for macro cloud shape
                float2 texUV1 = skyPlaneUV + windOffset;
                float2 texUV2 = skyPlaneUV * 1.75 + (windOffset * 1.3) + float2(0.3, 0.7);
                half texSample1 = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, texUV1).r;
                half texSample2 = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, texUV2).r;
                half baseTexNoise = lerp(texSample1, texSample2, 0.35);

                // Sample procedural Warped FBM for realistic billowy micro-structure
                float proceduralNoise = WarpedFBM(skyPlaneUV * 1.4 + windOffset * 0.8, evolveTime);

                // Combine macro texture and procedural billows
                half cumulusNoise = baseTexNoise * 0.55 + proceduralNoise * 0.45;

                // ── 2. High-Altitude Cirrus Ribbons (Layer 2) ──
                float2 cirrusUV = (dir.xz / max(0.06, dir.y + 0.28)) * _CirrusTiling;
                float2 cirrusWindOffset = float2(windDir.x * 1.5 - windDir.y * 0.5, windDir.y * 1.5 + windDir.x * 0.5) * windDist;
                float cirrusNoise = FBM4(cirrusUV + cirrusWindOffset);
                float cirrusAlpha = smoothstep(0.40, 0.75, cirrusNoise) * _CirrusOpacity;

                // ── 3. Cumulus Coverage & Density ──
                float threshold = 1.0 - _Coverage;
                float halfSoft = max(0.01, _Softness * 0.5);
                float cumulusAlpha = smoothstep(threshold - halfSoft, threshold + halfSoft, cumulusNoise);
                cumulusAlpha = saturate(cumulusAlpha * _Density);

                // Combine layers (cumulus + cirrus)
                float totalAlpha = saturate(cumulusAlpha + cirrusAlpha * (1.0 - cumulusAlpha));
                totalAlpha = totalAlpha * horizonFade * _Visibility;

                if (totalAlpha <= 0.005)
                    discard;

                // ── 4. Cinematic Solar, Lunar & Sunset Lighting ──
                float3 V = normalize(_WorldSpaceCameraPos - input.localPos);
                float3 sunDir  = normalize(_SunDirection.xyz);
                float3 moonDir = normalize(_MoonDirection.xyz);

                // Sunset factor (Sun near horizon between -3° and 15°)
                float sunsetFactor = saturate(1.0 - abs(_SunAltitude - 2.5) / 12.0);

                // Underside shading based on cloud thickness (dense clouds have dark, volumetric bellies)
                float thicknessShadow = saturate((cumulusNoise - (threshold - halfSoft)) * 1.8);
                
                // Color transition from midday sunlit to sunset crimson
                half3 sunlitTop = lerp(_BaseColor.rgb * _SunColor.rgb, _SunsetColor.rgb, sunsetFactor * 0.85);
                half3 dayCloudColor = lerp(_ShadowColor.rgb, sunlitTop, thicknessShadow);

                // Blend between day sunlight and nighttime moonlight
                float sunWeight  = saturate(_SunIntensity);
                float moonWeight = saturate(_MoonIntensity);
                half3 ambientCloud = lerp(_NightCloudColor.rgb, dayCloudColor, sunWeight);

                // ── 5. Silver & Golden Lining (Forward Mie Scattering) ──
                // Strongest when looking toward Sun/Moon through cloud borders
                float cosSun = saturate(dot(-V, sunDir));
                float silverLiningSun = pow(cosSun, _SilverLiningExponent) * _SilverLiningIntensity * sunWeight;
                half3 sunRimColor = lerp(_SunColor.rgb, _SunsetColor.rgb, sunsetFactor);
                half3 sunRim = sunRimColor * silverLiningSun;

                float cosMoon = saturate(dot(-V, moonDir));
                float silverLiningMoon = pow(cosMoon, _SilverLiningExponent) * (_SilverLiningIntensity * 0.85) * moonWeight;
                half3 moonRim = _MoonColor.rgb * silverLiningMoon;

                // Thin border mask: rim lighting is concentrated on the translucent outer edges
                float edgeMask = 1.0 - abs(totalAlpha - 0.45) * 2.2;
                edgeMask = saturate(edgeMask * 1.6);

                half3 finalRGB = ambientCloud + (sunRim + moonRim) * edgeMask;

                return half4(finalRGB, totalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
