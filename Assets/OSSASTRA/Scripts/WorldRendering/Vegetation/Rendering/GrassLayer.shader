Shader "Ossastra/Vegetation/GrassLayer"
{
 Properties{
  _MainTex("现有透明草图",2D)="white"{}
  _Color("整体色",Color)=(1,1,1,1)
  _RootTint("Legacy根部色组",Color)=(.78,.83,.70,1)
  _TipTint("Legacy尖部色组",Color)=(1.03,1.05,.91,1)
  _Cutoff("透明裁剪",Range(0,1))=.35
  _GrassWind("风向强度/环境秒/最大位移",Vector)=(0,0,0,.14)
  _GrassCameraLod("相机XZ/密度距离；不等于配色距离",Vector)=(0,0,10000,10001)
  [Enum(LegacyA,0,TutorialArt,1,ProjectArt,2)]_ArtMode("颜色路线",Float)=0
  [Enum(Legacy,0,Fine,1,Coarse,2,Combined,3,CombinedColor,4)]_WindMode("运动路线",Float)=0
  _RGBDetail("源RGB细节；0仅保留Alpha",Range(0,1))=0
  _NearColor("Depth近色",Color)=(.5,.7,.15,1)
  _FarColor("Depth远色",Color)=(.15,.35,.08,1)
  _NearFarRange("Depth观察距离；不改变密度",Vector)=(3.1,17.79,0,0)
  _DepthColorStrength("Depth加色权重",Range(0,1))=.25
  _HeightBlend("逐簇归一化高度混合",Range(.01,1))=.85
  _BottomColor("BottomColor/无地色回退",Color)=(.2,.35,.08,1)
  _ShadowColor("Tutorial手工阴影色组",Color)=(.42,.52,.44,1)
  [Toggle]_UseTerrainColor("Shadow-or-Bottom使用地色",Float)=1
  _TerrainColor("对应表面未受光线性颜色图",2D)="white"{}
  _TerrainRect("地色世界XZ起点/覆盖宽高",Vector)=(0,0,1,1)
  [HideInInspector]_TerrainColorValid("表面映射已核验",Float)=0
  _WindTexture("平铺粗风信号",2D)="gray"{}
  _WindNoiseScale("粗风世界覆盖尺寸",Vector)=(6,9,0,0)
  _WindNoiseSpeed("固定16秒滚动跨度",Float)=1
  _WindNoiseContrast("粗风Smoothstep区间",Vector)=(.2,.8,0,0)
  _WindTextureLod("顶点采样LOD",Range(0,6))=0
  _FineWindStrength("细摆形态权重",Range(0,2))=.35
  _CoarseWindStrength("粗风形态权重",Range(0,2))=.8
  _WindColorStrength("形色关联权重",Range(0,1))=.25
  [HideInInspector]_WindDisplacement("诊断位移开关；正常1",Float)=1
  [Enum(Normal,0,Height,1,Terrain,2,Shadow,3,Depth,4,Coarse,5)]_ArtDebug("分项显色；正常0",Float)=0
  [HideInInspector]_RoomPositiveDepthWrite("Room truth depth write",Float)=1
  [HideInInspector]_RoomPositivePartial("Room partial permission",Float)=0
  [HideInInspector]_RoomPositiveCutRoof("Room roof preview",Float)=0
  [HideInInspector]_RoomPositiveColor("Room truth color",Color)=(0,1,1,1)
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
 TEXTURE2D(_TerrainColor);SAMPLER(sampler_TerrainColor);
 TEXTURE2D(_WindTexture);SAMPLER(sampler_WindTexture);
 CBUFFER_START(UnityPerMaterial)
 float4 _MainTex_ST,_Color,_RootTint,_TipTint,_GrassWind,_GrassCameraLod;
 float4 _NearColor,_FarColor,_NearFarRange,_BottomColor,_ShadowColor,_TerrainRect,_WindNoiseScale,_WindNoiseContrast;
 float _Cutoff,_ArtMode,_WindMode,_RGBDetail,_DepthColorStrength,_HeightBlend,_UseTerrainColor,_TerrainColorValid;
 float _WindNoiseSpeed,_WindTextureLod,_FineWindStrength,_CoarseWindStrength,_WindColorStrength,_WindDisplacement,_ArtDebug;
 CBUFFER_END
 #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
 #define OSSASTRA_GRASS_VIDEO_INPUTS 1
 #include "GrassWind.hlsl"
 #include "GrassArtColor.hlsl"
 struct GrassInput{float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;float4 root:TEXCOORD1;float4 color:COLOR;};
 struct GrassVarying{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;float3 normal:TEXCOORD2;float weight:TEXCOORD3;float3 root:TEXCOORD4;float phase:TEXCOORD5;float coarse:TEXCOORD6;};
 GrassVarying GrassVertex(GrassInput v){GrassVarying o=(GrassVarying)0;float coarse=.5;o.root=TransformObjectToWorld(v.root.xyz);o.world=GrassDeformWithSignal(TransformObjectToWorld(v.positionOS.xyz),o.root,v.root.w,_GrassWind,coarse);o.coarse=coarse;o.positionCS=TransformWorldToHClip(o.world);o.uv=v.uv;o.normal=TransformObjectToWorldNormal(v.normalOS);o.weight=v.root.w;o.phase=v.color.r;return o;}
 half4 GrassColor(GrassVarying i){float fade=saturate((distance(i.root.xz,_GrassCameraLod.xy)-_GrassCameraLod.z)/max(.01,_GrassCameraLod.w-_GrassCameraLod.z));clip(i.phase<=.5?1:1-fade-(i.phase-.5)*2);half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);clip(c.a-_Cutoff);if(_ArtMode<.5)c.rgb*=_Color.rgb*lerp(_RootTint.rgb,_TipTint.rgb,i.weight);return c;}
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
  half4 GrassFragment(GrassVarying i,bool front:SV_IsFrontFace):SV_Target
  {
   half4 source=GrassColor(i);float3 normal=normalize(lerp((front?1:-1)*normalize(i.normal),float3(0,1,0),.72));
   if(_ArtMode<.5)return half4(EnvironmentShade(source.rgb,i.world,normal,1),1);
   float4 terrain=GrassTerrainSample(i.root);float viewDistance=length(GetWorldSpaceViewDir(i.world));
   if(_ArtDebug>.5&&_ArtDebug<1.5)return half4(GrassHeightBlend(i.weight).xxx,1);
   if(_ArtDebug>1.5&&_ArtDebug<2.5)return half4(terrain.a>.5?terrain.rgb:float3(1,0,1),1);
   if(_ArtDebug>3.5&&_ArtDebug<4.5)return half4(GrassNearFactor(viewDistance).xxx,1);
   if(_ArtDebug>4.5)return half4(i.coarse.xxx,1);
   if(_ArtMode<1.5||(_ArtDebug>2.5&&_ArtDebug<3.5))
   {
    float shadow=_EnvironmentReceiveShadow<.5?1:EnvironmentMainShadow(i.world);
    if(_ArtDebug>2.5&&_ArtDebug<3.5)return half4(shadow.xxx,1);
    return half4(GrassVideoArt(source.rgb,terrain.rgb,i.weight,viewDistance,shadow,i.coarse,_GrassWind),1);
   }
   // Terrain texture is UNLIT albedo. Do not bake a manual shadow into it and then multiply main shadow again.
   float3 designed=GrassVideoArt(source.rgb,terrain.rgb,i.weight,viewDistance,1,i.coarse,_GrassWind);
   return half4(EnvironmentShade(designed,i.world,normal,1),1);
  }
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
 // Appended Room passes reuse the original deformed vertices, normalized height,
 // per-clump LOD clip and source-alpha clip. Legacy pass indices 0..3 stay intact.
 Pass{
  Name "RoomSight"
  HLSLPROGRAM
  #pragma target 4.5
  #pragma vertex GrassVertex
  #pragma fragment RoomGrassFragment
  #pragma shader_feature_local _GRASS_WIND
  #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
  #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
  #include "Assets/OSSASTRA/Scripts/SceneVisibility/Rooms/Rendering/RoomSightClip.hlsl"
  #include "Assets/OSSASTRA/Scripts/EnvironmentSimulation/Rendering/EnvironmentLighting.hlsl"
  half4 RoomGrassSurface(GrassVarying i,bool front,half4 source)
  {
   float3 normal=normalize(lerp((front?1:-1)*normalize(i.normal),float3(0,1,0),.72));
   bool shade=EnvironmentCanShadeWorld(i.world);
   if(_ArtMode<.5)return half4(shade?EnvironmentShade(source.rgb,i.world,normal,1):source.rgb,1);
   float4 terrain=GrassTerrainSample(i.root);float viewDistance=length(GetWorldSpaceViewDir(i.world));
   if(_ArtDebug>.5&&_ArtDebug<1.5)return half4(GrassHeightBlend(i.weight).xxx,1);
   if(_ArtDebug>1.5&&_ArtDebug<2.5)return half4(terrain.a>.5?terrain.rgb:float3(1,0,1),1);
   if(_ArtDebug>3.5&&_ArtDebug<4.5)return half4(GrassNearFactor(viewDistance).xxx,1);
   if(_ArtDebug>4.5)return half4(i.coarse.xxx,1);
   if(_ArtMode<1.5||(_ArtDebug>2.5&&_ArtDebug<3.5))
   {
    float shadow=_EnvironmentReceiveShadow<.5||!shade?1:EnvironmentMainShadow(i.world);
    if(_ArtDebug>2.5&&_ArtDebug<3.5)return half4(shadow.xxx,1);
    return half4(GrassVideoArt(source.rgb,terrain.rgb,i.weight,viewDistance,shadow,i.coarse,_GrassWind),1);
   }
   float3 designed=GrassVideoArt(source.rgb,terrain.rgb,i.weight,viewDistance,1,i.coarse,_GrassWind);
   return half4(shade?EnvironmentShade(designed,i.world,normal,1):designed,1);
  }
  half4 RoomGrassFragment(GrassVarying i,bool front:SV_IsFrontFace):SV_Target
  {
   // Most card pixels are empty. Reject alpha/LOD before the more costly room domains and fog.
   half4 source=GrassColor(i);
   float3 world=RoomSightWorldFromDeviceDepth(i.positionCS.xy,i.positionCS.z);
   if(_EnvironmentFullSurface<.5)clip(RoomSightPartialContains(world)?1:-1);
   return EnvironmentFogColor(RoomGrassSurface(i,front,source),world,0);
  }
  ENDHLSL
 }
 Pass{
  Name "RoomTruth"
  ZWrite[_RoomPositiveDepthWrite]
  HLSLPROGRAM
  #pragma target 4.5
  #pragma vertex GrassVertex
  #pragma fragment RoomGrassTruth
  #pragma shader_feature_local _GRASS_WIND
  #include "Assets/OSSASTRA/Scripts/SceneVisibility/Rooms/Rendering/RoomSightClip.hlsl"
  float _RoomPositivePartial,_RoomPositiveCutRoof;float4 _RoomPositiveColor;
  half4 RoomGrassTruth(GrassVarying i):SV_Target
  {
   GrassColor(i);
   float3 world=RoomSightWorldFromDeviceDepth(i.positionCS.xy,i.positionCS.z);
   if(_RoomPositivePartial>.5)clip(RoomSightPartialContains(world)?1:-1);
   RoomSightPreviewRoofClip(i.positionCS.xy,_RoomPositiveCutRoof);
   return RoomSightTruthColor(world,_RoomPositiveColor,_RoomPositivePartial);
  }
  ENDHLSL
 }

 }
 Fallback Off
}
