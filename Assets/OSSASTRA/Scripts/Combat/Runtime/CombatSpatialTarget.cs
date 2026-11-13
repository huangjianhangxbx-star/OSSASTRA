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
      out Dictionary<IHitTarget, CombatContactPoint> points, CombatPlane plane = CombatPlane.GroundXZ)
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
          // 伤害仍用原重叠估计。仅作者登记的横版实体可在当前碰撞体朝镜头的表面冻结一个画面锚点。
          Vector3? visual = plane == CombatPlane.SideXY && target is CombatVitals vitals && vitals.SideViewOpaqueImpactSurface
              ? CameraFacingContact(collider, sample, vitals.gameObject.scene.handle)
              : null;
          var point = new CombatContactPoint(sample, CombatContactPointQuality.EstimatedOverlap, null, visual);
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

  private static Vector3? CameraFacingContact(Collider collider, Vector3 point, int sceneHandle)
  {
      var camera = Camera.main;
      if (camera == null || camera.gameObject.scene.handle != sceneHandle ||
          !camera.TryGetComponent<Ossastra.Characters.SideViewCameraFollow>(out _) ||
          Mathf.Abs(camera.transform.forward.z) < .999f) return null;

      // 仅沿横版平面法线采样同一 Collider。保留接触的 X/Y，且只偏移 6 mm 避免同一表面的深度精度冲突。
      var rayDirection = Vector3.forward * Mathf.Sign(camera.transform.forward.z);
      var extent = collider.bounds.extents.z * 2f + .02f;
      if (!collider.Raycast(new Ray(point - rayDirection * extent, rayDirection), out var visible, extent + .02f))
          return null;
      return visible.point - rayDirection * .006f;
  }
 }
}
