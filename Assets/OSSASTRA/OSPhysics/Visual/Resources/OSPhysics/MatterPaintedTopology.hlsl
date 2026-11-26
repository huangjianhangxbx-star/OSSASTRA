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
    uint kind;
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

    trace.kind = cell.kind;
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


// 新资产视图以有符号距离描绘液体，分类、防墙及深度仍来自同一快照。
bool IsLiquidAir(PaintedCell cell)
{
    return !IsBlocked(cell) && cell.kind == 0;
}

bool FindLiquidEnvelope(float2 local, inout int2 index, inout PaintedCell cell)
{
    if (IsLiquid(cell)) return true;
    if (!IsLiquidAir(cell)) return false;

    // 最多借到贴面空格的近侧。对角本来不接通时，保留拐角处的空气。
    int2 side = int2(local.x < .5 ? -1 : 1, local.y < .5 ? -1 : 1);
    float2 distance = min(local, 1 - local);
    int2 horizontal = int2(side.x, 0);
    int2 vertical = int2(0, side.y);
    PaintedCell lateral = (PaintedCell)0, upright = (PaintedCell)0;
    if (distance.x < .34) lateral = ReadMatter(index + horizontal);
    if (distance.y < .34) upright = ReadMatter(index + vertical);
    bool hasLateral = IsLiquid(lateral);
    bool hasUpright = IsLiquid(upright);
    if (!hasLateral && !hasUpright) return false;
    if (hasLateral && hasUpright &&
        (!SameLiquid(lateral, upright) || !SameLiquid(lateral, ReadMatter(index + horizontal + vertical))))
        return false;

    bool useLateral = hasLateral && (!hasUpright || distance.x < distance.y);
    if (useLateral) cell = lateral;
    else cell = upright;
    index += useLateral ? horizontal : vertical;
    return true;
}

float RoundedLiquidDistance(float2 sampleAt, float2 lower, float2 upper, float roundness)
{
    float2 halfSize = (upper - lower) * .5;
    float corner = min(min(halfSize.x, halfSize.y), roundness);
    float2 outside = abs(sampleAt - (lower + halfSize)) - halfSize + corner;
    return corner - length(max(outside, 0)) - min(max(outside.x, outside.y), 0);
}

float ContinuousLiquidDepth(PaintedCell cell, PaintedCell above, int2 index, float topDepth, float y)
{
    if (!SameLiquid(cell, above)) return max(0, topDepth);
    float depth = above.amount + 1 - y;
    // 深度只查有限同相列。每段共享边界高度，超过上限只保留深水色。
    [unroll] for (int stepUp = 2; stepUp <= 4; stepUp++)
    {
        PaintedCell higher = ReadMatter(index + int2(0, stepUp));
        if (!SameLiquid(cell, higher)) break;
        depth += 1 - above.amount + higher.amount;
        above = higher;
    }
    return max(0, depth);
}

LiquidTrace DescribeContinuousLiquid(PaintedCell cell, float2 gridPosition, float antialias, float wave)
{
    LiquidTrace trace = (LiquidTrace)0;
    int2 index = (int2)floor(gridPosition);
    if (!FindLiquidEnvelope(frac(gridPosition), index, cell)) return trace;

    float2 local = gridPosition - index;
    PaintedCell above = ReadMatter(index + int2(0, 1));
    PaintedCell below = ReadMatter(index + int2(0, -1));
    PaintedCell left = ReadMatter(index + int2(-1, 0));
    PaintedCell right = ReadMatter(index + int2(1, 0));
    bool linkedAbove = SameLiquid(cell, above);
    bool linkedBelow = SameLiquid(cell, below);
    bool linkedLeft = SameLiquid(cell, left);
    bool linkedRight = SameLiquid(cell, right);
    bool openBelow = IsLiquidAir(below);
    bool wetAbove = IsLiquid(above);
    bool falling = openBelow;
    if (!falling && linkedBelow && below.amount < .79)
    {
        PaintedCell lower = ReadMatter(index + int2(0, -2));
        falling = IsLiquidAir(lower) || (SameLiquid(cell, lower) && lower.amount < .79);
    }

    trace.kind = cell.kind;
    trace.offset = local - .5;
    float aa = min(antialias, .13);

    // 真正分离的格形成一颗闭合水滴。相邻异种液体依然按界面处理。
    if (openBelow && !wetAbove && !IsLiquid(left) && !IsLiquid(right))
    {
        float size = sqrt(cell.amount);
        float2 radii = float2(.12 + size * .37, .18 + size * .31);
        float2 center = float2(.5 + wave * .28, .47);
        float distance = (1 - length((local - center) / radii)) * min(radii.x, radii.y);
        trace.coverage = smoothstep(-aa, aa, distance);
        trace.falling = 1;
        trace.surfaceDepth = max(0, distance);
        return trace;
    }

    // 同一连续水面从左右共享高度。薄墙和水油交界只截断这种跨格计算。
    float centerHeight = max(.045, cell.amount);
    float leftHeight = centerHeight;
    float rightHeight = centerHeight;
    if (linkedLeft) leftHeight = IsLiquid(ReadMatter(index + int2(-1, 1))) ? 1 : max(.045, left.amount);
    if (linkedRight) rightHeight = IsLiquid(ReadMatter(index + int2(1, 1))) ? 1 : max(.045, right.amount);
    trace.slope = rightHeight - leftHeight;
    float sideHeight = local.x < .5 ? leftHeight : rightHeight;
    float blend = .5 * smoothstep(0, 1, abs(local.x * 2 - 1));
    float poolTop = lerp(centerHeight, sideHeight, blend);
    bool freeAbove = IsLiquidAir(above);
    if (freeAbove) poolTop += .075 * smoothstep(.86, 1, poolTop) + wave;

    // 多格宽流同样使用外侧收拢，内侧共享面延伸。临近上下格时插值边宽，避免逐行拼方块。
    float rowAmount = cell.amount;
    if (local.y < .5 && linkedBelow) rowAmount = lerp(cell.amount, below.amount, .5 * saturate(1 - 2 * local.y));
    if (local.y >= .5 && linkedAbove) rowAmount = lerp(cell.amount, above.amount, .5 * saturate(2 * local.y - 1));
    float turn = falling ? .046 * sin(gridPosition.y * 2.1 + index.x * .73) + wave * .2 : 0;
    float inset = falling ? .49 - .58 * sqrt(rowAmount) : -.045;
    float leftEdge = IsLiquidAir(left) ? inset + turn : -.31;
    float rightEdge = IsLiquidAir(right) ? 1 - inset + turn : 1.31;
    float bottomEdge = openBelow ? .04 : -.31;
    float topEdge = wetAbove ? 1.31 : poolTop;
    if (freeAbove && falling) topEdge = min(1.04, .78 + .25 * sqrt(rowAmount) + wave);
    topEdge = max(topEdge, bottomEdge + .055);
    float distance = RoundedLiquidDistance(local, float2(leftEdge, bottomEdge),
        float2(rightEdge, topEdge), falling ? .23 : .15);
    trace.coverage = smoothstep(-aa, aa, distance);
    if (trace.coverage <= 0) return trace;

    trace.falling = falling ? 1 : 0;
    float topDepth = topEdge - local.y;
    if (freeAbove && !falling) trace.airSurface = exp2(-max(0, topDepth) * 8);
    trace.surfaceDepth = falling ? max(0, topDepth) : ContinuousLiquidDepth(cell, above, index, topDepth, local.y);
    if (wetAbove && !linkedAbove) trace.oilBoundary = smoothstep(.56, 1, local.y);
    return trace;
}

#endif
