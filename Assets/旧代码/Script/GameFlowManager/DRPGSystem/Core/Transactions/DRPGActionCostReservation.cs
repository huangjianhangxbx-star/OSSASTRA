using System;
using System.Threading;

// One owner for the existing MP/posture and progress reservation lifecycles.
// Staging returns a detached draft; cancellation discards it, never rewinds a
// participant builder that a subsequent counterattack may already have changed.
internal sealed class DRPGActionCostReservation
{
    private static long nextReservationId;
    private readonly object gate = new object();
    private readonly long reservationId = Interlocked.Increment(ref nextReservationId);
    private readonly ActionIdentity identity;
    private readonly string skillId;
    private BattleRuleSnapshot stagedSnapshot;
    private DRPGBattleMutationWorkspace committingWorkspace;

    private DRPGActionCostReservation(ActionIdentity identity, string skillId,
        DRPGActionResourceTransaction resources, DRPGActionProgressTransaction progress)
    {
        this.identity = identity;
        this.skillId = skillId;
        Resources = resources;
        Progress = progress;
        resources.ClaimRootCostOwnership(this);
        progress.ClaimRootCostOwnership(this);
    }

    internal DRPGActionResourceTransaction Resources { get; }
    internal DRPGActionProgressTransaction Progress { get; }
    internal DRPGActionCostReceipt StagedCosts { get; private set; }
    internal DRPGActionResourceTransactionState State { get; private set; } = DRPGActionResourceTransactionState.Reserved;
    internal DRPGActionCostReceipt CommittedCosts => State == DRPGActionResourceTransactionState.Committed ? StagedCosts : null;

    internal static bool TryReserve(ActionIdentity identity, DRPGResolvedActionSpec spec, BattleParticipantRuntime actor,
        DRPGBattleProgressPipeline pipeline, out DRPGActionCostReservation reservation, out string error)
    {
        reservation = null;
        if (!identity.IsValid || spec == null || !Finite(spec.ManaCost) || !Finite(spec.PostureCost) ||
            spec.InputProgressCost.IsConfigured)
        { error = "Root action costs require a valid identity and finite action-only costs."; return false; }
        if (!DRPGActionResourceTransaction.TryReserve(spec, actor, out var resources, out error)) return false;
        if (!DRPGActionProgressTransaction.TryReserve(spec, actor, pipeline, out var progress, out error))
        { resources.Cancel(); return false; }
        reservation = new DRPGActionCostReservation(identity, spec.SkillId, resources, progress);
        return true;
    }

