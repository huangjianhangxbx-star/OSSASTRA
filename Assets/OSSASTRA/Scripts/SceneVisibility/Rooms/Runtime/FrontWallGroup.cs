/* 功能：作者明确登记可按相机方向剔除的墙段；不按角色圆窗或Renderer名称猜墙。
 * 边界：只服务当前全可视房间；共墙以实际种子所在侧判前向，不扩大另一侧权限。碰撞/遮光不变。
 */
using System;
using UnityEngine;
namespace Ossastra.SceneVisibility.Rooms
{
 [DisallowMultipleComponent]
 public sealed class FrontWallGroup : MonoBehaviour
 {
  public string wallId;
  public RoomRegion primaryRoom, secondaryRoom;
  [Tooltip("从primaryRoom指向墙外；按本组件局部方向登记。")]
  public Vector3 localOutwardNormal=Vector3.forward;
  public bool allowHide=true;
  public Renderer[] members=Array.Empty<Renderer>();
  public Vector3 OutwardNormal=>transform.TransformDirection(localOutwardNormal).normalized;
 }
}
