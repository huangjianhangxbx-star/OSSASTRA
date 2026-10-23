public static class BattleConflictPresentationLifecycle
{
    public static bool ShouldRestoreAfterRun(
        BattlePresentationMoment moment,
        BattlePresentationOutcome outcome,
        BattlePresentationOutcome gateOutcome)
    {
        if (moment == BattlePresentationMoment.ActionSettled)
            return true;

        if (outcome == BattlePresentationOutcome.Faulted ||
            outcome == BattlePresentationOutcome.TimedOut)
        {
            return true;
        }

        // A blocking action-flow beat may finish successfully while a trailing,
        // non-blocking decoration (quote/audio/etc.) is superseded by Impact.
        // That replacement is a normal phase transition, not a failed conflict.
        return outcome == BattlePresentationOutcome.Cancelled &&
            gateOutcome != BattlePresentationOutcome.Completed;
    }
}
