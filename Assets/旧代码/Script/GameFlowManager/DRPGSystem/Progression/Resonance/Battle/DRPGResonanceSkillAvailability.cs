public sealed class DRPGResonanceSkillAvailability
{
    public DRPGResonanceSkillAvailability(
        DRPGSkillSO skill,
        string partnerCharacterId,
        int currentProgress,
        int requiredProgress,
        bool isAvailable,
        string reason)
    {
        Skill = skill;
        PartnerCharacterId = partnerCharacterId ?? string.Empty;
        CurrentProgress = currentProgress;
        RequiredProgress = requiredProgress;
        IsAvailable = isAvailable;
        Reason = reason ?? string.Empty;
    }

    public DRPGSkillSO Skill { get; }
    public string PartnerCharacterId { get; }
    public int CurrentProgress { get; }
    public int RequiredProgress { get; }
    public bool IsAvailable { get; }
    public string Reason { get; }
}
