/* 脚本：FrostReserveState
 * 功能：本武器实例的霜寒余额、支付后恢复延迟及有效来源容量/恢复修饰。
 * 边界：只读本体传入的绝对模拟钟，不读设备/SO，不改变HP或另一角色资源。
 * 协作：支付及首次命中恢复由实际能力收据调用；容量来源变化沿冻结原型的增量/夹紧规则。
 */
using System;
namespace Ossastra.CharacterCore.Abilities
{
    public sealed class FrostReserveState
    {
        public double Current {get;private set;}=3;
        public double Capacity {get;private set;}=3;
        private double recoveryMultiplier=1,lastTime,paidAt=double.NegativeInfinity;
        public bool Spend(double amount,double now)
        {
            if(!Valid(amount)||!BasicAttackDefinition.Finite(now)||Current<amount)return false;
            Current-=amount;paidAt=now;return true;
        }
        public void Recover(double amount){if(Valid(amount))Current=Math.Min(Capacity,Current+amount);}
        public void Tick(double now,bool recovering=true)
        {
            if(!BasicAttackDefinition.Finite(now)||now<lastTime)throw new ArgumentException("Invalid frost simulation time");
            if(recovering)Current=Math.Min(Capacity,Current+Math.Max(0,now-Math.Max(lastTime,paidAt+2))*.8*recoveryMultiplier);
            lastTime=now;
        }
        public void SetModifiers(double capacity,double recovery)
        {
            if(!Valid(capacity)||capacity<=0||!Valid(recovery))throw new ArgumentException("Invalid frost modifiers");
            Current=Math.Min(capacity,Current+Math.Max(0,Math.Truncate(capacity-Capacity)));Capacity=capacity;recoveryMultiplier=recovery;
        }
        private static bool Valid(double value)=>BasicAttackDefinition.Finite(value)&&value>=0;
    }
}
