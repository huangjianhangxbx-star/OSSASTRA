/* 能力状态：使用原Run/事务账本；输入受理与世界效果推进分阶段，不能重复扣费。 */
namespace Ossastra.Characters
{
    public sealed class AbilityStateMachine : CharacterStateMachine, IParallelCharacterStateMachine
    {
        public override string StateMachineName => "AbilityStateMachine";
        public override bool IsActive => owner.StateAbilities != null && owner.StateAbilities.IsBusy;
        public override string CurrentStateName => IsActive ? "AbilityRun" : "Maintain";
        public void Prepare(CharacterInputFrame frame, float deltaTime)
        {
            if (owner.StateAbilities != null) owner.StateAbilities.TickInput(frame);
            else
            {
                owner.StateSkills?.TickInput(frame);
                owner.StateGuard?.Tick(frame.offhandHeld, owner.HasPlayerInput, owner.IsSimulationSuspended ? 0 : deltaTime);
            }
        }
        public override void Update(float deltaTime)
        {
            if (owner.StateAbilities != null) owner.StateAbilities.TickEffects(deltaTime);
            else owner.StateSkills?.TickEffects(deltaTime);
        }
    }
}
