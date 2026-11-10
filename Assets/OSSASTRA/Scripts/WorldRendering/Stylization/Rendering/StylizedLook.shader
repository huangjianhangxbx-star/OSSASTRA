Shader "Hidden/Ossastra/WorldRendering/StylizedLook"
{
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" }
  Cull Off ZWrite Off ZTest Always
  HLSLINCLUDE
  #pragma target 4.5
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
  #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
  #include "StylizedLookMath.hlsl"
  TEXTURE2D(_LD_Coverage);
  TEXTURE2D(_LD_Depth);
  TEXTURE2D(_LD_Grid);
  TEXTURE2D(_LD_BloomTexture);
  TEXTURE2D(_LD_CharacterGlow);
  CBUFFER_START(UnityPerMaterial)
   float4 _LD_Size; // width, height, reciprocal width, reciprocal height
   float4 _LD_GridSize;
   float4 _LD_BloomSize;
   float4 _LD_Features; // theme, air, bands, tone mapping
   float4 _LD_PixelDebug; // block size, debug view, bloom present, glow present
   float4 _LD_Shadow, _LD_Mid, _LD_Light;
   float4 _LD_Theme; // env strength, actor strength, pivot, contrast
   float4 _LD_Warm; // strength, peak low, peak high
   float4 _LD_AirNear, _LD_AirMid, _LD_AirFar;
   float4 _LD_Density; // distance, height, falloff, base world height
   float4 _LD_AirRange; // observer eye depth, start offset, far offset, max opacity
   float4 _LD_Bands; // levels, env strength, actor strength, reference white
   float4 _LD_Camera; // xyz position, orthographic
   float4 _LD_Clip; // near clip, bloom intensity
   float4x4 _LD_InverseVP, _LD_View;
  CBUFFER_END

  int2 SafePixel(int2 p) { return clamp(p, int2(0,0), int2(_LD_Size.xy)-1); }
  float2 PixelUV(int2 p) { return (float2(p)+0.5)*_LD_Size.zw; }
  int2 ScreenPixel(float2 uv) { return SafePixel(int2(floor(uv*_LD_Size.xy))); }
  float4 Coverage(int2 p) { return LOAD_TEXTURE2D(_LD_Coverage, SafePixel(p)); }
  bool Eligible(float4 c) { return min(c.r,c.a)>0.998; }
  float4 Source(int2 p) { return LOAD_TEXTURE2D_X(_BlitTexture, SafePixel(p)); }
  float3 Glow(int2 p) { return _LD_PixelDebug.w>0.5 ? LOAD_TEXTURE2D(_LD_CharacterGlow,SafePixel(p)).rgb : 0; }
  float3 World(int2 p)
  {
   float d=LOAD_TEXTURE2D(_LD_Depth,SafePixel(p)).r;
   #if !UNITY_REVERSED_Z
   d=lerp(UNITY_NEAR_CLIP_VALUE,1.0,d);
   #endif
   return ComputeWorldSpacePosition(PixelUV(p),d,_LD_InverseVP);
  }
  float Eye(float3 p) { return -mul(_LD_View,float4(p,1)).z; }
  float3 Theme(float3 rgb, float actor)
  {
   if (_LD_Features.x<0.5) return rgb;
   float3 c=LD_Contrast(rgb,_LD_Theme.w,_LD_Theme.z);
   float guard=LD_WarmGuard(c,_LD_Warm.y,_LD_Warm.z)*_LD_Warm.x;
   return LD_Theme(c,_LD_Shadow.rgb,_LD_Mid.rgb,_LD_Light.rgb,
                  lerp(_LD_Theme.x,_LD_Theme.y,saturate(actor)),_LD_Theme.z,guard);
  }
  float3 Fog(float3 rgb, int2 p, float affinity, out float amount)
  {
   amount=0;
   if (_LD_Features.y<0.5 || affinity<=0) return rgb;
   float3 end=World(p);
   float eye=Eye(end);
   if (!(eye>0) || eye>1e7) return rgb;
   float nearDevice=0;
   #if UNITY_REVERSED_Z
   nearDevice=1;
   #else
   nearDevice=UNITY_NEAR_CLIP_VALUE;
   #endif
   float3 origin=_LD_Camera.xyz;
   float z0=0;
   if (_LD_Camera.w>0.5)
   {
    origin=ComputeWorldSpacePosition(PixelUV(p),nearDevice,_LD_InverseVP);
    z0=Eye(origin);
   }
   float start=max(_LD_Clip.x,_LD_AirRange.x+_LD_AirRange.y);
   float t=saturate((start-z0)/max(eye-z0,1e-5));
   float3 begin=lerp(origin,end,t);
   amount=LD_FogAmount(distance(begin,end),begin.y,end.y,_LD_Density.w,
       _LD_Density.x,_LD_Density.y,_LD_Density.z,_LD_AirRange.w)*saturate(affinity);
   float farEye=max(start+0.01,_LD_AirRange.x+_LD_AirRange.z);
   float v=saturate((eye-start)/(farEye-start));
   float3 tint=lerp(_LD_AirNear.rgb,_LD_AirMid.rgb,smoothstep(0,0.5,v));
   tint=lerp(tint,_LD_AirFar.rgb,smoothstep(0.5,1,v));
   return lerp(rgb,tint,amount);
  }
  // Bilinear colour with an explicit all-four validity guard. Hardware interpolation
  // alone would mix hidden energy back into legal pixels at the mask boundary.
  float3 Bloom(int2 p)
  {
   if (_LD_PixelDebug.z<0.5) return 0;
   float2 q=PixelUV(p)*_LD_BloomSize.xy-0.5;
   int2 a=int2(floor(q)); float2 t=frac(q); int2 hi=int2(_LD_BloomSize.xy)-1;
   float4 x=LOAD_TEXTURE2D(_LD_BloomTexture,clamp(a,int2(0,0),hi));
   float4 y=LOAD_TEXTURE2D(_LD_BloomTexture,clamp(a+int2(1,0),int2(0,0),hi));
   float4 z=LOAD_TEXTURE2D(_LD_BloomTexture,clamp(a+int2(0,1),int2(0,0),hi));
   float4 w=LOAD_TEXTURE2D(_LD_BloomTexture,clamp(a+int2(1,1),int2(0,0),hi));
   if (min(min(x.a,y.a),min(z.a,w.a))<0.998) return 0;
   return lerp(lerp(x.rgb,y.rgb,t.x),lerp(z.rgb,w.rgb,t.x),t.y)*_LD_Clip.y;
  }
  float4 StyleHDR(Varyings i):SV_Target
  {
   int2 p=ScreenPixel(i.texcoord); float4 cov=Coverage(p);
   if (!Eligible(cov)) return 0;
   float f; float3 c=Fog(Theme(Source(p).rgb,cov.g),p,cov.b,f);
   return float4(max(c,0),1);
  }
  float4 LogicalGrid(Varyings i):SV_Target
  {
   int n=(int)_LD_PixelDebug.x;
   int2 begin=int2(i.positionCS.xy)*n;
   int2 extent=min(int2(n,n),int2(_LD_Size.xy)-begin);
   float3 sum=0; float actors=0; float count=0; bool valid=true;
   [unroll] for(int y=0;y<4;y++) [unroll] for(int x=0;x<4;x++)
   {
    if(x>=extent.x || y>=extent.y) continue;
    int2 p=begin+int2(x,y); float4 cov=Coverage(p);
    valid=valid&&Eligible(cov); sum+=Source(p).rgb; actors+=cov.g; count++;
   }
   // Negative alpha is a sentinel; actor mean is packed into [1,2].
   return valid&&count>0 ? float4(sum/count,1+actors/count) : float4(0,0,0,-1);
  }
  float4 Composite(Varyings i):SV_Target
  {
   int2 p=ScreenPixel(i.texcoord); float4 src=Source(p), cov=Coverage(p);
   if (_LD_PixelDebug.y==1) return float4(cov.rgb,1);
   if (_LD_PixelDebug.y==2) return float4(saturate(Eye(World(p))/max(_LD_AirRange.x+16,0.01)).xxx,1);
   if (!Eligible(cov)) return float4(src.rgb+Glow(p),src.a);
   float3 c=src.rgb; float actor=cov.g; int2 fogPixel=p; float affinity=cov.b;
   int n=(int)_LD_PixelDebug.x;
   if(n>1)
   {
    int2 cell=p/n; float4 grid=LOAD_TEXTURE2D(_LD_Grid,cell);
    if(grid.a>=0.998)
    {
     c=grid.rgb;actor=saturate(grid.a-1);
     int2 rep=LD_PixelSource(p,int2(_LD_Size.xy),n);
     float repEye=Eye(World(rep)); float eye=Eye(World(p));
     if(abs(eye-repEye)>max(0.05,0.01*abs(repEye)))
     { fogPixel=rep;affinity=Coverage(rep).b; }
    }
   }
   c=Theme(c,actor);
   if(_LD_Features.z>0.5) c=LD_PosterizeLuma(c,_LD_Bands.x,lerp(_LD_Bands.y,_LD_Bands.z,actor),_LD_Bands.w);
   float f;c=Fog(c,fogPixel,affinity,f);
   if(_LD_PixelDebug.y==3) return float4(f.xxx,1);
   if(_LD_PixelDebug.y==4) { float ignored;c=Fog(Theme(src.rgb,cov.g),p,cov.b,ignored);return float4(c,1); }
   if(_LD_PixelDebug.y==5) return float4(Bloom(p),1);
   // The legal source copy excludes this independent energy cache. Add once,
   // after the Bloom source chain, so v1.1 outer glow is never bloomed twice.
   c+=Bloom(p)+Glow(p);
   if(_LD_Features.w>0.5) c=NeutralTonemap(max(c,0));
   return float4(c,src.a);
  }
  ENDHLSL
  Pass { Name "StyleHDR" HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment StyleHDR
  ENDHLSL }
  Pass { Name "LogicalGrid" HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment LogicalGrid
  ENDHLSL }
  Pass { Name "Composite" HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Composite
  ENDHLSL }
 }
}
