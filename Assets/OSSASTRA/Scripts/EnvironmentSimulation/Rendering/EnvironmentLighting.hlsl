/* 功能：给原无光照地表与角色提供同一份环境底光、真实主灯阴影及受限室内补光。
 * 边界：只改变表面受光，不修改alpha、深度、观察许可、Caster或源材质；场景融合仅重分配室外环境漫射色度，不染角色、直射或镜面。
 * 生命周期：唯一可见性Pass按相机上传有界全局数据；未启用时严格返回原色。
 * 上游片元函数：Unity URP 14.2.0-t1（Unity Technologies，Unity Companion License）与 Spine URP 4.3.25
 * （Esoteric Software，Spine Runtimes License）；完整许可见 docs/ossastra/ENV-01/*-LICENSE.md。
 */
#ifndef OSSASTRA_ENVIRONMENT_LIGHTING_INCLUDED
#define OSSASTRA_ENVIRONMENT_LIGHTING_INCLUDED

#if !defined(ENVIRONMENT_LEGACY_SPRITE)
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#else
 // UnitySprites依赖旧Unity全局变量，不能再次引入URP Core。此分支只声明URP14主灯阴影ABI。
 float4 _MainLightPosition;
 half4 _MainLightColor;
 Texture2D<float> _MainLightShadowmapTexture;
 SamplerComparisonState sampler_LinearClampCompare;
 CBUFFER_START(LightShadows)
 float4x4 _MainLightWorldToShadow[5];
 float4 _CascadeShadowSplitSpheres0, _CascadeShadowSplitSpheres1;
 float4 _CascadeShadowSplitSpheres2, _CascadeShadowSplitSpheres3;
 float4 _CascadeShadowSplitSphereRadii;
 float4 _MainLightShadowOffset0, _MainLightShadowOffset1;
 float4 _MainLightShadowParams, _MainLightShadowmapSize;
 CBUFFER_END
#endif

#include "Assets/OSSASTRA/Scripts/CharacterLighting/Rendering/CharacterLightingColor.hlsl"
float4 _EnvironmentStylizedSkyTint;
// 场景只调环境漫射色度；w分别为目标有效性和候选开关，未启用严格保留原受光。
float4 _EnvironmentSceneTint;
float4 _EnvironmentSceneChroma; // ground / decoration / voxel / enabled

float _EnvironmentLightingEnabled;
float _EnvironmentFullSurface;
float _EnvironmentSurfaceRole;
float _EnvironmentResponseOverride, _EnvironmentReceiveShadow, _EnvironmentMinimumOverride;
float4 _EnvironmentSkyAmbient, _EnvironmentHorizonAmbient, _EnvironmentGroundAmbient;
float4 _EnvironmentLightResponse;
float4 _EnvironmentCloudParameters, _EnvironmentCloudMotion;
float _EnvironmentWetness, _EnvironmentIndoorAmbientScale;
int _EnvironmentIndoorCount;
float4x4 _EnvironmentIndoorMatrices[32];
float4 _EnvironmentIndoorHalfExtents[32];
int _EnvironmentShelterCount;
float4x4 _EnvironmentShelterMatrices[16];
float4 _EnvironmentShelterExtents[16];
int _EnvironmentLocalLightCount;
float4 _EnvironmentLocalLightPositions[8], _EnvironmentLocalLightColors[8];

#if defined(OSSASTRA_ROOM_SIGHT_CLIP_INCLUDED)
// Full许可已由可见性后端证明，不再受Partial历史表现黑幕影响；此标记不自行授予观察权。
float4 EnvironmentFogColor(float4 color, float3 world, float premultiplied)
{
 return _EnvironmentFullSurface > .5 ? color : RoomSightFogColor(color, world, premultiplied);
}
bool EnvironmentCanShadeWorld(float3 world)
{
 return _EnvironmentFullSurface > .5 || !RoomSightIsHistoryOnly(world);
}
#endif

// 返回明确登记的房间序号；建筑包围盒、主控是否入室不能替代表面所属房间。
int EnvironmentRoomIndex(float3 world)
{
 [loop] for (int room = 0; room < min(max(_EnvironmentIndoorCount, 0), 32); room++)
 {
  float3 local = mul(_EnvironmentIndoorMatrices[room], float4(world, 1)).xyz;
  if (all(abs(local) <= max(_EnvironmentIndoorHalfExtents[room].xyz, 0) + .002)) return room;
 }
 return -1;
}

