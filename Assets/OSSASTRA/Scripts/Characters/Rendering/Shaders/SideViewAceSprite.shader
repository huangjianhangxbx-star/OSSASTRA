Shader "Ossastra/Characters/SideViewAceSprite"
{
 Properties
 {
  [PerRendererData] _MainTex("Sprite Texture",2D)="white"{}
  _Color("Tint",Color)=(1,1,1,1)
  [MaterialToggle] PixelSnap("Pixel snap",Float)=0
  [HideInInspector] _RendererColor("RendererColor",Color)=(1,1,1,1)
  [HideInInspector] _Flip("Flip",Vector)=(1,1,1,1)
  [PerRendererData] _AlphaTex("External Alpha",2D)="white"{}
  [PerRendererData] _EnableExternalAlpha("Enable External Alpha",Float)=0
  [HideInInspector] _SideViewPresentationOffset("Presented world offset",Vector)=(0,0,0,0)
  [HideInInspector] _VisualDepthFoot("Visual depth anchor",Vector)=(0,0,0,0)
  [HideInInspector] _VisualDepthNormal("Visual depth direction",Vector)=(0,0,0,0)
  [HideInInspector] _VisualDepthEnabled("Visual depth enabled",Float)=0

 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True"}
  Cull Off ZWrite Off ZTest LEqual Blend One OneMinusSrcAlpha
  Pass
  {
   Name "SideViewAceSprite"
   CGPROGRAM
   #pragma target 4.5
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #pragma multi_compile _ PIXELSNAP_ON
   #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
   #include "UnitySprites.cginc"
   #ifndef TEXTURE2D
   #define TEXTURE2D(name) Texture2D name
   #define SAMPLER(name) SamplerState name
   #define SAMPLE_TEXTURE2D(tex, samplerState, uv) tex.Sample(samplerState, uv)
   #endif
   #include "SideViewAceFusion.hlsl"
   struct Varyings { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; UNITY_VERTEX_OUTPUT_STEREO };
   Varyings Vert(appdata_t input)
   {
       UNITY_SETUP_INSTANCE_ID(input);
       v2f original = SpriteVert(input);
       Varyings output;
       UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
       float3 world = mul(unity_ObjectToWorld, UnityFlipSprite(input.vertex.xyz, _Flip)).xyz + _SideViewPresentationOffset.xyz;
       output.position = UnityWorldToClipPos(world);
       #ifdef PIXELSNAP_ON
       output.position = UnityPixelSnap(output.position);
       #endif
       output.uv = original.texcoord;
       output.color = original.color;
       return output;
   }
   float4 Frag(Varyings input):SV_Target
   {
       float4 color = SampleSpriteTexture(input.uv) * input.color;
       color.rgb = SideViewAceFusionColor(color.rgb * color.a, color.a, input.position);
       return color;
   }
   ENDCG
  }
  Pass
  {
   Name "SideViewVisibleSurface"
   Tags {"LightMode"="SideViewVisibleSurface"}
   ZWrite On ZTest Always Blend One Zero
   CGPROGRAM
   #pragma target 4.5
   #pragma vertex VertSurface
   #pragma fragment FragSurface
   #pragma multi_compile_instancing
   #pragma multi_compile _ PIXELSNAP_ON
   #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
   #include "UnitySprites.cginc"
   #include "Assets/OSSASTRA/Scripts/WorldRendering/SideView/Shaders/SideViewComparisonOpaqueDepth.hlsl"
   float4 _SideViewPresentationOffset;
   v2f VertSurface(appdata_t input)
   {
       UNITY_SETUP_INSTANCE_ID(input);
       v2f output = SpriteVert(input);
       float3 world = mul(unity_ObjectToWorld, UnityFlipSprite(input.vertex.xyz, _Flip)).xyz + _SideViewPresentationOffset.xyz;
       output.vertex = UnityWorldToClipPos(world);
       #ifdef PIXELSNAP_ON
       output.vertex = UnityPixelSnap(output.vertex);
       #endif
       return output;
   }
   float4 FragSurface(v2f input) : SV_Target
   {
       SideViewRespectOpaqueDepth(input.vertex);
       float alpha = SampleSpriteTexture(input.texcoord).a * input.color.a;
       clip(alpha - 1.0 / 255.0);
       return float4(0, 0, 0, 1);
   }
   ENDCG
  }
 }
 Fallback Off
}
