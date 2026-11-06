/* 功能：按明确owner和相机提交已准备环境快照，隔离每个可选消费者的通知故障。
 * 边界：登记失败可拒绝提交；订阅者故障仅记录来源，不撤销有效上游，不查场景或替换管线。
 * 生命周期：唯一owner可更新/撤销；先移除失效登记再通知，单个清理订阅者不能阻断其他清理。
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
  public static long ConsumerFaultCount{get;private set;}
  public static string LastConsumerFault{get;private set;}="";
  // 根在修改原生灯之前预检租约；此方法没有通知、提交或外部副作用。
  public static void ValidatePublish(object owner,Camera camera,EnvironmentFrame frame)
  {
   if(owner==null||camera==null||frame==null)throw new ArgumentException("环境登记需要明确owner、相机与快照。");
   int id=camera.GetInstanceID();
   if(entries.TryGetValue(id,out var entry)&&(!ReferenceEquals(entry.owner,owner)||entry.camera!=camera))throw new InvalidOperationException("该相机已有其它环境根。");
  }
  public static void Publish(object owner,Camera camera,EnvironmentFrame frame)
  {
   ValidatePublish(owner,camera,frame);int id=camera.GetInstanceID();
   if(entries.TryGetValue(id,out var entry))entry.frame=frame;
   else entries.Add(id,new Entry{owner=owner,camera=camera,frame=frame});
   NotifyPublished(camera);
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
   entries.Remove(id);NotifyReleased(id);NotifyPublished(camera);
  }
  // 异常隔离只放在外部消费者边界；有界历史诊断替代每帧刷日志，不吞上游校验错误。
  static void RecordFault(string phase,Delegate observer,Exception error)
  {
   if(ConsumerFaultCount<long.MaxValue)ConsumerFaultCount++;
   string message=error.Message??"";if(message.Length>256)message=message.Substring(0,256);
   LastConsumerFault=phase+" / "+observer.Method.DeclaringType?.FullName+"."+observer.Method.Name+" / "+error.GetType().Name+": "+message;
  }
  static void NotifyPublished(Camera camera)
  {
   var listeners=Published;if(listeners==null)return;
   foreach(Action<Camera> listener in listeners.GetInvocationList())
    try{listener(camera);}catch(Exception error){RecordFault("Published",listener,error);}
  }
  static void NotifyReleased(int id)
  {
   var listeners=Released;if(listeners==null)return;
   foreach(Action<int> listener in listeners.GetInvocationList())
    try{listener(id);}catch(Exception error){RecordFault("Released",listener,error);}
  }
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
  static void Reset(){var ids=new List<int>(entries.Keys);entries.Clear();ConsumerFaultCount=0;LastConsumerFault="";foreach(int id in ids)NotifyReleased(id);}
 }
}
