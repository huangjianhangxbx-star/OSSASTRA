public enum DRPGHitOutcome
{
    Hit = 0,
    Guarded = 1,
    Evaded = 2
}

public sealed class DRPGBattleHitResult
{
    public DRPGBattleHitResult(
        int actorRuntimeId,
        int targetRuntimeId,
        string skillId,
        float hpBefore,
        float appliedDamage,
        float hpAfter,
        bool wasDefeated,
        bool triggeredDamageBroken,
        DRPGBattleRosterRole targetRosterRole,
        string redirectReason = "",
        float postureBefore = 0f,
        float appliedPostureDamage = 0f,
        float postureAfter = 0f,
        DRPGGuardGrade guardGrade = DRPGGuardGrade.Miss,
        DRPGStrikeGrade strikeGrade = DRPGStrikeGrade.Miss,
        float shieldAbsorbed = 0f,
        DRPGDamageBreakdown damageBreakdown = null,
        bool wasPostureBreakExposed = false,
        DRPGHitOutcome outcome = DRPGHitOutcome.Hit)
    {
        ActorRuntimeId = actorRuntimeId;
        TargetRuntimeId = targetRuntimeId;
        SkillId = skillId ?? string.Empty;
        HpBefore = hpBefore;
        AppliedDamage = appliedDamage;
        HpAfter = hpAfter;
        WasDefeated = wasDefeated;
        TriggeredDamageBroken = triggeredDamageBroken;
        TargetRosterRole = targetRosterRole;
        RedirectReason = redirectReason ?? string.Empty;
        PostureBefore = postureBefore;
        AppliedPostureDamage = appliedPostureDamage;
        PostureAfter = postureAfter;
        GuardGrade = guardGrade;
        StrikeGrade = strikeGrade;
        ShieldAbsorbed = shieldAbsorbed;
        DamageBreakdown = damageBreakdown;
        WasPostureBreakExposed = wasPostureBreakExposed;
        Outcome = outcome;
    }

    public int ActorRuntimeId { get; }
    public int TargetRuntimeId { get; }
    public string SkillId { get; }
    public float HpBefore { get; }
    public float AppliedDamage { get; }
    public float HpAfter { get; }
    public bool WasDefeated { get; }
    public bool TriggeredDamageBroken { get; }
    public DRPGBattleRosterRole TargetRosterRole { get; }
    public string RedirectReason { get; }
    public bool WasRedirected => !string.IsNullOrEmpty(RedirectReason);
    public float PostureBefore { get; }
    public float AppliedPostureDamage { get; }
    public float PostureAfter { get; }
    public DRPGGuardGrade GuardGrade { get; }
    public DRPGStrikeGrade StrikeGrade { get; }
    public float ShieldAbsorbed { get; }
    public DRPGDamageBreakdown DamageBreakdown { get; }
    public bool WasPostureBreakExposed { get; }
    public DRPGHitOutcome Outcome { get; }
}
