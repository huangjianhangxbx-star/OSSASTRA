#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

internal static partial class DRPGActionResourceJournalProbe
{
    internal static void RunCompleteLedgerChecks()
    {
        CompleteLedgerExactlyOnce();
        CompleteLedgerFingerprintCoversReactionsAndResources();
        CompleteLedgerRejectsBrokenCausalityAndUnaccountedMutation();
        CompleteLedgerRejectsForeignLeaseAfterCommit();
        CompleteLedgerPublishesImmutableScopedFacts();
        CompleteLedgerAtomicFailureAndCounterEvade();
        CompleteLedgerWeaponEventIdentityCannotAlias();
        CompleteLedgerSkippedFactsAndMixedEntryIsolation();
    }

    internal static void CompleteLedgerExactlyOnce()
    {
        using (var plan = new CompletePlan())
        {
            var test = plan.Test;
            Check(test.Battle.Enemy.CurrentHp == 120f && test.ProgressValue == 10f && test.Weapon.CurrentDurability == 100,
                "Building a complete action changed live state.");
            var first = plan.Commit();
            Check(first.Status == DRPGPrimaryImpactCommitStatus.Committed, "Complete ledger did not commit: " + first.Error);
            Check(first.Receipt.Ledger.Reactions.Count == 1 && first.Receipt.Ledger.PrimaryImpact.Hit.HpAfter == 120f &&
                first.Receipt.Ledger.Reactions[0].Batch.Hits[0].HpAfter == 120f && test.Battle.Enemy.CurrentHp == 120f &&
                test.Battle.Enemy.CurrentPosture == 55f &&
                test.ProgressValue == 7f && test.Weapon.CurrentDurability == 99 && test.Battle.State.BattleRevision == 1,
                "The complete ledger omitted a reaction/resource or applied it more than once.");
            var replay = plan.Commit();
            Check(replay.Status == DRPGPrimaryImpactCommitStatus.AlreadyCommitted && ReferenceEquals(first.Receipt, replay.Receipt) &&
                test.Battle.State.BattleRevision == 1 && test.Battle.Enemy.CurrentHp == 120f &&
                test.Battle.Enemy.CurrentPosture == 55f && test.ProgressValue == 7f &&
                test.Weapon.CurrentDurability == 99, "A complete-action replay did not return the original receipt without consumption.");
            int impactTraces = 0;
            foreach (var entry in test.Battle.Arbiter.Trace.Snapshot())
            {
                if (entry.EventKind != ActionTraceEventKind.ImpactCommitted) continue;
                impactTraces++;
                Check(entry.HitOutcome == ActionTraceHitOutcome.Guarded && entry.BeforeProjection.IsSpecified &&
                    entry.AfterProjection.IsSpecified && entry.Actor.Value == test.Battle.Player.RuntimeId &&
                    entry.Target.Value == test.Battle.Enemy.RuntimeId && !string.IsNullOrEmpty(entry.LeaseId),
                    "The complete ledger's impact trace lost its outcome, before/after projection or owner context.");
            }
            Check(impactTraces == 1, "Replaying the ledger duplicated its committed impact trace.");
        }
    }

