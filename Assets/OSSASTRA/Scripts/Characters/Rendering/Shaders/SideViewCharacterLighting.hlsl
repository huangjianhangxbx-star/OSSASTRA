// Side-view material receiver for the shared environment and complete-silhouette lighting.
#ifndef OSSASTRA_SIDE_VIEW_LIGHTING_INCLUDED
#define OSSASTRA_SIDE_VIEW_LIGHTING_INCLUDED
#include "Assets/OSSASTRA/Scripts/EnvironmentSimulation/Rendering/EnvironmentLighting.hlsl"
#include "Assets/OSSASTRA/Scripts/CharacterLighting/Rendering/CharacterLightingRim.hlsl"
#include "Assets/OSSASTRA/Scripts/WorldRendering/SideView/Shaders/SideViewComparisonColor.hlsl"
float4 _SideViewPresentationOffset;
float4 _SideViewOriginalHeroTint, _SideViewAmbientGamma;
float _SideViewRenderMode;
float3 SideViewShade(float3 source, float alpha, float2 pixel, float3 world)
{
    // B 在反预乘后沿用原作 Gamma 受色，透明层仍交由现有角色混合约定。
    if (_SideViewRenderMode >= 1.5 && alpha > .00001)
        return SideViewOriginalHeroColor(source / alpha, _SideViewAmbientGamma.rgb, _SideViewOriginalHeroTint.rgb, -.75) * alpha;
    if (_CharacterLightControls.x < .5) return source;
    float3 normal = EnvironmentCharacterNormal(-UNITY_MATRIX_V[2].xyz);
    if (alpha <= .00001) return EnvironmentShadeLegacy(source, world, normal, 2);
    return EnvironmentShadeWithRim(source, world, normal, 2, CharacterLightingSurfaceRim(pixel, world));
}
#endif
