using System;
using UnityEngine;

public enum DRPGActionResourceTransactionState
{
    Reserved = 0,
    Committed = 1,
    Cancelled = 2
}

public sealed class DRPGActionResourceReceipt
{
    public DRPGActionResourceReceipt(
        int actorRuntimeId,
        float spentMana,
        float spentPosture,
        int spentStamina = 0)
    {
        ActorRuntimeId = actorRuntimeId;
        SpentMana = Mathf.Max(0f, spentMana);
        SpentPosture = Mathf.Max(0f, spentPosture);
        SpentStamina = Mathf.Max(0, spentStamina);
    }

    public int ActorRuntimeId { get; }
    public float SpentMana { get; }
    public float SpentPosture { get; }
    public int SpentStamina { get; }
}

public sealed class DRPGActionResourceTransaction
{
    private readonly object transactionGate = new object();
    private DRPGActionCostReservation rootCostOwner;
    private DRPGPrimaryImpactKey? boundPrimaryImpact;
    private DRPGBattleMutationWorkspace stagedWorkspace;
    private DRPGActionResourceReceipt stagedReceipt;
    private DRPGActionResourceMutationCheckpoint stagedCheckpoint;
    private bool hasStagedCheckpoint;

    private DRPGActionResourceTransaction(
        int actorRuntimeId,
        float manaCost,
        float postureCost,
        int staminaCost)
    {
        ActorRuntimeId = actorRuntimeId;
        ManaCost = Mathf.Max(0f, manaCost);
        PostureCost = Mathf.Max(0f, postureCost);
        StaminaCost = Mathf.Max(0, staminaCost);
        State = DRPGActionResourceTransactionState.Reserved;
    }

    public int ActorRuntimeId { get; }
    public float ManaCost { get; }
    public float PostureCost { get; }
    public int StaminaCost { get; }
    private DRPGActionResourceTransactionState legacyState;
    private DRPGActionResourceReceipt legacyReceipt;
    public DRPGActionResourceTransactionState State
    { get => rootCostOwner != null ? rootCostOwner.State : legacyState; private set => legacyState = value; }
    public DRPGActionResourceReceipt Receipt
    { get => rootCostOwner != null ? rootCostOwner.CommittedCosts?.Resources : legacyReceipt; private set => legacyReceipt = value; }

    public static bool TryReserve(
        DRPGResolvedActionSpec spec,
        BattleParticipantRuntime actor,
        out DRPGActionResourceTransaction transaction,
        out string error)
    {
        transaction = null;
        if (spec == null || actor == null ||
            spec.ActorRuntimeId != actor.RuntimeId)
        {
            error = "Action resource reservation has an invalid actor or spec.";
            return false;
        }
        if (!actor.CanAffordActionResources(
                spec.ManaCost, spec.PostureCost, spec.StaminaCost))
        {
            error = actor.CurrentMp + 0.0001f < spec.ManaCost
                ? "The actor does not have enough MP for this action."
                : actor.CurrentPosture + 0.0001f < spec.PostureCost
                    ? "The actor does not have enough posture for this action."
                    : "The actor does not have enough stamina for this action.";
            return false;
        }

        transaction = new DRPGActionResourceTransaction(
            actor.RuntimeId,
            spec.ManaCost,
            spec.PostureCost,
            spec.StaminaCost);
        error = string.Empty;
        return true;
    }

    internal bool TryBindPrimaryImpact(
        in DRPGPrimaryImpactKey key,
        out string error)
    {
        lock (transactionGate)
        {
            if (rootCostOwner != null)
            { error = "Complete-action costs cannot be rebound to a primary-only transaction."; return false; }
            if (State == DRPGActionResourceTransactionState.Cancelled)
            {
                error = "The action resource transaction was cancelled.";
                return false;
            }
            if (State == DRPGActionResourceTransactionState.Committed &&
                !boundPrimaryImpact.HasValue)
            {
                error = "Legacy-committed resources cannot be rebound to a primary impact.";
                return false;
            }
            if (boundPrimaryImpact.HasValue &&
                !boundPrimaryImpact.Value.Equals(key))
            {
                error = "Action resources are bound to another primary impact.";
                return false;
            }

            boundPrimaryImpact = key;
            error = string.Empty;
            return true;
        }
    }

