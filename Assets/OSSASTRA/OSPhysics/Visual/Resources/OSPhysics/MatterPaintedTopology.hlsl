#ifndef OSSASTRA_MATTER_PAINTED_TOPOLOGY_INCLUDED
#define OSSASTRA_MATTER_PAINTED_TOPOLOGY_INCLUDED

Texture2D<float4> _MatterState;
float4 _MatterGridSize;

// 状态来自 CPU 快照。整数载入先还原分类，连续遮罩只在同类范围内计算。
struct PaintedCell
{
    uint kind;
    uint fixedRock;
    uint barrier;
    uint burning;
    float amount;
    float heat;
    float smoke;
};

PaintedCell ReadMatter(int2 coordinates)
{
    PaintedCell cell = (PaintedCell)0;
    if (any(coordinates < 0) || any(coordinates >= (int2)_MatterGridSize.xy))
    {
        cell.fixedRock = 1;
        return cell;
    }

    float4 data = _MatterState.Load(int3(coordinates, 0));
    uint flags = (uint)round(data.r * 255);
    cell.kind = flags & 7;
    cell.fixedRock = (flags >> 3) & 1;
    cell.barrier = (flags >> 4) & 1;
    cell.burning = (flags >> 5) & 1;
    cell.amount = data.g;
    cell.heat = data.b;
    cell.smoke = data.a;
    return cell;
}

bool IsBlocked(PaintedCell cell)
{
    return (cell.fixedRock | cell.barrier) != 0;
}

bool IsLiquid(PaintedCell cell)
{
    return !IsBlocked(cell) && cell.kind >= 1 && cell.kind <= 2 && cell.amount > 0;
}

bool SameLiquid(PaintedCell a, PaintedCell b)
{
    return a.kind == b.kind && IsLiquid(b);
}

struct LiquidTrace
{
    float coverage;
    float airSurface;
    float oilBoundary;
    float surfaceDepth;
    float falling;
    float slope;
    float2 offset;
};

LiquidTrace DescribeLiquid(PaintedCell cell, float2 gridPosition, float antialias, float wave)
{
    LiquidTrace trace = (LiquidTrace)0;
    if (!IsLiquid(cell)) return trace;

    int2 coordinates = (int2)floor(gridPosition);
    float2 local = frac(gridPosition);
    PaintedCell above = ReadMatter(coordinates + int2(0, 1));
    PaintedCell below = ReadMatter(coordinates + int2(0, -1));
    PaintedCell left = ReadMatter(coordinates + int2(-1, 0));
    PaintedCell right = ReadMatter(coordinates + int2(1, 0));
    bool connectedLeft = SameLiquid(cell, left);
    bool connectedRight = SameLiquid(cell, right);
    bool connectedAbove = SameLiquid(cell, above);
    bool connectedBelow = SameLiquid(cell, below);
    bool liquidAbove = IsLiquid(above);
    bool separatedFromAir = liquidAbove || IsBlocked(above) || above.kind != 0;

    float centerHeight = max(.025, cell.amount);
    float leftHeight = connectedLeft ? max(.025, left.amount) : centerHeight;
    float rightHeight = connectedRight ? max(.025, right.amount) : centerHeight;
    trace.slope = rightHeight - leftHeight;
    // 两个相邻格共享交接处高度。每格只写自身的覆盖范围。
    float edgeHeight = local.x < .5 ? leftHeight : rightHeight;
    float height = lerp(centerHeight, edgeHeight, .5 * abs(local.x * 2 - 1));
    height = liquidAbove ? 1 : saturate(height + (separatedFromAir ? 0 : wave));
    trace.surfaceDepth = height - local.y;
    trace.offset = local - .5;
    bool separatedHorizontal = !connectedLeft && !connectedRight;
    bool openBelow = !IsBlocked(below) && below.kind == 0;
    bool verticalChain = connectedAbove || connectedBelow;

    // 下落列保留窄流与液滴。采样永远不画进邻接空格或另一种液体。
    if (separatedHorizontal && (openBelow || (connectedAbove && connectedBelow)))
    {
        float halfWidth = min(.48, .12 + sqrt(cell.amount) * .34);
        if (verticalChain)
        {
            float turn = .025 * sin(gridPosition.y * 2.8 + coordinates.x);
            trace.coverage = smoothstep(-antialias, antialias, halfWidth - abs(trace.offset.x + turn));
        }
        else
        {
            float radiusY = .23 + sqrt(cell.amount) * .23;
            float droplet = length(float2(trace.offset.x / halfWidth, (local.y - .43) / radiusY));
            trace.coverage = 1 - smoothstep(.85, 1.08, droplet);
        }
        trace.falling = 1;
        return trace;
    }

    float fill = liquidAbove ? 1 : smoothstep(-antialias, antialias, trace.surfaceDepth);
    float bank = .08 + (1 - cell.amount) * .14;
    // 只有面向空气的自由边收拢。墙面和水油接触处保持占格连续。
    if (!connectedLeft && left.kind == 0 && !IsBlocked(left))
        fill *= smoothstep(0, bank, local.x);
    if (!connectedRight && right.kind == 0 && !IsBlocked(right))
        fill *= smoothstep(0, bank, 1 - local.x);
    trace.coverage = fill;
    trace.airSurface = separatedFromAir ? 0 : exp2(-max(0, trace.surfaceDepth) * 8);
    if (liquidAbove && above.kind != cell.kind)
        trace.oilBoundary = smoothstep(.56, 1, local.y);
    return trace;
}

#endif
