/*
 * 横版战斗 FX 使用预乘颜色与同场景透明排序。
 * 元数据只标记已有可见发光像素，不登记身体覆盖，也不写相机深度。
 */
Shader "Ossastra/SideView/CombatEffect"
{
    Properties { _Color ("Layer tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
        float4 _Color;
        CBUFFER_END

        struct Attributes { float4 position : POSITION; float4 color : COLOR; };
        struct Varyings { float4 position : SV_POSITION; float4 color : COLOR; };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.position = TransformObjectToHClip(input.position.xyz);
            output.color = input.color * _Color;
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
                half alpha = saturate(input.color.a);
                return half4(input.color.rgb * alpha, alpha);
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
                clip(input.color.a - .10);
                return float4(0, 0, 0, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

