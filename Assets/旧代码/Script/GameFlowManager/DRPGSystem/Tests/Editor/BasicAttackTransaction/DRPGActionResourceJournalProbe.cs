#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;

internal static partial class DRPGActionResourceJournalProbe
{
    internal static void RunAll()
    {
        ResourceJournalIsDetachedAndAtomic();
        ResourceJournalRejectsDriftAndWrongOwner();
        ResourceJournalForkAndDedupe();
        ResourceJournalRejectsInsufficientAndUnbound();
        PrimaryOnlyStoreRejectsUnaccountedResources();
        PreparedResourcesCannotBypassState();
        ResourceJournalLateFailureAndBreakBoundary();
        PrimaryAndReactionShareTheResourceJournal();
    }

    internal static void ResourceJournalIsDetachedAndAtomic()
    {
        using (var test = new Fixture())
        {
            var workspace = test.Stage();
            Check(test.ProgressValue == 10f && test.Weapon.CurrentDurability == 100 &&
                test.Battle.Player.CurrentMp == 30f && test.Battle.Enemy.CurrentHp == 120f &&
                !test.Uses.IsCompleted(DRPGBattleActionResourceWorkspace.WeaponActionKey(test.Identity, test.Weapon.InstanceId)),
                "Staging a resource journal modified real progress, durability, MP, HP or weapon completion.");
            workspace.For(new ParticipantId(test.Battle.Enemy.RuntimeId)).SetHitPoints(float.NaN);
            Check(!test.Battle.State.TryCommitJournal(test.Lease, workspace).Succeeded &&
                test.ProgressValue == 10f && test.Weapon.CurrentDurability == 100 &&
                test.Battle.Player.CurrentMp == 30f && test.Battle.Enemy.CurrentHp == 120f &&
                test.Battle.State.BattleRevision == 0,
                "Failed participant validation partially committed progress or weapon usage.");
            workspace = test.Stage();
            var result = test.Battle.State.TryCommitJournal(test.Lease, workspace);
            Check(result.Succeeded, result.Error);
            Check(test.ProgressValue == 7f && test.Weapon.CurrentDurability == 99 &&
                test.Battle.Player.CurrentMp == 25f && test.Battle.Enemy.CurrentHp == 90f &&
                test.Battle.State.BattleRevision == 1 &&
                test.Uses.IsCompleted(DRPGBattleActionResourceWorkspace.WeaponActionKey(test.Identity, test.Weapon.InstanceId)),
                "One matching State commit did not apply the real progress/weapon/HP/MP journal together.");
            Check(!test.Battle.State.TryCommitJournal(test.Lease, workspace).Succeeded &&
                test.ProgressValue == 7f && test.Weapon.CurrentDurability == 99 && test.Battle.State.BattleRevision == 1,
                "The same resource journal was charged twice.");
        }
    }

    internal static void ResourceJournalRejectsDriftAndWrongOwner()
    {
        for (int scenario = 0; scenario < 6; scenario++)
        {
            using (var test = new Fixture())
            {
                var workspace = test.Stage(scenario == 3 ? new ActionIdentity(test.Identity.BattleGeneration, 2, 2) : test.Identity);
                if (scenario == 0)
                    Check(test.Pipeline.TryGain("workspace_player", "focus", DRPGProgressScope.Battle, 1f, out _, out _), "Drift setup failed.");
                if (scenario == 1) test.Weapon.RestoreDurability(100, 98);
                if (scenario == 2)
                    Check(test.Uses.TryRecordCommittedUse("unrelated", DRPGWeaponUseKind.FirearmShot).Success, "Use ledger drift setup failed.");
                if (scenario == 4)
                {
                    Check(test.Pipeline.TryGain("workspace_player", "focus", DRPGProgressScope.Battle, 1f, out _, out _) &&
                        test.Pipeline.TrySpend("workspace_player", "focus", DRPGProgressScope.Battle, 1f, out _, out _), "Progress ABA setup failed.");
                }
                if (scenario == 5)
                {
                    test.Weapon.RestoreDurability(100, 98);
                    test.Weapon.RestoreDurability(100, 100);
                }
                var result = test.Battle.State.TryCommitJournal(test.Lease, workspace);
                Check(!result.Succeeded && test.Battle.State.BattleRevision == 0 &&
                    test.Battle.Player.CurrentMp == 30f && test.Battle.Enemy.CurrentHp == 120f &&
                    test.ProgressValue == (scenario == 0 ? 11f : 10f) &&
                    test.Weapon.CurrentDurability == (scenario == 1 ? 98 : 100),
                    "Resource drift or another Root Action was accepted, or unrelated live changes were rolled back. Scenario=" + scenario);
            }
        }
    }