half EnvironmentIndoorFactor(float3 world)
{
 if (_EnvironmentLightingEnabled < .5) return 1;
 return EnvironmentRoomIndex(world) >= 0 ? saturate(_EnvironmentIndoorAmbientScale) : 1;
}

// 目标色只重新分配这份漫射已有的亮度；黑色仍黑，直射/局部灯/反射/自发光不进入此函数。
float3 EnvironmentSceneDiffuseChroma(float3 diffuse, float3 world, float role, float voxelFamily)
{
 if (_EnvironmentLightingEnabled < .5 || !CharacterLightingFinite(_EnvironmentSceneChroma)
     || !CharacterLightingFinite(_EnvironmentSceneTint) || !CharacterLightingFinite(role)
     || _EnvironmentSceneChroma.w < .5 || _EnvironmentSceneTint.w < .5
     || role < 0 || role > 1.5 || EnvironmentRoomIndex(world) >= 0) return diffuse;
 float strength = voxelFamily > .5 ? _EnvironmentSceneChroma.z
                   : (role < .5 ? _EnvironmentSceneChroma.x : _EnvironmentSceneChroma.y);
 if (!CharacterLightingFinite(strength) || strength <= 0) return diffuse;
 float3 target = _EnvironmentSceneTint.rgb;
 if (!all(target >= 0) || !all(target < 1e10)) return diffuse;
 float peak = max(target.r, max(target.g, target.b));
 if (peak <= .000001) return diffuse;
 target /= peak;
 const float3 luminanceWeights = float3(.2126,.7152,.0722);
 float targetLuminance = dot(target,luminanceWeights);
 // 极端纯蓝等低亮色先向白退饱和，保证归一化每通道增益<=4且精确保亮；不截断能量。
 if (targetLuminance < .25)
  target = lerp(float3(1,1,1),target,.75 / max(1-targetLuminance,.000001));
 target /= max(dot(target,luminanceWeights),.25);
 float basisLuminance = dot(diffuse,luminanceWeights);
 return lerp(diffuse,basisLuminance*target,saturate(strength));
}

// 湿润属于实际室外表面；观察者位置、揭顶显示和雨粒投影都不能让屋内或雨棚下的表面变湿。
half EnvironmentSurfaceWetness(float3 world, float role)
{
 half wet = saturate(_EnvironmentWetness);
 if (_EnvironmentLightingEnabled < .5 || wet <= 0 || role < -.5 || role > 1.5) return 0;
 if (EnvironmentRoomIndex(world) >= 0) return 0;
 if (_EnvironmentShelterCount < 0 || _EnvironmentShelterCount > 16) return 0;
 [loop] for (int shelter = 0; shelter < _EnvironmentShelterCount; shelter++)
 {
  float3 local = mul(_EnvironmentShelterMatrices[shelter], float4(world, 1)).xyz;
  float3 extent = max(_EnvironmentShelterExtents[shelter].xyz, 0);
  if (all(abs(local.xz) <= extent.xz + .002) && local.y <= extent.y + .002) return 0;
 }
 return wet;
}

float EnvironmentCloudHash(float2 samplePoint)
{
 samplePoint = frac(samplePoint * float2(.1031, .11369));
 samplePoint += dot(samplePoint, samplePoint.yx + 19.19);
 return frac((samplePoint.x + samplePoint.y) * samplePoint.x);
}

float EnvironmentCloudNoise(float2 samplePoint)
{
 float2 cell = floor(samplePoint), blend = frac(samplePoint);
 blend = blend * blend * (3 - 2 * blend);
 return lerp(lerp(EnvironmentCloudHash(cell), EnvironmentCloudHash(cell + float2(1, 0)), blend.x),
             lerp(EnvironmentCloudHash(cell + float2(0, 1)), EnvironmentCloudHash(cell + 1), blend.x), blend.y);
}

