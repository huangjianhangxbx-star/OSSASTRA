#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

internal static partial class DRPGActionResourceJournalProbe
{
    internal static void RunVitalLedgerChecks()
    {
        VitalRecoveryAndShieldCommitTogether();
        VitalEffectsSurroundDamageInAuthoredOrder();
        VitalShieldProtectsTheNextReaction();
        VitalNoChangeDoesNotExpandAReaction();
        VitalHealingCannotResurrectALethalTarget();
        VitalLaterFailureDiscardsAllRecovery();
        VitalLateResourceFailureLeavesStateUntouched();
        VitalFactsRejectForgeryAndUnaccountedWrites();
        VitalOverflowDiscardsAllChanges();
        VitalEnemyRecoveryUsesTheSameLedger();
    }

    internal static void VitalRecoveryAndShieldCommitTogether()
    {
        using (var plan = new VitalPlan(new[] {
            DRPGActionEffectDefinition.Heal(DRPGActionEffectTarget.PrimaryTarget, 50f),
            DRPGActionEffectDefinition.Shield(DRPGActionEffectTarget.PrimaryTarget, 12f),
            DRPGActionEffectDefinition.PostureRestore(DRPGActionEffectTarget.PrimaryTarget, 50f) }))
        {
            Check(plan.Closure.Succeeded, "Recovery closure failed: " + plan.Closure.Error);
            var effects = plan.Closure.Entries[0].Batch.Effects;
            Check(effects.Count == 3 && effects[0].AppliedAmount == 0f && effects[1].AppliedAmount == 12f &&
                effects[2].AppliedAmount == 50f && plan.Test.Battle.Enemy.CurrentHp == 120f &&
                plan.Test.Battle.Enemy.CurrentPosture == 80f && plan.Test.Battle.Enemy.CurrentShield == 0f,
                "Recovery did not clamp against the staged primary hit, or mutated live state while building.");
            var result = plan.Commit();
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && plan.Test.Battle.Enemy.CurrentHp == 120f &&
                plan.Test.Battle.Enemy.CurrentPosture == 110f && plan.Test.Battle.Enemy.CurrentShield == 12f &&
                plan.Test.ProgressValue == 7f && plan.Test.Weapon.CurrentDurability == 99 &&
                plan.Test.Battle.State.BattleRevision == 1 && result.Receipt.Ledger.Events[1].Hit == null &&
                result.Receipt.Ledger.Events[1].Effects.Count == 3 && result.Receipt.Ledger.Events[1].RequiresPresentation &&
                ReferenceEquals(result.Receipt, plan.Commit().Receipt) && plan.Test.Battle.State.BattleRevision == 1,
                "Recovery, resources and immutable non-hit facts did not commit/replay together: " + result.Error);
        }
    }

    internal static void VitalEffectsSurroundDamageInAuthoredOrder()
    {
        using (var plan = new VitalPlan(new[] {
            DRPGActionEffectDefinition.Heal(DRPGActionEffectTarget.PrimaryTarget, 10f),
            DRPGActionEffectDefinition.Shield(DRPGActionEffectTarget.PrimaryTarget, 3f),
            DRPGActionEffectDefinition.PostureRestore(DRPGActionEffectTarget.PrimaryTarget, 5f),
            DRPGActionEffectDefinition.Damage(DRPGActionEffectTarget.PrimaryTarget),
            DRPGActionEffectDefinition.Heal(DRPGActionEffectTarget.PrimaryTarget, 4f) }))
        {
            Check(plan.Closure.Succeeded, "Mixed recovery closure failed: " + plan.Closure.Error);
            var hit = plan.Closure.Entries[0].Batch.Hits[0];
            var result = plan.Commit();
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && hit.HpBefore == 120f && hit.HpAfter == 120f &&
                hit.AppliedDamage == 0f && hit.ShieldAbsorbed == 0f && hit.PostureBefore == 65f &&
                hit.PostureAfter == 55f && plan.Test.Battle.Enemy.CurrentHp == 120f &&
                plan.Test.Battle.Enemy.CurrentShield == 3f && plan.Test.Battle.Enemy.CurrentPosture == 55f,
                "Damage was validated against step-start state instead of its exact place among recovery effects: " + result.Error);
        }
    }

    internal static void VitalShieldProtectsTheNextReaction()
    {
        using (var plan = new VitalPlan(new[] { DRPGActionEffectDefinition.Shield(DRPGActionEffectTarget.PrimaryTarget, 6f) },
            new[] { DRPGActionEffectDefinition.Damage(DRPGActionEffectTarget.PrimaryTarget) }))
        {
            Check(plan.Closure.Succeeded, plan.Closure.Error);
            var result = plan.Commit();
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && plan.Closure.Entries.Count == 2 &&
                result.Receipt.Ledger.Events[2].Hit.ShieldAbsorbed == 0f &&
                result.Receipt.Ledger.Events[2].Hit.DamageBreakdown.RequestedDamage == 10f &&
                plan.Test.Battle.Enemy.CurrentHp == 120f && plan.Test.Battle.Enemy.CurrentPosture == 50f &&
                plan.Test.Battle.Enemy.CurrentShield == 6f,
                "A child hit ignored the previous non-hit shield or inherited a fake hit: " + result.Error);
        }
    }

    internal static void VitalNoChangeDoesNotExpandAReaction()
    {
        using (var plan = new VitalPlan(new[] {
            DRPGActionEffectDefinition.Heal(DRPGActionEffectTarget.Actor, 50f),
            DRPGActionEffectDefinition.Shield(DRPGActionEffectTarget.PrimaryTarget, 0f),
            DRPGActionEffectDefinition.PostureRestore(DRPGActionEffectTarget.Actor, 0f) }, failIfExpanded: true))
        {
            Check(plan.Closure.Succeeded, "No-op recovery expanded a child: " + plan.Closure.Error);
            var result = plan.Commit();
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && plan.Closure.Entries.Count == 1 &&
                result.Receipt.Ledger.Events[1].Disposition == DRPGLedgerDisposition.RuleOnly &&
                !result.Receipt.Ledger.Events[1].RequiresPresentation && plan.Test.Battle.Enemy.CurrentHp == 120f &&
                plan.Test.Battle.Enemy.CurrentPosture == 60f,
                "Unchanged recovery requested a presentation beat or mutated the action: " + result.Error);
        }
    }

    internal static void VitalHealingCannotResurrectALethalTarget()
    {
        using (var plan = new VitalPlan(new[] {
            DRPGActionEffectDefinition.Damage(DRPGActionEffectTarget.PrimaryTarget, 20f),
            DRPGActionEffectDefinition.Heal(DRPGActionEffectTarget.PrimaryTarget, 200f),
            DRPGActionEffectDefinition.Shield(DRPGActionEffectTarget.PrimaryTarget, 50f),
            DRPGActionEffectDefinition.PostureRestore(DRPGActionEffectTarget.PrimaryTarget, 50f) }))
        {
            Check(plan.Closure.Succeeded, plan.Closure.Error);
            var result = plan.Commit();
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && plan.Test.Battle.Enemy.CurrentHp == 0f &&
                plan.Test.Battle.Enemy.IsDeployed && plan.Test.Battle.Enemy.CurrentShield == 0f &&
                plan.Test.Battle.Enemy.CurrentPosture == 0f && result.Receipt.Ledger.Events[1].Hit.WasDefeated &&
                result.Receipt.Ledger.Events[1].Effects[result.Receipt.Ledger.Events[1].Effects.Count - 1].State ==
                    DRPGActionEffectResultState.Skipped,
                "Ordinary recovery resurrected or removed a lethal target before its return barrier: " + result.Error);
        }
    }

    internal static void VitalLaterFailureDiscardsAllRecovery()
    {
        using (var plan = new VitalPlan(new[] {
            DRPGActionEffectDefinition.PostureRestore(DRPGActionEffectTarget.PrimaryTarget, 10f) }, failIfExpanded: true))
        {
            Check(!plan.Closure.Succeeded && plan.Closure.Error.Contains("Injected after recovery") &&
                plan.Commit().Status == DRPGPrimaryImpactCommitStatus.Rejected, "Recovery did not reach the injected later fault: " + plan.Closure.Error);
            plan.AssertUnchanged(10f);
        }
    }

    internal static void VitalLateResourceFailureLeavesStateUntouched()
    {
        using (var plan = new VitalPlan(new[] { DRPGActionEffectDefinition.Shield(DRPGActionEffectTarget.PrimaryTarget, 12f) }))
        {
            Check(plan.Closure.Succeeded, plan.Closure.Error);
            Check(plan.Test.Pipeline.TryGain("workspace_player", "focus", DRPGProgressScope.Battle, 1f, out _, out _), "Progress setup failed.");
            Check(plan.Commit().Status == DRPGPrimaryImpactCommitStatus.Rejected, "Stale resource plan committed recovery.");
            plan.AssertUnchanged(11f);
        }
    }

    internal static void VitalFactsRejectForgeryAndUnaccountedWrites()
    {
        for (int scenario = 0; scenario < 7; scenario++)
        using (var plan = new VitalPlan(new[] {
            DRPGActionEffectDefinition.PostureRestore(DRPGActionEffectTarget.PrimaryTarget, 10f) }))
        {
            Check(plan.Closure.Succeeded, plan.Closure.Error);
            var workspace = plan.Closure.Workspace.Fork();
            var entries = new List<DRPGReactionLedgerEntry>(plan.Closure.Entries);
            var entry = entries[0]; var original = entry.Batch.Effects[0]; var change = original.VitalChange;
            var effects = new List<DRPGActionEffectResult>(entry.Batch.Effects);
            if (scenario == 0) effects.Clear();
            if (scenario == 1) effects[0] = new DRPGActionEffectResult(original.Kind, original.State,
                original.ActorRuntimeId, original.TargetRuntimeId, original.RequestedAmount, original.AppliedAmount);
            if (scenario == 2) effects[0] = new DRPGActionEffectResult(original.ActorRuntimeId,
                new DRPGActionVitalChange(change.Target, change.Kind, change.Requested, change.Before + 1f, change.After + 1f));
            if (scenario == 3) effects[0] = new DRPGActionEffectResult(plan.Test.Battle.Enemy.RuntimeId, change);
            if (scenario == 4) workspace.For(change.Target).SetPosture(71f);
            if (scenario == 5) workspace.For(new ParticipantId(plan.Test.Battle.Player.RuntimeId)).SetShield(1f);
            if (scenario == 6) effects[0] = new DRPGActionEffectResult(original.ActorRuntimeId,
                new DRPGActionVitalChange(change.Target, DRPGActionEffectKind.Shield, change.Requested, change.Before, change.After));
            entries[0] = new DRPGReactionLedgerEntry(entry.Identity, entry.ParentEventId, entry.EventId, entry.ReactionId,
                entry.SourceParticipant, entry.SourceId, entry.Category, entry.Depth, entry.InsertionOrdinal, entry.State,
                new DRPGResolvedActionBatch(entry.Batch.Hits, effects), entry.TargetParticipant);
            var altered = new DRPGImmediateReactionClosureResult(workspace, entries, null);
            Check(plan.Store.TryCommitActionResolution("vital-root", plan.Frozen, plan.Primary, altered, plan.Test.Lease).Status ==
                DRPGPrimaryImpactCommitStatus.Rejected, "Malformed recovery committed: " + scenario);
            plan.AssertUnchanged(10f);
            Check(plan.Commit().Status == DRPGPrimaryImpactCommitStatus.Committed &&
                plan.Test.Battle.Enemy.CurrentHp == 120f && plan.Test.Battle.Enemy.CurrentPosture == 70f &&
                plan.Store.TryCommitActionResolution("vital-root", plan.Frozen, plan.Primary, altered, plan.Test.Lease).Status ==
                DRPGPrimaryImpactCommitStatus.FingerprintConflict, "Malformed recovery poisoned a healthy retry or replayed as original: " + scenario);
            Check(change.Before == 60f && change.After == 70f, "Forged fork changed the original immutable fact.");
        }
    }

    internal static void VitalOverflowDiscardsAllChanges()
    {
        using (var plan = new VitalPlan(new[] {
            DRPGActionEffectDefinition.Heal(DRPGActionEffectTarget.PrimaryTarget, 10f),
            DRPGActionEffectDefinition.Shield(DRPGActionEffectTarget.PrimaryTarget, float.MaxValue),
            DRPGActionEffectDefinition.Shield(DRPGActionEffectTarget.PrimaryTarget, float.MaxValue) }))
        {
            Check(!plan.Closure.Succeeded && plan.Closure.Error.Contains("overflow") &&
                plan.Commit().Status == DRPGPrimaryImpactCommitStatus.Rejected, "Shield overflow was accepted or failed before reaching recovery: " + plan.Closure.Error);
            plan.AssertUnchanged(10f);
        }
    }

    internal static void VitalEnemyRecoveryUsesTheSameLedger()
    {
        using (var plan = new VitalPlan(new[] {
            DRPGActionEffectDefinition.Heal(DRPGActionEffectTarget.PrimaryTarget, 10f),
            DRPGActionEffectDefinition.Shield(DRPGActionEffectTarget.PrimaryTarget, 12f),
            DRPGActionEffectDefinition.PostureRestore(DRPGActionEffectTarget.PrimaryTarget, 7f) }, enemyAttacker: true))
        {
            Check(plan.Closure.Succeeded, plan.Closure.Error);
            var result = plan.Commit();
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && plan.Test.Battle.Player.CurrentHp == 100f &&
                plan.Test.Battle.Player.CurrentPosture == 87f && plan.Test.Battle.Player.CurrentShield == 12f &&
                result.Receipt.Ledger.Events[1].Source.Value == plan.Test.Battle.Enemy.RuntimeId &&
                result.Receipt.Ledger.Events[1].Target.Value == plan.Test.Battle.Player.RuntimeId &&
                result.Receipt.Ledger.Events[1].Hit == null && plan.Test.Battle.Scheduler.CurrentTime == 10f &&
                plan.Test.Battle.State.BattleRevision == 1 && plan.Test.ProgressValue == 10f,
                "Enemy-sourced recovery used different ordering or advanced the current action: " + result.Error);
        }
    }

    private sealed class VitalPlan : IDisposable
    {
        private readonly List<DRPGSkillSO> owned = new List<DRPGSkillSO>();
        internal VitalPlan(DRPGActionEffectDefinition[] effects, DRPGActionEffectDefinition[] child = null,
            bool failIfExpanded = false, bool enemyAttacker = false, float primaryPostureDamage = 10f,
            Action<BattleParticipantRuntime> initializeTarget = null)
        {
            Test = new Fixture(consumePlayer: true);
            var primary = CreateDamageSkill("vital-primary", 20f); owned.Add(primary);
            var first = CreateDamageSkill("vital-first", 10f); owned.Add(first);
            StatusSet(first, "effects", new List<DRPGActionEffectDefinition>(effects));
            var second = CreateDamageSkill("vital-child", 10f); owned.Add(second);
            if (child != null) StatusSet(second, "effects", new List<DRPGActionEffectDefinition>(child));
            var actor = enemyAttacker ? Test.Battle.Enemy : Test.Battle.Player;
            var target = enemyAttacker ? Test.Battle.Player : Test.Battle.Enemy;
            if (initializeTarget != null)
                using (Test.Battle.State.OpenCompatibilityMutationScope(Test.Lease)) initializeTarget(target);
            var actorId = new ParticipantId(actor.RuntimeId); var targetId = new ParticipantId(target.RuntimeId);
            Frozen = new DRPGBasicAttackFrozenSnapshot(new ActorTurnToken(Test.Identity.BattleGeneration, 1, actorId),
                new TargetSelectionToken(Test.Identity.BattleGeneration, 1, 1, actorId), targetId, primary).WithIdentity(Test.Identity);
            var workspace = Test.Battle.CreateWorkspace();
            if (!enemyAttacker)
            {
                Check(workspace.ActionResources.TryStageProgress(Test.Identity, Test.Spend(), out _, out string error), error);
                Check(workspace.ActionResources.TryStageWeaponUse(Test.Identity, "primary", Test.Weapon.InstanceId, 1, out error), error);
            }
            var resolver = new DRPGActionResolver(); var participants = new[] { actor, target };
            Primary = resolver.ResolveIntoWorkspace(DRPGActionSpecResolver.Resolve(
                DRPGBattleCommand.BasicAttack(actor.RuntimeId, target.RuntimeId, primary), actor), actor, target, participants,
                workspace, DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f, postureDamage: primaryPostureDamage));
            Closure = new DRPGImmediateReactionLedgerBuilder().Build(Test.Identity,
                "action-" + Test.Identity.BattleGeneration + "-1-1-impact-0", Primary, workspace, participants,
                new[] { new VitalSource(actorId, DRPGActionSpecResolver.Resolve(
                    DRPGBattleCommand.BasicAttack(actor.RuntimeId, target.RuntimeId, first), actor),
                    child == null ? null : DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(actor.RuntimeId, target.RuntimeId, second), actor),
                    failIfExpanded) }, resolver);
            Store = new DRPGBattleActionResolutionLedgerStore(Test.Battle.State, Test.Battle.Arbiter.Trace);
        }
        internal Fixture Test { get; }
        internal DRPGBasicAttackFrozenSnapshot Frozen { get; }
        internal DRPGWorkspaceResolutionResult Primary { get; }
        internal DRPGImmediateReactionClosureResult Closure { get; }
        internal DRPGBattleActionResolutionLedgerStore Store { get; }
        internal DRPGPrimaryImpactCommitResult Commit() => Store.TryCommitActionResolution("vital-root", Frozen, Primary, Closure, Test.Lease);
        internal void AssertUnchanged(float progress) => Check(Test.Battle.Enemy.CurrentHp == 120f &&
            Test.Battle.Enemy.CurrentPosture == 80f && Test.Battle.Enemy.CurrentShield == 0f && Test.ProgressValue == progress &&
            Test.Weapon.CurrentDurability == 100 && Test.Battle.State.BattleRevision == 0 &&
            Test.Battle.Scheduler.CurrentTime == 10f && Test.Battle.Scheduler.Count == 1, "Rejected recovery partially changed live state.");
        public void Dispose()
        {
            Test.Dispose();
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) UnityEngine.Object.DestroyImmediate(owned[i]);
        }
    }

    private sealed class VitalSource : IDRPGImmediateReactionSource
    {
        private readonly DRPGResolvedActionSpec first, second;
        private readonly bool fail;
        internal VitalSource(ParticipantId participant, DRPGResolvedActionSpec first, DRPGResolvedActionSpec second, bool fail)
        { Participant = participant; this.first = first; this.second = second; this.fail = fail; }
        public string SourceId => "vital-source";
        public ParticipantId Participant { get; }
        public int HomeSlot => 0;
        public bool IsPassive => false;
        public void Collect(DRPGImmediateReactionContext context, ICollection<DRPGImmediateReactionRequest> requests)
        {
            if (context.Depth > 1) return;
            if (context.Depth == 1)
            {
                if (fail) throw new InvalidOperationException("Injected after recovery.");
                if (second == null) return;
                Check(!context.HasHit, "Shield-only reaction inherited a primary hit.");
            }
            requests.Add(new DRPGImmediateReactionRequest(context.Identity, context.EventId, "vital-" + context.Depth,
                DRPGImmediateReactionCategory.OtherImmediate, 0, context.Depth == 0 ? first : second,
                DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f)));
        }
    }
}
#endif
