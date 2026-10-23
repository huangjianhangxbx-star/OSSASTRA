using System;
using System.Collections.Generic;

// Dormant production seam for stages 3 and 7. It never takes a turn, starts a
// presentation, releases the root lease, or publishes ActionCompleted.
internal sealed class DRPGStandardBasicAttackRuleKernel
{
    private readonly DRPGBattleSessionState state;
    private readonly DRPGBattleActionResolutionLedgerStore store;
    private readonly DRPGActionResolver resolver;
    private readonly IReadOnlyList<BattleParticipantRuntime> participants;
    private readonly DRPGBattleProgressPipeline progress;
    private readonly IDRPGBattleWeaponDurabilityService weaponService;
    private readonly IDRPGBattleResonanceAccess resonance;
    private readonly DRPGBattleReactionPipeline legacyReactions;
    private readonly Action<DRPGCommittedActionFacts> publish;
    private readonly HashSet<ActionIdentity> preparedIdentities = new HashSet<ActionIdentity>();

    internal DRPGStandardBasicAttackRuleKernel(DRPGBattleSessionState state, DRPGBattleActionResolutionLedgerStore store,
        DRPGActionResolver resolver, IReadOnlyList<BattleParticipantRuntime> participants, DRPGBattleProgressPipeline progress,
        IDRPGBattleWeaponDurabilityService weaponService, IDRPGBattleResonanceAccess resonance,
        DRPGBattleReactionPipeline legacyReactions, Action<DRPGCommittedActionFacts> publish)
    {
        this.state = state; this.store = store; this.resolver = resolver; this.participants = participants;
        this.progress = progress; this.weaponService = weaponService; this.resonance = resonance;
        this.legacyReactions = legacyReactions; this.publish = publish;
    }

    internal bool TryPrepare(in DRPGBasicAttackFrozenSnapshot frozen, DRPGResolvedActionSpec spec,
        BattleRuleMutationLease lease, out DRPGPreparedBasicAttackRuleAction action, out string error,
        IEnumerable<IDRPGImmediateReactionSource> reactions = null)
    {
        action = null; error = string.Empty; DRPGActionCostReservation costs = null;
        try
        {
            if (!frozen.Identity.IsValid || !state.OwnsRuleMutationLease(lease, BattleTransactionIdentity.ForRootAction(frozen.Identity)) ||
                preparedIdentities.Contains(frozen.Identity) || spec == null ||
                (spec.CommandKind != DRPGBattleCommandKind.BasicAttack &&
                 spec.CommandKind != DRPGBattleCommandKind.Skill) ||
                spec.ActorRuntimeId != frozen.Turn.Actor.Value || spec.TargetRuntimeId != frozen.Target.Value ||
                spec.SkillId != frozen.SkillId || spec.ManaCost != frozen.ManaCost ||
                spec.PostureCost != frozen.PostureCost ||
                spec.StaminaCost != frozen.StaminaCost ||
                spec.Effects.Count == 0)
                throw new InvalidOperationException("Rule preparation requires one matching frozen configured action and its unused Root lease.");
            if (legacyReactions.HasLegacySources)
                throw new InvalidOperationException("Legacy reaction sources require an explicit categorized frozen adapter before using the new root.");
            if ((weaponService != null && !(weaponService is DRPGBattleWeaponDurabilityService)) ||
                (resonance != null && !(resonance is DRPGBattleResonanceAccess)))
                throw new InvalidOperationException("Custom weapon or resonance services require an atomic action-resource adapter.");
            BattleParticipantRuntime actor = null, target = null;
            foreach (var participant in participants)
            {
                if (participant.RuntimeId == spec.ActorRuntimeId) actor = participant;
                if (participant.RuntimeId == spec.TargetRuntimeId) target = participant;
            }
            if (actor == null || target == null || !actor.IsAlive || !actor.IsDeployed ||
                !target.IsAlive || !target.IsDeployed || !spec.CanTarget(actor, target))
                throw new InvalidOperationException("The locked configured action has an illegal target.");
            if (weaponService == null && !string.IsNullOrEmpty(actor.Build.Equipment.WeaponInstanceId))
                throw new InvalidOperationException("An equipped actor requires its real weapon service binding.");
            var sources = new DRPGActionRuleSources(progress);
            var workspace = state.CreateWorkspace();
            if (workspace.ActionResources == null) throw new InvalidOperationException("The Session has no atomic action-resource binding.");
            if (!DRPGActionCostReservation.TryReserve(frozen.Identity, spec, actor, progress, out costs, out error) ||
                !costs.TryStageBeforeResolution(workspace, out var staged, out error))
                throw new InvalidOperationException(error);
            action = new DRPGPreparedBasicAttackRuleAction(state, store, resolver, participants, actor, target, frozen,
                spec, lease, costs, staged, sources, weaponService as DRPGBattleWeaponDurabilityService, reactions, publish);
            preparedIdentities.Add(frozen.Identity);
            return true;
        }
        catch (Exception exception) { costs?.Cancel(); error = exception.Message; return false; }
    }
}

