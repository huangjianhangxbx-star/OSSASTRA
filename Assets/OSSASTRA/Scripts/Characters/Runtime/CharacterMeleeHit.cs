/*
 * 脚本：CharacterMeleeHit
 * 功能：将一次释放的方向快照转换为物理候选、敌我资格与唯一伤害批次。
 * 边界：不建动作时钟/HP；来源死亡或身份不符只关闭空批次，不产生新伤害。
 * 协作：Root和敌人共用Released入口；AttackRelease扣血后通知真实目标，开发靶保留原ShowHit。
 */
using System;
using System.Linq;
using Ossastra.CharacterCore;
using UnityEngine;
namespace Ossastra.Characters
{
    public sealed class CharacterMeleeHit : MonoBehaviour, ICharacterHitResolver
    {
        [SerializeField, Min(0)] private float reach=.8f;
        [SerializeField, Min(.01f)] private float radius=.65f;
        [SerializeField] private float height=.8f;
        [SerializeField] private LayerMask targetLayers=~0;
        public string LastResult { get; private set; }="未释放";
        public void Resolve(AttackRelease release, Vector3 releaseDirection)
        {
            if (release==null) throw new ArgumentNullException(nameof(release));
            var source=GetComponent<CharacterVitals>();
            if (source!=null && (!source.IsAlive || source.Health.Id!=release.ActorId))
            { release.Resolve(Array.Empty<Health>()); LastResult="来源死亡或释放身份不符"; return; }
            var center=transform.position+Vector3.up*height+releaseDirection*reach;
            var targets=Physics.OverlapSphere(center,radius,targetLayers,QueryTriggerInteraction.Collide)
                .Select(c=>c.GetComponentsInParent<MonoBehaviour>().OfType<ICharacterHitTarget>().FirstOrDefault())
                .Where(t=>t!=null && t.Health!=null && CombatTargetPolicy.CanHit(release.ActorId,
                    source==null?CombatFaction.Neutral:source.Faction,source==null||source.IsAlive,
                    t.Health.Id,t is ICombatHitTarget combat?combat.Faction:CombatFaction.Neutral,t.Health.Current>0))
                .Distinct().ToArray();
            var outcomes=release.Resolve(targets.Select(t=>t.Health));
            foreach (var outcome in outcomes)
                if (outcome.Reason==HitReason.Applied)
                {
                    var target=targets.First(t=>t.Health.Id==outcome.TargetId);
                    if (target is ICombatHitTarget combat) combat.OnHitResolved(outcome); else target.ShowHit();
                }
            LastResult=outcomes.Count==0?"空挥／无合法敌对目标":string.Join("; ",outcomes.Select(o=>
                o.ActorId+" → "+o.TargetId+" "+o.HpBefore+"→"+o.HpAfter+" "+o.Reason+" #"+o.ActionId.ToString("N")));
        }
    }
}