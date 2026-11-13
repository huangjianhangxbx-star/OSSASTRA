Shader "Ossastra/Characters/SideViewAceSpine"
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
  [HideInInspector] _AceSlotTint("Apply native blend-slot material tint",Float)=0
  [HideInInspector] _AceSourceBlend("Source blend",Float)=1
  [HideInInspector] _AceDestinationBlend("Destination blend",Float)=10
  [HideInInspector] _SideViewCutout("Cutout source",Float)=0
  [HideInInspector] _SideViewPresentationOffset("Presented world offset",Vector)=(0,0,0,0)
  [HideInInspector] _VisualDepthFoot("Visual depth anchor",Vector)=(0,0,0,0)
  [HideInInspector] _VisualDepthNormal("Visual depth direction",Vector)=(0,0,0,0)
  [HideInInspector] _VisualDepthEnabled("Visual depth enabled",Float)=0

 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
  Cull Off ZWrite Off ZTest LEqual Blend [_AceSourceBlend] [_AceDestinationBlend]
  Stencil {Ref[_StencilRef] Comp[_StencilComp] Pass Keep}
  Pass
  {
   Name "SideViewAcePMA"
   Tags {"LightMode"="SRPDefaultUnlit"}
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_local _ _STRAIGHT_ALPHA_INPUT
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #define USE_URP
   #include "Assets/Spine/Runtime/spine-unity/Shaders/CGIncludes/Spine-Common.cginc"
   #include "SideViewAceFusion.hlsl"
   TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
   float4 _MainTex_ST, _Color;
   float _SideViewCutout, _PmaInput, _Cutoff, _AceSlotTint;

   struct Attributes { float4 position : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
   struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
   Varyings Vert(Attributes input)
   {
       Varyings output;
       float3 world = TransformObjectToWorld(input.position.xyz) + _SideViewPresentationOffset.xyz;
       output.position = TransformWorldToHClip(world);
       output.uv = _SideViewCutout > .5 ? TRANSFORM_TEX(input.uv, _MainTex) : input.uv;
       output.color = PMAGammaToTargetSpace(input.color);
       if (_SideViewCutout > .5 || _AceSlotTint > .5) output.color *= float4(_Color.rgb * _Color.a, _Color.a);
       return output;
   }
   float4 Frag(Varyings input) : SV_Target
   {
       float4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
       float4 color = texel * input.color;
       if (_SideViewCutout > .5)
       {
           clip(color.a - _Cutoff);
           // The Spine mesh already includes the vertex opacity in RGB.
           if (_PmaInput < .5) color.rgb *= texel.a;
       }
       #if defined(_STRAIGHT_ALPHA_INPUT)
       else color.rgb *= texel.a;
       #endif
       // Separate native blend slots keep their operator input and do not get surface gain twice.
       if (_AceSlotTint < .5) color.rgb = SideViewAceFusionColor(color.rgb, color.a, input.position);
       return color;
   }
   ENDHLSL
  }
  Pass
  {
   Name "SideViewVisibleSurface"
   Tags {"LightMode"="SideViewVisibleSurface"}
   ZWrite On ZTest Always Blend One Zero
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex VertSurface
   #pragma fragment FragSurface
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Assets/OSSASTRA/Scripts/WorldRendering/SideView/Shaders/SideViewComparisonOpaqueDepth.hlsl"
   TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
   float4 _MainTex_ST, _Color, _SideViewPresentationOffset;
   float _SideViewCutout, _Cutoff, _AceSlotTint;
   struct Attributes { float4 position : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
   struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float alpha : TEXCOORD1; };
   Varyings VertSurface(Attributes input)
   {
       Varyings output;
       output.position = TransformWorldToHClip(TransformObjectToWorld(input.position.xyz) + _SideViewPresentationOffset.xyz);
       output.uv = _SideViewCutout > .5 ? TRANSFORM_TEX(input.uv, _MainTex) : input.uv;
       output.alpha = input.color.a * (_SideViewCutout > .5 || _AceSlotTint > .5 ? _Color.a : 1);
       return output;
   }
   float4 FragSurface(Varyings input) : SV_Target
   {
       SideViewRespectOpaqueDepth(input.position);
       float alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * input.alpha;
       clip(alpha - (_SideViewCutout > .5 ? _Cutoff : 1.0 / 255.0));
       return float4(0, 0, 0, 1);
   }
   ENDHLSL
  }
 }
 Fallback Off
}
