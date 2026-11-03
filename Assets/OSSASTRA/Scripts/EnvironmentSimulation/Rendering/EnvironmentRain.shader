// 功能：自有有界程序雨；当前许可及真实深度裁剪，建筑足迹与明确雨棚下不下雨。
Shader "Hidden/Ossastra/Environment/Rain"
{
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline"}
  Pass
  {
   Blend SrcAlpha OneMinusSrcAlpha Cull Off ZWrite Off ZTest LEqual
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "../../SceneVisibility/Rooms/Rendering/RoomSightClip.hlsl"
   float4 _EnvironmentRain,_EnvironmentRainWind,_EnvironmentRainColor;
   TEXTURE2D(_EnvironmentRainLegalDepth);SAMPLER(sampler_EnvironmentRainLegalDepth);
   float _EnvironmentRainDepthReady;
   int _EnvironmentShelterCount;
   float4x4 _EnvironmentShelterMatrices[16];float4 _EnvironmentShelterExtents[16];
   struct Varyings{float4 position:SV_POSITION;float3 world:TEXCOORD0;float2 uv:TEXCOORD1;float enabled:TEXCOORD2;};
   float Hash(float n){return frac(sin(n*127.1+311.7)*43758.5453);}
   Varyings Vert(uint vertex:SV_VertexID,uint instance:SV_InstanceID)
   {
    float2 corners[6]={float2(-1,0),float2(1,0),float2(1,1),float2(-1,0),float2(1,1),float2(-1,1)};
    float seed=instance+1;float fall=frac(Hash(seed+32)-_EnvironmentRain.z*.75);
    float3 center=_RoomSightObserver+float3((Hash(seed)-.5)*30,fall*9-1.4,(Hash(seed+17)-.5)*30);
    center.xz+=_EnvironmentRainWind.xy*fall;
    float3 right=normalize(UNITY_MATRIX_I_V._m00_m10_m20);
    float2 uv=corners[vertex];float3 world=center+right*uv.x*.012+float3(_EnvironmentRainWind.x*.02,.36,_EnvironmentRainWind.y*.02)*uv.y;
    Varyings o;o.world=world;o.position=TransformWorldToHClip(world);o.uv=float2(uv.x,uv.y);o.enabled=Hash(seed+77)<_EnvironmentRain.x?1:0;return o;
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
    if(_EnvironmentShelterCount<0||_EnvironmentShelterCount>16)discard;
    [loop]for(int shelter=0;shelter<_EnvironmentShelterCount;shelter++)
    {
     float3 local=mul(_EnvironmentShelterMatrices[shelter],float4(input.world,1)).xyz;
     float3 below=mul(_EnvironmentShelterMatrices[shelter],float4(surfaceWorld,1)).xyz;
     if((all(abs(local.xz)<=_EnvironmentShelterExtents[shelter].xz)&&local.y<=_EnvironmentShelterExtents[shelter].y)||(all(abs(below.xz)<=_EnvironmentShelterExtents[shelter].xz)&&below.y<=_EnvironmentShelterExtents[shelter].y+.02))discard;
    }
    float alpha=(1-abs(input.uv.x))*sin(input.uv.y*3.1415927)*_EnvironmentRain.y;
    return half4(_EnvironmentRainColor.rgb,alpha);
   }
   ENDHLSL
  }
 }
}
