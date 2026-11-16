using System.Collections.Generic;
using System.Linq;
using Ossastra.CharacterCore;
using Ossastra.Combat;
using UnityEngine;

namespace Ossastra.Characters
{
    /// <summary>俯冲攻击球沿实际固定步路径前进，按距离结算至首个合法敌对直击。</summary>
    internal static class SideViewDiveSweep
    {
        private readonly struct Contact
        {
            public readonly IHitTarget Target;
            public readonly CombatContactPoint Point;
            public readonly float Distance;
            public Contact(IHitTarget target, CombatContactPoint point, float distance)
            { Target = target; Point = point; Distance = distance; }
        }

        public static bool Resolve(AttackRelease release, CombatVitals source, CombatContactGeometry geometry,
            Vector3 attackEnd, float height, float radius, LayerMask layers, bool terrainBlocked)
        {
            if (release == null || source == null || !source.IsAvailable) return false;
            var start = geometry.ReleaseOrigin + Vector3.up * height;
            var travel = attackEnd - geometry.ReleaseOrigin;
            travel.z = 0;
            var distance = travel.magnitude;
            var direction = distance > .0001f ? travel / distance : geometry.ReleaseDirection;
            var contacts = new List<Contact>();
            foreach (var overlap in Physics.OverlapSphere(start, radius, layers, QueryTriggerInteraction.Collide))
            {
                var target = CombatSpatialTarget.Find(overlap);
                if (target == null || !ValidEnemy(target, source, release)) continue;
                var point = overlap is MeshCollider mesh && !mesh.convex ? overlap.bounds.ClosestPoint(start) : overlap.ClosestPoint(start);
                contacts.Add(new Contact(target, new CombatContactPoint(point, CombatContactPointQuality.EstimatedOverlap), 0));
            }

            var terrain = terrainBlocked ? distance : float.PositiveInfinity;
            if (distance > .0001f)
                foreach (var hit in Physics.SphereCastAll(start, radius, direction, distance, layers, QueryTriggerInteraction.Collide))
                {
                    var target = CombatSpatialTarget.Find(hit.collider);
                    if (target == null)
                    {
                        if (!hit.collider.isTrigger && !hit.collider.transform.IsChildOf(source.transform) &&
                            hit.distance > .0001f)
                            terrain = Mathf.Min(terrain, hit.distance);
                        continue;
                    }
                    if (hit.distance <= .0001f || !ValidEnemy(target, source, release)) continue;
                    contacts.Add(new Contact(target, new CombatContactPoint(hit.point, CombatContactPointQuality.PhysicsCast, hit.normal), hit.distance));
                }

            var nearest = contacts.Where(c => SideViewDiveRules.IsBeforeObstruction(c.Distance, terrain) &&
                    HasLineOfContact(source, start + direction * c.Distance, c.Point.Position, layers))
                .GroupBy(c => c.Target).Select(g => g.OrderBy(c => c.Distance).First())
                .OrderBy(c => c.Distance).ThenBy(c => c.Target.Health.Id, System.StringComparer.Ordinal);
            var groups = nearest.GroupBy(c => c.Target.Health.Id).Where(g => g.Count() == 1).Select(g => g.First());
            foreach (var contact in groups)
            {
                var outcomes = CombatContactGateway.Resolve(release, source, new[] { contact.Target },
                    start + direction * contact.Distance, CombatContactCause.Direct, "side-view-dive",
                    geometry, _ => contact.Point, null);
                if (outcomes.Any(o => o.Reason == HitReason.Applied)) return true;
            }
            return false;
        }

        private static bool ValidEnemy(IHitTarget target, CombatVitals source, AttackRelease release)
            => target is CombatVitals vitals && vitals.Faction == CombatFaction.Enemy &&
                vitals.gameObject.scene == source.gameObject.scene && vitals.IsAvailable &&
                CombatContactGateway.AllowsTargetFaction(release, source, target);

        private static bool HasLineOfContact(CombatVitals source, Vector3 origin, Vector3 contact, LayerMask layers)
        {
            var delta = contact - origin;
            var length = delta.magnitude;
            return length < .002f || !Physics.RaycastAll(origin, delta / length, length, layers, QueryTriggerInteraction.Ignore)
                .Any(hit => !hit.collider.transform.IsChildOf(source.transform) && CombatSpatialTarget.Find(hit.collider) == null);
        }
    }
}
