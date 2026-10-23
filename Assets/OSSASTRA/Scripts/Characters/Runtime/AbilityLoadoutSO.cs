/* 脚本：AbilityLoadoutSO
 * 功能：主手职业技能目录与固定E定义；Q/R选择属于主手实例payload。
 * 边界：SO不保存选择、CD或执行状态；不根据角色名字分派技能。
 */
using System;
using System.Linq;
using UnityEngine;
namespace Ossastra.Characters
{
    [CreateAssetMenu(menuName="OSSASTRA/Abilities/Main Hand Loadout")]
    public sealed class AbilityLoadoutSO:ScriptableObject
    {
        public string professionId;
        public AbilityDefinitionSO[] regularChoices=Array.Empty<AbilityDefinitionSO>();
        public AbilityDefinitionSO nativeAbility,defaultQ,defaultR;
        public string effectProfession;
        public EffectSourceDefinitionSO[] sourceCatalog=Array.Empty<EffectSourceDefinitionSO>();
        public AbilityDefinitionSO[] experimentalChoices=Array.Empty<AbilityDefinitionSO>();
        public void Validate()
        {
            if(string.IsNullOrWhiteSpace(professionId)||nativeAbility==null||regularChoices==null||
                regularChoices.Any(d=>d==null||d==nativeAbility)||regularChoices.Select(d=>d.definitionId).Distinct().Count()!=regularChoices.Length||
                defaultQ!=null&&!regularChoices.Contains(defaultQ)||defaultR!=null&&!regularChoices.Contains(defaultR))
                throw new ArgumentException("invalid main hand skill directory/defaults");
            if(experimentalChoices==null||experimentalChoices.Any(d=>d==null)||
                regularChoices.Concat(experimentalChoices).Select(d=>d.definitionId).Distinct().Count()!=regularChoices.Length+experimentalChoices.Length)
                throw new ArgumentException("invalid experimental directory/duplicate identity");
            var all=regularChoices.Concat(experimentalChoices).Concat(new[]{nativeAbility}).ToArray();
            if(all.Select(d=>d.definitionId).Distinct().Count()!=all.Length)throw new ArgumentException("duplicate native/regular/experimental definition identity");
            foreach(var d in all)d.Compile();
        }
    }
}
