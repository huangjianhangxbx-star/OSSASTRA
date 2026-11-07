/* 功能：在接管绘制前准备单相机的有界环境空间与点灯工作集；只读作者登记，不拥有Unity/GPU资源。
 * 边界：不截断超预算条目、不授予可见权。照明专用室内覆盖与RoomRegion可见性体积独立，灯仍匹配本工作集实际室内序号。
 * 作者覆盖：每个已登记房间最多一个同场景OBB；禁用Collider可作为只读元数据，不启用碰撞；未声明者保留原房间体积。
 * 生命周期：复用固定GPU数组与CPU集合；失败使本批计数归零，调用者只能局部关闭环境表面效果。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Ossastra.SceneVisibility.Rooms;
using Ossastra.SceneVisibility.Rooms.Rendering;
namespace Ossastra.EnvironmentSimulation.Rendering
{
 internal sealed class EnvironmentSurfaceWorkingSet
 {
  const int MaximumRooms=32,MaximumLights=8,MaximumShelters=16;
  readonly Matrix4x4[] matrices=new Matrix4x4[MaximumRooms],shelterMatrices=new Matrix4x4[MaximumShelters];
  readonly Vector4[] extents=new Vector4[MaximumRooms],positions=new Vector4[MaximumLights],colors=new Vector4[MaximumLights],shelterExtents=new Vector4[MaximumShelters];
  readonly Plane[] frustum=new Plane[6];
  readonly HashSet<Light> seenLights=new HashSet<Light>();readonly HashSet<RoomRegion> seenRooms=new HashSet<RoomRegion>();readonly HashSet<BoxCollider> seenShelters=new HashSet<BoxCollider>();
  readonly Dictionary<RoomRegion,BoxCollider> indoorOverrides=new Dictionary<RoomRegion,BoxCollider>();
  int roomCount,lightCount,shelterCount;
  public void Clear(){roomCount=lightCount=shelterCount=0;seenLights.Clear();seenRooms.Clear();seenShelters.Clear();indoorOverrides.Clear();}
  public bool TryPrepare(Camera camera,RoomRegion[] rooms,Light[] lights,BoxCollider[] shelters,out string reason)
   =>TryPrepare(camera,rooms,lights,shelters,null,out reason);
  public bool TryPrepare(Camera camera,RoomRegion[] rooms,Light[] lights,BoxCollider[] shelters,EnvironmentIndoorVolumeOverride[] overrides,out string reason)
  {
   Clear();reason=null;if(camera==null){reason="缺少明确环境相机";return false;}
   if(!Finite(camera.worldToCameraMatrix)||!Finite(camera.projectionMatrix))return Fail("环境相机矩阵无效",out reason);
   foreach(var item in overrides??Array.Empty<EnvironmentIndoorVolumeOverride>())
   {
    if(item==null||item.room==null||item.volume==null)return Fail("照明室内覆盖缺少明确房间或体积",out reason);
    var room=item.room;var box=item.volume;bool registered=false;
    foreach(var candidate in rooms??Array.Empty<RoomRegion>())if(candidate==room){registered=true;break;}
    if(!registered||!room.isActiveAndEnabled)return Fail("照明室内覆盖必须属于已登记的有效房间",out reason);
    if(indoorOverrides.ContainsKey(room))return Fail("同一房间不能重复登记照明室内覆盖",out reason);
    if(!room.gameObject.scene.IsValid()||!room.gameObject.scene.isLoaded||room.gameObject.scene!=camera.gameObject.scene||box.gameObject.scene!=room.gameObject.scene)
     return Fail("照明室内覆盖必须与房间及相机同场景",out reason);
    if(!box.gameObject.activeInHierarchy||!Finite(box.center)||!Finite(box.size)||box.size.x<=0||box.size.y<=0||box.size.z<=0||!ValidScale(box.transform.lossyScale)||!Finite(box.transform.worldToLocalMatrix))
     return Fail("照明室内覆盖OBB中心/尺寸/Transform无效",out reason);
    indoorOverrides.Add(room,box);
   }
   GeometryUtility.CalculateFrustumPlanes(camera,frustum);
   foreach(var light in lights??Array.Empty<Light>())
   {
    if(light==null||!light.isActiveAndEnabled||light.type!=LightType.Point||light.range<=0||light.intensity<=0||!seenLights.Add(light))continue;
    Vector3 p=light.transform.position;Color c=light.color.linear*light.intensity;
    if(!Finite(p)||!Finite(light.range)||!Finite(c.r)||!Finite(c.g)||!Finite(c.b))return Fail("局部灯位置/范围/颜色无效",out reason);
    if(!GeometryUtility.TestPlanesAABB(frustum,new Bounds(p,Vector3.one*(light.range*2))))continue;
    if(lightCount==MaximumLights)return Fail("本相机有效点灯超过8；本帧环境表面/雨暂停，不截断灯",out reason);
    positions[lightCount]=new Vector4(p.x,p.y,p.z,light.range);colors[lightCount++]=new Vector4(c.r,c.g,c.b,0);
   }
   foreach(var room in rooms??Array.Empty<RoomRegion>())
   {
    if(room==null||!room.isActiveAndEnabled||!seenRooms.Add(room))continue;
    Vector3 scale=room.transform.lossyScale;if(!ValidScale(scale)||!Finite(room.transform.worldToLocalMatrix))return Fail("房间体积Transform无效",out reason);
    if(indoorOverrides.TryGetValue(room,out var interior))
    {
     var volume=new RoomMaskVolume{WorldToLocal=Matrix4x4.Translate(-interior.center)*interior.transform.worldToLocalMatrix,HalfExtents=interior.size*.5f};
     if(!AppendRoomVolume(volume,out reason))return false;
     continue;
    }
    foreach(var box in room.volumes??Array.Empty<RoomLocalBox>())
    {
     if(!Finite(box.center)||!Finite(box.size)||box.size.x<=0||box.size.y<=0||box.size.z<=0)return Fail("房间体积中心/尺寸无效",out reason);
     var volume=new RoomMaskVolume{WorldToLocal=Matrix4x4.Translate(-box.center)*room.transform.worldToLocalMatrix,HalfExtents=box.size*.5f};
     if(!AppendRoomVolume(volume,out reason))return false;
    }
   }
   for(int lamp=0;lamp<lightCount;lamp++)
   {var p=new Vector3(positions[lamp].x,positions[lamp].y,positions[lamp].z);var c=colors[lamp];for(int room=0;room<roomCount;room++)if(Contains(new RoomMaskVolume{WorldToLocal=matrices[room],HalfExtents=extents[room]},p)){c.w=room+1;break;}colors[lamp]=c;}
   // 雨棚作用于其下方柱体，不能仅按屋顶薄片的相机AABB剔除，否则可见地板会错误变湿。
   foreach(var box in shelters??Array.Empty<BoxCollider>())
   {
    if(box==null||!box.enabled||!box.gameObject.activeInHierarchy||!seenShelters.Add(box))continue;
    if(!Finite(box.center)||!Finite(box.size)||box.size.x<=0||box.size.y<=0||box.size.z<=0||!ValidScale(box.transform.lossyScale)||!Finite(box.transform.worldToLocalMatrix))return Fail("雨棚体积无效",out reason);
    if(shelterCount==MaximumShelters)return Fail("有效雨棚超过16；环境表面/雨暂停，不截断遮雨",out reason);
    shelterMatrices[shelterCount]=Matrix4x4.Translate(-box.center)*box.transform.worldToLocalMatrix;shelterExtents[shelterCount++]=box.size*.5f;
   }
   reason="表面就绪：相关房间 "+roomCount+"/32；点灯 "+lightCount+"/8；雨棚 "+shelterCount+"/16";return true;
  }
  bool AppendRoomVolume(RoomMaskVolume volume,out string reason)
  {
   reason=null;
   bool relevant=GeometryUtility.TestPlanesAABB(frustum,WorldBounds(volume));
   for(int i=0;!relevant&&i<lightCount;i++)relevant=Contains(volume,new Vector3(positions[i].x,positions[i].y,positions[i].z));
   if(!relevant)return true;
   if(roomCount==MaximumRooms)return Fail("本相机相关房间体积超过32；环境表面/雨暂停，不截断空间",out reason);
   matrices[roomCount]=volume.WorldToLocal;extents[roomCount++]=volume.HalfExtents;
   return true;
  }
  bool Fail(string value,out string reason){Clear();reason=value;return false;}
  public void Upload(CommandBuffer cmd)
  {
   cmd.SetGlobalInteger("_EnvironmentIndoorCount",roomCount);cmd.SetGlobalMatrixArray("_EnvironmentIndoorMatrices",matrices);cmd.SetGlobalVectorArray("_EnvironmentIndoorHalfExtents",extents);
   cmd.SetGlobalInteger("_EnvironmentLocalLightCount",lightCount);cmd.SetGlobalVectorArray("_EnvironmentLocalLightPositions",positions);cmd.SetGlobalVectorArray("_EnvironmentLocalLightColors",colors);
   cmd.SetGlobalInteger("_EnvironmentShelterCount",shelterCount);cmd.SetGlobalMatrixArray("_EnvironmentShelterMatrices",shelterMatrices);cmd.SetGlobalVectorArray("_EnvironmentShelterExtents",shelterExtents);
  }
  static bool Contains(RoomMaskVolume volume,Vector3 point){var local=volume.WorldToLocal.MultiplyPoint3x4(point);var e=volume.HalfExtents;return Mathf.Abs(local.x)<=e.x&&Mathf.Abs(local.y)<=e.y&&Mathf.Abs(local.z)<=e.z;}
  static Bounds WorldBounds(RoomMaskVolume volume)
  {
   Matrix4x4 m=volume.WorldToLocal.inverse;Vector3 e=volume.HalfExtents;
   return new Bounds(m.MultiplyPoint3x4(Vector3.zero),new Vector3(Mathf.Abs(m.m00)*e.x+Mathf.Abs(m.m01)*e.y+Mathf.Abs(m.m02)*e.z,Mathf.Abs(m.m10)*e.x+Mathf.Abs(m.m11)*e.y+Mathf.Abs(m.m12)*e.z,Mathf.Abs(m.m20)*e.x+Mathf.Abs(m.m21)*e.y+Mathf.Abs(m.m22)*e.z)*2);
  }
  static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
  static bool Finite(Vector3 p)=>Finite(p.x)&&Finite(p.y)&&Finite(p.z);
  static bool Finite(Matrix4x4 matrix){for(int i=0;i<16;i++)if(!Finite(matrix[i]))return false;return true;}
  static bool ValidScale(Vector3 scale)=>Finite(scale)&&Mathf.Min(Mathf.Abs(scale.x),Mathf.Min(Mathf.Abs(scale.y),Mathf.Abs(scale.z)))>=.0001f;
 }
}
