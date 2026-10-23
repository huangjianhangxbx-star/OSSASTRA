/* 闪避状态：共享绝对动作时钟；不取消并行主动作，不另开Update。 */
using Ossastra.CharacterCore;
namespace Ossastra.Characters
{
    public sealed class DodgeStateMachine : CharacterStateMachine, IParallelCharacterStateMachine
    {
        public override string StateMachineName => "DodgeStateMachine";
        public override bool IsActive => owner.StateDodge != null && owner.StateDodge.IsActive;
        public override string CurrentStateName => IsActive ? "Dodge" : "Idle";
        public override bool AllowsRequest(CharacterActionKind kind, out string reason)
        {
            reason = null;
            if (kind == CharacterActionKind.Dodge && (!owner.DataBus.AllowsMovement || !owner.DataBus.MoveReady || IsActive)) reason = "motion-not-ready";
            else if (kind == CharacterActionKind.Main && IsActive) reason = "main-not-ready";
            else if (kind == CharacterActionKind.Guard && IsActive) reason = "dodge-active";
            return reason == null;
        }
        public void Prepare(CharacterInputFrame frame)
        {
            if (owner.StateDodge == null) return;
            if (!owner.HasPlayerInput || !owner.CanSimulateIntent) owner.StateDodge.Cancel();
            owner.StateDodge.Tick(owner.SimulationTime);
            if (frame.dodgePressed) owner.StateDodge.Request(frame.move, owner.HasPlayerInput);
        }
        public override void Update(float deltaTime) => owner.StateDodge?.Tick(owner.SimulationTime);
    }
}
