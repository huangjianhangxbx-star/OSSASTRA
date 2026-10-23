#if UNITY_EDITOR
using System;

internal static partial class DRPGActionResourceJournalProbe
{
    internal static void RunPostureWorkspaceChecks()
    {
        // The former exposed/stability/forced-skip posture lifecycle is no longer
        // part of the approved rules. Keep its historical probes below as migration
        // documentation, but gate the infrastructure contract on the replacement:
        // posture is the first health layer and recovery has no authored-value cap.
        BattleLayeredHealthProbe.RunAll();
    }

    internal static void PostureWorkspaceBreakClearsDefenseBeforeChildDamage()
    {
        using (var plan = new VitalPlan(new[] { DRPGActionEffectDefinition.Damage(DRPGActionEffectTarget.PrimaryTarget) },
            primaryPostureDamage: 80f, initializeTarget: target => target.BeginDefend()))
        {
            Check(plan.Closure.Succeeded, plan.Closure.Error);
            var hit = plan.Closure.Entries[0].Batch.Hits[0];
            Check(hit.AppliedDamage == 12.5f && hit.WasPostureBreakExposed && plan.Test.Battle.Enemy.IsDefending &&
                !plan.Test.Battle.Enemy.PostureBreak.IsExposed, "Child damage used live defense/break instead of staged break.");
            var result = plan.Commit();
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && plan.Test.Battle.Enemy.CurrentHp == 97.5f &&
                !plan.Test.Battle.Enemy.IsDefending && plan.Test.Battle.Enemy.PostureBreak.IsExposed &&
                plan.Test.Battle.Enemy.PostureBreak.State == DRPGPostureBreakState.ExposedPendingSkip,
                "Defense/break did not share the action commit: " + result.Error);
        }
    }

    internal static void PostureWorkspaceRestoreDoesNotEraseEarlierBreak()
    {
        using (var plan = new VitalPlan(new[] { DRPGActionEffectDefinition.PostureRestore(DRPGActionEffectTarget.PrimaryTarget, 30f) },
            new[] { DRPGActionEffectDefinition.Damage(DRPGActionEffectTarget.PrimaryTarget) }, primaryPostureDamage: 80f))
        {
            Check(plan.Closure.Succeeded, plan.Closure.Error);
            var result = plan.Commit();
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && plan.Test.Battle.Enemy.CurrentHp == 87.5f &&
                plan.Test.Battle.Enemy.CurrentPosture == 30f && plan.Test.Battle.Enemy.PostureBreak.IsExposed &&
                result.Receipt.Ledger.Events[2].Hit.WasPostureBreakExposed,
                "A later restore erased a transient zero/break from the committed action: " + result.Error);
        }
    }

    internal static void PostureWorkspaceStandaloneBreakPrecedesDamage()
    {
        using (var plan = new VitalPlan(new[] {
            DRPGActionEffectDefinition.PostureDamage(DRPGActionEffectTarget.PrimaryTarget, 70f),
            DRPGActionEffectDefinition.Damage(DRPGActionEffectTarget.PrimaryTarget) }))
        {
            Check(plan.Closure.Succeeded, "Standalone posture failed: " + plan.Closure.Error);
            var result = plan.Commit();
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && plan.Test.Battle.Enemy.CurrentHp == 87.5f &&
                plan.Test.Battle.Enemy.CurrentPosture == 0f && plan.Test.Battle.Enemy.PostureBreak.IsExposed &&
                result.Receipt.Ledger.Events[1].Effects[0].CausedPostureBreak && result.Receipt.Ledger.Events[1].Hit.WasPostureBreakExposed,
                "Ordered independent posture loss did not enter break before damage: " + result.Error);
        }
    }

    internal static void PostureWorkspaceStabilityReducesIndependentDamage()
    {
        using (var plan = new VitalPlan(new[] { DRPGActionEffectDefinition.PostureDamage(DRPGActionEffectTarget.PrimaryTarget, 10f) },
            initializeTarget: target => { target.ReducePosture(80f); target.ResolvePostureBreakOwnTurnStart(); target.ResolvePostureBreakOwnTurnStart(); }))
        {
            Check(plan.Closure.Succeeded, plan.Closure.Error);
            var result = plan.Commit();
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && plan.Test.Battle.Enemy.CurrentPosture == 30f &&
                result.Receipt.Ledger.Events[1].Hit == null && result.Receipt.Ledger.Events[1].Effects[0].AppliedAmount == 5f &&
                plan.Test.Battle.Enemy.PostureBreak.State == DRPGPostureBreakState.RecoveryStability &&
                plan.Test.Battle.Enemy.PostureBreak.RemainingStabilityOwnActions == 3,
                "Independent posture damage ignored recovery stability or prematurely completed an own action: " + result.Error);
        }
    }

    internal static void PostureWorkspaceRejectsBreakStateDrift()
    {
        using (var plan = new VitalPlan(new[] { DRPGActionEffectDefinition.Shield(DRPGActionEffectTarget.PrimaryTarget, 10f) }))
        {
            Check(plan.Closure.Succeeded, plan.Closure.Error);
            using (plan.Test.Battle.State.OpenCompatibilityMutationScope(plan.Test.Lease)) plan.Test.Battle.Enemy.PostureBreak.Enter();
            Check(plan.Commit().Status == DRPGPrimaryImpactCommitStatus.Rejected && plan.Test.Battle.Enemy.PostureBreak.IsExposed,
                "A workspace with stale break state was committed.");
            plan.AssertUnchanged(10f);
        }
    }

    internal static void PostureWorkspacePrimaryOnlyGatePreservesBreak()
    {
        using (var test = new Fixture())
        {
            var skill = CreateDamageSkill("posture-primary", 20f);
            try
            {
                var actor = test.Battle.Player; var target = test.Battle.Enemy;
                var workspace = test.Battle.CreateWorkspace();
                var result = new DRPGActionResolver().ResolveIntoWorkspace(DRPGActionSpecResolver.Resolve(
                    DRPGBattleCommand.BasicAttack(actor.RuntimeId, target.RuntimeId, skill), actor), actor, target,
                    new[] { actor, target }, workspace, DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f, 80f));
                var actorId = new ParticipantId(actor.RuntimeId);
                var frozen = new DRPGBasicAttackFrozenSnapshot(new ActorTurnToken(test.Identity.BattleGeneration, 1, actorId),
                    new TargetSelectionToken(test.Identity.BattleGeneration, 1, 1, actorId), new ParticipantId(target.RuntimeId), skill).WithIdentity(test.Identity);
                var store = new DRPGBattleActionResolutionLedgerStore(test.Battle.State, test.Battle.Arbiter.Trace);
                var commit = store.TryCommitPrimaryImpact(test.Identity, 0, "posture-primary-root", frozen, result, workspace, test.Lease);
                Check(commit.Status == DRPGPrimaryImpactCommitStatus.Committed && target.CurrentPosture == 0f && target.PostureBreak.IsExposed,
                    "The primary-only commit rejected its derived break lifecycle: " + commit.Error);
            }
            finally { UnityEngine.Object.DestroyImmediate(skill); }
        }
    }

    internal static void PostureWorkspaceForgedBreakFactsAreRejected()
    {
        using (var plan = new VitalPlan(new[] { DRPGActionEffectDefinition.PostureDamage(DRPGActionEffectTarget.PrimaryTarget, 70f) }))
        {
            Check(plan.Closure.Succeeded, plan.Closure.Error);
            var entry = plan.Closure.Entries[0]; var change = entry.Batch.Effects[0].PostureChange;
            var forged = new DRPGActionPostureChange(change.Target, change.Requested, change.Before, change.After,
                change.BeforeState, DRPGPostureRuleSnapshot.Stable, change.BeforeDefending, change.AfterDefending, false);
            var effects = new[] { new DRPGActionEffectResult(entry.SourceParticipant.Value, forged) };
            var replacement = new DRPGReactionLedgerEntry(entry.Identity, entry.ParentEventId, entry.EventId, entry.ReactionId,
                entry.SourceParticipant, entry.SourceId, entry.Category, entry.Depth, entry.InsertionOrdinal, entry.State,
                new DRPGResolvedActionBatch(entry.Batch.Hits, effects), entry.TargetParticipant);
            var altered = new DRPGImmediateReactionClosureResult(plan.Closure.Workspace, new[] { replacement }, null);
            Check(plan.Store.TryCommitActionResolution("vital-root", plan.Frozen, plan.Primary, altered, plan.Test.Lease).Status ==
                DRPGPrimaryImpactCommitStatus.Rejected && !plan.Test.Battle.Enemy.PostureBreak.IsExposed, "A forged break state committed.");
            plan.AssertUnchanged(10f);
            Check(plan.Commit().Status == DRPGPrimaryImpactCommitStatus.Committed &&
                plan.Store.TryCommitActionResolution("vital-root", plan.Frozen, plan.Primary, altered, plan.Test.Lease).Status ==
                DRPGPrimaryImpactCommitStatus.FingerprintConflict, "A changed break fact replayed as the healthy original.");
        }
    }
}
#endif
