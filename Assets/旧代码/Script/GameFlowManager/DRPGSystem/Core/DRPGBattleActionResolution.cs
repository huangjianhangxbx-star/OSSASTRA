using System;
using System.Collections.Generic;

public sealed class DRPGBattleActionResolution
{
    public DRPGBattleActionResolution(
        DRPGBattlePreparedAction preparedAction,
        IReadOnlyList<DRPGBattleHitResult> hits)
        : this(
            preparedAction,
            hits,
            Array.Empty<DRPGActionEffectResult>())
    {
    }

    public DRPGBattleActionResolution(
        DRPGBattlePreparedAction preparedAction,
        IReadOnlyList<DRPGBattleHitResult> hits,
        IReadOnlyList<DRPGActionEffectResult> effects)
        : this(preparedAction, hits, effects, 0, true)
    {
    }

    public DRPGBattleActionResolution(
        DRPGBattlePreparedAction preparedAction,
        IReadOnlyList<DRPGBattleHitResult> hits,
        IReadOnlyList<DRPGActionEffectResult> effects,
        int impactIndex,
        bool completesImpactSequence)
    {
        PreparedAction = preparedAction ??
            throw new ArgumentNullException(nameof(preparedAction));
        Hits = Copy(hits ?? throw new ArgumentNullException(nameof(hits)));
        Effects = Copy(
            effects ?? throw new ArgumentNullException(nameof(effects)));
        ImpactIndex = impactIndex;
        CompletesImpactSequence = completesImpactSequence;
    }

    public DRPGBattlePreparedAction PreparedAction { get; }
    public IReadOnlyList<DRPGBattleHitResult> Hits { get; }
    public IReadOnlyList<DRPGActionEffectResult> Effects { get; }
    public int ImpactIndex { get; }
    public bool CompletesImpactSequence { get; }
    public bool HasPrimaryHitOutcome => Hits.Count > 0;
    public DRPGHitOutcome PrimaryHitOutcome => HasPrimaryHitOutcome
        ? Hits[0].Outcome
        : DRPGHitOutcome.Hit;
    public DRPGActionResourceReceipt ResourceReceipt =>
        PreparedAction.ResourceReceipt;

    private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source)
    {
        if (source.Count == 0)
            return Array.Empty<T>();
        var result = new T[source.Count];
        for (int i = 0; i < source.Count; i++)
            result[i] = source[i];
        return Array.AsReadOnly(result);
    }
}
