// 草场视频候选：旧四参数入口与legacy数学原样保留；新纹理风由主Shader显式启用。
#ifndef OSSASTRA_GRASS_WIND
#define OSSASTRA_GRASS_WIND
float3 GrassDeformLegacy(float3 world,float3 root,float weight,float4 wind)
{
#if defined(_GRASS_WIND)
 float t=saturate(weight);float amplitude=wind.w;
 float phase=wind.z*(6.28318530718/16.0);
 float broad=.62+.30*sin(dot(root.xz,float2(.57,.23))-phase);
 float detail=.08*sin(dot(root.xz,float2(1.13,-.71))+phase*2);
 float2 bend=wind.xy*(broad+detail)*amplitude*t*t;
 world.xz+=bend;world.y-=length(bend)*.08*t;
#endif
 return world;
}

#if defined(OSSASTRA_GRASS_VIDEO_INPUTS)
// Main shader supplies _WindTexture and its material fields. An old standalone probe does not opt in.
float2 GrassGradientVector(float2 cell)
{
 float2 h=frac(float2(dot(cell,float2(127.1,311.7)),dot(cell,float2(269.5,183.3)))*.1031);
 h=frac((h+h.yx+19.19)*(h+float2(37.1,61.7)))*2-1;
 return h*rsqrt(max(dot(h,h),.0001));
}
float GrassGradientNoise(float2 uv)
{
 float2 cell=floor(uv),f=frac(uv);float2 u=f*f*f*(f*(f*6-15)+10);
 float a=dot(GrassGradientVector(cell),f);
 float b=dot(GrassGradientVector(cell+float2(1,0)),f-float2(1,0));
 float c=dot(GrassGradientVector(cell+float2(0,1)),f-float2(0,1));
 float d=dot(GrassGradientVector(cell+float2(1,1)),f-float2(1,1));
 return saturate(.5+.70710678*lerp(lerp(a,b,u.x),lerp(c,d,u.x),u.y));
}
float GrassWindStrength(float4 wind){return saturate(length(wind.xy));}
float2 GrassWindDirection(float4 wind)
{return dot(wind.xy,wind.xy)>.0000001?wind.xy*rsqrt(dot(wind.xy,wind.xy)):float2(1,0);}
float GrassLoopFade(float phase){return phase*phase*(3-2*phase);}
// Two advected views meet at the exact same UV when phase wraps. Smoothstep endpoints also match derivatives.
// No assumption that wind direction times speed is an integer texture tile. Environment speed changes amplitude only.
float GrassCoarseNoise(float3 root,float4 wind)
{
 float2 uv=root.xz/max(abs(_WindNoiseScale.xy),float2(.01,.01));
 float phase=frac(wind.z/16.0);float2 travel=GrassWindDirection(wind)*_WindNoiseSpeed;
 float first=SAMPLE_TEXTURE2D_LOD(_WindTexture,sampler_WindTexture,uv-travel*phase,max(_WindTextureLod,0)).r;
 float second=SAMPLE_TEXTURE2D_LOD(_WindTexture,sampler_WindTexture,uv-travel*(phase-1),max(_WindTextureLod,0)).r;
 float value=lerp(first,second,GrassLoopFade(phase));
 return smoothstep(_WindNoiseContrast.x,max(_WindNoiseContrast.x+.0001,_WindNoiseContrast.y),value);
}
float GrassFineNoise(float3 root,float weight,float4 wind)
{
 float hash=frac(dot(floor(root.xz*37.1),float2(.1031,.11369)));hash=frac(hash*(hash+19.19));
 float2 uv=float2(weight*3.7+hash*13.1,hash*7.3)+root.xz*.57;
 float phase=frac(wind.z/16.0);float2 travel=float2(3.1,-2.7);
 return lerp(GrassGradientNoise(uv+travel*phase),GrassGradientNoise(uv+travel*(phase-1)),GrassLoopFade(phase))*2-1;
}
float3 GrassDeformVideo(float3 world,float3 root,float weight,float4 wind,out float coarse)
{
 coarse=.5;
#if defined(_GRASS_WIND)
 float t=saturate(weight),strength=GrassWindStrength(wind);
 bool fineEnabled=(_WindMode>.5&&_WindMode<1.5)||_WindMode>2.5;
 bool coarseEnabled=_WindMode>1.5;
 if(coarseEnabled)coarse=GrassCoarseNoise(root,wind);
 float fine=fineEnabled?GrassFineNoise(root,t,wind):0;
 float2 direction=GrassWindDirection(wind),side=float2(-direction.y,direction.x);
 float2 shape=coarseEnabled?direction*max(_CoarseWindStrength,0)*(.22+.78*coarse):float2(0,0);
 if(fineEnabled)shape+=(direction*.28+side*.45)*fine*max(_FineWindStrength,0);
 shape/=max(1,length(shape)); // Actual maximum displacement remains <= the existing declared maximumBend.
 float2 bend=shape*strength*max(wind.w,0)*t*t*saturate(_WindDisplacement);
 world.xz+=bend;world.y-=length(bend)*.08*t;
#endif
 return world;
}
#endif

float3 GrassDeformWithSignal(float3 world,float3 root,float weight,float4 wind,out float coarse)
{
 coarse=.5;
#if defined(OSSASTRA_GRASS_VIDEO_INPUTS)
 if(_WindMode>.5){float signal=.5;float3 result=GrassDeformVideo(world,root,weight,wind,signal);coarse=signal;return result;}
#endif
 return GrassDeformLegacy(world,root,weight,wind);
}
// Contract kept for the existing 24 GPU probe and any former caller. Default/no-video compile is exact LegacyA.
float3 GrassDeform(float3 world,float3 root,float weight,float4 wind)
{float ignored;return GrassDeformWithSignal(world,root,weight,wind,ignored);}
#endif
