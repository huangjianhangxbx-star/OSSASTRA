using System;
using System.Collections.Generic;

public enum DRPGActionEffectResultState
{
    Applied = 0,
    Skipped = 1
}

public sealed class DRPGDamageBreakdown
{
    public DRPGDamageBreakdown(
        float baseMagnitude,
        float actorMultiplier,
        float impactMultiplier,
        float affinityMultiplier,
        float statusMultiplier,
        float defenseMultiplier,
        float requestedDamage,
        float shieldAbsorbed,
        float hpDamage,
        DRPGDamageTag tags)
    {
        BaseMagnitude = Math.Max(0f, baseMagnitude);
        ActorMultiplier = Math.Max(0f, actorMultiplier);
        ImpactMultiplier = Math.Max(0f, impactMultiplier);
        AffinityMultiplier = Math.Max(0f, affinityMultiplier);
        StatusMultiplier = Math.Max(0f, statusMultiplier);
        DefenseMultiplier = Math.Max(0f, defenseMultiplier);
        RequestedDamage = Math.Max(0f, requestedDamage);
        ShieldAbsorbed = Math.Max(0f, shieldAbsorbed);
        HpDamage = Math.Max(0f, hpDamage);
        Tags = tags;
    }

    public float BaseMagnitude { get; }
    public float ActorMultiplier { get; }
    public float ImpactMultiplier { get; }
    public float AffinityMultiplier { get; }
    public float StatusMultiplier { get; }
    public float DefenseMultiplier { get; }
    public float RequestedDamage { get; }
    public float ShieldAbsorbed { get; }
    public float HpDamage { get; }
    public DRPGDamageTag Tags { get; }
}

public sealed class DRPGActionEffectResult
{
    internal DRPGActionEffectResult(int actorRuntimeId, DRPGActionStatusChange statusChange)
        : this(DRPGActionEffectKind.ApplyStatus, DRPGActionEffectResultState.Applied, actorRuntimeId,
            statusChange.Target.Value, statusChange.Mutation.Stacks, statusChange.AppliedStatus.Stacks,
            statusChange.AppliedStatus.RemainingTurns, statusChange.Mutation.StatusId)
    {
        StatusChange = statusChange;
    }

    internal DRPGActionStatusChange StatusChange { get; }

    internal DRPGActionEffectResult(int actorRuntimeId, DRPGActionTimelineChange change)
        : this(DRPGActionEffectKind.TimelineShift, change.WasApplied ? DRPGActionEffectResultState.Applied : DRPGActionEffectResultState.Skipped,
            actorRuntimeId, change.Target.Value, change.Delta,
            (change.After?.ScheduledTime ?? 0f) - (change.Before?.ScheduledTime ?? 0f), change.PriorityDelta,
            timelineBefore: change.Before?.ScheduledTime ?? 0f, timelineAfter: change.After?.ScheduledTime ?? 0f)
    {
        TimelineChange = change;
    }
    internal DRPGActionTimelineChange TimelineChange { get; }

    internal DRPGActionEffectResult(int actorRuntimeId, DRPGActionVitalChange change)
        : this(change.Kind, change.WasApplied ? DRPGActionEffectResultState.Applied : DRPGActionEffectResultState.Skipped,
            actorRuntimeId, change.Target.Value, change.Requested, change.After - change.Before,
            postureBefore: change.Kind == DRPGActionEffectKind.PostureRestore ? change.Before : float.NaN,
            postureAfter: change.Kind == DRPGActionEffectKind.PostureRestore ? change.After : float.NaN)
    {
        VitalChange = change;
    }
    internal DRPGActionVitalChange VitalChange { get; }

    internal DRPGActionEffectResult(int actorRuntimeId, DRPGActionPostureChange change)
        : this(DRPGActionEffectKind.PostureDamage, change.IsImpact || change.WasApplied ? DRPGActionEffectResultState.Applied : DRPGActionEffectResultState.Skipped,
            actorRuntimeId, change.Target.Value, change.Requested, change.Before - change.After,
            change.IsImpact ? change.Before - change.After : 0f, change.IsImpact ? "impact_posture" : string.Empty,
            postureBefore: change.Before, postureAfter: change.After)
    { PostureChange = change; }
    internal DRPGActionPostureChange PostureChange { get; }

