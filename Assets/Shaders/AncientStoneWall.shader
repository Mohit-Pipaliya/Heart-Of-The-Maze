Shader "Heart Of The Maze/Ancient Stone Wall"
{
    Properties
    {
        [MainTexture] _BaseMap("Stone Albedo", 2D) = "white" {}
        [MainColor] _Tint("Stone Tint", Color) = (0.32, 0.29, 0.24, 1)
        [Normal] _NormalMap("Stone Normal", 2D) = "bump" {}
        _NormalStrength("Normal Strength", Range(0, 2)) = 0.8
        _OcclusionMap("Ambient Occlusion", 2D) = "white" {}
        _OcclusionStrength("AO Strength", Range(0, 1)) = 0.8
        _MossAmount("Moss Amount", Range(0, 1)) = 0.32
        _MossColor("Moss Color", Color) = (0.12, 0.16, 0.075, 1)
        _MossHeight("Moss Height", Float) = 2.4
        _CrackAmount("Crack Amount", Range(0, 1)) = 0.38
        _RuneAmount("Rune Amount", Range(0, 1)) = 0.18
        _RuneColor("Rune Color", Color) = (1, 0.19, 0.035, 1)
        _RuneIntensity("Rune Intensity", Range(0, 4)) = 1
        _RuneSpeed("Rune Speed", Float) = 1
        _Tiling("Texture Tiling", Float) = 1.8
        _Smoothness("Smoothness", Range(0, 1)) = 0.18
        _WallBaseY("Wall Base Height", Float) = 0
        _EmissionIntensity("Emission Intensity", Range(0, 4)) = 2.2
        _MaxBrightness("Maximum Brightness", Range(0.1, 1)) = 0.62
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_OcclusionMap);
            SAMPLER(sampler_OcclusionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _Tint;
                float4 _MossColor;
                float4 _RuneColor;
                float _NormalStrength;
                float _OcclusionStrength;
                float _MossAmount;
                float _MossHeight;
                float _CrackAmount;
                float _RuneAmount;
                float _RuneIntensity;
                float _RuneSpeed;
                float _Tiling;
                float _Smoothness;
                float _WallBaseY;
                float _EmissionIntensity;
                float _MaxBrightness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 tangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                float4 shadowCoord : TEXCOORD5;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = float4(normalInputs.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap) * _Tiling;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                output.shadowCoord = GetShadowCoord(positionInputs);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 albedo = baseSample.rgb * _Tint.rgb;

                half mossNoise = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv * 0.37 + 0.41).g;
                half mossMask = saturate((mossNoise - 0.42h) * 2.2h) *
                    saturate((input.positionWS.y - _WallBaseY) / max(_MossHeight, 0.01));
                albedo = lerp(albedo, albedo * _MossColor.rgb * 2.0h, mossMask * _MossAmount);

                half crackNoise = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv * 3.1).r;
                albedo *= lerp(0.58h, 1.0h, saturate(crackNoise + _CrackAmount * 0.25h));

                half3 normalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv),
                    _NormalStrength);
                half3 bitangentWS = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                half3 normalWS = NormalizeNormalPerPixel(
                    TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS)));

                half occlusionSample = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, input.uv).r;
                half occlusion = lerp(1.0h, occlusionSample, _OcclusionStrength);
                half3 ambient = SampleSH(normalWS) * occlusion;

                Light mainLight = GetMainLight(input.shadowCoord);
                half nDotL = saturate(dot(normalWS, mainLight.direction));
                half3 direct = mainLight.color * nDotL * mainLight.shadowAttenuation;

                half runePulse = (sin(_Time.y * _RuneSpeed + input.positionWS.y * 1.7) * 0.5h + 0.5h);
                half runePattern = step(1.0h - _RuneAmount, frac(sin(dot(floor(input.positionWS.xz * 1.7), float2(12.9898, 78.233))) * 43758.5453));
                half3 emission = _RuneColor.rgb * runePattern * runePulse * _RuneIntensity * _EmissionIntensity * 0.12h;

                half3 color = min(albedo * (ambient + direct) + emission, _MaxBrightness * 2.5h);
                color = MixFog(color, input.fogFactor);
                return half4(color, baseSample.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }

    FallBack "Universal Render Pipeline/Lit"
}