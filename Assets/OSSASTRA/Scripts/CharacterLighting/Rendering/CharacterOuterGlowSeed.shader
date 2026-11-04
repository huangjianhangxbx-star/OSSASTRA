/* 功能：将当帧实际角色片元转换为有界受光源，或写入保守的最近角色深度。
 * 边界：自身形状只读完整coverage，颜色不从本体亮度/保底生成；所有光源和新像素仍受当前视野与最近实体深度约束。
 * RGB为已乘覆盖alpha的线性柔光能量，A为源device-depth；不混入原图RGB，不改原PMA/Straight输入。
 */
Shader "Hidden/Ossastra/CharacterLighting/OuterGlowSeed"
{
 Properties { _OuterDepthBlend("Nearest blend operation",Float)=4 }
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline"}
  Cull Off ZWrite Off ZTest LEqual
  HLSLINCLUDE
  #pragma target 4.5
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  #include "Assets/OSSASTRA/Scripts/SceneVisibility/Rooms/Rendering/RoomSightClip.hlsl"
  #include "Assets/OSSASTRA/Scripts/EnvironmentSimulation/Rendering/EnvironmentLighting.hlsl"
  #include "Assets/OSSASTRA/Scripts/CharacterLighting/Rendering/CharacterLightingRim.hlsl"
  TEXTURE2D(_CharacterOuterSourceTexture);SAMPLER(sampler_CharacterOuterSourceTexture);
  TEXTURE2D(_CharacterOuterLegalDepth);SAMPLER(sampler_CharacterOuterLegalDepth);
  float4 _CharacterOuterSourceST,_CharacterOuterSourceFoot,_CharacterOuterSourceNormal;
  float4 _CharacterOuterControls,_CharacterOuterWidth;
  float _CharacterOuterSourceAlpha,_CharacterOuterSourceCutoff,_CharacterOuterSourceUpright;
  struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
  struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float alpha:TEXCOORD1;float eyeOffset:TEXCOORD2;};
  V Vert(A i){V o;float3 world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(world);o.uv=i.uv*_CharacterOuterSourceST.xy+_CharacterOuterSourceST.zw;o.alpha=i.color.a*_CharacterOuterSourceAlpha;o.eyeOffset=-TransformWorldToView(world).z+TransformWorldToView(_CharacterOuterSourceFoot.xyz).z;return o;}
  float SourceDepth(V i)
  {
   if(_CharacterOuterSourceUpright<.5)return i.positionCS.z;
   float2 uv=i.positionCS.xy/_CharacterCoverageScreenSize.xy;
   float3 nearWS=ComputeWorldSpacePosition(uv,UNITY_NEAR_CLIP_VALUE,UNITY_MATRIX_I_VP);
   float4 farWS=mul(UNITY_MATRIX_I_VP,ComputeClipSpacePosition(uv,UNITY_RAW_FAR_CLIP_VALUE));
   float3 direction=farWS.xyz-nearWS*farWS.w;float denominator=dot(direction,_CharacterOuterSourceNormal.xyz);clip(abs(denominator)-1e-8);
   float t=dot(_CharacterOuterSourceFoot.xyz-nearWS,_CharacterOuterSourceNormal.xyz)/denominator;
   float3 view=TransformWorldToView(nearWS+direction*t);view.z-=i.eyeOffset;float eye=-view.z;clip(eye-_ProjectionParams.y);clip(_ProjectionParams.z-eye);
   float4 projected=TransformWViewToHClip(view);float ndc=projected.z/projected.w;
   #if UNITY_REVERSED_Z
    return saturate(ndc);
   #else
    return saturate((ndc-UNITY_NEAR_CLIP_VALUE)/(1-UNITY_NEAR_CLIP_VALUE));
   #endif
  }
  float SourceAlpha(V i){float a=saturate(SAMPLE_TEXTURE2D(_CharacterOuterSourceTexture,sampler_CharacterOuterSourceTexture,i.uv).a*i.alpha);clip(a-max(.00001,_CharacterOuterSourceCutoff));return a;}
  float3 SourceWorld(V i,float depth){float3 world=RoomSightWorldFromDeviceDepth(i.positionCS.xy,depth);clip(RoomSightEyeContains(world)?1:-1);return world;}
  float NearestFrag(V i):SV_Target{SourceAlpha(i);float depth=SourceDepth(i);SourceWorld(i,depth);return depth;}
  float4 SeedFrag(V i):SV_Target
  {
   float alpha=SourceAlpha(i);float depth=SourceDepth(i);float3 world=SourceWorld(i,depth);
   float sceneDepth=SAMPLE_TEXTURE2D(_CharacterOuterLegalDepth,sampler_CharacterOuterLegalDepth,i.positionCS.xy*_CharacterCoverageScreenSize.zw).r;
   #if UNITY_REVERSED_Z
    if(asuint(sceneDepth)>asuint(depth)+4u)discard;
   #else
    if(asuint(depth)>asuint(sceneDepth)+4u)discard;
   #endif
   clip(_CharacterOuterControls.x-.5);clip(_EnvironmentLightingEnabled-.5);clip(_EnvironmentSurfaceRole-1.5);
   if(!EnvironmentCanShadeWorld(world))discard;
   float edgeWidth=max(.75,min(2,_CharacterOuterWidth.x*_CharacterCoverageScreenSize.y/1080));
   float3 edge=CharacterLightingRimDirectionAndBand(i.positionCS.xy,edgeWidth,1);
   if(edge.z<=0)discard;
   float3 energy=0;float len=dot(_MainLightPosition.xyz,_MainLightPosition.xyz);
   if(len>.00001)energy=_MainLightColor.rgb*CharacterLightingDirectionWeight(edge.xy,_MainLightPosition.xyz*rsqrt(len))*EnvironmentMainShadow(world)*EnvironmentDirectCloud(world);
   float3 delta=_CharacterRimLocalPosition.xyz-world;float squared=dot(delta,delta),range=max(.01,_CharacterRimLocalPosition.w);
   if(_CharacterRimLocalColor.w>.5&&squared>.00001){float falloff=saturate(1-squared/(range*range));energy+=_CharacterRimLocalColor.rgb*falloff*falloff*CharacterLightingDirectionWeight(edge.xy,delta*rsqrt(squared));}
   energy=max(0,energy)*edge.z*alpha*max(0,_CharacterOuterControls.y);
   // 两个独立角色各占一半预算；合并后不超过较高角色的maxOuterGain，强光仍保持色度。
   float peak=max(energy.r,max(energy.g,energy.b)),budget=max(0,_CharacterOuterControls.z)*.5;
   energy*=budget/max(.000001,budget+peak);if(max(energy.r,max(energy.g,energy.b))<=.000001)discard;
   return float4(energy,depth);
  }
  ENDHLSL
  Pass
  {
   Name "VisibleNaturalEdgeEnergy"
   Blend Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment SeedFrag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   ENDHLSL
  }
  Pass
  {
   Name "NearestBodyIncludingTranslucentEdge"
   Blend One One BlendOp[_OuterDepthBlend] ColorMask R
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment NearestFrag
   ENDHLSL
  }
 }
 Fallback Off
}

