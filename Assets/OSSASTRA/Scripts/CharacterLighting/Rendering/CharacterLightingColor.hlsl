/* 功能：角色光感的纯线性增益运算：环境底光色度、克制轮廓预算和总增益软肩。
 * 边界：调用者负责role/接收者/空间许可/真实光源资格；本文件不采屏幕颜色、不改alpha或深度。
 * PMA：所有非线性只作用于独立于贴图/slot alpha的光照增益；最后统一乘原色，不能直接压预乘RGB。
 * 归属：本项目原创数学；既有Unity/Spine采样、混合与许可保持在原适配Shader。
 */
#ifndef OSSASTRA_CHARACTER_LIGHTING_COLOR_INCLUDED
#define OSSASTRA_CHARACTER_LIGHTING_COLOR_INCLUDED

// 唯一合法相机的逐接收者作用域上传；x是模块准入，y/z为轮廓/保护开关后的强度。
float4 _CharacterLightControls; // enabled, rimStrength, protectStrength, maxRimGain
float4 _CharacterLightShoulder; // knee, ceiling, reserved, reserved

float CharacterLightingLuminance(float3 color)
{
    return dot(color, float3(.2126, .7152, .0722));
}

// 仅环境贡献进入此函数；局部/主灯必须在其后相加，不能对最终颜色重复乘天空色。
float3 CharacterLightingTintAmbient(float3 ambient, float4 tint)
{
    if (tint.w <= 0) return ambient;
    float luminance = CharacterLightingLuminance(ambient);
    if (luminance <= .000001) return ambient;
    // 染色系数不小于1/4，保证亮度重归一化最多4倍；纯黑/近黑环境不会凭空发光。
    float3 multiplier = lerp(float3(1, 1, 1), clamp(tint.rgb, .25, 1), saturate(tint.w));
    float3 tinted = ambient * multiplier;
    float tintedLuminance = CharacterLightingLuminance(tinted);
    return tinted * min(luminance / max(tintedLuminance, .000001), 4);
}

// 轮廓只增加有来源的有限增益；主体很亮时额外预算收窄，防止两者叠成白块。
float3 CharacterLightingLimitRim(float3 rimGain, float3 bodyGain, float maxRimGain)
{
    rimGain = max(rimGain, 0);
    float rimLuminance = CharacterLightingLuminance(rimGain);
    if (rimLuminance <= .000001 || maxRimGain <= 0) return float3(0, 0, 0);
    float bodyLuminance = max(CharacterLightingLuminance(bodyGain), 0);
    float budget = clamp(maxRimGain, 0, 2) / (1 + max(bodyLuminance - 1, 0));
    return rimGain * min(1, budget / max(rimLuminance, .000001));
}

// RGB同倍率软肩不改变光色比例；低于拐点/零强度精确恒等，作用于gain而非预乘后的色。
float3 CharacterLightingProtectGain(float3 gain, float knee, float ceiling, float amount)
{
    if (amount <= 0) return gain;
    float luminance = CharacterLightingLuminance(gain);
    knee = max(knee, .0001);
    if (luminance <= knee) return gain;
    float shoulder = max(ceiling - knee, .0001);
    float compressed = knee + shoulder * (1 - exp(-(luminance - knee) / shoulder));
    return gain * lerp(1, compressed / max(luminance, .000001), saturate(amount));
}

#endif
