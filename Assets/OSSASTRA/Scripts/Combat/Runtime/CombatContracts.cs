/*
 * 脚本：CombatContracts
 * 功能：中立命中、生命及世界目标契约；玩家输入和敌方决策共用。
 * 边界：Health为唯一HP权威；接口通知不重复扣血，不保存玩家Current。
 * 协作：玩家根/敌人根/物理解析/感知真实消费；旧开发靶只需IHitTarget。
 */
using Ossastra.CharacterCore;
using UnityEngine;
namespace Ossastra.Combat
{
    public interface ICombatEvasion { bool IsEvading { get; } }
    public interface ICombatGuard
    {
        bool IsGuarding { get; }
        Vector3 GuardDirection { get; }
        double MinimumDot { get; }
        double PostureScale { get; }
        void OnGuarded(HitOutcome outcome);
    }
    /// <summary>只采样防御资格；回执后才允许更新盾耐久，不直接写生命或架势。</summary>
    public interface ICombatDefense
    {
        DefenseSnapshot CaptureDefense(Vector3 incomingOrigin);
        void OnDefenseResolved(HitOutcome outcome);
    }
    /// <summary>世界命中器向同一合法存活目标递交架势冲击，不拥有第二次HP写入。</summary>
    public interface ICombatPostureReceiver
    {
        void ReceiveImpact(HitOutcome outcome, double impact);
    }
    public interface ICombatLifeState { bool IsAlive { get; } }
    public interface IHitTarget { Health Health { get; } void ShowHit(); }
    /// <summary>特定场景实体在HP写入前拒绝来源释放，例如冰柱忽略产生自己的凿冰执行。</summary>
    public interface ICombatContactFilter { bool Accepts(AttackRelease release); }
    /// <summary>世界实体可拒绝直射弹体接触，仍接受近战区域和落地范围；不改变HP结算规则。</summary>
    public interface ICombatProjectileContact { bool AllowsDirectProjectile { get; } }
    /// <summary>场景伤害实体可明确禁止成为虚血夺回来源；未实现者保留普通战斗目标规则。</summary>
    public interface ICombatReclaimPolicy { bool AllowsReclaim { get; } }
    public interface ICombatTarget : IHitTarget, ICombatLifeState
    {
        bool IsAvailable { get; }
        CombatFaction Faction { get; }
        Vector3 WorldPosition { get; }
        Transform TargetTransform { get; }
        void OnHitResolved(HitOutcome outcome);
    }
    public interface ICombatHitResolver
    {
        string LastResult { get; }
        void Resolve(AttackRelease release, Vector3 releaseDirection);
    }
    /// <summary>原物理结算的只读回执；禁止消费者再查物理或再扣血。</summary>
    public interface ICombatHitReceiptSource
    {
        event System.Action<System.Guid, bool> Resolved;
    }
    /// <summary>接口非null不代表Unity宿主有效；销毁/禁用/无生命立即失效。</summary>
    public static class CombatTargetValidity
    {
        public static bool IsAvailable(IHitTarget target)
        {
            var component=target as MonoBehaviour;
            return component!=null && component.isActiveAndEnabled && target.Health!=null &&
                target.Health.Current>0 && (!(target is ICombatTarget combat) || combat.IsAvailable);
        }
    }
}
