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
float _SvOSLightCandidate;
TEXTURE2D(_SvCandidateGradeLut);
float4 _SvCandidateGradeParams;

// 浅色角色预留世界曲线的偏色余量。只估算大色块的泛光，不改背景或透明覆盖。
float3 SideViewCandidateNeutralBalance(float3 lit, float3 source)
{
    if (_SvCandidateGradeParams.x < .5) return lit;
    float3 display = SideViewAceEncode(lit);
    float y = dot(display, float3(.220, .707, .071));
    float energy = max(y - _SvCandidateGradeParams.y, 0);
    float afterBloom = saturate(y + energy * _SvCandidateGradeParams.z);
    float3 neutral = float3(
        SAMPLE_TEXTURE2D(_SvCandidateGradeLut, sampler_LinearClamp, float2(afterBloom, .125)).r,
        SAMPLE_TEXTURE2D(_SvCandidateGradeLut, sampler_LinearClamp, float2(afterBloom, .375)).g,
        SAMPLE_TEXTURE2D(_SvCandidateGradeLut, sampler_LinearClamp, float2(afterBloom, .625)).b);
    float3 cast = neutral - dot(neutral, float3(.220, .707, .071));
    float chroma = max(source.r, max(source.g,source.b)) - min(source.r,min(source.g,source.b));
    float weight = .65 * smoothstep(.2,.6,y) * (1 - .6 * smoothstep(.15,.6,chroma));
    return SideViewAceDecode(max(0, display - cast * weight * _SvCandidateGradeParams.w / (1 + _SvCandidateGradeParams.z)));
}
float4 _SvCandidateActorBody[2], _SvCandidateActorEdges[2];

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
    int actor = _SideViewAceFusionActor > 1.5 ? 1 : 0;
    float4 rimSettings = _SideViewAceFusionRim;
    if (_SvOSLightCandidate > .5)
    {
        rimSettings.x = _SvCandidateActorEdges[actor].x;
        rimSettings.z = _SvCandidateActorEdges[actor].y;
        body = lerp(float3(1,1,1), gain, _SvCandidateActorBody[actor].y);
    }
    if (rimSettings.z > 0)
    {
        SideViewAceEdgeLight light = SideViewAceSampleEdge(positionCS.xy);
        if (light.weight > .0001)
        {
            float width = rimSettings.x * _SideViewAceFusionScreen.y / 1080;
            float3 shape = CharacterLightingRimDirectionAndBandAt(positionCS.xy, width, _SideViewAceFusionRim.y,
                _SideViewAceFusionRect[actor], _SideViewAceFusionScreen, _SideViewAceFusionTile[actor],
                _SideViewAceFusionTexel, _SideViewAceFusionCoverage, sampler_LinearClamp);
            float facing = saturate(dot(shape.xy, light.direction));
            rim = light.energy * (shape.z * facing * facing * rimSettings.z);
        }
    }
    float3 limited = CharacterLightingLimitRim(rim, body, _SideViewAceFusionRim.w);
    if (_SvOSLightCandidate > .5)
    {
        // 在直通线性颜色上按最大通道统一缩放，保留色相，最后还原预乘。
        float3 source = sourcePma / alpha;
#if defined(UNITY_COLORSPACE_GAMMA)
        source = SideViewAceDecode(source);
#endif
        float3 lit = source * body;
        float peak = max(lit.r, max(lit.g, lit.b));
        float midWeight = 1 - smoothstep(.18, .8, peak);
        lit *= lerp(1, _SvCandidateActorBody[actor].x, midWeight);
        lit += source * limited;
        peak = max(lit.r, max(lit.g, lit.b));
        float excess = max(0, peak - .26);
        float shoulder = .26 + excess / (1 + 4 * _SvCandidateActorBody[actor].z * excess);
        if (peak > .26) lit *= shoulder / peak;
        lit = SideViewCandidateNeutralBalance(lit, source);
#if defined(UNITY_COLORSPACE_GAMMA)
        lit = SideViewAceEncode(lit);
#endif
        return lit * alpha;
    }
    float3 protectedGain = CharacterLightingProtectGain(body + limited,
        _SideViewAceFusionShoulder.x, _SideViewAceFusionShoulder.y, 1);
#if defined(UNITY_COLORSPACE_GAMMA)
    return sourcePma * SideViewAceEncode(protectedGain);
#else
    return sourcePma * protectedGain;
#endif
}
#endif
