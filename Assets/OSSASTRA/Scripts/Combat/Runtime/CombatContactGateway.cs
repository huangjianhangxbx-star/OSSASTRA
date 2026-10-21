/* 脚本：CombatContactGateway
 * 功能：把空间候选收敛为一个身份合法、防御已采样的Release批次，再分发同批回执。
 * 边界：HP只经AttackRelease；多碰撞体合并、冒名整组拒绝；不拥有动作时钟。
 * 协作：近战/范围适配器先查空间与LOS；网关复核来源和目标，HP反馈后处理存活者架势。
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
        public static IReadOnlyList<HitOutcome> Resolve(AttackRelease release, CombatVitals source, IEnumerable<IHitTarget> candidates, Vector3 origin)
        {
            if(release==null)throw new ArgumentNullException(nameof(release));
            if(source!=null && (!source.IsAvailable || source.Health.Id!=release.ActorId))return release.Resolve(Array.Empty<Health>());
            var targets=candidates.Where(CombatTargetValidity.IsAvailable).Distinct()
                .GroupBy(t=>t.Health.Id,StringComparer.Ordinal).Where(g=>g.Count()==1).Select(g=>g.Single())
                .Where(t=>CombatTargetPolicy.CanHit(release.ActorId,source==null?CombatFaction.Neutral:source.Faction,
                    source==null||source.IsAlive,t.Health.Id,t is ICombatTarget combat?combat.Faction:CombatFaction.Neutral,true)).ToArray();
            var defenses=targets.ToDictionary(t=>t,t=>(t as MonoBehaviour)?.GetComponents<MonoBehaviour>().OfType<ICombatDefense>().FirstOrDefault(x=>(x as Behaviour).isActiveAndEnabled));
            var outcomes=release.ResolveContacts(targets.Select(t=>new AttackContact(t.Health,defenses[t]?.CaptureDefense(origin)??DefenseSnapshot.Normal)));
            foreach(var outcome in outcomes)
            {
                var target=targets.First(t=>t.Health.Id==outcome.TargetId);
                if(outcome.Reason==HitReason.Applied)
                {if(target is ICombatTarget combat)combat.OnHitResolved(outcome);else target.ShowHit();}
                if(outcome.Reason==HitReason.Guarded||outcome.Reason==HitReason.Evaded)defenses[target]?.OnDefenseResolved(outcome);
                if(outcome.HpAfter>0 && outcome.PostureScale>0 && target is MonoBehaviour component && component!=null)
                    foreach(var receiver in component.GetComponents<MonoBehaviour>().OfType<ICombatPostureReceiver>())
                        receiver.ReceiveImpact(outcome,release.PostureImpact*outcome.PostureScale);
            }
            return outcomes;
        }
    }
}
