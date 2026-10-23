public enum DRPGProgressScope
{
    Battle = 0,
    Expedition = 1
}

public enum DRPGProgressMutationKind
{
    Register = 0,
    Gain = 1,
    Spend = 2,
    Reset = 3
}

public enum DRPGProgressMutationState
{
    Applied = 0,
    RejectedInvalidOwner = 1,
    RejectedInvalidResource = 2,
    RejectedUnsupportedScope = 3,
    RejectedInsufficientValue = 4,
    RejectedInvalidRequest = 5
}

public enum DRPGProgressTraceState
{
    Applied = 0,
    SkippedDuplicate = 1,
    SkippedReactionDepth = 2,
    RejectedInvalidOwner = 3,
    RejectedInvalidResource = 4,
    RejectedUnsupportedScope = 5,
    RejectedInsufficientValue = 6,
    RejectedInvalidRequest = 7,
    SourceFaulted = 8,
    StoreFaulted = 9
}