    internal bool TryStageBeforeResolution(DRPGBattleMutationWorkspace input, out DRPGBattleMutationWorkspace staged, out string error)
    {
        lock (gate)
        {
            staged = null;
            error = string.Empty;
            if (!IsReserved || stagedSnapshot != null || input == null || input.ActionCosts != null ||
                input.Snapshot.BattleGeneration != identity.BattleGeneration)
            { error = "Root costs require an unused reservation and its battle workspace."; return false; }
            try
            {
                if (input.BuildValidatedJournal().DirtySet != DRPGBattleMutationDirtySet.None)
                    throw new InvalidOperationException("Action costs must be staged before any rule or resource mutations.");
                var draft = input.Fork();
                var actor = draft.For(new ParticipantId(Resources.ActorRuntimeId));
                if (!StringComparer.Ordinal.Equals(actor.Snapshot.OwnerStableId, Progress.OwnerStableId))
                    throw new InvalidOperationException("The reserved progress owner is not the captured acting participant.");
                if (actor.Mana < Resources.ManaCost || actor.Posture < Resources.PostureCost ||
                    actor.Stamina.Available < Resources.StaminaCost)
                    throw new InvalidOperationException("The captured actor cannot afford the reserved action costs.");
                var resourceReceipt = new DRPGActionResourceReceipt(Resources.ActorRuntimeId,
                    Resources.ManaCost, Resources.PostureCost, Resources.StaminaCost);
                DRPGProgressMutationResult progressReceipt = null;
                if (Progress.Cost.IsConfigured)
                {
                    if (draft.ActionResources == null)
                        throw new InvalidOperationException("Action progress costs require captured real progress bindings.");
                    var request = DRPGProgressMutationRequest.Spend("root-action-cost", "action-cost:" + identity, 0,
                        Progress.OwnerStableId, Progress.Cost.ResourceId, DRPGProgressScope.Battle, Progress.Cost.Amount);
                    if (!draft.ActionResources.TryStageProgress(identity, request, out progressReceipt, out error)) return false;
                }
                ApplyParticipantCosts(draft, resourceReceipt);
                var receipt = new DRPGActionCostReceipt(reservationId, identity, resourceReceipt, progressReceipt);
                stagedSnapshot = input.Snapshot;
                StagedCosts = receipt;
                draft.AttachActionCosts(this);
                staged = draft;
                return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }
    }

    internal bool MatchesAction(in DRPGBasicAttackFrozenSnapshot frozen, DRPGBattleMutationWorkspace workspace, out string error)
    {
        lock (gate)
        {
            error = string.Empty;
            if (frozen.Identity != identity || frozen.SkillId != skillId ||
                !ReferenceEquals(workspace?.ActionCosts, this) || !ReferenceEquals(workspace.Snapshot, stagedSnapshot) ||
                StagedCosts == null || Resources.State == DRPGActionResourceTransactionState.Cancelled ||
                Progress.State == DRPGActionProgressTransactionState.Cancelled ||
                frozen.Turn.Actor.Value != Resources.ActorRuntimeId ||
                frozen.ManaCost != Resources.ManaCost || frozen.PostureCost != Resources.PostureCost ||
                frozen.StaminaCost != Resources.StaminaCost ||
                frozen.ActionProgressCost.ResourceId != Progress.Cost.ResourceId ||
                frozen.ActionProgressCost.Amount != Progress.Cost.Amount || frozen.InputProgressCost.IsConfigured)
            { error = "The root cost reservation does not match this frozen action and staged draft."; return false; }
            // The action cost is the first progress operation. Later reactions may
            // spend/gain again, but cannot replace the cost with an unrelated spend.
            if (Progress.Cost.IsConfigured)
            {
                var journal = workspace.ActionResources?.BuildJournal();
                if (journal == null || journal.ProgressMutations.Count == 0 ||
                    !ReferenceEquals(journal.ProgressMutations[0], StagedCosts.Progress))
                { error = "The action progress cost is missing from its ordered resource journal."; return false; }
            }
            return true;
        }
    }

    internal BattleJournalCommitResult CommitValidatedJournal(DRPGBattleSessionState state,
        BattleRuleMutationLease lease, DRPGBattleMutationWorkspace workspace,
        DRPGBattleActionResolutionLedgerStore ledgerStore = null)
    {
        lock (gate)
        {
            if (ledgerStore == null || !ledgerStore.OwnsCostCommit(this, workspace) ||
                !IsReserved || !ReferenceEquals(workspace?.ActionCosts, this) ||
                !ReferenceEquals(workspace.Snapshot, stagedSnapshot) ||
                !state.OwnsRuleMutationLease(lease, BattleTransactionIdentity.ForRootAction(identity)))
                return BattleJournalCommitResult.Failure(state.BattleRevision, "Root costs are no longer reserved by this draft/lease.");
            committingWorkspace = workspace;
            try
            {
                var result = state.TryCommitCostJournal(lease, workspace, this);
                if (result.Succeeded)
                {
                    // All validation and allocation happened before State apply.
                    State = DRPGActionResourceTransactionState.Committed;
                }
                return result;
            }
            finally { committingWorkspace = null; }
        }
    }

    internal bool IsCommitting(DRPGBattleMutationWorkspace workspace) =>
        Monitor.IsEntered(gate) && ReferenceEquals(committingWorkspace, workspace) && committingWorkspace != null;

    internal bool Cancel()
    {
        lock (gate)
        {
            if (!IsReserved || committingWorkspace != null) return false;
            State = DRPGActionResourceTransactionState.Cancelled;
            return true;
        }
    }

    internal static void ApplyParticipantCosts(DRPGBattleMutationWorkspace workspace, DRPGActionResourceReceipt receipt)
    {
        var actor = workspace.For(new ParticipantId(receipt.ActorRuntimeId));
        if (receipt.SpentMana > 0f) actor.SetMana(actor.Mana - receipt.SpentMana);
        if (receipt.SpentPosture > 0f) actor.SetPosture(actor.Posture - receipt.SpentPosture);
        if (receipt.SpentStamina > 0)
        {
            if (!actor.Stamina.TrySpend(receipt.SpentStamina,
                    out BattleStaminaValue next))
                throw new InvalidOperationException(
                    "The captured actor cannot spend the reserved stamina.");
            actor.SetStamina(next);
        }
    }

    private bool IsReserved => State == DRPGActionResourceTransactionState.Reserved;
    private static bool Finite(float value) => value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
}

// Immutable facts, safe for committed ledgers to retain without a mutable
// reservation alias. Reservation identity participates in full-request dedupe.
internal sealed class DRPGActionCostReceipt
{
    internal DRPGActionCostReceipt(long reservationId, ActionIdentity identity,
        DRPGActionResourceReceipt resources, DRPGProgressMutationResult progress)
    { ReservationId = reservationId; Identity = identity; Resources = resources; Progress = progress; }
    internal long ReservationId { get; }
    internal ActionIdentity Identity { get; }
    internal DRPGActionResourceReceipt Resources { get; }
    internal DRPGProgressMutationResult Progress { get; }
}
