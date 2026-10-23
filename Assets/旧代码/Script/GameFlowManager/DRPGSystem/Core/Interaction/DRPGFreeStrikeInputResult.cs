using System;
using System.Collections.Generic;

public sealed class DRPGFreeStrikeImpactResult
{
    public DRPGFreeStrikeImpactResult(
        DRPGFreeStrikeBodyContact contact,
        DRPGBattleHitResult hit)
    {
        Contact = contact;
        Hit = hit ?? throw new ArgumentNullException(nameof(hit));
    }

    public DRPGFreeStrikeBodyContact Contact { get; }
    public DRPGBattleHitResult Hit { get; }
    public DRPGFreeStrikeImpactRole Role => Contact.Role;
}

public sealed class DRPGFreeStrikeInputResult
{
    public DRPGFreeStrikeInputResult(
        IReadOnlyList<DRPGFreeStrikeImpactResult> impacts,
        bool actorPostureExhausted,
        DRPGStrikeGrade highestStrikeGrade,
        bool windowResourceExhausted = false)
    {
        if (impacts == null)
            throw new ArgumentNullException(nameof(impacts));
        var copy = new DRPGFreeStrikeImpactResult[impacts.Count];
        for (int i = 0; i < impacts.Count; i++)
            copy[i] = impacts[i];
        Impacts = copy;
        ActorPostureExhausted = actorPostureExhausted;
        HighestStrikeGrade = highestStrikeGrade;
        WindowResourceExhausted = windowResourceExhausted;
    }

    public IReadOnlyList<DRPGFreeStrikeImpactResult> Impacts { get; }
    public bool ActorPostureExhausted { get; }
    public DRPGStrikeGrade HighestStrikeGrade { get; }
    public bool WindowResourceExhausted { get; }
}
