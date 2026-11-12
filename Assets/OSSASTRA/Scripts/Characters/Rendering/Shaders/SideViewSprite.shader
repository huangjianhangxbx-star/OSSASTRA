Shader "Ossastra/Characters/SideViewSprite"
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
  [HideInInspector] _EnvironmentSurfaceRole("Character surface",Float)=2
  [HideInInspector] _EnvironmentResponseOverride("Response",Float)=1
  [HideInInspector] _EnvironmentReceiveShadow("Main light shadows",Float)=1
  [HideInInspector] _EnvironmentMinimumOverride("Readability",Float)=0
  [HideInInspector] _EnvironmentStylizedSkyTint("A tint",Vector)=(0,0,0,0)
  [HideInInspector] _CharacterEnvironmentTint("B tint",Vector)=(0,0,0,0)
  [HideInInspector] _CharacterEnvironmentBlend("B blend",Float)=0
  [HideInInspector] _CharacterLightControls("Light controls",Vector)=(0,0,0,0)
  [HideInInspector] _CharacterLightShoulder("Protection shoulder",Vector)=(0,0,0,0)
  [HideInInspector] _CharacterRimShape("Rim shape",Vector)=(0,0,0,0)
  [HideInInspector] _CharacterRimLocalPosition("Rim light position",Vector)=(0,0,0,0)
  [HideInInspector] _CharacterRimLocalColor("Rim light color",Vector)=(0,0,0,0)
  [HideInInspector] _CharacterCoverageReady("Coverage ready",Float)=0
  [HideInInspector] _CharacterCoverageTexture("Coverage",2D)="black"{}
  [HideInInspector] _CharacterCoverageRect("Coverage rect",Vector)=(0,0,0,0)
  [HideInInspector] _CharacterCoverageAtlasRect("Atlas rect",Vector)=(0,0,0,0)
  [HideInInspector] _CharacterCoverageScreenSize("Screen size",Vector)=(0,0,0,0)
  [HideInInspector] _CharacterCoverageTexelSize("Texel size",Vector)=(0,0,0,0)
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True"}
  Cull Off ZWrite Off ZTest LEqual Blend One OneMinusSrcAlpha
  Pass
  {
   Name "SideViewLitSprite"
   CGPROGRAM
   #pragma target 4.5
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #pragma multi_compile _ PIXELSNAP_ON
   #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
   #include "UnitySprites.cginc"
   #define ENVIRONMENT_LEGACY_SPRITE
   #ifndef TEXTURE2D
   #define TEXTURE2D(name) Texture2D name
   #define SAMPLER(name) SamplerState name
   #define SAMPLE_TEXTURE2D(tex, samplerState, uv) tex.Sample(samplerState, uv)
   #endif
   #include "SideViewCharacterLighting.hlsl"
   struct Varyings { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; float3 world:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
   Varyings Vert(appdata_t input)
   {
       UNITY_SETUP_INSTANCE_ID(input);
       v2f original = SpriteVert(input);
       Varyings output;
       UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
       output.world = mul(unity_ObjectToWorld, UnityFlipSprite(input.vertex.xyz, _Flip)).xyz + _SideViewPresentationOffset.xyz;
       output.position = UnityWorldToClipPos(output.world);
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
       color.rgb = SideViewShade(color.rgb * color.a, color.a, input.position.xy, input.world);
       return color;
   }
   ENDCG
  }
 }
 Fallback Off
}
