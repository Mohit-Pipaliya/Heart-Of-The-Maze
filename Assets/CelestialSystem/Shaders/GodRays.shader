Shader "CelestialSystem/GodRays"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        Cull Off

        // ─────────────────────────────────────────────────────────────
        //  Pass 0: Radial Blur from Sun Screen Center
        // ─────────────────────────────────────────────────────────────
        Pass
        {
            Name "GodRays_RadialBlur"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBlur
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // Uniforms set by GodRaysRendererFeature
            float4 _SunScreenPos;   // xy = screen UV (0..1), z = on-screen mask (0..1), w = altitude/vis factor (0..1)
            float4 _GodRayParams;   // x = density, y = weight, z = decay, w = exposure
            int    _SampleCount;    // number of samples along ray (16-32)
            half4  _RayTint;        // tint color of god rays

            half4 FragBlur(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 sunUV = _SunScreenPos.xy;
                float onScreen = _SunScreenPos.z * _SunScreenPos.w;

                if (onScreen <= 0.001)
                    return half4(0.0, 0.0, 0.0, 0.0);

                // Direction vector from current pixel towards sun screen position
                float2 deltaUV = (sunUV - uv);
                float distToSun = length(deltaUV);

                // Normalize step by sample count and density
                int samples = clamp(_SampleCount, 8, 32);
                deltaUV *= (1.0 / (float)samples) * _GodRayParams.x;

                // Initial sample at current UV
                half3 accumColor = half3(0.0, 0.0, 0.0);
                float illuminationDecay = 1.0;
                float2 currentUV = uv;

                // Thresholding: we only want high luminance pixels (sun / bright sky) to cast rays
                // Luminance threshold = 0.6
                for (int i = 0; i < samples; i++)
                {
                    currentUV += deltaUV;

                    // Clamp to screen bounds
                    if (currentUV.x < 0.0 || currentUV.x > 1.0 || currentUV.y < 0.0 || currentUV.y > 1.0)
                        break;

                    half3 sampleColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, currentUV).rgb;

                    // Extract high luminance highlights
                    half lum = dot(sampleColor, half3(0.299, 0.587, 0.114));
                    half highlight = saturate((lum - 0.55) / 0.45);
                    half3 brightSample = sampleColor * highlight;

                    accumColor += brightSample * (illuminationDecay * _GodRayParams.y);
                    illuminationDecay *= _GodRayParams.z;
                }

                // Apply exposure, tint, and screen boundary fade
                half3 finalRays = accumColor * (_GodRayParams.w * onScreen) * _RayTint.rgb;

                return half4(finalRays, 1.0);
            }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────
        //  Pass 1: Additive Composite onto Active Color
        // ─────────────────────────────────────────────────────────────
        Pass
        {
            Name "GodRays_Composite"

            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half4 FragComposite(Varyings input) : SV_Target
            {
                half3 rays = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord).rgb;
                return half4(rays, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

