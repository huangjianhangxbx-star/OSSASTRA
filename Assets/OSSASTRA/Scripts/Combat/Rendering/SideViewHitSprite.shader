// 横版帧动画敌人使用 Unity Sprite 的图集、翻转和外置 alpha，受击只改变预乘后的 RGB。
Shader "Ossastra/Combat/SideViewHitSprite"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1, 1, 1, 1)
        [MaterialToggle] PixelSnap("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor("Renderer Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _Flip("Flip", Vector) = (1, 1, 1, 1)
        [PerRendererData] _AlphaTex("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha("Enable External Alpha", Float) = 0
        [HideInInspector] _OssastraHitFlash("Hit Flash", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "CanUseSpriteAtlas" = "True" }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment HitSpriteFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            float _OssastraHitFlash;

            float4 HitSpriteFrag(v2f input) : SV_Target
            {
                float4 color = SampleSpriteTexture(input.texcoord) * input.color;
                color.rgb = lerp(color.rgb * color.a, color.aaa, _OssastraHitFlash);
                return color;
            }
            ENDCG
        }
    }
    Fallback Off
}
