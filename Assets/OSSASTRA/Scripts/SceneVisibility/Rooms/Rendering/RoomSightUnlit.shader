/* 功能：复用本机URP14 Unlit的原Forward着色与材质属性，在颜色/深度写入前裁剪合法视域。
 * 边界：只作为按原材质分桶的正向overrideMaterial；不替换原材质、Caster或光照权威；房顶像素由独立许可决定。
 * 生命周期：无自有纹理/材质，显式Shader引用由当前相机RendererFeature持有。
 * ENV-01：原无光照表面接环境主灯/阴影；原生Lit保留原光照，仅追加房间GI系数与主灯云场，alpha/深度/许可保持。
 */
Shader "Hidden/Ossastra/Rooms/SightUnlit"
{
    Properties
{

        [MainTexture] _BaseMap("Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
        _Cutoff("AlphaCutout", Range(0.0, 1.0)) = 0.5

        // BlendMode
        _Surface("__surface", Float) = 0.0
        _Blend("__mode", Float) = 0.0
        _Cull("__cull", Float) = 2.0
        [ToggleUI] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _BlendOp("__blendop", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 0.0

        // Editmode props
        _QueueOffset("Queue offset", Float) = 0.0

        // ObsoleteProperties
        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color("Base Color", Color) = (0.5, 0.5, 0.5, 1)
        [HideInInspector] _SampleGI("SampleGI", float) = 0.0 // needed from bakedlit
    }
    SubShader
    {

        Tags
        {
            "RenderType" = "Opaque"
            "IgnoreProjector" = "True"
            "UniversalMaterialType" = "Unlit"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100

        // -------------------------------------
        // Render State Commands
        Blend [_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]
        ZWrite [_ZWrite]
        Cull [_Cull]
Pass
{
            Name "Unlit"

            // -------------------------------------
            // Render State Commands
            AlphaToMask[_AlphaToMask]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            // -------------------------------------
            // Shader Stages
            #pragma vertex RoomSightUnlitVertex
            #pragma fragment RoomSightUnlitFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ALPHAMODULATE_ON

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile _ DEBUG_DISPLAY
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            //#pragma multi_compile_vertex _ ENABLE_VS_SKINNING

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/UnlitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/UnlitForwardPass.hlsl"

            #include "RoomSightClip.hlsl"
            #include "Assets/OSSASTRA/Scripts/EnvironmentSimulation/Rendering/EnvironmentLighting.hlsl"
            // 保留包内完整Varyings及其条件语义，只附加本视域所需的世界点。
            struct RoomSightUnlitVaryings
            {
                Varyings original;
                float3 sightWorld : TEXCOORD7;
                float3 sightNormal : TEXCOORD6;
            };
            // URP Unlit 的 normalOS 只在 DEBUG_DISPLAY 存在；环境入口始终显式读取真实网格法线。
            struct RoomSightUnlitAttributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            RoomSightUnlitVaryings RoomSightUnlitVertex(RoomSightUnlitAttributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Attributes nativeInput = (Attributes)0;
                nativeInput.positionOS = input.positionOS;
                nativeInput.uv = input.uv;
                UNITY_TRANSFER_INSTANCE_ID(input, nativeInput);
                #if defined(DEBUG_DISPLAY)
                nativeInput.normalOS = input.normalOS;
                nativeInput.tangentOS = input.tangentOS;
                #endif
                RoomSightUnlitVaryings output;
                output.original = UnlitPassVertex(nativeInput);
                output.sightWorld = TransformObjectToWorld(input.positionOS.xyz);
                output.sightNormal = TransformObjectToWorldNormal(input.normalOS, true);
                return output;
            }
            // URP14 Unlit本地适配：受光发生在原生雾之前，源包与材质只读。
            void UnlitPassFragmentEnvironment(
    Varyings input, float3 world, float3 normal
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out float4 outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half2 uv = input.uv;
    half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
    half3 color = texColor.rgb * _BaseColor.rgb;
    half alpha = texColor.a * _BaseColor.a;

    alpha = AlphaDiscard(alpha, _Cutoff);
    color = AlphaModulate(color, alpha);

#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif

    InputData inputData;
    InitializeInputData(input, inputData);
    SETUP_DEBUG_TEXTURE_DATA(inputData, input.uv, _BaseMap);

#ifdef _DBUFFER
    ApplyDecalToBaseColor(input.positionCS, color);
#endif

    half4 finalColor = UniversalFragmentUnlit(inputData, color, alpha);

#if defined(_SCREEN_SPACE_OCCLUSION) && !defined(_SURFACE_TYPE_TRANSPARENT)
    float2 normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(normalizedScreenSpaceUV);
    finalColor.rgb *= aoFactor.directAmbientOcclusion;
#endif

// 只给业务表面受光，原生AO、alpha和雾的顺序保持不变。
    if(EnvironmentCanShadeWorld(world)) finalColor.rgb=EnvironmentShade(finalColor.rgb,world,normal,_EnvironmentSurfaceRole);

#if defined(_FOG_FRAGMENT)
#if (defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2))
    float viewZ = -input.fogCoord;
    float nearToFarZ = max(viewZ - _ProjectionParams.y, 0);
    half fogFactor = ComputeFogFactorZ0ToFar(nearToFarZ);
#else
    half fogFactor = 0;
#endif
#else
    half fogFactor = input.fogCoord;
#endif
    finalColor.rgb = MixFog(finalColor.rgb, fogFactor);
    finalColor.a = OutputAlpha(finalColor.a, IsSurfaceTypeTransparent(_Surface));

    outColor = finalColor;

#ifdef _WRITE_RENDERING_LAYERS
    uint renderingLayers = GetMeshRenderingLayer();
    outRenderingLayers = float4(EncodeMeshRenderingLayer(renderingLayers), 0, 0, 0);
#endif
}
            void RoomSightUnlitFragment(RoomSightUnlitVaryings input, out half4 outColor : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out float4 outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input.original);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input.original);
                    if(_EnvironmentFullSurface<.5)clip(RoomSightPartialContains(input.sightWorld) ? 1 : -1);
                UnlitPassFragmentEnvironment(input.original, input.sightWorld, input.sightNormal, outColor
                #ifdef _WRITE_RENDERING_LAYERS
                    , outRenderingLayers
                #endif
                );
                outColor=EnvironmentFogColor(outColor,input.sightWorld,0);
            }
            ENDHLSL
        }
    }
}
