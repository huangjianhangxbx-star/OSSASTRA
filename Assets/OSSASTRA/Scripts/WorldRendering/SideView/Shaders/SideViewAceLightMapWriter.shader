// 横版点灯 UV 写入方向、亮边、亮部和环境四张屏幕附件。颜色为原作 Gamma 数值。
Shader "Hidden/Ossastra/SideView/AceLightMapWriter"
{
    Properties
    {
        [HideInInspector] _AcePreviewSource ("Debug preview source", 2D) = "black" {}
        [HideInInspector] _AcePreviewDirection ("Debug preview direction", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "AceClearMaps"
            Cull Off ZWrite Off ZTest Always Blend Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ClearVert
            #pragma fragment ClearFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _AceWriterAmbientClear;
            struct ClearMaps
            {
                float4 direction : SV_Target0;
                float4 highlight : SV_Target1;
                float4 bright : SV_Target2;
                float4 ambient : SV_Target3;
            };

            float4 ClearVert(uint id : SV_VertexID) : SV_POSITION
            {
                return GetFullScreenTriangleVertexPosition(id);
            }

            ClearMaps ClearFrag()
            {
                ClearMaps output;
                output.direction = float4(.5, .5, 0, 0);
                output.highlight = output.bright = 0;
                output.ambient = float4(_AceWriterAmbientClear.rgb, 1);
                return output;
            }
            ENDHLSL
        }
        Pass
        {
            Name "AceLightMap"
            Cull Off ZWrite Off ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_AceWriterMask); SAMPLER(sampler_AceWriterMask);
            float4x4 _AceWriterViewProjection;
            float4 _AceWriterShape, _AceWriterAmbient, _AceWriterBright, _AceWriterHighlight;
            float _AceWriterIntensity;

            struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(uint id : SV_VertexID)
            {
                static const float2 corners[6] = {
                    float2(0,0), float2(1,0), float2(1,1), float2(0,0), float2(1,1), float2(0,1)
                };
                Varyings output;
                output.uv = corners[id];
                float3 world = TransformObjectToWorld(float3(output.uv - .5, 0));
                output.position = mul(_AceWriterViewProjection, float4(world,1));
                return output;
            }

            struct LightMaps
            {
                float4 direction : SV_Target0;
                float4 highlight : SV_Target1;
                float4 bright : SV_Target2;
                float4 ambient : SV_Target3;
            };

            LightMaps Frag(Varyings input)
            {
                float2 fromCenter = input.uv - .5;
                float distance = length(fromCenter);
                float radial = saturate((1 - 2 * distance) * _AceWriterShape.y);
                float weight = pow(radial, _AceWriterShape.x) * SAMPLE_TEXTURE2D(_AceWriterMask, sampler_AceWriterMask, input.uv).a;
                float cover = saturate(weight * _AceWriterIntensity);
                float2 towardLight = -fromCenter / max(.00001, distance);
                LightMaps output;
                output.direction = float4(.5 + towardLight * (_AceWriterShape.z / 6), _AceWriterShape.w, cover * _AceWriterHighlight.a);
                output.highlight = float4(_AceWriterHighlight.rgb, cover * _AceWriterHighlight.a);
                output.bright = float4(_AceWriterBright.rgb, cover * _AceWriterBright.a);
                output.ambient = float4(_AceWriterAmbient.rgb, cover * _AceWriterAmbient.a);
                return output;
            }
            ENDHLSL
        }
        Pass
        {
            Name "AceEdgeMaps"
            Tags { "LightMode"="SideViewAceEdgeMaps" }
            Cull Off ZWrite Off ZTest Always
            Blend One One

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex EdgeVert
            #pragma fragment EdgeFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_AceWriterMask); SAMPLER(sampler_AceWriterMask);
            float4x4 _AceWriterViewProjection;
            float4 _AceWriterShape, _AceWriterAmbient, _AceWriterHighlight;
            float _AceWriterIntensity;
            float4 _AceEdgeTarget;

            struct EdgeVaryings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 projected : TEXCOORD1;
                nointerpolation float4 center : TEXCOORD2;
            };

            EdgeVaryings EdgeVert(uint id : SV_VertexID)
            {
                static const float2 corners[6] = {
                    float2(0,0), float2(1,0), float2(1,1), float2(0,0), float2(1,1), float2(0,1)
                };
                EdgeVaryings output;
                output.uv = corners[id];
                float3 world = TransformObjectToWorld(float3(output.uv - .5, 0));
                output.position = output.projected = mul(_AceWriterViewProjection, float4(world,1));
                output.center = mul(_AceWriterViewProjection, float4(TransformObjectToWorld(float3(0,0,0)),1));
                return output;
            }

            struct EdgeMaps
            {
                float4 direction : SV_Target0;
                float4 energy : SV_Target1;
            };

            EdgeMaps EdgeFrag(EdgeVaryings input)
            {
                EdgeMaps output = (EdgeMaps)0;
                if (input.center.w < .00001 || input.projected.w < .00001) return output;

                // Measure in camera pixels and undo the GPU and light-map projection flips.
                float2 toward = input.center.xy / input.center.w - input.projected.xy / input.projected.w;
                toward *= .5 * _AceEdgeTarget.xy;
                toward.y *= _AceEdgeTarget.z;
#if UNITY_UV_STARTS_AT_TOP
                toward.y = -toward.y;
#endif
                float distancePixels = length(toward);
                float directionFade = smoothstep(.5, 2, distancePixels);

                float radial = saturate((1 - 2 * length(input.uv - .5)) * _AceWriterShape.y);
                float cover = saturate(pow(radial, _AceWriterShape.x) *
                    SAMPLE_TEXTURE2D(_AceWriterMask, sampler_AceWriterMask, input.uv).a * _AceWriterIntensity);
                cover *= _AceWriterAmbient.a * _AceWriterHighlight.a * _AceWriterShape.z * directionFade;
                // Reuse the visible local lamp's color. Highlight alpha and edge offset control its edge participation.
                float3 source = max(0, _AceWriterAmbient.rgb);
                float3 lightColor = min(4, lerp(source / 12.92, pow((source + .055) / 1.055, 2.4), step(.04045, source)));
                float weight = cover * saturate(max(lightColor.r, max(lightColor.g, lightColor.b)));
                output.direction = float4(toward * (weight / max(.00001, distancePixels)), weight, 0);
                output.energy = float4(lightColor * cover, weight);
                return output;
            }
            ENDHLSL
        }
        Pass
        {
            Name "AceDebugPreview"
            Tags { "LightMode"="SideViewAceDebugPreview" }
            Cull Off ZWrite Off ZTest Always Blend Off

            HLSLPROGRAM
            #pragma vertex PreviewVert
            #pragma fragment PreviewFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            TEXTURE2D(_AcePreviewSource); SAMPLER(sampler_AcePreviewSource);
            float _AcePreviewDirection;

            struct PreviewVaryings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };

            PreviewVaryings PreviewVert(uint id : SV_VertexID)
            {
                PreviewVaryings output;
                output.position = GetFullScreenTriangleVertexPosition(id);
                output.uv = GetFullScreenTriangleTexCoord(id);
                return output;
            }

            float4 PreviewFrag(PreviewVaryings input) : SV_Target
            {
                float4 value = SAMPLE_TEXTURE2D(_AcePreviewSource, sampler_AcePreviewSource, input.uv);
                if (_AcePreviewDirection < .5) return float4(value.aaa, 1);
                // R/G show signed screen X/Y about gray. B shows accumulated lamp weight.
                float3 display = .5 + .5 * float3(clamp(value.rg, -1, 1), saturate(value.b));
#if !defined(UNITY_COLORSPACE_GAMMA)
                display = SRGBToLinear(display);
#endif
                return float4(display, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