    internal bool TryCommit(
        BattleParticipantRuntime actor,
        out DRPGActionResourceReceipt receipt,
        out string error)
    {
        lock (transactionGate)
        {
            receipt = Receipt;
            if (boundPrimaryImpact.HasValue || rootCostOwner != null)
            {
                error = "Primary-impact resources require their bound workspace commit.";
                return false;
            }
            if (State == DRPGActionResourceTransactionState.Committed)
            {
                error = string.Empty;
                return true;
            }
            if (State == DRPGActionResourceTransactionState.Cancelled)
            {
                error = "The action resource transaction was cancelled.";
                return false;
            }
            if (actor == null || actor.RuntimeId != ActorRuntimeId)
            {
                error = "The action resource transaction actor no longer matches.";
                return false;
            }
            if (!actor.TrySpendActionResources(
                    ManaCost,
                    PostureCost,
                    StaminaCost,
                    out float spentMana,
                    out float spentPosture,
                    out int spentStamina))
            {
                error = "The reserved action resources are no longer available.";
                return false;
            }

            Receipt = new DRPGActionResourceReceipt(
                ActorRuntimeId,
                spentMana,
                spentPosture,
                spentStamina);
            receipt = Receipt;
            State = DRPGActionResourceTransactionState.Committed;
            error = string.Empty;
            return true;
        }
    }

    internal bool TryStageIntoWorkspace(
        in DRPGPrimaryImpactKey key,
        DRPGBattleMutationWorkspace workspace,
        out DRPGActionResourceReceipt receipt,
        out string error)
    {
        lock (transactionGate)
        {
            receipt = stagedReceipt ?? Receipt;
            if (!boundPrimaryImpact.HasValue ||
                !boundPrimaryImpact.Value.Equals(key))
            {
                error = "Action resources are not bound to this primary impact.";
                return false;
            }
            if (State == DRPGActionResourceTransactionState.Cancelled)
            {
                error = "The action resource transaction was cancelled.";
                return false;
            }
            if (State == DRPGActionResourceTransactionState.Committed)
            {
                error = "Committed action resources require the original impact receipt.";
                return false;
            }
            if (workspace == null)
            {
                error = "A mutation workspace is required for action resources.";
                return false;
            }
            if (stagedWorkspace != null)
            {
                if (!ReferenceEquals(stagedWorkspace, workspace))
                {
                    error = "Action resources are already staged in another workspace.";
                    return false;
                }
                error = string.Empty;
                return true;
            }

            ParticipantMutationBuilder actor;
            try
            {
                actor = workspace.For(new ParticipantId(ActorRuntimeId));
            }
            catch (InvalidOperationException exception)
            {
                error = exception.Message;
                return false;
            }
            if (actor.Mana + 0.0001f < ManaCost)
            {
                error = "The reserved MP is no longer available in the workspace.";
                return false;
            }
            if (actor.Posture + 0.0001f < PostureCost)
            {
                error = "The reserved posture is no longer available in the workspace.";
                return false;
            }
            if (actor.Stamina.Available < StaminaCost)
            {
                error = "The reserved stamina is no longer available in the workspace.";
                return false;
            }

            stagedCheckpoint = actor.CaptureActionResources();
            hasStagedCheckpoint = true;
            if (ManaCost > 0f)
                actor.SetMana(Mathf.Max(0f, actor.Mana - ManaCost));
            if (PostureCost > 0f)
                actor.SetPosture(Mathf.Max(0f, actor.Posture - PostureCost));
            if (StaminaCost > 0)
            {
                if (!actor.Stamina.TrySpend(StaminaCost, out BattleStaminaValue next))
                {
                    RestoreStagedWorkspace();
                    error = "The reserved stamina could not be staged.";
                    return false;
                }
                actor.SetStamina(next);
            }
            stagedWorkspace = workspace;
            stagedReceipt = new DRPGActionResourceReceipt(
                ActorRuntimeId,
                ManaCost,
                PostureCost,
                StaminaCost);
            receipt = stagedReceipt;
            error = string.Empty;
            return true;
        }
    }

