Shader "Ossastra/Vegetation/GrassLayer"
{
 Properties{
  _MainTex("现有透明草图",2D)="white"{}
  _Color("整体色",Color)=(1,1,1,1)
  _RootTint("根部色组",Color)=(.78,.83,.70,1)
  _TipTint("尖部色组",Color)=(1.03,1.05,.91,1)
  _Cutoff("透明裁剪",Range(0,1))=.35
  _GrassWind("风向强度/环境秒/最大位移",Vector)=(0,0,0,.14)
  _GrassCameraLod("相机XZ/密度距离",Vector)=(0,0,10000,10001)
  [HideInInspector]_EnvironmentResponseOverride("同环境受光",Float)=1
  [HideInInspector]_EnvironmentReceiveShadow("接收真实阴影",Float)=1
  [HideInInspector]_EnvironmentMinimumOverride("不自发光",Float)=0
 }
 SubShader{
 Tags{"RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout"}
 Cull Off ZWrite On Blend Off
 HLSLINCLUDE
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
 CBUFFER_START(UnityPerMaterial)
 float4 _MainTex_ST,_Color,_RootTint,_TipTint,_GrassWind,_GrassCameraLod;
 float _Cutoff;
 CBUFFER_END
 #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
 #include "GrassWind.hlsl"
 struct GrassInput{float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;float4 root:TEXCOORD1;float4 color:COLOR;};
 struct GrassVarying{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;float3 normal:TEXCOORD2;float weight:TEXCOORD3;float3 root:TEXCOORD4;float phase:TEXCOORD5;};
 GrassVarying GrassVertex(GrassInput v){GrassVarying o;o.world=GrassDeform(TransformObjectToWorld(v.positionOS.xyz),TransformObjectToWorld(v.root.xyz),v.root.w,_GrassWind);o.positionCS=TransformWorldToHClip(o.world);o.uv=v.uv;o.normal=TransformObjectToWorldNormal(v.normalOS);o.weight=v.root.w;o.root=TransformObjectToWorld(v.root.xyz);o.phase=v.color.r;return o;}
 half4 GrassColor(GrassVarying i){float fade=saturate((distance(i.root.xz,_GrassCameraLod.xy)-_GrassCameraLod.z)/max(.01,_GrassCameraLod.w-_GrassCameraLod.z));clip(i.phase<=.5?1:1-fade-(i.phase-.5)*2);half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);clip(c.a-_Cutoff);c.rgb*=_Color.rgb*lerp(_RootTint.rgb,_TipTint.rgb,i.weight);return c;}
 half4 GrassDepth(GrassVarying i):SV_Target{GrassColor(i);return 0;}
 ENDHLSL
 Pass{
  Name "ForwardLit" Tags{"LightMode"="UniversalForward"}
  HLSLPROGRAM
  #pragma target 3.5
  #pragma vertex GrassVertex
  #pragma fragment GrassFragment
  #pragma shader_feature_local _GRASS_WIND
  #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
  #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
  #include "Assets/OSSASTRA/Scripts/EnvironmentSimulation/Rendering/EnvironmentLighting.hlsl"
  half4 GrassFragment(GrassVarying i,bool front:SV_IsFrontFace):SV_Target{half4 c=GrassColor(i);float3 normal=normalize(lerp((front?1:-1)*normalize(i.normal),float3(0,1,0),.72));return half4(EnvironmentShade(c.rgb,i.world,normal,1),1);}
  ENDHLSL
 }
 Pass{
  Name "DepthOnly" Tags{"LightMode"="DepthOnly"} ColorMask 0
  HLSLPROGRAM
  #pragma target 3.5
  #pragma vertex GrassVertex
  #pragma fragment GrassDepth
  #pragma shader_feature_local _GRASS_WIND
  ENDHLSL
 }
 Pass{
  Name "DepthNormals" Tags{"LightMode"="DepthNormals"}
  HLSLPROGRAM
  #pragma target 3.5
  #pragma vertex GrassVertex
  #pragma fragment GrassNormals
  #pragma shader_feature_local _GRASS_WIND
  half4 GrassNormals(GrassVarying i,bool front:SV_IsFrontFace):SV_Target{GrassColor(i);return half4(normalize(i.normal)*(front?1:-1),0);}
  ENDHLSL
 }
 Pass{
  Name "ShadowCaster" Tags{"LightMode"="ShadowCaster"} ColorMask 0
  HLSLPROGRAM
  #pragma target 3.5
  #pragma vertex GrassShadow
  #pragma fragment GrassDepth
  #pragma shader_feature_local _GRASS_WIND
  #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
  float3 _LightDirection,_LightPosition;
  GrassVarying GrassShadow(GrassInput v){GrassVarying o=GrassVertex(v);float3 lightDirection=_LightDirection;
  #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
   lightDirection=normalize(_LightPosition-o.world);
  #endif
   o.positionCS=TransformWorldToHClip(ApplyShadowBias(o.world,o.normal,lightDirection));
  #if UNITY_REVERSED_Z
   o.positionCS.z=min(o.positionCS.z,UNITY_NEAR_CLIP_VALUE*o.positionCS.w);
  #else
   o.positionCS.z=max(o.positionCS.z,UNITY_NEAR_CLIP_VALUE*o.positionCS.w);
  #endif
   return o;}
  ENDHLSL
 }
 }
 Fallback Off
}