internal sealed class DRPGPreparedBasicAttackRuleAction : IDisposable
{
    private readonly DRPGBattleSessionState state;
    private readonly DRPGBattleActionResolutionLedgerStore store;
    private readonly DRPGActionResolver resolver;
    private readonly IReadOnlyList<BattleParticipantRuntime> participants;
    private readonly BattleParticipantRuntime actor, target;
    private readonly DRPGBasicAttackFrozenSnapshot frozen;
    private readonly DRPGResolvedActionSpec spec;
    private readonly BattleRuleMutationLease lease;
    private readonly DRPGActionCostReservation costs;
    private readonly DRPGBattleMutationWorkspace workspace;
    private readonly DRPGActionRuleSources sources;
    private readonly DRPGBattleWeaponDurabilityService weapons;
    private readonly IReadOnlyList<IDRPGImmediateReactionSource> reactions;
    private readonly Action<DRPGCommittedActionFacts> publish;
    private DRPGPrimaryImpactCommitResult result;
    private DRPGBattleImpactContext resolvedContext;
    private DRPGHitOutcome resolvedOutcome;
    private bool attempted, disposed;
    private DRPGBattlePostReturnResult postReturnResult;
    private DRPGBattleDepartureJournal departureDescription;
    private DRPGImmediateReactionLedgerBuilder.Continuation reactionContinuation;
    private DRPGBattleSettlementLedger settlement;
    private DRPGBattleDepartureJournal settlementDepartureDescription;
    private DRPGBattlePostReturnResult finalReconciliation;
    internal DRPGBattlePreparedAction CreateCompletionAction(DRPGBattleCommand command)
    {
        if (command == null ||
            (command.Kind != DRPGBattleCommandKind.BasicAttack &&
             command.Kind != DRPGBattleCommandKind.Skill) ||
            command.ActorRuntimeId != frozen.Turn.Actor.Value || command.TargetRuntimeId != frozen.Target.Value || command.Skill.SkillId != frozen.SkillId)
            throw new ArgumentException("Completion notification must describe the frozen command.", nameof(command));
        return new DRPGBattlePreparedAction(frozen.Identity, command, spec, costs.Resources,
            progressTransaction: costs.Progress, basicAttackSnapshot: frozen);
    }

    internal DRPGPreparedBasicAttackRuleAction(DRPGBattleSessionState state, DRPGBattleActionResolutionLedgerStore store,
        DRPGActionResolver resolver, IReadOnlyList<BattleParticipantRuntime> participants, BattleParticipantRuntime actor,
        BattleParticipantRuntime target, DRPGBasicAttackFrozenSnapshot frozen, DRPGResolvedActionSpec spec,
        BattleRuleMutationLease lease, DRPGActionCostReservation costs, DRPGBattleMutationWorkspace workspace,
        DRPGActionRuleSources sources, DRPGBattleWeaponDurabilityService weapons, IEnumerable<IDRPGImmediateReactionSource> reactions,
        Action<DRPGCommittedActionFacts> publish)
    {
        this.state = state; this.store = store; this.resolver = resolver; this.participants = participants;
        this.actor = actor; this.target = target; this.frozen = frozen; this.spec = spec; this.lease = lease;
        this.costs = costs; this.workspace = workspace; this.sources = sources; this.weapons = weapons;
        var frozenSources = new List<IDRPGImmediateReactionSource>();
        if (reactions != null) foreach (var reaction in reactions) frozenSources.Add(new FrozenSource(reaction));
        this.reactions = Array.AsReadOnly(frozenSources.ToArray());
        this.publish = publish;
    }

