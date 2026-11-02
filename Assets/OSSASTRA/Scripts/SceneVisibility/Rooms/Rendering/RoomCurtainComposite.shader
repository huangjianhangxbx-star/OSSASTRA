// 只叠加黑幕颜色；不复制相机颜色、不重画世界或人物，Overlay HUD在后续原通路正常呈现。
Shader "Hidden/Ossastra/Rooms/CurtainComposite"
{
 Properties
 {
  _RoomExteriorColor("室外隔离黑幕颜色",Color)=(0,0,0,1)
  _RoomInteriorColor("室内未知黑幕颜色",Color)=(0,0,0,1)
  _RoomExteriorEnabled("室外黑幕",Float)=1
  _RoomInteriorEnabled("室内未知黑幕",Float)=1
  _RoomMaskDebug("显示许可mask",Float)=0
 }
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline"}
  Pass
  {
   Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_RoomVisibilityPermission);SAMPLER(sampler_RoomVisibilityPermission);
   float4 _RoomMaskSize;
   CBUFFER_START(UnityPerMaterial)
    half4 _RoomExteriorColor,_RoomInteriorColor;float _RoomExteriorEnabled,_RoomInteriorEnabled,_RoomMaskDebug;
   CBUFFER_END
   struct V{float4 positionCS:SV_POSITION;};
   V Vert(uint vertexID:SV_VertexID){V output;output.positionCS=GetFullScreenTriangleVertexPosition(vertexID);return output;}
   half4 Frag(V input):SV_Target
   {
    float2 uv=input.positionCS.xy*_RoomMaskSize.zw;
    half2 permission=saturate(SAMPLE_TEXTURE2D(_RoomVisibilityPermission,sampler_RoomVisibilityPermission,uv).rg);
    half exterior=lerp(1,permission.r,saturate(_RoomExteriorEnabled));
    half interior=lerp(1,permission.g,saturate(_RoomInteriorEnabled));
    if(_RoomMaskDebug>.5)
    {
     half value=_RoomMaskDebug<1.5?permission.r:_RoomMaskDebug<2.5?permission.g:exterior*interior;
     return half4(value.xxx,1);
    }
    half interiorAlpha=(1-interior)*_RoomInteriorColor.a;
    half exteriorAlpha=(1-exterior)*_RoomExteriorColor.a;
    half alpha=1-(1-interiorAlpha)*(1-exteriorAlpha);
    half3 weighted=_RoomInteriorColor.rgb*interiorAlpha*(1-exteriorAlpha)+_RoomExteriorColor.rgb*exteriorAlpha;
    return half4(weighted/max(alpha,.00001h),alpha);
   }
   ENDHLSL
  }
 }
}
