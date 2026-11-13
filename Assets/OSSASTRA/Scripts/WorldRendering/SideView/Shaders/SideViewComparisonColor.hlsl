/*
 * 横版 B 色彩适配候选。原作从 Gamma 工程取样；本项目在线性管线调用。
 * 身体与环境和角色光图三个贡献相互独立，透明度与顶点位置由原绘制 Pass 管理。
 */
#ifndef OSSASTRA_SIDE_VIEW_COMPARISON_COLOR
#define OSSASTRA_SIDE_VIEW_COMPARISON_COLOR

float3 SideViewEncodeGamma(float3 linearRgb)
{
    linearRgb = max(0, linearRgb);
    #if defined(UNITY_COLORSPACE_GAMMA)
    return linearRgb;
    #else
    return lerp(12.92 * linearRgb, 1.055 * pow(linearRgb, 1.0 / 2.4) - .055, step(.0031308, linearRgb));
    #endif
}

float3 SideViewDecodeGamma(float3 gammaRgb)
{
    gammaRgb = max(0, gammaRgb);
    #if defined(UNITY_COLORSPACE_GAMMA)
    return gammaRgb;
    #else
    return lerp(gammaRgb / 12.92, pow((gammaRgb + .055) / 1.055, 2.4), step(.04045, gammaRgb));
    #endif
}

float3 SideViewOriginalHeroColor(float3 linearRgb, float3 ambientGamma, float3 regionTintGamma, float desaturation)
{
    float3 source = SideViewEncodeGamma(linearRgb);
    // IS_HERO 变体固定混合四成双倍环境色。区域未启用时作者色为白。
    float3 tinted = source * regionTintGamma;
    tinted = lerp(tinted, tinted * (2 * ambientGamma), .4);
    float brightness = saturate(dot(tinted, float3(.220, .707, .071)));
    // 开场原值是 -.75，保留带符号的插值；饱和限制只用于亮度。
    tinted = lerp(tinted, brightness.xxx, desaturation);
    return SideViewDecodeGamma(tinted);
}

float3 SideViewOriginalWorldColor(float3 linearTexture, float3 authoredTintGamma, float3 ambientGamma, float ambientWeight)
{
    float3 source = SideViewEncodeGamma(linearTexture) * authoredTintGamma;
    return SideViewDecodeGamma(lerp(source, source * (2 * ambientGamma), saturate(ambientWeight)));
}

#endif
