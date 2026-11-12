// Side-view material receiver for the shared environment and complete-silhouette lighting.
#ifndef OSSASTRA_SIDE_VIEW_LIGHTING_INCLUDED
#define OSSASTRA_SIDE_VIEW_LIGHTING_INCLUDED
#include "Assets/OSSASTRA/Scripts/EnvironmentSimulation/Rendering/EnvironmentLighting.hlsl"
#include "Assets/OSSASTRA/Scripts/CharacterLighting/Rendering/CharacterLightingRim.hlsl"
float4 _SideViewPresentationOffset;
float3 SideViewShade(float3 source, float alpha, float2 pixel, float3 world)
{
    if (_CharacterLightControls.x < .5) return source;
    float3 normal = EnvironmentCharacterNormal(-UNITY_MATRIX_V[2].xyz);
    if (alpha <= .00001) return EnvironmentShadeLegacy(source, world, normal, 2);
    return EnvironmentShadeWithRim(source, world, normal, 2, CharacterLightingSurfaceRim(pixel, world));
}
#endif
