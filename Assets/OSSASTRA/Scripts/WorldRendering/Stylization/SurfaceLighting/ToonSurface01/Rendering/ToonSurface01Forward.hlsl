// Adapted from Unity URP14.2.0-t1, Unity Companion License; see docs/ossastra/ENV-01.
#ifndef OSSASTRA_VOXEL_FORWARD_INCLUDED
#define OSSASTRA_VOXEL_FORWARD_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

// GLES2 has limited amount of interpolators
#if defined(_PARALLAXMAP) && !defined(SHADER_API_GLES)
#define REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR
#endif

#if (defined(_NORMALMAP) || (defined(_PARALLAXMAP) && !defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR))) || defined(_DETAIL)
#define REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR
#endif

// keep this file in sync with LitGBufferPass.hlsl

struct Attributes
{
    float4 positionOS   : POSITION;
    half4 color : COLOR;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 texcoord     : TEXCOORD0;
    float2 staticLightmapUV   : TEXCOORD1;
    float2 dynamicLightmapUV  : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv                       : TEXCOORD0;

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    float3 positionWS               : TEXCOORD1;
#endif

    float3 normalWS                 : TEXCOORD2;
#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
    half4 tangentWS                : TEXCOORD3;    // xyz: tangent, w: sign
#endif

#ifdef _ADDITIONAL_LIGHTS_VERTEX
    half4 fogFactorAndVertexLight   : TEXCOORD5; // x: fogFactor, yzw: vertex light
#else
    half  fogFactor                 : TEXCOORD5;
#endif

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord              : TEXCOORD6;
#endif

#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirTS                : TEXCOORD7;
#endif

    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 8);
#ifdef DYNAMICLIGHTMAP_ON
    float2  dynamicLightmapUV : TEXCOORD9; // Dynamic lightmap UVs
#endif

#if defined(REQUIRES_VERTEX_PROBE_SHADOW_MASK)
    half4 probeShadowMask        : TEXCOORD10;
#endif

    half4 color : TEXCOORD11;
    float4 positionCS               : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

void InitializeInputData(Varyings input, half3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    inputData.positionWS = input.positionWS;
#endif

    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
#if defined(_NORMALMAP) || defined(_DETAIL)
    float sgn = input.tangentWS.w;      // should be either +1 or -1
    float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
    half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);

    #if defined(_NORMALMAP)
    inputData.tangentToWorld = tangentToWorld;
    #endif
    inputData.normalWS = TransformTangentToWorld(normalTS, tangentToWorld);
#else
    inputData.normalWS = input.normalWS;
#endif

    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
    inputData.viewDirectionWS = viewDirWS;

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    inputData.shadowCoord = input.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif
#ifdef _ADDITIONAL_LIGHTS_VERTEX
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactorAndVertexLight.x);
    inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
#else
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
#endif
    
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);

#if defined(DYNAMICLIGHTMAP_ON)
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
#elif defined(USE_PROBE_SYSTEM) && !defined(LIGHTMAP_ON)
    // APS path: only for non-lightmapped objects
    #if defined(EVALUATE_SH_VERTEX)
        // Use probe data sampled in vertex shader
        inputData.bakedGI = input.vertexSH;
        #if defined(REQUIRES_VERTEX_PROBE_SHADOW_MASK)
            inputData.shadowMask = input.probeShadowMask;
        #else
            inputData.shadowMask = 1.0;
        #endif
    #else
        // APS pixel mode
        inputData.bakedGI = SAMPLE_GI(inputData.positionWS, inputData.normalWS, inputData.normalizedScreenSpaceUV, inputData.shadowMask);
    #endif
#else
    // Standard GI: SAMPLE_GI macro dispatches to SampleLightmap (LIGHTMAP_ON) or SampleSHPixel
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
#endif


    #if defined(DEBUG_DISPLAY)
    #if defined(DYNAMICLIGHTMAP_ON)
    inputData.dynamicLightmapUV = input.dynamicLightmapUV;
    #endif
    #if defined(LIGHTMAP_ON)
    inputData.staticLightmapUV = input.staticLightmapUV;
    #else
    inputData.vertexSH = input.vertexSH;
    #endif
    #endif
}

///////////////////////////////////////////////////////////////////////////////
//                  Vertex and Fragment functions                            //
///////////////////////////////////////////////////////////////////////////////

// Used in Standard (Physically Based) shader
Varyings LitPassVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);

    // normalWS and tangentWS already normalize.
    // this is required to avoid skewing the direction during interpolation
    // also required for per-vertex lighting and SH evaluation
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);

    half fogFactor = 0;
    #if !defined(_FOG_FRAGMENT)
        fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
    #endif

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.color = input.color;

    // already normalized from normal transform to WS.
    output.normalWS = normalInput.normalWS;
