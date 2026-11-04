/* 功能：保留Skeleton/UprightSpinePma原PMA颜色、直通alpha关键字、Stencil与混合。
 * 边界：不新增alpha-test或反预乘；许可点取最终显示深度，保留原ZWrite Off。
 * 生命周期：仅用于可见性正向适配材质；角色内侧轮廓只在合法片元消费，零alpha加色仍沿原受光，原Caster/物理/动画不由本适配器接管。
 * ENV-01：原无光照表面接环境主灯/阴影；原生Lit保留原光照，仅追加房间GI系数与主灯云场，alpha/深度/许可保持。
 */
Shader "Hidden/Ossastra/Rooms/SightSpine"
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
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
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
   #include "RoomSightClip.hlsl"
   #include "Assets/OSSASTRA/Scripts/EnvironmentSimulation/Rendering/EnvironmentLighting.hlsl"
   #include "Assets/OSSASTRA/Scripts/CharacterLighting/Rendering/CharacterLightingRim.hlsl"
   struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;float eyeOffset:TEXCOORD1;};
   struct O{float4 color:SV_Target;float depth:SV_Depth;};
   V Vert(A input){V output;float3 world=TransformObjectToWorld(input.positionOS.xyz);output.positionCS=TransformWorldToHClip(world);output.uv=input.uv;output.color=PMAGammaToTargetSpace(input.color);output.eyeOffset=CharacterVisualEyeOffset(world);return output;}
   O Frag(V input)
   {
    float deviceDepth=CharacterVisualDeviceDepth(input.positionCS,input.eyeOffset);
    float3 logicalWorld=RoomSightWorldFromDeviceDepth(input.positionCS.xy,deviceDepth);
    if(_EnvironmentFullSurface<.5)clip(RoomSightPartialContains(logicalWorld)?1:-1);
    float4 texel=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv);
    #if defined(_STRAIGHT_ALPHA_INPUT)
     texel.rgb*=texel.a;
    #endif
    float4 color=texel*input.color;
    if(EnvironmentCanShadeWorld(logicalWorld))color.rgb=color.a<=.00001?EnvironmentShadeLegacy(color.rgb,logicalWorld,EnvironmentCharacterNormal(_VisualDepthNormal.xyz),_EnvironmentSurfaceRole):EnvironmentShadeWithRim(color.rgb,logicalWorld,EnvironmentCharacterNormal(_VisualDepthNormal.xyz),_EnvironmentSurfaceRole,CharacterLightingSurfaceRim(input.positionCS.xy,logicalWorld));
    O output;output.color=EnvironmentFogColor(color,logicalWorld,1);output.depth=deviceDepth;return output;
   }
   ENDHLSL
  }
  // 阴影由未替换的源材质Caster承担，避免另造一条未许可的绘制入口。
  // 原Spine家族的硬件逐采样深度；调用方按真实源Shader选pass1。
  Pass
  {
   Name "SourceHardwareDepth"
   Tags{"LightMode"="SRPDefaultUnlit"}
   HLSLPROGRAM
   #pragma target 4.5
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
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
   #include "RoomSightClip.hlsl"
   #include "Assets/OSSASTRA/Scripts/EnvironmentSimulation/Rendering/EnvironmentLighting.hlsl"
   #include "Assets/OSSASTRA/Scripts/CharacterLighting/Rendering/CharacterLightingRim.hlsl"
   struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;float eyeOffset:TEXCOORD1;};

   V Vert(A input){V output;float3 world=TransformObjectToWorld(input.positionOS.xyz);output.positionCS=TransformWorldToHClip(world);output.uv=input.uv;output.color=PMAGammaToTargetSpace(input.color);output.eyeOffset=CharacterVisualEyeOffset(world);return output;}
   float4 Frag(V input):SV_Target
   {
    float deviceDepth=input.positionCS.z;
    float3 logicalWorld=RoomSightWorldFromDeviceDepth(input.positionCS.xy,deviceDepth);
    if(_EnvironmentFullSurface<.5)clip(RoomSightPartialContains(logicalWorld)?1:-1);
    float4 texel=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv);
    #if defined(_STRAIGHT_ALPHA_INPUT)
     texel.rgb*=texel.a;
    #endif
    float4 color=texel*input.color;
    if(EnvironmentCanShadeWorld(logicalWorld))color.rgb=color.a<=.00001?EnvironmentShadeLegacy(color.rgb,logicalWorld,EnvironmentCharacterNormal(_VisualDepthNormal.xyz),_EnvironmentSurfaceRole):EnvironmentShadeWithRim(color.rgb,logicalWorld,EnvironmentCharacterNormal(_VisualDepthNormal.xyz),_EnvironmentSurfaceRole,CharacterLightingSurfaceRim(input.positionCS.xy,logicalWorld));
    return EnvironmentFogColor(color,logicalWorld,1);
   }
   ENDHLSL
  }
 }
}