    internal static void ResourceJournalForkAndDedupe()
    {
        using (var test = new Fixture())
        {
            var workspace = test.Stage();
            var fork = workspace.Fork();
            Check(fork.ActionResources.TryStageProgress(test.Identity, test.Spend(), out _, out string error), error);
            Check(fork.ActionResources.TryStageWeaponUse(test.Identity, "primary", test.Weapon.InstanceId, 1, out error), error);
            Check(!fork.ActionResources.TryStageWeaponUse(test.Identity, "primary", test.Weapon.InstanceId, 2, out _),
                "A duplicate weapon event with a changed cost was accepted.");
            Check(!fork.ActionResources.TryStageProgress(test.Identity, test.Spend(4f), out _, out _),
                "A duplicate progress request with a changed amount was accepted.");
            Check(fork.ActionResources.TryStageProgress(test.Identity,
                DRPGProgressMutationRequest.Gain("rule", "reaction", 0, "workspace_player", "focus", DRPGProgressScope.Battle, 2f), out _, out error), error);
            Check(test.Battle.State.TryCommitJournal(test.Lease, fork).Succeeded && test.ProgressValue == 9f &&
                test.Weapon.CurrentDurability == 99, "Workspace fork lost pending writes or duplicated a use.");
            Check(workspace.ActionResources.TryGetProgress("workspace_player", "focus", out var original) && original.CurrentValue == 7f,
                "Extending a fork changed the source workspace's pending progress.");
        }
    }

    internal static void ResourceJournalRejectsInsufficientAndUnbound()
    {
        using (var test = new Fixture())
        {
            var workspace = test.Battle.CreateWorkspace();
            Check(!workspace.ActionResources.TryStageProgress(test.Identity, test.Spend(11f), out _, out _),
                "Insufficient progress was accepted.");
            Check(!workspace.ActionResources.TryStageWeaponUse(test.Identity, "too-many", test.Weapon.InstanceId, 101, out _),
                "Insufficient durability was accepted.");
            Check(!workspace.ActionResources.TryStageWeaponUse(test.Identity, "missing", "foreign-weapon", 1, out _),
                "A weapon outside the State resource binding was accepted.");
            Check(!workspace.ActionResources.TryStageProgress(test.Identity,
                DRPGProgressMutationRequest.Register("rule", "foreign-owner", 0, "outside-battle",
                    new DRPGProgressResourceDefinition("focus", DRPGProgressScope.Battle, 20f, 10f)), out _, out _),
                "A progress owner outside the captured participants was accepted.");
            Check(test.ProgressValue == 10f && test.Weapon.CurrentDurability == 100 && test.Battle.State.BattleRevision == 0,
                "A rejected resource operation leaked live mutation.");
        }
    }

