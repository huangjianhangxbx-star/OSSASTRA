using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

internal sealed partial class DRPGBattleActionResolutionLedgerStore
{
    private DRPGBattleMutationWorkspace costCommitWorkspace;
    private DRPGActionCostReservation costCommitReservation;

    internal bool OwnsCostCommit(DRPGActionCostReservation costs, DRPGBattleMutationWorkspace workspace) =>
        Monitor.IsEntered(commitGate) && costs != null && ReferenceEquals(costCommitReservation, costs) &&
        workspace != null && ReferenceEquals(costCommitWorkspace, workspace);

    internal DRPGPrimaryImpactCommitResult TryCommitActionResolution(string parentEventId,
        in DRPGBasicAttackFrozenSnapshot frozenAction, DRPGWorkspaceResolutionResult primary,
        DRPGImmediateReactionClosureResult closure, BattleRuleMutationLease lease)
    {
        var identity = frozenAction.Identity;
        if (!identity.IsValid || identity.BattleGeneration != state.BattleGeneration || string.IsNullOrWhiteSpace(parentEventId))
            return DRPGPrimaryImpactCommitResult.Rejected("A complete ledger requires its battle/action and locked parent identity.");
        var owner = BattleTransactionIdentity.ForRootAction(identity);
        if (lease == null || !state.OwnsRuleMutationLease(lease, owner) || closure == null || !closure.Succeeded ||
            closure.Workspace.Snapshot.BattleGeneration != identity.BattleGeneration)
            return DRPGPrimaryImpactCommitResult.Rejected("A complete ledger requires its Root Action lease and successful closure.");
        if (primary.Status != DRPGWorkspaceResolutionStatus.Succeeded || primary.Batch == null ||
            primary.Batch.Hits.Count != 1 || primary.Batch.Hits[0] == null)
            return DRPGPrimaryImpactCommitResult.Rejected("A complete ledger requires one successful primary outcome.");
        var hit = primary.Batch.Hits[0];
        var costs = closure.Workspace.ActionCosts;
        if (!ValidateFrozenRequest(identity, frozenAction, hit, primary.Batch.Effects, costs?.Resources, out string error))
            return DRPGPrimaryImpactCommitResult.Rejected(error);
        if (costs != null)
        {
            if (!costs.MatchesAction(frozenAction, closure.Workspace, out error))
                return DRPGPrimaryImpactCommitResult.Rejected(error);
        }
        else if (frozenAction.ActionProgressCost.IsConfigured || frozenAction.InputProgressCost.IsConfigured)
            return DRPGPrimaryImpactCommitResult.Rejected("Frozen progress costs require a matching root action reservation.");
        var key = new DRPGPrimaryImpactKey(identity, 0);
        string eventId = string.Format(CultureInfo.InvariantCulture, "action-{0}-{1}-{2}-impact-0",
            identity.BattleGeneration, identity.TurnSerial, identity.ActionId);
        lock (commitGate)
        {
            DRPGBattleMutationJournal journal;
            ulong fingerprint;
            try
            {
                journal = closure.Workspace.BuildValidatedJournal();
                fingerprint = DRPGActionResolutionFingerprint.ComputeComplete(key, parentEventId, frozenAction,
                    hit, primary.Batch.Effects, closure.Entries, journal);
            }
            catch (Exception exception) { return DRPGPrimaryImpactCommitResult.Rejected(exception.Message); }

            // A full request is replayed with its original detached journal. Empty
            // workspaces are not a wildcard for dropping reactions or resource costs.
            if (committed.TryGetValue(key, out var existing))
                return existing.RequestFingerprint == fingerprint
                    ? DRPGPrimaryImpactCommitResult.AlreadyCommitted(existing)
                    : TraceFingerprintConflict(owner, 0, eventId, parentEventId, fingerprint);

            DRPGBattleActionResolutionLedger ledger;
            DRPGPrimaryImpactCommitReceipt receipt;
            try
            {
                if (hit.Outcome != DRPGHitOutcome.Evaded && !Near(hit.DamageBreakdown.BaseMagnitude,
                    Math.Max(0f, frozenAction.Effects[0].ResolvedMagnitude)))
                    throw new InvalidOperationException("Primary magnitude differs from the frozen action.");
                ValidateCompleteJournal(identity, eventId, primary.Batch, closure.Entries, journal, closure.Workspace.Snapshot);
                ulong primaryFingerprint = DRPGActionResolutionFingerprint.ComputeRequest(key, parentEventId,
                    frozenAction, hit, primary.Batch.Effects, costs?.Resources);
                var entry = new DRPGPrimaryImpactEntry(identity, 0, eventId, parentEventId, hit.Outcome, hit,
                    primary.Batch.Effects, primaryFingerprint);
                ledger = new DRPGBattleActionResolutionLedger(identity, frozenAction, entry, closure.Entries, journal, fingerprint);
                receipt = new DRPGPrimaryImpactCommitReceipt(ledger, checked(journal.BaseRevision + 1), fingerprint);
                // Allocate the cache slot before State applies any primitive values.
                committed.EnsureCapacity(committed.Count + 1);
            }
            catch (Exception exception) { return DRPGPrimaryImpactCommitResult.Rejected(exception.Message); }

            BattleJournalCommitResult commit;
            costCommitWorkspace = closure.Workspace;
            costCommitReservation = costs;
            try
            {
                commit = costs != null ? costs.CommitValidatedJournal(state, lease, closure.Workspace, this) :
                    state.TryCommitJournal(lease, closure.Workspace);
            }
            finally { costCommitWorkspace = null; costCommitReservation = null; }
            if (!commit.Succeeded) return DRPGPrimaryImpactCommitResult.Rejected(commit.Error);
            committed.Add(key, receipt);
            closure.Workspace.Snapshot.TryGetParticipant(new ParticipantId(hit.TargetRuntimeId), out var targetBefore);
            trace.Record(0d, 0d, ActionTraceIdentity.ForTransaction(owner), ActionTraceEventKind.ImpactCommitted,
                DRPGBattleActionStage.ImpactAndReactions, 0, eventId, parentEventId,
                new ActionTraceContext(actor: new ParticipantId(hit.ActorRuntimeId), target: new ParticipantId(hit.TargetRuntimeId),
                    source: new ParticipantId(hit.ActorRuntimeId), fingerprint: fingerprint.ToString("X16", CultureInfo.InvariantCulture),
                    hitOutcome: ToTraceOutcome(hit.Outcome),
                    beforeProjection: new ActionTraceProjection(ToTraceValue(hit.HpBefore), ToTraceValue(hit.PostureBefore),
                        hit.HpBefore > 0f, targetBefore.IsDeployed),
                    afterProjection: new ActionTraceProjection(ToTraceValue(hit.HpAfter), ToTraceValue(hit.PostureAfter),
                        hit.HpAfter > 0f, targetBefore.IsDeployed),
                    leaseId: lease.LeaseSerial.ToString(CultureInfo.InvariantCulture), detail: "Complete action ledger committed."));
            return DRPGPrimaryImpactCommitResult.Committed(receipt);
        }
    }

