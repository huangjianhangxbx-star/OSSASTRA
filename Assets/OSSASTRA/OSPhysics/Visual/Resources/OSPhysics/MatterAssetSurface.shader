Shader "Ossastra/OSPhysics/PlacedMapAsset"
{
    Properties
    {
        _MainTex ("地图图件", 2D) = "white" {}
        _AssetDamage ("属主与局部耗损", 2D) = "black" {}
        _WorldMood ("明暗", Float) = 1
        _UseOSLightWorldMap ("世界灯图", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex, _AssetDamage;
            sampler2D _SideViewAceWorldAmbient;
            float4 _SideViewAceScreenToMap;
            float _WorldMood, _UseOSLightWorldMap, _SideViewAceLightMapsReady;

            struct Vertex
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
                float2 fuel : TEXCOORD1;
                float2 owner : TEXCOORD2;
                float4 color : COLOR;
            };

            struct Varying
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 fuel : TEXCOORD1;
                float2 owner : TEXCOORD2;
                float4 color : COLOR;
            };

            Varying vert(Vertex v)
            {
                Varying o;
                o.position = UnityObjectToClipPos(v.position);
                o.uv = v.uv;
                o.fuel = v.fuel;
                o.owner = v.owner;
                o.color = v.color;
                return o;
            }

            float4 frag(Varying i) : SV_Target
            {
                float4 result = tex2D(_MainTex, i.uv) * i.color;
                if (any(i.owner > 0))
                {
                    // 每个位置从本图件专属的可见耗损小图采样。整张原图的 Alpha 仍是剪影。
                    float2 local = tex2D(_AssetDamage, i.fuel).rg;
                    result.a *= smoothstep(.04, .72, local.x);
                    result.rgb = lerp(result.rgb, result.rgb * .21 + float3(.031, .014, .007), local.y);
                }

                if (_UseOSLightWorldMap > .5 && _SideViewAceLightMapsReady > .5)
                {
                    float2 uv = i.position.xy * _SideViewAceScreenToMap.xy;
                    uv.y = uv.y * _SideViewAceScreenToMap.z + _SideViewAceScreenToMap.w;
                    if (all(uv >= 0) && all(uv <= 1))
                    {
                        float3 lighting = tex2D(_SideViewAceWorldAmbient, uv).rgb;
                        result.rgb = GammaToLinearSpace(max(0, LinearToGammaSpace(max(0, result.rgb)) * lighting));
                    }
                }
                else result.rgb *= lerp(.38, 1, saturate(_WorldMood)) + .24 * max(0, _WorldMood - 1);
                return result;
            }
            ENDCG
        }
    }
}