// 天空与地面共同采样此函数；世界尺度与连续相位均由模拟器负责，不使用材质私有时钟。
float EnvironmentCloudDensity(float2 worldXZ)
{
 float coverage = saturate(_EnvironmentCloudParameters.x);
 if (_EnvironmentCloudMotion.w < .5 || coverage <= 0) return 0;
 float2 samplePoint = (worldXZ - _EnvironmentCloudMotion.xy * _EnvironmentCloudMotion.z)
                / max(_EnvironmentCloudParameters.w, .1);
 float noise = EnvironmentCloudNoise(samplePoint) * .7 + EnvironmentCloudNoise(samplePoint * 2.03 + 13.1) * .3;
 float edge = 1 - coverage;
 return smoothstep(edge - .12, edge + .12, noise) * saturate(_EnvironmentCloudParameters.y);
}

half EnvironmentDirectCloud(float3 world)
{
 if (_EnvironmentLightingEnabled < .5 || _EnvironmentSurfaceRole < -.5) return 1;
 return 1 - EnvironmentCloudDensity(world.xz) * saturate(_EnvironmentCloudParameters.z);
}

half EnvironmentMainShadow(float3 world)
{
 #if defined(ENVIRONMENT_LEGACY_SPRITE)
  #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
   int cascade = 0;
   #if defined(_MAIN_LIGHT_SHADOWS_CASCADE)
    float3 d0 = world - _CascadeShadowSplitSpheres0.xyz, d1 = world - _CascadeShadowSplitSpheres1.xyz;
    float3 d2 = world - _CascadeShadowSplitSpheres2.xyz, d3 = world - _CascadeShadowSplitSpheres3.xyz;
    float4 weights = step(float4(dot(d0,d0),dot(d1,d1),dot(d2,d2),dot(d3,d3)), _CascadeShadowSplitSphereRadii);
    weights.yzw = saturate(weights.yzw - weights.xyz);
    cascade = (int)(4 - dot(weights, float4(4,3,2,1)));
   #endif
   float3 coordinate = mul(_MainLightWorldToShadow[cascade], float4(world, 1)).xyz;
   if (coordinate.z <= 0 || coordinate.z >= 1) return 1;
   half sampled = _MainLightShadowmapTexture.SampleCmpLevelZero(sampler_LinearClampCompare, coordinate.xy, coordinate.z);
   #if defined(_SHADOWS_SOFT) || defined(_SHADOWS_SOFT_LOW) || defined(_SHADOWS_SOFT_MEDIUM) || defined(_SHADOWS_SOFT_HIGH)
    if (_MainLightShadowParams.y > .5)
    {
     sampled = _MainLightShadowmapTexture.SampleCmpLevelZero(sampler_LinearClampCompare, coordinate.xy + _MainLightShadowOffset0.xy, coordinate.z);
     sampled += _MainLightShadowmapTexture.SampleCmpLevelZero(sampler_LinearClampCompare, coordinate.xy + _MainLightShadowOffset0.zw, coordinate.z);
     sampled += _MainLightShadowmapTexture.SampleCmpLevelZero(sampler_LinearClampCompare, coordinate.xy + _MainLightShadowOffset1.xy, coordinate.z);
     sampled += _MainLightShadowmapTexture.SampleCmpLevelZero(sampler_LinearClampCompare, coordinate.xy + _MainLightShadowOffset1.zw, coordinate.z);
     sampled *= .25;
    }
   #endif
   half shadow = lerp(1, sampled, _MainLightShadowParams.x);
   float3 delta = world - _WorldSpaceCameraPos;
   return lerp(shadow, 1, saturate(dot(delta,delta) * _MainLightShadowParams.z + _MainLightShadowParams.w));
  #else
   return 1;
  #endif
 #else
  return MainLightRealtimeShadow(TransformWorldToShadowCoord(world));
 #endif
}

float3 EnvironmentCharacterNormal(float3 normalHint)
{
 float3 horizontal = float3(normalHint.x, 0, normalHint.z);
 float squareLength = dot(horizontal, horizontal);
 horizontal = squareLength > .0001 ? horizontal * rsqrt(squareLength) : float3(0,0,1);
 return normalize(horizontal * .45 + float3(0, .9, 0));
}

