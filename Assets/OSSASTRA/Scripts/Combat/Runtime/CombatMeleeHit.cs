/*
 * 脚本：CombatMeleeHit
 * 功能：将一次释放转换为物理候选、身份/阵营资格和唯一伤害批次。
 * 边界：中立能力，无玩家Root依赖，不建动作时钟或HP；空挥也关闭Release。
 * 协作：两类战斗根各自释放；Applied先反馈/死亡，存活者再接可选架势，不重复扣血。
 */
using System;
using System.Linq;
using Ossastra.CharacterCore;
using UnityEngine;
namespace Ossastra.Combat
{
    public sealed class CombatMeleeHit : MonoBehaviour, ICombatHitResolver, ICombatHitReceiptSource
    {
        [SerializeField, Min(0)] private float reach=.8f;
        [SerializeField, Min(.01f)] private float radius=.65f;
        [SerializeField] private float height=.8f;
        [SerializeField] private LayerMask targetLayers=~0;
        public string LastResult { get; private set; }="未释放";
        public event Action<Guid, bool> Resolved;
        public float Reach => reach;
        public float Radius => radius;
        public float Height => height;
        public void Resolve(AttackRelease release, Vector3 releaseDirection)
        {
            if(release==null) throw new ArgumentNullException(nameof(release));
            var source=GetComponent<CombatVitals>();
            if(!isActiveAndEnabled || (source!=null && (!source.IsAvailable || source.Health.Id!=release.ActorId)))
            { release.Resolve(Array.Empty<Health>()); LastResult="来源失效或释放身份不符"; Resolved?.Invoke(release.ActionId,false); return; }
            var center=transform.position+Vector3.up*height+releaseDirection*reach;
            var candidates=Physics.OverlapSphere(center,radius,targetLayers,QueryTriggerInteraction.Collide)
                .Select(c=>c.GetComponentsInParent<MonoBehaviour>().OfType<IHitTarget>().FirstOrDefault())
                .Where(CombatTargetValidity.IsAvailable).Distinct()
                .Where(t=>t is MonoBehaviour body && CombatSight.IsClear(transform,body.transform,height)).ToArray();
            // 同一接收者多个Collider合并；不同接收者冒用同ID整组拒绝，不能任取一个扣HP。
            var targets=candidates.GroupBy(t=>t.Health.Id,StringComparer.Ordinal)
                .Where(g=>g.Count()==1).Select(g=>g.Single())
                .Where(t=>CombatTargetPolicy.CanHit(release.ActorId,
                    source==null?CombatFaction.Neutral:source.Faction,source==null||source.IsAlive,
                    t.Health.Id,t is ICombatTarget combat?combat.Faction:CombatFaction.Neutral,true)).ToArray();
            var outcomes=release.Resolve(targets.Select(t=>t.Health));
            foreach(var outcome in outcomes)
                if(outcome.Reason==HitReason.Applied)
                {
                    var target=targets.First(t=>t.Health.Id==outcome.TargetId);
                    if(target is ICombatTarget combat) combat.OnHitResolved(outcome); else target.ShowHit();
                    // HP/死亡通知已完成；同批存活者才消费架势。不会再做空间查询或伤害结算。
                    if (outcome.HpAfter > 0 && target is MonoBehaviour component && component != null)
                        foreach (var receiver in component.GetComponents<MonoBehaviour>().OfType<ICombatPostureReceiver>())
                            receiver.ReceiveImpact(outcome, release.PostureImpact);
                }
            LastResult=outcomes.Count==0?"空挥／无合法敌对目标":string.Join("; ",outcomes.Select(o=>
                o.ActorId+" → "+o.TargetId+" "+o.HpBefore+"→"+o.HpAfter+" "+o.Reason+" #"+o.ActionId.ToString("N")));
            Resolved?.Invoke(release.ActionId,outcomes.Any(o=>o.Reason==HitReason.Applied));
        }
    }
}
