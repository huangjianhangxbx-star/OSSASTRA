#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

internal static class DRPGStatusInstanceBoundaryProbe
{
    internal static void RunAll()
    {
        DifferentSourcesRemainIndependentThroughCommit();
        TurnStartDoesNotConsumeAfterActionDuration();
        RoundEndWithoutProviderFailsBuild();
        RootBoundaryPreventsSameActionTick();
        OneInstanceConsumesOnceAtItsMatchingBoundary();
        LegacySnapshotMigrationKeepsValuesAndIdentity();
        RemoveReapplyCannotAliasAnOldInstanceOrEmptySnapshot();
        BoundaryOverflowLeavesDurationAndIdentityUntouched();
        AttachedCollectionCannotConsumeOutsideState();
        FrozenTimingAndPriorityIgnoreAuthoringChanges();
        RemovingAStatusDefinitionClearsAllItsSourcesOnly();
        DRPGActionResourceJournalProbe.StatusIdentitySurvivesImmediateReactionLedger();
        DRPGActionResourceJournalProbe.StatusIdentityForgeryRejectsCommitAndReplay();
    }

    // Regression: statusId-only lookup merges different casters into one instance.
    internal static void DifferentSourcesRemainIndependentThroughCommit()
    {
        using (var test = DRPGBattleStateMutationFixture.Create())
        {
            var definition = ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>();
            var skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
            try
            {
                Set(definition, "statusId", "source-separated-poison");
                Set(definition, "maxStacks", 10);
                Set(definition, "durationTurns", 3);
                Set(definition, "turnStartHpDelta", -2f);
                Set(skill, "effects", new List<DRPGActionEffectDefinition> {
                    DRPGActionEffectDefinition.ApplyStatus(DRPGActionEffectTarget.PrimaryTarget, definition) });
                var draft = test.CreateWorkspace();
                var resolver = new DRPGActionResolver();
                foreach (var actor in new[] { test.Player, test.Player, test.Enemy })
                {
                    var spec = DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(
                        actor.RuntimeId, test.Enemy.RuntimeId, skill), actor);
                    var result = resolver.ResolveIntoWorkspace(spec, actor, test.Enemy,
                        new[] { test.Player, test.Enemy }, draft,
                        DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f));
                    Check(result.Status == DRPGWorkspaceResolutionStatus.Succeeded, result.Error);
                }
                var projected = draft.For(new ParticipantId(test.Enemy.RuntimeId)).CreateStatusProjection().Snapshot();
                Check(projected.Count == 2 && projected[0].Stacks == 2 && projected[1].Stacks == 1,
                    "Different casters were merged by statusId; expected two instances with stacks 2 and 1.");
                Check(test.Enemy.Statuses.Count == 0, "A status projection leaked into live state.");
                Check(test.State.TryCommitJournal(test.Lease, draft).Succeeded, "Source-aware status commit failed.");
                var committed = test.Enemy.Statuses.Snapshot();
                Check(committed.Count == 2 && committed[0].Stacks == 2 && committed[1].Stacks == 1,
                    "Prepared commit lost source-separated instances.");
                var next = test.CreateWorkspace().For(new ParticipantId(test.Enemy.RuntimeId)).CreateStatusProjection().Snapshot();
                Check(DRPGActionStatusChange.SameSnapshots(committed, next), "Snapshot round-trip changed status instances.");
            }
            finally { UnityEngine.Object.DestroyImmediate(skill); UnityEngine.Object.DestroyImmediate(definition); }
        }
    }

    // Regression: legacy TickTurnStart consumes every status regardless of its authored timing.
    internal static void TurnStartDoesNotConsumeAfterActionDuration()
    {
        var definition = ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>();
        try
        {
            Set(definition, "triggerTiming", DRPGStatusTriggerTiming.AfterAction);
            Set(definition, "durationTurns", 3);
            Set(definition, "turnStartHpDelta", -5f);
            var statuses = new DRPGStatusRuntimeCollection(new ParticipantId(41));
            statuses.Apply(DRPGResolvedStatusSpec.From(definition));
            var turnStart = statuses.TickTurnStart();
            Check(turnStart.HpDelta == 0 && !turnStart.Changed && statuses.Snapshot()[0].RemainingTurns == 3,
                "TurnStart consumed a status authored for AfterAction.");
        }
        finally { UnityEngine.Object.DestroyImmediate(definition); }
    }

    internal static void RoundEndWithoutProviderFailsBuild()
    {
        var definition = ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>();
        try
        {
            Set(definition, "triggerTiming", DRPGStatusTriggerTiming.RoundEnd);
            bool rejected = false;
            try { DRPGResolvedStatusSpec.From(definition); }
            catch (InvalidOperationException e) { rejected = e.Message.Contains("Round"); }
            Check(rejected, "RoundEnd compiled without a deterministic round boundary provider.");
        }
        finally { UnityEngine.Object.DestroyImmediate(definition); }
    }

    internal static void RootBoundaryPreventsSameActionTick()
    {
        using (var test = DRPGBattleStateMutationFixture.Create())
        {
            test.Arbiter.Release(test.Lease);
            Check(test.Arbiter.TryAcquire(BattleTransactionIdentity.ForRootAction(new ActionIdentity(test.BattleGeneration, 4, 19)),
                out var lease, out string error), error);
            var definition = ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>();
            try
            {
                Set(definition, "triggerTiming", DRPGStatusTriggerTiming.AfterAction);
                Set(definition, "durationTurns", 3);
                Set(definition, "maxStacks", 5);
                Set(definition, "turnStartHpDelta", -2f);
                Set(definition, "priority", 7);
                var draft = test.CreateWorkspace();
                var target = draft.For(new ParticipantId(test.Enemy.RuntimeId));
                target.ApplyStatus(DRPGResolvedStatusSpec.From(definition), 3, 0, new ParticipantId(test.Player.RuntimeId));
                var projection = target.CreateStatusProjection();
                var instance = projection.Snapshot()[0];
                Check(instance.AppliedBoundarySerial == 4 && instance.EligibleFromBoundarySerial == 5 && instance.Priority == 7,
                    "The status application lost the active Root boundary (turn 4), or used action count 19 as lifecycle time.");
                var actor = new ParticipantId(test.Player.RuntimeId);
                Check(Due(projection, new DRPGStatusBoundary(DRPGStatusTriggerTiming.AfterAction, 4, actor)).Count == 0,
                    "A new ordinary status became due inside the same Root action.");
                var due = Due(projection, new DRPGStatusBoundary(DRPGStatusTriggerTiming.AfterAction, 5, actor));
                Check(due.Count == 1 && due[0].Stacks == 3 && projection.Snapshot()[0].RemainingTurns == 3,
                    "A multi-stack instance split into multiple steps, or querying due steps consumed duration.");
                Check(test.State.TryCommitJournal(lease, draft).Succeeded, "Boundary-aware application commit failed.");
                Check(test.Enemy.Statuses.Snapshot()[0].Same(instance), "Commit changed frozen boundary identity.");
            }
            finally { UnityEngine.Object.DestroyImmediate(definition); test.Arbiter.Release(lease); }
        }
    }

    internal static void OneInstanceConsumesOnceAtItsMatchingBoundary()
    {
        var definition = ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>();
        try
        {
            Set(definition, "triggerTiming", DRPGStatusTriggerTiming.ActorTurnEnd);
            Set(definition, "durationTurns", 2); Set(definition, "maxStacks", 5);
            Set(definition, "turnStartHpDelta", -2f); Set(definition, "turnStartPostureDelta", -1f);
            var owner = new ParticipantId(41);
            var statuses = new DRPGStatusRuntimeCollection(owner);
            var applied = statuses.Apply(DRPGResolvedStatusSpec.From(definition), 3);
            Check(Due(statuses, new DRPGStatusBoundary(DRPGStatusTriggerTiming.ActorTurnEnd, 1, new ParticipantId(42))).Count == 0,
                "Another actor's turn consumed this owner's ActorTurnEnd status.");
            Check(Due(statuses, new DRPGStatusBoundary(DRPGStatusTriggerTiming.AfterAction, 1, owner)).Count == 0,
                "A different timing consumed this status.");
            var boundary = new DRPGStatusBoundary(DRPGStatusTriggerTiming.ActorTurnEnd, 1, owner);
            Check(Consume(statuses, boundary, applied.StatusInstanceId, out var step) && step.HpDelta == -6 &&
                step.PostureDelta == -3 && step.Before.Stacks == 3 && step.After.RemainingTurns == 1 && !step.IsExpired,
                "Three stacks did not produce one aggregate step with one duration decrement.");
            Check(!Consume(statuses, boundary, applied.StatusInstanceId, out _) && statuses.Snapshot()[0].RemainingTurns == 1,
                "The same instance consumed the same lifecycle boundary twice.");
            var clone = DRPGStatusRuntimeCollection.FromSnapshot(statuses.Snapshot());
            Check(Due(clone, boundary).Count == 0 && Consume(clone,
                new DRPGStatusBoundary(DRPGStatusTriggerTiming.ActorTurnEnd, 2, owner), applied.StatusInstanceId, out var final) &&
                final.IsExpired && clone.Count == 0 && statuses.Count == 1,
                "Round-trip lost consumed-boundary state, or a detached expiry mutated the original.");
        }
        finally { UnityEngine.Object.DestroyImmediate(definition); }
    }

    private static bool Consume(DRPGStatusRuntimeCollection statuses, DRPGStatusBoundary boundary, string id,
        out DRPGStatusBoundaryStep step)
    {
        return statuses.TryConsumeBoundaryInstance(boundary, id, out step);
    }

    private static IReadOnlyList<DRPGStatusRuntimeSnapshot> Due(DRPGStatusRuntimeCollection statuses, DRPGStatusBoundary boundary)
    {
        return statuses.GetDueAtBoundary(boundary);
    }

    internal static void LegacySnapshotMigrationKeepsValuesAndIdentity()
    {
        using (var test = DRPGBattleStateMutationFixture.Create())
        {
            var owner = new ParticipantId(test.Player.RuntimeId);
            var legacy = new[] { new DRPGStatusRuntimeSnapshot(test.CreateStatus("legacy", 5, 4), 3, 2) };
            var migrated = DRPGStatusRuntimeCollection.FromSnapshot(legacy, owner);
            var first = migrated.Snapshot()[0];
            Check(first.OwnerParticipantId == owner && first.SourceParticipantId == owner && first.Stacks == 3 &&
                first.RemainingTurns == 2 && first.StableInsertionOrdinal == 1 && first.AppliedBoundarySerial == 0 &&
                first.EligibleFromBoundarySerial == 1 && !string.IsNullOrEmpty(first.StatusInstanceId),
                "Explicit legacy owner migration changed stacks/duration or left instance identity unresolved.");
            var repeatMigration = DRPGStatusRuntimeCollection.FromSnapshot(legacy, owner);
            var roundTrip = DRPGStatusRuntimeCollection.FromSnapshot(migrated.Snapshot());
            Check(DRPGActionStatusChange.SameSnapshots(migrated.Snapshot(), repeatMigration.Snapshot()) &&
                DRPGActionStatusChange.SameSnapshots(migrated.Snapshot(), roundTrip.Snapshot()),
                "Legacy migration or runtime round-trip created unstable instance IDs.");
            var refreshed = roundTrip.Apply(first.Spec, 1, 4);
            Check(refreshed.StatusInstanceId == first.StatusInstanceId && refreshed.Stacks == 4 && refreshed.RemainingTurns == 4,
                "Single-source legacy stack/refresh semantics changed after migration.");
        }
    }

    internal static void RemoveReapplyCannotAliasAnOldInstanceOrEmptySnapshot()
    {
        using (var test = DRPGBattleStateMutationFixture.Create())
        {
            var target = new ParticipantId(test.Enemy.RuntimeId);
            var spec = test.CreateStatus("aba");
            var seed = test.CreateWorkspace(); seed.For(target).ApplyStatus(spec);
            Check(test.State.TryCommitJournal(test.Lease, seed).Succeeded, "ABA seed failed.");
            var first = test.Enemy.Statuses.Snapshot()[0];
            var stale = test.CreateWorkspace(); stale.For(target).SetHitPoints(100);
            using (test.State.OpenCompatibilityMutationScope(test.Lease))
            { test.Enemy.Statuses.Remove("aba"); test.Enemy.Statuses.Apply(spec); }
            Check(first.StatusInstanceId != test.Enemy.Statuses.Snapshot()[0].StatusInstanceId &&
                !test.State.TryCommitJournal(test.Lease, stale).Succeeded && test.Enemy.CurrentHp == 120,
                "Identical statusId/stacks/duration concealed remove-and-reapply from a captured journal.");
            var remove = test.CreateWorkspace(); remove.For(target).RemoveStatus("aba");
            Check(test.State.TryCommitJournal(test.Lease, remove).Succeeded, "ABA removal failed.");
            var empty = test.Enemy.Statuses.Snapshot();
            Check(empty.Count == 0 && empty.NextInsertionOrdinal == 3, "Removing the last status reset its sequence.");
            var clone = DRPGStatusRuntimeCollection.FromSnapshot(empty);
            Check(clone.Apply(spec).StableInsertionOrdinal == 3 && test.Enemy.Statuses.Count == 0,
                "Empty snapshot restore reused an old identity or mutated live state.");
            var next = test.CreateWorkspace(); next.For(target).ApplyStatus(spec);
            Check(test.State.TryCommitJournal(test.Lease, next).Succeeded &&
                test.Enemy.Statuses.Snapshot()[0].StableInsertionOrdinal == 3,
                "Prepared apply after an empty snapshot discarded the high-water mark.");
        }
    }

    internal static void BoundaryOverflowLeavesDurationAndIdentityUntouched()
    {
        var definition = ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>();
        try
        {
            Set(definition, "maxStacks", 3); Set(definition, "durationTurns", 2);
            Set(definition, "turnStartHpDelta", float.MaxValue);
            var owner = new ParticipantId(41);
            var statuses = new DRPGStatusRuntimeCollection(owner);
            var spec = DRPGResolvedStatusSpec.From(definition);
            var applied = statuses.Apply(spec, 2);
            var before = statuses.Snapshot(); bool rejected = false;
            try { Consume(statuses, new DRPGStatusBoundary(DRPGStatusTriggerTiming.TurnStart, 1, owner), applied.StatusInstanceId, out _); }
            catch (InvalidOperationException e) { rejected = e.Message.Contains("finite"); }
            Check(rejected && DRPGActionStatusChange.SameSnapshots(before, statuses.Snapshot()),
                "Overflowing tick partially consumed duration or boundary identity.");
            var empty = new DRPGStatusRuntimeCollection(owner);
            rejected = false;
            try { empty.Apply(spec, appliedBoundarySerial: long.MaxValue); }
            catch (OverflowException) { rejected = true; }
            Check(rejected && empty.Count == 0 && empty.Snapshot().NextInsertionOrdinal == 1,
                "Overflowing eligibility consumed an instance ordinal or added a partial status.");
        }
        finally { UnityEngine.Object.DestroyImmediate(definition); }
    }

    internal static void AttachedCollectionCannotConsumeOutsideState()
    {
        using (var test = DRPGBattleStateMutationFixture.Create())
        {
            var owner = new ParticipantId(test.Enemy.RuntimeId);
            var seed = test.CreateWorkspace(); seed.For(owner).ApplyStatus(test.CreateStatus("owned-status"));
            Check(test.State.TryCommitJournal(test.Lease, seed).Succeeded, "Owned status seed failed.");
            var before = test.Enemy.Statuses.Snapshot(); bool rejected = false;
            try { Consume(test.Enemy.Statuses, new DRPGStatusBoundary(DRPGStatusTriggerTiming.TurnStart, 1, owner), before[0].StatusInstanceId, out _); }
            catch (InvalidOperationException e) { rejected = e.Message.Contains("owning battle state"); }
            Check(rejected && DRPGActionStatusChange.SameSnapshots(before, test.Enemy.Statuses.Snapshot()),
                "A detached-style boundary call bypassed the battle state capability.");
        }
    }

    internal static void FrozenTimingAndPriorityIgnoreAuthoringChanges()
    {
        var definition = ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>();
        try
        {
            Set(definition, "triggerTiming", DRPGStatusTriggerTiming.AfterAction); Set(definition, "priority", 9);
            var statuses = new DRPGStatusRuntimeCollection(new ParticipantId(41));
            statuses.Apply(DRPGResolvedStatusSpec.From(definition));
            Set(definition, "triggerTiming", DRPGStatusTriggerTiming.TurnStart); Set(definition, "priority", -8);
            var turn = statuses.TickTurnStart();
            Check(!turn.Changed && statuses.Snapshot()[0].Priority == 9 &&
                Due(statuses, new DRPGStatusBoundary(DRPGStatusTriggerTiming.AfterAction, 1, new ParticipantId(42))).Count == 1,
                "Authored changes retimed or reordered a frozen live status.");
        }
        finally { UnityEngine.Object.DestroyImmediate(definition); }
    }

    internal static void RemovingAStatusDefinitionClearsAllItsSourcesOnly()
    {
        using (var test = DRPGBattleStateMutationFixture.Create())
        {
            var target = new ParticipantId(test.Enemy.RuntimeId);
            var source = new ParticipantId(test.Player.RuntimeId);
            var shared = test.CreateStatus("shared"); var unrelated = test.CreateStatus("unrelated");
            var seed = test.CreateWorkspace();
            seed.For(target).ApplyStatus(shared, sourceParticipantId: source).ApplyStatus(shared, sourceParticipantId: target).ApplyStatus(unrelated);
            Check(test.State.TryCommitJournal(test.Lease, seed).Succeeded, "Multi-source removal seed failed.");
            var remove = test.CreateWorkspace(); remove.For(target).RemoveStatus("shared");
            var remaining = remove.For(target).CreateStatusProjection().Snapshot();
            Check(remaining.Count == 1 && remaining[0].StatusId == "unrelated",
                "RemoveStatus(statusId) arbitrarily removed only one caster, or removed an unrelated definition.");
            Check(test.State.TryCommitJournal(test.Lease, remove).Succeeded && test.Enemy.Statuses.Count == 1 &&
                test.Enemy.Statuses.Contains("unrelated") && !test.Enemy.Statuses.Contains("shared"),
                "Prepared definition removal diverged from its source-aware projection.");
        }
    }

    private static void Set(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) throw new MissingFieldException(target.GetType().Name, name);
        field.SetValue(target, value);
    }
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}

