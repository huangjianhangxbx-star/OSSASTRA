Shader "Hidden/Ossastra/SideView/ComparisonDarkness"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        TEXTURE2D(_SvDarknessSpriteTexture);
        TEXTURE2D_X(_SvDarknessCutoutTexture);
        TEXTURE2D_X(_SvDarknessOverlaysTexture);

        float4x4 _SvDarknessGpuVP;
        float4 _SvDarknessMaskTexelSize;
        float4 _SvDarknessTintGamma;
        float _SvDarknessAlphaOnly;
        float _SvDarknessFeather;

        struct DarknessAttributes
        {
            float3 positionOS : POSITION;
            float2 uv : TEXCOORD0;
        };

        struct DarknessVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        DarknessVaryings DarknessVertex(DarknessAttributes input)
        {
            DarknessVaryings output;
            float3 world = TransformObjectToWorld(input.positionOS);
            output.positionCS = mul(_SvDarknessGpuVP, float4(world, 1.0));
            output.uv = input.uv;
            return output;
        }

        float4 CutoutFragment(DarknessVaryings input) : SV_Target
        {
            float spriteAlpha = SAMPLE_TEXTURE2D(_SvDarknessSpriteTexture, sampler_LinearClamp, input.uv).a;
            float coverage = saturate(spriteAlpha * _SvDarknessTintGamma.a);
            float alphaOnly = saturate(_SvDarknessAlphaOnly);
            // 原作遮罩相机 _PreviewLerp=1，RGB 取作者通道，纹理只贡献 alpha。
            return float4(saturate(_SvDarknessTintGamma.rgb) * coverage * (1.0 - alphaOnly), coverage * alphaOnly);
        }

        float4 SampleCutout(DarknessVaryings input)
        {
            float2 uv = input.positionCS.xy * _SvDarknessMaskTexelSize.xy;
            return SAMPLE_TEXTURE2D_X(_SvDarknessCutoutTexture, sampler_LinearClamp, uv);
        }

        float4 SampleOverlaySprite(float2 uv)
        {
            if (_SvDarknessFeather > 0.5)
            {
                // 16 列原图的 alpha 从 255 均匀递减到 0，每行相同。
                float alpha = 1.0 - saturate((16.0 * uv.x - 0.5) / 15.0);
                return float4(0.0, 0.0, 0.0, alpha);
            }
            return SAMPLE_TEXTURE2D(_SvDarknessSpriteTexture, sampler_LinearClamp, uv);
        }

        float4 OverlayColor(float3 linearSpriteRgb, float coverage)
        {
            float3 rgb = saturate(LinearToSRGB(saturate(linearSpriteRgb)) * _SvDarknessTintGamma.rgb);
            return float4(rgb * coverage, coverage);
        }

        float4 SceneBorderFragment(DarknessVaryings input) : SV_Target
        {
            float4 sprite = SampleOverlaySprite(input.uv);
            float coverage = sprite.a * saturate(_SvDarknessTintGamma.a) * (1.0 - SampleCutout(input).a);
            return OverlayColor(sprite.rgb, coverage);
        }

        float4 MaskBlackoutFragment(DarknessVaryings input) : SV_Target
        {
            float4 sprite = SampleOverlaySprite(input.uv);
            float4 cutout = SampleCutout(input);
            float vertexAlpha = saturate(_SvDarknessTintGamma.a);
            float coverage = saturate(sprite.a * lerp(vertexAlpha, 1.0 - vertexAlpha, cutout.b) + cutout.g);
            return OverlayColor(sprite.rgb, coverage);
        }

        float4 CompositeOverlaysFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
            float2 overlayUv = (input.texcoord - _BlitScaleBias.zw) / _BlitScaleBias.xy;
            float4 overlays = SAMPLE_TEXTURE2D_X(_SvDarknessOverlaysTexture, sampler_LinearClamp, overlayUv);
            if (overlays.a <= 0.0)
                return source;

            float3 world = LinearToSRGB(saturate(source.rgb));
            float3 composite = saturate(overlays.rgb + world * (1.0 - overlays.a));
            return float4(SRGBToLinear(composite), source.a);
        }
        ENDHLSL

        Pass
        {
            Name "Cutout"
            Blend One OneMinusSrcColor
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DarknessVertex
            #pragma fragment CutoutFragment
            ENDHLSL
        }

        Pass
        {
            Name "IS_SCENE_BORDER"
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DarknessVertex
            #pragma fragment SceneBorderFragment
            ENDHLSL
        }

        Pass
        {
            Name "IS_MASK_BLACKOUT"
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DarknessVertex
            #pragma fragment MaskBlackoutFragment
            ENDHLSL
        }

        Pass
        {
            Name "CompositeOverlays"
            Blend Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment CompositeOverlaysFragment
            ENDHLSL
        }
    }
    Fallback Off
}
