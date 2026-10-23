/* 脚本：CharacterStamina
 * 功能：把每本体体力配置与纯余额接入Root；记录最近一次实际付款。
 * 边界：没有独立Update，不创建动作或处理设备输入；只有Root模拟时间推进。
 */
using System;
using Ossastra.CharacterCore;
using UnityEngine;
namespace Ossastra.Characters
{
    [DisallowMultipleComponent]
    public sealed class CharacterStamina:MonoBehaviour,ICharacterStamina
    {
        [SerializeField,Min(1)] private float maximum=100;
        [SerializeField,Min(0)] private float recoveryDelay=.5f,recoveryRate=20,attackCost=10;
        private StaminaState state;
        public double Current=>state?.Current??0;
        public double Maximum=>state?.Maximum??maximum;
        public double Reserved=>state?.Reserved??0;
        public double Spendable=>state?.Spendable??0;
        public float AttackCost=>attackCost;
        public Guid LastToken=>state?.LastToken??Guid.Empty;
        public string LastPayment {get;private set;}="无";
        public void Initialize(){if(state==null)state=new StaminaState(maximum,recoveryDelay,recoveryRate);}
        public bool CanSpend(double amount)=>isActiveAndEnabled&&state!=null&&state.CanSpend(amount);
        public bool TryReserve(Guid token,double amount)=>isActiveAndEnabled&&state!=null&&state.TryReserve(token,amount);
        public bool CommitReserved(Guid token,double time,Func<bool> commitSource,string source="ability-release")
        {
            var before=Current;var ok=isActiveAndEnabled&&state!=null&&state.CommitReserved(token,time,commitSource);
            LastPayment=source+" #"+token.ToString("N")+(ok?" 已支付 ":" 拒绝 ")+(before-Current);return ok;
        }
        public bool CancelReservation(Guid token)=>state!=null&&state.CancelReservation(token);
        public void CancelReservations()=>state?.CancelReservations();
        public bool Spend(Guid token,double amount,double time,string source)
        {var ok=isActiveAndEnabled&&state!=null&&state.TrySpend(token,amount,time);LastPayment=source+" #"+token.ToString("N")+(ok?" 已支付 ":" 拒绝 ")+amount;return ok;}
        public void Tick(double time,bool idle){state?.Tick(time,idle&&isActiveAndEnabled);}
        public void Restore(){state?.Restore();}
        public bool Drain(double amount,double time)=>isActiveAndEnabled&&state!=null&&state.Drain(amount,time);
    }
}
