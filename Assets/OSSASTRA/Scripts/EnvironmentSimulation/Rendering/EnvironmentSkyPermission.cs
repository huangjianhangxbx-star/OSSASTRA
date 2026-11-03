/* 功能：以建筑、相机窗洞与结构盒证明天空射线许可；与GPU背景使用同一保守规则。
 * 边界：只读当帧许可，不读颜色/深度，不授予角色视野或未知房间内容。
 */
using UnityEngine;
using Ossastra.SceneVisibility.Rooms.Rendering;
namespace Ossastra.EnvironmentSimulation.Rendering
{
 public static class EnvironmentSkyPermission
 {
  public static bool Allows(RoomMaskFrame frame,Vector3 origin,Vector3 direction)
  {
   if(frame==null||!frame.Enabled||!frame.PositiveRenderingEnabled||direction.sqrMagnitude<.001f)return false;
   direction.Normalize();Vector3 far=origin+direction*10000;
   if(!frame.ExteriorView)
   {
    bool aperture=false;
    foreach(var domain in frame.WindowSightDomains)
    {
     if(!domain.TargetExterior||!domain.IsCameraWindow||domain.Planes.Length<4)continue;
     bool inside=true;foreach(var plane in domain.Planes)if(Vector3.Dot(new Vector3(plane.x,plane.y,plane.z),far)+plane.w<0){inside=false;break;}
     if(inside){aperture=true;break;}
    }
    if(!aperture)return false;
   }
   foreach(var box in frame.BuildingVolumes)
   {
    // 室内窗景由洞口约束离开自身建筑；其它建筑仍截断天空背景。
    if(!frame.ExteriorView&&Contains(box,frame.ObserverPosition))continue;
    if(Intersects(box,origin,direction))return false;
   }
   foreach(var box in frame.CameraOpaqueBlockers)if(Intersects(box,origin,direction))return false;
   return true;
  }
  static bool Contains(RoomMaskVolume box,Vector3 p){p=box.WorldToLocal.MultiplyPoint3x4(p);return Mathf.Abs(p.x)<=box.HalfExtents.x&&Mathf.Abs(p.y)<=box.HalfExtents.y&&Mathf.Abs(p.z)<=box.HalfExtents.z;}
  static bool Intersects(RoomMaskVolume box,Vector3 origin,Vector3 direction)
  {
   var p=box.WorldToLocal.MultiplyPoint3x4(origin);var d=box.WorldToLocal.MultiplyVector(direction);float entry=.01f,leave=10000;
   for(int axis=0;axis<3;axis++){if(Mathf.Abs(d[axis])<1e-7f){if(Mathf.Abs(p[axis])>box.HalfExtents[axis])return false;}else{float a=(-box.HalfExtents[axis]-p[axis])/d[axis],b=(box.HalfExtents[axis]-p[axis])/d[axis];entry=Mathf.Max(entry,Mathf.Min(a,b));leave=Mathf.Min(leave,Mathf.Max(a,b));}}
   return leave>=entry;
  }
 }
}
