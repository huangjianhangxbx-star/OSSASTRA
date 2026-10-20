/*
 * 脚本：PostureState
 * 功能：独占单实体架势、硬直及恢复计时；纯模型，不拥有HP或动作身份。
 * 协作：CombatPosture接收合法命中，各自根只推进一次Tick。
 * 边界：归零当次硬直、不续期；阶段跨越消费剩余delta，不重复计算时间。
 */
using System;

namespace Ossastra.CombatCore
{
    public enum PosturePhase { Normal, Recovering, Staggered }

    public sealed class PostureState
    {
        private readonly double staggerDuration, recoveryFraction, recoveryDelay, recoveryRate;
        public double Maximum { get; }
        public double Current { get; private set; }
        public double StaggerRemaining { get; private set; }
        public double RegenDelayRemaining { get; private set; }
        public bool IsStaggered => StaggerRemaining > 0;
        public PosturePhase Phase => IsStaggered ? PosturePhase.Staggered :
            Current < Maximum ? PosturePhase.Recovering : PosturePhase.Normal;

        public PostureState(double maximum, double duration, double fraction, double delay, double rate)
        {
            if (!Finite(maximum) || maximum <= 0 || !Finite(duration) || duration <= 0 ||
                !Finite(fraction) || fraction <= 0 || fraction > 1 ||
                !Finite(delay) || delay < 0 || !Finite(rate) || rate < 0)
                throw new ArgumentOutOfRangeException(nameof(maximum), "Invalid posture configuration");
            Maximum = maximum;
            staggerDuration = duration;
            recoveryFraction = fraction;
            recoveryDelay = delay;
            recoveryRate = rate;
            Reset();
        }

        /// <summary>返回是否本次新破势；受控中的冲击不续期，也不重置等待。</summary>
        public bool ApplyImpact(double impact)
        {
            if (!Finite(impact) || impact <= 0 || IsStaggered) return false;
            Current = Math.Max(0, Current - impact);
            RegenDelayRemaining = recoveryDelay;
            if (Current > 0) return false;
            StaggerRemaining = staggerDuration;
            return true;
        }

        /// <summary>暂停由根传入零delta；主动作仅暂停自然恢复，不能冻结硬直。</summary>
        public void Tick(double delta, bool primaryActionActive)
        {
            if (!Finite(delta) || delta <= 0) return;
            if (IsStaggered)
            {
                var used = Math.Min(delta, StaggerRemaining);
                StaggerRemaining = Math.Max(0, StaggerRemaining - used);
                delta -= used;
                if (IsStaggered) return;
                Current = Maximum * recoveryFraction;
                RegenDelayRemaining = recoveryDelay;
            }
            if (primaryActionActive) return;
            var wait = Math.Min(delta, RegenDelayRemaining);
            RegenDelayRemaining -= wait;
            delta -= wait;
            Current = Math.Min(Maximum, Current + delta * Maximum * recoveryRate);
        }

        public void Reset()
        {
            Current = Maximum;
            StaggerRemaining = 0;
            RegenDelayRemaining = 0;
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
