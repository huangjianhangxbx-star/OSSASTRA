public readonly struct DRPGActionDelayContext
{
    public DRPGActionDelayContext(
        BattleParticipantRuntime actor,
        DRPGSkillSO skill,
        float unmodifiedDelay)
    {
        Actor = actor;
        Skill = skill;
        UnmodifiedDelay = unmodifiedDelay;
    }

    public BattleParticipantRuntime Actor { get; }
    public DRPGSkillSO Skill { get; }
    public float UnmodifiedDelay { get; }
}
