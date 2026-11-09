// 只绘显式地表未受光基础色；矩阵和材质参数均属于私有材质，不写共享全局。
Shader "Hidden/Ossastra/Vegetation/TerrainBaseColorBake"
{
 Properties
 {
  _BakeMap("Base color",2D)="white"{}
  _BakeTint("Tint",Color)=(1,1,1,1)
  _BakeST("UV scale / offset",Vector)=(1,1,0,0)
  _BakeClipAlpha("Alpha clip",Float)=0
  _BakeCutoff("Cutoff",Float)=.5
  _BakeVertexColor("Vertex color",Float)=0
 }
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline"}
  Pass
  {
   Name "TerrainBaseColor"
   Cull Off ZWrite On ZTest LEqual Blend Off
   HLSLPROGRAM
   #pragma target 3.0
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_BakeMap);SAMPLER(sampler_BakeMap);
   CBUFFER_START(UnityPerMaterial)
    float4x4 _BakeViewProjection;
    float4 _BakeTint,_BakeST;
    float _BakeClipAlpha,_BakeCutoff,_BakeVertexColor;
   CBUFFER_END
   struct Attributes{float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct Varyings{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   Varyings Vert(Attributes input)
   {
    Varyings output;output.positionCS=mul(_BakeViewProjection,float4(TransformObjectToWorld(input.positionOS.xyz),1));
    output.uv=input.uv*_BakeST.xy+_BakeST.zw;output.color=lerp(float4(1,1,1,1),input.color,saturate(_BakeVertexColor));return output;
   }
   half4 Frag(Varyings input):SV_Target
   {
    half4 color=SAMPLE_TEXTURE2D(_BakeMap,sampler_BakeMap,input.uv)*_BakeTint*input.color;
    if(_BakeClipAlpha>.5)clip(color.a-_BakeCutoff);
    return half4(color.rgb,1); // alpha是有效地表覆盖，不是受光或阴影。
   }
   ENDHLSL
  }
 }
}
