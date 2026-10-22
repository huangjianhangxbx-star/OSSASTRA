/*
 * 脚本：AttackRelease
 * 功能：结算一次动作释放的目标批次，并返回命中结果；派生释放保留父执行与节点索引。
 * 边界：新旧入口共享一次性批次；按目标身份去重；HP只经Health写入，零伤防御不调用扣血。
 * 协作：世界适配器先做空间筛选，再调用Resolve；空挥关闭批次。防御快照决定HP及架势倍率，同批回执供世界适配器消费。
 */
using System;
using System.Collections.Generic;
using System.Linq;


namespace Ossastra.CharacterCore
{
    public enum HitReason { Applied, Duplicate, Dead, Self, Guarded, Evaded }

    /// <summary>空间/身份已合法的候选及同帧只读防御快照；不另持有HP余额。</summary>
    public readonly struct AttackContact
    {
        public Health Health { get; }
        public DefenseSnapshot Defense { get; }
        public AttackContact(Health health, DefenseSnapshot defense)
        { Health=health ?? throw new ArgumentNullException(nameof(health)); Defense=defense; }
    }

    public sealed class HitOutcome
    {
        public HitReason Reason { get; }
        public int HpBefore { get; }
        public int HpAfter { get; }
        public Guid ActionId { get; }
        public string ActorId { get; }
        public string TargetId { get; }
        public double PostureScale { get; }

        internal HitOutcome(HitReason reason, int before, Health target, Guid actionId, string actorId, double postureScale=1)
        {
            Reason = reason;
            HpBefore = before;
            HpAfter = target.Current;
            TargetId = target.Id;
            ActionId = actionId;
            ActorId = actorId;
            PostureScale = reason==HitReason.Applied || reason==HitReason.Guarded ? postureScale : 0;
        }
    }

    // 每次释放只能提交一个瞬时近战候选批次；空挥也关闭，避免永久开启命中窗口。
    public sealed class AttackRelease
    {
        private readonly Guid actionId;
        private readonly string actorId;
        private readonly int damage;
        private bool resolved;
        private HashSet<string> windowTargets;
        public string ActorId => actorId;
        public Guid ActionId => actionId;
        public double PostureImpact { get; }
        public Guid ParentExecutionId {get;private set;}
        public int EmissionIndex {get;private set;}
        public double ReclaimRate {get;private set;}=.5;
        public double ReclaimBudgetFraction {get;private set;}=.05;

        internal AttackRelease(Guid actionId, string actorId, int damage, double postureImpact = 0)
        {
            this.actionId = actionId;
            this.actorId = actorId;
            this.damage = damage;
            PostureImpact = postureImpact;
            ParentExecutionId=actionId;
        }
        /// <summary>已接受执行的多次释放各有一次性批次；保留共同父身份供整次预算/条件去重。</summary>
        public static AttackRelease ForEmission(Guid parent,int index,string actorId,int damage,double postureImpact,double reclaimBudget=.05,double reclaimRate=.5)
        {
            if(parent==Guid.Empty||index<0||string.IsNullOrWhiteSpace(actorId)||damage<=0||!BasicAttackDefinition.Finite(postureImpact)||postureImpact<0||
                !BasicAttackDefinition.Finite(reclaimBudget)||reclaimBudget<0||reclaimBudget>1||!BasicAttackDefinition.Finite(reclaimRate)||reclaimRate<0||reclaimRate>1)
                throw new ArgumentException("Invalid derived release identity/payload");
            return new AttackRelease(Guid.NewGuid(),actorId,damage,postureImpact){ParentExecutionId=parent,EmissionIndex=index,ReclaimBudgetFraction=reclaimBudget,ReclaimRate=reclaimRate};
        }
        /// <summary>有限持续窗口显式选择此入口；各批次共享波次身份和已接触目标集，不放宽普通瞬时释放。</summary>
        public static AttackRelease ForContactWindow(Guid parent,int index,string actorId,int damage,double postureImpact,double reclaimBudget=.05,double reclaimRate=.5)
        {
            var release=ForEmission(parent,index,actorId,damage,postureImpact,reclaimBudget,reclaimRate);
            release.windowTargets=new HashSet<string>(StringComparer.Ordinal);return release;
        }

        // 调用方必须先完成实际空间/阵营过滤；本方法不谎称已做物理检测。
        public IReadOnlyList<HitOutcome> Resolve(IEnumerable<Health> targets)
        {
            if(targets==null) throw new ArgumentNullException(nameof(targets));
            return ResolveContacts(targets.Select(t=>new AttackContact(t,DefenseSnapshot.Normal)));
        }

        /// <summary>与旧Resolve共享一次性水位；盾挡零伤不调用ApplyDamage，闪避不消费架势。</summary>
        public IReadOnlyList<HitOutcome> ResolveContacts(IEnumerable<AttackContact> contacts)
        {
            if (resolved&&windowTargets==null) throw new InvalidOperationException("Attack release already resolved");
            if (contacts == null) throw new ArgumentNullException(nameof(contacts));
            var batch = contacts.ToArray();
            if (batch.Any(t => t.Health == null)) throw new ArgumentException("Null candidate target", nameof(contacts));
            resolved = true;
            var seen = windowTargets??new HashSet<string>(StringComparer.Ordinal);
            var outcomes = new List<HitOutcome>();
            foreach (var contact in batch)
            {
                var target=contact.Health;
                var before = target.Current;
                HitReason reason;
                if (!seen.Add(target.Id)) reason = HitReason.Duplicate;
                else if (target.Id == actorId) reason = HitReason.Self;
                else if (target.Current == 0) reason = HitReason.Dead;
                else if (contact.Defense.Evading) reason=HitReason.Evaded;
                else if (contact.Defense.Guarded) reason=HitReason.Guarded;
                else
                {
                    target.ApplyDamage(damage);
                    reason = HitReason.Applied;
                }
                outcomes.Add(new HitOutcome(reason, before, target, actionId, actorId,contact.Defense.PostureScale));
            }
            return outcomes;
        }
    }
}
