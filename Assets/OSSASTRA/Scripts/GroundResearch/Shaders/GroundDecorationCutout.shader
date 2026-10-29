// 原创装饰的正常深度裁剪；SpriteRenderer提供_MainTex，不添加置顶或屏幕空间偏移。
Shader "Ossastra/GroundWorkshop/DecorationCutout"
{
 Properties
 {
  [PerRendererData] _MainTex("装饰Sprite",2D)="white"{}
  _Color("颜色",Color)=(1,1,1,1)
  _Cutoff("透明裁剪",Range(0,1))=.35
 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" "CanUseSpriteAtlas"="True" }
  Pass
  {
   Name "GroundDecoration"
   Cull Off ZWrite On ZTest LEqual Blend Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
   CBUFFER_START(UnityPerMaterial)
    float4 _Color;
    float _Cutoff;
   CBUFFER_END
   struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
   struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
   V vert(A input)
   {
    V output; output.positionCS=TransformObjectToHClip(input.positionOS.xyz);output.uv=input.uv;output.color=input.color*_Color;return output;
   }
   half4 frag(V input):SV_Target
   {
    half4 color=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv)*input.color;
    clip(color.a-_Cutoff);return half4(color.rgb,1);
   }
   ENDHLSL
  }
 }
}
