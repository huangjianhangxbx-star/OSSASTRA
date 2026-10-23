/* 脚本：AreaContactClock
 * 功能：同一来源的重叠区域共用每目标脉冲时钟；退出区域才释放该接触。
 * 边界：只接收已推进的模拟时间，不查物理、不写HP；暂停时重复时间不产生第二脉冲。
 */
using System;
using System.Collections.Generic;
using System.Linq;
namespace Ossastra.CharacterCore
{
 public sealed class AreaContactClock
 {
  private readonly Dictionary<string,double> next=new Dictionary<string,double>(StringComparer.Ordinal);
  private readonly double interval;
  public int Count=>next.Count;
  public AreaContactClock(double interval){if(double.IsNaN(interval)||double.IsInfinity(interval)||interval<=0)throw new ArgumentException("Invalid interval");this.interval=interval;}
  public bool TryPulse(string target,double now)
  {
   if(string.IsNullOrWhiteSpace(target)||double.IsNaN(now)||double.IsInfinity(now)||now<0)throw new ArgumentException("Invalid contact clock");
   if(!next.TryGetValue(target,out var due))due=now;
   if(now+1e-9<due)return false;
   next[target]=due+interval;return true;
  }
  public void Retain(IEnumerable<string> present){var ids=new HashSet<string>(present,StringComparer.Ordinal);foreach(var id in next.Keys.Where(k=>!ids.Contains(k)).ToArray())next.Remove(id);}
  public void Clear()=>next.Clear();
 }
}
