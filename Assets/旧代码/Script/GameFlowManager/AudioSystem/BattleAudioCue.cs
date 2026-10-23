public enum BattleAudioCue
{
    BattleBgm = 0,
    SkillSelected = 1,
    Attack = 2,
    Hit = 3,
    Voice = 4,
    BreakEnter = 5,
    BreakExit = 6,
    Victory = 7,
    Defeat = 8,
    SpecialWarning = 9,
    InteractiveWindowOpen = 10,
    Guard = 11,
    PerfectGuard = 12,
    Miss = 13,
    WeakHit = 14,
    InteractiveWindowClose = 15
}

public readonly struct BattleAudioCueRequest
{
    public BattleAudioCueRequest(
        BattleAudioCue cue,
        int actorRuntimeId = 0,
        int targetRuntimeId = 0,
        string skillId = "",
        string semanticCueId = "")
    {
        Cue = cue;
        ActorRuntimeId = actorRuntimeId;
        TargetRuntimeId = targetRuntimeId;
        SkillId = skillId ?? string.Empty;
        SemanticCueId = semanticCueId ?? string.Empty;
    }

    public BattleAudioCue Cue { get; }
    public int ActorRuntimeId { get; }
    public int TargetRuntimeId { get; }
    public string SkillId { get; }
    public string SemanticCueId { get; }
}
