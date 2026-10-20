/*
 * 脚本：EnemyContracts
 * 功能：敌根消费的决策与实际运动窄接口；固定/追击策略可替换。
 * 边界：接口只交意图，动作事实与伤害仍由EnemyRoot原链条负责。
 */
using Ossastra.Combat;
using UnityEngine;
namespace Ossastra.Enemies
{
    public interface IEnemyBrain
    {
        bool IsAvailable { get; }
        Vector3 MoveDirection { get; }
        bool Initialize(CombatVitals owner);
        void Tick(float now,float delta);
        bool IsEligible(ICombatTarget target);
        bool TrySelect(float now,out ICombatTarget target);
        void OnAccepted(float now);
        void ResetDecision(float now);
    }
    public interface IEnemyMovement
    {
        Vector3 Velocity { get; }
        bool HasMovementRequest { get; }
        void Tick(Vector3 direction,float delta);
        void Stop();
    }
}
