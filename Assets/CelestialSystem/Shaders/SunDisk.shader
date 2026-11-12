Shader "CelestialSystem/SunDisk"
{
    Properties
    {
        [Header(Base Appearance)]
        [HDR] _BaseColor ("Sun Color (Noon)", Color) = (1.0, 0.98, 0.92, 1.0)
        [HDR] _HorizonReddeningColor ("Sun Color (Sunset/Dawn)", Color) = (1.0, 0.38, 0.12, 1.0)
        _DiskSize ("Disk Angular Radius", Range(0.05, 0.5)) = 0.22
        _SoftEdge ("Soft Edge Width", Range(0.005, 0.1)) = 0.02

        [Header(Limb Darkening)]
        _LimbDarkening ("Limb Darkening Factor", Range(0.0, 1.0)) = 0.65
        _LimbDarkeningPower ("Limb Darkening Exponent", Range(0.5, 3.0)) = 1.0

        [Header(Corona and Halo)]
        _CoronaIntensity ("Corona Brightness", Range(0.0, 5.0)) = 2.0
        _CoronaSize ("Corona Radius Factor", Range(0.05, 0.5)) = 0.25
        _OuterHaloIntensity ("Outer Halo Brightness", Range(0.0, 3.0)) = 0.6
        _OuterHaloSize ("Outer Halo Radius Factor", Range(0.05, 0.6)) = 0.35

        [Header(Atmospheric FX)]
        _EmissionMultiplier ("HDR Emission Multiplier", Range(1.0, 20.0)) = 3.5
        _HorizonExpansion ("Horizon Size Expansion Factor", Range(1.0, 2.0)) = 1.35
        _FlickerSpeed ("Corona Shimmer Speed", Range(0.0, 20.0)) = 4.0
        _FlickerIntensity ("Corona Shimmer Amount", Range(0.0, 0.2)) = 0.04

        [Header(Driven By Scripts)]
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
            "RenderType" = "Transparent"
            "Queue" = "Transparent+500"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Stencil
        {
            Ref [_StencilRef]
            Comp [_StencilComp]
            Pass Keep
        }

        Blend One One // Additive blend for HDR celestial disk
        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "SunDiskForward"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _HorizonReddeningColor;
                float _DiskSize;
                float _SoftEdge;
                float _LimbDarkening;
                float _LimbDarkeningPower;
                float _CoronaIntensity;
                float _CoronaSize;
                float _OuterHaloIntensity;
                float _OuterHaloSize;
                float _EmissionMultiplier;
                float _HorizonExpansion;
                float _FlickerSpeed;
                float _FlickerIntensity;
                float _SunAltitude;
                float _Visibility;
            CBUFFER_END

            // Pseudo-random hash for atmospheric shimmer
            float Hash21(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Visibility or below horizon cutoff
                // Fade out when altitude drops below 0 deg, fully gone at -3 deg
                float horizonFade = saturate((_SunAltitude + 3.0) / 3.0);
                float totalVis = _Visibility * horizonFade;
                if (totalVis <= 0.0001)
                    discard;

                // Center UV coords from -1 to 1
                float2 centeredUV = (input.uv - 0.5) * 2.0;
                float r = length(centeredUV);

                // Outside unit circle is strictly discarded — guarantees 100% perfectly round disk
                if (r >= 1.0)
                    discard;

                // Horizon apparent size expansion (moon/sun illusion + atmospheric refraction)
                float horizonFactor = saturate((18.0 - _SunAltitude) / 18.0);
                float sizeMultiplier = lerp(1.0, _HorizonExpansion, horizonFactor);
                float effectiveDiskSize = _DiskSize * sizeMultiplier;
                float effectiveSoftEdge = _SoftEdge * sizeMultiplier;

                // Color shift near horizon (Rayleigh scattering: red shift)
                half3 sunColor = lerp(_BaseColor.rgb, _HorizonReddeningColor.rgb, horizonFactor);

                // Subtle corona shimmer flicker
                float flicker = 1.0;
                if (_FlickerIntensity > 0.001)
                {
                    float angle = atan2(centeredUV.y, centeredUV.x);
                    float2 noiseCoord = float2(angle * 3.0, _Time.y * _FlickerSpeed);
                    flicker = 1.0 + (Hash21(noiseCoord) - 0.5) * _FlickerIntensity;
                }

                // ── 1. Sun Disk (r <= effectiveDiskSize) ──
                // Normalized radius inside the disk: 0 (center) to 1 (rim)
                float diskNormR = r / max(0.0001, effectiveDiskSize);
                float diskMask = 1.0 - smoothstep(1.0 - effectiveSoftEdge, 1.0 + effectiveSoftEdge, diskNormR);

                // Solar Limb Darkening: I(mu) = 1 - u*(1 - mu^alpha)
                // where mu = cos(theta) = sqrt(max(0, 1 - diskNormR^2))
                float mu = sqrt(max(0.0, 1.0 - min(1.0, diskNormR * diskNormR)));
                float limbDarkening = 1.0 - _LimbDarkening * (1.0 - pow(mu, _LimbDarkeningPower));
                float diskBrightness = diskMask * limbDarkening;

                // ── 2. Corona & Halo (r > effectiveDiskSize) ──
                float coronaDist = max(0.0, r - effectiveDiskSize);
                float innerCorona = exp(-coronaDist / max(0.001, _CoronaSize)) * _CoronaIntensity * flicker;
                float outerHalo   = exp(-coronaDist / max(0.001, _OuterHaloSize)) * _OuterHaloIntensity;

                // Smooth quad radial border falloff to ensure zero sharp boundaries
                float borderFade = smoothstep(1.0, 0.70, r);

                // Combine disk and corona
                float totalIntensity = (diskBrightness + innerCorona + outerHalo) * borderFade;

                // HDR emission output
                half3 finalColor = sunColor * (totalIntensity * _EmissionMultiplier * totalVis);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

