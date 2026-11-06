/* 功能：显式体素材质的共享URP14 PBR响应；普通/许可入口使用同一漫反射、镜面与诊断。
 * 边界：不声明材质参数，不改变2D路径/光源/环境帧/深度/许可；调用前由入口初始化InputData和房间GI。
 * 接口：调用方UnityPerMaterial提供任务记录中的_Voxel*参数，并定义ENVIRONMENT_NATIVE_LIT。
 * 上游BRDF、灯循环与GI查询：本机URP14.2.0-t1（Unity Technologies，Unity Companion License）。
 * mixed说明：bakedGI仍是URP的烘焙/探针漫反射通道；不声称拆出其中已烘焙的直射与真实反弹。
 */
#ifndef OSSASTRA_VOXEL_SURFACE_LIGHTING_INCLUDED
#define OSSASTRA_VOXEL_SURFACE_LIGHTING_INCLUDED

#include "Assets/OSSASTRA/Scripts/EnvironmentSimulation/Rendering/EnvironmentLighting.hlsl"

struct VoxelLightingContributions
{
    half3 diffuse;
    half3 specular;
};

// Shader也拒绝NaN/Inf；材质作者/Core负责范围校验，这里只提供有界的失效回退。
float VoxelFiniteControl(float value, float fallback)
{
    return (asuint(value) & 0x7f800000u) == 0x7f800000u ? fallback : value;
}

// 连续艺术方向响应：f(0)=0/f(1)=1；导数(1-s)+6s*x*(1-x)>=0。
// 默认强度0；仅主方向光使用非零shape，额外方向/点/聚光继续原始NdotL。
float VoxelShapeNdotL(float normalLight, float strength)
{
    float x = saturate(normalLight);
    float s = saturate(VoxelFiniteControl(strength, 0));
    return lerp(x, x * x * (3 - 2 * x), s);
}

// 对线性光贡献使用同一RGB倍率；knee/ceiling约束peak-RGB，不逐通道截断。
// diffuse与specular独立累计预算，镜面观察方向不驱动漫反射压缩；Emission不进入本函数。
float VoxelHighlightScale(float3 lightContribution)
{
    float amount = saturate(VoxelFiniteControl(_VoxelHighlightStrength, 0));
    if (amount <= 0) return 1;
    float peak = max(max(lightContribution.r, lightContribution.g), lightContribution.b);
    float knee = max(VoxelFiniteControl(_VoxelHighlightKnee, 4), .0001);
    if (peak <= knee) return 1;
    float shoulder = max(VoxelFiniteControl(_VoxelHighlightCeiling, 8) - knee, .0001);
    float compressed = knee + shoulder * (1 - exp(-(peak - knee) / shoulder));
    return lerp(1, compressed / max(peak, .0001), amount);
}

float3 VoxelProtectLight(float3 lightContribution)
{
    return lightContribution * VoxelHighlightScale(lightContribution);
}

// 分离的是BRDF的diffuse/specular，不替换统一radiance；镜面和coat保留原NdotL。
// light已经携带URP cookie、direct AO、距离和真实阴影；main的云影在混GI之后只施加一次。
VoxelLightingContributions VoxelEvaluateDirectLighting(
    BRDFData brdfData, BRDFData brdfDataClearCoat, Light light,
    half3 normalWS, half3 viewDirectionWS, half clearCoatMask,
    bool specularHighlightsOff, half shape)
{
    VoxelLightingContributions terms = (VoxelLightingContributions)0;
    half normalLight = saturate(dot(normalWS, light.direction));
    float3 attenuation = light.color * (light.distanceAttenuation * light.shadowAttenuation);
    terms.diffuse = brdfData.diffuse * (attenuation * VoxelShapeNdotL(normalLight, shape));

#ifndef _SPECULARHIGHLIGHTS_OFF
    [branch] if (!specularHighlightsOff)
    {
        half3 specular = brdfData.specular * DirectBRDFSpecular(brdfData, normalWS, light.direction, viewDirectionWS);
#if defined(_CLEARCOAT) || defined(_CLEARCOATMAP)
        half coatBRDF = kDielectricSpec.r * DirectBRDFSpecular(brdfDataClearCoat, normalWS, light.direction, viewDirectionWS);
        half normalView = saturate(dot(normalWS, viewDirectionWS));
        half coatFresnel = kDielectricSpec.x + kDielectricSpec.a * Pow4(1 - normalView);
        half baseTransmission = 1 - clearCoatMask * coatFresnel;
        terms.diffuse *= baseTransmission;
        specular = specular * baseTransmission + coatBRDF * clearCoatMask;
#endif
        float response = max(VoxelFiniteControl(_VoxelDirectSpecular, 1), 0);
        terms.specular = specular * (attenuation * normalLight) * response;
    }
#endif
    return terms;
}

