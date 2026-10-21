/* 脚本：StaminaState
 * 功能：每本体唯一体力余额、按接受身份去重支付、模拟时钟恢复。
 * 边界：不接受动作、不读输入；调用者先验证动作资格再同步付款；暂停不推进时间。
 */
using System;
using System.Collections.Generic;
namespace Ossastra.CharacterCore
{
    public sealed class StaminaState
    {
        private readonly HashSet<Guid> spent=new HashSet<Guid>();
        private readonly double delay,rate;
        private double time,recoverAt;
        public double Current { get; private set; }
        public double Maximum { get; }
        public Guid LastToken { get; private set; }
        public StaminaState(double maximum,double delay,double rate)
        {
            if(!BasicAttackDefinition.Finite(maximum)||maximum<=0||!BasicAttackDefinition.Finite(delay)||delay<0||!BasicAttackDefinition.Finite(rate)||rate<0)throw new ArgumentOutOfRangeException(nameof(maximum));
            Maximum=Current=maximum;this.delay=delay;this.rate=rate;
        }
        public bool CanSpend(double amount)=>BasicAttackDefinition.Finite(amount)&&amount>=0&&Current>=amount;
        public bool TrySpend(Guid token,double amount,double now)
        {
            if(token==Guid.Empty||!CanSpend(amount)||!BasicAttackDefinition.Finite(now)||now<time||spent.Contains(token))return false;
            Current-=amount;spent.Add(token);LastToken=token;recoverAt=now+delay;return true;
        }
        public void Tick(double now,bool canRecover)
        {
            if(!BasicAttackDefinition.Finite(now)||now<time)throw new ArgumentOutOfRangeException(nameof(now));
            if(canRecover)Current=Math.Min(Maximum,Current+Math.Max(0,now-Math.Max(time,recoverAt))*rate);
            time=now;
        }
        public void Restore(){Current=Maximum;recoverAt=time+delay;}
    }
}