// role：0地面、1场景装饰、2角色及装备、负值为信息标记/自发光业务表现。
float3 EnvironmentLightingGain(float3 world, float3 normal, float role, bool characterEffects, out float3 sceneAmbientGain)
{
 sceneAmbientGain=0;
 if (_EnvironmentLightingEnabled < .5 || role < -.5) return float3(1,1,1);
 normal = normalize(normal + float3(0, .00001, 0));
 half hemisphere = saturate(normal.y * .5 + .5);
 half3 ambient = hemisphere > .5
  ? lerp(_EnvironmentHorizonAmbient.rgb, _EnvironmentSkyAmbient.rgb, hemisphere * 2 - 1)
  : lerp(_EnvironmentGroundAmbient.rgb, _EnvironmentHorizonAmbient.rgb, hemisphere * 2);
 int room = EnvironmentRoomIndex(world);
 if (room >= 0) ambient *= saturate(_EnvironmentIndoorAmbientScale);
 float3 ambientColorDelta=0;
 bool characterOutdoor = characterEffects && role > 1.5 && _CharacterLightControls.x > .5 && room < 0;
 // A精确保留既有手动天空底光色差；增强分支会替换整份结果，不能把两种染色相加。
 if (characterOutdoor)
  ambientColorDelta = CharacterLightingTintAmbient(ambient, _EnvironmentStylizedSkyTint)-ambient;
 float3 lightDirection = _MainLightPosition.xyz;
 half normalLight = saturate(dot(normal, lightDirection));
 half shadow = _EnvironmentReceiveShadow < .5 ? 1 : EnvironmentMainShadow(world);
 half cloud = EnvironmentDirectCloud(world);
 half3 direct = _MainLightColor.rgb * normalLight * shadow * cloud;
 half3 local = 0;
 [loop] for (int lamp = 0; lamp < min(max(_EnvironmentLocalLightCount, 0), 8); lamp++)
 {
  // 所属房间不同的补光不得穿过未开放墙体；此项只补局部底光，不冒充额外灯阴影。
  int ownerRoom = (int)round(_EnvironmentLocalLightColors[lamp].w) - 1;
  if (ownerRoom != room) continue;
  float3 delta = _EnvironmentLocalLightPositions[lamp].xyz - world;
  float squareDistance = dot(delta, delta);
  float range = max(_EnvironmentLocalLightPositions[lamp].w, .01);
  half attenuation = saturate(1 - squareDistance / (range * range));
  attenuation *= attenuation;
  half diffuse = saturate(dot(normal, delta * rsqrt(max(squareDistance, .001))));
  local += _EnvironmentLocalLightColors[lamp].rgb * attenuation * (.25 + .75 * diffuse);
 }
 half3 lighting = max(ambient + direct + local, 0);
 half response = role < .5 ? _EnvironmentLightResponse.x : (role < 1.5 ? _EnvironmentLightResponse.y : _EnvironmentLightResponse.z);
 response *= saturate(_EnvironmentResponseOverride);
 half minimum = saturate(_EnvironmentMinimumOverride);
 if (role > 1.5) minimum = max(minimum, saturate(_EnvironmentLightResponse.w));
 lighting = max(lighting, minimum.xxx);
 // 原minimum数值/response保持；A与新增强度0始终回到此既有结果。
 lighting += ambientColorDelta;
 if (characterOutdoor && CharacterLightingFinite(_CharacterEnvironmentBlend) && _CharacterEnvironmentBlend > 0
     && CharacterLightingHasEnvironmentTint(_CharacterEnvironmentTint))
 {
  // F仅补旧逐分量max实际缺少的光；有强D/L时自动缩减，不多加一整份minimum。
  // 原底光和F合成B后统一分配环境色度，之后不再用中性max冲回灰色。
  float3 supplement = max(minimum.xxx - max(ambient + direct + local, 0), 0);
  float3 basis = max(ambient, 0) + supplement;
  float3 enhanced = CharacterLightingTintBasis(basis, _CharacterEnvironmentTint) + direct + local;
  lighting = lerp(lighting, enhanced, saturate(_CharacterEnvironmentBlend));
 }
 // 与原生受光共用室外/遮雨判定；不把预乘装备变成额外高亮的透明片。
 half wet = EnvironmentSurfaceWetness(world, role);
 sceneAmbientGain=ambient*(1-wet*.08)*saturate(response);
 return lerp(half3(1,1,1), lighting * (1 - wet * .08), saturate(response));
}
float3 EnvironmentLightingGain(float3 world,float3 normal,float role,bool characterEffects)
{
 float3 unusedAmbient;
 return EnvironmentLightingGain(world,normal,role,characterEffects,unusedAmbient);
}

