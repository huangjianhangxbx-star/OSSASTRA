using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct DRPGFreeStrikeTargetGeometry
{
    public DRPGFreeStrikeTargetGeometry(int targetRuntimeId, Rect bodyBounds)
    {
        if (targetRuntimeId <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetRuntimeId));
        TargetRuntimeId = targetRuntimeId;
        BodyBounds = bodyBounds;
    }

    public int TargetRuntimeId { get; }
    public Rect BodyBounds { get; }
}

public readonly struct DRPGFreeStrikeTargetHit
{
    public DRPGFreeStrikeTargetHit(int targetRuntimeId)
    {
        if (targetRuntimeId <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetRuntimeId));
        TargetRuntimeId = targetRuntimeId;
    }

    public int TargetRuntimeId { get; }
}

public static class DRPGFreeStrikeBattlefieldGeometry
{
    public static DRPGFreeStrikeTargetHit[] ClassifyTargets(
        DRPGFreeStrikeSample sample,
        IReadOnlyList<DRPGFreeStrikeTargetGeometry> targets)
    {
        if (targets == null)
            throw new ArgumentNullException(nameof(targets));

        var hits = new List<DRPGFreeStrikeTargetHit>(targets.Count);
        for (int i = 0; i < targets.Count; i++)
        {
            DRPGFreeStrikeTargetGeometry target = targets[i];
            if (DRPGFreeStrikeGeometry.IntersectsBody(sample, target.BodyBounds))
                hits.Add(new DRPGFreeStrikeTargetHit(target.TargetRuntimeId));
        }
        return hits.ToArray();
    }
}