    internal static void CompleteLedgerFingerprintCoversReactionsAndResources()
    {
        for (int scenario = 0; scenario < 5; scenario++)
        using (var plan = new CompletePlan())
        {
            var first = plan.Commit();
            Check(first.Status == DRPGPrimaryImpactCommitStatus.Committed, "Fingerprint control did not commit: " + first.Error);
            var original = plan.Closure.Entries[0];
            var workspace = plan.Closure.Workspace.Fork();
            var entries = new List<DRPGReactionLedgerEntry>(plan.Closure.Entries);
            if (scenario == 0)
                entries[0] = new DRPGReactionLedgerEntry(original.Identity, original.ParentEventId, original.EventId,
                    original.ReactionId + "-changed", original.SourceParticipant, original.SourceId, original.Category,
                    original.Depth, original.InsertionOrdinal, original.State, original.Batch);
            if (scenario == 1)
                Check(workspace.ActionResources.TryStageProgress(plan.Test.Identity,
                    DRPGProgressMutationRequest.Gain("late", "gain", 0, "workspace_player", "focus", DRPGProgressScope.Battle, 1f),
                    out _, out string error), error);
            if (scenario == 2)
                Check(workspace.ActionResources.TryStageWeaponUse(plan.Test.Identity, "late-use", plan.Test.Weapon.InstanceId, 1, out string error), error);
            if (scenario == 3) entries.Clear();
            if (scenario == 4)
                entries.Add(new DRPGReactionLedgerEntry(original.Identity, original.EventId, original.EventId + "-skip", "budget",
                    original.SourceParticipant, original.SourceId, original.Category, 2, 2,
                    DRPGImmediateReactionState.SkippedBudget, null));
            var changed = new DRPGImmediateReactionClosureResult(workspace, entries, null);
            var result = plan.Store.TryCommitActionResolution("root-locked", plan.Frozen, plan.Primary, changed, plan.Test.Lease);
            Check(result.Status == DRPGPrimaryImpactCommitStatus.FingerprintConflict && result.Receipt == null &&
                plan.Test.Battle.State.BattleRevision == 1 && plan.Test.Battle.Enemy.CurrentHp == 120f &&
                plan.Test.Battle.Enemy.CurrentPosture == 55f &&
                plan.Test.ProgressValue == 7f && plan.Test.Weapon.CurrentDurability == 99,
                "Changed reaction/resource content was treated as the original request: " + scenario);
            Check(ReferenceEquals(first.Receipt, plan.Commit().Receipt), "A conflict poisoned the original cached receipt.");
        }
    }

