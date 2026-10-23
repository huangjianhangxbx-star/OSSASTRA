Shader "Hidden/ANKALUTE/GameRenderCharacterMask"
{
    Properties { _MainTex ("Sprite or Spine Atlas", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Name "Coverage"
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float alpha : TEXCOORD1; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.alpha = input.color.a;
                return output;
            }
            float4 frag(Varyings input) : SV_Target
            {
                float coverage = saturate(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * input.alpha);
                return float4(coverage, coverage, coverage, coverage);
            }
            ENDHLSL
        }
    }
}
