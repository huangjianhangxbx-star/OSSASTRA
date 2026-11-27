/*
 * 脚本：BasicChainState
 * 功能：以同一模拟时钟管理手动普攻接段资格与最后一次合法预输入。
 * 边界：按前驱身份锁存行为结果；不持有 Unity 目标、不产生动作或伤害。
 */
using System;

namespace Ossastra.CharacterCore
{
    public enum ChainBehaviorResult { None, Pending, Succeeded, Failed }

    public sealed class BasicChainState
    {
        private readonly double retentionSeconds, continuationSeconds;
        private readonly int stageCount;
        private readonly bool requireHit;
        private Guid actionId;
        private int stage;
        private bool hit, buffered, awaitingMarker;
        private double readyAt, bufferUntil;

        public bool HasBuffer => buffered;
        public ChainBehaviorResult BehaviorResult { get; private set; }
        public Guid PreviousActionId => actionId;
        public bool AwaitingMarker => awaitingMarker;
        private bool HasSuccessor => stage > 0 && stage < stageCount;
        private bool CanBuffer => HasSuccessor && BehaviorResult != ChainBehaviorResult.Failed &&
            (!requireHit || hit || BehaviorResult == ChainBehaviorResult.Pending);
        private bool CanContinue => CanBuffer && BehaviorResult != ChainBehaviorResult.Pending && (!requireHit || hit);
        public double WindowUntil => CanBuffer ? readyAt + continuationSeconds : -1;

        public bool IsConnectionOpen(Guid id, double now) =>
            id == actionId && HasSuccessor && BehaviorResult != ChainBehaviorResult.Failed &&
            now <= readyAt + continuationSeconds;

        // 旧投递和重复回执都不能把一次已判定的连接重新打开。
        public bool ReportBehavior(Guid id, bool succeeded, double now)
        {
            if (BehaviorResult != ChainBehaviorResult.Pending || !IsConnectionOpen(id, now)) return false;
            BehaviorResult = succeeded ? ChainBehaviorResult.Succeeded : ChainBehaviorResult.Failed;
            if (!succeeded) buffered = false;
            return true;
        }

        public void InvalidateBehavior(Guid id)
        {
            if (id != actionId || BehaviorResult == ChainBehaviorResult.None) return;
            BehaviorResult = ChainBehaviorResult.Failed;
            buffered = false;
        }

        public BasicChainState(double retentionSeconds, double continuationSeconds, int stageCount, bool requireHit)
        {
            ValidateDuration(retentionSeconds, nameof(retentionSeconds));
            ValidateDuration(continuationSeconds, nameof(continuationSeconds));
            if (stageCount < 1) throw new ArgumentOutOfRangeException(nameof(stageCount));
            this.retentionSeconds = retentionSeconds;
            this.continuationSeconds = continuationSeconds;
            this.stageCount = stageCount;
            this.requireHit = requireHit;
        }

        // 旧公开构造入口只在创建时转成单一期限。保留历史两段组件的命中资格。
        public BasicChainState(double preInput, double bufferLifetime, double continuationWindow, int stageCount = 2)
            : this(preInput, bufferLifetime, continuationWindow, stageCount, true) { }

        public BasicChainState(double preInput, double bufferLifetime, double continuationWindow, int stageCount, bool requireHit)
            : this(LegacyRetention(preInput, bufferLifetime), continuationWindow, stageCount, requireHit) { }

        public static double LegacyRetention(double preInput, double bufferLifetime)
        {
            ValidateDuration(preInput, nameof(preInput));
            ValidateDuration(bufferLifetime, nameof(bufferLifetime));
            return Math.Min(preInput, bufferLifetime);
        }

        private static void ValidateDuration(double value, string parameter)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
                throw new ArgumentOutOfRangeException(parameter);
        }

        // 定时动作的开放许可由动作运行时直接提交。避免重复比较两个不同的时钟累计表达式。
        private bool CanRequestNow(bool attackReady) => attackReady && !awaitingMarker;

        private void ExpireBuffer(double now)
        {
            if (buffered && (!CanBuffer || now > bufferUntil || now > WindowUntil))
                buffered = false;
        }

        /// <summary>只接收一次真实按下。开放前的新边沿更新唯一缓存，不提前发起动作。</summary>
        public int Request(double now, bool attackReady, bool eligible)
        {
            if (!eligible)
            {
                Clear();
                return 0;
            }
            ExpireBuffer(now);
            if (CanRequestNow(attackReady) && (BehaviorResult != ChainBehaviorResult.Pending || !IsConnectionOpen(actionId, now)))
            {
                buffered = false;
                return CanContinue && now <= WindowUntil ? stage + 1 : 1;
            }
            if (CanBuffer && now <= WindowUntil)
            {
                buffered = true;
                bufferUntil = now + retentionSeconds;
            }
            return 0;
        }

        public int Poll(double now, bool attackReady)
        {
            ExpireBuffer(now);
            if (!buffered || !CanContinue || !CanRequestNow(attackReady)) return 0;
            // 先消费再向外提交。即使费用或动作准入拒绝，也不逐帧重试。
            buffered = false;
            return stage + 1;
        }

        public void Accepted(int stage, Guid id, double now, double attackReadyAt, bool eventDriven = false,
            bool requireBehaviorSuccess = false)
        {
            this.stage = stage;
            actionId = id;
            awaitingMarker = eventDriven;
            readyAt = eventDriven ? double.PositiveInfinity : now + attackReadyAt;
            hit = buffered = false;
            BehaviorResult = requireBehaviorSuccess && HasSuccessor ? ChainBehaviorResult.Pending : ChainBehaviorResult.None;
        }

        public void MarkReady(Guid id, double now)
        {
            if (actionId != id || !awaitingMarker) return;
            awaitingMarker = false;
            // 事件驱动以真实开放时刻开始记忆。旧定时保持原定时刻，慢帧不会延长。
            readyAt = now;
        }

        public void Hit(Guid id, bool applied)
        {
            if (id == actionId) hit |= applied;
        }

        public void DiscardBufferedPress() => buffered = false;

        public void Clear()
        {
            stage = 0;
            actionId = Guid.Empty;
            hit = buffered = awaitingMarker = false;
            readyAt = 0;
            BehaviorResult = ChainBehaviorResult.None;
        }
    }
}
