/* 功能：角色光感的纯线性增益运算：兼容底光染色、主体环境基础色度、克制轮廓预算和总增益软肩。
 * 边界：调用者负责role/接收者/空间许可/真实光源资格；本文件不采屏幕颜色、不改alpha或深度。
 * PMA：所有非线性只作用于独立于贴图/slot alpha的光照增益；最后统一乘原色，不能直接压预乘RGB。
 * 归属：本项目原创数学；既有Unity/Spine采样、混合与许可保持在原适配Shader。
 */
#ifndef OSSASTRA_CHARACTER_LIGHTING_COLOR_INCLUDED
#define OSSASTRA_CHARACTER_LIGHTING_COLOR_INCLUDED

// 唯一合法相机的逐接收者作用域上传；x是模块准入，y/z为轮廓/保护开关后的强度。
float4 _CharacterLightControls; // enabled, rimStrength, protectStrength, maxRimGain
float4 _CharacterLightShoulder; // knee, ceiling, reserved, reserved
float4 _CharacterEnvironmentTint; // 环境目标RGB、染色有效强度；不含D/L或最低亮度。
float _CharacterEnvironmentBlend; // 0为原A路径，1为增强基础贡献；由接收者单独上传和清除。

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

// 用IEEE指数位拒绝NaN/Inf；当前DX11实测isfinite未能阻断已上传NaN，不能仅依赖该内建。
bool CharacterLightingFinite(float value)
{
    return (asuint(value) & 0x7f800000u) != 0x7f800000u;
}
bool CharacterLightingFinite(float3 value)
{
    return all((asuint(value) & 0x7f800000u) != 0x7f800000u);
}
bool CharacterLightingFinite(float4 value)
{
    return all((asuint(value) & 0x7f800000u) != 0x7f800000u);
}

// 非法/近黑目标不建立增强分支，调用者直接保留旧A；避免零强度回成“无染色”而非用户基线。
bool CharacterLightingHasEnvironmentTint(float4 tint)
{
    return CharacterLightingFinite(tint) && tint.w > 0 && max(max(tint.r, tint.g), tint.b) > .000001;
}

// 只改变B=A+实际保底缺口的色度；目标不是再乘一次原冷色底光，也不处理纹理或主/局部灯。
float3 CharacterLightingTintBasis(float3 basis, float4 tint)
{
    if (!CharacterLightingFinite(basis)) return float3(0, 0, 0);
    basis = max(basis, 0);
    if (!CharacterLightingHasEnvironmentTint(tint)) return basis;
    float luminance = CharacterLightingLuminance(basis);
    if (luminance <= .000001) return basis;
    float3 target = max(tint.rgb, 0);
    target /= max(max(target.r, target.g), target.b);
    // peak归一后的目标Y至少.0722（纯蓝）。1/16的显式保险把单位色度增益限在16内，
    // 因而无需旧.25通道下限，也不会为饱和暖/冷色引入无界亮度除法。
    float3 unitChroma = target / max(CharacterLightingLuminance(target), .0625);
    return lerp(basis, unitChroma * luminance, saturate(tint.w));
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
