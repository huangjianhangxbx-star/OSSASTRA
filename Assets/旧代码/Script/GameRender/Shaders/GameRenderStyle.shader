Shader "Hidden/ANKALUTE/GameRenderStyle"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float _InkStrength;
        float _DarkThreshold;
        float _DarkSpread;
        float _OuterLine;
        float _InnerLine;
        float _HighlightLift;
        float _RoughEdge;
        float _GrainStrength;
        float _GrainPixelSize;
        float _GrainFrame;
        float _DebugClassification;
        TEXTURE2D_X(_GameRenderProtectedCoverage);

        float3 ReadColor(float2 uv)
        {
            return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(uv)).rgb;
        }

        float Luma(float3 rgb)
        {
            return dot(max(rgb, 0), float3(0.2126, 0.7152, 0.0722));
        }

        float4 StyleFragment(Varyings input) : SV_Target
        {
            float2 uv = input.texcoord;
            float3 color = ReadColor(uv);
            float protectedCoverage = SAMPLE_TEXTURE2D_X(_GameRenderProtectedCoverage,
                sampler_LinearClamp, uv).r;
            if (_DebugClassification > 0.5)
                return float4(protectedCoverage, 0, 0, 1);
            float lightness = Luma(color);
            float2 pixel = 1.0 / max(_BlitTextureSize.xy, float2(1, 1));
            float left = Luma(ReadColor(uv - float2(pixel.x, 0)));
            float right = Luma(ReadColor(uv + float2(pixel.x, 0)));
            float up = Luma(ReadColor(uv + float2(0, pixel.y)));
            float down = Luma(ReadColor(uv - float2(0, pixel.y)));

            float minNeighbour = min(min(left, right), min(up, down));
            float maxNeighbour = max(max(left, right), max(up, down));
            float darkCore = 1.0 - smoothstep(_DarkThreshold - 0.025,
                                               _DarkThreshold + 0.025, lightness);
            float spreadSeed = 1.0 - smoothstep(_DarkThreshold - 0.015,
                                                _DarkThreshold + 0.015, minNeighbour);
            float2 gradient = float2(right - left, up - down);
            float corner = saturate((abs(gradient.x) + abs(gradient.y)) * 3.0);
            // Directional, hard-sided wedges grow only from existing dark neighbours.
            float wedge = step(abs(gradient.y), abs(gradient.x) * 1.7 + 0.004);
            float spread = saturate(spreadSeed - darkCore) * corner *
                           lerp(0.72, 1.0, wedge) * _DarkSpread;
            float irregularity = (saturate(abs(right - left) * 3.0) -
                                   saturate(abs(up - down) * 3.0)) * _RoughEdge * 0.025;
            float outerEdge = saturate((maxNeighbour - lightness - 0.045 + irregularity) * 3.0) *
                              saturate(_OuterLine);
            float innerEdge = saturate((abs(right - left) + abs(up - down) - 0.07) * 1.5) *
                              saturate(_InnerLine);
            float highlightGuard = 1.0 - smoothstep(0.45, 0.9, lightness);
            float ink = saturate((darkCore * 0.45 + spread + outerEdge + innerEdge) *
                                 _InkStrength * highlightGuard);
            float3 styled = lerp(color, color * 0.08, ink);
            float highlight = smoothstep(0.55, 1.25, lightness) * _HighlightLift;
            styled += highlight * (1.0 - saturate(styled));
            return float4(lerp(styled, color, saturate(protectedCoverage)), 1);
        }

        float Hash(float2 value)
        {
            return frac(sin(dot(value, float2(12.9898, 78.233))) * 43758.5453);
        }

        float4 GrainFragment(Varyings input) : SV_Target
        {
            float4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
            float2 pixel = floor(input.texcoord * _ScreenParams.xy / max(_GrainPixelSize, 0.5));
            float noise = Hash(pixel + _GrainFrame * float2(17.0, 29.0)) - 0.5;
            float midtone = 1.0 - smoothstep(0.82, 1.35, Luma(color.rgb));
            float protectedCoverage = SAMPLE_TEXTURE2D_X(_GameRenderProtectedCoverage,
                sampler_LinearClamp, input.texcoord).r;
            color.rgb = max(0, color.rgb + noise * _GrainStrength * midtone *
                               (1.0 - saturate(protectedCoverage)));
            return color;
        }
        ENDHLSL

        Pass
        {
            Name "World Style"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment StyleFragment
            ENDHLSL
        }
        Pass
        {
            Name "World Grain"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment GrainFragment
            ENDHLSL
        }
    }
}
