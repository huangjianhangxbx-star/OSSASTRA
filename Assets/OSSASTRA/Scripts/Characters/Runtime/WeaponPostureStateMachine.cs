/* 兼容副手状态：新能力路径互斥；保留旧装备路径的独立身份和既有世界效果。 */
using Ossastra.CharacterCore;
namespace Ossastra.Characters
{
    public sealed class WeaponPostureStateMachine : CharacterStateMachine, IParallelCharacterStateMachine
    {
        public override string StateMachineName => "WeaponPostureStateMachine";
        public override bool IsActive => owner.StateGuard != null && owner.StateGuard.IsGuarding;
        public override string CurrentStateName => IsActive ? "Guard" : "Maintain";
        public void Prepare(CharacterInputFrame frame)
        {
            if (owner.StateAbilities != null) return;
            if (frame.offhandPressed && owner.StateEquipment != null && owner.StateEquipment.State.LeftKind == OffhandKind.Bomb)
                owner.StateOffhand?.Request(owner.StatePointerGroundPoint(frame), owner.HasPlayerInput);
        }
        public override void Update(float deltaTime)
        {
            if (owner.StateAbilities == null) owner.StateOffhand?.Tick(deltaTime, owner.StateInput.HasPlayerControl);
        }
        public void MaintainDeath()
        {
            owner.StateOffhand?.CancelAll();
            owner.StateAbilities?.CancelPending("death");
            owner.StateAbilities?.TickEffects(0);
        }
    }
}
