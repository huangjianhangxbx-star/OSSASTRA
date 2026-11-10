/* Explicit LOOKDEV Ground/Grass base-color opt-in. Other Environment consumers do not include this file. */
#ifndef OSSASTRA_SURFACE_LIGHTING_GROUND_CHROMA_INCLUDED
#define OSSASTRA_SURFACE_LIGHTING_GROUND_CHROMA_INCLUDED
#include "Assets/OSSASTRA/Scripts/EnvironmentSimulation/Rendering/EnvironmentLighting.hlsl"
float _EnvironmentGroundBaseStrength;
// LOOKDEV_GROUND_BASE_BEGIN
// This artist input is used only by the registered Ground voxel and Environment grass
// entrances BEFORE their ordinary lighting. It cannot recolor character/decoration roles,
// cannot invent light, and does not change alpha, wind, depth or observation permission.
float3 EnvironmentGroundBaseChroma(float3 albedo, float3 world, float role)
{
 if(_EnvironmentLightingEnabled<.5 || !CharacterLightingFinite(_EnvironmentGroundBaseStrength)
    || _EnvironmentGroundBaseStrength<=0 || !CharacterLightingFinite(role) || role!=0
    || !CharacterLightingFinite(_EnvironmentSceneTint) || _EnvironmentSceneTint.w<.5
    || !CharacterLightingFinite(albedo) || any(albedo<0) || EnvironmentRoomIndex(world)>=0) return albedo;
 float3 target=_EnvironmentSceneTint.rgb;
 if(any(target<0))return albedo;
 float peak=max(target.r,max(target.g,target.b));
 if(peak<=.000001)return albedo;
 target/=peak;
 const float3 weights=float3(.2126,.7152,.0722);
 float targetY=dot(target,weights);
 if(targetY<.25)target=lerp(float3(1,1,1),target,.75/max(1-targetY,.000001));
 target/=max(dot(target,weights),.25);
 return lerp(albedo,dot(albedo,weights)*target,clamp(_EnvironmentGroundBaseStrength,0,.5));
}
// LOOKDEV_GROUND_BASE_END
#endif
