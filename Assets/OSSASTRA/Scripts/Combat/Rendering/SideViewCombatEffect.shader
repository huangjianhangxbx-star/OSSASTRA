/*
 * 横版战斗 FX 使用预乘颜色与同场景透明排序。
 * 元数据只标记已有可见发光像素，不登记身体覆盖，也不写相机深度。
 */
Shader "Ossastra/SideView/CombatEffect"
{
    Properties
    {
        _Color ("Layer tint", Color) = (1,1,1,1)
        [NoScaleOffset] _MainTex ("Frame strip", 2D) = "white" {}
        [HideInInspector] _FrameUv ("Frame UV", Vector) = (1,1,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        CBUFFER_START(UnityPerMaterial)
        float4 _Color;
        float4 _FrameUv;
        CBUFFER_END

        struct Attributes { float4 position : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
        struct Varyings { float4 position : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.position = TransformObjectToHClip(input.position.xyz);
            output.color = input.color * _Color;
            output.uv = input.uv * _FrameUv.xy + _FrameUv.zw;
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "SideViewCombatEffect"
            Tags { "LightMode"="SRPDefaultUnlit" }
            ZWrite Off ZTest LEqual
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment ColorFragment

            half4 ColorFragment(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                half alpha = saturate(color.a);
                return half4(color.rgb * alpha, alpha);
            }
            ENDHLSL
        }
        Pass
        {
            Name "SideViewVisibleSurface"
            Tags { "LightMode"="SideViewVisibleSurface" }
            ZWrite On ZTest Always
            Blend One Zero
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment MetadataFragment
            #include "Assets/OSSASTRA/Scripts/WorldRendering/SideView/Shaders/SideViewComparisonOpaqueDepth.hlsl"

            float4 MetadataFragment(Varyings input) : SV_Target
            {
                SideViewRespectOpaqueDepth(input.position);
                clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * input.color.a - .10);
                return float4(0, 0, 0, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

