#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

internal static partial class DRPGActionResourceJournalProbe
{
    internal static void RunPureStatusChecks()
    {
        PureStatusCommitsWithoutInventingHit();
        PureStatusCanParentALaterDamageReaction();
        PureStatusDepthBudgetIsEight();
        PureStatusStepBudgetIsThirtyTwo();
        PureStatusLaterFailureDiscardsTheWholeAction();
        PureStatusOnDefeatCannotReuseThePrimaryDeath();
        PureStatusSkippedEffectsRemainRuleOnly();
        PureStatusForgeryCannotPartiallyCommit();
        PureStatusCannotManufactureGuardOrEvade();
    }

    internal static void PureStatusCommitsWithoutInventingHit()
    {
        using (var plan = new PureStatusPlan())
        {
            Check(plan.Closure.Succeeded, "Pure status closure failed: " + plan.Closure.Error);
            Check(plan.Closure.Entries.Count == 1 && plan.Closure.Entries[0].Batch.Hits.Count == 0 &&
                plan.Test.Battle.Enemy.CurrentHp == 120f && plan.Test.Battle.Enemy.Statuses.Count == 0,
                "A pure status draft invented a hit or changed live state.");
            var result = plan.Commit();
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed, result.Error);
            Check(plan.Test.Battle.Enemy.CurrentHp == 120f && plan.Test.Battle.Enemy.CurrentPosture == 60f &&
                plan.Test.Battle.Enemy.Statuses.Snapshot()[0].Stacks == 1 &&
                plan.Test.ProgressValue == 7f && plan.Test.Weapon.CurrentDurability == 99 && plan.Test.Battle.State.BattleRevision == 1,
                "Pure status and the primary/resource journal did not commit once.");
            var fact = result.Receipt.Ledger.Events[1];
            Check(fact.Hit == null && fact.Effects.Count == 1 && fact.RequiresPresentation &&
                fact.Source.Value == plan.Test.Battle.Player.RuntimeId && fact.Target.Value == plan.Test.Battle.Enemy.RuntimeId &&
                fact.Identity == plan.Test.Identity && fact.ParentEventId == result.Receipt.Ledger.Events[0].EventId,
                "A pure status event requires a fake hit or lost its scoped presentation identity.");
            Check(ReferenceEquals(result.Receipt, plan.Commit().Receipt) && plan.Test.Battle.Enemy.CurrentHp == 120f &&
                plan.Test.Battle.Enemy.CurrentPosture == 60f &&
                plan.Test.Battle.Enemy.Statuses.Snapshot()[0].Stacks == 1 && plan.Test.Battle.State.BattleRevision == 1,
                "Replay duplicated a pure status reaction.");
        }
    }

    internal static void PureStatusCanParentALaterDamageReaction()
    {
        using (var plan = new PureStatusPlan(PureStatusMode.Followup))
        {
            Check(plan.Closure.Succeeded, "Pure status child closure failed: " + plan.Closure.Error);
            var result = plan.Commit();
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && plan.Test.Battle.Enemy.CurrentHp == 120f &&
                plan.Test.Battle.Enemy.CurrentPosture == 50f &&
                result.Receipt.Ledger.Events.Count == 3 && result.Receipt.Ledger.Events[1].Hit == null &&
                result.Receipt.Ledger.Events[2].ParentEventId == result.Receipt.Ledger.Events[1].EventId &&
                result.Receipt.Ledger.Events[2].Hit.HpBefore == 120f &&
                result.Receipt.Ledger.Events[2].Hit.PostureAfter == 50f &&
                result.Receipt.Ledger.Events[2].Hit.DamageBreakdown.RequestedDamage == 10f &&
                plan.Test.Battle.State.BattleRevision == 1,
                "A status parent could not trigger ordered damage in the same root transaction: " + result.Error);
        }
    }

    internal static void PureStatusDepthBudgetIsEight()
    {
        using (var plan = new PureStatusPlan(PureStatusMode.Depth))
        {
            Check(plan.Closure.Succeeded, "Pure status depth closure failed: " + plan.Closure.Error);
            var result = plan.Commit();
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && plan.Closure.Entries.Count == 9 &&
                plan.Closure.Entries[7].State == DRPGImmediateReactionState.Resolved && plan.Closure.Entries[7].Depth == 8 &&
                plan.Closure.Entries[8].State == DRPGImmediateReactionState.SkippedBudget && plan.Closure.Entries[8].Depth == 9 &&
                plan.Closure.Entries[8].Batch == null && !plan.Closure.Entries[8].RequiresPresentation &&
                plan.Test.Battle.Enemy.CurrentHp == 120f && plan.Test.Battle.Enemy.CurrentPosture == 60f &&
                plan.Test.Battle.Enemy.Statuses.Snapshot()[0].Stacks == 8 &&
                plan.Test.Battle.Enemy.Statuses.Snapshot()[0].RemainingTurns == 16,
                "Status-only descendants bypassed the depth-8 mutation/presentation budget: " + result.Error);
        }
    }

    internal static void PureStatusStepBudgetIsThirtyTwo()
    {
        using (var plan = new PureStatusPlan(PureStatusMode.Fanout))
        {
            Check(plan.Closure.Succeeded, "Pure status fanout closure failed: " + plan.Closure.Error);
            var result = plan.Commit();
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && plan.Closure.Entries.Count == 33 &&
                plan.Closure.Entries[31].State == DRPGImmediateReactionState.Resolved &&
                plan.Closure.Entries[32].State == DRPGImmediateReactionState.SkippedBudget &&
                plan.Test.Battle.Enemy.Statuses.Snapshot()[0].Stacks == 32 && plan.Test.Battle.Enemy.Statuses.Snapshot()[0].RemainingTurns == 64 &&
                plan.Test.Battle.Enemy.CurrentHp == 120f && plan.Test.Battle.Enemy.CurrentPosture == 60f &&
                plan.Test.Battle.State.BattleRevision == 1,
                "Pure status entries bypassed the shared 32-step budget: " + result.Error);
        }
    }

    internal static void PureStatusLaterFailureDiscardsTheWholeAction()
    {
        using (var plan = new PureStatusPlan(PureStatusMode.Fault))
        {
            Check(!plan.Closure.Succeeded && plan.Closure.Error.Contains("Injected post-status failure") &&
                plan.Commit().Status == DRPGPrimaryImpactCommitStatus.Rejected && plan.Test.Battle.Enemy.CurrentHp == 120f &&
                plan.Test.Battle.Enemy.Statuses.Count == 0 && plan.Test.ProgressValue == 10f &&
                plan.Test.Weapon.CurrentDurability == 100 && plan.Test.Battle.State.BattleRevision == 0,
                "The status effect was not reached, or a later source failure leaked partial mutations: " + plan.Closure.Error);
        }
    }

    internal static void PureStatusOnDefeatCannotReuseThePrimaryDeath()
    {
        using (var plan = new PureStatusPlan(PureStatusMode.OnDefeat))
        {
            Check(plan.Closure.Succeeded, "Pure status OnDefeat closure failed: " + plan.Closure.Error);
            var result = plan.Commit();
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && plan.Closure.Entries.Count == 2 &&
                plan.Closure.Entries[0].State == DRPGImmediateReactionState.Resolved &&
                plan.Closure.Entries[1].State == DRPGImmediateReactionState.SkippedIneligible &&
                plan.Closure.Entries[1].ParentEventId == plan.Closure.Entries[0].EventId &&
                plan.Test.Battle.Player.Statuses.Snapshot()[0].Stacks == 1 && plan.Test.Battle.Player.CurrentHp == 100f &&
                plan.Test.Battle.Enemy.CurrentHp == 0f && plan.Test.Battle.Enemy.IsDeployed &&
                result.Receipt.Ledger.Events[1].Hit == null && result.Receipt.Ledger.Events[1].Source.Value == plan.Test.Battle.Enemy.RuntimeId &&
                result.Receipt.Ledger.Events[1].Target.Value == plan.Test.Battle.Player.RuntimeId,
                "A status-only OnDefeat step reused the primary death, removed its actor early, or invented damage: " + result.Error);
        }
    }

    internal static void PureStatusSkippedEffectsRemainRuleOnly()
    {
        using (var plan = new PureStatusPlan(PureStatusMode.SkippedOnDefeat))
        {
            Check(plan.Closure.Succeeded, "Skipped status closure failed: " + plan.Closure.Error);
            var entry = plan.Closure.Entries[0];
            Check(entry.State == DRPGImmediateReactionState.Resolved && !entry.RequiresPresentation &&
                entry.Disposition == DRPGLedgerDisposition.RuleOnly && plan.Closure.Entries.Count == 1,
                "An ineffective pure status result acquired a presentation beat or expanded descendants.");
            var result = plan.Commit();
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed &&
                result.Receipt.Ledger.Events[1].Disposition == DRPGLedgerDisposition.RuleOnly &&
                result.Receipt.Ledger.Events[1].Hit == null && result.Receipt.Ledger.Events[1].Effects.Count == 1 &&
                result.Receipt.Ledger.Events[1].Effects[0].State == DRPGActionEffectResultState.Skipped &&
                plan.Test.Battle.Player.Statuses.Count == 0 && plan.Test.Battle.Enemy.Statuses.Count == 0 &&
                plan.Test.Battle.Enemy.CurrentHp == 0f && plan.Test.Battle.Enemy.IsDeployed &&
                plan.Test.Battle.State.BattleRevision == 1 && ReferenceEquals(result.Receipt, plan.Commit().Receipt),
                "Rule-only status diagnostics lost their scoped facts or mutated/committed twice: " + result.Error);
        }
    }

    internal static void PureStatusForgeryCannotPartiallyCommit()
    {
        for (int scenario = 0; scenario < 7; scenario++)
        using (var plan = new PureStatusPlan())
        {
            Check(plan.Closure.Succeeded, plan.Closure.Error);
            var workspace = plan.Closure.Workspace.Fork();
            var original = plan.Closure.Entries[0];
            var effects = new List<DRPGActionEffectResult>(original.Batch.Effects);
            var target = original.TargetParticipant;
            if (scenario == 0) target = default;
            if (scenario == 2) effects.Clear();
            if (scenario == 3) effects[0] = new DRPGActionEffectResult(DRPGActionEffectKind.ApplyStatus,
                DRPGActionEffectResultState.Applied, original.SourceParticipant.Value, target.Value, 1, 1, 2, "pure-vulnerability");
            if (scenario == 4) effects[0] = new DRPGActionEffectResult(target.Value, effects[0].StatusChange);
            if (scenario == 5) workspace.For(target).SetHitPoints(90f);
            if (scenario == 6) effects.Add(new DRPGActionEffectResult(DRPGActionEffectKind.Damage,
                DRPGActionEffectResultState.Applied, original.SourceParticipant.Value, target.Value, 10, 10, 0, "invented-hit"));
            var batch = scenario == 1 ? null : new DRPGResolvedActionBatch(original.Batch.Hits, effects);
            var changed = new DRPGImmediateReactionClosureResult(workspace, new[] {
                new DRPGReactionLedgerEntry(original.Identity, original.ParentEventId, original.EventId, original.ReactionId,
                    original.SourceParticipant, original.SourceId, original.Category, original.Depth, original.InsertionOrdinal,
                    original.State, batch, target) }, null);
            var result = plan.Store.TryCommitActionResolution("pure-status-root", plan.Frozen, plan.Primary, changed, plan.Test.Lease);
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Rejected && plan.Test.Battle.Enemy.CurrentHp == 120f &&
                plan.Test.Battle.Enemy.Statuses.Count == 0 && plan.Test.ProgressValue == 10f && plan.Test.Weapon.CurrentDurability == 100 &&
                plan.Test.Battle.State.BattleRevision == 0, "Malformed zero-hit draft leaked a partial mutation: " + scenario);
            Check(plan.Commit().Status == DRPGPrimaryImpactCommitStatus.Committed, "Invalid status draft poisoned a valid retry.");
            Check(plan.Store.TryCommitActionResolution("pure-status-root", plan.Frozen, plan.Primary, changed, plan.Test.Lease).Status ==
                DRPGPrimaryImpactCommitStatus.FingerprintConflict, "Changed zero-hit facts replayed as the original request: " + scenario);
        }
    }

    internal static void PureStatusCannotManufactureGuardOrEvade()
    {
        foreach (var mode in new[] { PureStatusMode.Guarded, PureStatusMode.Evaded })
        using (var plan = new PureStatusPlan(mode))
        {
            Check(!plan.Closure.Succeeded && plan.Closure.Error.Contains("pure status step") &&
                plan.Commit().Status == DRPGPrimaryImpactCommitStatus.Rejected && plan.Test.Battle.Enemy.CurrentHp == 120f &&
                plan.Test.Battle.Enemy.Statuses.Count == 0 && plan.Test.ProgressValue == 10f && plan.Test.Weapon.CurrentDurability == 100 &&
                plan.Test.Battle.State.BattleRevision == 0,
                "A zero-hit status request manufactured a Guard/Evade outcome or leaked state: " + plan.Closure.Error);
        }
    }

    private enum PureStatusMode { Once, Followup, Depth, Fanout, Fault, OnDefeat, SkippedOnDefeat, Guarded, Evaded }

    private sealed class PureStatusPlan : IDisposable
    {
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        internal PureStatusPlan(PureStatusMode mode = PureStatusMode.Once)
        {
            Test = new Fixture();
            bool onDefeat = mode == PureStatusMode.OnDefeat || mode == PureStatusMode.SkippedOnDefeat;
            var primary = CreateDamageSkill("pure-status-primary", onDefeat ? 200f : 20f);
            var statusSkill = CreateDamageSkill("pure-status-step", 0f);
            var followup = CreateDamageSkill("pure-status-followup", 5f);
            owned.Add(primary); owned.Add(statusSkill); owned.Add(followup);
            var definition = ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>(); owned.Add(definition);
            StatusSet(definition, "statusId", "pure-vulnerability");
            StatusSet(definition, "maxStacks", 100);
            StatusSet(definition, "durationTurns", 2);
            StatusSet(definition, "incomingDamageMultiplier", 2f);
            StatusSet(definition, "refreshPolicy", DRPGStatusRefreshPolicy.AddDuration);
            StatusSet(statusSkill, "effects", new List<DRPGActionEffectDefinition> {
                DRPGActionEffectDefinition.ApplyStatus(mode == PureStatusMode.SkippedOnDefeat ? DRPGActionEffectTarget.Actor :
                    DRPGActionEffectTarget.PrimaryTarget, definition) });
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
            var sourceActor = onDefeat ? target : actor;
            var sourceTarget = onDefeat ? actor : target;
            Closure = new DRPGImmediateReactionLedgerBuilder().Build(Test.Identity,
                "action-" + Test.Identity.BattleGeneration + "-1-1-impact-0", Primary, workspace, participants,
                new[] { new PureStatusSource(new ParticipantId(sourceActor.RuntimeId), DRPGActionSpecResolver.Resolve(
                    DRPGBattleCommand.BasicAttack(sourceActor.RuntimeId, sourceTarget.RuntimeId, statusSkill), sourceActor),
                    DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(sourceActor.RuntimeId, sourceTarget.RuntimeId, followup), sourceActor), mode) }, resolver);
            Store = new DRPGBattleActionResolutionLedgerStore(Test.Battle.State, Test.Battle.Arbiter.Trace);
        }
        internal Fixture Test { get; }
        internal DRPGBasicAttackFrozenSnapshot Frozen { get; }
        internal DRPGWorkspaceResolutionResult Primary { get; }
        internal DRPGImmediateReactionClosureResult Closure { get; }
        internal DRPGBattleActionResolutionLedgerStore Store { get; }
        internal DRPGPrimaryImpactCommitResult Commit() => Store.TryCommitActionResolution("pure-status-root", Frozen, Primary, Closure, Test.Lease);
        public void Dispose()
        {
            Test.Dispose();
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) UnityEngine.Object.DestroyImmediate(owned[i]);
        }
    }

    private sealed class PureStatusSource : IDRPGImmediateReactionSource
    {
        private readonly DRPGResolvedActionSpec status, followup;
        private readonly PureStatusMode mode;
        internal PureStatusSource(ParticipantId participant, DRPGResolvedActionSpec status, DRPGResolvedActionSpec followup, PureStatusMode mode)
        { Participant = participant; this.status = status; this.followup = followup; this.mode = mode; }
        public string SourceId => "pure-status-source";
        public ParticipantId Participant { get; }
        public int HomeSlot => 0;
        public bool IsPassive => false;
        public void Collect(DRPGImmediateReactionContext context, ICollection<DRPGImmediateReactionRequest> requests)
        {
            if (context.Depth == 1)
            {
                Check(!context.HasHit && context.Hit == null && context.Effects.Count == 1 &&
                    context.SourceParticipant == Participant && context.TargetParticipant.Value == status.TargetRuntimeId,
                    "A pure status context inherited a hit or lost its explicit source/target/effects.");
                Check(mode != PureStatusMode.SkippedOnDefeat, "A rule-only skipped status incorrectly expanded descendants.");
                Check(context.Effects[0].StatusChange != null && context.Effects[0].StatusChange.AppliedStatus.Stacks > 0,
                    "A status source cannot inspect its immutable typed effect.");
                if (mode == PureStatusMode.Fault) throw new InvalidOperationException("Injected post-status failure.");
                if (mode == PureStatusMode.Followup)
                    requests.Add(new DRPGImmediateReactionRequest(context.Identity, context.EventId, "pure-damage-child",
                        DRPGImmediateReactionCategory.Pursuit, 0, followup, DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f)));
            }
            if (context.Depth != 0 && mode != PureStatusMode.Depth && mode != PureStatusMode.OnDefeat) return;
            int count = mode == PureStatusMode.Fanout ? 33 : 1;
            for (int i = 0; i < count; i++)
                requests.Add(new DRPGImmediateReactionRequest(context.Identity, context.EventId, "pure-status-" + i,
                    DRPGImmediateReactionCategory.OtherImmediate, count - i, status, DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f),
                    mode == PureStatusMode.Guarded ? DRPGHitOutcome.Guarded : mode == PureStatusMode.Evaded ? DRPGHitOutcome.Evaded : DRPGHitOutcome.Hit,
                    mode == PureStatusMode.OnDefeat || mode == PureStatusMode.SkippedOnDefeat));
        }
    }
}
#endif
