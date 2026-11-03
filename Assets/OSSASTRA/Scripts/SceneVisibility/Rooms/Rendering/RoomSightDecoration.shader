/* 功能：保留装饰原图集、顶点颜色、alpha裁剪和普通深度，先裁合法观察片段。
 * 边界：不添加透明圆窗、置顶或屏幕偏移，未登记空间不写颜色/深度。
 * 生命周期：唯一可见性根临时适配材质调用，不修改Sprite原材质。
 * ENV-01：原无光照表面接环境主灯/阴影；原生Lit保留原光照，仅追加房间GI系数与主灯云场，alpha/深度/许可保持。
 */
Shader "Hidden/Ossastra/Rooms/SightDecoration"
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
   #pragma target 4.5
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
   CBUFFER_START(UnityPerMaterial)
    float4 _Color;
    float _Cutoff;
   CBUFFER_END
   #include "RoomSightClip.hlsl"
   #include "Assets/OSSASTRA/Scripts/EnvironmentSimulation/Rendering/EnvironmentLighting.hlsl"
   struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
   struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; float3 world:TEXCOORD1; };
   V vert(A input)
   {
    V output;output.world=TransformObjectToWorld(input.positionOS.xyz);output.positionCS=TransformWorldToHClip(output.world);output.uv=input.uv;output.color=input.color*_Color;return output;
   }
   half4 frag(V input):SV_Target
   {
    if(_EnvironmentFullSurface<.5)clip(RoomSightPartialContains(RoomSightWorldFromDeviceDepth(input.positionCS.xy,input.positionCS.z))?1:-1);
    half4 color=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv)*input.color;
    clip(color.a-_Cutoff);float3 logicalWorld=RoomSightWorldFromDeviceDepth(input.positionCS.xy,input.positionCS.z);
    if(EnvironmentCanShadeWorld(logicalWorld))color.rgb=EnvironmentShade(color.rgb,logicalWorld,float3(0,1,0),_EnvironmentSurfaceRole);
    return EnvironmentFogColor(half4(color.rgb,1),logicalWorld,0);
   }
   ENDHLSL
  }
 }
}
