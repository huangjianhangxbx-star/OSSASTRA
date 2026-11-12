Shader "Ossastra/Characters/SideViewOuterLight"
{
 Properties
 {
  [HideInInspector] _SideViewOuter("Outer light settings",Vector)=(0,0,0,0)
  [HideInInspector] _CharacterRimLocalPosition("Light position",Vector)=(0,0,0,0)
  [HideInInspector] _CharacterRimLocalColor("Light color",Vector)=(0,0,0,0)
  [HideInInspector] _CharacterCoverageReady("Coverage ready",Float)=0
  [HideInInspector] _CharacterCoverageTexture("Coverage",2D)="black"{}
  [HideInInspector] _CharacterCoverageRect("Coverage rect",Vector)=(0,0,0,0)
  [HideInInspector] _CharacterCoverageAtlasRect("Atlas rect",Vector)=(0,0,0,0)
  [HideInInspector] _CharacterCoverageScreenSize("Screen size",Vector)=(0,0,0,0)
  [HideInInspector] _CharacterCoverageTexelSize("Texel size",Vector)=(0,0,0,0)
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
  Cull Off ZWrite Off ZTest LEqual Blend One One ColorMask RGB
  Pass
  {
   Name "SideViewOrderedOuterLight"
   Tags {"LightMode"="SRPDefaultUnlit"}
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "SideViewCharacterLighting.hlsl"
   float4 _SideViewOuter;
   struct Varyings { float4 position: SV_POSITION; float3 world: TEXCOORD0; };
   Varyings Vert(float4 positionOS: POSITION)
   {
       Varyings output;
       output.world = TransformObjectToWorld(positionOS.xyz);
       output.position = TransformWorldToHClip(output.world);
       return output;
   }
   float4 Frag(Varyings input): SV_Target
   {
       clip(_CharacterCoverageReady - .5);
       float2 screenUv = input.position.xy * _CharacterCoverageScreenSize.zw;
       float2 uv = (screenUv - _CharacterCoverageRect.xy) / _CharacterCoverageRect.zw;
       float self = CharacterLightingCoverageAlpha(uv);
       clip(.998 - self);
       float3 light = _CharacterRimLocalPosition.xyz - input.world;
       float squareDistance = dot(light, light);
       clip(squareDistance - .00001);
       float range = max(_CharacterRimLocalPosition.w, .01);
       float fade = saturate(1 - squareDistance / (range * range));
       float2 radius = clamp(_SideViewOuter.x, 1, 4) * _CharacterCoverageScreenSize.y / 1080 *
           _CharacterCoverageScreenSize.zw / _CharacterCoverageRect.zw;
       float coverage = 0;
       [unroll] for (int direction = 0; direction < 8; direction++)
       {
           float angle = direction * .7853981634;
           float2 outward = float2(cos(angle), sin(angle));
           float directional = CharacterLightingDirectionWeight(outward, light * rsqrt(squareDistance));
           float inside = CharacterLightingCoverageAlpha(uv - outward * radius * .38);
           float edge = CharacterLightingCoverageAlpha(uv - outward * radius * .78);
           float soft = exp2(-max(.25, _SideViewOuter.w));
           coverage = max(coverage, directional * max(inside * .8, edge * soft));
       }
       float3 energy = max(0, _CharacterRimLocalColor.rgb) * fade * fade * _SideViewOuter.y * coverage * (1 - self);
       float peak = max(energy.r, max(energy.g, energy.b));
       float budget = max(0, _SideViewOuter.z) * .5;
       return float4(energy * budget / max(.00001, budget + peak), 0);
   }
   ENDHLSL
  }
 }
 Fallback Off
}