    internal static void PrimaryOnlyStoreRejectsUnaccountedResources()
    {
        for (int scenario = 0; scenario < 2; scenario++)
        using (var test = new Fixture())
        {
            var skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
            try
            {
                typeof(DRPGSkillSO).GetField("skillId", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(skill, "primary-resource-boundary");
                typeof(DRPGSkillSO).GetField("power", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(skill, 20f);
                typeof(DRPGSkillSO).GetField("effects", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(skill,
                    new System.Collections.Generic.List<DRPGActionEffectDefinition> {
                        DRPGActionEffectDefinition.Damage(DRPGActionEffectTarget.PrimaryTarget, 1f, DRPGDamageTag.Slash) });
                var actor = new ParticipantId(test.Battle.Player.RuntimeId);
                var target = new ParticipantId(test.Battle.Enemy.RuntimeId);
                var turn = new ActorTurnToken(test.Identity.BattleGeneration, 1, actor);
                var selection = new TargetSelectionToken(test.Identity.BattleGeneration, 1, 1, actor);
                var frozen = new DRPGBasicAttackFrozenSnapshot(turn, selection, target, skill).WithIdentity(test.Identity);
                var spec = DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(actor.Value, target.Value, skill), test.Battle.Player);
                var workspace = test.Battle.CreateWorkspace();
                var resolved = new DRPGActionResolver().ResolveIntoWorkspace(spec, test.Battle.Player, test.Battle.Enemy,
                    new[] { test.Battle.Player, test.Battle.Enemy }, workspace, DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f));
                Check(resolved.Status == DRPGWorkspaceResolutionStatus.Succeeded, resolved.Error);
                if (scenario == 1)
                    Check(workspace.ActionResources.TryStageProgress(test.Identity, test.Spend(), out _, out string error), error);
                var result = new DRPGBattleActionResolutionLedgerStore(test.Battle.State, test.Battle.Arbiter.Trace)
                    .TryCommitPrimaryImpact(test.Identity, 0, "locked-primary", frozen, resolved, workspace, test.Lease);
                if (scenario == 0)
                {
                    Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && test.ProgressValue == 10f &&
                        test.Battle.Enemy.CurrentHp == 120f && test.Battle.Enemy.CurrentPosture == 60f &&
                        test.Battle.State.BattleRevision == 1,
                        "The primary boundary test's valid control did not commit: " + result.Error);
                    continue;
                }
                Check(result.Status == DRPGPrimaryImpactCommitStatus.Rejected && test.ProgressValue == 10f &&
                    test.Battle.Enemy.CurrentHp == 120f && test.Battle.State.BattleRevision == 0,
                    "The primary-only store committed progress that is absent from its request fingerprint and receipt.");
            }
            finally { UnityEngine.Object.DestroyImmediate(skill); }
        }
    }

    internal static void PreparedResourcesCannotBypassState()
    {
        using (var test = new Fixture())
        {
            var workspace = test.Stage();
            var prepared = test.Bindings.Prepare(workspace.BuildValidatedJournal().ActionResources, test.Lease.Owner);
            bool rejected = false;
            try { prepared.Apply(); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected && test.ProgressValue == 10f && test.Weapon.CurrentDurability == 100 &&
                test.Battle.State.BattleRevision == 0, "A prepared resource commit could apply outside its State capability.");
        }
    }

