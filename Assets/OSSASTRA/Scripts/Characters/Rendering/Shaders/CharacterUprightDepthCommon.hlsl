// 角色显示专用深度：屏幕XY不动；眼深度来自脚点竖直面和实际原顶点eye残差。
// 必须在各Shader的UnityPerMaterial声明_VisualDepthFoot/_VisualDepthNormal/_VisualDepthEnabled后include。
#ifndef OSSASTRA_CHARACTER_UPRIGHT_DEPTH_INCLUDED
#define OSSASTRA_CHARACTER_UPRIGHT_DEPTH_INCLUDED
float CharacterVisualEyeOffset(float3 world)
{
 return -TransformWorldToView(world).z+TransformWorldToView(_VisualDepthFoot.xyz).z;
}
float CharacterVisualDeviceDepth(float4 rasterPosition,float eyeOffset)
{
 // SceneView、observer和未Prepare实例保持原device depth。没有ZTestAlways，也不改变队列。
 if(_VisualDepthEnabled<.5)return rasterPosition.z;
 float2 uv=rasterPosition.xy/_ScaledScreenParams.xy;
 float3 nearWS=ComputeWorldSpacePosition(uv,UNITY_NEAR_CLIP_VALUE,UNITY_MATRIX_I_VP);
 float4 farWSH=mul(UNITY_MATRIX_I_VP,ComputeClipSpacePosition(uv,UNITY_RAW_FAR_CLIP_VALUE));
 float3 direction=farWSH.xyz-nearWS*farWSH.w;
 float denominator=dot(direction,_VisualDepthNormal.xyz);clip(abs(denominator)-1e-8);
 float t=dot(_VisualDepthFoot.xyz-nearWS,_VisualDepthNormal.xyz)/denominator;
 float3 view=TransformWorldToView(nearWS+direction*t);view.z-=eyeOffset;
 float eye=-view.z;clip(eye-_ProjectionParams.y);clip(_ProjectionParams.z-eye);
 float4 projected=TransformWViewToHClip(view);float ndc=projected.z/projected.w;
 #if UNITY_REVERSED_Z
  return saturate(ndc);
 #else
  return saturate((ndc-UNITY_NEAR_CLIP_VALUE)/(1.0-UNITY_NEAR_CLIP_VALUE));
 #endif
}
#endif
