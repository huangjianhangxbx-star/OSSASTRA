/* 运动状态：方向采样在请求前，唯一真实位移在动作事实之后，使用本帧最新许可。 */
using UnityEngine;
namespace Ossastra.Characters
{
    public sealed class MovementStateMachine : CharacterStateMachine
    {
        public override string StateMachineName => "MovementStateMachine";
        public override bool IsActive => owner.StateMovement != null && owner.StateMovement.IsMoving;
        public override string CurrentStateName => IsActive ? "Move" : "Idle";
        public void ResolveDirections(CharacterInputFrame frame)
        {
            var canMove = owner.StateFocused && !owner.IsPaused && owner.DataBus.AllowsMovement && owner.DataBus.MoveReady;
            if (owner.StateFocused && !owner.IsPaused)
                owner.StateOrienter.ResolveDirections(frame.move, owner.StatePointerDirection(frame), frame.hasDirectionalAim ? frame.aimDirection : Vector2.zero, canMove);
        }
        public override void Update(float deltaTime)
        {
            var canMove = owner.StateFocused && !owner.IsPaused && owner.DataBus.AllowsMovement && (owner.DataBus.MoveReady || owner.DataBus.AbilityMotionActive);
            owner.StateMover.Tick(canMove, owner.IsSimulationSuspended ? 0 : deltaTime);
        }
    }
}
