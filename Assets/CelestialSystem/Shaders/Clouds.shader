Shader "CelestialSystem/Clouds"
{
    Properties
    {
        [Header(Textures and Scrolling)]
        _CloudTex ("Seamless Cloud Noise Texture", 2D) = "white" {}
        _Tiling ("Cumulus Cloud Tiling", Float) = 1.10
        _WindSpeed ("Wind Velocity", Range(0.0, 0.5)) = 0.03
        _WindDirection ("Wind Direction (X, Z)", Vector) = (1.0, 0.0, 0.35, 0.0)
        _TurbulenceSpeed ("Cloud Evolution & Boiling Speed", Range(0.0, 0.5)) = 0.04

        [Header(Cloud Morphology)]
        _Coverage ("Cloud Coverage (0 = Clear, 1 = Overcast)", Range(0.0, 1.0)) = 0.42
        _Density ("Cloud Opacity Density", Range(0.5, 5.0)) = 1.8
        _Softness ("Cloud Boundary Softness", Range(0.05, 0.6)) = 0.28

        [Header(Color and Shading)]
        _BaseColor ("Sunlit Cloud Highlights", Color) = (1.0, 0.99, 0.97, 1.0)
        _ShadowColor ("Cloud Ambient Underside", Color) = (0.78, 0.84, 0.92, 1.0)
        _SunsetColor ("Sunset Cloud Reddening", Color) = (1.0, 0.58, 0.35, 1.0)
        _NightCloudColor ("Night Ambient Cloud Tint", Color) = (0.12, 0.16, 0.25, 1.0)
        _CirrusColor ("High Cirrus White Color", Color) = (0.95, 0.98, 1.0, 1.0)

        [Header(Silver Lining Rim Lighting)]
        _SilverLiningIntensity ("Silver Lining Rim Brightness", Range(0.0, 8.0)) = 2.8
        _SilverLiningExponent ("Silver Lining Concentration", Range(2.0, 32.0)) = 14.0

        [Header(High Altitude Cirrus Layer)]
        _CirrusTiling ("Cirrus Cloud Tiling", Float) = 1.0
        _CirrusOpacity ("Cirrus Cloud Visibility", Range(0.0, 1.0)) = 0.22

        [Header(Horizon Fade)]
        _HorizonFadeAngle ("Horizon Fade Altitude Deg", Range(1.0, 30.0)) = 8.0

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
                half4 _CirrusColor;
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
            //  AAA Smooth Procedural Billow & Cirrus Noise (Zero Speckles)
            // ───────────────────────────────────────────────────────────────
            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float SmoothNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = Hash12(i + float2(0.0, 0.0));
                float b = Hash12(i + float2(1.0, 0.0));
                float c = Hash12(i + float2(0.0, 1.0));
                float d = Hash12(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // Low-frequency billow FBM: broad, majestic, rolling cumulus masses
            float BillowFBM(float2 p, float evolve)
            {
                float v = 0.0;
                float a = 0.55;
                float2x2 rot = float2x2(0.80, 0.60, -0.60, 0.80);

                // Octave 1: Primary cloud body
                v += a * SmoothNoise(p + float2(evolve * 0.25, 0.0));
                p = mul(rot, p) * 1.85 + float2(1.7, 3.2);
                a *= 0.45;

                // Octave 2: Billowy rolls
                v += a * SmoothNoise(p - float2(0.0, evolve * 0.20));
                p = mul(rot, p) * 1.90 + float2(4.3, 1.1);
                a *= 0.40;

                // Octave 3: Soft feathered lobes (no micro-noise!)
                v += a * SmoothNoise(p + float2(evolve * 0.15, evolve * 0.15));

                return v;
            }

            // Stretched wispy ribbons along wind direction for authentic high-altitude cirrus
            float CirrusNoise(float2 p, float2 windDir)
            {
                float2 stretched = float2(
                    dot(p, windDir) * 0.30,
                    dot(p, float2(-windDir.y, windDir.x)) * 1.10
                );
                float n1 = SmoothNoise(stretched);
                float n2 = SmoothNoise(stretched * 1.90 + float2(3.1, 5.7));
                return n1 * 0.65 + n2 * 0.35;
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

                // Horizon elevation
                float elevationDeg = asin(clamp(dir.y, -1.0, 1.0)) * (180.0 / PI);
                if (elevationDeg <= 0.2)
                    discard;

                // Smooth horizon fade to seamlessly blend with atmospheric haze
                float horizonFade = saturate(elevationDeg / max(0.5, _HorizonFadeAngle));

                // ── 1. Planar UV Mapping with Wind Vector ──
                float2 skyPlaneUV = (dir.xz / max(0.06, dir.y + 0.25)) * _Tiling;
                float2 windDir = normalize(_WindDirection.xz + float2(0.0001, 0.0001));

                float windDist = _Time.y * _WindSpeed;
                float evolveTime = _Time.y * _TurbulenceSpeed;
                float2 windOffset = windDir * windDist;

                // Sample base texture for macro cloud distribution
                float2 texUV1 = skyPlaneUV * 0.65 + windOffset;
                float2 texUV2 = skyPlaneUV * 1.15 + (windOffset * 1.25) + float2(0.3, 0.7);
                half texSample1 = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, texUV1).r;
                half texSample2 = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, texUV2).r;
                half baseTexNoise = lerp(texSample1, texSample2, 0.35);

                // Smooth procedural billow FBM
                float billowNoise = BillowFBM(skyPlaneUV * 0.90 + windOffset * 0.75, evolveTime);

                // Combine macro texture and billows
                half cumulusNoise = baseTexNoise * 0.50 + billowNoise * 0.50;

                // ── 2. High-Altitude Cirrus Ribbons (Layer 2) ──
                float2 cirrusUV = (dir.xz / max(0.06, dir.y + 0.25)) * _CirrusTiling;
                float2 cirrusWindOffset = windDir * (windDist * 1.4);
                float cirrusN = CirrusNoise(cirrusUV + cirrusWindOffset, windDir);
                float cirrusAlpha = smoothstep(0.42, 0.78, cirrusN) * _CirrusOpacity;

                // ── 3. Cumulus Coverage & Density ──
                float threshold = 1.0 - _Coverage;
                float halfSoft = max(0.02, _Softness * 0.5);
                float cumulusAlpha = smoothstep(threshold - halfSoft, threshold + halfSoft, cumulusNoise);
                cumulusAlpha = saturate(cumulusAlpha * _Density);

                // Combine layers (cumulus + cirrus)
                float totalAlpha = saturate(cumulusAlpha + cirrusAlpha * (1.0 - cumulusAlpha));
                totalAlpha = totalAlpha * horizonFade * _Visibility;

                if (totalAlpha <= 0.002)
                    discard;

                // ── 4. Cinematic Solar, Lunar & Sunset Lighting ──
                float3 sunDir  = normalize(_SunDirection.xyz);
                float3 moonDir = normalize(_MoonDirection.xyz);

                // Sunset factor (Sun near horizon between -3° and 16°)
                float sunsetFactor = saturate(1.0 - abs(_SunAltitude - 3.0) / 14.0);

                // Underside shading based on cloud thickness (ambient sky bounce, NEVER black!)
                float thicknessShadow = saturate((cumulusNoise - (threshold - halfSoft)) * 1.6);

                // Sunlit tops vs ambient skylight underside
                half3 sunlitTop = lerp(_BaseColor.rgb * _SunColor.rgb, _SunsetColor.rgb, sunsetFactor * 0.85);
                half3 ambientUnderside = lerp(_ShadowColor.rgb, _SunsetColor.rgb * 0.75, sunsetFactor * 0.5);
                half3 cumulusColor = lerp(ambientUnderside, sunlitTop, thicknessShadow);

                // Cirrus is always luminous high ice crystals (never in dark shadow!)
                half3 cirrusDay = lerp(_CirrusColor.rgb * _SunColor.rgb, _SunsetColor.rgb, sunsetFactor * 0.90);

                // Day cloud RGB blended between cirrus and cumulus
                half3 dayCloudColor = lerp(cirrusDay, cumulusColor, saturate(cumulusAlpha * 2.0));

                // Day / Night transition
                float sunWeight  = saturate(_SunIntensity);
                float moonWeight = saturate(_MoonIntensity);

                half3 nightCloud = lerp(_NightCloudColor.rgb, _MoonColor.rgb * 0.55, moonWeight * 0.6);
                half3 ambientCloud = lerp(nightCloud, dayCloudColor, sunWeight);

                // ── 5. Silver & Golden Lining (Forward Mie Scattering) ──
                // Strongest when looking toward Sun/Moon through translucent cloud borders
                float cosSun  = saturate(dot(dir, sunDir));
                float silverLiningSun = pow(cosSun, _SilverLiningExponent) * _SilverLiningIntensity * sunWeight;
                half3 sunRimColor = lerp(_SunColor.rgb, _SunsetColor.rgb, sunsetFactor);
                half3 sunRim = sunRimColor * silverLiningSun;

                float cosMoon = saturate(dot(dir, moonDir));
                float silverLiningMoon = pow(cosMoon, _SilverLiningExponent) * (_SilverLiningIntensity * 0.70) * moonWeight;
                half3 moonRim = _MoonColor.rgb * silverLiningMoon;

                // Rim lighting concentrated on translucent outer edges
                float edgeMask = 1.0 - abs(totalAlpha - 0.45) * 2.2;
                edgeMask = saturate(edgeMask * 1.4);

                half3 finalRGB = ambientCloud + (sunRim + moonRim) * edgeMask;

                return half4(finalRGB, totalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
