Shader "Hidden/Ossastra/SideView/ComparisonSilksongLayers"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

        TEXTURE2D(_SvHeroLightTexture);
        TEXTURE2D(_SvVignetteTexture);
        TEXTURE2D_X(_SvDarknessCutoutTexture);

        // RenderPass 按当前相机写入。Rect 为 Unity viewport 左下原点 (x,y,w,h)。
        float2 _SvLayerTexelStep;
        float4 _SvHeroLightViewportRect;
        float4 _SvHeroLightTintGamma;
        float4 _SvVignetteViewportRect;
        float4 _SvVignetteTintGamma;
        float4 _SvDarknessCutoutScaleBias;
        float _SvDarknessCutoutEnabled;

        float2 ViewportSampleUv(float2 sourceUv)
        {
            return (sourceUv - _BlitScaleBias.zw) / _BlitScaleBias.xy;
        }

        float2 BottomLeftViewportUv(float2 viewportUv)
        {
            #if UNITY_UV_STARTS_AT_TOP
                viewportUv.y = 1.0 - viewportUv.y;
            #endif
            return viewportUv;
        }

        bool IsSpriteUvInside(float2 uv)
        {
            return all(uv >= 0.0) && all(uv <= 1.0);
        }

        float3 FarDisplaySample(float2 uv)
        {
            float3 linearRgb = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
            return LinearToSRGB(saturate(linearRgb));
        }

        float4 FarBlur(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = input.texcoord;
            float2 stepUv = _SvLayerTexelStep;
            // 原作 Hollow Knight/Light Blur 有偏移的 5 抽头核。横向之后纵向。
            float3 blurred = FarDisplaySample(uv) * 0.227027;
            blurred += (FarDisplaySample(uv + 1.384615 * stepUv) + FarDisplaySample(uv - 0.615385 * stepUv)) * 0.316261;
            blurred += (FarDisplaySample(uv + 3.230769 * stepUv) + FarDisplaySample(uv - 2.769231 * stepUv)) * 0.070270;
            return float4(SRGBToLinear(saturate(blurred)), 1.0);
        }

        float4 HeroLight(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
            if (any(_SvHeroLightViewportRect.zw <= 0.0))
                return source;

            float2 viewportUv = BottomLeftViewportUv(ViewportSampleUv(input.texcoord));
            float2 spriteUv = (viewportUv - _SvHeroLightViewportRect.xy) / _SvHeroLightViewportRect.zw;
            if (!IsSpriteUvInside(spriteUv))
                return source;

            float4 sprite = SAMPLE_TEXTURE2D(_SvHeroLightTexture, sampler_LinearClamp, spriteUv);
            float coverage = saturate(sprite.a * _SvHeroLightTintGamma.a);
            if (coverage < 0.01)
                return source;

            float3 world = LinearToSRGB(saturate(source.rgb));
            float3 light = LinearToSRGB(saturate(sprite.rgb)) * _SvHeroLightTintGamma.rgb;
            // 原作 LDR UNorm 输出合并在混合前钳制片元值。
            float3 over = saturate(world + 2.0 * light - 1.0);
            return float4(SRGBToLinear(lerp(world, over, coverage)), source.a);
        }

        float4 Vignette(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
            if (any(_SvVignetteViewportRect.zw <= 0.0))
                return source;

            float2 sampleUv = ViewportSampleUv(input.texcoord);
            float2 viewportUv = BottomLeftViewportUv(sampleUv);
            float2 spriteUv = (viewportUv - _SvVignetteViewportRect.xy) / _SvVignetteViewportRect.zw;
            if (!IsSpriteUvInside(spriteUv))
                return source;

            float4 sprite = SAMPLE_TEXTURE2D(_SvVignetteTexture, sampler_LinearClamp, spriteUv);
            float coverage = saturate(sprite.a * _SvVignetteTintGamma.a);
            if (_SvDarknessCutoutEnabled > 0.5)
            {
                float2 cutoutUv = sampleUv * _SvDarknessCutoutScaleBias.xy + _SvDarknessCutoutScaleBias.zw;
                float cutout = SAMPLE_TEXTURE2D_X(_SvDarknessCutoutTexture, sampler_LinearClamp, cutoutUv).r;
                coverage *= 1.0 - 0.8 * saturate(cutout);
            }
            if (coverage <= 0.0)
                return source;

            float3 world = LinearToSRGB(saturate(source.rgb));
            float3 darkness = saturate(LinearToSRGB(saturate(sprite.rgb)) * _SvVignetteTintGamma.rgb);
            return float4(SRGBToLinear(lerp(world, darkness, coverage)), source.a);
        }

        float4 FarBackdropCopy(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float3 background = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord).rgb;
            return float4(background, 1.0);
        }
        Varyings FarBackdropVertex(Attributes input)
        {
            Varyings output = Vert(input);
            output.positionCS.z = UNITY_RAW_FAR_CLIP_VALUE * output.positionCS.w;
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "FarBlur"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FarBlur
            ENDHLSL
        }

        Pass
        {
            Name "HeroLight"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment HeroLight
            ENDHLSL
        }

        Pass
        {
            Name "Vignette"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Vignette
            ENDHLSL
        }

        Pass
        {
            Name "FarBackdropCopy"
            ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FarBackdropVertex
            #pragma fragment FarBackdropCopy
            ENDHLSL
        }
    }
    Fallback Off
}
