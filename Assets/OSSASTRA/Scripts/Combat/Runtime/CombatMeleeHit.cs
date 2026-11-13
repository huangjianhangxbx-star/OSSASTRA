/*
 * 脚本：CombatMeleeHit
 * 功能：将一次释放转换为物理候选、可用接触估计和唯一伤害批次。横版空目标攻击可记录实际墙面接触。
 * 边界：中立能力，无玩家Root依赖，不建动作时钟或HP；空挥也关闭Release。
 * 协作：两类战斗根各自释放；空间/LOS通过后交统一防御网关；回执仅描述是否Applied；连段是否要求命中由角色配置决定，不由命中适配器决定。
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
            => Resolve(release, releaseDirection, 0);

        internal void Resolve(AttackRelease release, Vector3 releaseDirection, int basicAttackStage)
        {
            if(release==null) throw new ArgumentNullException(nameof(release));
            var source=GetComponent<CombatVitals>();
            if(!isActiveAndEnabled || (source!=null && (!source.IsAvailable || source.Health.Id!=release.ActorId)))
            { release.Resolve(Array.Empty<Health>()); LastResult="来源失效或释放身份不符"; Resolved?.Invoke(release.ActionId,false); return; }
            var plane = CombatSpatialTarget.PlaneFor(source);
            var geometry = new CombatContactGeometry(plane, transform.position, releaseDirection, releaseDirection, basicAttackStage);
            CombatContactGateway.PublishSectorRelease(release, source, geometry, height);
            var reachDirection = plane == CombatPlane.SideXY ? geometry.ReleaseDirection : releaseDirection;
            var weaponOrigin = geometry.ReleaseOrigin + Vector3.up * height;
            var center = weaponOrigin + reachDirection * reach;
            var overlaps = Physics.OverlapSphere(center, radius, targetLayers, QueryTriggerInteraction.Collide);
            var candidates = CombatSpatialTarget.OverlapContacts(overlaps, center,
                target => target is MonoBehaviour body && CombatSight.IsClear(transform, body.transform, height), out var points, plane);
            var environment = plane == CombatPlane.SideXY &&
                !candidates.Any(target => CombatContactGateway.AllowsTargetFaction(release, source, target))
                ? TraceEnvironment(weaponOrigin, reachDirection, overlaps)
                : null;
            var outcomes = CombatContactGateway.Resolve(release, source, candidates, geometry.ReleaseOrigin,
                CombatContactCause.Direct, null, geometry, target => points[target], environment);
            LastResult=outcomes.Count==0?(environment.HasValue?"接触环境／无伤害目标":"空挥／无合法敌对目标"):string.Join("; ",outcomes.Select(o=>
                o.ActorId+" → "+o.TargetId+" "+o.HpBefore+"→"+o.HpAfter+" "+o.Reason+" #"+o.ActionId.ToString("N")));
            Resolved?.Invoke(release.ActionId,outcomes.Any(o=>o.Reason==HitReason.Applied));
        }

        private CombatContactPoint? TraceEnvironment(Vector3 origin, Vector3 direction, Collider[] overlaps)
        {
            if (direction.sqrMagnitude < .0001f || overlaps.Length == 0) return null;
            RaycastHit? nearest = null;
            // 限定在这次实际近战查询覆盖的Collider内，并要求刀口射线先接触它。
            // 该射线只补点和法线，不建立第二次受击判定。
            foreach (var hit in Physics.RaycastAll(origin, direction, reach + radius, targetLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || nearest.HasValue && hit.distance >= nearest.Value.distance) continue;
                nearest = hit;
            }
            if (!nearest.HasValue) return null;
            var surface = nearest.Value;
            if (!overlaps.Contains(surface.collider) || CombatSpatialTarget.Find(surface.collider) != null) return null;
            return new CombatContactPoint(surface.point, CombatContactPointQuality.PhysicsCast, surface.normal);
        }
    }
}
