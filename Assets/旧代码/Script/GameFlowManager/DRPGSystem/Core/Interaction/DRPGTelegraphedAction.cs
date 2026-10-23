using System;

public sealed class DRPGTelegraphedAction
{
    public DRPGTelegraphedAction(
        int actorRuntimeId,
        int targetRuntimeId,
        DRPGSkillSO skill,
        DRPGResolvedActionSpec spec = null)
    {
        if (actorRuntimeId <= 0)
            throw new ArgumentOutOfRangeException(nameof(actorRuntimeId));
        if (targetRuntimeId <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetRuntimeId));

        ActorRuntimeId = actorRuntimeId;
        TargetRuntimeId = targetRuntimeId;
        Skill = skill ?? throw new ArgumentNullException(nameof(skill));
        Spec = spec;
    }

    public int ActorRuntimeId { get; }
    public int TargetRuntimeId { get; }
    public DRPGSkillSO Skill { get; }
    public DRPGResolvedActionSpec Spec { get; }

    public bool Matches(DRPGBattleCommand command)
    {
        return command != null &&
            command.ActorRuntimeId == ActorRuntimeId &&
            command.TargetRuntimeId == TargetRuntimeId &&
            ReferenceEquals(command.Skill, Skill) &&
            command.Stage == DRPGBattleCommandStage.Release;
    }
}
