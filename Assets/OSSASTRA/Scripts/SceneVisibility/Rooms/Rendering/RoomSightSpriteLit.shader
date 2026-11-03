/* 功能：复用本机Spine URP Sprite的ForwardLit、alpha/混合/颜色/法线/阴影变体，追加正向视域裁剪。
 * 边界：只适配该源Forward家族，不新增Outline/Caster或独立照明；真实光照由DrawRenderers提供。
 * 生命周期：原材质与包源码只读，当前RendererFeature显式持有Shader引用。
 * ENV-01：原无光照表面接环境主灯/阴影；原生Lit保留原光照，仅追加房间GI系数与主灯云场，alpha/深度/许可保持。
 */
Shader "Hidden/Ossastra/Rooms/SightSpriteLit"
{
    Properties
{

		_MainTex("Main Texture", 2D) = "white" {}
		_Color("Color", Color) = (1,1,1,1)

		_BumpScale("Scale", Float) = 1.0
		_BumpMap("Normal Map", 2D) = "bump" {}

		[MaterialToggle] PixelSnap("Pixel snap", Float) = 0

		_EmissionColor("Color", Color) = (0,0,0,0)
		_EmissionMap("Emission", 2D) = "white" {}
		_EmissionPower("Emission Power", Float) = 2.0

		_Glossiness("Smoothness", Range(0.0, 1.0)) = 0.5
		_GlossMapScale("Smoothness Scale", Range(0.0, 1.0)) = 1.0
		[Gamma] _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
		_MetallicGlossMap("Metallic", 2D) = "white" {}

		_DiffuseRamp("Diffuse Ramp Texture", 2D) = "gray" {}

		_FixedNormal("Fixed Normal", Vector) = (0,0,1,1)
		_ZWrite("Depth Write", Float) = 0.0
		_Cutoff("Depth alpha cutoff", Range(0,1)) = 0.0
		_ShadowAlphaCutoff("Shadow alpha cutoff", Range(0,1)) = 0.1
		_CustomRenderQueue("Custom Render Queue", Float) = 0.0

		_OverlayColor("Overlay Color", Color) = (0,0,0,0)
		_Hue("Hue", Range(-0.5,0.5)) = 0.0
		_Saturation("Saturation", Range(0,2)) = 1.0
		_Brightness("Brightness", Range(0,2)) = 1.0

		_RimPower("Rim Power", Float) = 2.0
		_RimColor("Rim Color", Color) = (1,1,1,1)

		_BlendTex("Blend Texture", 2D) = "white" {}
		_BlendAmount("Blend", Range(0,1)) = 0.0

		[MaterialToggle(_LIGHT_AFFECTS_ADDITIVE)] _LightAffectsAdditive("Light Affects Additive", Float) = 0
		[MaterialToggle(_TINT_BLACK_ON)]  _TintBlack("Tint Black", Float) = 0
		_Black("Dark Color", Color) = (0,0,0,0)

		[HideInInspector] _SrcBlend("__src", Float) = 1.0
		[HideInInspector] _DstBlend("__dst", Float) = 0.0
		[HideInInspector] _RenderQueue("__queue", Float) = 0.0
		[HideInInspector] _Cull("__cull", Float) = 0.0
		[HideInInspector] _StencilRef("Stencil Reference", Float) = 1.0
		[Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp("Stencil Compare", Float) = 8 // Set to Always as default

		// Outline properties are drawn via custom editor.
		[HideInInspector] _OutlineWidth("Outline Width", Range(0,8)) = 3.0
		[HideInInspector][MaterialToggle(_USE_SCREENSPACE_OUTLINE_WIDTH)] _UseScreenSpaceOutlineWidth("Width in Screen Space", Float) = 0
		[HideInInspector] _OutlineColor("Outline Color", Color) = (1,1,0,1)
		[HideInInspector][MaterialToggle(_OUTLINE_FILL_INSIDE)]_Fill("Fill", Float) = 0
		[HideInInspector] _OutlineReferenceTexWidth("Reference Texture Width", Int) = 1024
		[HideInInspector] _ThresholdEnd("Outline Threshold", Range(0,1)) = 0.25
		[HideInInspector] _OutlineSmoothness("Outline Smoothness", Range(0,1)) = 1.0
		[HideInInspector][MaterialToggle(_USE8NEIGHBOURHOOD_ON)] _Use8Neighbourhood("Sample 8 Neighbours", Float) = 1
		[HideInInspector] _OutlineOpaqueAlpha("Opaque Alpha", Range(0,1)) = 1.0
		[HideInInspector] _OutlineMipLevel("Outline Mip Level", Range(0,3)) = 0
	}
    SubShader
    {

		// Universal Pipeline tag is required. If Universal render pipeline is not set in the graphics settings
		// this Subshader will fail.
		Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Sprite" "AlphaDepth"="False" "CanUseSpriteAtlas"="True" "IgnoreProjector"="True" }
		LOD 150

		Stencil {
			Ref[_StencilRef]
			Comp[_StencilComp]
			Pass Keep
		}

		// ------------------------------------------------------------------
		//  Forward pass.
Pass
{
			// Lightmode matches the ShaderPassName set in UniversalRenderPipeline.cs. SRPDefaultUnlit and passes with
			// no LightMode tag are also rendered by Universal Render Pipeline
			Name "ForwardLit"
			Tags{"LightMode" = "UniversalForward"}
			Blend[_SrcBlend][_DstBlend]
			ZWrite[_ZWrite]
			Cull[_Cull]

			HLSLPROGRAM
			// Required to compile gles 2.0 with standard SRP library
			// All shaders must be compiled with HLSLcc and currently only gles is not using HLSLcc by default
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x

			// -------------------------------------
			// Material Keywords
			#pragma shader_feature _ _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON _ALPHAPREMULTIPLY_VERTEX_ONLY _ADDITIVEBLEND _ADDITIVEBLEND_SOFT _MULTIPLYBLEND _MULTIPLYBLEND_X2
			#pragma shader_feature _ _FIXED_NORMALS_VIEWSPACE _FIXED_NORMALS_VIEWSPACE_BACKFACE _FIXED_NORMALS_MODELSPACE _FIXED_NORMALS_MODELSPACE_BACKFACE _FIXED_NORMALS_WORLDSPACE
			#pragma shader_feature _ _SPECULAR _SPECULAR_GLOSSMAP
			#pragma shader_feature _NORMALMAP
			#pragma shader_feature _ALPHA_CLIP
			#pragma shader_feature _EMISSION
			#pragma shader_feature _DIFFUSE_RAMP
			#pragma shader_feature _ _FULLRANGE_HARD_RAMP _FULLRANGE_SOFT_RAMP _OLD_HARD_RAMP _OLD_SOFT_RAMP
			#pragma shader_feature _COLOR_ADJUST
			#pragma shader_feature _RIM_LIGHTING
			#pragma shader_feature _TEXTURE_BLEND
			#pragma shader_feature _FOG
			#pragma shader_feature _RECEIVE_SHADOWS_OFF
			#pragma shader_feature _LIGHT_AFFECTS_ADDITIVE
			#pragma shader_feature _TINT_BLACK_ON

			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma multi_compile_fog
			#pragma multi_compile _ PIXELSNAP_ON

			// -------------------------------------
			// Universal Pipeline keywords
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
			#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
			#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
			#pragma multi_compile _ _SHADOWS_SOFT
			#pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
			#pragma multi_compile_fragment _ _LIGHT_COOKIES

			// Farward+ renderer keywords
			#pragma multi_compile_fragment _ _LIGHT_LAYERS
			#pragma multi_compile _ _FORWARD_PLUS
			#pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS

			// -------------------------------------
			// Unity defined keywords
			#pragma multi_compile _ DIRLIGHTMAP_COMBINED
			#pragma multi_compile _ LIGHTMAP_ON
			#pragma multi_compile _ PROBE_VOLUMES_L1 PROBE_VOLUMES_L2

			//--------------------------------------
			// GPU Instancing
			#pragma multi_compile_instancing
			#pragma instancing_options renderinglayer

			//--------------------------------------
			// Spine related keywords
			#pragma vertex ForwardPassVertexSprite
			#pragma fragment RoomSightSpriteLitFragment
            #pragma target 4.5

			#define NEEDS_POSITION_WS
            #define USE_URP
			#define fixed4 half4
			#define fixed3 half3
			#define fixed half
			#include "Packages/com.esotericsoftware.spine.urp-shaders/Shaders/Include/Spine-Input-Sprite-URP.hlsl"
			#include "Packages/com.esotericsoftware.spine.urp-shaders/Shaders/Include/Spine-Sprite-ForwardPass-URP.hlsl"

            #include "RoomSightClip.hlsl"
   #include "Assets/OSSASTRA/Scripts/EnvironmentSimulation/Rendering/EnvironmentLighting.hlsl"

// Spine原生函数本地钩子：保留原授权依赖和所有原生着色项；第三方包源码只读。
#if defined(SPECULAR)
half4 EnvironmentLightweightFragmentPBRSimplified(InputData inputData, half4 texAlbedoAlpha, half metallic, half3 specular,
	half smoothness, half3 emission, half4 vertexColor)
{
#if !defined(_TINT_BLACK_ON)
	half4 albedo = texAlbedoAlpha * vertexColor;
#else
	half4 albedo = texAlbedoAlpha;
#endif

	BRDFData brdfData;
	half ignoredAlpha = 1; // ignore alpha, otherwise
	InitializeBRDFData(albedo.rgb, metallic, specular, smoothness, ignoredAlpha, brdfData);
	brdfData.specular *= albedo.a;

#ifndef _MAIN_LIGHT_VERTEX
#if (defined(_MAIN_LIGHT_SHADOWS) || defined(MAIN_LIGHT_CALCULATE_SHADOWS)) && !defined(_RECEIVE_SHADOWS_OFF)
	Light mainLight = GetMainLight(inputData.shadowCoord);
#else
	Light mainLight = GetMainLight();
#endif
#if defined(USE_LIGHT_COOKIES)
	half3 cookieColor = SampleMainLightCookie(inputData.positionWS);
	mainLight.color *= cookieColor;
#endif
	mainLight.color *= EnvironmentDirectCloud(inputData.positionWS);

	half3 finalColor = inputData.bakedGI * albedo.rgb;
	finalColor += LightingPhysicallyBased(brdfData, mainLight, inputData.normalWS, inputData.viewDirectionWS);
#else // _MAIN_LIGHT_VERTEX
	half3 finalColor = inputData.bakedGI * albedo.rgb;
#endif // _MAIN_LIGHT_VERTEX

#ifdef _ADDITIONAL_LIGHTS
	uint meshRenderingLayers = GetMeshRenderingLayerBackwardsCompatible();

#if defined(_ADDITIONAL_LIGHT_SHADOWS) && !defined(_RECEIVE_SHADOWS_OFF)
	half4 shadowMask = CalculateShadowMaskBackwardsCompatible(inputData);
#else
	half4 shadowMask = half4(1, 1, 1, 1);
#endif

#if USE_FORWARD_PLUS
	for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
	{
		FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
		finalColor += ProcessLightPBRSimplified(inputData, brdfData, shadowMask, meshRenderingLayers, lightIndex);
	}
#endif
	uint pixelLightCount = GetAdditionalLightsCount();
	LIGHT_LOOP_BEGIN_SPINE(pixelLightCount)
		finalColor += ProcessLightPBRSimplified(inputData, brdfData, shadowMask, meshRenderingLayers, lightIndex);
	LIGHT_LOOP_END_SPINE

#endif // _ADDITIONAL_LIGHTS

#ifdef _ADDITIONAL_LIGHTS_VERTEX
	finalColor += inputData.vertexLighting * brdfData.diffuse;
#endif
	finalColor += emission;
	return prepareLitPixelForOutput(half4(finalColor, albedo.a), texAlbedoAlpha.a, vertexColor.a);
}
#else
half4 EnvironmentLightweightFragmentBlinnPhongSimplified(InputData inputData, half4 texDiffuseAlpha, half3 emission, half4 vertexColor)
{
#if !defined(_TINT_BLACK_ON)
	half4 diffuse = texDiffuseAlpha * vertexColor;
#else
	half4 diffuse = texDiffuseAlpha;
#endif

#ifndef _MAIN_LIGHT_VERTEX
#if (defined(_MAIN_LIGHT_SHADOWS) || defined(MAIN_LIGHT_CALCULATE_SHADOWS)) && !defined(_RECEIVE_SHADOWS_OFF)
	Light mainLight = GetMainLight(inputData.shadowCoord);
#else
	Light mainLight = GetMainLight();
#endif
#if defined(USE_LIGHT_COOKIES)
	half3 cookieColor = SampleMainLightCookie(inputData.positionWS);
	mainLight.color *= cookieColor;
#endif
	mainLight.color *= EnvironmentDirectCloud(inputData.positionWS);

	half3 diffuseLighting = inputData.bakedGI;

	half3 attenuation = mainLight.distanceAttenuation* mainLight.shadowAttenuation;
	half3 attenuatedLightColor = mainLight.color * attenuation;
#ifndef _DIFFUSE_RAMP
	diffuseLighting += LightingLambert(attenuatedLightColor, mainLight.direction, inputData.normalWS);
#else
	diffuseLighting += LightingLambertRamped(mainLight.color, attenuation, mainLight.direction, inputData.normalWS);
#endif
#else // _MAIN_LIGHT_VERTEX
	half3 diffuseLighting = inputData.bakedGI;
#endif // _MAIN_LIGHT_VERTEX

#ifdef _ADDITIONAL_LIGHTS
	uint meshRenderingLayers = GetMeshRenderingLayerBackwardsCompatible();

#if defined(_ADDITIONAL_LIGHT_SHADOWS) && !defined(_RECEIVE_SHADOWS_OFF)
	half4 shadowMask = CalculateShadowMaskBackwardsCompatible(inputData);
#else
	half4 shadowMask = half4(1, 1, 1, 1);
#endif
#if USE_FORWARD_PLUS
	for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
	{
		FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
		diffuseLighting += ProcessLightLambert(inputData, shadowMask, meshRenderingLayers, lightIndex);
	}
#endif
	uint pixelLightCount = GetAdditionalLightsCount();
	LIGHT_LOOP_BEGIN_SPINE(pixelLightCount)
		diffuseLighting += ProcessLightLambert(inputData, shadowMask, meshRenderingLayers, lightIndex);
	LIGHT_LOOP_END_SPINE

#endif
#ifdef _ADDITIONAL_LIGHTS_VERTEX
	diffuseLighting += inputData.vertexLighting;
#endif
	diffuseLighting += emission;
	//half3 finalColor = diffuseLighting * diffuse + emission;
	half3 finalColor = diffuseLighting * diffuse.rgb;
	return prepareLitPixelForOutput(half4(finalColor, diffuse.a), texDiffuseAlpha.a, vertexColor.a);
}
#endif

            // Spine原生片元的局部适配；第三方源码保持只读。
            half4 ForwardPassFragmentSpriteEnvironment(VertexOutputLWRP input
#ifdef USE_WRITE_RENDERING_LAYERS
	, out float4 outRenderingLayers : SV_Target1
#endif
) : SV_Target0
{
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

	fixed4 texureColor = calculateTexturePixel(input.texcoord.xy);

	RETURN_UNLIT_IF_ADDITIVE_SLOT_TINT(texureColor, input.vertexColor, input.darkColor, _Color.a, _Black.a) // shall be called before ALPHA_CLIP

	ALPHA_CLIP(texureColor, input.vertexColor)

#if defined(_TINT_BLACK_ON)
	texureColor = fragTintedColor(texureColor, input.darkColor, input.vertexColor, _Color.a, _Black.a);
#endif

	// fill out InputData struct
	InputData inputData;
#if !defined(_RECEIVE_SHADOWS_OFF)
	#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
		inputData.shadowCoord = input.shadowCoord;
	#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
		inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS.xyz);
	#elif defined(_MAIN_LIGHT_SHADOWS)
		inputData.shadowCoord = input.shadowCoord;
	#else
		inputData.shadowCoord = float4(0, 0, 0, 0);
	#endif
#endif

	inputData.viewDirectionWS = input.viewDirectionWS;
	inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;

#if defined(PER_PIXEL_LIGHTING) && defined(_NORMALMAP)
	half3 normalWS = calculateNormalFromBumpMap(input.texcoord.xy, input.tangentWorld.xyz, input.binormalWorld.xyz, input.normalWorld.xyz);
#else
	half3 normalWS = input.normalWorld.xyz;
#endif

	inputData.normalWS = normalWS;
#if defined(USE_ADAPTIVE_PROBE_VOLUMES)
	half4 shadowMask = 1.0;
	float4 ignoredProbeOcclusion;
	inputData.bakedGI = SAMPLE_GI(input.vertexSH,
		GetAbsolutePositionWS(input.positionWS),
		inputData.normalWS.xyz,
		GetWorldSpaceNormalizeViewDir(input.positionWS),
		input.positionCS.xy,
		ignoredProbeOcclusion,
		shadowMask);
#else // USE_ADAPTIVE_PROBE_VOLUMES
	inputData.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, inputData.normalWS);
#endif

// 保留Spine原生法线、PMA、着色与主灯；只有环境GI按片元所属房间衰减。
	inputData.bakedGI *= EnvironmentIndoorFactor(input.positionWS.xyz);

// 云主灯也需要实际世界点，不能依赖是否启用额外灯或Rim。
	inputData.positionWS = input.positionWS.rgb;
#if defined(_ADDITIONAL_LIGHTS) && USE_FORWARD_PLUS
	inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.pos);
	// note: don't assign normalizedScreenSpaceUV otherwise since old URP versions are missing this member variable
#endif

#if defined(SPECULAR)
	half2 metallicGloss = getMetallicGloss(input.texcoord.xy);
	half metallic = metallicGloss.x;
	half smoothness = metallicGloss.y; // this is 1 minus the square root of real roughness m.

	half3 specular = half3(0, 0, 0);
	half4 emission = half4(0, 0, 0, 1);
	APPLY_EMISSION_SPECULAR(emission, input.texcoord.xy)
	half4 pixel = EnvironmentLightweightFragmentPBRSimplified(inputData, texureColor, metallic, specular, smoothness, emission.rgb, input.vertexColor);
#else
	half3 emission = half3(0, 0, 0);
	APPLY_EMISSION(emission, input.texcoord.xy)
	half4 pixel = EnvironmentLightweightFragmentBlinnPhongSimplified(inputData, texureColor, emission, input.vertexColor);
#endif

#if defined(_RIM_LIGHTING)
	pixel.rgb = applyRimLighting(input.positionWS.xyz, normalWS, pixel);
#endif

	COLORISE(pixel)
	APPLY_FOG_LWRP(pixel, input.fogFactorAndVertexLight.x)

#ifdef USE_WRITE_RENDERING_LAYERS
	uint renderingLayers = GetMeshRenderingLayerBackwardsCompatible();
	outRenderingLayers = float4(EncodeMeshRenderingLayer(renderingLayers), 0, 0, 0);
#endif

	return pixel;
}
            half4 RoomSightSpriteLitFragment(VertexOutputLWRP input
            #ifdef USE_WRITE_RENDERING_LAYERS
                , out float4 outRenderingLayers : SV_Target1
            #endif
            ) : SV_Target0
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                if(_EnvironmentFullSurface<.5)clip(RoomSightPartialContains(input.positionWS.xyz) ? 1 : -1);
                half4 color=ForwardPassFragmentSpriteEnvironment(input
                #ifdef USE_WRITE_RENDERING_LAYERS
                    , outRenderingLayers
                #endif
                );
                return EnvironmentFogColor(color,input.positionWS.xyz,1);
            }
            ENDHLSL
		}
    }
}
