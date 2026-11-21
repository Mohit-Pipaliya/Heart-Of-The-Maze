Shader "Hidden/OptiWaterUnderwater"
{
    Properties
    {
        _UnderwaterOpacity("Underwater Opacity (deep color ratio)", Range(0.0, 1.0)) = 0.9
        _UnderwaterDarken("Underwater Color Darken", Range(0.0, 1.0)) = 0.4
        _DepthFeather("Water Level Feather (m, depth precision band)", Range(0.0, 2.0)) = 0.3

        _DistortStrength("Distort Strength (uv offset)", Range(0.0, 0.05)) = 0.006
        _DistortSpeed("Distort Speed", Range(0.0, 5.0)) = 1.5
        _DistortScale("Distort Scale (wave density)", Range(1.0, 100.0)) = 35.0

        [HideInInspector] _UWStencilComp("UW Stencil Comp", Int) = 3
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }

        Pass
        {
            Name "UnderwaterEffect"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            Stencil
            {
                Ref 0
                ReadMask 1
                Comp [_UWStencilComp]
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_UnderwaterSceneColor);
            SAMPLER(sampler_UnderwaterSceneColor);

            float4 _UnderwaterColor;
            float  _UnderwaterOpacity;
            float  _UnderwaterDarken;
            float  _UnderwaterDistortOn;
            float  _DistortStrength;
            float  _DistortSpeed;
            float  _DistortScale;
            float  _DepthFeather;
            float  _UnderwaterWaterLevel;
            float  _UnderwaterDebugMode;

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings OUT;
                float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
                OUT.uv = uv;
#if UNITY_UV_STARTS_AT_TOP
                OUT.uv.y = 1.0 - uv.y;
#endif
                OUT.positionHCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                return OUT;
            }

            float4 Frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;

                if (_UnderwaterDebugMode > 8.5)
                    return float4(1.0, 0.0, 0.0, 1.0);

                float rawDepth = SampleSceneDepth(uv);
                if (rawDepth < 0.0001 || rawDepth > 0.9999)
                    discard;

                float3 terrainWorld = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float terrainY = terrainWorld.y;

                if (_UnderwaterDebugMode > 4.5 && _UnderwaterDebugMode < 5.5)
                {
                    float t = saturate((terrainY + 50.0) / 200.0);
                    return float4(t, 0.15, 1.0 - t, 1.0);
                }

                float feather = max(_DepthFeather, 1e-4);
                float weight = 1.0 - smoothstep(_UnderwaterWaterLevel - feather, _UnderwaterWaterLevel, terrainY);

                if (_UnderwaterDebugMode > 7.5 && _UnderwaterDebugMode < 8.5)
                {
                    return weight > 0.001 ? float4(0.0, 1.0, 0.0, 1.0) : float4(0.12, 0.12, 0.12, 1.0);
                }

                if (weight <= 0.001)
                    discard;

                if (_UnderwaterDebugMode > 1.5 && _UnderwaterDebugMode < 2.5)
                    return float4(1.0, 0.0, 1.0, 1.0);

                float3 deepCol = _UnderwaterColor.rgb * _UnderwaterDarken;

                if (_UnderwaterDistortOn > 0.5)
                {
                    float2 distort = float2(
                        sin(uv.y * _DistortScale + _Time.y * _DistortSpeed),
                        cos(uv.x * _DistortScale * 0.85 + _Time.y * _DistortSpeed * 0.8)
                    ) * _DistortStrength * weight;

                    float3 sceneCol = SAMPLE_TEXTURE2D(_UnderwaterSceneColor, sampler_UnderwaterSceneColor, uv + distort).rgb;
                    float3 col = lerp(sceneCol, deepCol, _UnderwaterOpacity);
                    return float4(col, weight);
                }

                return float4(deepCol, _UnderwaterOpacity * weight);
            }
            ENDHLSL
        }
    }
}
