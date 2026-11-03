/* 功能：按明确owner和相机发布环境快照；供渲染方读取，不查场景、不替换管线。
 * 生命周期：唯一owner可更新/撤销；销毁相机的托管包装器仍按稳定ID归还登记，子系统重建时清空。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Ossastra.EnvironmentSimulation
{
 public static class EnvironmentCameraRegistry
 {
  sealed class Entry{internal object owner;internal Camera camera;internal EnvironmentFrame frame;}
  static readonly Dictionary<int,Entry> entries=new Dictionary<int,Entry>();
  public static event Action<int> Released;
  public static event Action<Camera> Published;
  public static int RegisteredCameraCount=>entries.Count;
  public static void Publish(object owner,Camera camera,EnvironmentFrame frame)
  {
   if(owner==null||camera==null||frame==null)throw new ArgumentException("环境登记需要明确owner、相机与快照。");
   int id=camera.GetInstanceID();
   if(entries.TryGetValue(id,out var entry))
   {if(!ReferenceEquals(entry.owner,owner)||entry.camera!=camera)throw new InvalidOperationException("该相机已有其它环境根。");entry.frame=frame;}
   else entries.Add(id,new Entry{owner=owner,camera=camera,frame=frame});
   Published?.Invoke(camera);
  }
  public static bool TryGet(Camera camera,out EnvironmentFrame frame)
  {
   frame=null;if(camera==null||!entries.TryGetValue(camera.GetInstanceID(),out var entry)||entry.camera!=camera)return false;
   frame=entry.frame;return frame!=null;
  }
  public static void Release(object owner,Camera camera)
  {
   if(owner==null||ReferenceEquals(camera,null))return;int id=camera.GetInstanceID();
   if(!entries.TryGetValue(id,out var entry)||!ReferenceEquals(entry.owner,owner))return;
   entries.Remove(id);Released?.Invoke(id);Published?.Invoke(camera);
  }
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
  static void Reset(){var ids=new List<int>(entries.Keys);entries.Clear();foreach(int id in ids)Released?.Invoke(id);}
 }
}
