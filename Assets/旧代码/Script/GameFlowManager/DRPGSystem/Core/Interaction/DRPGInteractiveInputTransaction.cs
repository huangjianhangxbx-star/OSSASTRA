using System;

public enum DRPGInteractiveInputTransactionState
{
    Reserved = 0,
    Committed = 1,
    Cancelled = 2
}

public sealed class DRPGInteractiveInputReceipt
{
    public DRPGInteractiveInputReceipt(
        int actorRuntimeId,
        float spentPosture,
        DRPGProgressMutationResult progressMutation,
        bool enterPostureBreakAfterImpact,
        bool weaponUseRecorded)
    {
        ActorRuntimeId = actorRuntimeId;
        SpentPosture = Math.Max(0f, spentPosture);
        ProgressMutation = progressMutation;
        EnterPostureBreakAfterImpact = enterPostureBreakAfterImpact;
        WeaponUseRecorded = weaponUseRecorded;
    }

    public int ActorRuntimeId { get; }
    public float SpentPosture { get; }
    public DRPGProgressMutationResult ProgressMutation { get; }
    public bool EnterPostureBreakAfterImpact { get; }
    public bool WeaponUseRecorded { get; }
}

/// <summary>
/// Represents one accepted click/swing inside an interactive action. All
/// mutable dependencies are preflighted before the first mutation so a
/// rejected input never consumes charge, posture, or durability.
/// </summary>
public sealed class DRPGInteractiveInputTransaction
{
    private const float Epsilon = 0.0001f;

    private DRPGInteractiveInputTransaction(
        int actorRuntimeId,
        string ownerStableId,
        DRPGResolvedProgressCost progressCost,
        float postureCost,
        DRPGSelfPostureDepletionPolicy depletionPolicy,
        bool recordsWeaponUse)
    {
        ActorRuntimeId = actorRuntimeId;
        OwnerStableId = ownerStableId ?? string.Empty;
        ProgressCost = progressCost ?? DRPGResolvedProgressCost.None;
        PostureCost = Math.Max(0f, postureCost);
        DepletionPolicy = depletionPolicy;
        RecordsWeaponUse = recordsWeaponUse;
        State = DRPGInteractiveInputTransactionState.Reserved;
    }

    public int ActorRuntimeId { get; }
    public string OwnerStableId { get; }
    public DRPGResolvedProgressCost ProgressCost { get; }
    public float PostureCost { get; }
    public DRPGSelfPostureDepletionPolicy DepletionPolicy { get; }
    public bool RecordsWeaponUse { get; }
    public DRPGInteractiveInputTransactionState State { get; private set; }
    public DRPGInteractiveInputReceipt Receipt { get; private set; }

    public static bool TryPrepare(
        BattleParticipantRuntime actor,
        DRPGResolvedActionSpec spec,
        DRPGBattleProgressPipeline progressPipeline,
        IDRPGBattleWeaponDurabilityService durabilityService,
        DRPGBattleWeaponActionContext weaponContext,
        out DRPGInteractiveInputTransaction transaction,
        out string error)
    {
        transaction = null;
        if (actor == null || spec == null || progressPipeline == null ||
            spec.ActorRuntimeId != actor.RuntimeId)
        {
            error = "Interactive input has an invalid actor, spec, or progress pipeline.";
            return false;
        }

        string owner = actor.Profile != null
            ? actor.Profile.CombatantId
            : string.Empty;
        var candidate = new DRPGInteractiveInputTransaction(
            actor.RuntimeId,
            owner != null ? owner.Trim() : string.Empty,
            spec.InputProgressCost,
            spec.FreeStrikeSpec.PostureCost,
            spec.SelfPostureDepletionPolicy,
            weaponContext != null);
        if (!candidate.CanCommit(
                actor,
                progressPipeline,
                durabilityService,
                weaponContext,
                out error))
        {
            return false;
        }

        transaction = candidate;
        error = string.Empty;
        return true;
    }