#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR) || defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    real sign = input.tangentOS.w * GetOddNegativeScale();
    half4 tangentWS = half4(normalInput.tangentWS.xyz, sign);
#endif
#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
    output.tangentWS = tangentWS;
#endif

#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
    half3 viewDirTS = GetViewDirectionTangentSpace(tangentWS, output.normalWS, viewDirWS);
    output.viewDirTS = viewDirTS;
#endif

    OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
#ifdef DYNAMICLIGHTMAP_ON
    output.dynamicLightmapUV = input.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
#endif
#if defined(USE_PROBE_SYSTEM) && defined(EVALUATE_SH_VERTEX) && !defined(LIGHTMAP_ON)
    // Deferred to the end of LitPassVertex when positionCS is available
#else
    OUTPUT_SH(output.normalWS.xyz, output.vertexSH);
#endif
#ifdef _ADDITIONAL_LIGHTS_VERTEX
    output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);
#else
    output.fogFactor = fogFactor;
#endif

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    output.positionWS = vertexInput.positionWS;
#endif

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = GetShadowCoord(vertexInput);
#endif

    output.positionCS = vertexInput.positionCS;

#if defined(USE_PROBE_SYSTEM) && defined(EVALUATE_SH_VERTEX) && !defined(LIGHTMAP_ON)
    // Sample probe in vertex shader for better performance
    float2 positionSS = GetNormalizedScreenSpaceUV(output.positionCS);
    #if defined(REQUIRES_VERTEX_PROBE_SHADOW_MASK)
        output.vertexSH = SampleAdaptiveProbeSystem(vertexInput.positionWS, normalInput.normalWS, positionSS, output.probeShadowMask);
    #else
        float4 unusedShadowMask;
        output.vertexSH = SampleAdaptiveProbeSystem(vertexInput.positionWS, normalInput.normalWS, positionSS, unusedShadowMask);
    #endif
#endif

    return output;
}

// Used in Standard (Physically Based) shader
#if defined(VOXEL_ROOM_SIGHT)
#include "Assets/OSSASTRA/Scripts/SceneVisibility/Rooms/Rendering/RoomSightClip.hlsl"
#endif
#define ENVIRONMENT_NATIVE_LIT
#include "ToonSurface01Lighting.hlsl"

// Vertex colours affect RGB only: alpha coverage, shadow and depth keep source texture alpha.
void VoxelPassFragment(Varyings input, out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
, out float4 outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
#if defined(VOXEL_ROOM_SIGHT)
    if (_RoomSightRoofMode > .5) RoomSightPreviewRoofClip(input.positionCS.xy, _RoomSightCutRoof);
    else if (_EnvironmentFullSurface < .5) clip(RoomSightPartialContains(input.positionWS) ? 1 : -1);
#endif
#if defined(_PARALLAXMAP)
#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirTS = input.viewDirTS;
#else
    half3 viewDirTS = GetViewDirectionTangentSpace(input.tangentWS, input.normalWS, GetWorldSpaceNormalizeViewDir(input.positionWS));
#endif
    ApplyPerPixelDisplacement(viewDirTS, input.uv UNITY_GDRP_MATERIAL_PAGE_OFFSET_ARGUMENT);
#endif
    SurfaceData surface;
    InitializeStandardLitSurfaceData(input.uv, surface);
    surface.albedo *= lerp(half3(1,1,1), input.color.rgb, saturate(_VoxelVertexColors));
#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif
    InputData data;
    InitializeInputData(input, surface.normalTS, data);
    data.bakedGI *= EnvironmentIndoorFactor(input.positionWS);
    SETUP_DEBUG_TEXTURE_DATA(data, input.uv, _BaseMap);
#ifdef _DBUFFER
    ApplyDecalToSurfaceData(input.positionCS, surface, data);
#endif
    half4 color = VoxelUniversalFragmentPBR(data, surface);
    if (_VoxelDiagnostic < .5) color.rgb = MixFog(color.rgb, data.fogCoord);
    color.a = OutputAlpha(color.a, IsSurfaceTypeTransparent(_Surface));
#if defined(VOXEL_ROOM_SIGHT)
    if (_RoomSightRoofMode < .5)
    {
        // History never carries new dynamic lighting; legal coverage still applies to diagnostics.
        if (_EnvironmentFullSurface < .5 && RoomSightIsHistoryOnly(input.positionWS))
            color.rgb = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).rgb*_BaseColor.rgb
                *lerp(half3(1,1,1),input.color.rgb,saturate(_VoxelVertexColors));
        color = EnvironmentFogColor(color,input.positionWS,0);
    }
#endif
    outColor = color;
#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = float4(EncodeMeshRenderingLayer(GetMeshRenderingLayer()),0,0,0);
#endif
}
#endif