internal static partial class DRPGActionResourceJournalProbe
{
    internal static void StatusIdentitySurvivesImmediateReactionLedger()
    {
        using (var plan = new PureStatusPlan(PureStatusMode.Followup))
        {
            Check(plan.Closure.Succeeded, plan.Closure.Error);
            var receipt = plan.Commit();
            Check(receipt.Status == DRPGPrimaryImpactCommitStatus.Committed, receipt.Error);
            var events = receipt.Receipt.Ledger.Events;
            var state = plan.Test.Battle.Enemy.Statuses;
            var status = state.Snapshot()[0];
            var owner = new ParticipantId(plan.Test.Battle.Enemy.RuntimeId);
            Check(events.Count == 3 && events[1].Hit == null && events[2].ParentEventId == events[1].EventId &&
                events[1].Identity == events[2].Identity && status.SourceParticipantId.Value == plan.Test.Battle.Player.RuntimeId &&
                status.AppliedBoundarySerial == plan.Test.Identity.TurnSerial &&
                state.GetDueAtBoundary(new DRPGStatusBoundary(DRPGStatusTriggerTiming.TurnStart, plan.Test.Identity.TurnSerial, owner)).Count == 0 &&
                state.GetDueAtBoundary(new DRPGStatusBoundary(DRPGStatusTriggerTiming.TurnStart, plan.Test.Identity.TurnSerial + 1, owner)).Count == 1,
                "An explicit immediate child lost its Root/parent, or caused a new ordinary status to tick at the same boundary.");
        }
    }

