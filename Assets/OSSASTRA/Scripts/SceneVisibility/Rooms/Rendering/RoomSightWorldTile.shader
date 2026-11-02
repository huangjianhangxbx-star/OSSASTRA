/* 功能：保留原世界XZ底纹、作者界限、mask与基层覆盖，仅追加正向片元许可。
 * 边界：继续使用原硬件深度，不将普通地表切换为薄顶面或改变层序。
 * 生命周期：仅临时适配材质使用，不持有场景或材质资源。
 */
Shader "Hidden/Ossastra/Rooms/SightWorldTile"
{
 Properties {
  [PerRendererData] _MainTex("原规则边缘",2D)="white"{}
  _SecondTex("底纹",2D)="white"{}
  _WorldPeriod("候选世界周期（米）",Float)=16
  _Cutoff("透明裁剪",Range(0,1))=.5
  _Color("颜色",Color)=(1,1,1,1)
  _GroundBounds("世界范围 minX/minZ/maxX/maxZ",Vector)=(-32,-24,32,24)
  _BaseCoverage("适配基层满覆盖",Float)=0
  _DebugMask("仅看实际mask",Float)=0
 }
 SubShader {
  Tags{"RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" "CanUseSpriteAtlas"="True"}
  Pass {
   Name "GroundUnlitAdaptation"
   Cull Off ZWrite On ZTest LEqual Blend Off
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
   TEXTURE2D(_SecondTex); SAMPLER(sampler_SecondTex);
   CBUFFER_START(UnityPerMaterial)
    float4 _Color; float4 _GroundBounds;
    float _WorldPeriod; float _Cutoff; float _BaseCoverage; float _DebugMask;
   CBUFFER_END
   #include "RoomSightClip.hlsl"
   struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;float4 color:COLOR;};
   V vert(A i){V o;o.world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.uv=i.uv;o.color=i.color*_Color;return o;}
   half4 frag(V i):SV_Target {
    clip(RoomSightPartialContains(RoomSightWorldFromDeviceDepth(i.positionCS.xy,i.positionCS.z))?1:-1);
    clip(i.world.x-_GroundBounds.x);clip(i.world.z-_GroundBounds.y);clip(_GroundBounds.z-i.world.x);clip(_GroundBounds.w-i.world.z);
    half mask=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).a;
    mask=lerp(mask,1,saturate(_BaseCoverage));clip(mask*i.color.a-_Cutoff);
    half3 detail=SAMPLE_TEXTURE2D(_SecondTex,sampler_SecondTex,i.world.xz/max(_WorldPeriod,.0001)).rgb;
    return RoomSightFogColor(half4(lerp(detail,mask.xxx,saturate(_DebugMask))*i.color.rgb,1),RoomSightWorldFromDeviceDepth(i.positionCS.xy,i.positionCS.z),0);
   }
   ENDHLSL
  }
 }
}
