Shader "Ossastra/Characters/SideViewSpine"
{
 Properties
 {
  [NoScaleOffset] _MainTex("Main Texture",2D)="black"{}
  _Color("Tint",Color)=(1,1,1,1)
  _Cutoff("Cutout alpha",Range(.001,1))=.06
  _PmaInput("DepthPlane texture is premultiplied",Float)=0
  [Toggle(_STRAIGHT_ALPHA_INPUT)] _StraightAlphaInput("Straight Alpha Texture",Int)=1
  [HideInInspector] _StencilRef("Stencil Reference",Float)=1
  [HideInInspector] _StencilComp("Stencil Comparison",Float)=8
  [HideInInspector] _SideViewCutout("Cutout source",Float)=0
  [HideInInspector] _SideViewPresentationOffset("Presented world offset",Vector)=(0,0,0,0)
  [HideInInspector] _EnvironmentSurfaceRole("Character surface",Float)=2
  [HideInInspector] _EnvironmentResponseOverride("Response",Float)=1
  [HideInInspector] _EnvironmentReceiveShadow("Main light shadows",Float)=1
  [HideInInspector] _EnvironmentMinimumOverride("Readability",Float)=0
  [HideInInspector] _EnvironmentStylizedSkyTint("A tint",Vector)=(0,0,0,0)
  [HideInInspector] _CharacterEnvironmentTint("B tint",Vector)=(0,0,0,0)
  [HideInInspector] _CharacterEnvironmentBlend("B blend",Float)=0
  [HideInInspector] _CharacterLightControls("Light controls",Vector)=(0,0,0,0)
  [HideInInspector] _CharacterLightShoulder("Protection shoulder",Vector)=(0,0,0,0)
  [HideInInspector] _CharacterRimShape("Rim shape",Vector)=(0,0,0,0)
  [HideInInspector] _CharacterRimLocalPosition("Rim light position",Vector)=(0,0,0,0)
  [HideInInspector] _CharacterRimLocalColor("Rim light color",Vector)=(0,0,0,0)
  [HideInInspector] _CharacterCoverageReady("Coverage ready",Float)=0
  [HideInInspector] _CharacterCoverageTexture("Coverage",2D)="black"{}
  [HideInInspector] _CharacterCoverageRect("Coverage rect",Vector)=(0,0,0,0)
  [HideInInspector] _CharacterCoverageAtlasRect("Atlas rect",Vector)=(0,0,0,0)
  [HideInInspector] _CharacterCoverageScreenSize("Screen size",Vector)=(0,0,0,0)
  [HideInInspector] _CharacterCoverageTexelSize("Texel size",Vector)=(0,0,0,0)
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
  Cull Off ZWrite Off ZTest LEqual Blend One OneMinusSrcAlpha
  Stencil {Ref[_StencilRef] Comp[_StencilComp] Pass Keep}
  Pass
  {
   Name "SideViewLitPMA"
   Tags {"LightMode"="SRPDefaultUnlit"}
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_local _ _STRAIGHT_ALPHA_INPUT
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #define USE_URP
   #include "Assets/Spine/Runtime/spine-unity/Shaders/CGIncludes/Spine-Common.cginc"
   #include "SideViewCharacterLighting.hlsl"
   TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
   float4 _MainTex_ST, _Color;
   float _SideViewCutout, _PmaInput, _Cutoff;

   struct Attributes { float4 position : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
   struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; float3 world : TEXCOORD1; };
   Varyings Vert(Attributes input)
   {
       Varyings output;
       output.world = TransformObjectToWorld(input.position.xyz) + _SideViewPresentationOffset.xyz;
       output.position = TransformWorldToHClip(output.world);
       output.uv = _SideViewCutout > .5 ? TRANSFORM_TEX(input.uv, _MainTex) : input.uv;
       output.color = _SideViewCutout > .5 ? input.color * _Color : PMAGammaToTargetSpace(input.color);
       return output;
   }
   float4 Frag(Varyings input) : SV_Target
   {
       float4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
       float4 color = texel * input.color;
       if (_SideViewCutout > .5)
       {
           clip(color.a - _Cutoff);
           color.rgb *= _PmaInput > .5 ? color.a / max(texel.a, .001) : color.a;
       }
       #if defined(_STRAIGHT_ALPHA_INPUT)
       else color.rgb *= texel.a;
       #endif
       color.rgb = SideViewShade(color.rgb, color.a, input.position.xy, input.world);
       return color;
   }
   ENDHLSL
  }
 }
 Fallback Off
}
