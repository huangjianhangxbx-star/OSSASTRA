/* 功能：保留角色DepthPlane原颜色、PMA、alpha和竖直显示深度，按最终深度重建逻辑点裁剪。
 * 边界：pass0用于UprightDepthPlane；pass1用于原MapResearch/DepthPlane的硬件逐样本深度。
 * 生命周期：只作正向绘制适配材质；新增逐角色完整轮廓只在既有合法片元内提亮；源Caster/碰撞/角色状态保持由原根维护。
 * ENV-01：原无光照表面接环境主灯/阴影；原生Lit保留原光照，仅追加房间GI系数与主灯云场，alpha/深度/许可保持。
 */
Shader "Hidden/Ossastra/Rooms/SightDepthPlane"
{
 Properties
 {

  [MainTexture] _MainTex("Texture",2D)="white"{}
  [MainColor] _Color("Tint",Color)=(1,1,1,1)
  _Cutoff("Depth Alpha Cutoff",Range(0.001,1))=.06
  [Toggle] _PmaInput("Premultiplied Texture Input",Float)=0
  [HideInInspector] _VisualDepthFoot("Owner foot",Vector)=(0,0,0,1)
  [HideInInspector] _VisualDepthNormal("Horizontal view normal",Vector)=(0,0,1,0)
  [HideInInspector] _VisualDepthEnabled("Gameplay camera only",Float)=0
 }
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest"}
  Pass
  {
   Tags{"LightMode"="SRPDefaultUnlit"}
   Cull Off ZWrite On ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
   HLSLPROGRAM
   #pragma target 4.5
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
   CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;half4 _Color;half _Cutoff;half _PmaInput;
    float4 _VisualDepthFoot;float4 _VisualDepthNormal;float _VisualDepthEnabled;
   CBUFFER_END
   #include "Assets/OSSASTRA/Scripts/Characters/Rendering/Shaders/CharacterUprightDepthCommon.hlsl"
   #include "RoomSightClip.hlsl"
   #include "Assets/OSSASTRA/Scripts/EnvironmentSimulation/Rendering/EnvironmentLighting.hlsl"
   #include "Assets/OSSASTRA/Scripts/CharacterLighting/Rendering/CharacterLightingRim.hlsl"
   struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
   struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;float eyeOffset:TEXCOORD1;};
   struct O{half4 color:SV_Target;float depth:SV_Depth;};
   V Vert(A input){V output;float3 world=TransformObjectToWorld(input.positionOS.xyz);output.positionCS=TransformWorldToHClip(world);output.uv=TRANSFORM_TEX(input.uv,_MainTex);output.color=input.color*_Color;output.eyeOffset=CharacterVisualEyeOffset(world);return output;}
   O Frag(V input)
   {
    float deviceDepth=CharacterVisualDeviceDepth(input.positionCS,input.eyeOffset);
    float3 logicalWorld=RoomSightWorldFromDeviceDepth(input.positionCS.xy,deviceDepth);
    if(_EnvironmentFullSurface<.5)clip(RoomSightPartialContains(logicalWorld)?1:-1);
    half4 texel=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv);
    if(_PmaInput>.5h)texel.rgb/=max(texel.a,.001h);
    half4 color=texel*input.color;clip(color.a-_Cutoff);
    if(EnvironmentCanShadeWorld(logicalWorld))color.rgb=EnvironmentShadeWithRim(color.rgb,logicalWorld,EnvironmentCharacterNormal(_VisualDepthNormal.xyz),_EnvironmentSurfaceRole,CharacterLightingSurfaceRim(input.positionCS.xy,logicalWorld));
    O output;output.color=EnvironmentFogColor(color,logicalWorld,0);output.depth=deviceDepth;return output;
   }
   ENDHLSL
  }
  // 原MapResearch/DepthPlane专用硬件深度Pass；调用方须按真实源Shader选择pass1。
  Pass
  {
   Name "SourceHardwareDepth"
   Tags{"LightMode"="SRPDefaultUnlit"}
   Cull Off ZWrite On ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
   HLSLPROGRAM
   #pragma target 4.5
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
   CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;half4 _Color;half _Cutoff;half _PmaInput;
    float4 _VisualDepthFoot;float4 _VisualDepthNormal;float _VisualDepthEnabled;
   CBUFFER_END
   #include "RoomSightClip.hlsl"
   #include "Assets/OSSASTRA/Scripts/EnvironmentSimulation/Rendering/EnvironmentLighting.hlsl"
   #include "Assets/OSSASTRA/Scripts/CharacterLighting/Rendering/CharacterLightingRim.hlsl"
   struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
   struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;float3 world:TEXCOORD1;};
   V Vert(A input)
   {
    V output;output.world=TransformObjectToWorld(input.positionOS.xyz);
    output.positionCS=TransformWorldToHClip(output.world);output.uv=TRANSFORM_TEX(input.uv,_MainTex);output.color=input.color*_Color;return output;
   }
   half4 Frag(V input):SV_Target
   {
    if(_EnvironmentFullSurface<.5)clip(RoomSightPartialContains(RoomSightWorldFromDeviceDepth(input.positionCS.xy,input.positionCS.z))?1:-1);
    half4 texel=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv);
    if(_PmaInput>.5h)texel.rgb/=max(texel.a,.001h);
    half4 color=texel*input.color;clip(color.a-_Cutoff);float3 logicalWorld=RoomSightWorldFromDeviceDepth(input.positionCS.xy,input.positionCS.z);
    if(EnvironmentCanShadeWorld(logicalWorld))color.rgb=EnvironmentShadeWithRim(color.rgb,logicalWorld,EnvironmentCharacterNormal(_VisualDepthNormal.xyz),_EnvironmentSurfaceRole,CharacterLightingSurfaceRim(input.positionCS.xy,logicalWorld));
    return EnvironmentFogColor(color,logicalWorld,0);
   }
   ENDHLSL
  }
 }
}
