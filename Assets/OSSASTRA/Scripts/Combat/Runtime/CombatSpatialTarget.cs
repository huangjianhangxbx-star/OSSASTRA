/* 脚本：CombatSpatialTarget
 * 功能：解析Collider及本次查询的目标点证据，让场景实体与角色共享空间入口。
 * 边界：不创建角色根、不筛阵营、不扣血；最终身份/来源过滤仍在CombatContactGateway。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Ossastra.Combat
{
 public static class CombatSpatialTarget
 {
  public static IHitTarget Find(Collider collider)=>collider==null?null:collider.GetComponentsInParent<MonoBehaviour>().OfType<IHitTarget>().FirstOrDefault();
  public static Vector3 Position(IHitTarget target)=>target is ICombatTarget combat?combat.WorldPosition:((MonoBehaviour)target).transform.position;

  internal static CombatPlane PlaneFor(CombatVitals source)
      => source != null && source.TryGetComponent<Ossastra.Characters.CharacterMovement>(out var movement) && movement.IsSideView
          ? CombatPlane.SideXY : CombatPlane.GroundXZ;

  internal static IHitTarget[] OverlapContacts(Collider[] overlaps, Vector3 sampleCenter, Predicate<IHitTarget> accepts,
      out Dictionary<IHitTarget, CombatContactPoint> points)
  {
      var candidates = new List<IHitTarget>();
      var rejected = new HashSet<IHitTarget>();
      points = new Dictionary<IHitTarget, CombatContactPoint>();
      foreach (var collider in overlaps)
      {
          var target = Find(collider);
          if (target == null || rejected.Contains(target)) continue;
          var known = points.TryGetValue(target, out var previous);
          if (!known && (!CombatTargetValidity.IsAvailable(target) || !accepts(target)))
          {
              rejected.Add(target);
              continue;
          }
          // Overlap没有射线交点；只在Unity支持ClosestPoint的几何上估计，不伪造法线。
          var exactShape = collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider ||
              collider is MeshCollider mesh && mesh.convex;
          var sample = exactShape ? collider.ClosestPoint(sampleCenter) : collider.bounds.ClosestPoint(sampleCenter);
          var point = new CombatContactPoint(sample, CombatContactPointQuality.EstimatedOverlap);
          if (!known)
          {
              candidates.Add(target);
              points.Add(target, point);
          }
          else if ((point.Position - sampleCenter).sqrMagnitude < (previous.Position - sampleCenter).sqrMagnitude)
              points[target] = point;
      }
      return candidates.ToArray();
  }
 }
}