    public bool CanCommit(
        BattleParticipantRuntime actor,
        DRPGBattleProgressPipeline progressPipeline,
        IDRPGBattleWeaponDurabilityService durabilityService,
        DRPGBattleWeaponActionContext weaponContext,
        out string error)
    {
        if (State == DRPGInteractiveInputTransactionState.Cancelled)
        {
            error = "The interactive input transaction was cancelled.";
            return false;
        }
        if (State == DRPGInteractiveInputTransactionState.Committed)
        {
            error = string.Empty;
            return true;
        }
        if (actor == null || actor.RuntimeId != ActorRuntimeId || !actor.IsAlive)
        {
            error = "The interactive input actor is no longer valid.";
            return false;
        }
        if (actor.IsPostureBroken)
        {
            error = "A posture-broken actor cannot submit another interactive input.";
            return false;
        }
        if (progressPipeline == null)
        {
            error = "The interactive input progress pipeline is missing.";
            return false;
        }
        if (ProgressCost.IsConfigured)
        {
            if (string.IsNullOrWhiteSpace(OwnerStableId))
            {
                error = "Interactive progress consumption requires a stable owner ID.";
                return false;
            }
            if (!progressPipeline.CanSpend(
                    OwnerStableId,
                    ProgressCost,
                    DRPGProgressScope.Battle,
                    out error))
            {
                return false;
            }
        }
        if (actor.CurrentPosture + Epsilon < PostureCost)
        {
            error = "The actor does not have enough posture for this input.";
            return false;
        }
        if (DepletionPolicy ==
                DRPGSelfPostureDepletionPolicy.RejectInputThatWouldBreak &&
            PostureCost > 0f &&
            actor.CurrentPosture - PostureCost <= Epsilon)
        {
            error = "This input would exhaust posture and is rejected by policy.";
            return false;
        }
        if (RecordsWeaponUse)
        {
            if (durabilityService == null || weaponContext == null)
            {
                error = "Interactive weapon durability context is incomplete.";
                return false;
            }
            DRPGEquipmentOperationResult durability =
                durabilityService.CanRecordCommittedUse(
                    weaponContext,
                    DRPGWeaponUseKind.FreeStrikeInput);
            if (!durability.Success)
            {
                error = durability.Message;
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    public bool TryCommit(
        BattleParticipantRuntime actor,
        DRPGBattleProgressPipeline progressPipeline,
        IDRPGBattleWeaponDurabilityService durabilityService,
        DRPGBattleWeaponActionContext weaponContext,
        out DRPGInteractiveInputReceipt receipt,
        out string error)
    {
        receipt = Receipt;
        if (!CanCommit(
                actor,
                progressPipeline,
                durabilityService,
                weaponContext,
                out error))
        {
            return false;
        }
        if (State == DRPGInteractiveInputTransactionState.Committed)
            return true;

        DRPGProgressMutationResult progressMutation = null;
        if (ProgressCost.IsConfigured && !progressPipeline.TrySpend(
                OwnerStableId,
                ProgressCost.ResourceId,
                DRPGProgressScope.Battle,
                ProgressCost.Amount,
                out progressMutation,
                out error))
        {
            return false;
        }

        bool deferredBreak = false;
        float spentPosture;
        if (DepletionPolicy ==
            DRPGSelfPostureDepletionPolicy.ResolveImpactThenEnterPostureBreak)
        {
            if (!actor.TrySpendPostureDeferredBreak(
                    PostureCost,
                    out spentPosture,
                    out deferredBreak))
            {
                error = "Interactive posture changed after preflight.";
                return false;
            }
        }
        else
        {
            spentPosture = actor.SpendPosture(PostureCost);
        }

        bool weaponUseRecorded = false;
        if (RecordsWeaponUse)
        {
            DRPGEquipmentOperationResult recorded =
                durabilityService.TryRecordCommittedUse(
                    weaponContext,
                    DRPGWeaponUseKind.FreeStrikeInput);
            if (!recorded.Success)
            {
                throw new InvalidOperationException(
                    "Durability commit failed after a successful atomic preflight: " +
                    recorded.Message);
            }
            weaponUseRecorded = true;
        }

        Receipt = new DRPGInteractiveInputReceipt(
            ActorRuntimeId,
            spentPosture,
            progressMutation,
            deferredBreak,
            weaponUseRecorded);
        receipt = Receipt;
        State = DRPGInteractiveInputTransactionState.Committed;
        error = string.Empty;
        return true;
    }

    public bool Cancel()
    {
        if (State != DRPGInteractiveInputTransactionState.Reserved)
            return false;
        State = DRPGInteractiveInputTransactionState.Cancelled;
        return true;
    }
}
