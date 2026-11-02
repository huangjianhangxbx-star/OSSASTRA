// 显式活动空间的有限并集；仅供本相机mask绘制，不代替作者房间身份解析。
#ifndef OSSASTRA_ROOM_MASK_VOLUME_INCLUDED
#define OSSASTRA_ROOM_MASK_VOLUME_INCLUDED
float4x4 _RoomWorldToLocal[8];
float4 _RoomHalfExtents[8];
int _RoomVolumeCount;
float4x4 _RoomKnownWorldToLocal[8];
float4 _RoomKnownHalfExtents[8];
int _RoomKnownVolumeCount;
bool RoomMaskContains(float3 world)
{
    [loop] for (int i = 0; i < min(_RoomVolumeCount, 8); ++i)
    {
        float3 local = mul(_RoomWorldToLocal[i], float4(world, 1)).xyz;
        if (all(abs(local) <= _RoomHalfExtents[i].xyz + 0.00001)) return true;
    }
    return false;
}
bool RoomMaskContainsKnown(float3 world)
{
    [loop] for (int i = 0; i < min(_RoomKnownVolumeCount, 8); ++i)
    {
        float3 local = mul(_RoomKnownWorldToLocal[i], float4(world, 1)).xyz;
        if (all(abs(local) <= _RoomKnownHalfExtents[i].xyz + 0.00001)) return true;
    }
    return false;
}
#endif
