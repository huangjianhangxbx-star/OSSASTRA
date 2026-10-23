#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

internal static partial class DRPGActionResourceJournalProbe
{
    internal static void RunProductionRuleKernelChecks()
    {
        ProductionRuleKernelCommitsFrozenResonanceAndEvents();
        ProductionRuleKernelRejectsPartnerAndEquipAba();
        ProductionRuleKernelCancelsBeforeImpactWithoutMutation();
        ProductionRuleKernelUsesBothSidesWithoutLegacyReactions();
        ProductionRuleKernelPublishesOnlyImmutableFacts();
        ProductionRuleKernelIsolatesListenerFaultAndReentry();
        ProductionRuleKernelRejectsChangedHitAndLostLease();
        ProductionRuleKernelLateReactionFaultDiscardsCosts();
        ProductionRuleKernelRejectsUnmigratedLegacySources();
        ProductionRuleKernelFreezesReactionSourceMetadataAtLock();
    }

    internal static void ProductionRuleKernelCommitsFrozenResonanceAndEvents()
    {
        using (var test = new ProductionRulePlan())
        {
            using (var prepared = test.Prepare())
            {
                StatusSet(test.Node, "requiredBattleProgress", 20);
                StatusSet(test.ResonanceSource, "progressAmount", 10);
                var result = prepared.ResolveAndCommit(DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f));
                Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && test.Enemy.CurrentHp == 120f &&
                    test.Enemy.CurrentPosture == 60f &&
                    test.Player.CurrentMp == 28f && test.Expedition.GetProgress(test.Friend.Profile.CombatantId, "production-resonance") == 2 &&
                    test.Session.TransactionState.BattleRevision == 1 && test.LedgerEvents == 1 && test.LegacyEvents == 0 &&
                    result.Receipt.Ledger.MutationJournal.ActionResources.ResonanceMutations.Count == 1,
                    "Production kernel did not commit frozen resonance/costs/HP or bypassed the ledger event channel: " + result.Error);
                Check(ReferenceEquals(result.Receipt, prepared.ResolveAndCommit(DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f)).Receipt) &&
                    test.LedgerEvents == 1 && test.Session.CurrentActor == null && test.Session.TransactionArbiter.Owns(test.Lease, test.Lease.Owner),
                    "Repeated hit callback republished events, changed actor or released the root lease.");
            }
        }
    }

    internal static void ProductionRuleKernelRejectsPartnerAndEquipAba()
    {
        for (int scenario = 0; scenario < 3; scenario++)
        using (var test = new ProductionRulePlan())
        using (var prepared = test.Prepare())
        {
            if (scenario == 0)
            { test.Service.State.SetPartner("temporary"); test.Service.State.SetPartner(test.Friend.Profile.CombatantId); }
            if (scenario == 1)
            { Check(test.Service.TryEquipActiveSkill(test.Friend.Profile.CombatantId, null).Success, "Unequip setup failed.");
              Check(test.Service.TryEquipActiveSkill(test.Friend.Profile.CombatantId, test.ResonanceSkill).Success, "Re-equip setup failed."); }
            if (scenario == 2) { test.Expedition.ClearAll(); test.Expedition.AddProgress(test.Friend.Profile.CombatantId, "production-resonance", 1); }
            var result = prepared.ResolveAndCommit(DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f));
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Rejected && test.Enemy.CurrentHp == 120f && test.Player.CurrentMp == 30f &&
                test.Expedition.GetProgress(test.Friend.Profile.CombatantId, "production-resonance") == 1 && test.LedgerEvents == 0 &&
                test.Session.TransactionState.BattleRevision == 0, "Stale production resonance partially committed: " + scenario + " " + result.Error);
        }
    }

    internal static void ProductionRuleKernelCancelsBeforeImpactWithoutMutation()
    {
        using (var test = new ProductionRulePlan())
        {
            var prepared = test.Prepare(); prepared.Dispose();
            Check(prepared.ResolveAndCommit(DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f)).Status == DRPGPrimaryImpactCommitStatus.Rejected &&
                test.Player.CurrentMp == 30f && test.Enemy.CurrentHp == 120f && test.LedgerEvents == 0 &&
                test.Expedition.GetProgress(test.Friend.Profile.CombatantId, "production-resonance") == 1,
                "Disposed pre-hit rule action could mutate its root.");
        }
    }

    internal static void ProductionRuleKernelUsesBothSidesWithoutLegacyReactions()
    {
        using (var test = new ProductionRulePlan(enemyAttacker: true))
        using (var prepared = test.Prepare())
        {
            var result = prepared.ResolveAndCommit(DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f));
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && test.Player.CurrentHp == 100f &&
                test.Player.CurrentPosture == 80f && test.Enemy.CurrentMp == 18f &&
                test.LedgerEvents == 1 && test.LegacyEvents == 0 && test.Session.CurrentActor == null &&
                test.Expedition.GetProgress(test.Friend.Profile.CombatantId, "production-resonance") == 1,
                "Enemy root used a different rule path or charged protagonist-only resonance: " + result.Error);
        }
    }

    internal static void ProductionRuleKernelPublishesOnlyImmutableFacts()
    {
        using (var test = new ProductionRulePlan())
        using (var prepared = test.Prepare())
        {
            bool immutable = false;
            test.Session.ActionLedgerCommitted += facts => immutable = facts.GetType().GetProperty("MutationJournal",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public) == null;
            var result = prepared.ResolveAndCommit(DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f));
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && immutable,
                "Published action payload exposes a mutation journal containing live equipment/participant aliases.");
        }
    }

    internal static void ProductionRuleKernelIsolatesListenerFaultAndReentry()
    {
        using (var test = new ProductionRulePlan())
        using (var prepared = test.Prepare())
        {
            DRPGPrimaryImpactCommitResult reentrant = default;
            test.Session.ActionLedgerCommitted += _ => throw new InvalidOperationException("Injected observer fault.");
            test.Session.ActionLedgerCommitted += _ => reentrant = prepared.ResolveAndCommit(DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f));
            var result = prepared.ResolveAndCommit(DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f));
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && ReferenceEquals(result.Receipt, reentrant.Receipt) &&
                test.LedgerEvents == 1 && test.Enemy.CurrentHp == 120f && test.Enemy.CurrentPosture == 60f &&
                test.Session.TransactionState.BattleRevision == 1,
                "A faulty or reentrant observer changed the committed result, blocked siblings or repeated the hit.");
        }
    }

    internal static void ProductionRuleKernelRejectsChangedHitAndLostLease()
    {
        using (var test = new ProductionRulePlan())
        using (var prepared = test.Prepare())
        {
            Check(prepared.ResolveAndCommit(DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f)).Status == DRPGPrimaryImpactCommitStatus.Committed, "Control hit failed.");
            Check(prepared.ResolveAndCommit(DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 2f)).Status == DRPGPrimaryImpactCommitStatus.Rejected &&
                prepared.ResolveAndCommit(DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f), DRPGHitOutcome.Evaded).Status == DRPGPrimaryImpactCommitStatus.Rejected,
                "A duplicate hit changed its resolved context or outcome.");
            test.Lease.Dispose();
            Check(prepared.ResolveAndCommit(DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f)).Status == DRPGPrimaryImpactCommitStatus.Rejected &&
                test.Enemy.CurrentHp == 120f && test.Enemy.CurrentPosture == 60f && test.LedgerEvents == 1,
                "Expired root callback was accepted.");
        }
    }

    internal static void ProductionRuleKernelLateReactionFaultDiscardsCosts()
    {
        using (var test = new ProductionRulePlan())
        {
            var skill = CreateDamageSkill("production-shield", 1f);
            try
            {
                StatusSet(skill, "effects", new List<DRPGActionEffectDefinition> { DRPGActionEffectDefinition.Shield(DRPGActionEffectTarget.PrimaryTarget, 2f) });
                var spec = DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(test.Player.RuntimeId, test.Enemy.RuntimeId, skill), test.Player);
                using (var prepared = test.Prepare(new[] { new VitalSource(new ParticipantId(test.Player.RuntimeId), spec, null, true) }))
                {
                    var result = prepared.ResolveAndCommit(DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f));
                    Check(result.Status == DRPGPrimaryImpactCommitStatus.Rejected && result.Error.Contains("Injected after recovery") &&
                        test.Player.CurrentMp == 30f && test.Enemy.CurrentHp == 120f && test.Enemy.CurrentShield == 0f && test.LedgerEvents == 0 &&
                        test.Expedition.GetProgress(test.Friend.Profile.CombatantId, "production-resonance") == 1,
                        "Late production reaction failure committed root costs, damage, shield or resonance: " + result.Error);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(skill); }
        }
    }

    internal static void ProductionRuleKernelRejectsUnmigratedLegacySources()
    {
        var legacy = new UnmigratedProductionSource();
        using (var test = new ProductionRulePlan(legacy: new DRPGBattleReactionPipeline(new[] { legacy })))
        {
            Check(!test.Session.StandardBasicAttackRules.TryPrepare(test.Frozen, test.Spec, test.Lease, out _, out var error) &&
                error.Contains("explicit categorized frozen adapter") && legacy.Calls == 0 && test.Enemy.CurrentHp == 120f && test.Player.CurrentMp == 30f,
                "An unmigrated legacy source was silently ignored or executed by the new root: " + error);
        }
    }

    private sealed class UnmigratedProductionSource : IDRPGBattleReactionSource
    {
        internal int Calls;
        public string SourceId => "unmigrated-production";
        public void Collect(DRPGBattleReactionContext context, ICollection<DRPGBattleReactionRequest> requests) { Calls++; }
    }

    internal static void ProductionRuleKernelFreezesReactionSourceMetadataAtLock()
    {
        using (var test = new ProductionRulePlan())
        {
            var skill = CreateDamageSkill("locked-reaction-skill", 1f);
            try
            {
                StatusSet(skill, "effects", new List<DRPGActionEffectDefinition> { DRPGActionEffectDefinition.Shield(DRPGActionEffectTarget.PrimaryTarget, 2f) });
                var spec = DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(test.Player.RuntimeId, test.Enemy.RuntimeId, skill), test.Player);
                var source = new MutableProductionSource(new ParticipantId(test.Player.RuntimeId), spec);
                using (var prepared = test.Prepare(new[] { source }))
                {
                    source.SourceId = "changed-source"; source.HomeSlot = 9; source.IsPassive = true;
                    var result = prepared.ResolveAndCommit(DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f));
                    Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && result.Receipt.Ledger.Facts.Events.Count == 2 &&
                        result.Receipt.Ledger.Facts.Events[1].SourceId == "locked-source" && result.Receipt.Ledger.Facts.Events[1].SkillId == "locked-reaction-skill",
                        "Reaction source identity was reread after action lock: " + result.Error);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(skill); }
        }
    }

    private sealed class MutableProductionSource : IDRPGImmediateReactionSource
    {
        private readonly DRPGResolvedActionSpec spec;
        internal MutableProductionSource(ParticipantId participant, DRPGResolvedActionSpec spec) { Participant = participant; this.spec = spec; }
        public string SourceId { get; set; } = "locked-source";
        public ParticipantId Participant { get; }
        public int HomeSlot { get; set; }
        public bool IsPassive { get; set; }
        public void Collect(DRPGImmediateReactionContext context, ICollection<DRPGImmediateReactionRequest> requests)
        {
            if (context.Depth == 0) requests.Add(new DRPGImmediateReactionRequest(context.Identity, context.EventId, "locked-reaction",
                DRPGImmediateReactionCategory.OtherImmediate, 0, spec, DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f)));
        }
    }

    private sealed class ProductionRulePlan : IDisposable
    {
        private readonly DRPGBattleStateMutationFixture donor = DRPGBattleStateMutationFixture.Create();
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        internal ProductionRulePlan(bool enemyAttacker = false, DRPGBattleReactionPipeline legacy = null)
        {
            var basic = CreateDamageSkill("production-basic", 20f); owned.Add(basic); StatusSet(basic, "manaCost", 2f);
            StatusSet(donor.Player.Profile, "skills", new List<DRPGSkillSO> { basic });
            StatusSet(donor.Enemy.Profile, "skills", new List<DRPGSkillSO> { basic });
            Player = new BattleParticipantRuntime(9851, new BattleParticipantSpec(donor.Player.Profile, DRPGBattleSide.Player, DRPGBattleRosterRole.Protagonist, 0));
            Enemy = new BattleParticipantRuntime(9852, new BattleParticipantSpec(donor.Enemy.Profile, DRPGBattleSide.Enemy, DRPGBattleRosterRole.Enemy, 0));
            Friend = donor.CreateDetachedParticipant(9853);
            var progression = ScriptableObject.CreateInstance<DRPGCharacterProgressionDefinitionSO>(); owned.Add(progression);
            StatusSet(progression, "personnelKind", DRPGPersonnelKind.Friend); StatusSet(Friend.Profile, "progressionDefinition", progression);
            ResonanceSkill = CreateDamageSkill("production-resonance", 20f); owned.Add(ResonanceSkill);
            Node = new DRPGResonanceNodeDefinition("active", 0, DRPGResonanceNodeRewardKind.ActiveSkill, ResonanceSkill, null);
            ResonanceSource = new DRPGResonanceProgressSource(DRPGBattleEventKind.ActionImpactResolved, DRPGResonanceProgressActorRule.Protagonist, 1);
            StatusSet(Node, "requiredBattleProgress", 3); StatusSet(Node, "progressSources", new List<DRPGResonanceProgressSource> { ResonanceSource });
            var branch = ScriptableObject.CreateInstance<DRPGResonanceBranchSO>(); owned.Add(branch);
            StatusSet(branch, "branchId", "production-branch"); StatusSet(branch, "friendProfile", Friend.Profile);
            StatusSet(branch, "nodes", new List<DRPGResonanceNodeDefinition> { Node });
            var definition = ScriptableObject.CreateInstance<DRPGResonanceDefinitionSO>(); owned.Add(definition);
            StatusSet(definition, "protagonistProfile", Player.Profile); StatusSet(definition, "branches", new List<DRPGResonanceBranchSO> { branch });
            Service = new DRPGResonanceService(definition);
            var party = new DRPGPartyCompositionState(Player.Profile, new[] { Friend.Profile });
            Check(Service.TrySelectPartner(Friend.Profile, party).Success && Service.TryUnlockNode(Friend.Profile.CombatantId, "active").Success &&
                Service.TryEquipActiveSkill(Friend.Profile.CombatantId, ResonanceSkill).Success, "Production resonance configuration setup failed.");
            Expedition = new DRPGResonanceExpeditionState(); Expedition.AddProgress(Friend.Profile.CombatantId, "production-resonance", 1);
            var access = new DRPGBattleResonanceAccess(Service, Expedition, party);
            Session = new DRPGBattleSession(new[] { Player, Friend, Enemy }, resonanceAccess: access, reactionPipeline: legacy);
            Session.ActionLedgerCommitted += _ => LedgerEvents++;
            Session.BattleEventBatchPublished += _ => LegacyEvents++;
            Session.ActionCompleted += _ => LegacyEvents++;
            Identity = new ActionIdentity(Session.BattleGeneration, 1, 1);
            Check(Session.TransactionArbiter.TryAcquire(BattleTransactionIdentity.ForRootAction(Identity), out var lease, out var error), error); Lease = lease;
            var actor = enemyAttacker ? Enemy : Player; var target = enemyAttacker ? Player : Enemy; var id = new ParticipantId(actor.RuntimeId);
            Frozen = new DRPGBasicAttackFrozenSnapshot(new ActorTurnToken(Identity.BattleGeneration, 1, id),
                new TargetSelectionToken(Identity.BattleGeneration, 1, 1, id), new ParticipantId(target.RuntimeId), basic).WithIdentity(Identity);
            Spec = DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(actor.RuntimeId, target.RuntimeId, basic), actor);
        }
        internal BattleParticipantRuntime Player { get; }
        internal BattleParticipantRuntime Enemy { get; }
        internal BattleParticipantRuntime Friend { get; }
        internal DRPGBattleSession Session { get; }
        internal DRPGResonanceService Service { get; }
        internal DRPGResonanceExpeditionState Expedition { get; }
        internal DRPGResonanceNodeDefinition Node { get; }
        internal DRPGResonanceProgressSource ResonanceSource { get; }
        internal DRPGSkillSO ResonanceSkill { get; }
        internal BattleRuleMutationLease Lease { get; }
        internal ActionIdentity Identity { get; }
        internal DRPGBasicAttackFrozenSnapshot Frozen { get; }
        internal DRPGResolvedActionSpec Spec { get; }
        internal int LedgerEvents, LegacyEvents;
        internal DRPGPreparedBasicAttackRuleAction Prepare(IEnumerable<IDRPGImmediateReactionSource> reactions = null)
        { Check(Session.StandardBasicAttackRules.TryPrepare(Frozen, Spec, Lease, out var action, out var error, reactions), error); return action; }
        public void Dispose()
        { Lease?.Dispose(); for (int i = owned.Count - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(owned[i]); donor.Dispose(); }
    }
}
#endif
