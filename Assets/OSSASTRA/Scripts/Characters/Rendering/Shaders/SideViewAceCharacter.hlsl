// LitRole scene-light branch, using the project's existing PMA/straight-alpha slot contracts.
#ifndef OSSASTRA_SIDE_VIEW_ACE_CHARACTER_INCLUDED
#define OSSASTRA_SIDE_VIEW_ACE_CHARACTER_INCLUDED
#include "Assets/OSSASTRA/Scripts/WorldRendering/SideView/Shaders/SideViewAceLightMaps.hlsl"

float4 _SideViewPresentationOffset;
// Camera-scoped test override: real map, white, source PMA bypass, or fixed Gamma RGB.
float4 _SideViewAceColorDiagnostic;

float3 SideViewAceCharacterColor(float3 premultiplied, float alpha, float4 positionCS)
{
    // Spine can store additive energy in RGB with a zero alpha. It cannot be un-premultiplied.
    float diagnostic = _SideViewAceColorDiagnostic.x;
    if (alpha <= .00001 || _SideViewAceLightMapsReady < .5 || (diagnostic > 1.5 && diagnostic < 2.5))
        return premultiplied;

    float3 ambient = diagnostic > 2.5 ? _SideViewAceColorDiagnostic.yzw :
        (diagnostic > .5 ? 1 : SideViewAceSample(positionCS, 1).ambient);
    // These character atlases lack verified Extra/Extra2 channels. Bright, shifted highlight,
    // palette remap and emission require those inputs, so only the verified main term is used.
#if defined(UNITY_COLORSPACE_GAMMA)
    return premultiplied * ambient;
#else
    float3 source = premultiplied / alpha;
    return SideViewAceDecode(SideViewAceEncode(source) * ambient) * alpha;
#endif
}
#endif
