// 功能：显示四组中最近的实心表面并写其真实深度；柔边与核心留给后续正常深度合成。
Shader "Hidden/Ossastra/SceneVisibility/FiniteGroupOpaque"
{
 Properties
 {
  _GroupColor0("Color0",2D)="black"{} _GroupDepth0("Depth0",2D)="black"{}
  _GroupColor1("Color1",2D)="black"{} _GroupDepth1("Depth1",2D)="black"{}
  _GroupColor2("Color2",2D)="black"{} _GroupDepth2("Depth2",2D)="black"{}
  _GroupColor3("Color3",2D)="black"{} _GroupDepth3("Depth3",2D)="black"{}
  _VisScreen("Width height",Vector)=(1,1,1,1) _GroupActive("Active",Vector)=(0,0,0,0)
  _GroupOrder("Stable order",Vector)=(0,1,2,3) _SupportStrength("Support strengths",Vector)=(0,0,0,0)
  _CircleA0("A0",Vector)=(0,0,0,0) _CircleA1("A1",Vector)=(0,0,0,0) _CircleA2("A2",Vector)=(0,0,0,0) _CircleA3("A3",Vector)=(0,0,0,0)
  _CircleB0("B0",Vector)=(0,0,0,0) _CircleB1("B1",Vector)=(0,0,0,0) _CircleB2("B2",Vector)=(0,0,0,0) _CircleB3("B3",Vector)=(0,0,0,0)
  _Support0("Support0",Vector)=(0,0,0,0) _Support1("Support1",Vector)=(0,0,0,0) _Support2("Support2",Vector)=(0,0,0,0) _Support3("Support3",Vector)=(0,0,0,0)
  _Strength0("Strength0",Vector)=(0,0,0,0) _Strength1("Strength1",Vector)=(0,0,0,0) _Strength2("Strength2",Vector)=(0,0,0,0) _Strength3("Strength3",Vector)=(0,0,0,0)
 }
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque"}
  Pass
  {
   Name "VisibilityOpaque" Tags{"LightMode"="SRPDefaultUnlit"} Cull Off ZWrite On ZTest LEqual Blend Off
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex VisVert
   #pragma fragment VisOpaque
   #include "FiniteGroupCommon.hlsl"
   ENDHLSL
  }
  Pass
  {
   Name "DepthOnly" Tags{"LightMode"="DepthOnly"} Cull Off ZWrite On ZTest LEqual ColorMask 0
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex VisVert
   #pragma fragment VisOpaqueDepth
   #include "FiniteGroupCommon.hlsl"
   ENDHLSL
  }
 }
}