    internal static void ResourceJournalLateFailureAndBreakBoundary()
    {
        using (var test = new Fixture())
        {
            string key = DRPGBattleActionResourceWorkspace.WeaponActionKey(test.Identity, test.Weapon.InstanceId);
            Check(test.Uses.TryRecordCommittedUse(key, DRPGWeaponUseKind.MeleeCommittedSwing).Success &&
                test.Uses.CompleteAction(key, test.Weapon).Success, "Duplicate completion setup failed.");
            var workspace = test.Stage();
            Check(!test.Battle.State.TryCommitJournal(test.Lease, workspace).Succeeded && test.ProgressValue == 10f &&
                test.Weapon.CurrentDurability == 99 && test.Battle.Enemy.CurrentHp == 120f && test.Battle.Player.CurrentMp == 30f &&
                test.Battle.State.BattleRevision == 0,
                "A late weapon completion failure applied the already-prepared progress or participant values.");
        }
        using (var test = new Fixture())
        {
            var workspace = test.Battle.CreateWorkspace();
            Check(workspace.ActionResources.TryStageWeaponUse(test.Identity, "last-use", test.Weapon.InstanceId, 100, out string error), error);
            Check(test.Battle.State.TryCommitJournal(test.Lease, workspace).Succeeded && test.Weapon.CurrentDurability == 0,
                "A signature weapon could not reach zero through the atomic journal.");
        }
        using (var test = new Fixture(ordinary: true))
        {
            var workspace = test.Battle.CreateWorkspace();
            int before = test.Weapon.CurrentDurability;
            Check(!workspace.ActionResources.TryStageWeaponUse(test.Identity, "last-use", test.Weapon.InstanceId, before, out _) &&
                test.Weapon.CurrentDurability == before && test.Battle.State.BattleRevision == 0,
                "Ordinary weapon break was partially accepted without fragment/equipment reconciliation.");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    internal static void PrimaryAndReactionShareTheResourceJournal()
    {
        for (int scenario = 0; scenario < 2; scenario++)
        using (var test = new Fixture())
        {
            var primarySkill = CreateDamageSkill("atomic-primary", 20f);
            var reactionSkill = CreateDamageSkill("atomic-reaction", 5f);
            try
            {
                var actor = test.Battle.Player;
                var target = test.Battle.Enemy;
                var participants = new[] { actor, target };
                var workspace = test.Battle.CreateWorkspace();
                workspace.For(new ParticipantId(actor.RuntimeId)).SetMana(25f);
                Check(workspace.ActionResources.TryStageProgress(test.Identity, test.Spend(), out _, out string error), error);
                Check(workspace.ActionResources.TryStageWeaponUse(test.Identity, "primary", test.Weapon.InstanceId, 1, out error), error);
                var resolver = new DRPGActionResolver();
                var primary = resolver.ResolveIntoWorkspace(
                    DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(actor.RuntimeId, target.RuntimeId, primarySkill), actor),
                    actor, target, participants, workspace, DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f));
                var reactionSpec = DRPGActionSpecResolver.Resolve(
                    DRPGBattleCommand.BasicAttack(actor.RuntimeId, target.RuntimeId, reactionSkill), actor);
                var closure = new DRPGImmediateReactionLedgerBuilder().Build(test.Identity, "primary-event", primary,
                    workspace, participants, new[] { new ResourceReactionSource(new ParticipantId(actor.RuntimeId), reactionSpec, scenario == 1) }, resolver);
                if (scenario == 1)
                {
                    Check(!closure.Succeeded && closure.Workspace == null && closure.Entries.Count == 0 &&
                        test.ProgressValue == 10f && test.Weapon.CurrentDurability == 100 && actor.CurrentMp == 30f &&
                        target.CurrentHp == 120f && test.Battle.State.BattleRevision == 0,
                        "An incomplete reaction closure consumed root resources or exposed a partially resolved battle.");
                    Check(workspace.ActionResources.TryGetProgress("workspace_player", "focus", out var pending) && pending.CurrentValue == 7f &&
                        workspace.For(new ParticipantId(target.RuntimeId)).HitPoints == 120f &&
                        workspace.For(new ParticipantId(target.RuntimeId)).Posture == 60f,
                        "A failed reaction altered its caller's detached primary/resource plan.");
                    continue;
                }
                Check(closure.Succeeded && closure.Entries.Count == 1, closure.Error);
                Check(test.Battle.State.TryCommitJournal(test.Lease, closure.Workspace).Succeeded &&
                    target.CurrentHp == 120f && target.CurrentPosture == 55f &&
                    actor.CurrentMp == 25f && test.ProgressValue == 7f &&
                    test.Weapon.CurrentDurability == 99 && test.Battle.State.BattleRevision == 1,
                    "Primary, immediate reaction and real resources did not share one State journal commit.");
            }
            finally { UnityEngine.Object.DestroyImmediate(primarySkill); UnityEngine.Object.DestroyImmediate(reactionSkill); }
        }
    }

