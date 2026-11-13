// 仅 C 场景和复刻角色消费同相机光图。图中颜色为 Gamma 数值，方向/发光为线性数据。
#ifndef OSSASTRA_SIDE_VIEW_ACE_LIGHT_MAPS_INCLUDED
#define OSSASTRA_SIDE_VIEW_ACE_LIGHT_MAPS_INCLUDED

TEXTURE2D(_SideViewAceWorldDirection); TEXTURE2D(_SideViewAceWorldHighlight);
TEXTURE2D(_SideViewAceWorldBright); TEXTURE2D(_SideViewAceWorldAmbient);
TEXTURE2D(_SideViewAceCharacterDirection); TEXTURE2D(_SideViewAceCharacterHighlight);
TEXTURE2D(_SideViewAceCharacterBright); TEXTURE2D(_SideViewAceCharacterAmbient);
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
float _SideViewAceLightMapsReady;
float4 _SideViewAceScreenToMap;

struct SideViewAceMaps
{
    float3 ambient;
    float3 bright;
    float3 highlight;
    float2 direction;
    float emission;
};

float3 SideViewAceEncode(float3 value)
{
    value = max(0, value);
    return lerp(value * 12.92, 1.055 * pow(value, 1.0 / 2.4) - .055, step(.0031308, value));
}

float3 SideViewAceDecode(float3 value)
{
    value = max(0, value);
    return lerp(value / 12.92, pow((value + .055) / 1.055, 2.4), step(.04045, value));
}

SideViewAceMaps SideViewAceSampleUv(float2 uv, float group)
{
    SideViewAceMaps maps;
    maps.ambient = 1;
    maps.bright = maps.highlight = 0;
    maps.direction = 0;
    maps.emission = 0;
    if (_SideViewAceLightMapsReady < .5) return maps;

    float4 encoded;
    if (group < .5)
    {
        encoded = SAMPLE_TEXTURE2D(_SideViewAceWorldDirection, sampler_LinearClamp, uv);
        maps.ambient = SAMPLE_TEXTURE2D(_SideViewAceWorldAmbient, sampler_LinearClamp, uv).rgb;
        maps.bright = SAMPLE_TEXTURE2D(_SideViewAceWorldBright, sampler_LinearClamp, uv).rgb;
        maps.highlight = SAMPLE_TEXTURE2D(_SideViewAceWorldHighlight, sampler_LinearClamp, uv).rgb;
    }
    else
    {
        encoded = SAMPLE_TEXTURE2D(_SideViewAceCharacterDirection, sampler_LinearClamp, uv);
        maps.ambient = SAMPLE_TEXTURE2D(_SideViewAceCharacterAmbient, sampler_LinearClamp, uv).rgb;
        maps.bright = SAMPLE_TEXTURE2D(_SideViewAceCharacterBright, sampler_LinearClamp, uv).rgb;
        maps.highlight = SAMPLE_TEXTURE2D(_SideViewAceCharacterHighlight, sampler_LinearClamp, uv).rgb;
    }
    maps.direction = encoded.rg * 2 - 1;
    maps.emission = encoded.b;
    return maps;
}

SideViewAceMaps SideViewAceSample(float4 positionCS, float group)
{
    float2 uv = positionCS.xy * _SideViewAceScreenToMap.xy;
    uv.y = uv.y * _SideViewAceScreenToMap.z + _SideViewAceScreenToMap.w;
    return SideViewAceSampleUv(uv, group);
}

SideViewAceMaps SideViewAceSampleLinear(float4 positionCS, float group)
{
    SideViewAceMaps maps = SideViewAceSample(positionCS, group);
    maps.ambient = SideViewAceDecode(maps.ambient);
    maps.bright = SideViewAceDecode(maps.bright);
    maps.highlight = SideViewAceDecode(maps.highlight);
    return maps;
}

// Current scene art has painted relief but no matched BrightTex. Keep that mask zero until authored data exists.
// Call with straight RGB and preserve the original alpha and blend at the draw site.
float3 SideViewAceWorldColor(float3 linearTexture, float3 authoredTintGamma, float alpha, float4 positionCS, float response)
{
    float3 source = SideViewAceEncode(linearTexture) * authoredTintGamma;
    if (alpha <= .00001 || response <= 0 || _SideViewAceLightMapsReady < .5) return SideViewAceDecode(source);
    SideViewAceMaps maps = SideViewAceSample(positionCS, 0);
    return SideViewAceDecode(lerp(source, source * maps.ambient, saturate(response)));
}

// Optional source-faithful path for a card that actually supplies a verified BrightTex and an offset alpha sample.
float3 SideViewAceWorldColor(float3 linearTexture, float3 authoredTintGamma, float alpha, float4 positionCS, float response,
    float4 brightTex, float offsetAlpha, float3 emissionGamma, float emissionIntensity)
{
    float3 base = SideViewAceEncode(linearTexture);
    float3 source = base * authoredTintGamma;
    if (alpha <= .00001 || response <= 0 || _SideViewAceLightMapsReady < .5) return SideViewAceDecode(source);
    SideViewAceMaps maps = SideViewAceSample(positionCS, 0);
    float highlightWeight = brightTex.g * max(1 - offsetAlpha, brightTex.b);
    float3 lit = (base * maps.ambient + maps.bright * brightTex.r + maps.highlight * highlightWeight) * authoredTintGamma;
    lit += emissionGamma * max(0, saturate(maps.emission) * brightTex.a * emissionIntensity);
    return SideViewAceDecode(lerp(source, lit, saturate(response)));
}

#endif
