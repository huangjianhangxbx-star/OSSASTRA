Shader "ANKALUTE/BattleCloseupComposite"
{
    Properties
    {
        _BlurStrength ("Blur Strength", Range(0,1)) = 0.58
        _SpectatorDim ("Dim", Range(0,1)) = 0.42
        _ReturnProgress ("Return Progress", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 screenPos : TEXCOORD0; };
            TEXTURE2D(_CameraSortingLayerTexture);
            SAMPLER(sampler_CameraSortingLayerTexture);
            float4 _CameraSortingLayerTexture_TexelSize;
            float _BlurStrength;
            float _SpectatorDim;
            float _ReturnProgress;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.screenPos.xy / input.screenPos.w;
                float2 sourceTexel = _CameraSortingLayerTexture_TexelSize.xy;
                if (sourceTexel.x <= 0 || sourceTexel.y <= 0)
                    sourceTexel = 1.0 / max(_ScreenParams.xy, float2(1, 1));
                float2 stepUV = sourceTexel * lerp(1.25, 5.5, _BlurStrength);
                half4 center = SAMPLE_TEXTURE2D(_CameraSortingLayerTexture, sampler_CameraSortingLayerTexture, uv);
                half4 blur = center * .20h;
                blur += SAMPLE_TEXTURE2D(_CameraSortingLayerTexture, sampler_CameraSortingLayerTexture, uv + float2(stepUV.x, 0)) * .12h;
                blur += SAMPLE_TEXTURE2D(_CameraSortingLayerTexture, sampler_CameraSortingLayerTexture, uv - float2(stepUV.x, 0)) * .12h;
                blur += SAMPLE_TEXTURE2D(_CameraSortingLayerTexture, sampler_CameraSortingLayerTexture, uv + float2(0, stepUV.y)) * .12h;
                blur += SAMPLE_TEXTURE2D(_CameraSortingLayerTexture, sampler_CameraSortingLayerTexture, uv - float2(0, stepUV.y)) * .12h;
                blur += SAMPLE_TEXTURE2D(_CameraSortingLayerTexture, sampler_CameraSortingLayerTexture, uv + stepUV) * .08h;
                blur += SAMPLE_TEXTURE2D(_CameraSortingLayerTexture, sampler_CameraSortingLayerTexture, uv - stepUV) * .08h;
                blur += SAMPLE_TEXTURE2D(_CameraSortingLayerTexture, sampler_CameraSortingLayerTexture, uv + float2(stepUV.x, -stepUV.y)) * .08h;
                blur += SAMPLE_TEXTURE2D(_CameraSortingLayerTexture, sampler_CameraSortingLayerTexture, uv + float2(-stepUV.x, stepUV.y)) * .08h;
                half4 result = lerp(center, blur, _BlurStrength);
                result.rgb *= _SpectatorDim;
                // The sorting-layer copy is pre-post-process; keep it a subtle
                // veil over the already rendered world to avoid double-toning.
                result.a = (1 - _ReturnProgress) * .85h;
                return result;
            }
            ENDHLSL
        }
    }
}
