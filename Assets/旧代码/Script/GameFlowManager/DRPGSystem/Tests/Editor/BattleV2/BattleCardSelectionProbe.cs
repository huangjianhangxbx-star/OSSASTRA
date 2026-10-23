#if UNITY_EDITOR
using System;

public static class BattleCardSelectionProbe
{
    public static void RunAll()
    {
        OneInputSerialCannotCancelAndSelect();
        LockedSelectionCannotBeCancelled();
        CardRulesRejectIllegalTargetsWithoutConsumption();
        UsedCardLeavesItsOriginalSlotEmpty();
    }

    private static void OneInputSerialCannotCancelAndSelect()
    {
        var mode = new BattleSelectionMode();
        mode.Reset(1);
        Check(mode.TryBegin(BattleSelectionKind.Skill, 0, 10),
            "skill selection failed");
        Check(mode.Cancel(11), "selection cancel failed");
        Check(!mode.TryBegin(BattleSelectionKind.Card, 0, 11),
            "one right click cancelled skill and selected card");
        Check(mode.TryBegin(BattleSelectionKind.Card, 0, 12),
            "next independent input was rejected");
    }

    private static void LockedSelectionCannotBeCancelled()
    {
        var mode = new BattleSelectionMode();
        mode.Reset(2);
        Check(mode.TryBegin(BattleSelectionKind.Card, 1, 20),
            "card selection failed");
        mode.Lock();
        Check(mode.Kind == BattleSelectionKind.Locked && !mode.Cancel(21),
            "locked action was cancelled");
        mode.Reset(3);
        Check(mode.Kind == BattleSelectionKind.None,
            "new battle generation retained old selection");
    }

    private static void CardRulesRejectIllegalTargetsWithoutConsumption()
    {
        var hand = new BattleCardHand();
        hand.Reset(new BattleCardTargetRule?[] {
            BattleCardTargetRule.Ally,
            BattleCardTargetRule.Enemy,
            BattleCardTargetRule.AnyLiving,
            null });

        Check(!hand.TryUse(0, true, true, true) && hand.HasCard(0),
            "illegal enemy consumed ally card");
        Check(!hand.TryUse(1, false, true, true) && hand.HasCard(1),
            "illegal ally consumed enemy card");
        Check(!hand.TryUse(2, false, false, true) && hand.HasCard(2),
            "dead target consumed card");
        Check(!hand.TryUse(2, false, true, false) && hand.HasCard(2),
            "undeployed target consumed card");
    }

    private static void UsedCardLeavesItsOriginalSlotEmpty()
    {
        var hand = new BattleCardHand();
        hand.Reset(new BattleCardTargetRule?[] {
            BattleCardTargetRule.Ally,
            BattleCardTargetRule.Enemy,
            BattleCardTargetRule.AnyLiving,
            null });

        Check(hand.TryUse(0, false, true, true), "legal card use failed");
        Check(!hand.HasCard(0) && hand.HasCard(1) && hand.HasCard(2),
            "card use repacked or refilled hand");
        Check(hand.SlotCount == 4, "hand lost fixed slot count");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
