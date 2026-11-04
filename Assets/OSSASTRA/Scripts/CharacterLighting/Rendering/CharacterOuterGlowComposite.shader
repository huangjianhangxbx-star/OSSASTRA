/* 功能：只在自身完整剪影外，将当前角色受光源作有限、带许可和深度路径检测的柔化。
 * 边界：每角色独立seed；每段最多一屏幕像素，禁画岛/前墙/前景角色当帧截断，无历史/拖影。
 * 坐标：宽度=1080p等效最终像素×实际target高度/1080；RGB为预乘线性能量。pass0写独立能量层，pass1原样追加一次，不改目标alpha。
 */
Shader "Hidden/Ossastra/CharacterLighting/OuterGlowComposite"
{
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline"}
  Cull Off ZWrite Off ZTest LEqual Blend One One ColorMask RGB
  Pass
  {
   Name "DepthAndPermissionConstrainedSoftExterior"
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Assets/OSSASTRA/Scripts/SceneVisibility/Rooms/Rendering/RoomSightClip.hlsl"
   #include "CharacterLightingSurface.hlsl"
   TEXTURE2D(_CharacterOuterSeed);TEXTURE2D(_CharacterOuterLegalDepth);TEXTURE2D(_CharacterOuterPermission);
   float4 _CharacterOuterControls,_CharacterOuterWidth;
   struct V{float4 positionCS:SV_POSITION;};
   V Vert(uint id:SV_VertexID){V o;o.positionCS=GetFullScreenTriangleVertexPosition(id);return o;}
   bool Allowed(float2 pixel,float depth)
   {
    int2 p=(int2)pixel;if(any(p<0)||any(p>=(int2)_CharacterCoverageScreenSize.xy))return false;
    float4 permit=LOAD_TEXTURE2D(_CharacterOuterPermission,p);
    // 解析后必须近全许可，不把混合边缘、黑色背景与逻辑黑幕混为一谈。
    if(min(permit.r,permit.g)<.999)return false;
    float scene=LOAD_TEXTURE2D(_CharacterOuterLegalDepth,p).r;
    #if UNITY_REVERSED_Z
     if(asuint(scene)>asuint(depth)+4u)return false;
    #else
     if(asuint(depth)>asuint(scene)+4u)return false;
    #endif
    return RoomSightEyeContains(RoomSightWorldFromDeviceDepth(pixel,depth));
   }
   bool PathAllowed(int2 target,int2 source,float depth)
   {
    int2 delta=source-target,step=int2(delta.x<0?-1:1,delta.y<0?-1:1),at=target;
    int2 count=abs(delta);float2 increment=float2(count.x==0?1e6:1.0/count.x,count.y==0?1e6:1.0/count.y);
    float2 crossing=increment*.5;
    // Supercover DDA：角点同时碰到的两格都必须获准，不能斜穿单像素黑幕/前景边缘。
    [loop]for(int i=0;i<16;++i)
    {
     if(all(at==source))return true;
     if(abs(crossing.x-crossing.y)<.000001)
     {
      if(!Allowed(float2(at+int2(step.x,0))+.5,depth)||!Allowed(float2(at+int2(0,step.y))+.5,depth))return false;
      at+=step;crossing+=increment;
     }
     else if(crossing.x<crossing.y){at.x+=step.x;crossing.x+=increment.x;}
     else {at.y+=step.y;crossing.y+=increment.y;}
     if(!Allowed(float2(at)+.5,depth))return false;
    }
    return all(at==source);
   }
   float4 Frag(V i):SV_Target
   {
    if(_CharacterOuterControls.x<.5||_CharacterOuterControls.y<=0||_CharacterOuterControls.z<=0||_CharacterCoverageReady<.5)discard;
    float2 pixel=i.positionCS.xy;float2 local=(pixel*_CharacterCoverageScreenSize.zw-_CharacterCoverageRect.xy)/_CharacterCoverageRect.zw;
    float own=CharacterLightingCoverageAlpha(local);if(own>=.999)discard;
    float radius=clamp(_CharacterOuterWidth.x,1,4)*_CharacterCoverageScreenSize.y/1080;
    if(radius<=.00001||radius>8.001)discard;
    int ir=(int)ceil(radius);float3 energy=0;float normalizer=0;
    [loop]for(int y=-ir;y<=ir;++y)[loop]for(int x=-ir;x<=ir;++x)
    {
     float distancePixels=length(float2(x,y));if(distancePixels<=0||distancePixels>=radius+.001)continue;
     int2 p=(int2)pixel+int2(x,y);if(any(p<0)||any(p>=(int2)_CharacterCoverageScreenSize.xy))continue;
     float4 source=LOAD_TEXTURE2D(_CharacterOuterSeed,p);if(max(source.r,max(source.g,source.b))<=.000001)continue;
     if(!Allowed(float2(p)+.5,source.a)||!Allowed(pixel,source.a))continue;
     if(!PathAllowed((int2)pixel,p,source.a))continue;
     float weight=pow(saturate(1-distancePixels/(radius+.5)),clamp(_CharacterOuterControls.w,.25,4));
     energy+=source.rgb*weight;normalizer+=weight;
    }
    if(normalizer<=0)discard;
    energy=energy/max(1,normalizer)*(1-own);
    // 源和目标均合法，但自然轮廓不足的亚像素发丝按连续alpha衰减。
    return float4(energy,0);
   }
   ENDHLSL
  }
  Pass
  {
   Name "AppendValidatedEnergyOnce"
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex EnergyVert
   #pragma fragment EnergyFrag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_CharacterOuterAccumulated);
   struct EnergyV{float4 positionCS:SV_POSITION;};
   EnergyV EnergyVert(uint id:SV_VertexID){EnergyV output;output.positionCS=GetFullScreenTriangleVertexPosition(id);return output;}
   float4 EnergyFrag(EnergyV input):SV_Target{return float4(LOAD_TEXTURE2D(_CharacterOuterAccumulated,(int2)input.positionCS.xy).rgb,0);}
   ENDHLSL
  }
 }
 Fallback Off
}

