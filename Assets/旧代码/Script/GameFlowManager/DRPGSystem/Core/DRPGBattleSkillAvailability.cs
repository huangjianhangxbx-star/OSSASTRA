using System;

public sealed class DRPGBattleSkillAvailability
{
    public DRPGBattleSkillAvailability(
        DRPGSkillSO skill,
        bool isAvailable,
        string unavailableReason,
        DRPGProgressSnapshot progress)
    {
        Skill = skill;
        IsAvailable = isAvailable;
        UnavailableReason = isAvailable
            ? string.Empty
            : unavailableReason ?? string.Empty;
        Progress = progress;
    }

    public DRPGSkillSO Skill { get; }
    public bool IsAvailable { get; }
    public string UnavailableReason { get; }
    public DRPGProgressSnapshot Progress { get; }
    public bool HasProgress => Progress != null;
}
