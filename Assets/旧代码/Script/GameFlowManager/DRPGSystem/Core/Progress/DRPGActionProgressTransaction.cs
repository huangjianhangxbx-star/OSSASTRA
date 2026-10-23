using System;

public enum DRPGActionProgressTransactionState
{
    Reserved = 0,
    Committed = 1,
    Cancelled = 2
}

public sealed class DRPGActionProgressTransaction
{
    private DRPGActionCostReservation rootCostOwner;
    private DRPGActionProgressTransaction(
        string ownerStableId,
        DRPGResolvedProgressCost cost)
    {
        OwnerStableId = ownerStableId ?? string.Empty;
        Cost = cost ?? DRPGResolvedProgressCost.None;
        State = DRPGActionProgressTransactionState.Reserved;
    }

    public string OwnerStableId { get; }
    public DRPGResolvedProgressCost Cost { get; }
    private DRPGActionProgressTransactionState legacyState;
    private DRPGProgressMutationResult legacyMutation;
    public DRPGActionProgressTransactionState State
    {
        get => rootCostOwner == null ? legacyState : rootCostOwner.State == DRPGActionResourceTransactionState.Committed
            ? DRPGActionProgressTransactionState.Committed : rootCostOwner.State == DRPGActionResourceTransactionState.Cancelled
                ? DRPGActionProgressTransactionState.Cancelled : DRPGActionProgressTransactionState.Reserved;
        private set => legacyState = value;
    }
    public DRPGProgressMutationResult Mutation
    { get => rootCostOwner != null ? rootCostOwner.CommittedCosts?.Progress : legacyMutation; private set => legacyMutation = value; }

    public static bool TryReserve(
        DRPGResolvedActionSpec spec,
        BattleParticipantRuntime actor,
        DRPGBattleProgressPipeline pipeline,
        out DRPGActionProgressTransaction transaction,
        out string error)
    {
        transaction = null;
        if (spec == null || actor == null || pipeline == null ||
            actor.Profile == null || spec.ActorRuntimeId != actor.RuntimeId)
        {
            error = "Action progress reservation has an invalid actor, spec, or pipeline.";
            return false;
        }
        string owner = actor.Profile.CombatantId;
        if (string.IsNullOrWhiteSpace(owner))
        {
            error = "Action progress owner requires a stable character ID.";
            return false;
        }
        if (!pipeline.CanSpend(
                owner,
                spec.ActionProgressCost,
                DRPGProgressScope.Battle,
                out error))
        {
            return false;
        }
        transaction = new DRPGActionProgressTransaction(
            owner.Trim(),
            spec.ActionProgressCost);
        error = string.Empty;
        return true;
    }

    public bool CanCommit(
        DRPGBattleProgressPipeline pipeline,
        out string error)
    {
        if (rootCostOwner != null)
        { error = "Complete-action progress requires its root cost journal commit."; return false; }
        if (State == DRPGActionProgressTransactionState.Cancelled)
        {
            error = "The action progress transaction was cancelled.";
            return false;
        }
        if (State == DRPGActionProgressTransactionState.Committed)
        {
            error = string.Empty;
            return true;
        }
        return pipeline.CanSpend(
            OwnerStableId,
            Cost,
            DRPGProgressScope.Battle,
            out error);
    }

    public bool TryCommit(
        DRPGBattleProgressPipeline pipeline,
        out DRPGProgressMutationResult mutation,
        out string error)
    {
        mutation = Mutation;
        if (!CanCommit(pipeline, out error))
            return false;
        if (State == DRPGActionProgressTransactionState.Committed)
            return true;
        if (!Cost.IsConfigured)
        {
            State = DRPGActionProgressTransactionState.Committed;
            error = string.Empty;
            return true;
        }
        if (!pipeline.TrySpend(
                OwnerStableId,
                Cost.ResourceId,
                DRPGProgressScope.Battle,
                Cost.Amount,
                out mutation,
                out error))
        {
            return false;
        }
        Mutation = mutation;
        State = DRPGActionProgressTransactionState.Committed;
        return true;
    }

    public bool Cancel()
    {
        if (rootCostOwner != null || State != DRPGActionProgressTransactionState.Reserved)
            return false;
        State = DRPGActionProgressTransactionState.Cancelled;
        return true;
    }

    internal void ClaimRootCostOwnership(DRPGActionCostReservation owner)
    {
        if (owner == null || rootCostOwner != null || State != DRPGActionProgressTransactionState.Reserved)
            throw new InvalidOperationException("Only an unused progress reservation can acquire a root cost owner.");
        rootCostOwner = owner;
    }

}
