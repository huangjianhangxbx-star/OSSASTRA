// 仅指定的世界标签使用；保持uGUI纹理/字体/Stencil/RectMask/alpha，叠加同相机双许可mask。
// 不使用相机cleanup已清零的调试flag；Root登记与最终合成显式控制独立的Overlay有效期。
Shader "Ossastra/Rooms/WorldLabel"
{
 Properties
 {
  [PerRendererData] _MainTex("Sprite Texture",2D)="white"{}
  _Color("Tint",Color)=(1,1,1,1)
  _StencilComp("Stencil Comparison",Float)=8
  _Stencil("Stencil ID",Float)=0
  _StencilOp("Stencil Operation",Float)=0
  _StencilWriteMask("Stencil Write Mask",Float)=255
  _StencilReadMask("Stencil Read Mask",Float)=255
  _ColorMask("Color Mask",Float)=15
  _RoomWorldLabelRequirePortal("室外标签限定真实开口",Float)=0
  [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use Alpha Clip",Float)=0
 }
 SubShader
 {
  Tags{"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True"}
  Stencil{Ref[_Stencil] Comp[_StencilComp] Pass[_StencilOp] ReadMask[_StencilReadMask] WriteMask[_StencilWriteMask]}
  Cull Off Lighting Off ZWrite Off ZTest[unity_GUIZTestMode] Blend SrcAlpha OneMinusSrcAlpha ColorMask[_ColorMask]
  Pass
  {
   Name "WorldLabelPermission"
   CGPROGRAM
   #pragma target 2.0
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
   #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
   #include "UnityCG.cginc"
   #include "UnityUI.cginc"
   sampler2D _MainTex;float4 _MainTex_ST;fixed4 _Color,_TextureSampleAdd;
   sampler2D _RoomVisibilityPermission;
   float _RoomWorldLabelMaskEnabled,_RoomWorldLabelMaskReady,_RoomWorldLabelExteriorEnabled,_RoomWorldLabelInteriorEnabled;
   float _RoomWorldLabelRequirePortal;
   struct A{float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V{float4 vertex:SV_POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;float4 worldPosition:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
   float4 _ClipRect;
   V Vert(A input)
   {
    V output;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.worldPosition=input.vertex;output.vertex=UnityObjectToClipPos(output.worldPosition);output.uv=TRANSFORM_TEX(input.uv,_MainTex);output.color=input.color*_Color;return output;
   }
   fixed4 Frag(V input):SV_Target
   {
    fixed4 color=(tex2D(_MainTex,input.uv)+_TextureSampleAdd)*input.color;
    #ifdef UNITY_UI_CLIP_RECT
     color.a*=UnityGet2DClipping(input.worldPosition.xy,_ClipRect);
    #endif
    if(_RoomWorldLabelMaskEnabled>.5)
    {
     clip(_RoomWorldLabelMaskReady-.5);
     float3 permission=tex2D(_RoomVisibilityPermission,input.vertex.xy/_ScreenParams.xy).rgb;
     float exterior=_RoomWorldLabelRequirePortal>.5?permission.b:permission.r;
     color.a*=lerp(1,saturate(exterior),saturate(_RoomWorldLabelExteriorEnabled))*lerp(1,saturate(permission.g),saturate(_RoomWorldLabelInteriorEnabled));
    }
    #ifdef UNITY_UI_ALPHACLIP
     clip(color.a-.001);
    #endif
    return color;
   }
   ENDCG
  }
 }
}
