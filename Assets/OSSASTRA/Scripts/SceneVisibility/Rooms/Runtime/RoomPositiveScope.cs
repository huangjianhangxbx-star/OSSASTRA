/* 功能：在唯一相机回调内暂借Renderer渲染标记及阴影权限。
 * 边界：不改GameObject层、碰撞或材质；只恢复仍等于本scope写入的值。
 * 生命周期：Begin前清旧scope，End/异常/卸载逆序归还，禁止复活外部已销毁对象。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace Ossastra.SceneVisibility.Rooms
{
 internal sealed class RoomPositiveScope:IDisposable
 {
  struct Entry {public Renderer R;public uint Before,Written;public ShadowCastingMode ShadowBefore,ShadowWritten;}
  readonly List<Entry> entries=new List<Entry>(512);
  public void Apply(Renderer r,uint bits,bool suppressShadow)
  {
   if(r==null||!r.enabled||!r.gameObject.activeInHierarchy||r.forceRenderingOff)return;
   var e=new Entry{R=r,Before=r.renderingLayerMask,Written=r.renderingLayerMask|bits,ShadowBefore=r.shadowCastingMode};
   e.ShadowWritten=suppressShadow?ShadowCastingMode.Off:e.ShadowBefore;
   entries.Add(e);r.renderingLayerMask=e.Written;r.shadowCastingMode=e.ShadowWritten;
  }
  public void Dispose(){for(int i=entries.Count-1;i>=0;i--){var e=entries[i];if(e.R==null)continue;if(e.R.renderingLayerMask==e.Written)e.R.renderingLayerMask=e.Before;if(e.R.shadowCastingMode==e.ShadowWritten)e.R.shadowCastingMode=e.ShadowBefore;}entries.Clear();}
 }
}
