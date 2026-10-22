/* 脚本：RecoverableHealthState
 * 功能：保存实际受伤产生的虚血、延迟衰减和以父执行归并的夺回预算。
 * 边界：引用唯一Health，不保存第二份HP；伤害/防御结果已确定后才记账，死亡不能夺回。
 * 协作：根传原模拟delta；普通治疗按真实缺血夹紧；同次技能多波/多目标共享预算。
 */
using System;
using System.Collections.Generic;
namespace Ossastra.CharacterCore.Abilities
{
    public sealed class RecoverableHealthState
    {
        private sealed class Budget { public double remaining,credit; }
        private readonly Health health;
        private readonly Dictionary<Guid,Budget> budgets=new Dictionary<Guid,Budget>();
        private double gray,hold;
        public double Gray=>Math.Max(0,Math.Min(gray,health.Maximum-health.Current));
        public double HoldRemaining=>hold;
        public RecoverableHealthState(Health health){this.health=health??throw new ArgumentNullException(nameof(health));}
        public void RecordLoss(int actualLoss)
        {if(actualLoss<=0)return;gray=Math.Min(health.Maximum-health.Current,Gray+actualLoss);hold=4;}
        public void Tick(double delta)
        {
            if(!BasicAttackDefinition.Finite(delta)||delta<0)throw new ArgumentException("Invalid recovery delta");
            gray=Gray;if(health.Current<=0)return;
            var afterHold=Math.Max(0,delta-hold);hold=Math.Max(0,hold-delta);
            gray=Math.Max(0,gray-afterHold*health.Maximum*.1);
        }
        public int Reclaim(Guid parent,int actualDamage,double fraction,double rate)
        {
            if(parent==Guid.Empty||actualDamage<=0||health.Current<=0||!BasicAttackDefinition.Finite(fraction)||!BasicAttackDefinition.Finite(rate)||fraction<=0||rate<=0)return 0;
            gray=Gray;if(!budgets.TryGetValue(parent,out var budget)){budget=new Budget{remaining=health.Maximum*fraction};budgets.Add(parent,budget);}
            var permitted=Math.Max(0,Math.Min(Math.Min(gray,health.Maximum-health.Current),Math.Min(actualDamage*rate,budget.remaining)));
            budget.remaining-=permitted;budget.credit+=permitted;
            // 当前唯一Health为整数；不足1点的夺回额度在本次父执行中累计，不另造浮点HP镜像。
            var amount=(int)Math.Floor(Math.Min(Math.Min(budget.credit,gray),health.Maximum-health.Current));
            if(amount<=0)return 0;var healed=health.Heal(amount);budget.credit-=healed;gray-=healed;return healed;
        }
        public void CompleteParent(Guid parent){budgets.Remove(parent);}
        public void Reset(){gray=0;hold=0;budgets.Clear();}
    }
}
