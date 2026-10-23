#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

internal static partial class DRPGActionResourceJournalProbe
{
    internal static void RunTimelineLedgerChecks()
    {
        TimelineReactionsCommitInOrderWithoutAdvancingTurn();
        TimelineAndDamageCanShareOneStep();
        TimelineThenLethalKeepsCandidateUntilReconciliation();
        TimelineCannotReinsertTheCurrentActor();
        TimelineLaterFailureDiscardsTheWholeAction();
        TimelinePriorityOverflowRejectsTheWholeAction();
        TimelineNearPastTimeCannotPartiallyApplyState();
        TimelineFactsRejectForgeryAndUnaccountedWrites();
        TimelineResourcePrepareFailureLeavesQueueUntouched();
        TimelineNoChangeRemainsRuleOnly();
        TimelinePriorityFingerprintPreservesIntegerPrecision();
    }

    internal static void TimelineReactionsCommitInOrderWithoutAdvancingTurn()
    {
        using (var plan = new TimelinePlan())
        {
            Check(plan.Closure.Succeeded, "Timeline closure failed: " + plan.Closure.Error);
            Check(plan.Schedule.ScheduledTime == 20f && plan.Test.Battle.Enemy.CurrentHp == 120f,
                "Building the timeline closure mutated live time or HP.");
            var first = plan.Closure.Entries[0].Batch.Effects[0];
            var second = plan.Closure.Entries[1].Batch.Effects[0];
            Check(first.TimelineBefore == 20f && first.TimelineAfter == 25f && second.TimelineBefore == 25f &&
                second.TimelineAfter == 10f && plan.Closure.Workspace.BuildValidatedJournal().TimelineMutations.Count == 1,
                "Timeline descendants read the captured value instead of the previous step, or ignored logical-time clamping.");
            var result = plan.Commit();
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && plan.Schedule.ScheduledTime == 10f &&
                plan.Schedule.Priority == 1 && plan.Test.Battle.Enemy.CurrentHp == 120f &&
                plan.Test.Battle.Enemy.CurrentPosture == 60f && plan.Test.ProgressValue == 7f &&
                plan.Test.Weapon.CurrentDurability == 99 && plan.Test.Battle.State.BattleRevision == 1 &&
                plan.Test.Battle.Scheduler.CurrentTime == 10f && plan.Test.Battle.Scheduler.Count == 1 &&
                plan.Test.Lease.Owner == BattleTransactionIdentity.ForRootAction(plan.Test.Identity),
                "Timeline, primary damage and resources did not commit together under the current owner: " + result.Error);
            var fact = result.Receipt.Ledger.Events[1];
            Check(fact.Hit == null && fact.Effects[0].Kind == DRPGActionEffectKind.TimelineShift &&
                fact.Target.Value == plan.Test.Battle.Enemy.RuntimeId && fact.RequiresPresentation &&
                result.Receipt.Ledger.Events[2].ParentEventId == fact.EventId &&
                ReferenceEquals(result.Receipt, plan.Commit().Receipt) && plan.Schedule.Priority == 1 &&
                plan.Test.Battle.State.BattleRevision == 1, "Timeline event identity or exactly-once replay failed.");
        }
    }

    internal static void TimelineAndDamageCanShareOneStep()
    {
        using (var plan = new TimelinePlan(TimelineMode.Mixed))
        {
            Check(plan.Closure.Succeeded, "Mixed timeline closure failed: " + plan.Closure.Error);
            var result = plan.Commit();
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && plan.Test.Battle.Enemy.CurrentHp == 120f &&
                plan.Test.Battle.Enemy.CurrentPosture == 55f &&
                plan.Schedule.ScheduledTime == 25f && plan.Schedule.Priority == 2 &&
                result.Receipt.Ledger.Events[1].Hit.DamageBreakdown.RequestedDamage == 5f &&
                result.Receipt.Ledger.Events[1].Effects[0].Kind == DRPGActionEffectKind.TimelineShift &&
                result.Receipt.Ledger.Events[1].Effects[1].Kind == DRPGActionEffectKind.Damage,
                "A timeline effect attached to damage lost its ordered facts: " + result.Error);
        }
    }

    internal static void TimelineThenLethalKeepsCandidateUntilReconciliation()
    {
        using (var plan = new TimelinePlan(TimelineMode.Lethal))
        {
            Check(plan.Closure.Succeeded, "Timeline/lethal closure failed: " + plan.Closure.Error);
            var result = plan.Commit();
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && plan.Test.Battle.Enemy.CurrentHp == 0f &&
                plan.Test.Battle.Enemy.IsDeployed && plan.Schedule.ScheduledTime == 25f && plan.Schedule.Priority == 2 &&
                plan.Test.Battle.Scheduler.Count == 1 && plan.Test.Battle.Scheduler.CurrentTime == 10f &&
                plan.Test.Battle.State.BattleRevision == 1,
                "A later lethal hit rejected the earlier timeline fact or removed its candidate before the barrier: " + result.Error);
        }
    }

    internal static void TimelineCannotReinsertTheCurrentActor()
    {
        using (var plan = new TimelinePlan(TimelineMode.Unscheduled))
        {
            Check(plan.Closure.Succeeded, "Unscheduled timeline closure failed: " + plan.Closure.Error);
            var result = plan.Commit();
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && plan.Closure.Entries.Count == 1 &&
                result.Receipt.Ledger.Events[1].Disposition == DRPGLedgerDisposition.RuleOnly &&
                result.Receipt.Ledger.Events[1].Effects.Count == 1 &&
                result.Receipt.Ledger.Events[1].Effects[0].State == DRPGActionEffectResultState.Skipped &&
                plan.Test.Battle.Scheduler.Count == 1 && plan.Schedule.Participant.RuntimeId == plan.Test.Battle.Enemy.RuntimeId &&
                plan.Schedule.ScheduledTime == 20f && plan.Test.Battle.Enemy.CurrentHp == 120f &&
                plan.Test.Battle.Enemy.CurrentPosture == 60f,
                "A timeline shift invented a scheduled actor/extra beat instead of preserving the absent candidate: " + result.Error);
        }
    }

    internal static void TimelineLaterFailureDiscardsTheWholeAction()
    {
        using (var plan = new TimelinePlan(TimelineMode.Fault))
        {
            Check(!plan.Closure.Succeeded && plan.Closure.Error.Contains("Injected post-timeline failure") &&
                plan.Commit().Status == DRPGPrimaryImpactCommitStatus.Rejected && plan.Schedule.ScheduledTime == 20f &&
                plan.Schedule.Priority == 0 && plan.Test.Battle.Enemy.CurrentHp == 120f && plan.Test.ProgressValue == 10f &&
                plan.Test.Weapon.CurrentDurability == 100 && plan.Test.Battle.State.BattleRevision == 0,
                "Timeline was not reached or a later source failure partially applied the action: " + plan.Closure.Error);
        }
    }

    internal static void TimelinePriorityOverflowRejectsTheWholeAction()
    {
        using (var plan = new TimelinePlan(TimelineMode.Overflow))
        {
            Check(!plan.Closure.Succeeded && plan.Closure.Error.Contains("overflow") &&
                plan.Commit().Status == DRPGPrimaryImpactCommitStatus.Rejected && plan.Schedule.Priority == 0 &&
                plan.Schedule.ScheduledTime == 20f && plan.Test.Battle.Enemy.CurrentHp == 120f &&
                plan.Test.ProgressValue == 10f && plan.Test.Weapon.CurrentDurability == 100 && plan.Test.Battle.State.BattleRevision == 0,
                "Timeline priority overflow wrapped or was not reached: " + plan.Closure.Error);
        }
    }

    private enum TimelineMode { Chain, Mixed, Lethal, Unscheduled, Fault, Overflow, NoChange, PriorityPrecision }

    internal static void TimelineNearPastTimeCannotPartiallyApplyState()
    {
        using (var test = new Fixture(consumePlayer: true))
        {
            var workspace = test.Stage();
            workspace.ScheduleAt(new ParticipantId(test.Battle.Enemy.RuntimeId), 9.99995f, 0);
            bool rejected;
            try { rejected = !test.Battle.State.TryCommitJournal(test.Lease, workspace).Succeeded; }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected && test.Battle.Enemy.CurrentHp == 120f && test.Battle.Player.CurrentMp == 30f &&
                test.ProgressValue == 10f && test.Weapon.CurrentDurability == 100 && test.Battle.State.BattleRevision == 0 &&
                test.Battle.Scheduler.GetTimelineSnapshot()[0].ScheduledTime == 20f,
                "Near-past timeline passed preflight and failed after HP/MP had already changed.");
        }
    }

    internal static void TimelineFactsRejectForgeryAndUnaccountedWrites()
    {
        for (int scenario = 0; scenario < 7; scenario++)
        using (var plan = new TimelinePlan())
        {
            Check(plan.Closure.Succeeded, plan.Closure.Error);
            var workspace = plan.Closure.Workspace.Fork();
            var entries = new List<DRPGReactionLedgerEntry>(plan.Closure.Entries);
            var entry = entries[0];
            var change = entry.Batch.Effects[0].TimelineChange;
            var effects = new List<DRPGActionEffectResult>(entry.Batch.Effects);
            if (scenario == 0) effects.Clear();
            if (scenario == 1) effects[0] = new DRPGActionEffectResult(DRPGActionEffectKind.TimelineShift,
                DRPGActionEffectResultState.Applied, entry.SourceParticipant.Value, change.Target.Value, 5, 5, 2,
                timelineBefore: 20, timelineAfter: 25);
            if (scenario == 2 || scenario == 3 || scenario == 4)
                effects[0] = new DRPGActionEffectResult(entry.SourceParticipant.Value, new DRPGActionTimelineChange(
                    change.Target, change.Delta, change.PriorityDelta,
                    scenario == 2 ? new BattleTimelineRuleSnapshot(change.Target, 21f, 0) : change.Before,
                    scenario == 3 ? new BattleTimelineRuleSnapshot(change.Target, 25f, 3) : change.After,
                    scenario == 4 ? DRPGTimelineShiftDisposition.Unchanged : change.Disposition));
            if (scenario == 5) workspace.ScheduleAt(change.Target, 11f, 1);
            if (scenario == 6) workspace.ScheduleAt(new ParticipantId(plan.Test.Battle.Player.RuntimeId), 15f, 0);
            entries[0] = new DRPGReactionLedgerEntry(entry.Identity, entry.ParentEventId, entry.EventId, entry.ReactionId,
                entry.SourceParticipant, entry.SourceId, entry.Category, entry.Depth, entry.InsertionOrdinal, entry.State,
                new DRPGResolvedActionBatch(entry.Batch.Hits, effects), entry.TargetParticipant);
            var altered = new DRPGImmediateReactionClosureResult(workspace, entries, null);
            Check(plan.Store.TryCommitActionResolution("timeline-root", plan.Frozen, plan.Primary, altered, plan.Test.Lease).Status ==
                DRPGPrimaryImpactCommitStatus.Rejected && plan.Schedule.ScheduledTime == 20f && plan.Test.Battle.Scheduler.Count == 1 &&
                plan.Test.Battle.Enemy.CurrentHp == 120f && plan.Test.ProgressValue == 10f && plan.Test.Weapon.CurrentDurability == 100 &&
                plan.Test.Battle.State.BattleRevision == 0, "Malformed timeline partially committed: " + scenario);
            Check(plan.Commit().Status == DRPGPrimaryImpactCommitStatus.Committed && plan.Schedule.ScheduledTime == 10f &&
                plan.Store.TryCommitActionResolution("timeline-root", plan.Frozen, plan.Primary, altered, plan.Test.Lease).Status ==
                DRPGPrimaryImpactCommitStatus.FingerprintConflict, "Invalid timeline poisoned a healthy retry or replayed as original: " + scenario);
            Check(change.Before.Value.ScheduledTime == 20f && change.After.Value.ScheduledTime == 25f,
                "Editing a fork mutated an immutable timeline event.");
        }
    }

    internal static void TimelineResourcePrepareFailureLeavesQueueUntouched()
    {
        using (var plan = new TimelinePlan())
        {
            Check(plan.Closure.Succeeded, plan.Closure.Error);
            Check(plan.Test.Pipeline.TryGain("workspace_player", "focus", DRPGProgressScope.Battle, 1f, out _, out _), "Resource drift setup failed.");
            Check(plan.Commit().Status == DRPGPrimaryImpactCommitStatus.Rejected && plan.Schedule.ScheduledTime == 20f &&
                plan.Schedule.Priority == 0 && plan.Test.Battle.Enemy.CurrentHp == 120f && plan.Test.ProgressValue == 11f &&
                plan.Test.Weapon.CurrentDurability == 100 && plan.Test.Battle.State.BattleRevision == 0,
                "A late resource prepare failure applied the already-prepared timeline or damage.");
        }
    }

    internal static void TimelineNoChangeRemainsRuleOnly()
    {
        using (var plan = new TimelinePlan(TimelineMode.NoChange))
        {
            Check(plan.Closure.Succeeded, plan.Closure.Error);
            var result = plan.Commit();
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && plan.Closure.Entries.Count == 1 &&
                result.Receipt.Ledger.Events[1].Disposition == DRPGLedgerDisposition.RuleOnly &&
                result.Receipt.Ledger.Events[1].Effects[0].TimelineChange.Disposition == DRPGTimelineShiftDisposition.Unchanged &&
                result.Receipt.Ledger.MutationJournal.TimelineMutations.Count == 0 && plan.Schedule.ScheduledTime == 20f && plan.Schedule.Priority == 0,
                "An unchanged timeline effect scheduled work, expanded reactions or requested an empty presentation beat: " + result.Error);
        }
    }

    internal static void TimelinePriorityFingerprintPreservesIntegerPrecision()
    {
        using (var plan = new TimelinePlan(TimelineMode.PriorityPrecision))
        {
            Check(plan.Closure.Succeeded, plan.Closure.Error);
            var result = plan.Commit();
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && plan.Schedule.Priority == 0, result.Error);
            var entries = new List<DRPGReactionLedgerEntry>(plan.Closure.Entries);
            var entry = entries[0]; var change = entry.Batch.Effects[0].TimelineChange;
            var changed = new DRPGActionEffectResult(entry.SourceParticipant.Value, new DRPGActionTimelineChange(change.Target,
                change.Delta, 16777217, change.Before, change.After, change.Disposition));
            Check(changed.AuxiliaryAmount == entry.Batch.Effects[0].AuxiliaryAmount,
                "The precision fixture does not exercise equal float summaries for distinct integer priorities.");
            entries[0] = new DRPGReactionLedgerEntry(entry.Identity, entry.ParentEventId, entry.EventId, entry.ReactionId,
                entry.SourceParticipant, entry.SourceId, entry.Category, entry.Depth, entry.InsertionOrdinal, entry.State,
                new DRPGResolvedActionBatch(entry.Batch.Hits, new[] { changed }), entry.TargetParticipant);
            var altered = new DRPGImmediateReactionClosureResult(plan.Closure.Workspace.Fork(), entries, null);
            Check(plan.Store.TryCommitActionResolution("timeline-root", plan.Frozen, plan.Primary, altered, plan.Test.Lease).Status ==
                DRPGPrimaryImpactCommitStatus.FingerprintConflict && plan.Schedule.Priority == 0 && plan.Test.Battle.State.BattleRevision == 1,
                "Timeline fingerprint collapsed distinct integer priorities into one float summary.");
        }
    }

    private sealed class TimelinePlan : IDisposable
    {
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        internal TimelinePlan(TimelineMode mode = TimelineMode.Chain)
        {
            Test = new Fixture(consumePlayer: true);
            var primary = CreateDamageSkill("timeline-primary", 20f);
            var first = CreateDamageSkill("timeline-first", 5f);
            var second = CreateDamageSkill("timeline-second", 200f);
            owned.Add(primary); owned.Add(first); owned.Add(second);
            var effects = new List<DRPGActionEffectDefinition> { DRPGActionEffectDefinition.TimelineShift(
                mode == TimelineMode.Unscheduled ? DRPGActionEffectTarget.Actor : DRPGActionEffectTarget.PrimaryTarget,
                mode == TimelineMode.NoChange ? 0f : 5f,
                mode == TimelineMode.Overflow ? int.MaxValue : mode == TimelineMode.PriorityPrecision ? 16777216 :
                    mode == TimelineMode.NoChange ? 0 : 2) };
            if (mode == TimelineMode.Mixed) effects.Add(DRPGActionEffectDefinition.Damage(DRPGActionEffectTarget.PrimaryTarget, 1f, DRPGDamageTag.Slash));
            StatusSet(first, "effects", effects);
            if (mode != TimelineMode.Lethal) StatusSet(second, "effects", new List<DRPGActionEffectDefinition> {
                DRPGActionEffectDefinition.TimelineShift(DRPGActionEffectTarget.PrimaryTarget, -40f,
                    mode == TimelineMode.Overflow ? 1 : mode == TimelineMode.PriorityPrecision ? -16777216 : -1) });
            var actor = Test.Battle.Player; var target = Test.Battle.Enemy;
            var actorId = new ParticipantId(actor.RuntimeId); var targetId = new ParticipantId(target.RuntimeId);
            Frozen = new DRPGBasicAttackFrozenSnapshot(new ActorTurnToken(Test.Identity.BattleGeneration, 1, actorId),
                new TargetSelectionToken(Test.Identity.BattleGeneration, 1, 1, actorId), targetId, primary).WithIdentity(Test.Identity);
            var workspace = Test.Battle.CreateWorkspace();
            Check(workspace.ActionResources.TryStageProgress(Test.Identity, Test.Spend(), out _, out string error), error);
            Check(workspace.ActionResources.TryStageWeaponUse(Test.Identity, "primary", Test.Weapon.InstanceId, 1, out error), error);
            var resolver = new DRPGActionResolver(); var participants = new[] { actor, target };
            Primary = resolver.ResolveIntoWorkspace(DRPGActionSpecResolver.Resolve(
                DRPGBattleCommand.BasicAttack(actor.RuntimeId, target.RuntimeId, primary), actor), actor, target, participants,
                workspace, DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f));
            Closure = new DRPGImmediateReactionLedgerBuilder().Build(Test.Identity,
                "action-" + Test.Identity.BattleGeneration + "-1-1-impact-0", Primary, workspace, participants,
                new[] { new TimelineSource(actorId, DRPGActionSpecResolver.Resolve(
                    DRPGBattleCommand.BasicAttack(actor.RuntimeId, target.RuntimeId, first), actor),
                    DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(actor.RuntimeId, target.RuntimeId, second), actor), mode) }, resolver);
            Store = new DRPGBattleActionResolutionLedgerStore(Test.Battle.State, Test.Battle.Arbiter.Trace);
        }
        internal Fixture Test { get; }
        internal DRPGBasicAttackFrozenSnapshot Frozen { get; }
        internal DRPGWorkspaceResolutionResult Primary { get; }
        internal DRPGImmediateReactionClosureResult Closure { get; }
        internal DRPGBattleActionResolutionLedgerStore Store { get; }
        internal DRPGTurnScheduleEntry Schedule => Test.Battle.Scheduler.GetTimelineSnapshot()[0];
        internal DRPGPrimaryImpactCommitResult Commit() => Store.TryCommitActionResolution("timeline-root", Frozen, Primary, Closure, Test.Lease);
        public void Dispose()
        {
            Test.Dispose();
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) UnityEngine.Object.DestroyImmediate(owned[i]);
        }
    }

    private sealed class TimelineSource : IDRPGImmediateReactionSource
    {
        private readonly DRPGResolvedActionSpec first, second;
        private readonly TimelineMode mode;
        internal TimelineSource(ParticipantId participant, DRPGResolvedActionSpec first, DRPGResolvedActionSpec second, TimelineMode mode)
        { Participant = participant; this.first = first; this.second = second; this.mode = mode; }
        public string SourceId => "timeline-source";
        public ParticipantId Participant { get; }
        public int HomeSlot => 0;
        public bool IsPassive => false;
        public void Collect(DRPGImmediateReactionContext context, ICollection<DRPGImmediateReactionRequest> requests)
        {
            if (context.Depth > 1) return;
            if (context.Depth == 1)
            {
                Check(mode != TimelineMode.Unscheduled && mode != TimelineMode.NoChange, "A skipped timeline shift expanded a descendant.");
                if (mode == TimelineMode.Fault) throw new InvalidOperationException("Injected post-timeline failure.");
                if (mode == TimelineMode.Mixed) return;
                Check(!context.HasHit && context.Effects[0].TimelineAfter == 25f, "Timeline context lost its non-hit outcome.");
            }
            requests.Add(new DRPGImmediateReactionRequest(context.Identity, context.EventId, "timeline-" + context.Depth,
                DRPGImmediateReactionCategory.OtherImmediate, 0, context.Depth == 0 ? first : second,
                DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f)));
        }
    }
}
#endif
