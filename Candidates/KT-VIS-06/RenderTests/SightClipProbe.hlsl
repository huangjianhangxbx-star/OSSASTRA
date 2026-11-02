// 使用本机Windows SDK fxc做DX11实际语法/资源绑定编译；不是实景像素验收。
#define UNITY_REVERSED_Z 1
#define UNITY_UV_STARTS_AT_TOP 1
#define UNITY_NEAR_CLIP_VALUE 1
#include "../../../Assets/OSSASTRA/Scripts/SceneVisibility/Rooms/Rendering/RoomSightClip.hlsl"
float4 main(float4 pixel:SV_Position,float3 world:TEXCOORD0):SV_Target
{
 clip(RoomSightPartialContains(world)?1:-1);
 return RoomSightFogColor(float4(1,.5,.25,1),world,0);
}
