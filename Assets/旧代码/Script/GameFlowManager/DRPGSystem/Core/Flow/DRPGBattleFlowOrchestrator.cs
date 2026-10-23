using System;

/// <summary>
/// Pure domain authority for battle lifecycle and action-transaction gates.
/// It deliberately contains no scheduling, damage, AI, presentation, or Unity
/// dependencies. Those modules may request transitions, but cannot bypass them.
/// </summary>
public sealed class DRPGBattleFlowOrchestrator
{
    public DRPGBattleLifecycleState LifecycleState { get; private set; } =
        DRPGBattleLifecycleState.Inactive;
    public DRPGBattleActionFlowStage ActionStage { get; private set; } =
        DRPGBattleActionFlowStage.None;
    public long ActiveActionId { get; private set; }

    public DRPGBattlePhase LegacyPhase
    {
        get
        {
            switch (LifecycleState)
            {
                case DRPGBattleLifecycleState.Inactive:
                    return DRPGBattlePhase.Inactive;
                case DRPGBattleLifecycleState.Starting:
                    return DRPGBattlePhase.Starting;
                case DRPGBattleLifecycleState.TurnBegin:
                    return DRPGBattlePhase.TurnBegin;
                case DRPGBattleLifecycleState.AwaitingCommand:
                    return DRPGBattlePhase.AwaitingCommand;
                case DRPGBattleLifecycleState.TargetSelection:
                    return DRPGBattlePhase.TargetSelection;
                case DRPGBattleLifecycleState.TurnSettlement:
                    return ActionStage == DRPGBattleActionFlowStage.Finalizing
                        ? DRPGBattlePhase.TurnEnd
                        : DRPGBattlePhase.ActionEnd;
                case DRPGBattleLifecycleState.Result:
                    return DRPGBattlePhase.Result;
                case DRPGBattleLifecycleState.ActionTransaction:
                    if (ActionStage ==
                        DRPGBattleActionFlowStage.InteractiveResolution)
                    {
                        return DRPGBattlePhase.InteractiveAction;
                    }
                    return ActionStage == DRPGBattleActionFlowStage.Locked
                        ? DRPGBattlePhase.PreparingAction
                        : DRPGBattlePhase.ExecutingAction;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public bool TryStartBattle(out string error)
    {
        if (LifecycleState != DRPGBattleLifecycleState.Inactive)
            return Reject("Only an inactive battle can start.", out error);

        LifecycleState = DRPGBattleLifecycleState.Starting;
        ClearAction();
        return Accept(out error);
    }

    public bool TryBeginTurn(out string error)
    {
        if (LifecycleState != DRPGBattleLifecycleState.Starting)
            return Reject("The initial turn can only begin after battle start.", out error);

        LifecycleState = DRPGBattleLifecycleState.TurnBegin;
        return Accept(out error);
    }

    public bool TryOpenCommandGate(out string error)
    {
        if (LifecycleState != DRPGBattleLifecycleState.TurnBegin)
            return Reject("Commands can only open from turn begin.", out error);

        LifecycleState = DRPGBattleLifecycleState.AwaitingCommand;
        return Accept(out error);
    }

    public bool TryBeginTargetSelection(out string error)
    {
        if (LifecycleState != DRPGBattleLifecycleState.AwaitingCommand &&
            LifecycleState != DRPGBattleLifecycleState.TargetSelection)
        {
            return Reject(
                "Target selection can only begin at the command gate.",
                out error);
        }

        LifecycleState = DRPGBattleLifecycleState.TargetSelection;
        return Accept(out error);
    }

    public bool TryCancelTargetSelection(out string error)
    {
        if (LifecycleState != DRPGBattleLifecycleState.TargetSelection ||
            ActionStage != DRPGBattleActionFlowStage.None)
        {
            return Reject("There is no cancellable target selection.", out error);
        }

        LifecycleState = DRPGBattleLifecycleState.AwaitingCommand;
        return Accept(out error);
    }

    public bool TryLockAction(long actionId, out string error)
    {
        if (actionId <= 0L)
            return Reject("An action identity must be positive.", out error);
        if (LifecycleState != DRPGBattleLifecycleState.AwaitingCommand &&
            LifecycleState != DRPGBattleLifecycleState.TargetSelection)
        {
            return Reject("An action can only lock from command input.", out error);
        }
        if (ActiveActionId != 0L || ActionStage != DRPGBattleActionFlowStage.None)
            return Reject("Another action transaction already owns the flow.", out error);

        LifecycleState = DRPGBattleLifecycleState.ActionTransaction;
        ActionStage = DRPGBattleActionFlowStage.Locked;
        ActiveActionId = actionId;
        return Accept(out error);
    }

    public bool TryBeginImpact(long actionId, out string error)
    {
        if (!RequireAction(
                actionId,
                DRPGBattleLifecycleState.ActionTransaction,
                DRPGBattleActionFlowStage.Locked,
                out error))
        {
            return false;
        }

        ActionStage = DRPGBattleActionFlowStage.ResolvingImpact;
        return Accept(out error);
    }

    public bool TryBeginInteractiveResolution(long actionId, out string error)
    {
        if (!RequireAction(
                actionId,
                DRPGBattleLifecycleState.ActionTransaction,
                DRPGBattleActionFlowStage.Locked,
                out error))
        {
            return false;
        }

        ActionStage = DRPGBattleActionFlowStage.InteractiveResolution;
        return Accept(out error);
    }

    public bool TryCompleteImpact(long actionId, out string error)
    {
        if (!RequireActionIdentity(actionId, out error) ||
            LifecycleState != DRPGBattleLifecycleState.ActionTransaction ||
            ActionStage != DRPGBattleActionFlowStage.ResolvingImpact &&
            ActionStage != DRPGBattleActionFlowStage.InteractiveResolution)
        {
            if (string.IsNullOrEmpty(error))
                error = "Impact can only complete from an active resolution stage.";
            return false;
        }

        ActionStage = DRPGBattleActionFlowStage.ImpactResolved;
        return Accept(out error);
    }

    public bool TryBeginCommit(long actionId, out string error)
    {
        if (!RequireAction(
                actionId,
                DRPGBattleLifecycleState.ActionTransaction,
                DRPGBattleActionFlowStage.ImpactResolved,
                out error))
        {
            return false;
        }

        ActionStage = DRPGBattleActionFlowStage.Committing;
        return Accept(out error);
    }

    public bool TryCompleteCommit(long actionId, out string error)
    {
        if (!RequireAction(
                actionId,
                DRPGBattleLifecycleState.ActionTransaction,
                DRPGBattleActionFlowStage.Committing,
                out error))
        {
            return false;
        }

        LifecycleState = DRPGBattleLifecycleState.TurnSettlement;
        ActionStage =
            DRPGBattleActionFlowStage.AwaitingSettlementPresentation;
        return Accept(out error);
    }

    public bool TryBeginFinalization(long actionId, out string error)
    {
        if (!RequireAction(
                actionId,
                DRPGBattleLifecycleState.TurnSettlement,
                DRPGBattleActionFlowStage.AwaitingSettlementPresentation,
                out error))
        {
            return false;
        }

        ActionStage = DRPGBattleActionFlowStage.Finalizing;
        return Accept(out error);
    }

    public bool TryCompleteFinalizationToNextTurn(
        long actionId,
        out string error)
    {
        if (!RequireAction(
                actionId,
                DRPGBattleLifecycleState.TurnSettlement,
                DRPGBattleActionFlowStage.Finalizing,
                out error))
        {
            return false;
        }

        ClearAction();
        LifecycleState = DRPGBattleLifecycleState.TurnBegin;
        return Accept(out error);
    }

    public bool TryCompleteFinalizationToResult(
        long actionId,
        out string error)
    {
        if (!RequireAction(
                actionId,
                DRPGBattleLifecycleState.TurnSettlement,
                DRPGBattleActionFlowStage.Finalizing,
                out error))
        {
            return false;
        }

        ClearAction();
        LifecycleState = DRPGBattleLifecycleState.Result;
        return Accept(out error);
    }

    public bool TryCancelActionToCommand(long actionId, out string error)
    {
        if (!RequireActionIdentity(actionId, out error) ||
            LifecycleState != DRPGBattleLifecycleState.ActionTransaction ||
            ActionStage != DRPGBattleActionFlowStage.Locked &&
            ActionStage != DRPGBattleActionFlowStage.ResolvingImpact)
        {
            if (string.IsNullOrEmpty(error))
                error = "The action has crossed the reversible transaction boundary.";
            return false;
        }

        ClearAction();
        LifecycleState = DRPGBattleLifecycleState.AwaitingCommand;
        return Accept(out error);
    }

    public bool TryEnterResult(out string error)
    {
        if (LifecycleState == DRPGBattleLifecycleState.Inactive)
            return Reject("An inactive battle cannot publish a result.", out error);

        ClearAction();
        LifecycleState = DRPGBattleLifecycleState.Result;
        return Accept(out error);
    }

    public void Reset()
    {
        ClearAction();
        LifecycleState = DRPGBattleLifecycleState.Inactive;
    }

    private bool RequireAction(
        long actionId,
        DRPGBattleLifecycleState lifecycle,
        DRPGBattleActionFlowStage stage,
        out string error)
    {
        if (!RequireActionIdentity(actionId, out error))
            return false;
        if (LifecycleState != lifecycle || ActionStage != stage)
        {
            error =
                $"Action {actionId} cannot transition from " +
                $"{LifecycleState}/{ActionStage}.";
            return false;
        }
        return true;
    }

    private bool RequireActionIdentity(long actionId, out string error)
    {
        if (actionId <= 0L || ActiveActionId != actionId)
        {
            error =
                $"Stale or unknown action identity. expected={ActiveActionId}, " +
                $"actual={actionId}.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void ClearAction()
    {
        ActiveActionId = 0L;
        ActionStage = DRPGBattleActionFlowStage.None;
    }

    private static bool Accept(out string error)
    {
        error = string.Empty;
        return true;
    }

    private static bool Reject(string message, out string error)
    {
        error = message;
        return false;
    }
}
