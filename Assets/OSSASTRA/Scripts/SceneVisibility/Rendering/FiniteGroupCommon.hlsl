// 功能：四组真实深度采样、双圆与支撑保护、逐像素远近排序；不改变角色或原始世界几何。
// 边界：固定四组、标准主相机投影，颜色和device-depth来自本模块有限捕获资源。
#ifndef OSSASTRA_FINITE_GROUP_CAPTURE_INCLUDED
#define OSSASTRA_FINITE_GROUP_CAPTURE_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
TEXTURE2D(_GroupColor0); SAMPLER(sampler_GroupColor0); TEXTURE2D(_GroupDepth0); SAMPLER(sampler_GroupDepth0);
TEXTURE2D(_GroupColor1); SAMPLER(sampler_GroupColor1); TEXTURE2D(_GroupDepth1); SAMPLER(sampler_GroupDepth1);
TEXTURE2D(_GroupColor2); SAMPLER(sampler_GroupColor2); TEXTURE2D(_GroupDepth2); SAMPLER(sampler_GroupDepth2);
TEXTURE2D(_GroupColor3); SAMPLER(sampler_GroupColor3); TEXTURE2D(_GroupDepth3); SAMPLER(sampler_GroupDepth3);
CBUFFER_START(UnityPerMaterial)
 float4 _VisScreen, _GroupActive, _GroupOrder, _SupportStrength;
 float4 _CircleA0,_CircleA1,_CircleA2,_CircleA3;
 float4 _CircleB0,_CircleB1,_CircleB2,_CircleB3;
 float4 _Support0,_Support1,_Support2,_Support3;
 float4 _Strength0,_Strength1,_Strength2,_Strength3;
 float _Rank;
CBUFFER_END
struct VisAttributes {float4 positionOS:POSITION;};
struct VisVaryings {float4 positionCS:SV_POSITION;};
struct VisOutput {half4 color:SV_Target;float depth:SV_Depth;};
struct GroupSample {half4 color;float depth;float opacity;float order;bool valid;};
VisVaryings VisVert(VisAttributes a) {VisVaryings o;o.positionCS=TransformObjectToHClip(a.positionOS.xyz);return o;}
float2 CameraBottomPixels(float2 raster)
{
 // The actual main projection decides the window/RT convention. A platform-only flip was
 // already falsified by the original Native M2 (the render-target projection was flipped too).
 #if UNITY_UV_STARTS_AT_TOP
  if(_ProjectionParams.x>0)raster.y=_VisScreen.y-raster.y;
 #endif
 return raster;
}
float Disk(float2 pixels,float4 circle,float strength)
{
 if(strength<=0 || circle.w<=circle.z)return 0;
 return saturate(strength)*(1-smoothstep(circle.z,circle.w,length(pixels-circle.xy*_VisScreen.xy)));
}
GroupSample ReadGroup(int slot,float2 pixels)
{
 GroupSample o;o.color=0;o.depth=0;o.opacity=0;o.order=_GroupOrder[slot];o.valid=false;
 if(_GroupActive[slot]<.5)return o;
 float2 uv=pixels/_VisScreen.xy;float4 a,b,strength,support;
 if(slot==0){o.color=SAMPLE_TEXTURE2D_LOD(_GroupColor0,sampler_GroupColor0,uv,0);o.depth=SAMPLE_TEXTURE2D_LOD(_GroupDepth0,sampler_GroupDepth0,uv,0).r;a=_CircleA0;b=_CircleB0;strength=_Strength0;support=_Support0;}
 else if(slot==1){o.color=SAMPLE_TEXTURE2D_LOD(_GroupColor1,sampler_GroupColor1,uv,0);o.depth=SAMPLE_TEXTURE2D_LOD(_GroupDepth1,sampler_GroupDepth1,uv,0).r;a=_CircleA1;b=_CircleB1;strength=_Strength1;support=_Support1;}
 else if(slot==2){o.color=SAMPLE_TEXTURE2D_LOD(_GroupColor2,sampler_GroupColor2,uv,0);o.depth=SAMPLE_TEXTURE2D_LOD(_GroupDepth2,sampler_GroupDepth2,uv,0).r;a=_CircleA2;b=_CircleB2;strength=_Strength2;support=_Support2;}
 else {o.color=SAMPLE_TEXTURE2D_LOD(_GroupColor3,sampler_GroupColor3,uv,0);o.depth=SAMPLE_TEXTURE2D_LOD(_GroupDepth3,sampler_GroupDepth3,uv,0).r;a=_CircleA3;b=_CircleB3;strength=_Strength3;support=_Support3;}
 #if UNITY_REVERSED_Z
  o.valid=o.depth>0 && o.depth<=1;
 #else
  o.valid=o.depth>=0 && o.depth<1;
 #endif
 float reveal=max(saturate(strength.z),max(Disk(pixels,a,strength.x),Disk(pixels,b,strength.y)));
 reveal*=1-Disk(pixels,support,_SupportStrength[slot]);o.opacity=1-saturate(reveal);return o;
}
bool Nearer(GroupSample a,GroupSample b)
{
 if(a.depth==b.depth)return a.order>b.order;
 #if UNITY_REVERSED_Z
  return a.depth>b.depth;
 #else
  return a.depth<b.depth;
 #endif
}
VisOutput VisOpaque(VisVaryings input)
{
 float2 pixels=CameraBottomPixels(input.positionCS.xy);GroupSample best;best.color=0;best.depth=0;best.opacity=0;best.order=0;best.valid=false;
 [unroll]for(int slot=0;slot<4;slot++)
 {
  GroupSample sample=ReadGroup(slot,pixels);
  if(sample.valid&&sample.opacity>=.999999&&(!best.valid||Nearer(sample,best)))best=sample;
 }
 clip(best.valid?1:-1);VisOutput o;o.color=best.color;o.depth=best.depth;return o;
}
float VisOpaqueDepth(VisVaryings input):SV_Depth {return VisOpaque(input).depth;}
VisOutput VisFeather(VisVaryings input)
{
 float2 pixels=CameraBottomPixels(input.positionCS.xy);GroupSample samples[4];
 [unroll]for(int i=0;i<4;i++){samples[i]=ReadGroup(i,pixels);samples[i].valid=samples[i].valid&&samples[i].opacity>0&&samples[i].opacity<.999999;}
 GroupSample selected;selected.color=0;selected.depth=0;selected.opacity=0;selected.order=0;selected.valid=false;
 [unroll]for(int i=0;i<4;i++)
 {
  if(!samples[i].valid)continue;int rank=0;
  [unroll]for(int j=0;j<4;j++)if(j!=i&&samples[j].valid&&Nearer(samples[i],samples[j]))rank++;
  if(rank==(int)_Rank)selected=samples[i];
 }
 clip(selected.valid?1:-1);VisOutput o;o.color=half4(selected.color.rgb,selected.opacity);o.depth=selected.depth;return o;
}
#endif
