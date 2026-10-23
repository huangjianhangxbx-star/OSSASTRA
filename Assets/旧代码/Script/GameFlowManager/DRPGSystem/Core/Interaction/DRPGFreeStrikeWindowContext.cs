using System;

public sealed class DRPGFreeStrikeWindowContext
{
    public DRPGFreeStrikeWindowContext(
        long actionId,
        DRPGResolvedFreeStrikeSpec spec)
    {
        if (actionId <= 0)
            throw new ArgumentOutOfRangeException(nameof(actionId));
        ActionId = actionId;
        Spec = spec;
    }

    public long ActionId { get; }
    public DRPGResolvedFreeStrikeSpec Spec { get; }
    public bool IsResourceExhausted { get; private set; }

    public void MarkResourceExhausted()
    {
        IsResourceExhausted = true;
    }
}
