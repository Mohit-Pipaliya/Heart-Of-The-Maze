Shader "CelestialSystem/SkyGradient"
{
    Properties
    {
        [Header(Day Colors)]
        _DayZenithColor ("Day Zenith Color", Color) = (0.22, 0.46, 0.82, 1.0)
        _DayHorizonColor ("Day Horizon Color", Color) = (0.68, 0.82, 0.96, 1.0)

        [Header(Sunset Dawn Colors)]
        _SunsetZenithColor ("Sunset Zenith Color", Color) = (0.16, 0.22, 0.48, 1.0)
        _SunsetHorizonColor ("Sunset Horizon Color", Color) = (0.98, 0.44, 0.14, 1.0)
        _TwilightColor ("Twilight Band Color", Color) = (0.86, 0.22, 0.38, 1.0)

        [Header(Night Colors)]
        _NightZenithColor ("Night Zenith Color", Color) = (0.008, 0.012, 0.030, 1.0)
        _NightHorizonColor ("Night Horizon Color", Color) = (0.024, 0.035, 0.065, 1.0)

        [Header(Ground Nadir Color)]
        _GroundColor ("Ground Nadir Color", Color) = (0.04, 0.04, 0.05, 1.0)

        [Header(Sun Glow Mie Scattering)]
        [HDR] _SunGlowColor ("Sun Atmospheric Glow Color", Color) = (1.0, 0.92, 0.75, 1.0)
        _SunGlowIntensity ("Sun Glow Intensity", Range(0.0, 8.0)) = 2.5
        _SunGlowExponent ("Sun Glow Focus", Range(2.0, 64.0)) = 18.0

        [Header(Horizon Band Dynamics)]
        _HorizonGlowWidth ("Horizon Band Sharpness", Range(0.02, 0.5)) = 0.15
        _HorizonOffset ("Horizon Height Offset", Range(-0.2, 0.2)) = 0.0

        [Header(Driven By Scripts)]
        _SunDirection ("Sun World Direction", Vector) = (0.0, 1.0, 0.0, 0.0)
        _SunAltitude ("Sun Altitude Deg", Float) = 45.0
        _Visibility ("Visibility (CelestialVolume)", Range(0.0, 1.0)) = 1.0

        [Header(Stencil)]
        _StencilRef ("Stencil Ref", Int) = 0
        _StencilComp ("Stencil Comp", Int) = 8 /* Always */
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Background"
            "Queue" = "Background"
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType" = "Skybox"
        }

        Stencil
        {
            Ref [_StencilRef]
            Comp [_StencilComp]
            Pass Keep
        }

        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "SkyGradientForward"

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
                float3 worldDir   : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _DayZenithColor;
                half4 _DayHorizonColor;
                half4 _SunsetZenithColor;
                half4 _SunsetHorizonColor;
                half4 _TwilightColor;
                half4 _NightZenithColor;
                half4 _NightHorizonColor;
                half4 _GroundColor;
                half4 _SunGlowColor;
                float4 _SunDirection;
                float _SunGlowIntensity;
                float _SunGlowExponent;
                float _HorizonGlowWidth;
                float _HorizonOffset;
                float _SunAltitude;
                float _Visibility;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                // In sky dome or sky box, local vertex position indicates view direction
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.worldDir = TransformObjectToWorldDir(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                if (_Visibility <= 0.0001)
                    discard;

                float3 V = normalize(input.worldDir);
                float y = V.y - _HorizonOffset;

                // ── 1. Phase Blend Weights (Civil, Nautical, Day, Sunset, Night) ──
                // Day: altitude > 10°
                // Sunset/Dawn: -3° to 15° (peaking at ~2°)
                // Twilight: -18° to 2°
                // Night: altitude < -10°
                float dayWeight     = saturate((_SunAltitude - 4.0) / 16.0);
                float sunsetWeight  = saturate(1.0 - abs(_SunAltitude - 1.5) / 10.0);
                float twilightWeight = saturate((_SunAltitude + 16.0) / 12.0) * saturate((4.0 - _SunAltitude) / 8.0);
                float nightWeight   = saturate((- _SunAltitude - 4.0) / 14.0);

                // Normalize phase weights so they sum to 1.0
                float totalWeight = dayWeight + sunsetWeight + twilightWeight + nightWeight + 0.0001;
                dayWeight     /= totalWeight;
                sunsetWeight  /= totalWeight;
                twilightWeight /= totalWeight;
                nightWeight   /= totalWeight;

                // ── 2. Composite Zenith & Horizon Colors ──
                half3 compositeZenith =
                    _DayZenithColor.rgb    * dayWeight +
                    _SunsetZenithColor.rgb * sunsetWeight +
                    _NightZenithColor.rgb  * (twilightWeight + nightWeight);

                half3 compositeHorizon =
                    _DayHorizonColor.rgb    * dayWeight +
                    _SunsetHorizonColor.rgb * sunsetWeight +
                    _TwilightColor.rgb      * twilightWeight +
                    _NightHorizonColor.rgb  * nightWeight;

                // ── 3. Elevation Gradient (Sky vs Ground) ──
                half3 skyColor;
                if (y >= 0.0)
                {
                    // Sky hemisphere: blend from horizon to zenith
                    float skyFactor = saturate(pow(y, 0.75));
                    skyColor = lerp(compositeHorizon, compositeZenith, skyFactor);

                    // Add subtle horizontal glow layer right at the horizon
                    float horizonGlow = exp(-y / max(0.001, _HorizonGlowWidth));
                    skyColor = lerp(skyColor, compositeHorizon * 1.15, horizonGlow * 0.4);
                }
                else
                {
                    // Below horizon / ground blend
                    float groundFactor = saturate(-y * 4.0);
                    skyColor = lerp(compositeHorizon, _GroundColor.rgb, groundFactor);
                }

                // ── 4. Mie Atmospheric Sun Glow (Forward Scattering) ──
                // Only if sun is above astronomical twilight (alt > -12°)
                float sunAboveGlow = saturate((_SunAltitude + 12.0) / 10.0);
                if (sunAboveGlow > 0.001)
                {
                    float3 sunDir = normalize(_SunDirection.xyz);
                    float cosTheta = dot(V, sunDir);
                    if (cosTheta > 0.0)
                    {
                        // Mie phase function approximation: pow(saturate(cosTheta), exponent)
                        float mieGlow = pow(saturate(cosTheta), _SunGlowExponent) * _SunGlowIntensity;
                        float broadHalo = pow(saturate(cosTheta), max(2.0, _SunGlowExponent * 0.25)) * (_SunGlowIntensity * 0.25);

                        half3 glowEmission = _SunGlowColor.rgb * ((mieGlow + broadHalo) * sunAboveGlow);
                        skyColor += glowEmission;
                    }
                }

                return half4(skyColor * _Visibility, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
