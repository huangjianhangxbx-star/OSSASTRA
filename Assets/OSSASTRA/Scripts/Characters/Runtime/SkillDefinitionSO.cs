/* 脚本：SkillDefinitionSO
 * 功能：技能内容引用与释放执行器选择；新增同类技能只增配置。
 * 边界：不存冷却/执行ID；不直接改生命。派生效果类型是受控枚举而非任意脚本回调。
 */
using System;
using UnityEngine;
namespace Ossastra.Characters
{
    public enum SkillEffectKind { Melee, DelayedArea }
    [CreateAssetMenu(menuName="OSSASTRA/Skills/Skill Definition")]
    public sealed class SkillDefinitionSO:ScriptableObject
    {
        public string definitionId,displayName;
        public ActionDefinitionSO action;
        public SkillEffectKind effect;
        [Min(0)] public float cooldown=4;
        [Min(.01f)] public float travelTime=.4f,radius=1.25f,range=2.5f;
        /// <summary>配置资格先于接受事务；不让非法ID、CD或物理参数在付款后失败。</summary>
        public bool IsValid(out string reason)
        {
            reason=string.IsNullOrWhiteSpace(definitionId)?"missing-definition-id":action==null?"missing-action":
                !Enum.IsDefined(typeof(SkillEffectKind),effect)?"unknown-effect":!Finite(cooldown)||cooldown<0?"invalid-cooldown":
                effect==SkillEffectKind.DelayedArea&&(!Finite(travelTime)||travelTime<=0||!Finite(radius)||radius<=0||!Finite(range)||range<0)?"invalid-effect-geometry":null;
            return reason==null;
        }
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
}
