/* 功能：有限世界覆盖快照的时序参考计算；与GPU相同地延续反转时的当前亮度。
 * 边界：只计算表现，不授予可见/业务权；动态必须另经当前硬许可。
 * 生命周期：无自有资源，供历史采样、诊断与离线行为验收使用。
 */
using System;
namespace Ossastra.SceneVisibility.Rooms.Rendering
{
    public static class RoomFogTransitionMath
    {
        public const double SampleInterval = 1d / 6d;
        public static float Evaluate(float[] margins, double[] times, double now, float initial, float gain, float loss, float delay)
        {
            if (margins == null || times == null || margins.Length != times.Length || margins.Length == 0 || margins.Length > 5)
                throw new ArgumentException("世界覆盖时序需要1—5个对应快照。");
            bool target = margins[0] >= 0; float from = target ? 1 : 0; double started = times[0];
            for (int i = 1; i < margins.Length; ++i)
            {
                if (times[i] < times[i - 1]) throw new ArgumentException("世界覆盖快照时间必须递增。");
                bool next = margins[i] >= 0;
                if (next == target) continue;
                double crossing = times[i - 1] + (times[i] - times[i - 1]) * Math.Abs(margins[i - 1]) / Math.Max(.000001f, Math.Abs(margins[i] - margins[i - 1]));
                from = Value(from, target, crossing - started, gain, loss, delay);
                if (next) from = Math.Max(initial, from);
                target = next; started = crossing;
            }
            return Value(from, target, now - started, gain, loss, delay);
        }
        static float Value(float from, bool target, double age, float gain, float loss, float delay)
        {
            float duration = target ? gain : loss;
            float localDelay = Math.Min(Math.Max(0, delay), Math.Max(0, duration - .0001f));
            float t = (float)Math.Max(0, Math.Min(1, (age - localDelay) / Math.Max(.0001f, duration - localDelay)));
            float eased = 1 - (1 - t) * (1 - t) * (1 - t);
            return target ? from + (1 - from) * eased : from * (1 - eased);
        }
        public static float DynamicVisibility(bool currentPermission, float presentation) => currentPermission ? presentation : 0;
        public static float FootprintMargin(float x, float y, float z)
        { float hard = Math.Min(x,Math.Min(y,z));return hard<0?hard:Math.Min(x,z); }
        public static float InwardFeather(float margin, float width)
        {
            if (margin < 0) return 0;
            if (width <= 0) return 1;
            float t = Math.Max(0, Math.Min(1, margin / width)); return t * t * (3 - 2 * t);
        }
    }
}
