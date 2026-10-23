/* 脚本：CombatStatusState
 * 功能：按来源保存有限的眩晕与减速时间，重复接触刷新而不叠乘速度。
 * 边界：不读Unity时间、不移动、不结算生命；根显式传入暂停后的delta。
 * 协作：CombatStatusEffects把结果交给各实体原有唯一运动/控制端口。
 */
using System;
using System.Collections.Generic;
namespace Ossastra.Combat
{
    public sealed class CombatStatusState
    {
        private sealed class Timed { public double remaining,ratio; }
        private readonly Dictionary<Guid,Timed> slows=new Dictionary<Guid,Timed>();
        private readonly Dictionary<Guid,Timed> stuns=new Dictionary<Guid,Timed>();
        private readonly List<Guid> expired=new List<Guid>();
        public bool IsStunned=>stuns.Count>0;
        public double SpeedScale { get {double strongest=0;foreach(var s in slows.Values)strongest=Math.Max(strongest,s.ratio);return 1-strongest;} }
        private static bool Positive(double x)=>!double.IsNaN(x)&&!double.IsInfinity(x)&&x>0;
        public bool ApplySlow(Guid id,double ratio,double seconds)
        {
            if(id==Guid.Empty||!Positive(ratio)||ratio>1||!Positive(seconds))return false;
            if(!slows.TryGetValue(id,out var effect))slows[id]=effect=new Timed();
            effect.ratio=Math.Max(effect.ratio,ratio);effect.remaining=Math.Max(effect.remaining,seconds);return true;
        }
        public bool ApplyStun(Guid id,double seconds)
        {
            if(id==Guid.Empty||!Positive(seconds))return false;
            if(!stuns.TryGetValue(id,out var effect))stuns[id]=effect=new Timed();
            effect.remaining=Math.Max(effect.remaining,seconds);return true;
        }
        public void Tick(double delta)
        {if(!Positive(delta))return;Advance(slows,delta);Advance(stuns,delta);}
        private void Advance(Dictionary<Guid,Timed> list,double delta)
        {
            expired.Clear();foreach(var p in list){p.Value.remaining-=delta;if(p.Value.remaining<=0)expired.Add(p.Key);}
            foreach(var id in expired)list.Remove(id);
        }
        public void Clear(){slows.Clear();stuns.Clear();expired.Clear();}
        public void Remove(Guid id){slows.Remove(id);stuns.Remove(id);}
    }
}
