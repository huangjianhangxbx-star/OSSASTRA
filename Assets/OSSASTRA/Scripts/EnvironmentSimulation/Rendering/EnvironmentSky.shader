// 功能：明确许可后的程序天空；预览仅复用同一环境，不参与场景业务或推进时钟。
Shader "Hidden/Ossastra/Environment/Sky"
{
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline"}
  Cull Off ZWrite Off ZTest LEqual
  HLSLINCLUDE
  #pragma target 4.5
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  #include "../../SceneVisibility/Rooms/Rendering/RoomSightClip.hlsl"
  #include "EnvironmentLighting.hlsl"
  float4 _EnvironmentSkyZenith,_EnvironmentSkyHorizon,_EnvironmentSkySun,_EnvironmentSkySunColor;
  struct Varyings{float4 position:SV_POSITION;float2 uv:TEXCOORD0;};
  Varyings Vert(uint id:SV_VertexID){Varyings o;o.position=GetFullScreenTriangleVertexPosition(id);
  #if UNITY_REVERSED_Z
   o.position.z=0;
  #else
   o.position.z=1;
  #endif
  o.uv=GetFullScreenTriangleTexCoord(id);return o;}
  bool SkyRayBox(float3 origin,float3 direction,float4x4 worldToLocal,float3 extent)
  {
   float3 p=mul(worldToLocal,float4(origin,1)).xyz,d=mul(worldToLocal,float4(direction,0)).xyz;
   if((abs(d.x)<1e-7&&abs(p.x)>extent.x)||(abs(d.y)<1e-7&&abs(p.y)>extent.y)||(abs(d.z)<1e-7&&abs(p.z)>extent.z))return false;
   float3 safeDirection=(step(0,d)*2-1)*max(abs(d),1e-7);
   float3 a=(-extent-p)/safeDirection,b=(extent-p)/safeDirection;
   float3 nearDistance=min(a,b),farDistance=max(a,b);
   return min(10000,min(farDistance.x,min(farDistance.y,farDistance.z)))>=max(.01,max(nearDistance.x,max(nearDistance.y,nearDistance.z)));
  }
  bool SkyAllowed(float3 origin,float3 direction)
  {
   if(_RoomSightBuildingCount<0||_RoomSightBuildingCount>32||_RoomSightCameraBlockerCount<0||_RoomSightCameraBlockerCount>64||_RoomSightWindowCount<0||_RoomSightWindowCount>8)return false;
   float3 farWorld=origin+direction*10000;
   if(_RoomSightExteriorView==0)
   {
    bool aperture=false;
    [loop]for(int d=0;d<_RoomSightWindowCount;d++)
    {
     int2 meta=(int2)_RoomSightWindowMeta[d];if(meta.x<0||meta.y<4||meta.y>10||meta.x+meta.y>80)continue;bool inside=true;
     [loop]for(int p=0;p<meta.y;p++)if(dot(_RoomSightWindowPlanes[meta.x+p],float4(farWorld,1))<0)inside=false;
     aperture=aperture||inside;
    }
    if(!aperture)return false;
   }
   [loop]for(int b=0;b<_RoomSightBuildingCount;b++)
   {
    if(_RoomSightExteriorView==0&&RoomSightInBox(_RoomSightObserver,_RoomSightBuildingMatrices[b],_RoomSightBuildingExtents[b].xyz))continue;
    if(SkyRayBox(origin,direction,_RoomSightBuildingMatrices[b],_RoomSightBuildingExtents[b].xyz))return false;
   }
   [loop]for(int blocker=0;blocker<_RoomSightCameraBlockerCount;blocker++)if(SkyRayBox(origin,direction,_RoomSightCameraBlockerMatrices[blocker],_RoomSightCameraBlockerExtents[blocker].xyz))return false;
   return true;
  }
  half4 SkyColor(float3 direction)
  {
   float3 color=lerp(_EnvironmentSkyHorizon.rgb,_EnvironmentSkyZenith.rgb,pow(saturate(direction.y),.5));
   float sun=pow(saturate(dot(direction,normalize(_EnvironmentSkySun.xyz))),1800);
   float moon=pow(saturate(dot(direction,-normalize(_EnvironmentSkySun.xyz))),2600)*_EnvironmentSkySun.w;
   color+=sun*_EnvironmentSkySunColor.rgb*2+moon*float3(.42,.51,.68);
   float2 cloudPosition=_WorldSpaceCameraPos.xz+direction.xz*(180/max(direction.y,.08));
   float cloud=EnvironmentCloudDensity(cloudPosition)*smoothstep(-.03,.15,direction.y);
   color=lerp(color,lerp(float3(.65,.69,.73),float3(.04,.055,.09),_EnvironmentSkySun.w),cloud*.72);
   return half4(color,1);
  }
  half4 Frag(Varyings input):SV_Target
  {
   #if UNITY_REVERSED_Z
    float depth=0;
   #else
    float depth=1;
   #endif
   float3 direction=normalize(RoomSightWorldFromDeviceDepth(input.position.xy,depth)-_WorldSpaceCameraPos);
   clip(SkyAllowed(_WorldSpaceCameraPos,direction)?1:-1);return SkyColor(direction);
  }
  half4 Preview(Varyings input):SV_Target
  {float azimuth=(input.uv.x-.5)*6.2831853,elevation=(input.uv.y-.5)*3.1415927;return SkyColor(float3(sin(azimuth)*cos(elevation),sin(elevation),cos(azimuth)*cos(elevation)));}
  ENDHLSL
  Pass{ Name "QualifiedSky" HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   ENDHLSL
  }
  Pass{ Name "Preview" HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Preview
   ENDHLSL
  }
 }
}
