/* 脚本：CombatContactGateway
 * 功能：把空间候选收敛为一个身份合法、防御已采样的Release批次，再分发含空间事实的同批回执。
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
        public static event Action<CombatReleaseVisualFact> SectorReleased;

        // 只有已接受的扇区或瞬时近战投递在这里发布一次。活动命中窗口不重复发布挥击。
        internal static void PublishSectorRelease(AttackRelease release, CombatVitals source,
            CombatContactGeometry geometry, float weaponHeight)
        {
            var observed = SectorReleased;
            if (observed == null || source == null || geometry.Plane != CombatPlane.SideXY) return;
            var fact = new CombatReleaseVisualFact(release.ParentExecutionId, release.ActionId,
                release.EmissionIndex, release.ActorId, source, geometry, weaponHeight);
            foreach (Action<CombatReleaseVisualFact> observer in observed.GetInvocationList())
                try { observer(fact); }
                catch (Exception error) { Debug.LogWarning("CombatContactGateway挥击观察者异常：" + error); }
        }

        public static IReadOnlyList<HitOutcome> Resolve(AttackRelease release, CombatVitals source, IEnumerable<IHitTarget> candidates, Vector3 origin, CombatContactCause cause=CombatContactCause.Direct)
            =>Resolve(release,source,candidates,origin,cause,null);
        /// <summary>只读效果元数据重载；结算顺序和旧四/五参数调用保持原样。</summary>
        public static IReadOnlyList<HitOutcome> Resolve(AttackRelease release, CombatVitals source, IEnumerable<IHitTarget> candidates, Vector3 origin, CombatContactCause cause,string effectSemantic)
            => Resolve(release,source,candidates,origin,cause,effectSemantic,null,null,null);

        /// <summary>接收投递器已经取得的空间证据。命中点与环境接触只影响反馈，不参与HP结算。</summary>
        internal static IReadOnlyList<HitOutcome> Resolve(AttackRelease release, CombatVitals source,
            IEnumerable<IHitTarget> candidates, Vector3 origin, CombatContactCause cause, string effectSemantic,
            CombatContactGeometry? geometry, Func<IHitTarget, CombatContactPoint?> contact,
            CombatContactPoint? environmentContact, DeliveryMode? deliveryMode = null, string channel = null)
        {
            if(release==null)throw new ArgumentNullException(nameof(release));
            if(source!=null && (!source.IsAvailable || source.Health.Id!=release.ActorId))return release.Resolve(Array.Empty<Health>());
            var trace = CombatTraceProbe.For(source,true);
            bool Permit(bool allowed, string step, string target = "")
            {
                if (!allowed) trace?.Note("判定","结算筛除",step,release.ActionId,release.ParentExecutionId,release.EmissionIndex,target);
                return allowed;
            }
            var targets=candidates.Where(t=>Permit(CombatTargetValidity.IsAvailable(t),"无效身份"))
                .Where(t=>Permit(!(t is ICombatContactFilter filter)||filter.Accepts(release),"目标接触策略",t.Health.Id)).Distinct()
                .GroupBy(t=>t.Health.Id,StringComparer.Ordinal).Where(g=>Permit(g.Count()==1,"同一身份对应多个目标",g.Key)).Select(g=>g.Single())
                .Where(t=>Permit(AllowsTargetFaction(release,source,t),"自身、源状态或阵营",t.Health.Id)).ToArray();
            var defenses=targets.ToDictionary(t=>t,t=>(t as MonoBehaviour)?.GetComponents<MonoBehaviour>().OfType<ICombatDefense>().FirstOrDefault(x=>(x as Behaviour).isActiveAndEnabled));
            var outcomes=release.ResolveContacts(targets.Select(t=>new AttackContact(t.Health,defenses[t]?.CaptureDefense(origin)??DefenseSnapshot.Normal)));
            // 只快照真实回执的位置；在死亡/破坏表现回调之前捕获，发布仍在原分发循环之后。
            var batch=Resolved==null?null:CaptureBatch(release,source,origin,outcomes,targets,cause,effectSemantic,
                geometry,contact,environmentContact,deliveryMode,channel);
            foreach(var outcome in outcomes)
            {
                var target=targets.First(t=>t.Health.Id==outcome.TargetId);
                var component = target as MonoBehaviour;
                // Freeze direction before feedback/death callbacks can move the actor or an attack source.
                int directionX = 0;
                if (outcome.HpAfter > 0 && component != null)
                {
                    var position = target is ICombatTarget combatTarget ? combatTarget.WorldPosition : component.transform.position;
                    float horizontal = geometry?.ArrivalDirection.x ?? 0;
                    if (Mathf.Abs(horizontal) <= .001f) horizontal = position.x - origin.x;
                    if (Mathf.Abs(horizontal) <= .001f) horizontal = geometry?.ReleaseDirection.x ?? 0;
                    directionX = horizontal < -.001f ? -1 : 1;
                }
                if(outcome.Reason==HitReason.Applied)
                {
                    if(target is ICombatTarget combat)combat.OnHitResolved(outcome);else target.ShowHit();
                    if(!(target is ICombatReclaimPolicy reclaimPolicy)||reclaimPolicy.AllowsReclaim)source?.Recovery?.OnApplied(release,outcome);
                }
                if(outcome.Reason==HitReason.Guarded||outcome.Reason==HitReason.Evaded)defenses[target]?.OnDefenseResolved(outcome);
                if (outcome.HpAfter <= 0 || component == null) continue;
                double impact = release.PostureImpact * outcome.PostureScale;
                foreach (var receiver in component.GetComponents<MonoBehaviour>().OfType<ICombatPostureReceiver>())
                {
                    if (receiver is IResolvedCombatPostureReceiver actionPosture && actionPosture.UsesActionPosture)
                        actionPosture.ReceiveContact(new ResolvedPostureContact(outcome, impact, cause, effectSemantic,
                            release.ParentExecutionId, release.EmissionIndex, directionX));
                    else if (outcome.PostureScale > 0)
                        receiver.ReceiveImpact(outcome, impact);
                }
            }
            if (source != null && release.HitStop.configured)
                foreach (var outcome in outcomes)
                    source.ApplyHitStop(release, targets.First(t => t.Health.Id == outcome.TargetId) as CombatVitals, outcome);
            if(batch!=null)Publish(batch);
            return outcomes;
        }

        // 物理筛选器可用同一阵营规则判断是否考虑墙面表现，最终身份和过滤仍在上面的结算批次。
        internal static bool AllowsTargetFaction(AttackRelease release, CombatVitals source, IHitTarget target)
            => CombatTargetPolicy.CanHit(release.ActorId, source == null ? CombatFaction.Neutral : source.Faction,
                source == null || source.IsAlive, target.Health.Id,
                target is ICombatTarget combat ? combat.Faction : CombatFaction.Neutral, true);

        private static CombatContactBatch CaptureBatch(AttackRelease release,CombatVitals source,Vector3 origin,
            IReadOnlyList<HitOutcome> outcomes,IHitTarget[] targets,CombatContactCause cause,string effectSemantic,
            CombatContactGeometry? geometry, Func<IHitTarget, CombatContactPoint?> contact,
            CombatContactPoint? environmentContact, DeliveryMode? deliveryMode, string channel)
        {
            try
            {
                var receipts=outcomes.Select(outcome=>
                {
                    var target=targets.First(t=>t.Health.Id==outcome.TargetId);
                    var position=target is ICombatTarget combat?combat.WorldPosition:target is Component component&&component!=null?component.transform.position:origin;
                    string material = Ossastra.Audio.SideViewFeedbackRuntime.MaterialOf(target as Component, false);
                    return new CombatContactReceipt(outcome,target as CombatVitals,position,contact?.Invoke(target),material);
                });
                return new CombatContactBatch(release.ParentExecutionId,release.ActionId,release.EmissionIndex,
                    release.ActorId,source,origin,receipts,cause,effectSemantic,geometry,environmentContact,
                    deliveryMode?.ToString() ?? (geometry?.BasicAttackStage > 0 ? "Sector" : null),channel);
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
