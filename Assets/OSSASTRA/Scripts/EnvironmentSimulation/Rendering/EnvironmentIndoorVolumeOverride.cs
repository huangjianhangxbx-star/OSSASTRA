/* 功能：为已登记房间明确提供照明专用室内范围，避免可见性扩展体积把外墙与屋面误判为室内。
 * 边界：只保存作者引用；不启用Collider、不修改RoomRegion或授予可见性，不持有运行资源。
 */
using System;
using UnityEngine;
using Ossastra.SceneVisibility.Rooms;
namespace Ossastra.EnvironmentSimulation.Rendering
{
 [Serializable]
 public sealed class EnvironmentIndoorVolumeOverride
 {
  [Tooltip("必须是当前相机明确登记的有效房间；每房间最多一个覆盖。")]
  public RoomRegion room;
  [Tooltip("仅作OBB元数据；允许禁用的BoxCollider，不将其加入物理触发或可见性判定。")]
  public BoxCollider volume;
 }
}
