// 横版点灯 UV 写入方向、亮边、亮部和环境四张屏幕附件。颜色为原作 Gamma 数值。
Shader "Hidden/Ossastra/SideView/AceLightMapWriter"
{
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
    }
    Fallback Off
}
