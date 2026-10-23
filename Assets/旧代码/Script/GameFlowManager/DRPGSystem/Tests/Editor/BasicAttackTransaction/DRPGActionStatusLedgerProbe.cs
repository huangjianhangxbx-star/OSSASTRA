#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

internal static partial class DRPGActionResourceJournalProbe
{
    internal static void RunStatusLedgerChecks()
    {
        StatusReactionsCommitWithDamageAndResourcesOnce();
        StatusEffectOrderIsPreserved();
        StatusClosureFailureDiscardsAllEffects();
        StatusAfterLethalIsSkippedWithoutRemovingActor();
        StatusFactsRejectForgeryAndUnaccountedMutation();
        StatusFactsFreezeDefinitionAndIconMetadata();
        StatusResourcePrepareFailureRemainsAtomic();
        StatusReactionsUseTheSamePlayerAndEnemyPath();
        StatusEvadeProducesNoStatusMutation();
    }

    internal static void StatusReactionsCommitWithDamageAndResourcesOnce()
    {
        using (var plan = new StatusPlan())
        {
            Check(plan.Closure.Succeeded, "Status reaction closure failed: " + plan.Closure.Error);
            Check(plan.Test.Battle.Enemy.CurrentHp == 120f && plan.Test.Battle.Enemy.Statuses.Count == 0,
                "Building a status reaction changed live state.");
            var result = plan.Commit();
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed, "Status ledger failed: " + result.Error);
            Check(plan.Test.Battle.Enemy.CurrentHp == 120f && plan.Test.Battle.Enemy.CurrentPosture == 40f &&
                plan.Test.Battle.Enemy.Statuses.Snapshot()[0].Stacks == 1 &&
                plan.Test.ProgressValue == 7f && plan.Test.Weapon.CurrentDurability == 99 && plan.Test.Battle.State.BattleRevision == 1,
                "Status, ordered damage and resource costs did not commit together.");
            Check(ReferenceEquals(result.Receipt, plan.Commit().Receipt) && plan.Test.Battle.Enemy.CurrentHp == 120f &&
                plan.Test.Battle.Enemy.CurrentPosture == 40f &&
                plan.Test.Battle.Enemy.Statuses.Snapshot()[0].Stacks == 1 && plan.Test.Battle.State.BattleRevision == 1,
                "Replaying a status reaction duplicated its stacks, damage or cost.");
        }
    }

    internal static void StatusEffectOrderIsPreserved()
    {
        using (var plan = new StatusPlan(statusFirst: false))
        {
            Check(plan.Closure.Succeeded, "After-hit status closure failed: " + plan.Closure.Error);
            var result = plan.Commit();
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && plan.Test.Battle.Enemy.CurrentHp == 120f &&
                plan.Test.Battle.Enemy.CurrentPosture == 45f &&
                result.Receipt.Ledger.Reactions[0].Batch.Hits[0].PostureAfter == 55f &&
                result.Receipt.Ledger.Reactions[1].Batch.Hits[0].PostureAfter == 45f,
                "A status applied after a hit retroactively changed that hit, or was absent from the next reaction: " + result.Error);
        }
    }

    internal static void StatusClosureFailureDiscardsAllEffects()
    {
        using (var plan = new StatusPlan(fault: true))
        {
            Check(!plan.Closure.Succeeded && !string.IsNullOrEmpty(plan.Closure.Error) &&
                plan.Commit().Status == DRPGPrimaryImpactCommitStatus.Rejected &&
                plan.Test.Battle.Enemy.CurrentHp == 120f && plan.Test.Battle.Enemy.Statuses.Count == 0 &&
                plan.Test.ProgressValue == 10f && plan.Test.Weapon.CurrentDurability == 100 && plan.Test.Battle.State.BattleRevision == 0,
                "A later reaction failure partially committed status, HP or resources. Error=" + plan.Closure.Error);
        }
    }

    internal static void StatusAfterLethalIsSkippedWithoutRemovingActor()
    {
        using (var plan = new StatusPlan(statusFirst: false, lethal: true))
        {
            Check(plan.Closure.Succeeded, "Lethal status closure failed: " + plan.Closure.Error);
            var result = plan.Commit();
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && plan.Test.Battle.Enemy.CurrentHp == 0f &&
                plan.Test.Battle.Enemy.Statuses.Count == 0 && plan.Test.Battle.Enemy.IsDeployed &&
                result.Receipt.Ledger.Reactions[0].Batch.Effects[result.Receipt.Ledger.Reactions[0].Batch.Effects.Count - 1].Kind ==
                    DRPGActionEffectKind.ApplyStatus &&
                result.Receipt.Ledger.Reactions[0].Batch.Effects[result.Receipt.Ledger.Reactions[0].Batch.Effects.Count - 1].State ==
                    DRPGActionEffectResultState.Skipped &&
                result.Receipt.Ledger.Reactions[1].State == DRPGImmediateReactionState.SkippedIneligible,
                "Lethal status handling applied to a dead target, removed its slot, or allowed the next normal reaction: " +
                result.Error + " HP=" + plan.Test.Battle.Enemy.CurrentHp +
                " posture=" + plan.Test.Battle.Enemy.CurrentPosture +
                " statuses=" + plan.Test.Battle.Enemy.Statuses.Count +
                " deployed=" + plan.Test.Battle.Enemy.IsDeployed +
                " finalEffect=" + result.Receipt.Ledger.Reactions[0].Batch.Effects[result.Receipt.Ledger.Reactions[0].Batch.Effects.Count - 1].State +
                " reaction1=" + result.Receipt.Ledger.Reactions[1].State);
        }
    }

    internal static void StatusFactsRejectForgeryAndUnaccountedMutation()
    {
        for (int scenario = 0; scenario < 8; scenario++)
        using (var plan = new StatusPlan())
        {
            Check(plan.Closure.Succeeded, plan.Closure.Error);
            var workspace = plan.Closure.Workspace.Fork();
            var entries = new List<DRPGReactionLedgerEntry>(plan.Closure.Entries);
            var entry = entries[0];
            var effects = new List<DRPGActionEffectResult>(entry.Batch.Effects);
            var original = effects[0].StatusChange;
            if (scenario == 0) workspace.For(original.Target).RemoveStatus(original.Mutation.StatusId);
            if (scenario == 1) workspace.For(original.Target).ApplyStatus(original.Mutation.Spec);
            if (scenario == 2) effects.RemoveAt(0);
            if (scenario == 3)
                effects[0] = new DRPGActionEffectResult(DRPGActionEffectKind.ApplyStatus, DRPGActionEffectResultState.Applied,
                    entry.SourceParticipant.Value, original.Target.Value, 1, 1, 2, original.Mutation.StatusId);
            if (scenario == 4)
                effects[0] = new DRPGActionEffectResult(entry.SourceParticipant.Value, new DRPGActionStatusChange(original.Target,
                    original.Mutation, original.After, original.After));
            if (scenario == 5)
                effects[0] = new DRPGActionEffectResult(entry.SourceParticipant.Value, new DRPGActionStatusChange(original.Target,
                    original.Mutation, original.Before, new[] { new DRPGStatusRuntimeSnapshot(original.Mutation.Spec, 2, 2) }));
            if (scenario == 6)
                effects[0] = new DRPGActionEffectResult(entry.SourceParticipant.Value, new DRPGActionStatusChange(original.Target,
                    new DRPGStatusMutation(DRPGStatusMutationKind.Apply, original.Mutation.Spec, original.Mutation.StatusId, 1, 99),
                    original.Before, original.After));
            if (scenario == 7) effects.Reverse();
            entries[0] = new DRPGReactionLedgerEntry(entry.Identity, entry.ParentEventId, entry.EventId, entry.ReactionId,
                entry.SourceParticipant, entry.SourceId, entry.Category, entry.Depth, entry.InsertionOrdinal, entry.State,
                new DRPGResolvedActionBatch(entry.Batch.Hits, effects));
            var changed = new DRPGImmediateReactionClosureResult(workspace, entries, null);
            var result = plan.Store.TryCommitActionResolution("status-root", plan.Frozen, plan.Primary, changed, plan.Test.Lease);
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Rejected && plan.Test.Battle.Enemy.CurrentHp == 120f &&
                plan.Test.Battle.Enemy.Statuses.Count == 0 && plan.Test.ProgressValue == 10f && plan.Test.Weapon.CurrentDurability == 100 &&
                plan.Test.Battle.State.BattleRevision == 0, "Forged status draft partially committed: " + scenario);
            Check(plan.Commit().Status == DRPGPrimaryImpactCommitStatus.Committed, "Invalid draft poisoned the valid status request.");
            Check(plan.Store.TryCommitActionResolution("status-root", plan.Frozen, plan.Primary, changed, plan.Test.Lease).Status ==
                DRPGPrimaryImpactCommitStatus.FingerprintConflict, "Changed status facts replayed as the original request: " + scenario);
        }
    }

    internal static void StatusFactsFreezeDefinitionAndIconMetadata()
    {
        using (var plan = new StatusPlan(icon: true))
        {
            Check(plan.Closure.Succeeded, plan.Closure.Error);
            var change = plan.Closure.Entries[0].Batch.Effects[0].StatusChange;
            var key = new DRPGPrimaryImpactKey(plan.Test.Identity, 0);
            var before = DRPGActionResolutionFingerprint.ComputeComplete(key, "status-root", plan.Frozen, plan.Primary.Batch.Hits[0],
                plan.Primary.Batch.Effects, plan.Closure.Entries, plan.Closure.Workspace.BuildValidatedJournal());
            StatusSet(plan.Definition, "incomingDamageMultiplier", 9f);
            StatusSet(plan.Definition, "durationTurns", 99);
            plan.Definition.Icon.name = "changed-after-lock";
            var after = DRPGActionResolutionFingerprint.ComputeComplete(key, "status-root", plan.Frozen, plan.Primary.Batch.Hits[0],
                plan.Primary.Batch.Effects, plan.Closure.Entries, plan.Closure.Workspace.BuildValidatedJournal());
            Check(before == after, "Frozen status fingerprint re-read mutable authored/icon metadata.");
            var result = plan.Commit();
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && plan.Test.Battle.Enemy.CurrentHp == 120f &&
                plan.Test.Battle.Enemy.CurrentPosture == 40f &&
                plan.Test.Battle.Enemy.Statuses.Snapshot()[0].RemainingTurns == 2,
                "Later authored changes altered the locked status request.");
            var fork = plan.Closure.Workspace.Fork();
            fork.For(change.Target).RemoveStatus(change.Mutation.StatusId);
            Check(change.Before.Count == 0 && change.After[0].Stacks == 1 && change.After[0].Spec.IncomingDamageMultiplier == 2f &&
                ReferenceEquals(result.Receipt.Ledger.Events[1].Effects[0].StatusChange, change),
                "Committed scoped events did not retain their original immutable status snapshots.");
        }
    }

    internal static void StatusResourcePrepareFailureRemainsAtomic()
    {
        using (var plan = new StatusPlan())
        {
            Check(plan.Closure.Succeeded, plan.Closure.Error);
            Check(plan.Test.Pipeline.TryGain("workspace_player", "focus", DRPGProgressScope.Battle, 1f, out _, out _),
                "Resource drift setup failed.");
            Check(plan.Commit().Status == DRPGPrimaryImpactCommitStatus.Rejected && plan.Test.Battle.Enemy.CurrentHp == 120f &&
                plan.Test.Battle.Enemy.Statuses.Count == 0 && plan.Test.ProgressValue == 11f && plan.Test.Weapon.CurrentDurability == 100 &&
                plan.Test.Battle.State.BattleRevision == 0, "Late resource preparation failure applied prepared statuses or damage.");
        }
    }

    internal static void StatusReactionsUseTheSamePlayerAndEnemyPath()
    {
        using (var plan = new StatusPlan(enemyReacts: true))
        {
            Check(plan.Closure.Succeeded, plan.Closure.Error);
            var result = plan.Commit();
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && plan.Test.Battle.Enemy.CurrentHp == 120f &&
                plan.Test.Battle.Enemy.CurrentPosture == 60f && plan.Test.Battle.Enemy.Statuses.Count == 0 &&
                plan.Test.Battle.Player.CurrentHp == 100f && plan.Test.Battle.Player.CurrentPosture == 80f &&
                plan.Test.Battle.Player.Statuses.Snapshot()[0].Spec.IncomingDamageMultiplier == 2f &&
                result.Receipt.Ledger.Events[1].Source.Value == plan.Test.Battle.Enemy.RuntimeId &&
                result.Receipt.Ledger.Events[1].Effects[0].StatusChange.Target.Value == plan.Test.Battle.Player.RuntimeId &&
                plan.Test.Battle.State.BattleRevision == 1, "Enemy-to-player status reaction did not use the same ordered transaction: " + result.Error);
        }
    }

    internal static void StatusEvadeProducesNoStatusMutation()
    {
        using (var plan = new StatusPlan(evade: true))
        {
            Check(plan.Closure.Succeeded, plan.Closure.Error);
            var result = plan.Commit();
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && plan.Test.Battle.Enemy.CurrentHp == 120f &&
                plan.Test.Battle.Enemy.CurrentPosture == 55f &&
                plan.Test.Battle.Enemy.Statuses.Count == 0 && result.Receipt.Ledger.Reactions[0].Batch.Effects.Count == 0 &&
                result.Receipt.Ledger.Reactions[0].Batch.Hits[0].Outcome == DRPGHitOutcome.Evaded &&
                result.Receipt.Ledger.Reactions[0].Batch.Hits[0].HpBefore == 120f &&
                result.Receipt.Ledger.Reactions[0].Batch.Hits[0].HpAfter == 120f,
                "An evaded reaction applied an attached status or altered the next hit: " + result.Error);
        }
    }

    private sealed class StatusPlan : IDisposable
    {
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        internal StatusPlan(bool statusFirst = true, bool fault = false, bool lethal = false, bool icon = false,
            bool enemyReacts = false, bool evade = false)
        {
            Test = new Fixture();
            var primarySkill = CreateDamageSkill("status-primary", 20f);
            var firstSkill = CreateDamageSkill("status-reaction", lethal ? 200f : 5f);
            var secondSkill = CreateDamageSkill("status-followup", fault ? float.NaN : 5f);
            owned.Add(primarySkill); owned.Add(firstSkill); owned.Add(secondSkill);
            Definition = ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>();
            owned.Add(Definition);
            if (icon)
            {
                var texture = new Texture2D(1, 1); owned.Add(texture);
                var sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero); owned.Add(sprite);
                sprite.name = "frozen-status-icon";
                StatusSet(Definition, "icon", sprite);
            }
            StatusSet(Definition, "statusId", "vulnerable");
            StatusSet(Definition, "incomingDamageMultiplier", 2f);
            StatusSet(Definition, "maxStacks", 3);
            StatusSet(Definition, "durationTurns", 2);
            var status = DRPGActionEffectDefinition.ApplyStatus(DRPGActionEffectTarget.PrimaryTarget, Definition);
            var damage = DRPGActionEffectDefinition.Damage(DRPGActionEffectTarget.PrimaryTarget, 1f, DRPGDamageTag.Slash);
            StatusSet(firstSkill, "effects", statusFirst ? new List<DRPGActionEffectDefinition> { status, damage } :
                new List<DRPGActionEffectDefinition> { damage, status });
            var actor = Test.Battle.Player; var target = Test.Battle.Enemy;
            var actorId = new ParticipantId(actor.RuntimeId); var targetId = new ParticipantId(target.RuntimeId);
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
            var sourceActor = enemyReacts ? target : actor;
            var sourceTarget = enemyReacts ? actor : target;
            Closure = new DRPGImmediateReactionLedgerBuilder().Build(Test.Identity,
                "action-" + Test.Identity.BattleGeneration + "-1-1-impact-0", Primary, workspace, participants,
                new[] { new StatusSource(new ParticipantId(sourceActor.RuntimeId), DRPGActionSpecResolver.Resolve(
                    DRPGBattleCommand.BasicAttack(sourceActor.RuntimeId, sourceTarget.RuntimeId, firstSkill), sourceActor),
                    DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(sourceActor.RuntimeId, sourceTarget.RuntimeId, secondSkill), sourceActor), evade) }, resolver);
            Store = new DRPGBattleActionResolutionLedgerStore(Test.Battle.State, Test.Battle.Arbiter.Trace);
        }
        internal Fixture Test { get; }
        internal DRPGStatusDefinitionSO Definition { get; }
        internal DRPGBasicAttackFrozenSnapshot Frozen { get; }
        internal DRPGWorkspaceResolutionResult Primary { get; }
        internal DRPGImmediateReactionClosureResult Closure { get; }
        internal DRPGBattleActionResolutionLedgerStore Store { get; }
        internal DRPGPrimaryImpactCommitResult Commit() => Store.TryCommitActionResolution("status-root", Frozen, Primary, Closure, Test.Lease);
        public void Dispose()
        {
            Test.Dispose();
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) UnityEngine.Object.DestroyImmediate(owned[i]);
        }
    }

    private sealed class StatusSource : IDRPGImmediateReactionSource
    {
        private readonly DRPGResolvedActionSpec first, second;
        private readonly bool evade;
        internal StatusSource(ParticipantId participant, DRPGResolvedActionSpec first, DRPGResolvedActionSpec second, bool evade)
        { Participant = participant; this.first = first; this.second = second; this.evade = evade; }
        public string SourceId => "status-source";
        public ParticipantId Participant { get; }
        public int HomeSlot => 0;
        public bool IsPassive => false;
        public void Collect(DRPGImmediateReactionContext context, ICollection<DRPGImmediateReactionRequest> requests)
        {
            if (context.Depth != 0) return;
            requests.Add(new DRPGImmediateReactionRequest(context.Identity, context.EventId, "status-first",
                DRPGImmediateReactionCategory.OtherImmediate, 1, first, DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f),
                evade ? DRPGHitOutcome.Evaded : DRPGHitOutcome.Hit));
            requests.Add(new DRPGImmediateReactionRequest(context.Identity, context.EventId, "status-second",
                DRPGImmediateReactionCategory.OtherImmediate, 0, second, DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f)));
        }
    }
    private static void StatusSet(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) throw new MissingFieldException(target.GetType().Name, name);
        field.SetValue(target, value);
    }
}
#endif
