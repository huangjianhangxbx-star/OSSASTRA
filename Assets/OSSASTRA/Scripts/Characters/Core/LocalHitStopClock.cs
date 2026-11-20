using System;

namespace Ossastra.CharacterCore
{
    /// <summary>逐动作传递的命中暂停配方。时长为秒，攻击与受击方有各自的请求。</summary>
    [Serializable]
    public struct AttackHitStop
    {
        public bool configured;
        public float attackerSeconds;
        public float receiverSeconds;
        public bool onGuard;

        public bool Accepts(HitReason reason) =>
            configured && (reason == HitReason.Applied || (onGuard && reason == HitReason.Guarded));
    }

    public enum HitStopEdge { None, Started, Ended }

    /// <summary>本体局部时钟。请求在首次可暂停的更新生效，按未缩放时间到期。</summary>
    public sealed class LocalHitStopClock
    {
        private const double MinimumRecovery = .08;
        private int requestedFrame, lastFrame;
        private double nextEligibleAt;
        public Guid Parent { get; private set; }
        public float RequestedSeconds { get; private set; }
        public double StartedAt { get; private set; }
        public double FinishedAt { get; private set; }
        public int Frames { get; private set; }
        public bool Frozen { get; private set; }

        public bool Request(Guid parent, float seconds, int frame, double now)
        {
            if (parent == Guid.Empty || seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds) ||
                Parent != Guid.Empty || now < nextEligibleAt)
                return false;

            Parent = parent;
            RequestedSeconds = Math.Min(.12f, seconds);
            requestedFrame = frame;
            StartedAt = FinishedAt = 0;
            Frames = 0;
            return true;
        }

        public HitStopEdge Advance(int frame, double now)
        {
            if (Parent == Guid.Empty || frame <= requestedFrame) return HitStopEdge.None;
            if (!Frozen)
            {
                Frozen = true;
                StartedAt = now;
                Frames = 1;
                lastFrame = frame;
                return HitStopEdge.Started;
            }

            if (frame == lastFrame) return HitStopEdge.None;
            lastFrame = frame;
            if (now - StartedAt < RequestedSeconds)
            {
                Frames++;
                return HitStopEdge.None;
            }

            Frozen = false;
            FinishedAt = now;
            Parent = Guid.Empty;
            // 两次停顿之间保留一次可连续推进的区间，持续命中不能无限冻住实体。
            nextEligibleAt = now + MinimumRecovery;
            return HitStopEdge.Ended;
        }

        public bool Cancel(Guid parent)
        {
            if (Parent == Guid.Empty || Parent != parent) return false;
            Reset();
            return true;
        }

        public void Reset()
        {
            Parent = Guid.Empty;
            Frozen = false;
            nextEligibleAt = 0;
        }
    }
}
