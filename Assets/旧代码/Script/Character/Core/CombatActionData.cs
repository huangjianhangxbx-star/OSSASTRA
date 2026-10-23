using System.Collections;
using System.Collections.Generic;
using UnityEngine;


//CombatActionData 的核心作用是“传参”：
//如果没有这个类，你的状态机只能切换状态，但无法知道：

//这个技能目标（Target）是谁？（是左边的史莱姆还是右边的巨龙？）
//这个技能的具体数据（SkillSO）是什么？

//概念 出现时机	它的作用（潜台词）	存储内容
//CombatActionData	点击按钮/选择目标时	“我想做什么？”（订单）	选了哪个技能？选了哪个目标？
//SkillPayload	技能真正开始释放时	“正在发生什么？”（现场数据）	我是谁？目标是谁？由于由于由于由于由于最终伤害是多少？


public class CombatActionData
{
    public CharacterStateType Skill { get; }
    //public CombatActionData(ESkill skill) => Skill = skill;
    public CharacterRoot Target; // 目标（谁被打了）
    // 未来你可以在这里加更多：cost消耗、触发条件、第几回合等
}
