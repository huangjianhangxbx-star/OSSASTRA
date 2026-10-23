using System;

public sealed class DRPGResolvedImpactPartition
{
    public DRPGResolvedImpactPartition(
        int index,
        int targetRuntimeId,
        DRPGBattleImpactContext context)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (targetRuntimeId < 0)
            throw new ArgumentOutOfRangeException(nameof(targetRuntimeId));

        Index = index;
        TargetRuntimeId = targetRuntimeId;
        Context = context;
    }

    public int Index { get; }
    public int TargetRuntimeId { get; }
    public DRPGBattleImpactContext Context { get; }
}
