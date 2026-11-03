// 功能：从刚重建的合法世界深度附件解析MSAA到独立R32纹理，不读取旧CameraDepthTexture。
// 边界：支持本机URP的1/2/4/8采样及反向Z；材质私有关键字不改URP全局关键字。
Shader "Hidden/Ossastra/WorldRendering/LegalDepthCopy"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            Name "ResolveFreshLegalDepth"
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ _WORLD_LOOK_DEPTH_MSAA_2 _WORLD_LOOK_DEPTH_MSAA_4 _WORLD_LOOK_DEPTH_MSAA_8
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #if defined(_WORLD_LOOK_DEPTH_MSAA_2)
                #define WORLD_LOOK_SAMPLES 2
            #elif defined(_WORLD_LOOK_DEPTH_MSAA_4)
                #define WORLD_LOOK_SAMPLES 4
            #elif defined(_WORLD_LOOK_DEPTH_MSAA_8)
                #define WORLD_LOOK_SAMPLES 8
            #else
                #define WORLD_LOOK_SAMPLES 1
            #endif
            #if WORLD_LOOK_SAMPLES == 1
                TEXTURE2D_X_FLOAT(_WorldLookDepthSource);
                SAMPLER(sampler_WorldLookDepthSource);
            #elif defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                Texture2DMSArray<float, WORLD_LOOK_SAMPLES> _WorldLookDepthSource;
            #else
                Texture2DMS<float, WORLD_LOOK_SAMPLES> _WorldLookDepthSource;
            #endif
            float4 _WorldLookDepthSourceSize;
            float Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                #if WORLD_LOOK_SAMPLES == 1
                    return SAMPLE_TEXTURE2D_X(_WorldLookDepthSource, sampler_WorldLookDepthSource, input.texcoord).r;
                #else
                    int2 coordinate = min((int2)(input.texcoord * _WorldLookDepthSourceSize.xy), (int2)_WorldLookDepthSourceSize.xy - 1);
                    // 与安装URP CopyDepth一致，取覆盖样本中的远值，避免深度解析凭空扩张近景。
                    #if UNITY_REVERSED_Z
                        float depth = 1;
                    #else
                        float depth = 0;
                    #endif
                    [unroll] for (int sampleIndex = 0; sampleIndex < WORLD_LOOK_SAMPLES; ++sampleIndex)
                    {
                        #if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                            float sampleDepth = _WorldLookDepthSource.Load(int3(coordinate, unity_StereoEyeIndex), sampleIndex);
                        #else
                            float sampleDepth = _WorldLookDepthSource.Load(coordinate, sampleIndex);
                        #endif
                        #if UNITY_REVERSED_Z
                            depth = min(depth, sampleDepth);
                        #else
                            depth = max(depth, sampleDepth);
                        #endif
                    }
                    return depth;
                #endif
            }
            ENDHLSL
        }
    }
    Fallback Off
}
