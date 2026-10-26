/* 脚本：CombatContactGateway
 * 功能：把空间候选收敛为一个身份合法、防御已采样的Release批次，再分发同批回执。
 * 边界：HP只经AttackRelease；多碰撞体合并、冒名整组拒绝；不拥有动作时钟。
 * 协作：适配器先查空间与LOS；网关复核来源和目标，HP实际结果交可选夺回模块，再处理存活者架势。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using Ossastra.CharacterCore;
using Ossastra.CombatCore;
using UnityEngine;
namespace Ossastra.Combat
{
    public static class CombatContactGateway
    {
        public static event Action<CombatContactBatch> Resolved;
        public static IReadOnlyList<HitOutcome> Resolve(AttackRelease release, CombatVitals source, IEnumerable<IHitTarget> candidates, Vector3 origin, CombatContactCause cause=CombatContactCause.Direct)
            =>Resolve(release,source,candidates,origin,cause,null);
        /// <summary>只读效果元数据重载；结算顺序和旧四/五参数调用保持原样。</summary>
        public static IReadOnlyList<HitOutcome> Resolve(AttackRelease release, CombatVitals source, IEnumerable<IHitTarget> candidates, Vector3 origin, CombatContactCause cause,string effectSemantic)
        {
            if(release==null)throw new ArgumentNullException(nameof(release));
            if(source!=null && (!source.IsAvailable || source.Health.Id!=release.ActorId))return release.Resolve(Array.Empty<Health>());
            var targets=candidates.Where(CombatTargetValidity.IsAvailable)
                .Where(t=>!(t is ICombatContactFilter filter)||filter.Accepts(release)).Distinct()
                .GroupBy(t=>t.Health.Id,StringComparer.Ordinal).Where(g=>g.Count()==1).Select(g=>g.Single())
                .Where(t=>CombatTargetPolicy.CanHit(release.ActorId,source==null?CombatFaction.Neutral:source.Faction,
                    source==null||source.IsAlive,t.Health.Id,t is ICombatTarget combat?combat.Faction:CombatFaction.Neutral,true)).ToArray();
            var defenses=targets.ToDictionary(t=>t,t=>(t as MonoBehaviour)?.GetComponents<MonoBehaviour>().OfType<ICombatDefense>().FirstOrDefault(x=>(x as Behaviour).isActiveAndEnabled));
            var outcomes=release.ResolveContacts(targets.Select(t=>new AttackContact(t.Health,defenses[t]?.CaptureDefense(origin)??DefenseSnapshot.Normal)));
            // 只快照真实回执的位置；在死亡/破坏表现回调之前捕获，发布仍在原分发循环之后。
            var batch=Resolved==null?null:CaptureBatch(release,source,origin,outcomes,targets,cause,effectSemantic);
            foreach(var outcome in outcomes)
            {
                var target=targets.First(t=>t.Health.Id==outcome.TargetId);
                if(outcome.Reason==HitReason.Applied)
                {
                    if(target is ICombatTarget combat)combat.OnHitResolved(outcome);else target.ShowHit();
                    if(!(target is ICombatReclaimPolicy reclaimPolicy)||reclaimPolicy.AllowsReclaim)source?.Recovery?.OnApplied(release,outcome);
                }
                if(outcome.Reason==HitReason.Guarded||outcome.Reason==HitReason.Evaded)defenses[target]?.OnDefenseResolved(outcome);
                if(outcome.HpAfter>0 && outcome.PostureScale>0 && target is MonoBehaviour component && component!=null)
                    foreach(var receiver in component.GetComponents<MonoBehaviour>().OfType<ICombatPostureReceiver>())
                        receiver.ReceiveImpact(outcome,release.PostureImpact*outcome.PostureScale);
            }
            if(batch!=null)Publish(batch);
            return outcomes;
        }
        private static CombatContactBatch CaptureBatch(AttackRelease release,CombatVitals source,Vector3 origin,IReadOnlyList<HitOutcome> outcomes,IHitTarget[] targets,CombatContactCause cause,string effectSemantic)
        {
            try
            {
                var receipts=outcomes.Select(outcome=>
                {
                    var target=targets.First(t=>t.Health.Id==outcome.TargetId);
                    var position=target is ICombatTarget combat?combat.WorldPosition:target is Component component&&component!=null?component.transform.position:origin;
                    return new CombatContactReceipt(outcome,target as CombatVitals,position);
                });
                return new CombatContactBatch(release.ParentExecutionId,release.ActionId,release.EmissionIndex,release.ActorId,source,origin,receipts,cause,effectSemantic);
            }
            catch(Exception error){Debug.LogWarning("CombatContactGateway事实快照异常："+error);return null;}
        }
        private static void Publish(CombatContactBatch batch)
        {
            var observed=Resolved;
            if(observed==null)return;
            foreach(Action<CombatContactBatch> observer in observed.GetInvocationList())
                try{observer(batch);}catch(Exception error){Debug.LogWarning("CombatContactGateway事实观察者异常："+error);}
        }
    }
}
