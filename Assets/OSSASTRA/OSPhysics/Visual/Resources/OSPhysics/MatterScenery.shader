Shader "Ossastra/OSPhysics/MatterScenery"
{
    Properties
    {
        _MainTex ("布景", 2D) = "white" {}
        _Tint ("布景颜色", Color) = (1,1,1,1)
        _WorldMood ("明暗对照", Range(0,2)) = 1
        _UseOSLightWorldMap ("真实世界灯图", Float) = 0
        _StageShape ("布景类型", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex, _SideViewAceWorldAmbient;
            float _SideViewAceLightMapsReady;
            float4 _SideViewAceScreenToMap;
            float4 _Tint;
            float _WorldMood, _UseOSLightWorldMap, _StageShape;

            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varying { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; float4 screen : TEXCOORD1; };

            Varying vert(Input v)
            {
                Varying o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                o.screen = ComputeScreenPos(o.pos);
                return o;
            }

            float4 frag(Varying i) : SV_Target
            {
                float4 painted = tex2D(_MainTex, i.uv) * _Tint * i.color;
                float localMood = lerp(.42, 1, saturate(_WorldMood)) + .22 * max(0,_WorldMood - 1);
                if (_StageShape > .5 && _StageShape < 1.5)
                {
                    float2 p = abs(i.uv - .5) * 2;
                    painted.a *= 1-smoothstep(.72, 1, max(p.x,p.y));
                }
                if (_UseOSLightWorldMap > .5 && _SideViewAceLightMapsReady > .5)
                {
                    float2 uv = i.pos.xy * _SideViewAceScreenToMap.xy;
                    uv.y = uv.y * _SideViewAceScreenToMap.z + _SideViewAceScreenToMap.w;
                    if (all(uv >= 0) && all(uv <= 1))
                    {
                        // 远景让其保留少量原画层次。使用正式世界图的 Gamma 调制口径。
                        float3 worldLight = tex2D(_SideViewAceWorldAmbient, uv).rgb;
                        float3 gamma = LinearToGammaSpace(max(0, painted.rgb));
                        painted.rgb = GammaToLinearSpace(max(0, gamma * lerp(.48, worldLight, .76)));
                    }
                }
                else painted.rgb *= localMood;
                return painted;
            }
            ENDCG
        }
    }
}
