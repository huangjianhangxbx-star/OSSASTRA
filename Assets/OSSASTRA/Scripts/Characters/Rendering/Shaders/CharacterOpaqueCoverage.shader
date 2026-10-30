// 功能：仅alpha≈1的装备像素写真实环境深度；ColorMask0，不改PMA颜色、原alpha或透明队列。
// 边界：主相机显式启用；普通Spine保留原深度，竖直PMA变体复用本轮已验证角色深度公式。
Shader "Ossastra/Characters/OpaqueCoverage"
{
 Properties
 {
  [NoScaleOffset] _MainTex("原始Spine纹理",2D)="black"{}
  [Toggle(_STRAIGHT_ALPHA_INPUT)] _StraightAlphaInput("Straight Alpha Texture",Int)=1
  [HideInInspector] _StencilRef("Stencil Reference",Float)=1
  [HideInInspector] _StencilComp("Stencil Comparison",Float)=8
  [HideInInspector] _VisualDepthFoot("Owner foot",Vector)=(0,0,0,1)
  [HideInInspector] _VisualDepthNormal("Horizontal view normal",Vector)=(0,0,1,0)
  [HideInInspector] _VisualDepthEnabled("Upright source only",Float)=0
  [HideInInspector] _CharacterCoverageEnabled("Main camera only",Float)=0
  [HideInInspector] _CharacterCoverageMinimum("Fully opaque only",Float)=.999
 }
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Geometry+499" "RenderType"="TransparentCutout"}
  Cull Off ZWrite On ZTest LEqual ColorMask 0 Blend Off
  Stencil{Ref[_StencilRef] Comp[_StencilComp] Pass Keep}
  HLSLINCLUDE
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
  CBUFFER_START(UnityPerMaterial)
   float4 _VisualDepthFoot,_VisualDepthNormal;float _VisualDepthEnabled,_CharacterCoverageEnabled,_CharacterCoverageMinimum;
  CBUFFER_END
  #include "Assets/OSSASTRA/Scripts/Characters/Rendering/Shaders/CharacterUprightDepthCommon.hlsl"
  struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
  struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float alpha:COLOR;float eyeOffset:TEXCOORD1;};
  V Vert(A input)
  {
   V output;float3 world=TransformObjectToWorld(input.positionOS.xyz);output.positionCS=TransformWorldToHClip(world);
   output.uv=input.uv;output.alpha=input.color.a;output.eyeOffset=CharacterVisualEyeOffset(world);return output;
  }
  float Frag(V input):SV_Depth
  {
   clip(_CharacterCoverageEnabled-.5);
   // PMA and straight-input modes change RGB only. Original Spine alpha is precisely texture*vertex.
   float alpha=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv).a*input.alpha;
   clip(alpha-_CharacterCoverageMinimum);
   return CharacterVisualDeviceDepth(input.positionCS,input.eyeOffset);
  }
  ENDHLSL
  Pass
  {
   Name "OpaqueCoverage" Tags{"LightMode"="SRPDefaultUnlit"}
   HLSLPROGRAM
   #pragma target 4.5
   #pragma multi_compile_local _ _STRAIGHT_ALPHA_INPUT
   #pragma vertex Vert
   #pragma fragment Frag
   ENDHLSL
  }
  Pass
  {
   Name "DepthOnly" Tags{"LightMode"="DepthOnly"}
   HLSLPROGRAM
   #pragma target 4.5
   #pragma multi_compile_local _ _STRAIGHT_ALPHA_INPUT
   #pragma vertex Vert
   #pragma fragment Frag
   ENDHLSL
  }
 }
}
