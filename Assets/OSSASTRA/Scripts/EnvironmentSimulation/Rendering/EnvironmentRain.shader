// 功能：自有有界程序雨；当前许可及真实深度裁剪，建筑足迹与明确雨棚下不下雨。
Shader "Hidden/Ossastra/Environment/Rain"
{
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline"}
  HLSLINCLUDE
   #pragma target 4.5
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "../../SceneVisibility/Rooms/Rendering/RoomSightClip.hlsl"
   float4 _EnvironmentRain,_EnvironmentRainWind,_EnvironmentRainColor;
   TEXTURE2D(_EnvironmentRainLegalDepth);SAMPLER(sampler_EnvironmentRainLegalDepth);
   float _EnvironmentRainDepthReady;
   TEXTURE2D(_EnvironmentSideViewRainPermission);
   TEXTURE2D_FLOAT(_EnvironmentSideViewRainDepth);
   float4 _EnvironmentSideViewRainRaster;
   float4x4 _EnvironmentSideViewInverseViewProjection;
   float3 _EnvironmentSideViewRainObserver;
   int _EnvironmentSideViewRainZoneCount;
   float4x4 _EnvironmentSideViewRainZoneMatrices[8];
   float4 _EnvironmentSideViewRainZoneExtents[8];
   int _EnvironmentShelterCount;
   float4x4 _EnvironmentShelterMatrices[16];float4 _EnvironmentShelterExtents[16];
   struct Varyings{float4 position:SV_POSITION;float3 world:TEXCOORD0;float2 uv:TEXCOORD1;float enabled:TEXCOORD2;};
   float Hash(float n){return frac(sin(n*127.1+311.7)*43758.5453);}
   Varyings BuildRainVertex(uint vertex,uint instance,float3 observer)
   {
    float2 corners[6]={float2(-1,0),float2(1,0),float2(1,1),float2(-1,0),float2(1,1),float2(-1,1)};
    float seed=instance+1;float fall=frac(Hash(seed+32)-_EnvironmentRain.z*.75);
    float3 center=observer+float3((Hash(seed)-.5)*30,fall*9-1.4,(Hash(seed+17)-.5)*30);
    center.xz+=_EnvironmentRainWind.xy*fall;
    float3 right=normalize(UNITY_MATRIX_I_V._m00_m10_m20);
    float2 uv=corners[vertex];float3 world=center+right*uv.x*.012+float3(_EnvironmentRainWind.x*.02,.36,_EnvironmentRainWind.y*.02)*uv.y;
    Varyings o;o.world=world;o.position=TransformWorldToHClip(world);o.uv=float2(uv.x,uv.y);o.enabled=Hash(seed+77)<_EnvironmentRain.x?1:0;return o;
   }
   Varyings Vert(uint vertex:SV_VertexID,uint instance:SV_InstanceID)
   { return BuildRainVertex(vertex,instance,_RoomSightObserver); }
   Varyings SideViewVert(uint vertex:SV_VertexID,uint instance:SV_InstanceID)
   { return BuildRainVertex(vertex,instance,_EnvironmentSideViewRainObserver); }
   bool RainSheltered(float3 dropletWorld,float3 surfaceWorld)
   {
    if(_EnvironmentShelterCount<0||_EnvironmentShelterCount>16)return true;
    [loop]for(int shelter=0;shelter<_EnvironmentShelterCount;shelter++)
    {
     float3 local=mul(_EnvironmentShelterMatrices[shelter],float4(dropletWorld,1)).xyz;
     float3 below=mul(_EnvironmentShelterMatrices[shelter],float4(surfaceWorld,1)).xyz;
     if((all(abs(local.xz)<=_EnvironmentShelterExtents[shelter].xz)&&local.y<=_EnvironmentShelterExtents[shelter].y)||(all(abs(below.xz)<=_EnvironmentShelterExtents[shelter].xz)&&below.y<=_EnvironmentShelterExtents[shelter].y+.02))return true;
    }
    return false;
   }
   half4 RainShading(Varyings input)
   {
    float alpha=(1-abs(input.uv.x))*sin(input.uv.y*3.1415927)*_EnvironmentRain.y;
    return half4(_EnvironmentRainColor.rgb,alpha);
   }
   half4 Frag(Varyings input):SV_Target
   {
    clip(input.enabled-.5);
    clip(_EnvironmentRainDepthReady-.5);
    if(!RoomSightEyeContains(input.world)&&!RoomSightWindowContains(input.world))discard;
    // 粒子即使在室外，也不能沿被隐藏的前墙投影到室内地面上；只消费刚完成的合法世界深度。
    float rawDepth=SAMPLE_TEXTURE2D(_EnvironmentRainLegalDepth,sampler_EnvironmentRainLegalDepth,input.position.xy/_RoomSightTargetSize.xy).r;
    #if UNITY_REVERSED_Z
     if(rawDepth<=.000001)discard;
    #else
     if(rawDepth>=.999999)discard;
    #endif
    float3 surfaceWorld=RoomSightWorldFromDeviceDepth(input.position.xy,rawDepth);
    if(!RoomSightEyeContains(surfaceWorld)&&!RoomSightWindowContains(surfaceWorld))discard;
    if(RoomSightBuildingContains(surfaceWorld))discard;
    // 降雨源永远不在建筑室内；揭顶只是颜色隐藏，不能变成室内淋雨。
    [loop]for(int b=0;b<_RoomSightBuildingCount;b++)
    {float3 local=mul(_RoomSightBuildingMatrices[b],float4(input.world,1)).xyz;if(all(abs(local.xz)<=_RoomSightBuildingExtents[b].xz))discard;}
    if(RainSheltered(input.world,surfaceWorld))discard;
    return RainShading(input);
   }
   bool SideViewOutdoorPair(float3 dropletWorld,float3 surfaceWorld)
   {
    if(_EnvironmentSideViewRainZoneCount<1||_EnvironmentSideViewRainZoneCount>8)return false;
    [loop]for(int zone=0;zone<_EnvironmentSideViewRainZoneCount;zone++)
    {
     float3 drop=mul(_EnvironmentSideViewRainZoneMatrices[zone],float4(dropletWorld,1)).xyz;
     float3 receiver=mul(_EnvironmentSideViewRainZoneMatrices[zone],float4(surfaceWorld,1)).xyz;
     float3 extent=_EnvironmentSideViewRainZoneExtents[zone].xyz;
     if(all(abs(drop)<=extent)&&all(abs(receiver)<=extent))return true;
    }
    return false;
   }
   half4 SideViewFrag(Varyings input):SV_Target
   {
    clip(input.enabled-.5);
    float2 uv=input.position.xy*_EnvironmentSideViewRainRaster.xy;
    float2 attachmentUv=float2(uv.x,uv.y*_EnvironmentSideViewRainRaster.z+_EnvironmentSideViewRainRaster.w);
    int2 size=(int2)round(rcp(_EnvironmentSideViewRainRaster.xy));
    int2 pixel=clamp((int2)(attachmentUv*size),0,size-1);
    float4 permission=LOAD_TEXTURE2D(_EnvironmentSideViewRainPermission,pixel);
    if(min(permission.r,min(permission.g,permission.a))<=.998)discard;
    float rawDepth=LOAD_TEXTURE2D(_EnvironmentSideViewRainDepth,pixel).r;
    #if UNITY_REVERSED_Z
     if(rawDepth<=.000001||input.position.z+.000001<rawDepth)discard;
     float deviceDepth=rawDepth;
    #else
     if(rawDepth>=.999999||input.position.z-.000001>rawDepth)discard;
     float deviceDepth=lerp(UNITY_NEAR_CLIP_VALUE,1,rawDepth);
    #endif
    float3 surfaceWorld=ComputeWorldSpacePosition(uv,deviceDepth,_EnvironmentSideViewInverseViewProjection);
    if(!SideViewOutdoorPair(input.world,surfaceWorld)||RainSheltered(input.world,surfaceWorld))discard;
    return RainShading(input);
   }
  ENDHLSL
  Pass
  {
   Name "QualifiedRain"
   Blend SrcAlpha OneMinusSrcAlpha Cull Off ZWrite Off ZTest LEqual
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   ENDHLSL
  }
  Pass
  {
   Name "SideViewRain"
   Blend SrcAlpha OneMinusSrcAlpha Cull Off ZWrite Off ZTest Always
   HLSLPROGRAM
   #pragma vertex SideViewVert
   #pragma fragment SideViewFrag
   ENDHLSL
  }
 }
}
