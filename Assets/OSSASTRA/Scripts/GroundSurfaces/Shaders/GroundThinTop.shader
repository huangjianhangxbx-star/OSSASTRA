// 独立薄顶面适配：沿用二维mask、world-XZ底纹与矩形作者界限；真实平台轮廓由几何裁剪负责。
// 与旧Tilemap Shader分离；只给派生材质设置层序与可测量深度偏移，不抬高真实几何/Collider。
Shader "Ossastra/GroundSurfaces/ThinTop"
{
 Properties
 {
  [PerRendererData] _MainTex("实际Sprite纹理",2D)="white"{}
  _SecondTex("底纹",2D)="white"{}
  _WorldPeriod("世界重复周期（米）",Float)=16
  _Cutoff("透明裁剪",Range(0,1))=.5
  _Color("颜色",Color)=(1,1,1,1)
  _GroundBounds("世界范围 minX/minZ/maxX/maxZ",Vector)=(-32,-24,32,24)
  _BaseCoverage("基层连续覆盖",Float)=0
  _DebugMask("仅看实际mask",Float)=0
  _DepthFactor("深度斜率偏移（默认0）",Float)=0
  _DepthUnits("层深度单位偏移（默认0）",Float)=0
 }
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout"}
  Pass
  {
   Name "ThinTopUnlit"
   Cull Off ZWrite On ZTest LEqual Blend Off
   Offset [_DepthFactor],[_DepthUnits]
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
   TEXTURE2D(_SecondTex); SAMPLER(sampler_SecondTex);
   CBUFFER_START(UnityPerMaterial)
    float4 _Color;float4 _GroundBounds;
    float _WorldPeriod;float _Cutoff;float _BaseCoverage;float _DebugMask;
    float _DepthFactor;float _DepthUnits;
   CBUFFER_END
   struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;float4 color:COLOR;};
   V vert(A input)
   {
    V output;output.world=TransformObjectToWorld(input.positionOS.xyz);output.positionCS=TransformWorldToHClip(output.world);
    output.uv=input.uv;output.color=input.color*_Color;return output;
   }
   half4 frag(V input):SV_Target
   {
    clip(input.world.x-_GroundBounds.x);clip(input.world.z-_GroundBounds.y);
    clip(_GroundBounds.z-input.world.x);clip(_GroundBounds.w-input.world.z);
    half mask=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv).a;
    mask=lerp(mask,1,saturate(_BaseCoverage));clip(mask*input.color.a-_Cutoff);
    half3 detail=SAMPLE_TEXTURE2D(_SecondTex,sampler_SecondTex,input.world.xz/max(_WorldPeriod,.0001)).rgb;
    return half4(lerp(detail,mask.xxx,saturate(_DebugMask))*input.color.rgb,1);
   }
   ENDHLSL
  }
 }
}
