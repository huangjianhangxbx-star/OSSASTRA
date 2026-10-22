/* 脚本：AbilityExecutorSO
 * 功能：定义专职能力行为的扩展位置；SO只负责构建每次执行的独立运行对象。
 * 边界：不在共享SO内保存可变角色状态，不按装备/技能名字集中分支。
 * 协作：来源、动作事务和调度由CharacterAbilities提供；实际投递/结算由实现消费共享Combat合同。
 */
using UnityEngine;
namespace Ossastra.Characters
{
    /// <summary>编译后执行策略；仅含冻结配置，CreateRun为每次接受创建独立状态。</summary>
    public abstract class CompiledAbilityExecutor
    {
        public abstract bool UsesBodyAction {get;}
        public virtual CharacterActionKind ActionKind=>UsesBodyAction?CharacterActionKind.Main:CharacterActionKind.Offhand;
        public virtual CompiledAbilityExecutor Prepare(CharacterRoot owner)=>this;
        public virtual bool StartsCooldownOnAccept=>true;
        public virtual float AimRange=>0;
        public abstract AbilityRun CreateRun(AbilityRunContext context);
    }
    public abstract class AbilityExecutorSO:ScriptableObject
    {
        public abstract bool UsesBodyAction {get;}
        public abstract void Validate(AbilityDefinitionSO definition);
        public abstract CompiledAbilityExecutor CompileExecutor();
    }
}
