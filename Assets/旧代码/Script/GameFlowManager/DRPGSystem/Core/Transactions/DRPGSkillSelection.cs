using System;

public enum DRPGSkillPresentationMode
{
    Conflict,
    InPlace
}

public readonly struct DRPGSkillSelection
{
    public DRPGSkillSelection(
        ActorTurnToken turn,
        int slotIndex,
        DRPGSkillSO skill,
        int primaryTargetRuntimeId)
    {
        if (!turn.IsValid)
            throw new ArgumentException("A valid actor turn is required.", nameof(turn));
        if (slotIndex < 0 || slotIndex >= DRPGSkillLoadout.SlotCount)
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        if (skill == null)
            throw new ArgumentNullException(nameof(skill));
        if (primaryTargetRuntimeId <= 0)
            throw new ArgumentOutOfRangeException(nameof(primaryTargetRuntimeId));
        Turn = turn;
        SlotIndex = slotIndex;
        Skill = skill;
        PrimaryTargetRuntimeId = primaryTargetRuntimeId;
    }

    public ActorTurnToken Turn { get; }
    public int SlotIndex { get; }
    public DRPGSkillSO Skill { get; }
    public int PrimaryTargetRuntimeId { get; }
}

public static class DRPGBattleSkillModePolicy
{
    public static bool IsAllowed(DRPGSkillSO skill)
    {
        if (skill == null ||
            skill.ExecutionMode != DRPGSkillExecutionMode.Standard)
            return false;
        if (skill.Effects.Count == 0)
            return skill.Purpose == DRPGSkillPurpose.Damage;
        for (int i = 0; i < skill.Effects.Count; i++)
        {
            DRPGActionEffectDefinition effect = skill.Effects[i];
            if (effect != null && effect.Kind == DRPGActionEffectKind.Damage)
                return true;
        }
        return false;
    }
}
