/* 根据有效接点计算边带，不改变裁片比例。重复相位固定在地图坐标。 */
using System;
using System.Collections.Generic;

namespace Ossastra.SideView.TerrainWorkshop
{
    internal static class TerrainStripPacking
    {
        internal readonly struct Slice
        {
            public float Origin { get; }
            public float Low { get; }
            public float High { get; }

            internal Slice(float origin, float low, float high) => (Origin, Low, High) = (origin, low, high);
        }

        public static IEnumerable<Slice> Place(float low, float high, float span, float step, bool whole)
        {
            if (!Finite(low) || !Finite(high) || !Finite(span) || !Finite(step) ||
                span < .025f || step < .025f || high <= low)
                yield break;
            if (whole && high - low + .0001f < span)
                yield break;

            double index = whole ? Math.Ceiling(low / (double)step) : Math.Floor((low - span) / (double)step) + 1;
            if (whole && index * step + span > high + .0001f)
            {
                yield return new Slice(low, low, low + span);
                yield break;
            }

            for (double x = index * step; x < high - .0001f; x = ++index * step)
            {
                float origin = (float)x;
                if (whole && origin + span > high + .0001f) break;
                float a = Math.Max(low, origin), b = Math.Min(high, origin + span);
                if (b - a >= .0001f)
                    yield return new Slice(origin, a, b);
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