    internal static void StatusIdentityForgeryRejectsCommitAndReplay()
    {
        for (int field = 0; field < 6; field++)
        using (var plan = new StatusPlan())
        {
            var entry = plan.Closure.Entries[0];
            var effects = new List<DRPGActionEffectResult>(entry.Batch.Effects);
            var original = effects[0].StatusChange;
            var before = original.After[0];
            var forged = new DRPGStatusRuntimeSnapshot(before.Spec, before.Stacks, before.RemainingTurns,
                field == 0 ? before.StatusInstanceId + "-forged" : before.StatusInstanceId,
                before.OwnerParticipantId,
                field == 1 ? new ParticipantId(plan.Test.Battle.Enemy.RuntimeId) : before.SourceParticipantId,
                field == 2 ? before.StableInsertionOrdinal + 1 : before.StableInsertionOrdinal,
                field == 3 ? before.AppliedBoundarySerial + 1 : before.AppliedBoundarySerial,
                field == 4 ? before.EligibleFromBoundarySerial + 1 : before.EligibleFromBoundarySerial,
                field == 5 ? before.LastConsumedBoundarySerial + 1 : before.LastConsumedBoundarySerial);
            var metadata = (DRPGStatusCollectionSnapshot)original.After;
            var after = new DRPGStatusCollectionSnapshot(metadata.OwnerParticipantId, metadata.NextInsertionOrdinal,
                new[] { forged }, metadata.ObservedBoundarySerial);
            var mutation = new DRPGStatusMutation(original.Mutation.Kind, original.Mutation.Spec, original.Mutation.StatusId,
                original.Mutation.Stacks, original.Mutation.Duration, forged.SourceParticipantId, original.Mutation.AppliedBoundarySerial);
            effects[0] = new DRPGActionEffectResult(entry.SourceParticipant.Value,
                new DRPGActionStatusChange(original.Target, mutation, original.Before, after));
            var entries = new List<DRPGReactionLedgerEntry>(plan.Closure.Entries);
            entries[0] = new DRPGReactionLedgerEntry(entry.Identity, entry.ParentEventId, entry.EventId, entry.ReactionId,
                entry.SourceParticipant, entry.SourceId, entry.Category, entry.Depth, entry.InsertionOrdinal, entry.State,
                new DRPGResolvedActionBatch(entry.Batch.Hits, effects));
            var changed = new DRPGImmediateReactionClosureResult(plan.Closure.Workspace.Fork(), entries, null);
            Check(plan.Store.TryCommitActionResolution("status-root", plan.Frozen, plan.Primary, changed, plan.Test.Lease).Status ==
                DRPGPrimaryImpactCommitStatus.Rejected && plan.Test.Battle.Enemy.Statuses.Count == 0 && plan.Test.Battle.State.BattleRevision == 0,
                "Forged status identity partially committed: field " + field);
            Check(plan.Commit().Status == DRPGPrimaryImpactCommitStatus.Committed, "Rejected identity poisoned valid status commit.");
            Check(plan.Store.TryCommitActionResolution("status-root", plan.Frozen, plan.Primary, changed, plan.Test.Lease).Status ==
                DRPGPrimaryImpactCommitStatus.FingerprintConflict, "Identity field omitted from the replay fingerprint: " + field);
        }
    }
}
#endif
