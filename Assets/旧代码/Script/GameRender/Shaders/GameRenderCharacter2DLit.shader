Shader "ANKALUTE/GameRender/Character2DLit"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite / Spine Atlas", 2D) = "white" {}
        _MaskTex("Shape Light Mask", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        [GameRenderOverride] _GRUseInkOverride("Override Ink", Float) = 0
        _GRInkOverride("Ink", Range(0,1)) = 0.18
        [GameRenderOverride] _GRUseOutlineOverride("Override Inner Outline", Float) = 0
        _GROutlineOverride("Inner Outline", Range(0,1)) = 0.25
        [GameRenderOverride] _GRUseHighlightOverride("Override Highlight", Float) = 0
        _GRHighlightOverride("Highlight", Range(0,1)) = 0.04
        [ToggleUI] _GRPremultipliedAlpha("Premultiplied Alpha", Float) = 0
        _GRUvMinMax("Valid Atlas UV Bounds", Vector) = (0,0,1,1)
        _GRAlphaEdgePixels("Inner Outline Width (texels)", Range(0.5,4)) = 2
        [HideInInspector] _SrcBlend("Source Blend", Float) = 5
        [HideInInspector] _DstBlend("Destination Blend", Float) = 10
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "Character2DLit"
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #pragma vertex CharacterVertex
            #pragma fragment CharacterFragment
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_0 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_1 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_2 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_3 __

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                half2 lightingUV : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);
            float4 _MainTex_TexelSize;
            float _GRGlobalCharacterInk;
            float _GRGlobalCharacterOutline;
            float _GRGlobalCharacterHighlight;
            float _GRGlobalStyleEnabled;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _GRUseInkOverride;
                float _GRInkOverride;
                float _GRUseOutlineOverride;
                float _GROutlineOverride;
                float _GRUseHighlightOverride;
                float _GRHighlightOverride;
                float _GRPremultipliedAlpha;
                float4 _GRUvMinMax;
                float _GRAlphaEdgePixels;
                float _SrcBlend;
                float _DstBlend;
            CBUFFER_END

            Varyings CharacterVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.lightingUV = half2(ComputeScreenPos(output.positionCS / output.positionCS.w).xy);
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"

            float ReadAlpha(float2 uv)
            {
                float2 safe = clamp(uv, _GRUvMinMax.xy + _MainTex_TexelSize.xy * 0.5,
                                        _GRUvMinMax.zw - _MainTex_TexelSize.xy * 0.5);
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, safe).a;
            }

            half4 CharacterFragment(Varyings input) : SV_Target
            {
                half4 raw = input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv);
                SurfaceData2D surfaceData;
                InputData2D inputData;
                InitializeSurfaceData(raw.rgb, raw.a, mask, surfaceData);
                InitializeInputData(input.uv, input.lightingUV, inputData);
                half4 lit = CombinedShapeLightShared(surfaceData, inputData);

                float alpha = saturate(lit.a);
                float3 straightColor = _GRPremultipliedAlpha > 0.5 ?
                    lit.rgb / max(alpha, 0.0001) : lit.rgb;
                float luminance = dot(max(straightColor, 0), float3(0.2126, 0.7152, 0.0722));
                float enabled = step(0.5, _GRGlobalStyleEnabled);
                float inkStrength = enabled * saturate(lerp(_GRGlobalCharacterInk, _GRInkOverride,
                    step(0.5, _GRUseInkOverride)));
                float outlineStrength = enabled * saturate(lerp(_GRGlobalCharacterOutline, _GROutlineOverride,
                    step(0.5, _GRUseOutlineOverride)));
                float highlightStrength = enabled * saturate(lerp(_GRGlobalCharacterHighlight, _GRHighlightOverride,
                    step(0.5, _GRUseHighlightOverride)));

                float2 d = _MainTex_TexelSize.xy * _GRAlphaEdgePixels;
                float neighbour = min(min(ReadAlpha(input.uv + float2(d.x, 0)),
                                          ReadAlpha(input.uv - float2(d.x, 0))),
                                      min(ReadAlpha(input.uv + float2(0, d.y)),
                                          ReadAlpha(input.uv - float2(0, d.y))));
                float innerContour = saturate((raw.a - neighbour) * 2.5);
                float darkRegion = 1.0 - smoothstep(0.14, 0.38, luminance);
                float ink = saturate(darkRegion * inkStrength * 0.45 +
                                     innerContour * outlineStrength);
                straightColor = lerp(straightColor, straightColor * 0.08, ink);
                float highlight = smoothstep(0.58, 1.2, luminance) * highlightStrength;
                straightColor += highlight * (1.0 - saturate(straightColor));
                lit.rgb = _GRPremultipliedAlpha > 0.5 ? straightColor * alpha : straightColor;
                return lit;
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/2D/Sprite-Lit-Default"
}