// 保留零alpha Additive的原业务能量；非线性只作用于不含贴图/slot alpha的增益。
half3 EnvironmentShadeLegacy(half3 albedo, float3 world, float3 normal, float role)
{
 float3 ambientGain;
 float3 gain=EnvironmentLightingGain(world,normal,role,false,ambientGain);
 float3 ambient=albedo*ambientGain;
 return albedo*gain+(EnvironmentSceneDiffuseChroma(ambient,world,role,0)-ambient);
}
half3 EnvironmentShadeWithRim(half3 albedo, float3 world, float3 normal, float role, float3 rimGain)
{
 float3 ambientGain;
 float3 gain=EnvironmentLightingGain(world,normal,role,true,ambientGain);
 if (_EnvironmentLightingEnabled>.5 && role>1.5 && _CharacterLightControls.x>.5)
 {
  gain += CharacterLightingLimitRim(rimGain * max(_CharacterLightControls.y,0),gain,_CharacterLightControls.w);
  gain = CharacterLightingProtectGain(gain,_CharacterLightShoulder.x,_CharacterLightShoulder.y,_CharacterLightControls.z);
 }
 float3 ambient=albedo*ambientGain;
 return albedo*gain+(EnvironmentSceneDiffuseChroma(ambient,world,role,0)-ambient);
}
half3 EnvironmentShade(half3 albedo, float3 world, float3 normal, float role)
{
 return EnvironmentShadeWithRim(albedo,world,normal,role,float3(0,0,0));
}

#if defined(ENVIRONMENT_NATIVE_LIT) && !defined(ENVIRONMENT_LEGACY_SPRITE)
// URP14 GlobalIllumination的环境漫射项；只取这一项作delta，反射采样、coat镜面和AO诊断仍归原函数。
half3 EnvironmentNativeIndirectDiffuse(BRDFData brdfData,half clearCoatMask,half3 bakedGI,
 half occlusion,half3 normalWS,half3 viewDirectionWS)
{
 if (IsOnlyAOLightingFeatureEnabled()) return 0;
 half3 diffuse=bakedGI*brdfData.diffuse;
#if defined(_CLEARCOAT) || defined(_CLEARCOATMAP)
 half normalView=saturate(dot(normalWS,viewDirectionWS));
 half coatFresnel=kDielectricSpec.x+kDielectricSpec.a*Pow4(1-normalView);
 diffuse*=1-coatFresnel*clearCoatMask;
#endif
 return diffuse*occlusion;
}

// 只修改运行时SurfaceData副本；干燥、受遮蔽及角色/信息完全保留原数据，不触碰材质资产。
void EnvironmentApplyNativeWetness(inout SurfaceData surfaceData, float3 world)
{
 half wet = EnvironmentSurfaceWetness(world, _EnvironmentSurfaceRole);
 if (wet <= 0) return;
 surfaceData.albedo *= 1 - .08 * wet;
 surfaceData.smoothness = saturate(surfaceData.smoothness + .05 * wet);
}

// 本机URP14原生光照函数的受限适配：BRDF前处理有界湿润，保留原GI混合/调试/额外灯并仅缩主灯云场。
half4 EnvironmentUniversalFragmentPBR(InputData inputData, SurfaceData surfaceData)
{
    EnvironmentApplyNativeWetness(surfaceData, inputData.positionWS);
    #if defined(_SPECULARHIGHLIGHTS_OFF)
    bool specularHighlightsOff = true;
    #else
    bool specularHighlightsOff = false;
    #endif
    BRDFData brdfData;

    // NOTE: can modify "surfaceData"...
    InitializeBRDFData(surfaceData, brdfData);

    #if defined(DEBUG_DISPLAY)
    half4 debugColor;

    if (CanDebugOverrideOutputColor(inputData, surfaceData, brdfData, debugColor))
    {
        return debugColor;
    }
    #endif

    // Clear-coat calculation...
    BRDFData brdfDataClearCoat = CreateClearCoatBRDFData(surfaceData, brdfData);
    half4 shadowMask = CalculateShadowMask(inputData);
    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData, surfaceData);
    uint meshRenderingLayers = GetMeshRenderingLayer();
    Light mainLight = GetMainLight(inputData, shadowMask, aoFactor);

    // NOTE: We don't apply AO to the GI here because it's done in the lighting calculation below...
    MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI);
    // 云不修改GI混合输入、额外灯或最终颜色，只削弱主直射。
    mainLight.color *= EnvironmentDirectCloud(inputData.positionWS);

    LightingData lightingData = CreateLightingData(inputData, surfaceData);

    lightingData.giColor = GlobalIllumination(brdfData, brdfDataClearCoat, surfaceData.clearCoatMask,
                                              inputData.bakedGI, aoFactor.indirectAmbientOcclusion, inputData.positionWS,
                                              inputData.normalWS, inputData.viewDirectionWS, inputData.normalizedScreenSpaceUV
                                              UNITY_GDRP_INSTANCE_ZERO_ARGUMENT);
    half3 sceneDiffuse=EnvironmentNativeIndirectDiffuse(brdfData,surfaceData.clearCoatMask,
        inputData.bakedGI,aoFactor.indirectAmbientOcclusion,inputData.normalWS,inputData.viewDirectionWS);
    lightingData.giColor += EnvironmentSceneDiffuseChroma(sceneDiffuse,inputData.positionWS,_EnvironmentSurfaceRole,1)-sceneDiffuse;
#ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
#endif
    {
        lightingData.mainLightColor = LightingPhysicallyBased(brdfData, brdfDataClearCoat,
                                                              mainLight,
                                                              inputData.normalWS, inputData.viewDirectionWS,
                                                              surfaceData.clearCoatMask, specularHighlightsOff);
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
            lightingData.additionalLightsColor += LightingPhysicallyBased(brdfData, brdfDataClearCoat, light,
                                                                          inputData.normalWS, inputData.viewDirectionWS,
                                                                          surfaceData.clearCoatMask, specularHighlightsOff);
        }
    }
    #endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);

#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
#endif
        {
            lightingData.additionalLightsColor += LightingPhysicallyBased(brdfData, brdfDataClearCoat, light,
                                                                          inputData.normalWS, inputData.viewDirectionWS,
                                                                          surfaceData.clearCoatMask, specularHighlightsOff);
        }
    LIGHT_LOOP_END
    #endif

    #if defined(_ADDITIONAL_LIGHTS_VERTEX)
    lightingData.vertexLightingColor += inputData.vertexLighting * brdfData.diffuse;
    #endif

#if REAL_IS_HALF
    // Clamp any half.inf+ to HALF_MAX
    return min(CalculateFinalColor(lightingData, surfaceData.alpha), HALF_MAX);
#else
    return CalculateFinalColor(lightingData, surfaceData.alpha);
#endif
}

half4 EnvironmentUniversalFragmentBlinnPhong(InputData inputData, SurfaceData surfaceData)
{
    EnvironmentApplyNativeWetness(surfaceData, inputData.positionWS);
    #if defined(DEBUG_DISPLAY)
    half4 debugColor;

    if (CanDebugOverrideOutputColor(inputData, surfaceData, debugColor))
    {
        return debugColor;
    }
    #endif

    uint meshRenderingLayers = GetMeshRenderingLayer();
    half4 shadowMask = CalculateShadowMask(inputData);
    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData, surfaceData);
    Light mainLight = GetMainLight(inputData, shadowMask, aoFactor);

    MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI, aoFactor);
    // 云不修改GI混合输入、额外灯或最终颜色，只削弱主直射。
    mainLight.color *= EnvironmentDirectCloud(inputData.positionWS);

    inputData.bakedGI *= surfaceData.albedo;
    inputData.bakedGI = EnvironmentSceneDiffuseChroma(inputData.bakedGI,inputData.positionWS,_EnvironmentSurfaceRole,1);

    LightingData lightingData = CreateLightingData(inputData, surfaceData);
#ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
#endif
    {
        lightingData.mainLightColor += CalculateBlinnPhong(mainLight, inputData, surfaceData);
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
            lightingData.additionalLightsColor += CalculateBlinnPhong(light, inputData, surfaceData);
        }
    }
    #endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
#endif
        {
            lightingData.additionalLightsColor += CalculateBlinnPhong(light, inputData, surfaceData);
        }
    LIGHT_LOOP_END
    #endif

    #if defined(_ADDITIONAL_LIGHTS_VERTEX)
    lightingData.vertexLightingColor += inputData.vertexLighting * surfaceData.albedo;
    #endif

    return CalculateFinalColor(lightingData, surfaceData.alpha);
}

#endif
#endif
