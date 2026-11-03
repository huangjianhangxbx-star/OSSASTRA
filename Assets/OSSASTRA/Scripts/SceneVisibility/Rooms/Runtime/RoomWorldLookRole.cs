/* 功能：为合法世界环境后处理划分显示角色，固定/可破坏场景物可参与，人物与信息保持清晰。
 * 边界：与黑幕静态历史白名单独立，不授予任何视野或交互权限；不改原材质和对象状态。
 */
using UnityEngine;
namespace Ossastra.SceneVisibility.Rooms
{
 public static class RoomWorldLookRole
 {
  public static bool IsEnvironment(Renderer renderer,bool informationPresentation)
  {
   if(renderer==null||informationPresentation||renderer is ParticleSystemRenderer)return false;
   if(renderer.GetComponentInParent<Ossastra.Characters.CharacterRoot>(true)!=null||
      renderer.GetComponentInParent<Ossastra.Enemies.EnemyRoot>(true)!=null||
      renderer.GetComponentInParent<Ossastra.Npcs.NpcRoot>(true)!=null||
      renderer.GetComponentInParent<Spine.Unity.SkeletonRenderer>(true)!=null)return false;
   return true;
  }
 }
}
