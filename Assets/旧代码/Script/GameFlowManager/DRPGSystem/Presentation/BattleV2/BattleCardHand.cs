using System;

public enum BattleCardTargetRule
{
    Ally,
    Enemy,
    AnyLiving
}

public sealed class BattleCardHand
{
    private BattleCardTargetRule?[] slots = new BattleCardTargetRule?[4];

    public int SlotCount => slots.Length;

    public void Reset(BattleCardTargetRule?[] fourRules)
    {
        if (fourRules == null)
            throw new ArgumentNullException(nameof(fourRules));
        if (fourRules.Length != 4)
            throw new ArgumentException("Preview hand requires exactly four slots.",
                nameof(fourRules));
        slots = (BattleCardTargetRule?[])fourRules.Clone();
    }

    public bool HasCard(int slot) => IsSlot(slot) && slots[slot].HasValue;

    public BattleCardTargetRule? GetTargetRule(int slot) =>
        IsSlot(slot) ? slots[slot] : null;

    public bool CanTarget(
        int slot,
        bool targetIsEnemy,
        bool alive,
        bool deployed)
    {
        if (!HasCard(slot) || !alive || !deployed)
            return false;
        BattleCardTargetRule rule = slots[slot].Value;
        return rule == BattleCardTargetRule.AnyLiving ||
            rule == BattleCardTargetRule.Enemy && targetIsEnemy ||
            rule == BattleCardTargetRule.Ally && !targetIsEnemy;
    }

    public bool TryUse(
        int slot,
        bool targetIsEnemy,
        bool alive,
        bool deployed)
    {
        if (!CanTarget(slot, targetIsEnemy, alive, deployed))
            return false;
        slots[slot] = null;
        return true;
    }

    private bool IsSlot(int slot) => slot >= 0 && slot < slots.Length;
}
