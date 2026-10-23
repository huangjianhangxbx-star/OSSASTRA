#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

internal static partial class DRPGActionResourceJournalProbe
{
    internal static void RunActionCostChecks()
    {
        ActionCostsPrecedeCounterAndCommitOnce();
        ActionCostsCancelAndRejectLegacyConsumption();
        ActionCostsRejectLateStageAndForeignIdentity();
        ActionCostsFailureLeavesReservationsUnconsumed();
        ActionCostsRequireMatchingFrozenProgress();
        ActionCostsCannotBypassCompleteLedger();
        ActionCostsRejectDirectCostCommit();
        ActionCostsRequireCapturedActorProgressOwner();
        ActionCostsRejectOwnerDriftAtCommit();
    }

    internal static void ActionCostsPrecedeCounterAndCommitOnce()
    {
        using (var plan = new CostPlan())
        {
            Check(plan.Input.BuildValidatedJournal().ParticipantMutations.Count == 0 &&
                plan.Test.Battle.Player.CurrentMp == 30f && plan.Test.ProgressValue == 10f,
                "Staging costs mutated the source workspace or live resources.");
            Check(plan.Closure.Entries[0].Batch.Hits[0].PostureBefore == 90f &&
                plan.Closure.Entries[0].Batch.Hits[0].PostureAfter == 85f,
                "Counterattack did not see posture after the 10-point action cost. " +
                plan.Closure.Entries[0].Batch.Hits[0].PostureBefore + "->" +
                plan.Closure.Entries[0].Batch.Hits[0].PostureAfter);
            var result = plan.Commit();
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed, "Cost ledger did not commit: " + result.Error);
            Check(plan.Test.Battle.Player.CurrentMp == 25f && plan.Test.Battle.Player.CurrentPosture == 85f &&
                plan.Test.Battle.Player.CurrentHp == 100f && plan.Test.Battle.Enemy.CurrentHp == 120f &&
                plan.Test.Battle.Enemy.CurrentPosture == 60f &&
                plan.Test.ProgressValue == 7f && plan.Test.Weapon.CurrentDurability == 99 && plan.Test.Battle.State.BattleRevision == 1,
                "Cost commit overwrote the counterattack or omitted a resource from its single revision.");
            Check(plan.Costs.Resources.State == DRPGActionResourceTransactionState.Committed &&
                plan.Costs.Resources.Receipt.SpentMana == 5f && plan.Costs.Resources.Receipt.SpentPosture == 10f &&
                plan.Costs.Progress.State == DRPGActionProgressTransactionState.Committed &&
                plan.Costs.Progress.Mutation.ValueBefore == 10f && plan.Costs.Progress.Mutation.ValueAfter == 7f,
                "Committed real reservation lifecycles did not retain the staged cost receipts.");
            var replay = plan.Commit();
            Check(replay.Status == DRPGPrimaryImpactCommitStatus.AlreadyCommitted && ReferenceEquals(result.Receipt, replay.Receipt) &&
                plan.Test.Battle.State.BattleRevision == 1 && plan.Test.Battle.Player.CurrentPosture == 85f && plan.Test.ProgressValue == 7f,
                "Replaying the cost-bearing closure consumed resources twice.");
            Check(!plan.Costs.Cancel(), "A committed reservation was cancelled.");
        }
    }

    internal static void ActionCostsCancelAndRejectLegacyConsumption()
    {
        using (var plan = new CostPlan())
        {
            Check(!plan.Costs.Resources.TryCommit(plan.Test.Battle.Player, out _, out _) &&
                !plan.Costs.Resources.TryBindPrimaryImpact(new DRPGPrimaryImpactKey(plan.Test.Identity, 0), out _) &&
                !plan.Costs.Progress.TryCommit(plan.Test.Pipeline, out _, out _) &&
                !plan.Costs.Resources.Cancel() && !plan.Costs.Progress.Cancel(),
                "A child transaction bypassed its root cost owner.");
            Check(plan.Costs.Cancel() && !plan.Costs.Cancel(), "Root cost cancellation was not terminal and idempotent.");
            Check(plan.Commit().Status == DRPGPrimaryImpactCommitStatus.Rejected &&
                plan.Costs.Resources.State == DRPGActionResourceTransactionState.Cancelled &&
                plan.Costs.Progress.State == DRPGActionProgressTransactionState.Cancelled,
                "A cancelled cost-bearing descendant committed.");
            AssertCostLiveUnchanged(plan);
        }
    }

    internal static void ActionCostsRejectLateStageAndForeignIdentity()
    {
        using (var plan = new CostPlan())
        {
            Check(DRPGActionCostReservation.TryReserve(plan.Test.Identity, plan.Spec, plan.Test.Battle.Player,
                plan.Test.Pipeline, out var other, out string error), error);
            Check(!other.TryStageBeforeResolution(plan.Closure.Workspace, out _, out _),
                "Costs were accepted after the reaction chain had already changed actor posture.");
            using (var foreign = new Fixture())
                Check(!other.TryStageBeforeResolution(foreign.Battle.CreateWorkspace(), out _, out _),
                    "A reservation staged into another battle generation.");
            Check(other.TryStageBeforeResolution(plan.Input, out _, out error), "A failed staging attempt poisoned retry: " + error);
            Check(other.Cancel(), "Unused reservation cancellation failed.");
            Check(plan.Commit().Status == DRPGPrimaryImpactCommitStatus.Committed,
                "A sibling reservation invalidated the original draft.");
        }
    }

    internal static void ActionCostsFailureLeavesReservationsUnconsumed()
    {
        for (int scenario = 0; scenario < 3; scenario++)
        using (var plan = new CostPlan())
        {
            var workspace = plan.Closure.Workspace.Fork();
            if (scenario == 0) workspace.For(new ParticipantId(plan.Test.Battle.Player.RuntimeId)).SetPosture(90f);
            if (scenario == 1) plan.Test.Weapon.RestoreDurability(100, 98);
            if (scenario == 2) workspace.For(new ParticipantId(plan.Test.Battle.Enemy.RuntimeId)).SetHitPoints(float.NaN);
            var invalid = new DRPGImmediateReactionClosureResult(workspace, plan.Closure.Entries, null);
            Check(plan.Store.TryCommitActionResolution("cost-root", plan.Frozen, plan.Primary, invalid, plan.Test.Lease).Status ==
                DRPGPrimaryImpactCommitStatus.Rejected && plan.Costs.Resources.State == DRPGActionResourceTransactionState.Reserved &&
                plan.Costs.Progress.State == DRPGActionProgressTransactionState.Reserved && plan.Test.Battle.Player.CurrentPosture == 100f &&
                plan.Test.Battle.Player.CurrentMp == 30f && plan.Test.ProgressValue == 10f && plan.Test.Battle.State.BattleRevision == 0,
                "A failed cost-bearing ledger partially consumed a reservation: " + scenario);
            if (scenario != 1) Check(plan.Commit().Status == DRPGPrimaryImpactCommitStatus.Committed,
                "Rejecting a modified descendant invalidated the original cost draft.");
            else Check(plan.Costs.Cancel(), "A stale draft could not release its reservations.");
        }
    }

    internal static void ActionCostsRequireMatchingFrozenProgress()
    {
        using (var plan = new CostPlan())
        {
            SetCostField(plan.PrimarySkill, "actionProgressCost", MakeProgressCost(4f));
            var mismatch = plan.Freeze();
            Check(plan.Store.TryCommitActionResolution("cost-root", mismatch, plan.Primary, plan.Closure, plan.Test.Lease).Status ==
                DRPGPrimaryImpactCommitStatus.Rejected, "A three-point reservation paid a frozen four-point progress cost.");
            AssertCostLiveUnchanged(plan);
            Check(plan.Commit().Status == DRPGPrimaryImpactCommitStatus.Committed, "Frozen cost rejection poisoned valid retry.");
        }
        using (var plan = new CompletePlan())
        {
            // A zero-MP action with a configured progress cost still needs its own reservation.
            var skill = CreateDamageSkill("complete-primary", 20f);
            try
            {
                SetCostField(skill, "actionProgressCost", MakeProgressCost(3f));
                var frozen = new DRPGBasicAttackFrozenSnapshot(plan.Frozen.Turn, plan.Frozen.Selection,
                    plan.Frozen.Target, skill).WithIdentity(plan.Test.Identity);
                Check(plan.Store.TryCommitActionResolution("root-locked", frozen, plan.Primary, plan.Closure, plan.Test.Lease).Status ==
                    DRPGPrimaryImpactCommitStatus.Rejected, "An unrelated progress journal substituted for the missing action reservation.");
            }
            finally { UnityEngine.Object.DestroyImmediate(skill); }
        }
    }

    internal static void ActionCostsCannotBypassCompleteLedger()
    {
        using (var plan = new CostPlan())
        {
            Check(!plan.Test.Battle.State.TryCommitJournal(plan.Test.Lease, plan.Closure.Workspace).Succeeded,
                "Direct State commit consumed a root cost draft without its complete ledger.");
            var primaryOnly = plan.Store.TryCommitPrimaryImpact(plan.Test.Identity, 0, "cost-root", plan.Frozen,
                plan.Primary, plan.Closure.Workspace, plan.Test.Lease, plan.Costs.Resources);
            Check(primaryOnly.Status == DRPGPrimaryImpactCommitStatus.Rejected,
                "Primary-only commit stole a complete root reservation.");
            AssertCostLiveUnchanged(plan);
            Check(plan.Commit().Status == DRPGPrimaryImpactCommitStatus.Committed, "Correct root could not commit after rejected bypasses.");
        }
    }

    private static void AssertCostLiveUnchanged(CostPlan plan) => Check(plan.Test.Battle.Player.CurrentMp == 30f &&
        plan.Test.Battle.Player.CurrentPosture == 100f && plan.Test.Battle.Player.CurrentHp == 100f &&
        plan.Test.Battle.Enemy.CurrentHp == 120f && plan.Test.ProgressValue == 10f &&
        plan.Test.Weapon.CurrentDurability == 100 && plan.Test.Battle.State.BattleRevision == 0,
        "An uncommitted reservation changed live battle values.");

    internal static void ActionCostsRejectDirectCostCommit()
    {
        using (var plan = new CostPlan())
        {
            var altered = plan.Closure.Workspace.Fork();
            altered.For(new ParticipantId(plan.Test.Battle.Player.RuntimeId)).SetPosture(90f);
            Check(!plan.Costs.CommitValidatedJournal(plan.Test.Battle.State, plan.Test.Lease, altered).Succeeded,
                "Calling the cost helper directly bypassed complete-ledger outcome validation.");
            AssertCostLiveUnchanged(plan);
            Check(plan.Commit().Status == DRPGPrimaryImpactCommitStatus.Committed, "Rejected direct cost commit poisoned the real ledger.");
        }
    }

    internal static void ActionCostsRequireCapturedActorProgressOwner()
    {
        using (var plan = new CostPlan())
        {
            Check(plan.Test.Pipeline.TryRegister("workspace_enemy", new DRPGProgressResourceDefinition("focus",
                DRPGProgressScope.Battle, 20f, 10f), out _, out string error), error);
            var alias = new BattleParticipantRuntime(plan.Test.Battle.Player.RuntimeId,
                new BattleParticipantSpec(plan.Test.Battle.Enemy.Profile, DRPGBattleSide.Player,
                    DRPGBattleRosterRole.Protagonist, 0));
            var spec = DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(alias.RuntimeId,
                plan.Test.Battle.Enemy.RuntimeId, plan.PrimarySkill), alias);
            Check(DRPGActionCostReservation.TryReserve(plan.Test.Identity, spec, alias, plan.Test.Pipeline,
                out var costs, out error), error);
            Check(!costs.TryStageBeforeResolution(plan.Test.Battle.CreateWorkspace(), out _, out _),
                "A matching runtime ID spent a different captured participant's progress resource.");
            Check(costs.Cancel(), "Foreign owner rejection poisoned the reservation lifecycle.");
            AssertCostLiveUnchanged(plan);
        }
    }

    internal static void ActionCostsRejectOwnerDriftAtCommit()
    {
        using (var plan = new CostPlan())
        {
            var profile = plan.Test.Battle.Player.Profile;
            string original = profile.CombatantId;
            try
            {
                SetCostField(profile, "combatantId", "workspace_enemy");
                Check(plan.Commit().Status == DRPGPrimaryImpactCommitStatus.Rejected,
                    "An actor's changed stable identity consumed the original owner's captured progress.");
                AssertCostLiveUnchanged(plan);
            }
            finally { SetCostField(profile, "combatantId", original); }
            Check(plan.Commit().Status == DRPGPrimaryImpactCommitStatus.Committed,
                "Owner drift rejection invalidated an otherwise unchanged original draft.");
        }
    }

    private static void SetCostField(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static DRPGSkillProgressCostDefinition MakeProgressCost(float amount)
    {
        var cost = new DRPGSkillProgressCostDefinition(null, amount);
        SetCostField(cost, "resourceId", "focus");
        return cost;
    }

    private sealed class CostCounterSource : IDRPGImmediateReactionSource
    {
        private readonly DRPGResolvedActionSpec spec;
        internal CostCounterSource(DRPGResolvedActionSpec spec) { this.spec = spec; }
        public ParticipantId Participant => new ParticipantId(spec.ActorRuntimeId);
        public string SourceId => "cost-counter-source";
        public int HomeSlot => 0;
        public bool IsPassive => true;
        public void Collect(DRPGImmediateReactionContext context, ICollection<DRPGImmediateReactionRequest> requests)
        {
            if (context.Depth != 0) return;
            requests.Add(new DRPGImmediateReactionRequest(context.Identity, context.EventId, "cost-counter",
                DRPGImmediateReactionCategory.Counterattack, 0, spec, DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f, 7f)));
        }
    }

    private sealed class CostPlan : IDisposable
    {
        private readonly DRPGSkillSO counterSkill;
        internal CostPlan()
        {
            try
            {
            Test = new Fixture();
            PrimarySkill = CreateDamageSkill("cost-primary", 20f);
            counterSkill = CreateDamageSkill("cost-counter", 5f);
            SetCostField(PrimarySkill, "manaCost", 5f);
            SetCostField(PrimarySkill, "postureCost", 10f);
            SetCostField(PrimarySkill, "actionProgressCost", MakeProgressCost(3f));
            var actor = Test.Battle.Player; var enemy = Test.Battle.Enemy;
            Spec = DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(actor.RuntimeId, enemy.RuntimeId, PrimarySkill), actor);
            Frozen = Freeze();
            Check(DRPGActionCostReservation.TryReserve(Test.Identity, Spec, actor, Test.Pipeline, out var costs, out string error),
                "Root cost reservation failed: " + error);
            Costs = costs;
            Input = Test.Battle.CreateWorkspace();
            Check(Costs.TryStageBeforeResolution(Input, out var workspace, out error), "Pre-resolution cost staging failed: " + error);
            Check(workspace.ActionResources.TryStageWeaponUse(Test.Identity, "primary", Test.Weapon.InstanceId, 1, out error), error);
            var resolver = new DRPGActionResolver();
            var participants = new[] { actor, enemy };
            Primary = resolver.ResolveIntoWorkspace(Spec, actor, enemy, participants, workspace,
                DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f));
            var counter = DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(enemy.RuntimeId, actor.RuntimeId, counterSkill), enemy);
            Closure = new DRPGImmediateReactionLedgerBuilder().Build(Test.Identity,
                "action-" + Test.Identity.BattleGeneration + "-1-1-impact-0", Primary, workspace, participants,
                new[] { new CostCounterSource(counter) }, resolver);
            Check(Closure.Succeeded, Closure.Error);
            Store = new DRPGBattleActionResolutionLedgerStore(Test.Battle.State, Test.Battle.Arbiter.Trace);
            }
            catch { Dispose(); throw; }
        }
        internal Fixture Test { get; }
        internal DRPGSkillSO PrimarySkill { get; }
        internal DRPGResolvedActionSpec Spec { get; }
        internal DRPGActionCostReservation Costs { get; }
        internal DRPGBattleMutationWorkspace Input { get; }
        internal DRPGBasicAttackFrozenSnapshot Frozen { get; }
        internal DRPGWorkspaceResolutionResult Primary { get; }
        internal DRPGImmediateReactionClosureResult Closure { get; }
        internal DRPGBattleActionResolutionLedgerStore Store { get; }
        internal DRPGBasicAttackFrozenSnapshot Freeze() => new DRPGBasicAttackFrozenSnapshot(
            new ActorTurnToken(Test.Identity.BattleGeneration, 1, new ParticipantId(Test.Battle.Player.RuntimeId)),
            new TargetSelectionToken(Test.Identity.BattleGeneration, 1, 1, new ParticipantId(Test.Battle.Player.RuntimeId)),
            new ParticipantId(Test.Battle.Enemy.RuntimeId), PrimarySkill).WithIdentity(Test.Identity);
        internal DRPGPrimaryImpactCommitResult Commit() => Store.TryCommitActionResolution("cost-root", Frozen, Primary, Closure, Test.Lease);
        public void Dispose() { Costs?.Cancel(); Test?.Dispose(); if (PrimarySkill != null) UnityEngine.Object.DestroyImmediate(PrimarySkill); if (counterSkill != null) UnityEngine.Object.DestroyImmediate(counterSkill); }
    }
}
#endif
