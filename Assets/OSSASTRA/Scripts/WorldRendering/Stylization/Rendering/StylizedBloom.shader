Shader "Hidden/Ossastra/WorldRendering/StylizedBloom"
{
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" }
  Cull Off ZWrite Off ZTest Always
  HLSLINCLUDE
  #pragma target 4.5
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
  #include "StylizedLookMath.hlsl"
  TEXTURE2D(_LD_LowTexture);
  TEXTURE2D(_LD_Coverage);
  CBUFFER_START(UnityPerMaterial)
   float4 _LD_SourceSize; // xy input dimensions; zw output dimensions
   float4 _LD_LowSize;
   float4 _LD_Bloom; // threshold, knee, max peak, actor contribution
   float _LD_Scatter;
  CBUFFER_END
  float4 Fetch(int2 p) { return LOAD_TEXTURE2D_X(_BlitTexture,clamp(p,int2(0,0),int2(_LD_SourceSize.xy)-1)); }
  float4 Reduce(Varyings i, bool first)
  {
   // Area footprint also handles odd dimensions. Every source texel belongs to
   // exactly one output cell; no dropped odd trailing row or hidden sample.
   int2 cell=int2(i.positionCS.xy);
   int2 begin=(int2)floor(float2(cell)*_LD_SourceSize.xy/_LD_SourceSize.zw);
   int2 end=(int2)floor(float2(cell+1)*_LD_SourceSize.xy/_LD_SourceSize.zw);
   end=max(begin+1,end); float3 sum=0; float count=0; float validity=1;
   [unroll] for(int y=0;y<3;y++) [unroll] for(int x=0;x<3;x++)
   {
    int2 p=begin+int2(x,y); if(any(p>=end)) continue;
    float4 s=Fetch(p);validity=min(validity,s.a);
    if(first) s.rgb*=lerp(1,_LD_Bloom.w,saturate(LOAD_TEXTURE2D(_LD_Coverage,p).g));
    sum+=s.rgb;count++;
   }
   if(validity<0.998) return 0;
   float3 c=sum/max(count,1);
   if(first) c=LD_BloomPrefilter(c,_LD_Bloom.x,_LD_Bloom.y,_LD_Bloom.z);
   return float4(c,1);
  }
  float4 Prefilter(Varyings i):SV_Target { return Reduce(i,true); }
  float4 Down(Varyings i):SV_Target { return Reduce(i,false); }
  float4 LowBilinear(float2 p)
  {
   int2 a=int2(floor(p));float2 t=frac(p);int2 hi=int2(_LD_LowSize.xy)-1;
   float4 s00=LOAD_TEXTURE2D(_LD_LowTexture,clamp(a,int2(0,0),hi));
   float4 s10=LOAD_TEXTURE2D(_LD_LowTexture,clamp(a+int2(1,0),int2(0,0),hi));
   float4 s01=LOAD_TEXTURE2D(_LD_LowTexture,clamp(a+int2(0,1),int2(0,0),hi));
   float4 s11=LOAD_TEXTURE2D(_LD_LowTexture,clamp(a+int2(1,1),int2(0,0),hi));
   float validity=min(min(s00.a,s10.a),min(s01.a,s11.a));
   return float4(lerp(lerp(s00.rgb,s10.rgb,t.x),lerp(s01.rgb,s11.rgb,t.x),t.y),validity);
  }
  float4 Up(Varyings i):SV_Target
  {
   int2 p=int2(i.positionCS.xy);float4 baseColor=Fetch(p);
   if(baseColor.a<0.998) return 0;
   float2 low=(float2(p)+0.5)*_LD_LowSize.xy/_LD_SourceSize.xy-0.5;
   float3 sum=0;float validity=1;
   [unroll]for(int y=-1;y<=1;y++)[unroll]for(int x=-1;x<=1;x++)
   {
    float4 s=LowBilinear(low+float2(x,y));
    float weight=(x==0?2.0:1.0)*(y==0?2.0:1.0);
    sum+=s.rgb*weight;validity=min(validity,s.a);
   }
   if(validity<0.998) return 0;
   return float4(lerp(baseColor.rgb,sum/16.0,_LD_Scatter),1);
  }
  ENDHLSL
  Pass { Name "Prefilter" HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Prefilter
  ENDHLSL }
  Pass { Name "Downsample" HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Down
  ENDHLSL }
  Pass { Name "Upsample" HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Up
  ENDHLSL }
 }
}
