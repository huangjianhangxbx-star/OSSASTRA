using System;

public enum BattleSelectionKind
{
    None,
    Skill,
    Card,
    Locked
}

public sealed class BattleSelectionMode
{
    private long generation;
    private long lastConsumedInputSerial;

    public BattleSelectionKind Kind { get; private set; }
    public int Slot { get; private set; } = -1;
    public long Generation => generation;

    public void Reset(long battleGeneration)
    {
        if (battleGeneration <= 0)
            throw new ArgumentOutOfRangeException(nameof(battleGeneration));
        generation = battleGeneration;
        lastConsumedInputSerial = 0;
        Kind = BattleSelectionKind.None;
        Slot = -1;
    }

    public bool TryBegin(BattleSelectionKind kind, int slot, long inputSerial)
    {
        if (kind != BattleSelectionKind.Skill && kind != BattleSelectionKind.Card)
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (slot < 0)
            throw new ArgumentOutOfRangeException(nameof(slot));
        if (!CanConsume(inputSerial) || Kind != BattleSelectionKind.None)
            return false;

        lastConsumedInputSerial = inputSerial;
        Kind = kind;
        Slot = slot;
        return true;
    }

    public bool Cancel(long inputSerial)
    {
        if (!CanConsume(inputSerial) || Kind == BattleSelectionKind.None ||
            Kind == BattleSelectionKind.Locked)
            return false;

        lastConsumedInputSerial = inputSerial;
        Kind = BattleSelectionKind.None;
        Slot = -1;
        return true;
    }

    public void Lock()
    {
        if (Kind == BattleSelectionKind.Skill || Kind == BattleSelectionKind.Card)
        {
            Kind = BattleSelectionKind.Locked;
            Slot = -1;
        }
    }

    private bool CanConsume(long inputSerial) =>
        generation > 0 && inputSerial > lastConsumedInputSerial;
}
