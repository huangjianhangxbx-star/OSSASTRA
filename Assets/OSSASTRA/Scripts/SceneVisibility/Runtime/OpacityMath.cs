using System;
using UnityEngine;

// 纯像素圆窗与opacity参考数学：不依赖Renderer、渲染管线或当前角色状态。
namespace Ossastra.SceneVisibility
{
    public readonly struct VisibilityCircle
    {
        public Vector2 CenterPx { get; }
        public float InnerRadiusPx { get; }
        public float OuterRadiusPx { get; }
        public float Strength { get; }

        public VisibilityCircle(Vector2 centerPx, float inner, float outer, float strength)
        {
            VisibilityNumbers.RequireFinite(centerPx.x, nameof(centerPx));
            VisibilityNumbers.RequireFinite(centerPx.y, nameof(centerPx));
            VisibilityNumbers.RequireNonNegative(inner, nameof(inner));
            VisibilityNumbers.RequireNonNegative(outer, nameof(outer));
            VisibilityNumbers.RequireUnit(strength, nameof(strength));
            if (outer < inner || (strength > 0f && outer <= inner))
                throw new ArgumentException("Active circles require outer > inner; a zero-sized circle must be disabled.");
            CenterPx = centerPx; InnerRadiusPx = inner; OuterRadiusPx = outer; Strength = strength;
        }
    }

    // CPU参考计算。Root提供同一viewport的像素坐标，以及足以覆盖身体的核心半径。
    public static class OpacityMath
    {
        // 与显示后端共享语义的CPU参考；先合并双窗/整组并保护支撑，再应用一次边缘样式。
        public static float ComputeEffectiveOpacity(float originalOpacity, Vector2 pixel,
            VisibilityCircle first, VisibilityCircle second, float wholeFade, VisibilityCircle support,
            VisibilityEdgeStyle style, float strength, float cell1080Pixels, int viewportHeight, float minimumOpacity = 0f)
        {
            VisibilityNumbers.RequireUnit(originalOpacity, nameof(originalOpacity));
            VisibilityNumbers.RequireUnit(wholeFade, nameof(wholeFade));
            VisibilityNumbers.RequireUnit(strength, nameof(strength));
            VisibilityNumbers.RequireUnit(minimumOpacity, nameof(minimumOpacity));
            float reveal = Math.Max(wholeFade, Math.Max(CircleReveal(pixel, first), CircleReveal(pixel, second)));
            reveal *= 1f - CircleReveal(pixel, support);
            float spatial = Math.Max(SpatialReveal(pixel, first), SpatialReveal(pixel, second));
            float gate = 4f * spatial * (1f - spatial);
            return Math.Max(originalOpacity * minimumOpacity,
                ApplyEdgeStyle(originalOpacity * (1f - reveal), pixel, style, strength * gate, cell1080Pixels, viewportHeight));
        }
        static float SpatialReveal(Vector2 pixel, VisibilityCircle circle) => circle.OuterRadiusPx <= circle.InnerRadiusPx ? 0f :
            CircleReveal(pixel, new VisibilityCircle(circle.CenterPx, circle.InnerRadiusPx, circle.OuterRadiusPx, 1f));

        // 稳定的viewport像素点阵，只弱混合过渡opacity；端点、强度0与Soft保持精确。
        public static float ApplyEdgeStyle(float opacity, Vector2 pixel, VisibilityEdgeStyle style,
            float strength, float cell1080Pixels, int viewportHeight)
        {
            VisibilityNumbers.RequireUnit(opacity, nameof(opacity)); VisibilityNumbers.RequireUnit(strength, nameof(strength));
            VisibilityNumbers.RequireFinite(pixel.x, nameof(pixel)); VisibilityNumbers.RequireFinite(pixel.y, nameof(pixel));
            if (style != VisibilityEdgeStyle.Soft && style != VisibilityEdgeStyle.SoftDitherEdge)
                throw new ArgumentOutOfRangeException(nameof(style));
            float cell = EdgeCellPixels(cell1080Pixels, viewportHeight);
            if (style == VisibilityEdgeStyle.Soft || strength == 0f || opacity == 0f || opacity == 1f) return opacity;
            int x = Mathf.FloorToInt(pixel.x / cell) & 3, y = Mathf.FloorToInt(pixel.y / cell) & 3;
            float threshold = (Bayer4[y * 4 + x] + .5f) / 16f;
            float coverage = opacity >= threshold ? 1f : 0f;
            return Mathf.Clamp01(Mathf.Lerp(opacity, coverage, strength * 4f * opacity * (1f - opacity)));
        }
        public static float EdgeCellPixels(float cell1080Pixels, int viewportHeight)
        {
            VisibilityNumbers.RequireNonNegative(cell1080Pixels, nameof(cell1080Pixels));
            if (cell1080Pixels > 2f || viewportHeight <= 0) throw new ArgumentOutOfRangeException(nameof(cell1080Pixels));
            float reference = cell1080Pixels > 0f ? cell1080Pixels : 1f;
            return Mathf.Max(1f, Mathf.Floor(reference * viewportHeight / 1080f + .5f));
        }
        static readonly int[] Bayer4 = {0,8,2,10,12,4,14,6,3,11,1,9,15,7,13,5};
        public static float CircleReveal(Vector2 pixel, VisibilityCircle circle)
        {
            VisibilityNumbers.RequireFinite(pixel.x, nameof(pixel));
            VisibilityNumbers.RequireFinite(pixel.y, nameof(pixel));
            if (circle.Strength <= 0f) return 0f; // 全零默认结构表示合法的禁用圆窗。
            double dx = (double)pixel.x - circle.CenterPx.x, dy = (double)pixel.y - circle.CenterPx.y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance <= circle.InnerRadiusPx) return circle.Strength;
            if (distance >= circle.OuterRadiusPx) return 0f;
            double t = (distance - circle.InnerRadiusPx) / ((double)circle.OuterRadiusPx - circle.InnerRadiusPx);
            return (float)(circle.Strength * (1d - t * t * (3d - 2d * t)));
        }

        public static float ComputeOpacity(float originalOpacity, Vector2 pixel,
            VisibilityCircle first, VisibilityCircle second, float wholeFade)
        {
            VisibilityNumbers.RequireUnit(originalOpacity, nameof(originalOpacity));
            VisibilityNumbers.RequireUnit(wholeFade, nameof(wholeFade));
            // 两目标和整组需求取最大值；不重复相乘，避免重叠圆窗额外加深透明度。
            float reveal = Math.Max(wholeFade, Math.Max(CircleReveal(pixel, first), CircleReveal(pixel, second)));
            return originalOpacity * (1f - reveal);
        }
    }

    // 统一有限值/归一化检查；只在输入无效时抛异常，不静默夹值掩盖上游错误。
    internal static class VisibilityNumbers
    {
        internal static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static bool IsUnit(float value) => IsFinite(value) && value >= 0f && value <= 1f;
        internal static void RequireFinite(float value, string name)
        {
            if (!IsFinite(value)) throw new ArgumentOutOfRangeException(name, "A finite value is required.");
        }
        internal static void RequireNonNegative(float value, string name)
        {
            if (!IsFinite(value) || value < 0f) throw new ArgumentOutOfRangeException(name, "A finite nonnegative value is required.");
        }
        internal static void RequireUnit(float value, string name)
        {
            if (!IsUnit(value)) throw new ArgumentOutOfRangeException(name, "A finite value in [0,1] is required.");
        }
    }
}
