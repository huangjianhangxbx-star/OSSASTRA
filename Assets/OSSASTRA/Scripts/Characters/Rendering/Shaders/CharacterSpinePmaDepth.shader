// Spine/Skeleton的独立PMA变体：保留Normal颜色/keyword/Stencil/Blend/ZWrite及原Caster。
// 不把透明装备改成AlphaTest，也不套用身体材质的Cutoff或直通alpha规则。
Shader "Ossastra/Characters/UprightSpinePma"
{
 Properties
 {
  _Cutoff("Shadow alpha cutoff",Range(0,1))=.1
  [NoScaleOffset] _MainTex("Main Texture",2D)="black"{}
  [Toggle(_STRAIGHT_ALPHA_INPUT)] _StraightAlphaInput("Straight Alpha Texture",Int)=1
  [HideInInspector] _StencilRef("Stencil Reference",Float)=1
  [HideInInspector][Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp("Stencil Comparison",Float)=8
  [HideInInspector] _VisualDepthFoot("Owner foot",Vector)=(0,0,0,1)
  [HideInInspector] _VisualDepthNormal("Horizontal view normal",Vector)=(0,0,1,0)
  [HideInInspector] _VisualDepthEnabled("Gameplay camera only",Float)=0
 }
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane"}
  Cull Off ZWrite Off ZTest LEqual Blend One OneMinusSrcAlpha
  Stencil{Ref[_StencilRef] Comp[_StencilComp] Pass Keep}
  Pass
  {
   Name "Normal"
   Tags{"LightMode"="SRPDefaultUnlit"}
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex Vert
   #pragma fragment Frag
   // runtime variant没有独立持久Material；保留两种输入避免Player按源Shader错误裁掉keyword。
   #pragma multi_compile_local _ _STRAIGHT_ALPHA_INPUT
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #define USE_URP
   #include "Assets/Spine/Runtime/spine-unity/Shaders/CGIncludes/Spine-Common.cginc"
   TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
   CBUFFER_START(UnityPerMaterial)
    float4 _VisualDepthFoot;float4 _VisualDepthNormal;float _VisualDepthEnabled;
   CBUFFER_END
   #include "Assets/OSSASTRA/Scripts/Characters/Rendering/Shaders/CharacterUprightDepthCommon.hlsl"
   struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;float eyeOffset:TEXCOORD1;};
   struct O{float4 color:SV_Target;float depth:SV_Depth;};
   V Vert(A input){V output;float3 world=TransformObjectToWorld(input.positionOS.xyz);output.positionCS=TransformWorldToHClip(world);output.uv=input.uv;output.color=PMAGammaToTargetSpace(input.color);output.eyeOffset=CharacterVisualEyeOffset(world);return output;}
   O Frag(V input){float4 texel=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv);
    #if defined(_STRAIGHT_ALPHA_INPUT)
     texel.rgb*=texel.a;
    #endif
    O output;output.color=texel*input.color;output.depth=CharacterVisualDeviceDepth(input.positionCS,input.eyeOffset);return output;}
   ENDHLSL
  }
  UsePass "Spine/Skeleton/Caster"
 }
}
