/* 功能：保留薄顶面底纹、边缘alpha与统一平面深度，仅在写颜色/深度前裁到合法观察片段。
 * 边界：不改原Shader/材质/地形高度；逻辑世界点从最终设备深度重建，与可见平面保持一致。
 * 生命周期：仅供唯一可见性根的临时适配材质绘制，源物理与Caster由原管线维护。
 */
Shader "Hidden/Ossastra/Rooms/SightThinTop"
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
  [HideInInspector] _DepthFactor("兼容旧材质；不参与渲染",Float)=0
  [HideInInspector] _DepthUnits("兼容旧材质；不参与渲染",Float)=0
 }
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout"}
  Pass
  {
   Name "ThinTopUnlit"
   Cull Off ZWrite On ZTest LEqual Blend Off
   Offset 0,0
   HLSLPROGRAM
   #pragma target 4.5
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
   #include "RoomSightClip.hlsl"
   struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct V
   {
    float4 positionCS:SV_POSITION;
    float2 uv:TEXCOORD0;
    float3 world:TEXCOORD1;
    nointerpolation float planeYWS:TEXCOORD2;
    float4 color:COLOR;
   };
   struct FragmentOutput{half4 color:SV_Target;float depth:SV_Depth;};
   V vert(A input)
   {
    V output;output.world=TransformObjectToWorld(input.positionOS.xyz);output.positionCS=TransformWorldToHClip(output.world);
    output.uv=input.uv;output.color=input.color*_Color;
    // Generator只提供水平顶面。共面层共享完全相同的常数，避免插值world.y重引入primitive差异。
    output.planeYWS=output.world.y;return output;
   }
   float UnifiedPlaneDeviceDepth(float2 rasterPixel,float planeYWS)
   {
    // URP14：ComputeClipSpacePosition自身处理平台Y翻转，不重复翻转screenUV。
    float2 screenUV=rasterPixel/_ScaledScreenParams.xy;
    float3 nearWS=ComputeWorldSpacePosition(screenUV,UNITY_NEAR_CLIP_VALUE,UNITY_MATRIX_I_VP);
    float4 farWSH=mul(UNITY_MATRIX_I_VP,ComputeClipSpacePosition(screenUV,UNITY_RAW_FAR_CLIP_VALUE));
    // 齐次方向同时兼容有限/无穷远投影；不除far.w。
    float3 directionWS=farWSH.xyz-nearWS*farWSH.w;
    clip(abs(directionWS.y)-1e-8);
    float t=(planeYWS-nearWS.y)/directionWS.y;
    float3 onPlaneWS=nearWS+directionWS*t;onPlaneWS.y=planeYWS;
    float3 view=TransformWorldToView(onPlaneWS);
    float eyeDepth=-view.z;
    clip(eyeDepth-_ProjectionParams.y);clip(_ProjectionParams.z-eyeDepth);
    float4 reprojection=TransformWViewToHClip(view);
    float ndcDepth=reprojection.z/reprojection.w;
    #if UNITY_REVERSED_Z
     return saturate(ndcDepth);
    #else
     return saturate((ndcDepth-UNITY_NEAR_CLIP_VALUE)/(1.0-UNITY_NEAR_CLIP_VALUE));
    #endif
   }
   FragmentOutput frag(V input)
   {
    float deviceDepth=UnifiedPlaneDeviceDepth(input.positionCS.xy,input.planeYWS);
    float3 logicalWorld=RoomSightWorldFromDeviceDepth(input.positionCS.xy,deviceDepth);
    clip(RoomSightPartialContains(logicalWorld)?1:-1);
    clip(input.world.x-_GroundBounds.x);clip(input.world.z-_GroundBounds.y);
    clip(_GroundBounds.z-input.world.x);clip(_GroundBounds.w-input.world.z);
    half mask=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv).a;
    mask=lerp(mask,1,saturate(_BaseCoverage));clip(mask*input.color.a-_Cutoff);
    half3 detail=SAMPLE_TEXTURE2D(_SecondTex,sampler_SecondTex,input.world.xz/max(_WorldPeriod,.0001)).rgb;
    FragmentOutput output;
    output.color=half4(lerp(detail,mask.xxx,saturate(_DebugMask))*input.color.rgb,1);
    output.depth=deviceDepth;return output;
   }
   ENDHLSL
  }
 }
}
