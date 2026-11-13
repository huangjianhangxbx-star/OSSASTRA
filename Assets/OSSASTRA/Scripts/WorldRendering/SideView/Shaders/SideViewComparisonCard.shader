/*
 * 横版场景接收材质候选。保留已移植画片的纹理、顶点色、混合和 UV。
 * 元数据 Pass 为同相机旧 WorldLook 提供真实透明顺序中的表面类型与设备深度。
 * 本文件在 Unity 工程外，交接后由正式场 GPU 对照确定导入版本。
 */
Shader "Ossastra/SideView/ComparisonCard"
{
    Properties
    {
        [PerRendererData] _MainTex ("Source texture", 2D) = "white" {}
        _Color ("Source tint", Color) = (1,1,1,1)
        [HideInInspector] _SrcBlend ("Source blend", Float) = 1
        [HideInInspector] _DstBlend ("Destination blend", Float) = 10
        [HideInInspector] _Premultiply ("Multiply RGB by alpha", Float) = 1
        [HideInInspector] _BlurPixels ("Original per-card blur radius", Float) = 0
        [HideInInspector] _SideViewEnvironmentRole ("Ground / decoration / optical", Float) = 1
        [HideInInspector] _SideViewMetadataRole ("Ignore / protected / eligible", Float) = 1
        [HideInInspector] _SideViewOriginalAmbient ("B source ambient participation", Float) = 0
        [HideInInspector] _SideViewOriginalColorScale ("B original vertex tint", Vector) = (1,1,1,1)
        [HideInInspector] _SideViewFarBackground ("B far background", Float) = 0
        [HideInInspector] _SideViewBelowActors ("B draw before actors", Float) = 0
        [HideInInspector] _SideViewReplacementCoverage ("Source One Zero replacement", Float) = 0
        [HideInInspector] _SideViewAceSurfaceResponse ("C surface response", Float) = 0
        [HideInInspector] _EnvironmentResponseOverride ("Surface response multiplier", Float) = 1
        [HideInInspector] _EnvironmentReceiveShadow ("Receive main shadow", Float) = 1
        [HideInInspector] _EnvironmentMinimumOverride ("Surface minimum", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Assets/OSSASTRA/Scripts/EnvironmentSimulation/Rendering/EnvironmentLighting.hlsl"
        #include "SideViewComparisonColor.hlsl"
        #include "SideViewAceLightMaps.hlsl"
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        float4 _Color, _SideViewOriginalColorScale;
        float _Premultiply, _BlurPixels, _SideViewEnvironmentRole, _SideViewMetadataRole, _SideViewOriginalAmbient;
        float _SideViewRenderMode, _SideViewSplitBackground, _SideViewFarBackground, _SideViewBelowActors, _SideViewReplacementCoverage;
        float _SideViewAceSurfaceResponse;
        float4 _SideViewAmbientGamma;
        struct Attributes { float4 position : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
        struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; float3 world : TEXCOORD1; };
        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.world = TransformObjectToWorld(input.position.xyz);
            output.position = TransformWorldToHClip(output.world);
            output.uv = input.uv;
            output.color = input.color * _Color;
            if (_SideViewRenderMode > 1.5 && _SideViewRenderMode < 2.5) output.color *= _SideViewOriginalColorScale;
            return output;
        }
        float4 SampleCard(Varyings input, bool applyCardBlur)
        {
            float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
            if (applyCardBlur && _BlurPixels > 0)
            {
                float radius = .5 * _BlurPixels * _ScreenParams.y / 540.0;
                float2 x = ddx(input.uv) * radius;
                float2 y = ddy(input.uv) * radius;
                color *= .22;
                color += (SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv+x) + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv-x)
                    + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv+y) + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv-y)) * .11;
                color += (SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv+x+y) + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv+x-y)
                    + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv-x+y) + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv-x-y)) * .065;
                color += (SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv+2*x) + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv-2*x)
                    + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv+2*y) + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv-2*y)) * .02;
            }
            return color;
        }
        float4 ShadeOriginal(Varyings input, bool applyCardBlur)
        {
            float4 sample = SampleCard(input, applyCardBlur);
            sample.rgb = SideViewOriginalWorldColor(sample.rgb, input.color.rgb, _SideViewAmbientGamma.rgb, _SideViewOriginalAmbient);
            sample.a *= input.color.a;
            sample.rgb *= lerp(1, sample.a, _Premultiply);
            return sample;
        }
        ENDHLSL
        Pass
        {
            Name "SideViewCard"
            Tags { "LightMode"="SRPDefaultUnlit" }
            ZWrite Off
            Blend [_SrcBlend] [_DstBlend]
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            float4 Frag(Varyings input) : SV_Target
            {
                if (_SideViewRenderMode > 1.5 && _SideViewRenderMode < 2.5)
                {
                    clip(.5 - _SideViewSplitBackground * _SideViewBelowActors);
                    return ShadeOriginal(input, true);
                }
                float4 sample = SampleCard(input, true) * input.color;
                if (_SideViewRenderMode > 2.5 && _SideViewAceSurfaceResponse > 0)
                {
                    sample.rgb = SideViewAceWorldColor(sample.rgb, float3(1, 1, 1), sample.a, input.position, _SideViewAceSurfaceResponse);
                }
                else if (_SideViewRenderMode > .5 && _SideViewRenderMode < 1.5 && _SideViewEnvironmentRole >= 0)
                {
                    float3 normal = TransformObjectToWorldNormal(float3(0,0,-1));
                    sample.rgb = EnvironmentShadeLegacy(sample.rgb, input.world, normal, _SideViewEnvironmentRole);
                }
                sample.rgb *= lerp(1, sample.a, _Premultiply);
                return sample;
            }
            ENDHLSL
        }
        Pass
        {
            Name "SideViewFarCard"
            Tags { "LightMode"="SideViewFarCard" }
            ZWrite Off ZTest Always
            Blend [_SrcBlend] [_DstBlend]
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FarCard
            float4 FarCard(Varyings input) : SV_Target
            {
                clip(_SideViewFarBackground - .5);
                return ShadeOriginal(input, false);
            }
            ENDHLSL
        }
        Pass
        {
            Name "SideViewUnderActorCard"
            Tags { "LightMode"="SideViewUnderActorCard" }
            ZWrite Off
            Blend [_SrcBlend] [_DstBlend]
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment UnderActorCard
            float4 UnderActorCard(Varyings input) : SV_Target
            {
                clip(_SideViewBelowActors - _SideViewFarBackground - .5);
                return ShadeOriginal(input, true);
            }
            ENDHLSL
        }
        Pass
        {
            Name "SideViewVisibleSurface"
            Tags { "LightMode"="SideViewVisibleSurface" }
            // 重放当前透明顺序，只写本候选专用的颜色标签与深度；不写 CameraDepth。
            ZWrite On
            ZTest Always
            Blend One Zero
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment SurfaceTag
            #include "SideViewComparisonOpaqueDepth.hlsl"
            float4 SurfaceTag(Varyings input) : SV_Target
            {
                clip(_SideViewMetadataRole + .5);
                SideViewRespectOpaqueDepth(input.position);
                float alpha = max(_SideViewReplacementCoverage, SampleCard(input, true).a * input.color.a);
                clip(alpha - 1.0 / 255.0);
                // 横版前景在人物后提交，承担洞顶黑幕与人物遮挡，沿用 WorldLook 的保护标签。
                float permitted = step(.5, _SideViewMetadataRole) * _SideViewBelowActors;
                return float4(smoothstep(.04, .99, alpha) * permitted, permitted, 0, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