    internal DRPGPrimaryImpactCommitResult ResolveAndCommit(DRPGBattleImpactContext context,
        DRPGHitOutcome outcome = DRPGHitOutcome.Hit)
    {
        if (disposed || !state.OwnsRuleMutationLease(lease, BattleTransactionIdentity.ForRootAction(frozen.Identity)))
            return DRPGPrimaryImpactCommitResult.Rejected("Rule action was disposed or lost its Root lease.");
        if (attempted)
            return resolvedContext.Equals(context) && resolvedOutcome == outcome ? result :
                DRPGPrimaryImpactCommitResult.Rejected("A repeated primary callback changed its resolved context.");
        attempted = true; resolvedContext = context; resolvedOutcome = outcome;
        // Install a terminal result before invoking any source/observer, so a
        // re-entrant callback cannot execute this root a second time.
        result = DRPGPrimaryImpactCommitResult.Rejected("The rule action is already resolving.");
        try
        {
            var draft = workspace.Fork();
            string primaryId = "action-" + frozen.Identity.BattleGeneration + "-" + frozen.Identity.TurnSerial + "-" + frozen.Identity.ActionId + "-impact-0";
            if (weapons != null && !weapons.TryStageRootUse(actor, frozen.Identity, primaryId,
                DRPGWeaponUseKind.MeleeCommittedSwing, draft, out var error)) throw new InvalidOperationException(error);
            var primary = resolver.ResolveIntoWorkspace(spec, actor, target, participants, draft, context, outcome);
            var closure = new DRPGImmediateReactionLedgerBuilder().Build(frozen.Identity, primaryId, primary, draft, participants, reactions, resolver);
            if (!closure.Succeeded) throw new InvalidOperationException(closure.Error);
            var continuation = closure.Continuation;
            closure = sources.Stage(frozen.Identity, primary, closure);
            if (!closure.Succeeded) throw new InvalidOperationException(closure.Error);
            result = store.TryCommitActionResolution("locked:" + frozen.Identity, frozen, primary, closure, lease);
            if (result.Status == DRPGPrimaryImpactCommitStatus.Committed)
            {
                reactionContinuation = continuation;
                try { publish?.Invoke(result.Receipt.Ledger.Facts); }
                catch (Exception) { /* Keep the committed result even if a notification adapter fails. */ }
            }
            else costs.Cancel();
        }
        catch (Exception exception) { costs.Cancel(); result = DRPGPrimaryImpactCommitResult.Rejected(exception.Message); }
        return result;
    }

    internal bool TryDescribeDepartures(out DRPGBattleDepartureJournal departures, out string error)
    {
        departures = null; error = string.Empty;
        var owner = BattleTransactionIdentity.ForRootAction(frozen.Identity);
        if (disposed || !state.OwnsRuleMutationLease(lease, owner) || result.Receipt == null)
        { error = "Departure description requires this committed action and its active Root lease."; return false; }
        if (departureDescription == null)
        {
            // Describe before transfer, reconcile rules AFTER transfer. Neither
            // branch derives membership from a live/filtered presentation roster.
            var members = new List<DRPGBattleDepartureMember>();
            foreach (var mutation in result.Receipt.Ledger.MutationJournal.ParticipantMutations)
            {
                if (mutation.HitPoints > 0f || !workspace.Snapshot.TryGetParticipant(mutation.Participant, out var before) ||
                    before.CurrentHp <= 0f) continue;
                members.Add(new DRPGBattleDepartureMember(mutation.Participant, before.Side));
            }
            departureDescription = new DRPGBattleDepartureJournal(owner, result.Receipt.Ledger.Fingerprint, members);
        }
        departures = departureDescription; return true;
    }

    internal bool TryReconcileFormationAfterReturn(out DRPGBattlePostReturnResult reconciled, out string error)
    {
        reconciled = null; error = string.Empty;
        var owner = BattleTransactionIdentity.ForRootAction(frozen.Identity);
        if (disposed || !state.OwnsRuleMutationLease(lease, owner) || result.Receipt == null)
        { error = "Post-return reconciliation requires this committed action and its active Root lease."; return false; }
        if (postReturnResult != null) { reconciled = postReturnResult; return true; }
        if (state.BattleRevision != result.Receipt.CommittedRevision)
        { error = "Post-return reconciliation cannot consume an intervening rule revision."; return false; }
        // A fresh post-impact workspace: never replay the pre-impact draft.
        // Departure membership is derived from the committed journal plus the
        // frozen pre-impact rule snapshot, never from presentation/live roster.
        var draft = state.CreateWorkspace();
        if (!TryDescribeDepartures(out var departures, out error)) return false;
        foreach (var member in departures.Members)
        {
            draft.RemoveFromTimeline(member.Participant);
            if (member.Side == DRPGBattleSide.Enemy) draft.For(member.Participant).SetDeployed(false);
        }
        var formationChanges = DRPGPostReturnFormationDraft.Stage(draft);
        var candidate = new DRPGBattlePostReturnResult(departures, state.BattleRevision + 1,
            formationChanges);
        var commit = state.TryCommitJournal(lease, draft);
        if (!commit.Succeeded) { error = commit.Error; return false; }
        postReturnResult = candidate; reconciled = candidate; return true;
    }

