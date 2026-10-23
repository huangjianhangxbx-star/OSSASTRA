/* 脚本：AbilityDefinitionSO
 * 功能：组合能力动作、来源费用、投递与专职执行策略；内容身份不进入Root分支。
 * 边界：只保存作者定义；Compile产生独立冻结快照，不保存CD、阶段、耐久或订阅。
 * 协作：CharacterAbilities先编译并验证实际来源/动画，再进入原动作接受事务。
 */
using System;
using System.Linq;
using UnityEngine;
using Ossastra.CharacterCore.Abilities;
using Ossastra.Combat;
namespace Ossastra.Characters
{
    public enum AbilityOperation { Secondary, SkillOne, SkillTwo, SkillThree }
    public enum AbilityResourceOwner { BoundEquipment, MainEquipment }
    [Serializable]
    public sealed class AbilityBinding
    {
        public AbilityOperation operation;
        public GestureMode gesture;
        public AbilityResourceOwner resourceOwner;
        [Min(.01f)] public float longPressThreshold=.3f;
        public AbilityDefinitionSO ability,longPressAbility;
    }
    [Serializable]
    public sealed class AbilityReleaseNode
    {
        [Min(0)] public float at;
        [Min(0)] public int damage=20;
        [Min(0)] public float postureImpact;
    }
    public sealed class CompiledAbility
    {
        public readonly string id,displayName,cooldownIdentity;
        public readonly string effectRoot,damageChannel;
        public readonly AbilityPlan plan;
        public readonly float cost,cooldown,holdCost,releaseCost;
        public readonly bool wearOnRelease;
        public readonly float reclaimBudget,reclaimRate;
        public readonly AbilityCooldownPolicy cooldownPolicy;
        public readonly DeliveryParameters delivery;
        public readonly float[] posture;
        public readonly ActionDefinitionSO animation;
        public readonly CompiledAbilityExecutor executor;
        internal CompiledAbility(AbilityDefinitionSO definition)
        {
            id=definition.definitionId;displayName=definition.displayName;cooldownIdentity=string.IsNullOrWhiteSpace(definition.cooldownIdentity)?id:definition.cooldownIdentity;cost=definition.acceptCost;cooldown=definition.cooldown;
            holdCost=definition.holdCostPerSecond;releaseCost=definition.releaseCost;wearOnRelease=definition.wearOnRelease;
            reclaimBudget=definition.reclaimBudget;reclaimRate=definition.reclaimRate;
            cooldownPolicy=definition.cooldownPolicy;
            effectRoot=definition.effectRoot;damageChannel=string.IsNullOrWhiteSpace(definition.damageChannel)?effectRoot:definition.damageChannel;
            delivery=definition.delivery.Freeze();animation=definition.action;executor=definition.executor.CompileExecutor();
            // 释放节点、冲击与几何均复制；这里只保留不可变资源对象引用，动画接受时另冻结片段/速率。
            posture=definition.releases.Select(n=>n.postureImpact).ToArray();
            plan=new AbilityPlan(id,definition.finishAt,definition.moveReadyAt,definition.attackReadyAt,
                definition.releases.Select((n,i)=>new AbilityEmission(n.at,n.damage,i)).ToArray(),definition.occupancy);
        }
    }
    [CreateAssetMenu(menuName="OSSASTRA/Abilities/Ability Definition")]
    public sealed class AbilityDefinitionSO:ScriptableObject
    {
        public const int CurrentSchema=1;
        public int schemaVersion=CurrentSchema;
        public AbilityCooldownPolicy cooldownPolicy;
        public string definitionId,displayName,cooldownIdentity;
        public string effectRoot,damageChannel;
        public AbilityExecutorSO executor;
        public ActionDefinitionSO action;
        public AbilityOccupancy occupancy=AbilityOccupancy.Body|AbilityOccupancy.MainHand;
        [Min(0)] public float acceptCost=18,cooldown=6,holdCostPerSecond,releaseCost;
        public bool wearOnRelease;
        [Range(0,1)] public float reclaimBudget=.05f,reclaimRate=.5f;
        [Min(.01f)] public float finishAt=.5f;
        [Min(0)] public float moveReadyAt=.4f,attackReadyAt=.4f;
        public DeliveryParameters delivery=new DeliveryParameters();
        public AbilityReleaseNode[] releases=Array.Empty<AbilityReleaseNode>();
        public CompiledAbility Compile()
        {
            try
            {
                if(schemaVersion!=CurrentSchema||!Enum.IsDefined(typeof(AbilityCooldownPolicy),cooldownPolicy)||string.IsNullOrWhiteSpace(definitionId)||executor==null||delivery==null||releases==null||
                    !ValidCost(acceptCost)||!ValidCost(cooldown)||!ValidCost(holdCostPerSecond)||!ValidCost(releaseCost)||
                    !ValidCost(reclaimBudget)||reclaimBudget>1||!ValidCost(reclaimRate)||reclaimRate>1||
                    releases.Any(n=>n==null||!ValidCost(n.postureImpact)))throw new ArgumentException("identity/executor/cost/releases invalid");
                executor.Validate(this);return new CompiledAbility(this);
            }
            catch(ArgumentException e){throw new ArgumentException(name+" ("+definitionId+"): "+e.Message,e);}
        }
        private static bool ValidCost(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n)&&n>=0;
    }
}
