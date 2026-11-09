// Tutorial-art color dataflow. Environment/lighting stays in the existing shared environment helper, called once by Forward.
#ifndef OSSASTRA_GRASS_ART_COLOR
#define OSSASTRA_GRASS_ART_COLOR
float GrassHeightBlend(float rootToTip)
{return smoothstep(0,max(_HeightBlend,.0001),saturate(rootToTip));}
float GrassNearFactor(float viewDistance)
{return 1-smoothstep(_NearFarRange.x,max(_NearFarRange.x+.0001,_NearFarRange.y),max(viewDistance,0));}
float3 GrassDepthColor(float viewDistance)
{return lerp(_FarColor.rgb,_NearColor.rgb,GrassNearFactor(viewDistance))*max(_DepthColorStrength,0);}
float4 GrassTerrainSample(float3 root)
{
 if(_TerrainColorValid<.5||_TerrainRect.z<=.0001||_TerrainRect.w<=.0001)return float4(_BottomColor.rgb,0);
 float2 uv=(root.xz-_TerrainRect.xy)/_TerrainRect.zw;
 if(any(uv<0)||any(uv>1))return float4(_BottomColor.rgb,0);
 float4 source=SAMPLE_TEXTURE2D_LOD(_TerrainColor,sampler_TerrainColor,uv,0);
 return source.a>.001?float4(source.rgb,1):float4(_BottomColor.rgb,0);
}
float GrassLinkedHeight(float height,float coarse,float4 wind)
{
#if defined(_GRASS_WIND)
 if(_WindMode>3.5)
 {
  // Final graph has Add->Saturate->Multiply(Height). Blend to it with actual wind energy so no-wind restores base color.
  float linked=height*saturate(height+coarse);
  return lerp(height,linked,saturate(_WindColorStrength)*GrassWindStrength(wind));
 }
#endif
 return height;
}
float3 GrassVideoArt(float3 textureRgb,float3 terrain,float rootToTip,float viewDistance,float shadow,float coarse,float4 wind)
{
 float height=GrassLinkedHeight(GrassHeightBlend(rootToTip),coarse,wind);
 float3 shadowTerrain=lerp(terrain*_ShadowColor.rgb,terrain,saturate(shadow));
 float3 shadowOrBottom=_UseTerrainColor>.5?shadowTerrain:_BottomColor.rgb;
 // Visible video Lerp: A=TerrainColor+Depth, B=Shadow-or-Bottom; root weight adaptation is per clump, not mesh/world Y.
 float3 result=lerp(terrain+GrassDepthColor(viewDistance),shadowOrBottom,height);
 result*=lerp(float3(1,1,1),textureRgb,saturate(_RGBDetail));
 return max(result*_Color.rgb,0);
}
#endif
