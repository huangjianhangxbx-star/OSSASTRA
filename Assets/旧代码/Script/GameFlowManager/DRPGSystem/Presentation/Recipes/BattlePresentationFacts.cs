using System;
using System.Collections.Generic;

public sealed class BattlePresentationFacts
{
    private static readonly BattlePresentationFacts empty =
        new BattlePresentationFacts(
            Array.Empty<int>(),
            Array.Empty<int>(),
            Array.Empty<int>());

    public BattlePresentationFacts(
        IEnumerable<int> damageBrokenCutInTargetIds,
        IEnumerable<int> defeatedTargetIds,
        IEnumerable<int> redirectedTargetIds)
    {
        DamageBrokenCutInTargetIds = CopyDistinct(damageBrokenCutInTargetIds);
        DefeatedTargetIds = CopyDistinct(defeatedTargetIds);
        RedirectedTargetIds = CopyDistinct(redirectedTargetIds);
    }

    public static BattlePresentationFacts Empty => empty;
    public IReadOnlyList<int> DamageBrokenCutInTargetIds { get; }
    public IReadOnlyList<int> DefeatedTargetIds { get; }
    public IReadOnlyList<int> RedirectedTargetIds { get; }
    public bool AnyHitTriggeredDamageBroken => DamageBrokenCutInTargetIds.Count > 0;
    public bool AnyTargetDefeated => DefeatedTargetIds.Count > 0;
    public bool AnyHitRedirected => RedirectedTargetIds.Count > 0;

    public static BattlePresentationFacts FromHits(
        IEnumerable<BattleHitPresentationSnapshot> hits)
    {
        if (hits == null)
            throw new ArgumentNullException(nameof(hits));

        var damageBrokenTargets = new List<int>();
        var defeatedTargets = new List<int>();
        var redirectedTargets = new List<int>();
        foreach (BattleHitPresentationSnapshot hit in hits)
        {
            if (hit == null)
                throw new ArgumentException("Hits cannot contain null.", nameof(hits));
            if (hit.TriggeredDamageBroken &&
                hit.TargetRosterRole != DRPGBattleRosterRole.Protagonist)
            {
                damageBrokenTargets.Add(hit.TargetRuntimeId);
            }
            if (hit.WasDefeated)
                defeatedTargets.Add(hit.TargetRuntimeId);
            if (hit.WasRedirected)
                redirectedTargets.Add(hit.TargetRuntimeId);
        }

        return new BattlePresentationFacts(
            damageBrokenTargets,
            defeatedTargets,
            redirectedTargets);
    }

    private static IReadOnlyList<int> CopyDistinct(IEnumerable<int> source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        var values = new List<int>();
        var seen = new HashSet<int>();
        foreach (int value in source)
        {
            if (seen.Add(value))
                values.Add(value);
        }
        return Array.AsReadOnly(values.ToArray());
    }
}