    private static DRPGSkillSO CreateDamageSkill(string id, float power)
    {
        var skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        typeof(DRPGSkillSO).GetField("skillId", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(skill, id);
        typeof(DRPGSkillSO).GetField("power", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(skill, power);
        typeof(DRPGSkillSO).GetField("effects", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(skill,
            new System.Collections.Generic.List<DRPGActionEffectDefinition> {
                DRPGActionEffectDefinition.Damage(DRPGActionEffectTarget.PrimaryTarget, 1f, DRPGDamageTag.Slash) });
        return skill;
    }

    private sealed class ResourceReactionSource : IDRPGImmediateReactionSource
    {
        private readonly DRPGResolvedActionSpec spec;
        private readonly bool fault;
        internal ResourceReactionSource(ParticipantId participant, DRPGResolvedActionSpec spec, bool fault)
        { Participant = participant; this.spec = spec; this.fault = fault; }
        public string SourceId => "resource-closure";
        public ParticipantId Participant { get; }
        public int HomeSlot => 0;
        public bool IsPassive => true;
        public void Collect(DRPGImmediateReactionContext context, System.Collections.Generic.ICollection<DRPGImmediateReactionRequest> requests)
        {
            if (context.Depth > 0)
            {
                if (fault) throw new InvalidOperationException("Injected closure resource fault.");
                return;
            }
            requests.Add(new DRPGImmediateReactionRequest(context.Identity, context.EventId, "followup",
                DRPGImmediateReactionCategory.Pursuit, 0, spec, DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f)));
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly DRPGWeaponDefinitionSO definition;
        internal Fixture(bool ordinary = false, bool consumePlayer = false, DRPGResonanceExpeditionState resonance = null)
        {
            Progress = new DRPGBattleProgressLedger();
            Pipeline = new DRPGBattleProgressPipeline(null, new DRPGProgressStoreRouter(new IDRPGProgressStore[] { Progress }));
            Check(Pipeline.TryRegister("workspace_player", new DRPGProgressResourceDefinition("focus", DRPGProgressScope.Battle, 20f, 10f), out _, out string error), error);
            definition = ScriptableObject.CreateInstance<DRPGWeaponDefinitionSO>();
            Set(definition, "weaponId", "atomic_signature");
            Set(definition, "ownershipKind", ordinary ? DRPGWeaponOwnershipKind.General : DRPGWeaponOwnershipKind.Signature);
            Set(definition, "signatureCharacterId", ordinary ? null : "workspace_player");
            Set(definition, "baseDurability", 100);
            Set(definition, "rarity", ordinary ? DRPGEquipmentRarity.Blue : DRPGEquipmentRarity.RedGold);
            Weapon = DRPGWeaponInstance.Create("atomic-weapon", definition, new MinimumRandom());
            Uses = new DRPGWeaponUseLedger();
            Bindings = new DRPGBattleActionResourceBindings(Progress, Uses,
                new[] { new DRPGBattleWeaponResourceBinding(new ParticipantId(9801), Weapon) }, resonance: resonance);
            Battle = DRPGBattleStateMutationFixture.CreateWithActionResources(Bindings, consumePlayer);
            Identity = new ActionIdentity(Battle.BattleGeneration, 1, 1);
            Battle.Lease.Dispose();
            Check(Battle.Arbiter.TryAcquire(BattleTransactionIdentity.ForRootAction(Identity), out var lease, out error), error);
            Lease = lease;
        }
        internal DRPGBattleStateMutationFixture Battle { get; }
        internal DRPGBattleProgressLedger Progress { get; }
        internal DRPGBattleProgressPipeline Pipeline { get; }
        internal DRPGWeaponInstance Weapon { get; }
        internal DRPGWeaponUseLedger Uses { get; }
        internal DRPGBattleActionResourceBindings Bindings { get; }
        internal ActionIdentity Identity { get; }
        internal BattleRuleMutationLease Lease { get; }
        internal float ProgressValue { get { Check(Pipeline.TryGetSnapshot("workspace_player", "focus", DRPGProgressScope.Battle, out var snapshot), "Progress missing."); return snapshot.CurrentValue; } }
        internal DRPGProgressMutationRequest Spend(float amount = 3f) => DRPGProgressMutationRequest.Spend(
            "rule", "cost", 0, "workspace_player", "focus", DRPGProgressScope.Battle, amount);
        internal DRPGBattleMutationWorkspace Stage() => Stage(Identity);
        internal DRPGBattleMutationWorkspace Stage(ActionIdentity identity)
        {
            var workspace = Battle.CreateWorkspace();
            Check(workspace.ActionResources != null, "State did not capture the real action resource bindings.");
            workspace.For(new ParticipantId(Battle.Player.RuntimeId)).SetMana(25f);
            workspace.For(new ParticipantId(Battle.Enemy.RuntimeId)).SetHitPoints(90f);
            Check(workspace.ActionResources.TryStageProgress(identity, Spend(), out _, out string error), error);
            Check(workspace.ActionResources.TryStageWeaponUse(identity, "primary", Weapon.InstanceId, 1, out error), error);
            return workspace;
        }
        public void Dispose() { Lease.Dispose(); Battle.Dispose(); UnityEngine.Object.DestroyImmediate(definition); }
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }

    private sealed class MinimumRandom : IDRPGRandomSource
    {
        public float Range(float minimumInclusive, float maximumInclusive) => minimumInclusive;
        public int RangeInclusive(int minimumInclusive, int maximumInclusive) => minimumInclusive;
    }
}
#endif