// 反射采样和贡献组合分开，使GPU合同可输入已知HDR反射；生产仍只走原生查询一次。
// AO在组合末尾只乘一次；coat反射与base反射一起受环境镜面响应控制。
VoxelLightingContributions VoxelEvaluateIndirectContributions(
    BRDFData brdfData, BRDFData brdfDataClearCoat, half clearCoatMask,
    half3 bakedGI, half3 indirectSpecular, half3 coatIndirectSpecular,
    half fresnelTerm, half occlusion)
{
    VoxelLightingContributions terms;
    float diffuseResponse = max(VoxelFiniteControl(_VoxelIndirectDiffuse, 1), 0);
    float specularResponse = max(VoxelFiniteControl(_VoxelEnvironmentSpecular, 1), 0);
    terms.diffuse = bakedGI * brdfData.diffuse * diffuseResponse;
    terms.specular = indirectSpecular * EnvironmentBRDFSpecular(brdfData, fresnelTerm) * specularResponse;
#if defined(_CLEARCOAT) || defined(_CLEARCOATMAP)
    half coatFresnel = kDielectricSpec.x + kDielectricSpec.a * fresnelTerm;
    half baseTransmission = 1 - coatFresnel * clearCoatMask;
    terms.diffuse *= baseTransmission;
    terms.specular = terms.specular * baseTransmission
        + EnvironmentBRDFClearCoat(brdfDataClearCoat, clearCoatMask, coatIndirectSpecular, fresnelTerm) * specularResponse;
#endif
    terms.diffuse *= occlusion;
    terms.specular *= occlusion;
    return terms;
}

VoxelLightingContributions VoxelEvaluateGlobalIllumination(
    BRDFData brdfData, BRDFData brdfDataClearCoat, half clearCoatMask,
    half3 bakedGI, half occlusion, float3 positionWS,
    half3 normalWS, half3 viewDirectionWS, float2 normalizedScreenSpaceUV)
{
    // 保留URP白底AO诊断，不把材质倍率/保护混入AO结果。
    if (IsOnlyAOLightingFeatureEnabled())
    {
        VoxelLightingContributions aoTerms = (VoxelLightingContributions)0;
        aoTerms.diffuse = GlobalIllumination(brdfData, brdfDataClearCoat, clearCoatMask,
            bakedGI, occlusion, positionWS, normalWS, viewDirectionWS,
            normalizedScreenSpaceUV UNITY_GDRP_INSTANCE_ZERO_ARGUMENT);
        return aoTerms;
    }
    half3 reflectVector = reflect(-viewDirectionWS, normalWS);
    half normalView = saturate(dot(normalWS, viewDirectionWS));
    half fresnelTerm = Pow4(1 - normalView);
    half3 reflection = GlossyEnvironmentReflection(reflectVector, positionWS, brdfData.perceptualRoughness,
        1.0h, normalizedScreenSpaceUV UNITY_GDRP_INSTANCE_ZERO_ARGUMENT);
    half3 coatReflection = 0;
#if defined(_CLEARCOAT) || defined(_CLEARCOATMAP)
    coatReflection = GlossyEnvironmentReflection(reflectVector, positionWS, brdfDataClearCoat.perceptualRoughness,
        1.0h, normalizedScreenSpaceUV UNITY_GDRP_INSTANCE_ZERO_ARGUMENT);
#endif
    return VoxelEvaluateIndirectContributions(brdfData, brdfDataClearCoat, clearCoatMask,
        bakedGI, reflection, coatReflection, fresnelTerm, occlusion);
}