    internal static void CompleteLedgerRejectsBrokenCausalityAndUnaccountedMutation()
    {
        for (int scenario = 0; scenario < 6; scenario++)
        using (var plan = new CompletePlan())
        {
            var original = plan.Closure.Entries[0];
            var workspace = plan.Closure.Workspace.Fork();
            var entries = new List<DRPGReactionLedgerEntry>(plan.Closure.Entries);
            if (scenario == 0) workspace.For(new ParticipantId(plan.Test.Battle.Enemy.RuntimeId)).SetHitPoints(94f);
            if (scenario == 1) workspace.For(new ParticipantId(plan.Test.Battle.Player.RuntimeId)).SetMana(20f);
            if (scenario == 2) entries.Add(original);
            if (scenario == 3)
                entries[0] = new DRPGReactionLedgerEntry(original.Identity, "missing-parent", original.EventId, original.ReactionId,
                    original.SourceParticipant, original.SourceId, original.Category, original.Depth, original.InsertionOrdinal, original.State, original.Batch);
            if (scenario == 4)
                entries[0] = new DRPGReactionLedgerEntry(new ActionIdentity(original.Identity.BattleGeneration, 2, 2), original.ParentEventId,
                    original.EventId, original.ReactionId, original.SourceParticipant, original.SourceId, original.Category,
                    original.Depth, original.InsertionOrdinal, original.State, original.Batch);
            if (scenario == 5)
                entries[0] = new DRPGReactionLedgerEntry(original.Identity, original.ParentEventId, original.EventId, original.ReactionId,
                    original.SourceParticipant, original.SourceId, original.Category, original.Depth, original.InsertionOrdinal,
                    DRPGImmediateReactionState.SkippedIneligible, original.Batch);
            var invalid = new DRPGImmediateReactionClosureResult(workspace, entries, null);
            var result = plan.Store.TryCommitActionResolution("root-locked", plan.Frozen, plan.Primary, invalid, plan.Test.Lease);
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Rejected && plan.Test.Battle.State.BattleRevision == 0 &&
                plan.Test.Battle.Enemy.CurrentHp == 120f && plan.Test.Battle.Player.CurrentMp == 30f &&
                plan.Test.ProgressValue == 10f && plan.Test.Weapon.CurrentDurability == 100,
                "Malformed full ledger partially committed: " + scenario);
            Check(plan.Commit().Status == DRPGPrimaryImpactCommitStatus.Committed,
                "Rejected draft prevented the valid original action from committing.");
        }
    }

    internal static void CompleteLedgerRejectsForeignLeaseAfterCommit()
    {
        using (var plan = new CompletePlan())
        {
            var first = plan.Commit();
            Check(first.Status == DRPGPrimaryImpactCommitStatus.Committed, first.Error);
            var foreignArbiter = new DRPGBattleTransactionArbiter(plan.Test.Identity.BattleGeneration, plan.Test.Battle.Arbiter.Trace);
            Check(foreignArbiter.TryAcquire(BattleTransactionIdentity.ForRootAction(plan.Test.Identity), out var foreign, out string error), error);
            using (foreign)
            {
                var result = plan.Store.TryCommitActionResolution("root-locked", plan.Frozen, plan.Primary, plan.Closure, foreign);
                Check(result.Status == DRPGPrimaryImpactCommitStatus.Rejected && result.Receipt == null,
                    "A different arbiter's same-identity lease obtained a committed action receipt.");
            }
            Check(ReferenceEquals(first.Receipt, plan.Commit().Receipt), "Foreign lease rejection invalidated the real owner.");
        }
    }

    internal static void CompleteLedgerPublishesImmutableScopedFacts()
    {
        using (var plan = new CompletePlan())
        {
            var result = plan.Commit();
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed, result.Error);
            var facts = result.Receipt.Ledger.Events;
            Check(facts.Count == 2 && facts[0].StepOrdinal == 0 && facts[1].StepOrdinal == 1 &&
                facts[0].Identity == plan.Test.Identity && facts[1].Identity == plan.Test.Identity &&
                facts[1].ParentEventId == facts[0].EventId && facts[0].Hit.HpAfter == 120f &&
                facts[1].Hit.HpAfter == 120f && facts[0].Hit.PostureAfter == 60f && facts[1].Hit.PostureAfter == 55f,
                "Committed events omitted the ordered immutable primary/reaction facts.");
            Check(plan.Store.TryGetCommittedActionEvent(plan.Test.Identity, facts[1].EventId, facts[1].Fingerprint, plan.Test.Lease, out var found) &&
                ReferenceEquals(found, facts[1]), "Committed reaction lookup did not return its original immutable fact.");
            Check(!plan.Store.TryGetCommittedActionEvent(new ActionIdentity(plan.Test.Identity.BattleGeneration, 2, 2),
                facts[1].EventId, facts[1].Fingerprint, plan.Test.Lease, out _) &&
                !plan.Store.TryGetCommittedActionEvent(plan.Test.Identity, facts[1].EventId, facts[1].Fingerprint ^ 1UL, plan.Test.Lease, out _) &&
                !plan.Store.TryGetCommittedActionEvent(plan.Test.Identity, "missing-event", facts[1].Fingerprint, plan.Test.Lease, out _),
                "Event lookup accepted a different action, event or fingerprint.");
            plan.Closure.Workspace.For(new ParticipantId(plan.Test.Battle.Enemy.RuntimeId)).SetHitPoints(80f);
            Check(facts[1].Hit.HpAfter == 120f && result.Receipt.Ledger.MutationJournal.ParticipantMutations[0].HitPoints == 120f &&
                result.Receipt.Ledger.MutationJournal.ParticipantMutations[0].Posture == 55f &&
                plan.Test.Battle.Enemy.CurrentHp == 120f && plan.Test.Battle.Enemy.CurrentPosture == 55f,
                "Editing a caller workspace mutated the committed ledger or live battle.");
            plan.Test.Lease.Dispose();
            Check(!plan.Store.TryGetCommittedActionEvent(plan.Test.Identity, facts[1].EventId, facts[1].Fingerprint, plan.Test.Lease, out _),
                "A released action lease obtained an event for presentation.");
        }
    }

    internal static void CompleteLedgerAtomicFailureAndCounterEvade()
    {
        for (int scenario = 0; scenario < 3; scenario++)
        using (var plan = new CompletePlan())
        {
            if (scenario == 0) plan.Test.Weapon.RestoreDurability(100, 98);
            if (scenario == 1) Check(plan.Test.Pipeline.TryGain("workspace_player", "focus", DRPGProgressScope.Battle, 1f, out _, out _), "Drift setup failed.");
            if (scenario == 2) plan.Closure.Workspace.For(new ParticipantId(plan.Test.Battle.Enemy.RuntimeId)).SetHitPoints(float.NaN);
            Check(plan.Commit().Status == DRPGPrimaryImpactCommitStatus.Rejected && plan.Test.Battle.State.BattleRevision == 0 &&
                plan.Test.Battle.Enemy.CurrentHp == 120f && plan.Test.ProgressValue == (scenario == 1 ? 11f : 10f) &&
                plan.Test.Weapon.CurrentDurability == (scenario == 0 ? 98 : 100),
                "A failed complete ledger exposed partial participant/resource changes.");
        }
        using (var plan = new CompletePlan(counter: true, evade: true))
        {
            var result = plan.Commit();
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && plan.Test.Battle.Enemy.CurrentHp == 120f &&
                plan.Test.Battle.Enemy.CurrentPosture == 60f &&
                plan.Test.Battle.Player.CurrentHp == 100f && plan.Test.ProgressValue == 7f && plan.Test.Weapon.CurrentDurability == 99 &&
                result.Receipt.Ledger.Reactions[0].Batch.Hits[0].Outcome == DRPGHitOutcome.Evaded,
                "Counter-evade facts did not commit without damage or extra resource consumption: " + result.Error);
        }
    }

    internal static void CompleteLedgerWeaponEventIdentityCannotAlias()
    {
        using (var plan = new CompletePlan())
        {
            var alternate = plan.RebuildWithWeaponEvent("different-weapon-event");
            Check(plan.Commit().Status == DRPGPrimaryImpactCommitStatus.Committed, "Weapon identity control failed.");
            var result = plan.Store.TryCommitActionResolution("root-locked", plan.Frozen, plan.Primary, alternate, plan.Test.Lease);
            Check(result.Status == DRPGPrimaryImpactCommitStatus.FingerprintConflict && plan.Test.Weapon.CurrentDurability == 99 &&
                plan.Test.Battle.State.BattleRevision == 1,
                "Equal weapon totals hid a different weapon-use event identity.");
        }
    }

    internal static void CompleteLedgerSkippedFactsAndMixedEntryIsolation()
    {
        using (var plan = new CompletePlan())
        {
            var previous = plan.Closure.Entries[0];
            var entries = new List<DRPGReactionLedgerEntry>(plan.Closure.Entries)
            {
                new DRPGReactionLedgerEntry(plan.Test.Identity, previous.EventId, "complete-skipped", "blocked-child",
                    previous.SourceParticipant, previous.SourceId, previous.Category, 2, 2,
                    DRPGImmediateReactionState.SkippedDuplicate, null)
            };
            var closure = new DRPGImmediateReactionClosureResult(plan.Closure.Workspace, entries, null);
            var result = plan.Store.TryCommitActionResolution("root-locked", plan.Frozen, plan.Primary, closure, plan.Test.Lease);
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed, result.Error);
            var facts = result.Receipt.Ledger.Events;
            Check(facts.Count == 3 && facts[2].StepOrdinal == 2 && !facts[2].RequiresPresentation &&
                facts[2].Disposition == DRPGLedgerDisposition.SkippedDiagnostic && facts[2].Hit == null && facts[2].Effects.Count == 0,
                "A skipped diagnostic became a damaging/presentable event.");
            var primaryOnly = plan.Store.TryCommitPrimaryImpact(plan.Test.Identity, 0, "root-locked", plan.Frozen,
                plan.Primary, plan.Closure.Workspace, plan.Test.Lease);
            Check(primaryOnly.Status == DRPGPrimaryImpactCommitStatus.FingerprintConflict && plan.Test.Battle.Enemy.CurrentHp == 120f &&
                plan.Test.Battle.Enemy.CurrentPosture == 55f &&
                plan.Test.ProgressValue == 7f && plan.Test.Weapon.CurrentDurability == 99 && plan.Test.Battle.State.BattleRevision == 1,
                "Primary-only and complete-action entry points independently consumed the same action key.");
        }
    }

    private sealed class CompleteReactionSource : IDRPGImmediateReactionSource
    {
        private readonly DRPGResolvedActionSpec spec;
        private readonly bool counter;
        private readonly bool evade;
        internal CompleteReactionSource(ParticipantId participant, DRPGResolvedActionSpec spec, bool counter, bool evade)
        { Participant = participant; this.spec = spec; this.counter = counter; this.evade = evade; }
        public ParticipantId Participant { get; }
        public string SourceId => "complete-ledger-source";
        public int HomeSlot => 0;
        public bool IsPassive => true;
        public void Collect(DRPGImmediateReactionContext context, ICollection<DRPGImmediateReactionRequest> requests)
        {
            if (context.Depth != 0) return;
            requests.Add(new DRPGImmediateReactionRequest(context.Identity, context.EventId, "complete-reaction",
                counter ? DRPGImmediateReactionCategory.Counterattack : DRPGImmediateReactionCategory.Pursuit, 0, spec,
                DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f), evade ? DRPGHitOutcome.Evaded : DRPGHitOutcome.Hit));
        }
    }

    private sealed class CompletePlan : IDisposable
    {
        private readonly DRPGSkillSO primarySkill;
        private readonly DRPGSkillSO reactionSkill;
        internal CompletePlan(bool counter = false, bool evade = false)
        {
            Test = new Fixture();
            primarySkill = CreateDamageSkill("complete-primary", 20f);
            reactionSkill = CreateDamageSkill("complete-reaction", 5f);
            var actor = Test.Battle.Player;
            var target = Test.Battle.Enemy;
            var actorId = new ParticipantId(actor.RuntimeId);
            var targetId = new ParticipantId(target.RuntimeId);
            Frozen = new DRPGBasicAttackFrozenSnapshot(new ActorTurnToken(Test.Identity.BattleGeneration, 1, actorId),
                new TargetSelectionToken(Test.Identity.BattleGeneration, 1, 1, actorId), targetId, primarySkill).WithIdentity(Test.Identity);
            var workspace = Test.Battle.CreateWorkspace();
            Check(workspace.ActionResources.TryStageProgress(Test.Identity, Test.Spend(), out _, out string error), error);
            Check(workspace.ActionResources.TryStageWeaponUse(Test.Identity, "primary", Test.Weapon.InstanceId, 1, out error), error);
            var participants = new[] { actor, target };
            var resolver = new DRPGActionResolver();
            Primary = resolver.ResolveIntoWorkspace(DRPGActionSpecResolver.Resolve(
                DRPGBattleCommand.BasicAttack(actor.RuntimeId, target.RuntimeId, primarySkill), actor), actor, target,
                participants, workspace, DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f));
            string eventId = "action-" + Test.Identity.BattleGeneration + "-1-1-impact-0";
            Closure = new DRPGImmediateReactionLedgerBuilder().Build(Test.Identity, eventId, Primary, workspace, participants,
                new[] { new CompleteReactionSource(counter ? targetId : actorId, DRPGActionSpecResolver.Resolve(
                    DRPGBattleCommand.BasicAttack(counter ? target.RuntimeId : actor.RuntimeId,
                        counter ? actor.RuntimeId : target.RuntimeId, reactionSkill), counter ? target : actor), counter, evade) }, resolver);
            Check(Closure.Succeeded, Closure.Error);
            Store = new DRPGBattleActionResolutionLedgerStore(Test.Battle.State, Test.Battle.Arbiter.Trace);
        }
        internal Fixture Test { get; }
        internal DRPGBasicAttackFrozenSnapshot Frozen { get; }
        internal DRPGWorkspaceResolutionResult Primary { get; }
        internal DRPGImmediateReactionClosureResult Closure { get; }
        internal DRPGBattleActionResolutionLedgerStore Store { get; }
        internal DRPGImmediateReactionClosureResult RebuildWithWeaponEvent(string eventId)
        {
            var actor = Test.Battle.Player;
            var target = Test.Battle.Enemy;
            var workspace = Test.Battle.CreateWorkspace();
            Check(workspace.ActionResources.TryStageProgress(Test.Identity, Test.Spend(), out _, out string error), error);
            Check(workspace.ActionResources.TryStageWeaponUse(Test.Identity, eventId, Test.Weapon.InstanceId, 1, out error), error);
            var resolver = new DRPGActionResolver();
            var participants = new[] { actor, target };
            var primary = resolver.ResolveIntoWorkspace(DRPGActionSpecResolver.Resolve(
                DRPGBattleCommand.BasicAttack(actor.RuntimeId, target.RuntimeId, primarySkill), actor), actor, target,
                participants, workspace, DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f));
            return new DRPGImmediateReactionLedgerBuilder().Build(Test.Identity,
                "action-" + Test.Identity.BattleGeneration + "-1-1-impact-0", primary, workspace, participants,
                new[] { new CompleteReactionSource(new ParticipantId(actor.RuntimeId), DRPGActionSpecResolver.Resolve(
                    DRPGBattleCommand.BasicAttack(actor.RuntimeId, target.RuntimeId, reactionSkill), actor), false, false) }, resolver);
        }
        internal DRPGPrimaryImpactCommitResult Commit() => Store.TryCommitActionResolution("root-locked", Frozen, Primary, Closure, Test.Lease);
        public void Dispose() { Test.Dispose(); UnityEngine.Object.DestroyImmediate(primarySkill); UnityEngine.Object.DestroyImmediate(reactionSkill); }
    }
}
#endif
