/* 脚本：CombatSpatialTarget
 * 功能：物理Collider解析为中立命中目标，让冰柱等场景实体与角色共享空间入口。
 * 边界：不创建角色根、不筛阵营、不扣血；最终身份/来源过滤仍在CombatContactGateway。
 */
using System.Linq;
using UnityEngine;
namespace Ossastra.Combat
{
 public static class CombatSpatialTarget
 {
  public static IHitTarget Find(Collider collider)=>collider==null?null:collider.GetComponentsInParent<MonoBehaviour>().OfType<IHitTarget>().FirstOrDefault();
  public static Vector3 Position(IHitTarget target)=>target is ICombatTarget combat?combat.WorldPosition:((MonoBehaviour)target).transform.position;
 }
}
