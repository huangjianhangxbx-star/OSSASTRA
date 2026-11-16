/* 脚本：SwitchLinkPresentationClock
 * 功能：计算一次成功交接的真实时间包络，并管理它对全局时间的有限贡献。
 * 边界：只计算建议值。外部时间写入优先，释放只恢复本请求仍持有的值。
 */
using System;

namespace Ossastra.Characters
{
    internal sealed class SwitchLinkPresentationClock
    {
        private double startedAt;
        private double enterSeconds, holdSeconds, leaveSeconds;
        private float baselineScale, lastScale;
        private bool mayWriteTime;

        public Guid ExchangeId { get; private set; }
        public bool Active { get; private set; }

        public bool TryBegin(Guid id, double now, float enter, float hold, float leave, float observedScale)
        {
            if (Active || id == Guid.Empty || id == ExchangeId || !Finite(now) ||
                !Finite(enter) || !Finite(hold) || !Finite(leave) || enter < 0f || hold < 0f || leave < 0f ||
                (double)enter + hold + leave <= 0.0 || !Finite(observedScale) || observedScale <= 0f)
                return false;

            ExchangeId = id;
            startedAt = now;
            enterSeconds = enter;
            holdSeconds = hold;
            leaveSeconds = leave;
            baselineScale = lastScale = observedScale;
            Active = mayWriteTime = true;
            return true;
        }

        public bool TrySample(double now, out float weight)
        {
            weight = 0f;
            if (!Active || !Finite(now)) return false;

            double elapsed = Math.Max(0.0, now - startedAt);
            double releaseAt = enterSeconds + holdSeconds;
            if (elapsed >= releaseAt + leaveSeconds) return false;

            if (elapsed < enterSeconds) weight = Ease(elapsed / enterSeconds);
            else if (elapsed < releaseAt) weight = 1f;
            else weight = 1f - Ease((elapsed - releaseAt) / leaveSeconds);
            return true;
        }

        public float ApplyTime(float observedScale, float targetScale, float weight)
        {
            if (!Active || !mayWriteTime) return observedScale;
            if (observedScale != lastScale)
            {
                // 后来写入的暂停或减速优先。保留它的数值，当前交接不再争用时间。
                mayWriteTime = false;
                return observedScale;
            }

            float target = Math.Min(baselineScale, Math.Max(.01f, Math.Min(1f, targetScale)));
            float amount = Math.Max(0f, Math.Min(1f, weight));
            lastScale = amount >= 1f ? target : baselineScale + (target - baselineScale) * amount;
            return lastScale;
        }

        public float Release(float observedScale)
        {
            float restored = Active && mayWriteTime && observedScale == lastScale ? baselineScale : observedScale;
            Active = mayWriteTime = false;
            return restored;
        }

        public static float OrthographicSize(float size, float zoom) => size / Math.Max(1f, zoom);

        public static float PerspectiveFieldOfView(float fieldOfView, float zoom)
        {
            double halfRadians = fieldOfView * Math.PI / 360.0;
            return (float)(Math.Atan(Math.Tan(halfRadians) / Math.Max(1f, zoom)) * 360.0 / Math.PI);
        }

        private static float Ease(double value) => (float)(value * value * (3.0 - 2.0 * value));
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
