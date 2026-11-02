// 功能：正向绘制的早期片元许可；人物观察点决定逻辑视域，相机只负责投影。
// 边界：有限8开口、每开口10平面、8目标盒，32建筑盒、64不透明OBB；调用侧须拒绝超限。
// 生命周期：每相机绘制显式上传，不读取旧发现状态或旧黑幕纹理。
#ifndef OSSASTRA_ROOM_SIGHT_CLIP_INCLUDED
#define OSSASTRA_ROOM_SIGHT_CLIP_INCLUDED
int _RoomSightSeedCount, _RoomSightBuildingCount, _RoomSightDomainCount, _RoomSightBlockerCount, _RoomSightExteriorView;
float3 _RoomSightObserver;
float4x4 _RoomSightSeedMatrices[8], _RoomSightBuildingMatrices[32], _RoomSightBlockerMatrices[64], _RoomSightTargetMatrices[64];
float4 _RoomSightSeedExtents[8], _RoomSightBuildingExtents[32], _RoomSightBlockerExtents[64], _RoomSightTargetExtents[64];
float4 _RoomSightPlanes[80], _RoomSightMeta[8], _RoomSightExterior[8];
int _RoomSightOwnerCount, _RoomSightOwnerExterior;
float4x4 _RoomSightOwnerMatrices[8];float4 _RoomSightOwnerExtents[8];
sampler2D _RoomSightCoverageTexture;
float _RoomPositiveCoverageMode;
float _RoomPositivePermissionMode;
float4 _RoomSightTargetSize;
float4x4 _RoomSightInverseViewProjection;
bool RoomSightBuildingContains(float3 worldPosition);
bool RoomSightSeedContains(float3 worldPosition);
float3 RoomSightWorldFromDeviceDepth(float2 rasterPixel, float rawDepth)
{
    float depth = rawDepth;
    #if !UNITY_REVERSED_Z
        depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawDepth);
    #endif
    float4 clipPosition = float4(rasterPixel / _RoomSightTargetSize.xy * 2 - 1, depth, 1);
    #if UNITY_UV_STARTS_AT_TOP
        clipPosition.y = -clipPosition.y;
    #endif
    float4 world = mul(_RoomSightInverseViewProjection, clipPosition);
    return world.xyz / world.w;
}
float4 RoomSightTruthColor(float3 world, float4 truthColor, float partial)
{
    if (_RoomPositivePermissionMode > .5)
    {
        bool portal = !RoomSightSeedContains(world) && (_RoomSightExteriorView == 0 || RoomSightBuildingContains(world));
        return float4(1, 1, portal ? 1 : 0, 1);
    }
    if (_RoomPositiveCoverageMode < .5) return truthColor;
    // 真值屋顶候选只消费合法室内可见片元；室外遮挡表面仍正常写黑色与深度。
    return partial > .5 && RoomSightBuildingContains(world) ? 1 : 0;
}

void RoomSightPreviewRoofClip(float2 rasterPixel, float cutRoof)
{
    if (cutRoof > .5 && _RoomPositiveCoverageMode < .5)
    {
        float coverage = tex2D(_RoomSightCoverageTexture, rasterPixel / _RoomSightTargetSize.xy).r;
        // 解析后接近全白才揭该像素，保守保留实际几何的混合边缘；不代签任意亚像素逻辑边界。
        clip(.998 - coverage);
    }
}

bool RoomSightInBox(float3 worldPosition, float4x4 worldToLocal, float3 halfExtents)
{ return all(abs(mul(worldToLocal, float4(worldPosition, 1)).xyz) <= halfExtents + .0002); }

bool RoomSightSeedContains(float3 worldPosition)
{
    if (_RoomSightSeedCount < 0 || _RoomSightSeedCount > 8) return false;
    [loop] for (int i = 0; i < _RoomSightSeedCount; ++i)
        if (RoomSightInBox(worldPosition, _RoomSightSeedMatrices[i], _RoomSightSeedExtents[i].xyz)) return true;
    return false;
}

bool RoomSightBuildingContains(float3 worldPosition)
{
    if (_RoomSightBuildingCount < 0 || _RoomSightBuildingCount > 32) return false;
    [loop] for (int i = 0; i < _RoomSightBuildingCount; ++i)
        if (RoomSightInBox(worldPosition, _RoomSightBuildingMatrices[i], _RoomSightBuildingExtents[i].xyz)) return true;
    return false;
}

