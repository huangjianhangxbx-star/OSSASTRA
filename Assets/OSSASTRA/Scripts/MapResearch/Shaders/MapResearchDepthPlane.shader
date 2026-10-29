/* 地图研究的局部深度材质：保留实例顶点透明度（含Spine换面显隐），
 * 透明轮廓外不写深度；只用于样地材质副本，不替换原角色/全局材质。
 * 这是Unity混合遮挡适配，不冒称原作渲染实现。 */
Shader "Ossastra/MapResearch/DepthPlane"
{
    Properties
    {
        [MainTexture] _MainTex ("Texture", 2D) = "white" {}
        [MainColor] _Color ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Depth Alpha Cutoff", Range(0.001,1)) = 0.06
        [Toggle] _PmaInput ("Premultiplied Texture Input", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite On
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Cutoff;
                half _PmaInput;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                // 图集来源决定PMA，不能对直通alpha图片再做一次预乘。
                if (_PmaInput > 0.5h) texel.rgb /= max(texel.a, 0.001h);
                half4 color = texel * input.color;
                clip(color.a - _Cutoff);
                return color;
            }
            ENDHLSL
        }
    }
}
