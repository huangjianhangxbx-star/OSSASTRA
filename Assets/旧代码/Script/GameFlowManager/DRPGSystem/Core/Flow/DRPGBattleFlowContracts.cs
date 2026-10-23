public enum DRPGBattleLifecycleState
{
    Inactive = 0,
    Starting = 1,
    TurnBegin = 2,
    AwaitingCommand = 3,
    TargetSelection = 4,
    ActionTransaction = 5,
    TurnSettlement = 6,
    Result = 7
}

public enum DRPGBattleActionFlowStage
{
    None = 0,
    Locked = 1,
    ResolvingImpact = 2,
    InteractiveResolution = 3,
    ImpactResolved = 4,
    Committing = 5,
    AwaitingSettlementPresentation = 6,
    Finalizing = 7
}
