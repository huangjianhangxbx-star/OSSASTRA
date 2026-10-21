/* 脚本：DodgeState
 * 功能：只按本体模拟时间保存一次闪避及半开免伤窗口。
 * 边界：不驱动物理、不付款；取消立即撤销窗口，结束不保留免伤。
 */
using System;
namespace Ossastra.CharacterCore
{
    public sealed class DodgeState
    {
        private readonly double duration,from,to;
        private double start,now;
        private bool active;
        public bool IsActive=>active;
        public bool IsInvulnerable=>active&&now-start>=from&&now-start<to;
        public DodgeState(double duration,double from,double to)
        {if(!BasicAttackDefinition.Finite(duration)||!BasicAttackDefinition.Finite(from)||!BasicAttackDefinition.Finite(to)||duration<=0||from<0||to<from||to>duration)throw new ArgumentOutOfRangeException(nameof(duration));this.duration=duration;this.from=from;this.to=to;}
        public bool TryStart(double time){if(active||!BasicAttackDefinition.Finite(time))return false;start=now=time;active=true;return true;}
        public void Tick(double time){if(!BasicAttackDefinition.Finite(time)||time<now)throw new ArgumentOutOfRangeException(nameof(time));now=time;if(active&&now-start>=duration)active=false;}
        public void Cancel(){active=false;}
    }
}
