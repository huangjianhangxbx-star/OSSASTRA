/* 功能：复用本机URP14 Unlit的原Forward着色与材质属性，在颜色/深度写入前裁剪合法视域。
 * 边界：只作为按原材质分桶的正向overrideMaterial；不替换原材质、Caster或光照权威；房顶像素由独立许可决定。
 * 生命周期：无自有纹理/材质，显式Shader引用由当前相机RendererFeature持有。
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
            // 保留包内完整Varyings及其条件语义，只附加本视域所需的世界点。
            struct RoomSightUnlitVaryings
            {
                Varyings original;
                float3 sightWorld : TEXCOORD7;
            };
            RoomSightUnlitVaryings RoomSightUnlitVertex(Attributes input)
            {
                RoomSightUnlitVaryings output;
                output.original = UnlitPassVertex(input);
                output.sightWorld = TransformObjectToWorld(input.positionOS.xyz);
                return output;
            }
            void RoomSightUnlitFragment(RoomSightUnlitVaryings input, out half4 outColor : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out float4 outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input.original);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input.original);
                    clip(RoomSightPartialContains(input.sightWorld) ? 1 : -1);
                UnlitPassFragment(input.original, outColor
                #ifdef _WRITE_RENDERING_LAYERS
                    , outRenderingLayers
                #endif
                );
                outColor=RoomSightFogColor(outColor,input.sightWorld,0);
            }
            ENDHLSL
        }
    }
}
