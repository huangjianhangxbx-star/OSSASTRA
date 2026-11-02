// 功能：复用本机Spine URP Sprite原顶点/片元alpha合同，输出正向绘制真值。
// 边界：仅ForwardLit源家族；不修改包源码，不替代最终光照或任意额外Outline Pass认证。
Shader "Hidden/Ossastra/Rooms/PositiveSpriteLitTruth"
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
	 _RoomPositiveDepthWrite("真值写深度/标签只比较",Float)=1
 _RoomPositivePartial("有限视域片元",Float)=0
 _RoomPositiveCutRoof("屋顶精确候选",Float)=0
 _RoomPositiveColor("真值颜色",Color)=(0,1,1,1)
}

 SubShader {
 Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Sprite"}
 Stencil {Ref[_StencilRef] Comp[_StencilComp] Pass Keep}
		Pass
		{
			// Lightmode matches the ShaderPassName set in UniversalRenderPipeline.cs. SRPDefaultUnlit and passes with
			// no LightMode tag are also rendered by Universal Render Pipeline
			Name "ForwardLit"
			Tags{"LightMode" = "UniversalForward"}
			Blend Off
			ZWrite[_RoomPositiveDepthWrite]
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
			// Truth target has only one color attachment.

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
			#pragma fragment RoomSpineTruthFrag
            #pragma target 4.5

			#define NEEDS_POSITION_WS
			#define USE_URP
			#define fixed4 half4
			#define fixed3 half3
			#define fixed half
			#include "Packages/com.esotericsoftware.spine.urp-shaders/Shaders/Include/Spine-Input-Sprite-URP.hlsl"
			#include "Packages/com.esotericsoftware.spine.urp-shaders/Shaders/Include/Spine-Sprite-ForwardPass-URP.hlsl"
			#include "RoomSightClip.hlsl"
            float _RoomPositivePartial,_RoomPositiveCutRoof;float4 _RoomPositiveColor;
            half4 RoomSpineTruthFrag(VertexOutputLWRP input):SV_Target0
            {
                float3 logicalWorld=input.positionWS.xyz;
                if(_RoomPositivePartial>.5)clip(RoomSightPartialContains(logicalWorld)?1:-1);
                RoomSightPreviewRoofClip(input.pos.xy,_RoomPositiveCutRoof);
                half4 original=ForwardPassFragmentSprite(input);
                clip(original.a-.000001);
                return RoomSightTruthColor(logicalWorld,_RoomPositiveColor,_RoomPositivePartial);
            }
            ENDHLSL
		}
 }
}