    internal bool TryCommitSettlement(IReadOnlyDictionary<ParticipantId, int> homes,
        out DRPGBattleSettlementLedger ledger, out string error)
    {
        ledger = null; error = string.Empty;
        if (disposed || !state.OwnsRuleMutationLease(lease, BattleTransactionIdentity.ForRootAction(frozen.Identity)) ||
            postReturnResult == null || reactionContinuation == null)
        { error = "Settlement requires the committed Root's reaction continuation and post-return reconciliation."; return false; }
        long expectedRevision = settlement != null ? settlement.CommittedRevision : postReturnResult.CommittedRevision;
        if (state.BattleRevision != expectedRevision)
        { error = "Settlement cannot consume an intervening rule revision."; return false; }
        if (!state.TryCommitSettlement(lease, frozen.Turn.Actor, homes, out ledger, out error, reactionContinuation)) return false;
        settlement = ledger; return true;
    }

    internal bool TryDescribeSettlementDepartures(out DRPGBattleDepartureJournal departures, out string error)
    {
        departures = null; error = string.Empty;
        if (disposed || settlement == null || !state.OwnsRuleMutationLease(lease, BattleTransactionIdentity.ForRootAction(frozen.Identity)))
        { error = "Settlement departures require the committed settlement and matching active Root."; return false; }
        if (settlementDepartureDescription == null) settlementDepartureDescription = DRPGSettlementDepartureFacts.Describe(settlement);
        departures = settlementDepartureDescription; return true;
    }

    internal bool TryReconcileFormationAfterSettlement(DRPGBattleDeparturePresentation departure,
        out DRPGBattlePostReturnResult reconciled, out string error)
    {
        reconciled = null; error = string.Empty;
        if (!TryDescribeSettlementDepartures(out _, out error)) return false;
        if (!state.TryReconcileSettlement(lease, settlement, departure, out reconciled, out error)) return false;
        finalReconciliation = reconciled;
        return true;
    }

    internal bool TryDescribeCompletion(RootActionTerminal terminal, out RootActionCompletion completion, out string error)
    {
        completion = default; error = string.Empty;
        if (disposed || finalReconciliation == null || departureDescription == null || settlementDepartureDescription == null ||
            !state.OwnsRuleMutationLease(lease, BattleTransactionIdentity.ForRootAction(frozen.Identity)) ||
            state.BattleRevision != finalReconciliation.CommittedRevision)
        { error = "Root finalization requires the last reconciliation revision and its original rule lease."; return false; }
        var members = new List<DRPGBattleDepartureMember>(departureDescription.Members);
        var ids = new HashSet<ParticipantId>();
        foreach (var member in members) ids.Add(member.Participant);
        foreach (var member in settlementDepartureDescription.Members)
            if (ids.Add(member.Participant)) members.Add(member);
        ulong source;
        unchecked { source = (departureDescription.SourceFingerprint ^ settlementDepartureDescription.SourceFingerprint) * 1099511628211UL; }
        var departures = new DRPGBattleDepartureJournal(lease.Owner, source == 0 ? 1UL : source, members, DRPGBattleActionStage.Settlement);
        completion = new RootActionCompletion(frozen.Identity, terminal, RootActionCompletion.DetermineOutcome(state.CaptureRuleSnapshot()),
            state.BattleRevision, departures, lease);
        return true;
    }

    public void Dispose() { disposed = true; costs.Cancel(); }

    private sealed class FrozenSource : IDRPGImmediateReactionSource
    {
        private readonly IDRPGImmediateReactionSource source;
        internal FrozenSource(IDRPGImmediateReactionSource source)
        {
            this.source = source ?? throw new ArgumentException("A reaction source is missing.");
            SourceId = source.SourceId; Participant = source.Participant; HomeSlot = source.HomeSlot; IsPassive = source.IsPassive;
            if (string.IsNullOrWhiteSpace(SourceId) || Participant.Value <= 0 || HomeSlot < 0)
                throw new ArgumentException("A reaction source requires its frozen identity and Home Slot.");
        }
        public string SourceId { get; }
        public ParticipantId Participant { get; }
        public int HomeSlot { get; }
        public bool IsPassive { get; }
        public void Collect(DRPGImmediateReactionContext context, ICollection<DRPGImmediateReactionRequest> requests) => source.Collect(context, requests);
    }
}