// 唯一三维体素片元受光入口。前后许可/alpha clip/fog/depth继续由薄Shader入口处理。
half4 VoxelUniversalFragmentPBR(InputData inputData, SurfaceData surfaceData)
{
    EnvironmentApplyNativeWetness(surfaceData, inputData.positionWS);
#if defined(_SPECULARHIGHLIGHTS_OFF)
    bool specularHighlightsOff = true;
#else
    bool specularHighlightsOff = false;
#endif
    BRDFData brdfData;
    InitializeBRDFData(surfaceData, brdfData);
#if defined(DEBUG_DISPLAY)
    half4 debugColor;
    if (CanDebugOverrideOutputColor(inputData, surfaceData, brdfData, debugColor)) return debugColor;
#endif
    BRDFData brdfDataClearCoat = CreateClearCoatBRDFData(surfaceData, brdfData);
    half4 shadowMask = CalculateShadowMask(inputData);
    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData, surfaceData);
    uint meshRenderingLayers = GetMeshRenderingLayer();
    Light mainLight = GetMainLight(inputData, shadowMask, aoFactor);

    // 与本机原URP一致：先让mixed/subtractive用原灯混合，再只缩实时主直射云场。
    MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI);
    mainLight.color *= EnvironmentDirectCloud(inputData.positionWS);

    VoxelLightingContributions gi = VoxelEvaluateGlobalIllumination(brdfData, brdfDataClearCoat,
        surfaceData.clearCoatMask, inputData.bakedGI, aoFactor.indirectAmbientOcclusion,
        inputData.positionWS, inputData.normalWS, inputData.viewDirectionWS, inputData.normalizedScreenSpaceUV);
    VoxelLightingContributions main = (VoxelLightingContributions)0;
    VoxelLightingContributions additional = (VoxelLightingContributions)0;
#ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
#endif
    {
        main = VoxelEvaluateDirectLighting(brdfData, brdfDataClearCoat, mainLight,
            inputData.normalWS, inputData.viewDirectionWS, surfaceData.clearCoatMask,
            specularHighlightsOff, _VoxelMainShape);
    }

#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();
#if USE_CLUSTERED_LIGHTING
    for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
#endif
        {
            VoxelLightingContributions terms = VoxelEvaluateDirectLighting(brdfData, brdfDataClearCoat, light,
                inputData.normalWS, inputData.viewDirectionWS, surfaceData.clearCoatMask, specularHighlightsOff, 0);
            additional.diffuse += terms.diffuse;
            additional.specular += terms.specular;
        }
    }
#endif
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
#endif
        {
            VoxelLightingContributions terms = VoxelEvaluateDirectLighting(brdfData, brdfDataClearCoat, light,
                inputData.normalWS, inputData.viewDirectionWS, surfaceData.clearCoatMask, specularHighlightsOff, 0);
            additional.diffuse += terms.diffuse;
            additional.specular += terms.specular;
        }
    LIGHT_LOOP_END
#endif

    half3 vertexDiffuse = 0;
#if defined(_ADDITIONAL_LIGHTS_VERTEX)
    // URP已逐顶点聚合的额外漫反射保持原样；此处没有每灯方向可供重新塑形。
    vertexDiffuse = inputData.vertexLighting * brdfData.diffuse;
#endif
    if (!IsOnlyAOLightingFeatureEnabled())
    {
        float diffuseScale = VoxelHighlightScale(gi.diffuse + main.diffuse + additional.diffuse + vertexDiffuse);
        float specularScale = VoxelHighlightScale(gi.specular + main.specular + additional.specular);
        gi.diffuse *= diffuseScale; main.diffuse *= diffuseScale; additional.diffuse *= diffuseScale; vertexDiffuse *= diffuseScale;
        gi.specular *= specularScale; main.specular *= specularScale; additional.specular *= specularScale;
    }

    int diagnostic = (int)round(VoxelFiniteControl(_VoxelDiagnostic, 0));
    if (diagnostic == 1) return half4(surfaceData.albedo, surfaceData.alpha);
    if (diagnostic == 2) return half4(inputData.normalWS * .5h + .5h, surfaceData.alpha);
    if (diagnostic == 3) return half4(main.diffuse, surfaceData.alpha);
    if (diagnostic == 4) return half4(gi.diffuse, surfaceData.alpha);
    if (diagnostic == 5) return half4(main.specular + additional.specular, surfaceData.alpha);
    if (diagnostic == 6) return half4(gi.specular, surfaceData.alpha);
    // 单独显示URP主阴影，不把cloud、distance、GI或观察许可伪装为阴影。
    if (diagnostic == 7) return half4(mainLight.shadowAttenuation.xxx, surfaceData.alpha);

    LightingData lightingData = CreateLightingData(inputData, surfaceData);
    lightingData.giColor = gi.diffuse + gi.specular;
    lightingData.mainLightColor = main.diffuse + main.specular;
    lightingData.additionalLightsColor = additional.diffuse + additional.specular;
    lightingData.vertexLightingColor = vertexDiffuse;
#if REAL_IS_HALF
    return min(CalculateFinalColor(lightingData, surfaceData.alpha), HALF_MAX);
#else
    return CalculateFinalColor(lightingData, surfaceData.alpha);
#endif
}

#endif
