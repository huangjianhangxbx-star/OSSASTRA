#ifndef OSSASTRA_MATTER_SPRITE_FIRE_CLIP
#define OSSASTRA_MATTER_SPRITE_FIRE_CLIP

uint MatterFireFlags(int2 cell)
{
    if (any(cell < 0) || any(cell >= (int2)_MatterGridSize.xy))
        return 8u;
    return (uint)round(_MatterState.Load(int3(cell, 0)).r * 255.0);
}

bool MatterFireBlocked(int2 cell)
{
    return (MatterFireFlags(cell) & 24u) != 0u;
}

// 沿真实燃点到火片片元遍历物质格。碰到实体、闸门或岩角时终止。
float MatterFireVisible(float2 source, float2 target)
{
    int2 cell = (int2)floor(source);
    int2 end = (int2)floor(target);
    uint flags = MatterFireFlags(cell);
    if ((flags & 56u) != 32u || MatterFireBlocked(end))
        return 0;

    float2 ray = target - source;
    int2 direction = int2(ray.x >= 0 ? 1 : -1, ray.y >= 0 ? 1 : -1);
    float2 edge = float2(cell) + float2(direction.x > 0, direction.y > 0);
    float2 stride = rcp(max(abs(ray), 1e-6));
    float2 next = abs(edge - source) * stride;

    // 单个批次火片高度受控，超过固定遍历预算时宁可截断。
    [loop] for (int i = 0; i < 32; i++)
    {
        if (all(cell == end))
            return 1;
        if (abs(next.x - next.y) < 1e-5)
        {
            if (MatterFireBlocked(cell + int2(direction.x, 0)) ||
                MatterFireBlocked(cell + int2(0, direction.y)))
                return 0;
            cell += direction;
            next += stride;
        }
        else if (next.x < next.y)
        {
            cell.x += direction.x;
            next.x += stride.x;
        }
        else
        {
            cell.y += direction.y;
            next.y += stride.y;
        }
        if (MatterFireBlocked(cell))
            return 0;
    }
    return 0;
}

#endif
