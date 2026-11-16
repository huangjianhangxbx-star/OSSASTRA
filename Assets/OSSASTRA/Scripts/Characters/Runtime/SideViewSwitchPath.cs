/* 地面连携的空间搜索顺序。几何事实由物理查询提供，路径遇到第一个断点即截断。 */
using System;

namespace Ossastra.Characters
{
    internal static class SideViewSwitchPath
    {
        internal static float ReachableDistance(float distance, float step, Func<float, bool> canReach)
        {
            int samples = (int)Math.Ceiling(Math.Abs(distance) / step - .00001f);
            float last = 0f;
            for (int i = 1; i <= samples; i++)
            {
                float offset = distance * i / samples;
                if (!canReach(offset)) break;
                last = offset;
            }
            return last;
        }

        internal static bool TryFindPlacementOffset(float range, float step, Func<float, bool> canPlace, out float offset)
        {
            offset = 0f;
            if (canPlace(offset)) return true;
            for (int i = 1; (i - 1) * step < range; i++)
            {
                float candidate = Math.Min(i * step, range);
                if (canPlace(-candidate)) { offset = -candidate; return true; }
                if (canPlace(candidate)) { offset = candidate; return true; }
            }
            return false;
        }

        internal static bool KeepsSupport(float dx, float dy, float slope, float tolerance)
            => Math.Abs(dy) <= Math.Abs(dx) * slope + tolerance;
    }
}
