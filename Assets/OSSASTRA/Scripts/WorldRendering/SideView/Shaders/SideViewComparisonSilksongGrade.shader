Shader "Hidden/Ossastra/SideView/ComparisonSilksongGrade"
{
    Properties
    {
        [NoScaleOffset] _SvRgbLut ("RGB 曲线", 2D) = "white" {}
        [NoScaleOffset] _SvNoiseTexture ("相机颗粒", 2D) = "gray" {}
        _SvBloomEnabled ("启用泛光", Float) = 1
        _SvBloomThreshold ("显示编码域泛光阈值", Float) = 0.35
        _SvBloomIntensity ("泛光强度", Float) = 0.43
        _SvBloomSize ("泛光半径", Float) = 2
        _SvBloomShape ("泛光水平比例", Float) = 0.5
        _SvGradeEnabled ("启用 RGB 曲线和饱和度", Float) = 1
        _SvWorldSaturation ("世界饱和度", Float) = 1.28
        _SvBrightness ("亮度", Float) = 1
        _SvContrast ("对比度", Float) = 1
        _SvNoiseStrength ("相机颗粒强度", Float) = 0
        _SvNoiseTimeSnap ("相机颗粒时间步长", Float) = 0.016666667
        _SvNoiseColor ("相机颗粒通道权重", Vector) = (0.8, 0.9, 1.1, 1)
    }

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

        TEXTURE2D_X(_SvBloomTexture);
        TEXTURE2D(_SvRgbLut);
        TEXTURE2D(_SvNoiseTexture);

        // 由 RenderPass 用 CommandBuffer 每次绑定。材质参数保持跨 Blit 稳定。
        float4 _SvBloomTexelSize;
        float4 _SvSourceTexelSize;
        float4 _SvBloomScaleBias;
        float _SvBloomIteration;

        CBUFFER_START(UnityPerMaterial)
            float4 _SvNoiseColor;
            float _SvBloomEnabled;
            float _SvBloomThreshold;
            float _SvBloomIntensity;
            float _SvBloomSize;
            float _SvBloomShape;
            float _SvGradeEnabled;
            float _SvWorldSaturation;
            float _SvBrightness;
            float _SvContrast;
            float _SvNoiseStrength;
            float _SvNoiseTimeSnap;
        CBUFFER_END

        static const float2 BloomSampleOffsets[8] = {
            float2( 0.44051,  0.10892), float2( 0.47208,  0.49420),
            float2( 0.08946, -0.13972), float2(-0.37576, -0.38780),
            float2(-0.10927,  0.52164), float2(-0.53347, -0.17318),
            float2( 0.45605, -0.41069), float2( 0.16156,  0.63224)
        };
        static const float3 LuminanceWeights = float3(0.220, 0.707, 0.071);

        float3 EncodeDisplayRgb(float3 linearRgb)
        {
            linearRgb = saturate(linearRgb);
            float3 low = linearRgb * 12.92;
            float3 high = 1.055 * pow(linearRgb, 1.0 / 2.4) - 0.055;
            return lerp(low, high, step(0.0031308, linearRgb));
        }

        float3 DecodeDisplayRgb(float3 displayRgb)
        {
            displayRgb = saturate(displayRgb);
            float3 low = displayRgb / 12.92;
            float3 high = pow((displayRgb + 0.055) / 1.055, 2.4);
            return lerp(low, high, step(0.04045, displayRgb));
        }

        float2 ClampSourceUv(float2 uv)
        {
            float2 a = _BlitScaleBias.zw;
            float2 b = a + _BlitScaleBias.xy;
            float2 inset = 0.5 * _SvSourceTexelSize.xy;
            return clamp(uv, min(a, b) + inset, max(a, b) - inset);
        }

        float4 ExtractBloom(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float3 sum = 0.0;
            float2 sampleStep = _SvBloomTexelSize.xy * abs(_BlitScaleBias.xy);
            [unroll] for (int i = 0; i < 8; ++i)
            {
                float2 uv = ClampSourceUv(input.texcoord + BloomSampleOffsets[i] * sampleStep);
                sum += EncodeDisplayRgb(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb);
            }

            float3 bright = max(sum * 0.125 - _SvBloomThreshold, 0.0) * _SvBloomIntensity;
            return float4(bright, 1.0);
        }

        // 泛光 RT 存储原作显示编码域的数值，采样与卷积不做颜色空间转换。
        float3 SampleBloomSource(float2 uv)
        {
            return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, ClampSourceUv(uv)).rgb;
        }

        float4 BlurBloom(float2 uv, float2 sampleStep)
        {
            float3 color = SampleBloomSource(uv) * 0.324;
            color += (SampleBloomSource(uv + sampleStep) + SampleBloomSource(uv - sampleStep)) * 0.232;
            color += (SampleBloomSource(uv + 2.0 * sampleStep) + SampleBloomSource(uv - 2.0 * sampleStep)) * 0.0855;
            color += (SampleBloomSource(uv + 3.0 * sampleStep) + SampleBloomSource(uv - 3.0 * sampleStep)) * 0.0205;
            return float4(color, 1.0);
        }

        float4 BlurVertical(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float radius = _SvBloomSize * 0.5 + _SvBloomIteration;
            return BlurBloom(input.texcoord, float2(0.0, radius * _SvSourceTexelSize.y));
        }

        float4 BlurHorizontal(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float radius = (_SvBloomSize * 0.5 + _SvBloomIteration) * _SvBloomShape;
            return BlurBloom(input.texcoord, float2(radius * _SvSourceTexelSize.x, 0.0));
        }

        float3 ApplyCameraNoise(float3 displayRgb, float2 viewportUv)
        {
            float snap = max(_SvNoiseTimeSnap, 0.0001);
            float2 time = round(_Time.zx / snap) * snap;
            float2 seed = float2(dot(time, float2(127.1, 311.7)), dot(time, float2(269.5, 183.3)));
            float2 offset = (2.0 * frac(sin(seed) * 43758.546875) - 1.0) * 162.185;
            float noise = SAMPLE_TEXTURE2D(_SvNoiseTexture, sampler_LinearRepeat, viewportUv + offset).r - 0.5;
            return saturate(displayRgb * (1.0 + noise * _SvNoiseStrength * _SvNoiseColor.rgb));
        }

        float3 ApplyRgbCurves(float3 displayRgb)
        {
            float3 curved;
            curved.r = SAMPLE_TEXTURE2D(_SvRgbLut, sampler_PointClamp, float2(displayRgb.r, 0.125)).r;
            curved.g = SAMPLE_TEXTURE2D(_SvRgbLut, sampler_PointClamp, float2(displayRgb.g, 0.375)).g;
            curved.b = SAMPLE_TEXTURE2D(_SvRgbLut, sampler_PointClamp, float2(displayRgb.b, 0.625)).b;
            float luminance = dot(curved, LuminanceWeights);
            return lerp(luminance.xxx, curved, _SvWorldSaturation);
        }

        float4 CompositeGrade(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, ClampSourceUv(input.texcoord));
            float2 viewportUv = (input.texcoord - _BlitScaleBias.zw) / _BlitScaleBias.xy;
            float3 displayRgb = EncodeDisplayRgb(source.rgb);
            if (_SvBloomEnabled > 0.5)
            {
                float2 bloomUv = viewportUv * _SvBloomScaleBias.xy + _SvBloomScaleBias.zw;
                float3 bloom = SAMPLE_TEXTURE2D_X(_SvBloomTexture, sampler_LinearClamp, bloomUv).rgb;
                displayRgb = saturate(displayRgb + bloom);
            }
            if (_SvNoiseStrength > 0.0)
                displayRgb = ApplyCameraNoise(displayRgb, viewportUv);
            if (_SvGradeEnabled > 0.5)
                displayRgb = ApplyRgbCurves(displayRgb);

            displayRgb = (displayRgb * _SvBrightness - 0.5) * _SvContrast + 0.5;
            return float4(DecodeDisplayRgb(displayRgb), source.a);
        }
        ENDHLSL

        Pass
        {
            Name "ExtractBloom"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment ExtractBloom
            ENDHLSL
        }

        Pass
        {
            Name "BlurVertical"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment BlurVertical
            ENDHLSL
        }

        Pass
        {
            Name "BlurHorizontal"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment BlurHorizontal
            ENDHLSL
        }

        Pass
        {
            Name "CompositeGrade"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment CompositeGrade
            ENDHLSL
        }
    }
    Fallback Off
}
