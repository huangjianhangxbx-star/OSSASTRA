// 功能：按每像素真实深度的远到近rank显示柔边；不写深度、不绕过场景正常遮挡。
Shader "Hidden/Ossastra/SceneVisibility/FiniteGroupFeather"
{
 Properties
 {
  _VisEdgeStyle("Style strength actual-cell-pixels diagnostic-gray",Vector)=(0,0,1,0)
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
  _Rank("Per-pixel far-to-near rank",Float)=0
 }
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
  Pass
  {
   Name "VisibilityFeather" Tags{"LightMode"="SRPDefaultUnlit"} Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex VisVert
   #pragma fragment VisFeather
   #include "FiniteGroupCommon.hlsl"
   ENDHLSL
  }
 }
}