    private static void ValidateCompleteJournal(ActionIdentity identity, string primaryEventId,
        DRPGResolvedActionBatch primary, IReadOnlyList<DRPGReactionLedgerEntry> reactions,
        DRPGBattleMutationJournal journal, BattleRuleSnapshot snapshot)
    {
        // Steps contain at most one hit interleaved with explicit non-hit facts.
        // Ineffective effects keep their diagnostics without manufacturing hits.
        // Formation/deployment belongs to later barriers. Timeline deltas must
        // be accounted by typed facts; untyped resources remain unsupported.
        if (journal.ResourceMutations.Count != 0)
            throw new InvalidOperationException("The action ledger cannot account for untyped resource writes.");
        var expected = new DRPGBattleMutationWorkspace(snapshot.Revision, snapshot);
        if (journal.ActionCosts != null)
            DRPGActionCostReservation.ApplyParticipantCosts(expected, journal.ActionCosts.Resources);
        ApplyExpectedHit(primary, expected);
        var parents = new Dictionary<string, (int Depth, DRPGImmediateReactionCategory? Category)>(StringComparer.Ordinal)
        { { primaryEventId, (0, null) } };
        var eventIds = new HashSet<string>(StringComparer.Ordinal) { primaryEventId };
        int resolvedCount = 0;
        foreach (var reaction in reactions)
        {
            if (reaction == null || reaction.Identity != identity || string.IsNullOrWhiteSpace(reaction.EventId) ||
                string.IsNullOrWhiteSpace(reaction.ParentEventId) || !eventIds.Add(reaction.EventId) ||
                !parents.TryGetValue(reaction.ParentEventId, out var parent) || reaction.Depth != parent.Depth + 1 ||
                reaction.InsertionOrdinal <= 0 || string.IsNullOrWhiteSpace(reaction.SourceId) ||
                string.IsNullOrWhiteSpace(reaction.ReactionId) || !snapshot.TryGetParticipant(reaction.SourceParticipant, out _) ||
                !Enum.IsDefined(typeof(DRPGImmediateReactionState), reaction.State) ||
                !Enum.IsDefined(typeof(DRPGImmediateReactionCategory), reaction.Category))
                throw new InvalidOperationException("Reaction identities, causal order or source metadata are invalid.");
            if (reaction.State != DRPGImmediateReactionState.Resolved)
            {
                if (reaction.Batch != null)
                    throw new InvalidOperationException("Skipped reactions cannot carry mutations or hit outcomes.");
                continue;
            }
            if (++resolvedCount > 32 || reaction.Depth > 8 ||
                (parent.Category == DRPGImmediateReactionCategory.Counterattack && reaction.Category == parent.Category) ||
                (parent.Category == DRPGImmediateReactionCategory.Reflected && reaction.Category == parent.Category) ||
                reaction.Batch == null || !snapshot.TryGetParticipant(reaction.TargetParticipant, out _) || reaction.Batch.Hits.Count > 1)
                throw new InvalidOperationException("A resolved reaction violates its source, type matrix or expansion budget.");
            if (reaction.Batch.Hits.Count == 1)
            {
                if (reaction.Batch.Hits[0] == null || reaction.Batch.Hits[0].ActorRuntimeId != reaction.SourceParticipant.Value ||
                    reaction.Batch.Hits[0].TargetRuntimeId != reaction.TargetParticipant.Value)
                    throw new InvalidOperationException("Reaction hit belongs to a different actor or target.");
                ApplyExpectedHit(reaction.Batch, expected);
            }
            else
            {
                if (reaction.Batch.Effects.Count == 0)
                    throw new InvalidOperationException("A non-hit reaction requires explicit effects.");
                foreach (var effect in reaction.Batch.Effects)
                {
                    if (effect == null || effect.ActorRuntimeId != reaction.SourceParticipant.Value ||
                        (effect.Kind != DRPGActionEffectKind.ApplyStatus && effect.Kind != DRPGActionEffectKind.TimelineShift && effect.Kind != DRPGActionEffectKind.PostureDamage &&
                         !DRPGActionVitalChange.Supports(effect.Kind)))
                        throw new InvalidOperationException("A non-hit reaction has an unsupported or foreign effect.");
                    if (effect.Kind == DRPGActionEffectKind.TimelineShift) ApplyExpectedTimeline(effect, expected);
                    else if (DRPGActionVitalChange.Supports(effect.Kind)) ApplyExpectedVital(effect, expected);
                    else if (effect.Kind == DRPGActionEffectKind.PostureDamage) ApplyExpectedPosture(effect, expected);
                    else ApplyExpectedStatus(effect, expected);
                }
            }
            // Only an actual outcome can seed the next reaction collection.
            if (reaction.RequiresPresentation)
                parents.Add(reaction.EventId, (reaction.Depth, reaction.Category));
        }
        var expectedJournal = expected.BuildValidatedJournal();
        ValidateExpectedTimeline(expectedJournal.TimelineMutations, journal.TimelineMutations);
        if (expectedJournal.ParticipantMutations.Count != journal.ParticipantMutations.Count)
            throw new InvalidOperationException("The journal contains missing or unaccounted participant mutations.");
        foreach (var actual in journal.ParticipantMutations)
        {
            if (!TryFind(expectedJournal.ParticipantMutations, actual.Participant.Value, out var wanted) ||
                actual.DirtySet != wanted.DirtySet || !DRPGActionStatusChange.SameMutations(actual.Statuses, wanted.Statuses) ||
                !Near(actual.HitPoints, wanted.HitPoints) || !Near(actual.Mana, wanted.Mana) ||
                !Near(actual.Posture, wanted.Posture) || !Near(actual.Shield, wanted.Shield) ||
                actual.Stamina != wanted.Stamina ||
                !actual.PostureState.Same(wanted.PostureState) || actual.IsDefending != wanted.IsDefending ||
                actual.FormationRow != wanted.FormationRow || actual.IsDeployed != wanted.IsDeployed)
                throw new InvalidOperationException(
                    "Participant journal does not equal the ordered action outcomes. " +
                    "actual=" + Describe(actual) + "; expected=" + Describe(wanted));
        }
    }

