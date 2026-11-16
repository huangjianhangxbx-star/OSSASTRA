/* 功能：登记实际出手产生的队伍连携机会，维护唯一准备凭证与共享冷却。
 * 边界：不处理玩家输入、角色控制、地面资格或伤害。时间由队伍传入统一战斗时钟。
 * 协作：真实投递完成后登记。准备只读，安全交接成功后以原主控身份消费。
 */
using System;
using System.Collections.Generic;

namespace Ossastra.CharacterCore
{
    public enum SwitchLinkReleaseSource { Other, PlayerAttack, PlayerOptInAbility, Companion, SwitchAttack }
    public enum SwitchLinkWindowStatus { NoOpportunity, OwnerChanged, Expired, CoolingDown, Ready, InvalidTime }

    /// <summary>从原动作申请冻结的本体、生命代际与主控代际。</summary>
    public readonly struct SwitchLinkIdentity
    {
        public string ActorId { get; }
        public int LifeGeneration { get; }
        public int ControlRevision { get; }

        public SwitchLinkIdentity(string actorId, int lifeGeneration, int controlRevision)
        {
            ActorId = actorId;
            LifeGeneration = lifeGeneration;
            ControlRevision = controlRevision;
        }

        internal bool IsValid => !string.IsNullOrWhiteSpace(ActorId) && LifeGeneration >= 0 && ControlRevision >= 0;

        internal bool Matches(SwitchLinkIdentity other) => LifeGeneration == other.LifeGeneration &&
            ControlRevision == other.ControlRevision && string.Equals(ActorId, other.ActorId, StringComparison.Ordinal);
    }

    /// <summary>只读准备凭证。新出手、清理或实际消费后，旧凭证不能再提交。</summary>
    public readonly struct SwitchLinkOpportunity
    {
        public SwitchLinkIdentity Identity { get; }
        public Guid ExecutionId { get; }
        public SwitchLinkReleaseSource Source { get; }
        public double ReleasedAt { get; }
        public double ExpiresAt { get; }
        internal SwitchLinkWindow Window { get; }

        internal SwitchLinkOpportunity(SwitchLinkWindow window, SwitchLinkIdentity identity, Guid executionId,
            SwitchLinkReleaseSource source, double releasedAt, double expiresAt)
        {
            Window = window;
            Identity = identity;
            ExecutionId = executionId;
            Source = source;
            ReleasedAt = releasedAt;
            ExpiresAt = expiresAt;
        }
    }

    public sealed class SwitchLinkWindow
    {
        private readonly double windowSeconds;
        private readonly double cooldownSeconds;
        // 同一个生命与控制代际的多节点可以迟到。保留执行身份，直到合法的新身份登记。
        private readonly HashSet<Guid> openedExecutions = new HashSet<Guid>();
        private SwitchLinkIdentity recordedIdentity;
        private SwitchLinkOpportunity opening;
        private double latestReleaseTime;
        private double cooldownEnd;

        public SwitchLinkWindow(double windowSeconds = 1, double cooldownSeconds = 2.5)
        {
            if (!ValidTime(windowSeconds) || windowSeconds == 0)
                throw new ArgumentOutOfRangeException(nameof(windowSeconds));
            if (!ValidTime(cooldownSeconds))
                throw new ArgumentOutOfRangeException(nameof(cooldownSeconds));
            this.windowSeconds = windowSeconds;
            this.cooldownSeconds = cooldownSeconds;
        }

        /// <summary>登记合格主控的实际释放。命中结果、发射节点数和空挥均不参与。</summary>
        public bool ObserveRelease(SwitchLinkIdentity released, SwitchLinkIdentity current, Guid executionId,
            SwitchLinkReleaseSource source, double combatTime)
        {
            if (!released.IsValid || !released.Matches(current) || executionId == Guid.Empty ||
                (source != SwitchLinkReleaseSource.PlayerAttack && source != SwitchLinkReleaseSource.PlayerOptInAbility) ||
                !ValidTime(combatTime) || combatTime < latestReleaseTime || !ValidTime(combatTime + windowSeconds))
                return false;

            if (!recordedIdentity.Matches(released))
            {
                openedExecutions.Clear();
                recordedIdentity = released;
            }
            if (!openedExecutions.Add(executionId)) return false;

            latestReleaseTime = combatTime;
            opening = new SwitchLinkOpportunity(this, released, executionId, source, combatTime, combatTime + windowSeconds);
            return true;
        }

        /// <summary>纯查询。外层另行检查双方状态、空间和动作准入，再提交实际交接。</summary>
        public bool TryPrepare(SwitchLinkIdentity current, double combatTime, out SwitchLinkOpportunity opportunity,
            out SwitchLinkWindowStatus status)
        {
            opportunity = default;
            if (!ValidTime(combatTime) || combatTime < latestReleaseTime)
                status = SwitchLinkWindowStatus.InvalidTime;
            else if (opening.ExecutionId == Guid.Empty)
                status = SwitchLinkWindowStatus.NoOpportunity;
            else if (!opening.Identity.Matches(current))
                status = SwitchLinkWindowStatus.OwnerChanged;
            else if (combatTime >= opening.ExpiresAt)
                status = SwitchLinkWindowStatus.Expired;
            else if (combatTime < cooldownEnd)
                status = SwitchLinkWindowStatus.CoolingDown;
            else
            {
                status = SwitchLinkWindowStatus.Ready;
                opportunity = opening;
            }
            return status == SwitchLinkWindowStatus.Ready;
        }

        /// <summary>成功交接后提交一次。身份仍传原主控的冻结值，冷却由队伍持有。</summary>
        public bool TryCommit(SwitchLinkOpportunity prepared, SwitchLinkIdentity outgoing, double combatTime)
        {
            if (prepared.Window != this || !ValidTime(combatTime + cooldownSeconds) ||
                !TryPrepare(outgoing, combatTime, out var current, out _) || prepared.ExecutionId != current.ExecutionId ||
                !prepared.Identity.Matches(current.Identity) || prepared.ReleasedAt != current.ReleasedAt)
                return false;
            ClearOpportunity();
            cooldownEnd = combatTime + cooldownSeconds;
            return true;
        }

        /// <summary>普通切换、受击或输入夺取只撤销机会。已登记动作和冷却继续有效。</summary>
        public void ClearOpportunity() => opening = default;

        public double OpportunityRemaining(SwitchLinkIdentity current, double combatTime) =>
            opening.ExecutionId != Guid.Empty && opening.Identity.Matches(current) &&
            ValidTime(combatTime) && combatTime >= opening.ReleasedAt ? Math.Max(0, opening.ExpiresAt - combatTime) : 0;

        public double CooldownRemaining(double combatTime) => ValidTime(combatTime) ? Math.Max(0, cooldownEnd - combatTime) : 0;

        private static bool ValidTime(double value) => value >= 0 && !double.IsInfinity(value);
    }
}
