Shader "HeartOfTheMaze/RiverWater"
{
    Properties
    {
        _DeepColor ("Deep Color", Color) = (0.012, 0.075, 0.095, 1)
        _ShallowColor ("Shallow Color", Color) = (0.04, 0.28, 0.30, 1)
        _HorizonColor ("Sky Reflection", Color) = (0.62, 0.78, 0.82, 1)
        _FoamColor ("Foam", Color) = (0.86, 0.94, 0.92, 1)
        _SpecularColor ("Sun Glint", Color) = (1, 0.97, 0.88, 1)
        _FresnelPower ("Fresnel", Range(0.6, 8)) = 3.4
        _RippleScale ("Ripple Scale", Float) = 0.38
        _RippleSpeed ("Ripple Speed", Float) = 0.45
        _NormalStrength ("Ripple Strength", Range(0, 1.5)) = 0.55
        _FoamStrength ("Crest Foam", Range(0, 1)) = 0.8
        _Gloss ("Specular Power", Range(8, 256)) = 110
        _WaveShade ("Wave Shade", Range(0, 2)) = 1.15
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half4 _ShallowColor;
                half4 _HorizonColor;
                half4 _FoamColor;
                half4 _SpecularColor;
                half _FresnelPower;
                half _RippleScale;
                half _RippleSpeed;
                half _NormalStrength;
                half _FoamStrength;
                half _Gloss;
                half _WaveShade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fog : TEXCOORD2;
                float foam : TEXCOORD3;
            };

            float2 Ripple(float2 p, float t)
            {
                float s1 = sin(p.x * 1.70 + t * 1.15) + sin(p.y * 1.35 - t * 0.85);
                float s2 = sin((p.x + p.y) * 2.40 - t * 1.55);
                float c1 = cos(p.y * 1.70 + t * 1.15) + cos(p.x * 1.20 + t * 0.70);
                float c2 = cos((p.x - p.y) * 2.10 + t * 1.35);
                return float2(s1 + s2, c1 + c2);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fog = ComputeFogFactor(p.positionCS.z);
                o.foam = v.color.a;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float t = _Time.y * _RippleSpeed;
                float2 p1 = i.positionWS.xz * _RippleScale;
                float2 p2 = i.positionWS.xz * (_RippleScale * 2.35) + float2(4.2, 1.7);
                float2 r = Ripple(p1, t) * 0.22 + Ripple(p2, t * 1.35) * 0.10;
                n = normalize(n + float3(r.x, 0.0, r.y) * _NormalStrength);

                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);
                float3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float ndotl = saturate(dot(n, lightDir));
                float3 halfDir = normalize(lightDir + viewDir);
                float spec = pow(saturate(dot(n, halfDir)), _Gloss);
                float fresnel = pow(saturate(1.0 - dot(n, viewDir)), _FresnelPower);

                float facing = saturate(n.y);
                float3 col = lerp(_DeepColor.rgb, _ShallowColor.rgb, facing);
                col *= lerp(0.72, 1.15, ndotl) * _WaveShade;
                col = lerp(col, _HorizonColor.rgb, fresnel * 0.78);

                float crest = saturate(i.foam);
                col = lerp(col, _FoamColor.rgb, crest * _FoamStrength * (0.45 + fresnel));

                float3 ambient = SampleSH(n);
                col *= mainLight.color * 0.85 + ambient * 0.55;
                col += _SpecularColor.rgb * spec * mainLight.color * (0.55 + fresnel);

                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
