// 将完整角色剪影与 ACE 局部光结合。普通透明槽保持原覆盖，亮度保护只压缩光增益。
#ifndef OSSASTRA_SIDE_VIEW_ACE_FUSION_INCLUDED
#define OSSASTRA_SIDE_VIEW_ACE_FUSION_INCLUDED
#include "SideViewAceCharacter.hlsl"
#include "Assets/OSSASTRA/Scripts/CharacterLighting/Rendering/CharacterLightingSurface.hlsl"
#include "Assets/OSSASTRA/Scripts/CharacterLighting/Rendering/CharacterLightingColor.hlsl"

TEXTURE2D(_SideViewAceFusionCoverage);
float _SideViewAceFusionActor;
float4 _SideViewAceFusionRim, _SideViewAceFusionShoulder, _SideViewAceFusionScreen;
float4 _SideViewAceFusionRect[2], _SideViewAceFusionTile[2];
float2 _SideViewAceFusionTexel;

float3 SideViewAceFusionColor(float3 sourcePma, float alpha, float4 positionCS)
{
    if (_SideViewAceFusionActor < .5 || alpha <= .00001 || _SideViewAceLightMapsReady < .5 || _SideViewAceColorDiagnostic.x > .5)
        return SideViewAceCharacterColor(sourcePma, alpha, positionCS);

    float3 ambient = SideViewAceSample(positionCS, 1).ambient;
    float3 gain = SideViewAceDecode(ambient);
    float luma = CharacterLightingLuminance(gain);
    // 可读性只轻抬有贡献的暗色，并随输入降至黑色。它不会生成边缘能量。
    float floorLift = _SideViewAceFusionShoulder.z * smoothstep(0, .025, luma);
    float3 body = lerp(gain, luma.xxx, _SideViewAceFusionShoulder.w) + floorLift;
    float3 rim = 0;
    if (_SideViewAceFusionRim.z > 0)
    {
        int actor = _SideViewAceFusionActor > 1.5 ? 1 : 0;
        SideViewAceEdgeLight light = SideViewAceSampleEdge(positionCS.xy);
        if (light.weight > .0001)
        {
            float width = _SideViewAceFusionRim.x * _SideViewAceFusionScreen.y / 1080;
            float3 shape = CharacterLightingRimDirectionAndBandAt(positionCS.xy, width, _SideViewAceFusionRim.y,
                _SideViewAceFusionRect[actor], _SideViewAceFusionScreen, _SideViewAceFusionTile[actor],
                _SideViewAceFusionTexel, _SideViewAceFusionCoverage, sampler_LinearClamp);
            float facing = saturate(dot(shape.xy, light.direction));
            rim = light.energy * (shape.z * facing * facing * _SideViewAceFusionRim.z);
        }
    }
    float3 limited = CharacterLightingLimitRim(rim, body, _SideViewAceFusionRim.w);
    float3 protectedGain = CharacterLightingProtectGain(body + limited,
        _SideViewAceFusionShoulder.x, _SideViewAceFusionShoulder.y, 1);
#if defined(UNITY_COLORSPACE_GAMMA)
    return sourcePma * SideViewAceEncode(protectedGain);
#else
    return sourcePma * protectedGain;
#endif
}
#endif
