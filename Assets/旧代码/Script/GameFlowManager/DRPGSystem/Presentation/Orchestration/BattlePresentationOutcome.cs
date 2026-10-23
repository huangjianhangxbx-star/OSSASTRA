public enum BattlePresentationOutcome
{
    Pending = 0,
    Completed = 1,
    Skipped = 2,
    Cancelled = 3,
    Faulted = 4,
    TimedOut = 5
}

public enum BattlePresentationCancelReason
{
    Replaced = 0,
    ScopeEnded = 1,
    BattleHidden = 2,
    SessionAborted = 3,
    TimedOut = 4,
    Faulted = 5
}
