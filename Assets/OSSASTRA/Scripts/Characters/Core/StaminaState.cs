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
        private readonly Dictionary<Guid,double> reservations=new Dictionary<Guid,double>();
        private double reserved;
        private readonly double delay,rate;
        private double time,recoverAt;
        public double Current { get; private set; }
        public double Maximum { get; }
        public double Reserved=>reserved;
        public double Spendable=>Math.Max(0,Current-reserved);
        public Guid LastToken { get; private set; }
        public StaminaState(double maximum,double delay,double rate)
        {
            if(!BasicAttackDefinition.Finite(maximum)||maximum<=0||!BasicAttackDefinition.Finite(delay)||delay<0||!BasicAttackDefinition.Finite(rate)||rate<0)throw new ArgumentOutOfRangeException(nameof(maximum));
            Maximum=Current=maximum;this.delay=delay;this.rate=rate;
        }
        public bool CanSpend(double amount)=>BasicAttackDefinition.Finite(amount)&&amount>=0&&Spendable>=amount;
        /// <summary>接受前持有释放预算；不提前扣当前体力，并禁止并行消费占用这部分余额。</summary>
        public bool TryReserve(Guid token,double amount)
        {
            if(token==Guid.Empty||spent.Contains(token)||reservations.ContainsKey(token)||!CanSpend(amount))return false;
            reservations.Add(token,amount);reserved+=amount;return true;
        }
        /// <summary>同一同步事务先提交来源，再扣已保证足额的预算；失败保留预留供调用者撤销/重试。</summary>
        public bool CommitReserved(Guid token,double now,Func<bool> commitSource)
        {
            if(token==Guid.Empty||commitSource==null||!BasicAttackDefinition.Finite(now)||now<time||
                spent.Contains(token)||!reservations.TryGetValue(token,out var amount)||Current<amount)return false;
            if(!commitSource())return false;
            reservations.Remove(token);reserved=Math.Max(0,reserved-amount);
            Current-=amount;spent.Add(token);LastToken=token;recoverAt=now+delay;return true;
        }
        public bool CancelReservation(Guid token)
        {
            if(!reservations.TryGetValue(token,out var amount))return false;
            reservations.Remove(token);reserved=Math.Max(0,reserved-amount);return true;
        }
        public void CancelReservations(){reservations.Clear();reserved=0;}
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
        public void Restore(){CancelReservations();Current=Maximum;recoverAt=time+delay;}
        /// <summary>连续举盾消耗不制造每帧动作token；余额耗尽立即返回false且不变负数。</summary>
        public bool Drain(double amount,double now)
        {
            if(!BasicAttackDefinition.Finite(amount)||amount<0||!BasicAttackDefinition.Finite(now)||now<time)throw new ArgumentOutOfRangeException(nameof(amount));
            Current-=Math.Min(Spendable,amount);if(amount>0)recoverAt=now+delay;return Spendable>0;
        }
    }
}
