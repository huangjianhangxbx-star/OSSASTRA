/* 功能：把原有视觉叶对象的生灭接入可选建筑表现登记，支持固定/跟随锚点。
 * 边界：无 Update、无查场景、无输入/物理/命中/库存权；不改变原 Renderer 或材质设置。
 * 生命周期：配置后随 Enable 登记，Disable/Destroy 精确撤销；不接管业务对象销毁。
 */
using UnityEngine;

namespace Ossastra.SceneVisibility.Rooms
{
 [DisallowMultipleComponent]
 public sealed class RoomPresentationBinding:MonoBehaviour
 {
  Renderer member;
  Transform follow;
  Vector3 anchor;
  bool configured,partial,requirePermission;

  public static RoomPresentationBinding Attach(Renderer renderer,Vector3 worldAnchor,bool forcePartial,
   Transform followAnchor=null,bool requireAnchorPermission=false)
  {
   if(renderer==null)return null;
   var binding=renderer.GetComponent<RoomPresentationBinding>();
   if(binding==null)binding=renderer.gameObject.AddComponent<RoomPresentationBinding>();
   binding.Configure(renderer,worldAnchor,forcePartial,followAnchor,requireAnchorPermission);
   return binding;
  }
  public void Configure(Renderer renderer,Vector3 worldAnchor,bool forcePartial,Transform followAnchor=null,bool requireAnchorPermission=false)
  {
   RoomPresentationRegistry.Release(this,member);
   member=renderer;anchor=worldAnchor;partial=forcePartial;follow=followAnchor;requirePermission=requireAnchorPermission;
   configured=renderer!=null;
   if(configured&&isActiveAndEnabled)Register();
  }
  void Register()=>RoomPresentationRegistry.Register(this,member,anchor,partial,follow,requirePermission);
  public void Suspend()=>RoomPresentationRegistry.Release(this,member);
  void OnEnable(){if(configured)Register();}
  void OnDisable()=>Suspend();
  void OnDestroy(){RoomPresentationRegistry.Release(this,member);member=null;follow=null;configured=false;}
 }
}
