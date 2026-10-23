using System;

public sealed class BattleHitPresentationSnapshot
{
    public BattleHitPresentationSnapshot(
        int actorRuntimeId,
        int targetRuntimeId,
        string skillId,
        float hpBefore,
        float appliedDamage,
        float hpAfter,
        bool triggeredDamageBroken,
        bool wasDefeated,
        bool wasRedirected,
        DRPGLowHpPresentationState finalLowHpState,
        DRPGBattleRosterRole targetRosterRole)
    {
        ActorRuntimeId = actorRuntimeId;
        TargetRuntimeId = targetRuntimeId;
        SkillId = skillId ?? string.Empty;
        HpBefore = hpBefore;
        AppliedDamage = appliedDamage;
        HpAfter = hpAfter;
        TriggeredDamageBroken = triggeredDamageBroken;
        WasDefeated = wasDefeated;
        WasRedirected = wasRedirected;
        FinalLowHpState = finalLowHpState;
        TargetRosterRole = targetRosterRole;
    }

    public int ActorRuntimeId { get; }
    public int TargetRuntimeId { get; }
    public string SkillId { get; }
    public float HpBefore { get; }
    public float AppliedDamage { get; }
    public float HpAfter { get; }
    public bool TriggeredDamageBroken { get; }
    public bool WasDefeated { get; }
    public bool WasRedirected { get; }
    public DRPGLowHpPresentationState FinalLowHpState { get; }
    public DRPGBattleRosterRole TargetRosterRole { get; }

    public static BattleHitPresentationSnapshot From(
        DRPGBattleHitResult hit,
        BattleParticipantRuntime resolvedTarget)
    {
        if (hit == null)
            throw new ArgumentNullException(nameof(hit));
        if (resolvedTarget == null)
            throw new ArgumentNullException(nameof(resolvedTarget));
        if (resolvedTarget.RuntimeId != hit.TargetRuntimeId)
        {
            throw new ArgumentException(
                "Resolved target does not match the hit target.",
                nameof(resolvedTarget));
        }

        return new BattleHitPresentationSnapshot(
            hit.ActorRuntimeId,
            hit.TargetRuntimeId,
            hit.SkillId,
            hit.HpBefore,
            hit.DamageBreakdown != null
                ? hit.DamageBreakdown.RequestedDamage
                : hit.AppliedDamage,
            hit.HpAfter,
            hit.TriggeredDamageBroken,
            hit.WasDefeated,
            hit.WasRedirected,
            resolvedTarget.LowHpState,
            hit.TargetRosterRole);
    }
}