    internal void CompleteWorkspaceCommit(
        in DRPGPrimaryImpactKey key,
        DRPGBattleMutationWorkspace workspace,
        DRPGActionResourceReceipt receipt)
    {
        lock (transactionGate)
        {
            if (State == DRPGActionResourceTransactionState.Committed)
            {
                if (boundPrimaryImpact.HasValue &&
                    boundPrimaryImpact.Value.Equals(key) &&
                    ReferenceEquals(Receipt, receipt))
                {
                    return;
                }
                throw new InvalidOperationException(
                    "Committed action resources belong to another impact receipt.");
            }
            if (!boundPrimaryImpact.HasValue ||
                !boundPrimaryImpact.Value.Equals(key) ||
                !ReferenceEquals(stagedWorkspace, workspace) ||
                stagedReceipt == null ||
                !ReferenceEquals(stagedReceipt, receipt))
            {
                throw new InvalidOperationException(
                    "Only the bound workspace resource receipt can be committed.");
            }
            Receipt = stagedReceipt;
            State = DRPGActionResourceTransactionState.Committed;
            ClearStage();
        }
    }

    internal void ReleaseWorkspaceStage(
        in DRPGPrimaryImpactKey key,
        DRPGBattleMutationWorkspace workspace)
    {
        lock (transactionGate)
        {
            if (!boundPrimaryImpact.HasValue ||
                !boundPrimaryImpact.Value.Equals(key) ||
                !ReferenceEquals(stagedWorkspace, workspace) ||
                State != DRPGActionResourceTransactionState.Reserved)
            {
                return;
            }
            RestoreStagedWorkspace();
            ClearStage();
        }
    }

    public bool CanCommit(BattleParticipantRuntime actor, out string error)
    {
        lock (transactionGate)
        {
            if (boundPrimaryImpact.HasValue || rootCostOwner != null)
            {
                error = "Primary-impact resources require their bound workspace commit.";
                return false;
            }
            if (State == DRPGActionResourceTransactionState.Cancelled)
            {
                error = "The action resource transaction was cancelled.";
                return false;
            }
            if (State == DRPGActionResourceTransactionState.Committed)
            {
                error = string.Empty;
                return true;
            }
            if (actor == null || actor.RuntimeId != ActorRuntimeId)
            {
                error = "The action resource transaction actor no longer matches.";
                return false;
            }
            if (!actor.CanAffordActionResources(
                    ManaCost, PostureCost, StaminaCost))
            {
                error = "The reserved action resources are no longer available.";
                return false;
            }
            error = string.Empty;
            return true;
        }
    }

    public bool Cancel()
    {
        lock (transactionGate)
        {
            if (rootCostOwner != null || State != DRPGActionResourceTransactionState.Reserved)
                return false;
            RestoreStagedWorkspace();
            ClearStage();
            State = DRPGActionResourceTransactionState.Cancelled;
            return true;
        }
    }

    private void RestoreStagedWorkspace()
    {
        if (stagedWorkspace == null || !hasStagedCheckpoint)
            return;
        stagedWorkspace.For(new ParticipantId(ActorRuntimeId))
            .RestoreActionResources(stagedCheckpoint);
    }

    internal void ClaimRootCostOwnership(DRPGActionCostReservation owner)
    {
        lock (transactionGate)
        {
            if (owner == null || rootCostOwner != null || boundPrimaryImpact.HasValue ||
                State != DRPGActionResourceTransactionState.Reserved || stagedWorkspace != null)
                throw new InvalidOperationException("Only an unused resource reservation can acquire a root cost owner.");
            rootCostOwner = owner;
        }
    }


    private void ClearStage()
    {
        stagedWorkspace = null;
        stagedReceipt = null;
        stagedCheckpoint = default;
        hasStagedCheckpoint = false;
    }
}
