/* 功能：只在合法角色片元内，将完整自身轮廓内带与主灯/合格局部灯方向结合。
 * 边界：不绘制新几何、不扩张alpha、不生成世界许可；主灯复用真实阴影/云，点灯资格由显式实体LOS接线。
 * 无有效光/近零屏幕投影时增益为0；亮度保底不作为轮廓来源。 */
#ifndef OSSASTRA_CHARACTER_LIGHTING_RIM_INCLUDED
#define OSSASTRA_CHARACTER_LIGHTING_RIM_INCLUDED
#include "CharacterLightingSurface.hlsl"
float4 _CharacterRimShape;
float4 _CharacterRimLocalPosition, _CharacterRimLocalColor;
float CharacterLightingDirectionWeight(float2 outward,float3 direction)
{
 float3 view=mul((float3x3)UNITY_MATRIX_V,direction);
 float2 projected=float2(view.x,-view.y);float lengthSquared=dot(projected,projected);
 if(lengthSquared<.00001)return 0;
 float agreement=saturate(dot(outward,projected*rsqrt(lengthSquared)));
 return agreement*agreement*saturate(sqrt(lengthSquared)*2);
}
float3 CharacterLightingSurfaceRim(float2 rasterXY,float3 world)
{
 if(_EnvironmentLightingEnabled<.5||_EnvironmentSurfaceRole<1.5||_CharacterLightControls.x<.5||_CharacterLightControls.y<=0)return 0;
 float3 edge=CharacterLightingRimDirectionAndBand(rasterXY,_CharacterRimShape.x,_CharacterRimShape.y);
 if(edge.z<=0)return 0;
 float3 mainDirection=_MainLightPosition.xyz;float mainLength=dot(mainDirection,mainDirection);
 float3 rim=0;
 if(mainLength>.00001)
  rim=_MainLightColor.rgb*CharacterLightingDirectionWeight(edge.xy,mainDirection*rsqrt(mainLength))*EnvironmentMainShadow(world)*EnvironmentDirectCloud(world);
 float3 delta=_CharacterRimLocalPosition.xyz-world;float squareDistance=dot(delta,delta),range=max(_CharacterRimLocalPosition.w,.01);
 if(_CharacterRimLocalColor.w>.5&&squareDistance>.00001)
 {
  float falloff=saturate(1-squareDistance/(range*range));falloff*=falloff;
  rim+=_CharacterRimLocalColor.rgb*falloff*CharacterLightingDirectionWeight(edge.xy,delta*rsqrt(squareDistance));
 }
 return max(rim,0)*edge.z;
}
#endif
