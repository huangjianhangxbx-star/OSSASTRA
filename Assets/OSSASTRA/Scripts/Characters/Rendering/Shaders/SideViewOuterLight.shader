// 横版柔光仍按角色透明排序绘制，融合分支从同帧的联合轮廓和 ACE 光图取样。
Shader "Ossastra/Characters/SideViewOuterLight"
{
 Properties
 {
  [HideInInspector] _SideViewOuter("Outer light settings",Vector)=(0,0,0,0)
  [HideInInspector] _SideViewAceOuterSlot("ACE outer slot",Float)=0
  [HideInInspector] _SideViewAceOuterDepthAnchor("ACE world depth anchor",Vector)=(0,0,0,0)
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
   #include "Assets/OSSASTRA/Scripts/WorldRendering/SideView/Shaders/SideViewAceLightMaps.hlsl"
   float4 _SideViewOuter;
   float _SideViewAceOuterEnabled, _SideViewAceOuterSlot;
   float4 _SideViewAceOuterDepthAnchor, _SideViewAceOuterViewport, _SideViewAceOuterAtlasTexel;
   float4 _SideViewAceOuterScreen[2], _SideViewAceOuterAtlas[2], _SideViewAceOuterSettings[2];
   float4 _SideViewAceOuterOtherScreen[2], _SideViewAceOuterOtherAtlas[2];
   TEXTURE2D(_SideViewAceOuterCoverage);
   struct Varyings { float4 position: SV_POSITION; float3 world: TEXCOORD0; };

   float FusionOuterRadius(float width)
   {
       return max(1, clamp(width, 1, 4) * _SideViewAceOuterViewport.y / 1080);
   }

   Varyings Vert(float4 positionOS: POSITION)
   {
       Varyings output;
       if (_SideViewAceOuterSlot > .5)
       {
           int index = clamp((int)_SideViewAceOuterSlot - 1, 0, 1);
           float4 region = _SideViewAceOuterScreen[index];
           if (_SideViewAceOuterEnabled < .5 || _SideViewAceLightMapsReady < .5 || _SideViewAceOuterSettings[index].y <= 0)
           {
               output.position = float4(-2, -2, 0, 1);
               output.world = 0;
               return output;
           }
           float2 pad = (FusionOuterRadius(_SideViewAceOuterSettings[index].x) + 1) * _SideViewAceOuterViewport.zw;
           float2 screen = region.xy - pad + (region.zw + 2 * pad) * positionOS.xy;
           output.world = _SideViewAceOuterDepthAnchor.xyz;
           float4 clipPosition = TransformWorldToHClip(output.world);
           // 从最终像素矩形扩张四角，沿用角色锚点的深度；保留原有 opaque ZTest 与透明排序。
           output.position = float4(float2(screen.x * 2 - 1, 1 - 2 * screen.y) * clipPosition.w, clipPosition.zw);
           return output;
       }
       output.world = TransformObjectToWorld(positionOS.xyz);
       output.position = TransformWorldToHClip(output.world);
       return output;
   }

   float FusionOuterAlpha(float2 screen, float4 region, float4 atlas)
   {
       if (any(region.zw <= 0)) return 0;
       float2 local = (screen - region.xy) / region.zw;
       if (any(local < 0) || any(local > 1)) return 0;
       float2 margin = _SideViewAceOuterAtlasTexel.xy * .5;
       float2 uv = clamp(atlas.xy + local * atlas.zw, atlas.xy + margin, atlas.xy + atlas.zw - margin);
       return saturate(SAMPLE_TEXTURE2D(_SideViewAceOuterCoverage, sampler_LinearClamp, uv).a);
   }

   float4 FusionOuter(float2 pixel, int index)
   {
       clip(min(_SideViewAceOuterEnabled, _SideViewAceLightMapsReady) - .5);
       float4 settings = _SideViewAceOuterSettings[index];
       clip(settings.y - .00001);
       float2 screen = pixel * _SideViewAceOuterViewport.zw;
       float4 region = _SideViewAceOuterScreen[index];
       float4 atlas = _SideViewAceOuterAtlas[index];
       float self = FusionOuterAlpha(screen, region, atlas);
       float other = FusionOuterAlpha(screen, _SideViewAceOuterOtherScreen[index], _SideViewAceOuterOtherAtlas[index]);
       // 普通透明抗锯齿也属于身体；两个角色的内部都不接外柔光。
       float outside = 1 - smoothstep(.015, .2, max(self, other));
       clip(outside - .00001);
       SideViewAceEdgeLight light = SideViewAceSampleEdge(pixel);
       clip(light.weight - .00001);
       float2 radius = FusionOuterRadius(settings.x) * _SideViewAceOuterViewport.zw;
       float3 spreadWeights = float3(.56, .29, .15) * exp2(-max(.25, settings.w) * float3(.33, .66, 1));
       float coverage = 0;
       [unroll] for (int direction = 0; direction < 8; direction++)
       {
           float angle = direction * .7853981634;
           float2 outward = float2(cos(angle), sin(angle));
           float facing = saturate(dot(outward, light.direction));
           if (facing <= .01) continue;
           float2 stride = outward * radius;
           float nearAlpha = FusionOuterAlpha(screen - stride * .33, region, atlas);
           float middleAlpha = FusionOuterAlpha(screen - stride * .66, region, atlas);
           float farAlpha = FusionOuterAlpha(screen - stride, region, atlas);
           float spread = dot(float3(nearAlpha, middleAlpha, farAlpha), spreadWeights);
           coverage = max(coverage, spread * facing * facing);
       }
       float3 energy = max(0, light.energy) * settings.y * coverage * outside;
       float peak = max(energy.r, max(energy.g, energy.b));
       float budget = max(0, settings.z);
       energy *= budget / max(.00001, budget + peak);
#if defined(UNITY_COLORSPACE_GAMMA)
       energy = SideViewAceEncode(energy);
#endif
       return float4(energy, 0);
   }

   float4 Frag(Varyings input): SV_Target
   {
       if (_SideViewAceOuterSlot > .5)
           return FusionOuter(input.position.xy, clamp((int)_SideViewAceOuterSlot - 1, 0, 1));
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
