using System;

public enum DRPGBattleEventKind
{
    EffectApplied = 0,
    DamageApplied = 1,
    PostureBreakEntered = 2,
    ParticipantDefeated = 3,
    ActionImpactResolved = 4,
    PostureBreakTurnSkipped = 5,
    PostureBreakRecovered = 6,
    PreparedActionInterrupted = 7
}

public sealed class DRPGBattleEvent
{
    public DRPGBattleEvent(
        long eventId,
        long rootActionId,
        int depth,
        DRPGBattleEventKind kind,
        int sourceActorRuntimeId,
        int targetRuntimeId,
        string skillId,
        DRPGActionEffectKind? effectKind = null,
        float requestedAmount = 0f,
        float appliedAmount = 0f,
        float auxiliaryAmount = 0f,
        DRPGDamageTag damageTags = DRPGDamageTag.None,
        string semanticId = "")
    {
        if (eventId <= 0)
            throw new ArgumentOutOfRangeException(nameof(eventId));
        if (rootActionId < 0 ||
            rootActionId == 0 && !IsLifecycleKind(kind))
            throw new ArgumentOutOfRangeException(nameof(rootActionId));
        if (depth < 0)
            throw new ArgumentOutOfRangeException(nameof(depth));
        if (sourceActorRuntimeId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceActorRuntimeId));
        }

        EventId = eventId;
        RootActionId = rootActionId;
        Depth = depth;
        Kind = kind;
        SourceActorRuntimeId = sourceActorRuntimeId;
        TargetRuntimeId = Math.Max(0, targetRuntimeId);
        SkillId = skillId ?? string.Empty;
        EffectKind = effectKind;
        RequestedAmount = requestedAmount;
        AppliedAmount = appliedAmount;
        AuxiliaryAmount = auxiliaryAmount;
        DamageTags = damageTags;
        SemanticId = semanticId ?? string.Empty;
    }

    public long EventId { get; }
    public long RootActionId { get; }
    public int Depth { get; }
    public DRPGBattleEventKind Kind { get; }
    public int SourceActorRuntimeId { get; }
    public int TargetRuntimeId { get; }
    public string SkillId { get; }
    public DRPGActionEffectKind? EffectKind { get; }
    public float RequestedAmount { get; }
    public float AppliedAmount { get; }
    public float AuxiliaryAmount { get; }
    public DRPGDamageTag DamageTags { get; }
    public string SemanticId { get; }

    private static bool IsLifecycleKind(DRPGBattleEventKind kind)
    {
        return kind == DRPGBattleEventKind.PostureBreakTurnSkipped ||
            kind == DRPGBattleEventKind.PostureBreakRecovered ||
            kind == DRPGBattleEventKind.PostureBreakEntered ||
            kind == DRPGBattleEventKind.PreparedActionInterrupted;
    }
}