    public DRPGActionEffectResult(
        DRPGActionEffectKind kind,
        DRPGActionEffectResultState state,
        int actorRuntimeId,
        int targetRuntimeId,
        float requestedAmount,
        float appliedAmount,
        float auxiliaryAmount = 0f,
        string semanticId = "",
        string message = "",
        DRPGDamageTag damageTags = DRPGDamageTag.None,
        DRPGFormationRow? formationBefore = null,
        DRPGFormationRow? formationAfter = null,
        float timelineBefore = 0f,
        float timelineAfter = 0f,
        float postureBefore = float.NaN,
        float postureAfter = float.NaN)
    {
        Kind = kind;
        State = state;
        ActorRuntimeId = actorRuntimeId;
        TargetRuntimeId = targetRuntimeId;
        RequestedAmount = requestedAmount;
        AppliedAmount = appliedAmount;
        AuxiliaryAmount = auxiliaryAmount;
        SemanticId = semanticId ?? string.Empty;
        Message = message ?? string.Empty;
        DamageTags = damageTags;
        FormationBefore = formationBefore;
        FormationAfter = formationAfter;
        TimelineBefore = timelineBefore;
        TimelineAfter = timelineAfter;
        PostureBefore = postureBefore;
        PostureAfter = postureAfter;
    }

    public DRPGActionEffectKind Kind { get; }
    public DRPGActionEffectResultState State { get; }
    public bool WasApplied => State == DRPGActionEffectResultState.Applied;
    public int ActorRuntimeId { get; }
    public int TargetRuntimeId { get; }
    public float RequestedAmount { get; }
    public float AppliedAmount { get; }
    public float AuxiliaryAmount { get; }
    public string SemanticId { get; }
    public string Message { get; }
    public DRPGDamageTag DamageTags { get; }
    public DRPGFormationRow? FormationBefore { get; }
    public DRPGFormationRow? FormationAfter { get; }
    public float TimelineBefore { get; }
    public float TimelineAfter { get; }
    public float PostureBefore { get; }
    public float PostureAfter { get; }
    public bool HasPostureSnapshot =>
        !float.IsNaN(PostureBefore) && !float.IsNaN(PostureAfter);
    public bool CausedPostureBreak =>
        Kind == DRPGActionEffectKind.PostureDamage &&
        WasApplied &&
        HasPostureSnapshot &&
        PostureBefore > 0f &&
        PostureAfter <= 0f;
}

public sealed class DRPGResolvedActionBatch
{
    public DRPGResolvedActionBatch(
        IReadOnlyList<DRPGBattleHitResult> hits,
        IReadOnlyList<DRPGActionEffectResult> effects)
    {
        Hits = Copy(hits);
        Effects = Copy(effects);
    }

    public IReadOnlyList<DRPGBattleHitResult> Hits { get; }
    public IReadOnlyList<DRPGActionEffectResult> Effects { get; }

    private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source)
    {
        if (source == null || source.Count == 0)
            return Array.Empty<T>();
        var result = new T[source.Count];
        for (int i = 0; i < source.Count; i++)
            result[i] = source[i];
        return Array.AsReadOnly(result);
    }
}

public interface IDRPGBattleEffectEnvironment
{
    IReadOnlyList<BattleParticipantRuntime> Participants { get; }

    bool TryApplyFormationShift(
        BattleParticipantRuntime target,
        int rowDelta,
        out DRPGFormationRow before,
        out DRPGFormationRow after,
        out string error);

    bool TryApplyTimelineShift(
        BattleParticipantRuntime target,
        float timelineDelta,
        int priorityDelta,
        out float before,
        out float after,
        out string error);
}
