/*
 * 脚本：AttackRelease
 * 功能：结算一次动作释放的目标批次，并返回命中结果。
 * 边界：同一释放只结算一次；按目标身份去重；HP只经Health写入。
 * 协作：世界适配器先做空间筛选，再调用Resolve；空挥关闭批次。冻结架势载荷由同批存活结果消费。
 */
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ossastra.CharacterCore
{
    public enum HitReason { Applied, Duplicate, Dead, Self }

    public sealed class HitOutcome
    {
        public HitReason Reason { get; }
        public int HpBefore { get; }
        public int HpAfter { get; }
        public Guid ActionId { get; }
        public string ActorId { get; }
        public string TargetId { get; }

        internal HitOutcome(HitReason reason, int before, Health target, Guid actionId, string actorId)
        {
            Reason = reason;
            HpBefore = before;
            HpAfter = target.Current;
            TargetId = target.Id;
            ActionId = actionId;
            ActorId = actorId;
        }
    }

    // 每次释放只能提交一个瞬时近战候选批次；空挥也关闭，避免永久开启命中窗口。
    public sealed class AttackRelease
    {
        private readonly Guid actionId;
        private readonly string actorId;
        private readonly int damage;
        private bool resolved;
        public string ActorId => actorId;
        public Guid ActionId => actionId;
        public double PostureImpact { get; }

        internal AttackRelease(Guid actionId, string actorId, int damage, double postureImpact = 0)
        {
            this.actionId = actionId;
            this.actorId = actorId;
            this.damage = damage;
            PostureImpact = postureImpact;
        }

        // 调用方必须先完成实际空间/阵营过滤；本方法不谎称已做物理检测。
        public IReadOnlyList<HitOutcome> Resolve(IEnumerable<Health> targets)
        {
            if (resolved) throw new InvalidOperationException("Attack release already resolved");
            if (targets == null) throw new ArgumentNullException(nameof(targets));
            var batch = targets.ToArray();
            if (batch.Any(t => t == null)) throw new ArgumentException("Null candidate target", nameof(targets));
            resolved = true;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var outcomes = new List<HitOutcome>();
            foreach (var target in batch)
            {
                var before = target.Current;
                HitReason reason;
                if (!seen.Add(target.Id)) reason = HitReason.Duplicate;
                else if (target.Id == actorId) reason = HitReason.Self;
                else if (target.Current == 0) reason = HitReason.Dead;
                else
                {
                    target.ApplyDamage(damage);
                    reason = HitReason.Applied;
                }
                outcomes.Add(new HitOutcome(reason, before, target, actionId, actorId));
            }
            return outcomes;
        }
    }
}