    private static string Describe(DRPGParticipantMutationRecord value)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "id={0},dirty={1},hp={2},mp={3},posture={4},shield={5},stamina={6}/{7}+{8},state={9},defend={10},row={11},deployed={12},statuses={13}",
            value.Participant.Value,
            value.DirtySet,
            value.HitPoints,
            value.Mana,
            value.Posture,
            value.Shield,
            value.Stamina.Current,
            value.Stamina.Capacity,
            value.Stamina.Pending,
            value.PostureState.State,
            value.IsDefending,
            value.FormationRow,
            value.IsDeployed,
            value.Statuses != null ? value.Statuses.Count : 0);
    }

    private static void ApplyExpectedHit(DRPGResolvedActionBatch batch, DRPGBattleMutationWorkspace expected)
    {
        if (batch == null || batch.Hits.Count != 1 || batch.Hits[0] == null)
            throw new InvalidOperationException("An action step requires one hit outcome.");
        var hit = batch.Hits[0];
        if (!expected.Snapshot.TryGetParticipant(new ParticipantId(hit.ActorRuntimeId), out _) ||
            !expected.Snapshot.TryGetParticipant(new ParticipantId(hit.TargetRuntimeId), out _))
            throw new InvalidOperationException("Hit participants are outside the captured battlefield.");
        var target = expected.For(new ParticipantId(hit.TargetRuntimeId));
        if (!Enum.IsDefined(typeof(DRPGHitOutcome), hit.Outcome) ||
            hit.AppliedDamage < 0f || hit.AppliedPostureDamage < 0f || hit.ShieldAbsorbed < 0f ||
            !Near(hit.HpBefore - hit.HpAfter, hit.AppliedDamage) ||
            !Near(hit.PostureBefore - hit.PostureAfter, hit.AppliedPostureDamage))
            throw new InvalidOperationException("Hit outcome before/after values break the action's causal chain.");
        if (hit.Outcome == DRPGHitOutcome.Evaded)
        {
            if (!Near(hit.HpBefore, target.HitPoints) || !Near(hit.PostureBefore, target.Posture) ||
                !Near(hit.AppliedDamage, 0f) || !Near(hit.AppliedPostureDamage, 0f) ||
                !Near(hit.ShieldAbsorbed, 0f) || hit.WasDefeated || hit.TriggeredDamageBroken ||
                hit.DamageBreakdown != null || batch.Effects.Count != 0)
                throw new InvalidOperationException("An evade cannot carry a damage result or mutation.");
            return;
        }
        if (hit.WasDefeated != (hit.HpAfter <= 0f) || hit.DamageBreakdown == null ||
            !Near(hit.DamageBreakdown.HpDamage, hit.AppliedDamage) ||
            !Near(hit.DamageBreakdown.ShieldAbsorbed, hit.ShieldAbsorbed))
            throw new InvalidOperationException("Damage, defeat or shield facts do not match their hit outcome.");
        int damageCount = 0, postureCount = 0;
        foreach (var effect in batch.Effects)
        {
            if (effect == null || effect.ActorRuntimeId != hit.ActorRuntimeId)
                throw new InvalidOperationException("Effect identity differs from its hit outcome.");
            if (effect.Kind == DRPGActionEffectKind.ApplyStatus)
            {
                ApplyExpectedStatus(effect, expected);
                continue;
            }
            if (effect.Kind == DRPGActionEffectKind.TimelineShift)
            {
                ApplyExpectedTimeline(effect, expected);
                continue;
            }
            if (DRPGActionVitalChange.Supports(effect.Kind))
            {
                ApplyExpectedVital(effect, expected);
                continue;
            }
            if (effect.Kind == DRPGActionEffectKind.PostureDamage && (effect.PostureChange == null || !effect.PostureChange.IsImpact))
            {
                ApplyExpectedPosture(effect, expected);
                continue;
            }
            if (effect.State != DRPGActionEffectResultState.Applied || effect.TargetRuntimeId != hit.TargetRuntimeId ||
                effect.StatusChange != null || effect.TimelineChange != null || effect.VitalChange != null)
                throw new InvalidOperationException("Damage effect identity differs from its hit outcome.");
            if (effect.Kind == DRPGActionEffectKind.Damage)
            {
                damageCount++;
                if (!Near(hit.HpBefore, target.HitPoints) || !Near(hit.PostureBefore, target.Posture) ||
                    target.Shield < hit.ShieldAbsorbed || !Near(effect.AppliedAmount, hit.AppliedDamage) || !Near(effect.AuxiliaryAmount, hit.ShieldAbsorbed) ||
                    // Battle V2 exposure is the absence of the posture health
                    // layer; the legacy timed break state is no longer entered.
                    effect.PostureChange != null || hit.WasPostureBreakExposed != (target.Posture <= 0f) ||
                    !Near(hit.DamageBreakdown.DefenseMultiplier, target.IncomingDefenseMultiplier) ||
                    !Near(effect.RequestedAmount, hit.DamageBreakdown.RequestedDamage) || effect.SemanticId != hit.SkillId ||
                    effect.DamageTags != hit.DamageBreakdown.Tags ||
                    !Near(hit.DamageBreakdown.StatusMultiplier, target.CreateStatusProjection().ResolveActionModifiers().IncomingDamageMultiplier))
                    throw new InvalidOperationException(string.Format(
                        CultureInfo.InvariantCulture,
                        "Damage effect differs from its hit outcome. targetHp={0},hitBefore={1},targetPosture={2},hitPostureBefore={3},targetShield={4},absorbed={5},effectApplied={6},hitApplied={7},effectAux={8},exposedHit={9},exposedState={10},defenseHit={11},defenseState={12},requestedEffect={13},requestedHit={14},semanticEffect={15},semanticHit={16},tagsEffect={17},tagsHit={18},statusHit={19},statusState={20}",
                        target.HitPoints, hit.HpBefore, target.Posture,
                        hit.PostureBefore, target.Shield, hit.ShieldAbsorbed,
                        effect.AppliedAmount, hit.AppliedDamage,
                        effect.AuxiliaryAmount, hit.WasPostureBreakExposed,
                        target.Posture <= 0f,
                        hit.DamageBreakdown.DefenseMultiplier,
                        target.IncomingDefenseMultiplier,
                        effect.RequestedAmount,
                        hit.DamageBreakdown.RequestedDamage,
                        effect.SemanticId, hit.SkillId,
                        effect.DamageTags, hit.DamageBreakdown.Tags,
                        hit.DamageBreakdown.StatusMultiplier,
                        target.CreateStatusProjection().ResolveActionModifiers().IncomingDamageMultiplier));
                target.SetHitPoints(hit.HpAfter);
                if (hit.ShieldAbsorbed > 0f)
                    target.SetShield(target.Shield - hit.ShieldAbsorbed);
            }
            else if (effect.Kind == DRPGActionEffectKind.PostureDamage)
            {
                postureCount++;
                if (damageCount != 1 || !Near(target.Posture, hit.PostureBefore) ||
                    !Near(effect.AppliedAmount, hit.AppliedPostureDamage) || !Near(effect.PostureBefore, hit.PostureBefore) ||
                    !Near(effect.PostureAfter, hit.PostureAfter))
                    throw new InvalidOperationException("Posture effect differs from its hit outcome.");
                ApplyExpectedPosture(effect, expected, true);
            }
            else throw new InvalidOperationException("Unsupported non-damage effect in this action ledger.");
        }
        if (damageCount != 1 || postureCount > 1 || (postureCount == 0 && !Near(hit.AppliedPostureDamage, 0f)))
            throw new InvalidOperationException("A damage step requires one damage effect and at most one posture effect.");
    }

    private static void ApplyExpectedStatus(DRPGActionEffectResult effect, DRPGBattleMutationWorkspace expected)
    {
        if (effect.TimelineChange != null || effect.VitalChange != null)
            throw new InvalidOperationException("A status fact cannot carry another kind of mutation.");
        var affected = expected.For(new ParticipantId(effect.TargetRuntimeId));
        if (effect.State == DRPGActionEffectResultState.Skipped)
        {
            if (effect.StatusChange != null || (affected.HitPoints > 0f && affected.IsDeployed) ||
                !Near(effect.RequestedAmount, 0f) || !Near(effect.AppliedAmount, 0f) || !Near(effect.AuxiliaryAmount, 0f))
                throw new InvalidOperationException("Skipped status effect is not an empty ineligible-target result.");
        }
        else
        {
            var change = effect.StatusChange;
            if (effect.State != DRPGActionEffectResultState.Applied || change == null ||
                change.Target.Value != effect.TargetRuntimeId || change.Mutation.StatusId != effect.SemanticId ||
                change.Mutation.SourceParticipantId.Value != effect.ActorRuntimeId ||
                !Near(effect.RequestedAmount, change.Mutation.Stacks) || !Near(effect.AppliedAmount, change.AppliedStatus.Stacks) ||
                !Near(effect.AuxiliaryAmount, change.AppliedStatus.RemainingTurns))
                throw new InvalidOperationException("Applied status effect is missing its exact immutable change.");
            change.Replay(affected);
        }
    }
}
