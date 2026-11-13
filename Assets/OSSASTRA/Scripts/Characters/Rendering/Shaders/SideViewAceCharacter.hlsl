// LitRole scene-light branch, using the project's existing PMA/straight-alpha slot contracts.
#ifndef OSSASTRA_SIDE_VIEW_ACE_CHARACTER_INCLUDED
#define OSSASTRA_SIDE_VIEW_ACE_CHARACTER_INCLUDED
#include "Assets/OSSASTRA/Scripts/WorldRendering/SideView/Shaders/SideViewAceLightMaps.hlsl"

float4 _SideViewPresentationOffset;

float3 SideViewAceCharacterColor(float3 premultiplied, float alpha, float4 positionCS)
{
    // Spine can store additive energy in RGB with a zero alpha. It cannot be un-premultiplied.
    if (alpha <= .00001 || _SideViewAceLightMapsReady < .5)
        return premultiplied;

    SideViewAceMaps maps = SideViewAceSample(positionCS, 1);
    // These character atlases lack verified Extra/Extra2 channels. Bright, shifted highlight,
    // palette remap and emission require those inputs, so only the verified main term is used.
#if defined(UNITY_COLORSPACE_GAMMA)
    return premultiplied * maps.ambient;
#else
    float3 source = premultiplied / alpha;
    return SideViewAceDecode(SideViewAceEncode(source) * maps.ambient) * alpha;
#endif
}
#endif
