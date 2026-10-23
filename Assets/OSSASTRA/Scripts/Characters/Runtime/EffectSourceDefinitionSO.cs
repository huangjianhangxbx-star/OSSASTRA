/* 脚本：EffectSourceDefinitionSO
 * 功能：保留冻结原型的来源ID、条件及修饰数据；可由Inspector配置而不修改能力执行代码。
 * 边界：不保存是否持有/生效/计时；Freeze复制为Core不可变定义，状态归主手实例。
 */
using System;
using System.Linq;
using UnityEngine;
using Ossastra.CharacterCore.Abilities;
namespace Ossastra.Characters
{
    [Serializable]
    public sealed class SourceSkillModifier
    {
        public string root,condition;
        public bool guaranteedCrit,passesTargets,instantReload;
        public int ammoCount;
        public float damageAdd,damagePerFrost,damageRatio,cooldownAdd,knockRatio,stunSeconds,reloadShotRate,reloadShotDamage,shotRateBonus,shotRateDuration;
        public EffectModifier Freeze()=>new EffectModifier(root,damageAdd,damagePerFrost,damageRatio,cooldownAdd,knockRatio,stunSeconds,reloadShotRate,reloadShotDamage,shotRateBonus,shotRateDuration,
            guaranteedCrit,passesTargets,instantReload,condition=="last-round",ammoCount);
    }
    [CreateAssetMenu(menuName="OSSASTRA/Abilities/Effect Source")]
    public sealed class EffectSourceDefinitionSO:ScriptableObject
    {
        public string definitionId,nativeName,profession,effect,requiredRoot,exclusivePoint,comboPrefix;
        public int phase,damage;
        public float hastePerFrost,frostCap,frostRecovery;
        public SourceSkillModifier[] modifiers=Array.Empty<SourceSkillModifier>();
        public EffectSourceDefinition Freeze()=>new EffectSourceDefinition(definitionId,profession,effect,phase,requiredRoot,exclusivePoint,hastePerFrost,frostCap,frostRecovery,damage,comboPrefix,modifiers.Select(m=>m.Freeze()).ToArray());
    }
}