// 有限线段相交；端点表面自身不算前景，实体内部与更早的实体仍阻挡。
bool RoomSightBlocked(float3 worldPosition)
{
    if (_RoomSightBlockerCount < 0 || _RoomSightBlockerCount > 64) return true;
    [loop] for (int i = 0; i < _RoomSightBlockerCount; ++i)
    {
        float3 origin = mul(_RoomSightBlockerMatrices[i], float4(_RoomSightObserver, 1)).xyz;
        float3 target = mul(_RoomSightBlockerMatrices[i], float4(worldPosition, 1)).xyz;
        float3 delta = target - origin, extent = _RoomSightBlockerExtents[i].xyz;
        float entry = 0, leave = 1;
        bool intersects = true;
        [unroll] for (int axis = 0; axis < 3; ++axis)
        {
            if (abs(delta[axis]) < 1e-7)
            { if (abs(origin[axis]) > extent[axis]) intersects = false; }
            else
            {
                float a = (-extent[axis] - origin[axis]) / delta[axis];
                float b = (extent[axis] - origin[axis]) / delta[axis];
                entry = max(entry, min(a, b)); leave = min(leave, max(a, b));
            }
        }
        float endpointBias = .02 / max(length(worldPosition - _RoomSightObserver), .02);
        if (intersects && leave >= entry && leave >= 0 && entry <= 1 - endpointBias) return true;
    }
    return false;
}

bool RoomSightPartialContains(float3 worldPosition)
{
    // SetInt旧API会把float位模式交给HLSL int；即便外部错误上传，也不能出现无界循环或越界放行。
    if (_RoomSightSeedCount < 0 || _RoomSightSeedCount > 8 || _RoomSightBuildingCount < 0 || _RoomSightBuildingCount > 32 ||
        _RoomSightBlockerCount < 0 || _RoomSightBlockerCount > 64 || _RoomSightDomainCount < 0 || _RoomSightDomainCount > 8 ||
        _RoomSightOwnerCount < 0 || _RoomSightOwnerCount > 8 || _RoomSightOwnerExterior < 0 || _RoomSightOwnerExterior > 1 ||
        _RoomSightExteriorView < 0 || _RoomSightExteriorView > 1) return false;
    if (_RoomSightOwnerExterior != 0 && RoomSightBuildingContains(worldPosition)) return false;
    if (_RoomSightOwnerCount > 0)
    {
        bool ownerContains = false;
        [loop] for (int i = 0; i < _RoomSightOwnerCount; ++i)
            if (RoomSightInBox(worldPosition, _RoomSightOwnerMatrices[i], _RoomSightOwnerExtents[i].xyz)) ownerContains = true;
        if (!ownerContains) return false;
    }
    if (_RoomSightExteriorView != 0 && !RoomSightBuildingContains(worldPosition)) return true;
    if (RoomSightSeedContains(worldPosition)) return true;
    bool domainContains = false;
    [loop] for (int domain = 0; domain < _RoomSightDomainCount; ++domain)
    {
        int4 meta = (int4)_RoomSightMeta[domain]; bool inside = true;
        if (meta.x < 0 || meta.y < 4 || meta.y > 10 || meta.x + meta.y > 80 ||
            meta.z < 0 || meta.w < 0 || meta.w > 8 || meta.z + meta.w > 64) continue;
        [loop] for (int p = 0; p < meta.y; ++p)
            if (dot(_RoomSightPlanes[meta.x + p], float4(worldPosition, 1)) < 0) inside = false;
        if (!inside) continue;
        if (_RoomSightExterior[domain].x > .5)
        { if (RoomSightBuildingContains(worldPosition)) continue; }
        else
        {
            bool target = false;
            [loop] for (int v = 0; v < meta.w; ++v)
                if (RoomSightInBox(worldPosition, _RoomSightTargetMatrices[meta.z + v], _RoomSightTargetExtents[meta.z + v].xyz)) target = true;
            if (!target) continue;
        }
        domainContains = true; break;
    }
    return domainContains && !RoomSightBlocked(worldPosition);
}
#endif
