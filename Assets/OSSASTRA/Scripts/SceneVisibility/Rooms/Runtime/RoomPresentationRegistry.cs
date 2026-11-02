/* 功能：让原掉落、装备和短命战斗表现登记已有 Renderer 与当前世界锚点。
 * 边界：不生成业务物体、不推进效果；FX 的 ForcePartial 只收紧片元许可，不授予邻室可见权。
 * 生命周期：原表现所有者/叶适配器登记并精确撤销；Append 仅采样登记项并清理已销毁引用。
 */
using System.Collections.Generic;
using UnityEngine;
namespace Ossastra.SceneVisibility.Rooms
{
 public static class RoomPresentationRegistry
 {
  public struct Entry
  {
   public object Owner;
   public Renderer Renderer;
   public Vector3 Anchor;
   public Transform FollowAnchor;
   public bool HasFollowAnchor,ForcePartial,RequireAnchorPermission;
  }
  static readonly Dictionary<Renderer,Entry> entries=new Dictionary<Renderer,Entry>();
  static readonly List<Renderer> stale=new List<Renderer>(64);
  public static void Register(object owner,Renderer renderer,Vector3 anchor)
   =>Register(owner,renderer,anchor,false,null,false);
  public static void Register(object owner,Renderer renderer,Vector3 anchor,bool forcePartial,Transform followAnchor=null,bool requireAnchorPermission=false)
  {
   if(owner==null||renderer==null)return;
   if(entries.TryGetValue(renderer,out var old)&&!ReferenceEquals(old.Owner,owner))return;
   entries[renderer]=new Entry{Owner=owner,Renderer=renderer,Anchor=anchor,FollowAnchor=followAnchor,
    HasFollowAnchor=!ReferenceEquals(followAnchor,null),ForcePartial=forcePartial,RequireAnchorPermission=requireAnchorPermission};
  }
  public static void Release(object owner)
  {
   stale.Clear();foreach(var e in entries)if(ReferenceEquals(e.Value.Owner,owner)||Dead(e.Value))stale.Add(e.Key);
   foreach(var key in stale)entries.Remove(key);stale.Clear();
  }
  public static void Release(object owner,Renderer renderer){if(ReferenceEquals(renderer,null))return;if(entries.TryGetValue(renderer,out var entry)&&ReferenceEquals(entry.Owner,owner))entries.Remove(renderer);}
  public static void Append(List<Entry> destination)
  {
   stale.Clear();
   foreach(var pair in entries)
   {
    var entry=pair.Value;
    if(Dead(entry)){stale.Add(pair.Key);continue;}
    if(entry.HasFollowAnchor)entry.Anchor=entry.FollowAnchor.position;
    destination.Add(entry);
   }
   foreach(var key in stale)entries.Remove(key);stale.Clear();
  }
  static bool Dead(Entry entry)=>entry.Renderer==null||(entry.HasFollowAnchor&&entry.FollowAnchor==null)||
   (entry.Owner is UnityEngine.Object owner&&owner==null);
 }
}
