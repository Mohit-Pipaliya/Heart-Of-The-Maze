Shader "MazeSystem/FogWall"
{
    Properties
    {
        _Color ("Fog Color", Color) = (0.62, 0.68, 0.74, 1)
        _Coverage ("Coverage", Range(0, 1)) = 0
        _NoiseScale ("Noise Scale", Float) = 3.5
        _Speed ("Drift Speed", Float) = 0.35
        _Softness ("Softness", Range(0.01, 1)) = 0.45
        _EdgeFade ("Edge Fade", Range(0.001, 0.5)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "MazeFogWall"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Coverage;
                float _NoiseScale;
                float _Speed;
                float _Softness;
                float _EdgeFade;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float sum = 0.0;
                float amp = 0.5;
                for (int i = 0; i < 4; i++)
                {
                    sum += ValueNoise(p) * amp;
                    p = p * 2.03 + float2(17.1, 9.7);
                    amp *= 0.5;
                }
                return sum;
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 drift = float2(_Time.y * _Speed, _Time.y * _Speed * 0.37);
                float2 p = i.uv * _NoiseScale + drift;
                float n = Fbm(p);
                float n2 = Fbm(p * 1.7 + float2(31.4, 11.8) - drift * 0.6);
                float density = saturate(lerp(n, n2, 0.45) * 1.55);

                float edge =
                    smoothstep(0.0, _EdgeFade, i.uv.x) *
                    smoothstep(1.0, 1.0 - _EdgeFade, i.uv.x) *
                    smoothstep(0.0, _EdgeFade, i.uv.y) *
                    smoothstep(1.0, 1.0 - _EdgeFade, i.uv.y);

                float soft = smoothstep(1.0 - _Softness, 1.0, density);
                float alpha = saturate(_Coverage * edge * lerp(0.28, 1.0, soft));

                float3 color = _Color.rgb * lerp(0.72, 1.12, density);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
