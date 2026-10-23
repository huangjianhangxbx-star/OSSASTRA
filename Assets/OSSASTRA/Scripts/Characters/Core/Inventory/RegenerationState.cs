/* 脚本：RegenerationState
 * 功能：把一次有效恢复按模拟delta累计成整数HP，限制时长、互斥与执行去重。
 * 边界：Health仍是唯一HP；不复活，不退款，不持有输入或物品数量。
 */
using System;
using System.Collections.Generic;
namespace Ossastra.Inventory
{
    public sealed class RegenerationState
    {
        private readonly Ossastra.CharacterCore.Health health;
        private readonly HashSet<Guid> accepted=new HashSet<Guid>();
        private double rate,remaining,carry;
        public bool Active=>remaining>0&&health.Current>0;
        public double Remaining=>remaining;
        public RegenerationState(Ossastra.CharacterCore.Health health){this.health=health??throw new ArgumentNullException(nameof(health));}
        public bool CanBegin(double perSecond,double seconds)=>!Active&&health.Current>0&&health.Current<health.Maximum&&Finite(perSecond)&&Finite(seconds)&&perSecond>0&&seconds>0;
        public bool TryBegin(Guid execution,double perSecond,double seconds)
        {if(execution==Guid.Empty||accepted.Contains(execution)||!CanBegin(perSecond,seconds))return false;accepted.Add(execution);rate=perSecond;remaining=seconds;carry=0;return true;}
        public void Tick(double delta,bool paused)
        {
            if(!Finite(delta)||delta<0)throw new ArgumentException("Invalid regeneration delta");
            if(health.Current<=0){remaining=carry=0;return;}if(paused||!Active)return;
            var step=Math.Min(delta,remaining);remaining=Math.Max(0,remaining-step);carry+=step*rate;
            var whole=(int)Math.Min(int.MaxValue,Math.Floor(carry+1e-8));if(whole>0){health.Heal(whole);carry-=whole;}
        }
        public void Clear(){remaining=carry=0;}
        private static bool Finite(double n)=>!double.IsNaN(n)&&!double.IsInfinity(n);
    }
}
