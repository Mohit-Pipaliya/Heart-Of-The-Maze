Shader "CelestialSystem/StarDome"
{
    Properties
    {
        [Header(Milky Way)]
        _MilkyWayTex ("Milky Way Panorama (Equirectangular)", 2D) = "black" {}
        [HDR] _MilkyWayColor ("Milky Way Tint Color", Color) = (0.85, 0.90, 1.0, 1.0)
        _MilkyWayIntensity ("Milky Way Brightness", Range(0.0, 4.0)) = 1.2

        [Header(Star Field Appearance)]
        _StarDensity ("Star Density", Range(50.0, 400.0)) = 180.0
        _StarBrightness ("Star Brightness", Range(0.5, 8.0)) = 2.5
        _TwinkleSpeed ("Scintillation Twinkle Speed", Range(0.0, 20.0)) = 5.0
        _TwinkleAmount ("Twinkle Depth", Range(0.0, 1.0)) = 0.55

        [Header(Atmospheric Day Bleed Fade)]
        _SunAltitude ("Sun Altitude Deg (Driven)", Float) = -20.0
        _FadeStartAlt ("Sun Alt Fade Start Deg", Float) = -4.0
        _FadeEndAlt ("Sun Alt Full Dark Deg", Float) = -14.0

        [Header(Driven By Scripts)]
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
            "Queue" = "Background+10"
            "RenderPipeline" = "UniversalPipeline"
        }

        Stencil
        {
            Ref [_StencilRef]
            Comp [_StencilComp]
            Pass Keep
        }

        Blend One One // Additive blend over the sky gradient
        Cull Front   // Render inside of celestial sphere
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "StarDomeForward"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 localPos   : TEXCOORD0;
            };

            TEXTURE2D(_MilkyWayTex);
            SAMPLER(sampler_MilkyWayTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _MilkyWayColor;
                float _MilkyWayIntensity;
                float _StarDensity;
                float _StarBrightness;
                float _TwinkleSpeed;
                float _TwinkleAmount;
                float _SunAltitude;
                float _FadeStartAlt;
                float _FadeEndAlt;
                float _Visibility;
            CBUFFER_END

            // Pseudo-random 2D to 3D hash
            float3 Hash23(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(443.897, 441.423, 437.195));
                p3 += dot(p3, p3.yzx + 19.19);
                return frac((p3.xxy + p3.yzz) * p3.zyx);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.localPos   = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Sunlight washes out stars: 0 when sun > _FadeStartAlt, 1 when sun < _FadeEndAlt
                float starFade = saturate((_FadeStartAlt - _SunAltitude) / max(0.1, _FadeStartAlt - _FadeEndAlt));
                float totalVis = _Visibility * starFade;
                if (totalVis <= 0.0001)
                    discard;

                float3 dir = normalize(input.localPos);

                // ── 1. Milky Way Panorama Sampling ──
                // Equirectangular mapping from sphere direction
                float u = atan2(dir.z, dir.x) * (0.5 / PI) + 0.5;
                float v = asin(clamp(dir.y, -1.0, 1.0)) * (1.0 / PI) + 0.5;
                half4 mwSample = SAMPLE_TEXTURE2D(_MilkyWayTex, sampler_MilkyWayTex, float2(u, v));
                half3 milkyWay = mwSample.rgb * (_MilkyWayColor.rgb * (_MilkyWayIntensity * totalVis));

                // ── 2. Procedural Star Grid (Cellular / Voronoi lattice) ──
                // Map latitude/longitude to 2D cellular grid
                float2 uv = float2(
                    atan2(dir.z, dir.x) * (0.5 / PI) * _StarDensity,
                    dir.y * (_StarDensity * 0.5)
                );

                float2 cellId = floor(uv);
                float2 cellUV = frac(uv);

                half3 starsAccum = half3(0.0, 0.0, 0.0);

                // Sample 3x3 neighboring cells to ensure stars don't clip at cell borders
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 neighbor = float2((float)x, (float)y);
                        float2 cId = cellId + neighbor;
                        float3 rnd = Hash23(cId);

                        // Star position inside cell
                        float2 starPos = neighbor + rnd.xy;
                        float dist = length(cellUV - starPos);

                        // Star magnitude (rnd.z gives random distribution: power curve so faint stars are common)
                        float mag = pow(rnd.z, 5.0);

                        // Scintillation / Twinkle based on star-specific phase and time
                        float twinklePhase = rnd.x * 6.28 + _Time.y * _TwinkleSpeed;
                        float twinkle = 1.0 + sin(twinklePhase) * _TwinkleAmount;

                        // Star point spread function (PSF)
                        float starRadius = lerp(0.035, 0.08, rnd.z);
                        float starIntensity = exp(-dist / starRadius) * mag * twinkle;

                        // Star color temperature variation: blue-white to amber
                        half3 starColor = lerp(
                            half3(0.85, 0.92, 1.0),  // Type B/A blue-white star
                            half3(1.0, 0.78, 0.45),  // Type K/M amber star
                            rnd.y
                        );

                        starsAccum += starColor * starIntensity;
                    }
                }

                half3 finalStars = starsAccum * (_StarBrightness * totalVis);
                half3 outputColor = milkyWay + finalStars;

                return half4(outputColor, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
