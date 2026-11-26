Shader "Ossastra/OSPhysics/MatterSpriteFire"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("完整火焰图", 2D) = "white" {}
        [HideInInspector] _MatterState ("物质快照", 2D) = "black" {}
        _FlameColor ("火焰色", Color) = (1, .48, .14, 1)
        _FireGain ("火焰强度", Range(0, 2)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+6" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            Texture2D _MainTex;
            SamplerState sampler_MatterLinearClamp;
            Texture2D<float4> _MatterState;
            float4 _MatterGridSize, _GridMapping, _FlameColor;
            float _VisualTime, _FireGain;
            #include "MatterSpriteFireClip.hlsl"

            struct VertexInput
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 site : TEXCOORD1;
            };

            struct VertexOutput
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 samplePosition : TEXCOORD1;
                nointerpolation float4 site : TEXCOORD2;
            };

            VertexOutput Vert(VertexInput input)
            {
                VertexOutput output;
                float2 source = _GridMapping.xy + input.site.xy * _GridMapping.z;
                float phase = input.site.z * 6.283185 + _VisualTime * 12.4;
                float2 local = input.vertex.xy - source;
                // 相位取自固定燃点，足部不晃，整张火焰的透明轮廓只做轻微形变。
                local.x += _GridMapping.z * sin(phase) * input.uv.y * input.uv.y * .68;
                local.y *= 1 + .11 * sin(phase * .81);
                input.vertex.xy = source + local;
                output.position = UnityObjectToClipPos(input.vertex);
                output.samplePosition = (input.vertex.xy - _GridMapping.xy) * _GridMapping.w;
                output.uv = input.uv;
                output.site = input.site;
                return output;
            }

            float4 Frag(VertexOutput input) : SV_Target
            {
                float4 art = _MainTex.Sample(sampler_MatterLinearClamp, input.uv);
                clip(art.a - .012);
                clip(MatterFireVisible(input.site.xy, input.samplePosition) - .5);

                float phase = input.site.z * 6.283185 + _VisualTime * 13.2;
                float brightness = (.76 + .18 * input.site.w + .06 * sin(phase)) * _FireGain;
                float core = (1 - input.uv.y) * (1 - abs(input.uv.x * 2 - 1) * .65);
                float3 maskTint = lerp(_FlameColor.rgb, float3(1, .90, .53), saturate(core * 1.7));
                float chroma = max(art.r, max(art.g, art.b)) - min(art.r, min(art.g, art.b));
                float3 color = lerp(maskTint, art.rgb, saturate(chroma * 3));
                return float4(color * (1.15 * brightness), art.a * saturate(brightness * 1.35));
            }
            ENDHLSL
        }
    }
}
