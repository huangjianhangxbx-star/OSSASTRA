#if UNITY_EDITOR
using System;
using UnityEngine;

internal static partial class DRPGActionResourceJournalProbe
{
    internal static void RunActionRuleSourceChecks()
    {
        ActionRuleSourceConfiguredGainCommitsWithTheLedger();
        ActionRuleSourceIncludesOnlyOptedInReactions();
        ActionRuleSourceConfigurationIsFrozen();
        ActionRuleSourceFailureDiscardsTheWholeDraft();
    }

    internal static void ActionRuleSourceConfiguredGainCommitsWithTheLedger()
    {
        using (var test = new SourcePlan(false))
        {
            var planned = test.Stage(); Check(planned.Succeeded, planned.Error);
            Check(test.Plan.Test.ProgressValue == 10f, "Collecting authored progress touched the live store.");
            var committed = test.Commit(planned);
            Check(committed.Status == DRPGPrimaryImpactCommitStatus.Committed && test.Plan.Test.ProgressValue == 9f &&
                test.Plan.Test.Battle.Enemy.CurrentHp == 120f && test.Plan.Test.Battle.Enemy.CurrentPosture == 60f &&
                test.Plan.Test.Battle.Enemy.CurrentShield == 2f &&
                test.Plan.Test.Weapon.CurrentDurability == 99 && test.Plan.Test.Battle.State.BattleRevision == 1 &&
                committed.Receipt.Ledger.MutationJournal.ActionResources.ProgressMutations.Count == 2,
                "Authored gain did not share root damage, reaction and resource commit: " + committed.Error);
            Check(ReferenceEquals(committed.Receipt, test.Commit(planned).Receipt) && test.Plan.Test.ProgressValue == 9f,
                "Ledger replay ran authored progress again.");
        }
    }

    internal static void ActionRuleSourceIncludesOnlyOptedInReactions()
    {
        using (var test = new SourcePlan(true))
        {
            var planned = test.Stage(); Check(planned.Succeeded, planned.Error);
            var duplicate = test.Sources.Stage(test.Plan.Test.Identity, test.Plan.Primary, planned);
            Check(duplicate.Succeeded, duplicate.Error);
            var committed = test.Commit(duplicate);
            Check(committed.Status == DRPGPrimaryImpactCommitStatus.Committed && test.Plan.Test.ProgressValue == 11f &&
                committed.Receipt.Ledger.MutationJournal.ActionResources.ProgressMutations.Count == 3,
                "Zero-hit authored reaction progress was lost or duplicate staging charged twice: " + committed.Error);
        }
    }

    internal static void ActionRuleSourceConfigurationIsFrozen()
    {
        using (var test = new SourcePlan(true))
        {
            StatusSet(test.Rule, "amount", 9f); StatusSet(test.Resource, "resourceId", "changed-resource");
            var planned = test.Stage(); Check(planned.Succeeded, planned.Error);
            Check(test.Commit(planned).Status == DRPGPrimaryImpactCommitStatus.Committed && test.Plan.Test.ProgressValue == 11f,
                "Locked event sources reread a mutable rule or resource asset.");
        }
    }

    internal static void ActionRuleSourceFailureDiscardsTheWholeDraft()
    {
        using (var test = new SourcePlan(true, missingResource: true))
        {
            var planned = test.Stage();
            Check(!planned.Succeeded && planned.Error.Contains("unregistered") && planned.Workspace == null,
                "Missing authored resource did not reject the complete draft for the expected reason: " + planned.Error);
            test.Plan.AssertUnchanged(10f);
            Check(test.Plan.Closure.Workspace.ActionResources.TryGetProgress("workspace_player", "focus", out var original) && original.CurrentValue == 7f,
                "Failure changed the caller's detached resource plan.");
        }
    }

    private sealed class SourcePlan : IDisposable
    {
        internal SourcePlan(bool includeReaction, bool missingResource = false)
        {
            Plan = new VitalPlan(new[] { DRPGActionEffectDefinition.Shield(DRPGActionEffectTarget.PrimaryTarget, 2f) });
            Resource = ScriptableObject.CreateInstance<DRPGProgressResourceSO>();
            StatusSet(Resource, "resourceId", missingResource ? "unregistered" : "focus");
            Rule = new DRPGProgressGainRuleDefinition("gain", Resource, DRPGBattleEventKind.ActionImpactResolved,
                2f, DRPGProgressOwnerSelection.Self, includeReactionDepth: includeReaction, oncePerRootAction: false);
            var source = new DRPGConfiguredProgressSource("production-configured", new[] {
                new DRPGConfiguredProgressRuleBinding("vital-primary", Rule), new DRPGConfiguredProgressRuleBinding("vital-first", Rule) });
            Sources = new DRPGActionRuleSources(new DRPGBattleProgressPipeline(new[] { source }));
        }
        internal VitalPlan Plan { get; }
        internal DRPGProgressResourceSO Resource { get; }
        internal DRPGProgressGainRuleDefinition Rule { get; }
        internal DRPGActionRuleSources Sources { get; }
        internal DRPGImmediateReactionClosureResult Stage() => Sources.Stage(Plan.Test.Identity, Plan.Primary, Plan.Closure);
        internal DRPGPrimaryImpactCommitResult Commit(DRPGImmediateReactionClosureResult closure) =>
            Plan.Store.TryCommitActionResolution("vital-root", Plan.Frozen, Plan.Primary, closure, Plan.Test.Lease);
        public void Dispose() { Plan.Dispose(); UnityEngine.Object.DestroyImmediate(Resource); }
    }
}
#endif
