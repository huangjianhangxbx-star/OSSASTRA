#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using UnityEngine;

internal static partial class DRPGWeaponCompletionJournalProbe
{
    internal static void RunAll()
    {
        OrdinaryBreakCommitsWithDamageAndProgress();
        SignatureBreakRetainsEquipmentWithoutFragments();
        HealthyUseAndReplayDoNotDuplicateConsumption();
        EquipmentOrInventoryDriftRejectsWholeAction();
        InvalidDamageAndFragmentOverflowDoNotPartiallyApply();
        MultipleBreaksShareOneInventoryCommit();
        SnapshotRestoreCannotReviveAStaleCompletion();
        BrokenWeaponsPermitTheNextFallbackAttack();
        BreakClampsResourcesAndPreservesOtherContributions();
        IncompleteResourceJournalCannotPrepare();
        ProductionWeaponKernelRefreshesAfterBreakAndReequip();
        ProductionWeaponKernelRejectsLateEquipmentDrift();
    }

    internal static void OrdinaryBreakCommitsWithDamageAndProgress()
    {
        using (var f = new Fixture())
        {
            var plan = f.Plan();
            Check(f.Weapon.CurrentDurability == 1 && f.Inventory.GetWeaponFragmentCount("journal-weapon") == 0 &&
                ReferenceEquals(f.Equipment.EquippedWeapon, f.Weapon) && f.Player.Build.ContainsBattleSkill("weapon-intrinsic") &&
                f.Enemy.CurrentHp == 120f && f.ProgressValue == 10f, "Preparing weapon completion changed live state.");
            var result = f.Commit(plan);
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed, "Ordinary break ledger did not commit: " + result.Error);
            Check(f.Weapon.CurrentDurability == 0 && f.Equipment.EquippedWeapon == null &&
                !f.Inventory.IsReservedAsEquipped(f.Weapon.InstanceId) && !f.Inventory.ContainsWeapon(f.Weapon.InstanceId) &&
                f.Inventory.GetWeaponFragmentCount("journal-weapon") == 1 && f.Enemy.CurrentHp == 120f &&
                f.Enemy.CurrentPosture == 80f &&
                f.ProgressValue == 7f && f.State.BattleRevision == 1,
                "Damage, progress, durability, unequip and fragments did not share one commit. " +
                "durability=" + f.Weapon.CurrentDurability + "; equipped=" + (f.Equipment.EquippedWeapon != null) +
                "; reserved=" + f.Inventory.IsReservedAsEquipped(f.Weapon.InstanceId) +
                "; contains=" + f.Inventory.ContainsWeapon(f.Weapon.InstanceId) +
                "; fragments=" + f.Inventory.GetWeaponFragmentCount("journal-weapon") +
                "; HP=" + f.Enemy.CurrentHp + "; posture=" + f.Enemy.CurrentPosture +
                "; progress=" + f.ProgressValue + "; revision=" + f.State.BattleRevision);
            Check(!f.Player.Build.ContainsBattleSkill("weapon-intrinsic") &&
                f.Player.Build.BasicAttack.SkillId == "fallback-basic" &&
                f.Player.Build.ConfiguredSkills[0].SkillId == "weapon-dependent" &&
                !f.Player.Build.ContainsBattleSkill("weapon-dependent"), "Weapon break did not rebuild skill availability while preserving configured slots.");
            Check(f.Commit(plan).Status == DRPGPrimaryImpactCommitStatus.AlreadyCommitted &&
                f.Inventory.GetWeaponFragmentCount("journal-weapon") == 1 && f.State.BattleRevision == 1,
                "Replaying ordinary break generated duplicate fragments.");
        }
    }

    internal static void SignatureBreakRetainsEquipmentWithoutFragments()
    {
        using (var f = new Fixture(signature: true))
        {
            var result = f.Commit(f.Plan());
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed, result.Error);
            Check(f.Weapon.CurrentDurability == 0 && ReferenceEquals(f.Equipment.EquippedWeapon, f.Weapon) &&
                f.Inventory.IsReservedAsEquipped(f.Weapon.InstanceId) && f.Inventory.GetWeaponFragmentCount("journal-weapon") == 0 &&
                f.Player.Build.Equipment.IsBroken && f.Player.Build.BasicAttack.SkillId == "fallback-basic" &&
                !f.Player.Build.ContainsBattleSkill("weapon-intrinsic"), "Signature break removed ownership, created fragments or retained functional-weapon skills.");
        }
    }

    internal static void HealthyUseAndReplayDoNotDuplicateConsumption()
    {
        using (var f = new Fixture(durability: 3))
        {
            var plan = f.Plan();
            Check(f.Service.TryStageRootUse(f.Player, f.Identity, "primary-use", DRPGWeaponUseKind.MeleeCommittedSwing,
                plan.Workspace, out string error), error);
            var result = f.Commit(plan);
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed, result.Error);
            Check(f.Weapon.CurrentDurability == 2 && f.Player.Build.Equipment.CurrentDurability == 2 &&
                f.Player.Build.ContainsBattleSkill("weapon-intrinsic") && ReferenceEquals(f.Equipment.EquippedWeapon, f.Weapon) &&
                f.Inventory.GetWeaponFragmentCount("journal-weapon") == 0, "Healthy completion consumed twice or lost equipment functionality.");
            Check(ReferenceEquals(f.Commit(plan).Receipt, result.Receipt) && f.Weapon.CurrentDurability == 2,
                "Healthy weapon replay did not return the original receipt.");
        }
    }

    internal static void EquipmentOrInventoryDriftRejectsWholeAction()
    {
        for (int scenario = 0; scenario < 4; scenario++)
        using (var f = new Fixture())
        {
            var plan = f.Plan();
            if (scenario == 0) Check(f.Equipment.TryReplaceWeapon(f.Weapon, null, "").Success, "Drift setup failed.");
            if (scenario == 1) Check(f.Inventory.TryReleaseReservedEquippedWeapon(f.Weapon).Success, "Ownership drift setup failed.");
            if (scenario == 2)
            {
                Check(f.Equipment.TryReplaceWeapon(f.Weapon, null, "").Success, "ABA setup failed.");
                Check(f.Equipment.TryReplaceWeapon(null, f.Weapon, "").Success, "ABA restore failed.");
            }
            if (scenario == 3)
                Check(f.Inventory.TryRestoreWeaponFragments(new[] { new DRPGWeaponFragmentSaveData {
                    weaponDefinitionId = "journal-weapon", count = 8 } }).Success, "Fragment drift setup failed.");
            Check(f.Commit(plan).Status == DRPGPrimaryImpactCommitStatus.Rejected && f.Weapon.CurrentDurability == 1 &&
                f.Enemy.CurrentHp == 120f && f.ProgressValue == 10f && f.State.BattleRevision == 0 &&
                f.Player.Build.ContainsBattleSkill("weapon-intrinsic"), "Stale equipment partially committed: " + scenario);
        }
    }

    internal static void InvalidDamageAndFragmentOverflowDoNotPartiallyApply()
    {
        using (var f = new Fixture())
        {
            var plan = f.Plan();
            var broken = plan.Workspace.Fork();
            broken.For(new ParticipantId(f.Enemy.RuntimeId)).SetHitPoints(float.NaN);
            var invalid = new DRPGImmediateReactionClosureResult(broken, plan.Entries, null);
            Check(f.Commit(invalid).Status == DRPGPrimaryImpactCommitStatus.Rejected && f.Weapon.CurrentDurability == 1 &&
                f.Equipment.EquippedWeapon == f.Weapon && f.Inventory.GetWeaponFragmentCount("journal-weapon") == 0 &&
                f.ProgressValue == 10f, "Invalid damage committed part of a weapon break.");
            Check(f.Commit(plan).Status == DRPGPrimaryImpactCommitStatus.Committed, "Invalid descendant poisoned original weapon completion.");
        }
        using (var f = new Fixture())
        {
            Check(f.Inventory.TryRestoreWeaponFragments(new[] { new DRPGWeaponFragmentSaveData {
                weaponDefinitionId = "journal-weapon", count = int.MaxValue } }).Success, "Overflow setup failed.");
            var workspace = f.State.CreateWorkspace();
            bool staged = f.Service.TryStageRootUse(f.Player, f.Identity, "overflow-use", DRPGWeaponUseKind.MeleeCommittedSwing,
                workspace, out _);
            if (staged)
            {
                var plan = f.Plan(workspace);
                Check(f.Commit(plan).Status == DRPGPrimaryImpactCommitStatus.Rejected, "Fragment count overflow was committed.");
            }
            Check(f.Weapon.CurrentDurability == 1 && f.Equipment.EquippedWeapon == f.Weapon &&
                f.Inventory.IsReservedAsEquipped(f.Weapon.InstanceId) && f.Inventory.GetWeaponFragmentCount("journal-weapon") == int.MaxValue &&
                f.State.BattleRevision == 0 && f.ProgressValue == 10f, "Overflow consumed durability, ownership or progress before failure.");
        }
    }

    internal static void MultipleBreaksShareOneInventoryCommit()
    {
        using (var f = new Fixture(secondWeapon: true))
        {
            var plan = f.Plan();
            Check(f.Service.TryStageRootUse(f.Enemy, f.Identity, "reaction-use", DRPGWeaponUseKind.MeleeCommittedSwing,
                plan.Workspace, out string error), error);
            var result = f.Commit(plan);
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed, result.Error);
            Check(f.Weapon.CurrentDurability == 0 && f.OtherWeapon.CurrentDurability == 0 &&
                f.Equipment.EquippedWeapon == null && f.OtherEquipment.EquippedWeapon == null &&
                f.Inventory.GetWeaponFragmentCount("journal-weapon") == 2 &&
                !f.Inventory.IsReservedAsEquipped(f.Weapon.InstanceId) && !f.Inventory.IsReservedAsEquipped(f.OtherWeapon.InstanceId) &&
                f.Player.Build.BasicAttack.SkillId == "fallback-basic" && f.Enemy.Build.BasicAttack.SkillId == "fallback-basic" &&
                f.Enemy.CurrentHp == 120f && f.Enemy.CurrentPosture == 80f && f.State.BattleRevision == 1,
                "Two breaks overwrote the shared inventory, damage or left one build unreconciled.");
        }
    }

    internal static void SnapshotRestoreCannotReviveAStaleCompletion()
    {
        for (int scenario = 0; scenario < 2; scenario++)
        using (var f = new Fixture())
        {
            var plan = f.Plan();
            if (scenario == 0)
            {
                var saved = f.Equipment.CaptureSnapshot();
                Check(f.Equipment.TryReplaceWeapon(f.Weapon, null, "").Success, "Restore drift setup failed.");
                f.Equipment.RestoreSnapshot(saved);
            }
            else
            {
                Check(f.Inventory.TryRestoreWeaponFragments(new[] { new DRPGWeaponFragmentSaveData {
                    weaponDefinitionId = "journal-weapon", count = 8 } }).Success, "Fragment restore drift setup failed.");
                Check(f.Inventory.TryRestoreWeaponFragments(Array.Empty<DRPGWeaponFragmentSaveData>()).Success, "Fragment restore failed.");
            }
            Check(f.Commit(plan).Status == DRPGPrimaryImpactCommitStatus.Rejected && f.Enemy.CurrentHp == 120f &&
                f.Weapon.CurrentDurability == 1 && f.ProgressValue == 10f && f.State.BattleRevision == 0,
                "Restoring an old revision/value revived a stale weapon completion: " + scenario);
        }
    }

    internal static void BrokenWeaponsPermitTheNextFallbackAttack()
    {
        foreach (bool signature in new[] { false, true })
        using (var f = new Fixture(signature: signature))
        {
            Check(f.Commit(f.Plan()).Status == DRPGPrimaryImpactCommitStatus.Committed, "Initial break failed.");
            f.Lease.Dispose();
            var identity = new ActionIdentity(f.Identity.BattleGeneration, 2, 2);
            Check(f.Arbiter.TryAcquire(BattleTransactionIdentity.ForRootAction(identity), out var lease, out string error), error);
            using (lease)
            {
                var workspace = f.State.CreateWorkspace();
                Check(f.Service.TryStageRootUse(f.Player, identity, "next-fallback", DRPGWeaponUseKind.MeleeCommittedSwing,
                    workspace, out error), "Broken equipment blocked fallback: " + error);
                var resolver = new DRPGActionResolver();
                var spec = DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(f.Player.RuntimeId, f.Enemy.RuntimeId, f.Player.Build.BasicAttack), f.Player);
                var primary = resolver.ResolveIntoWorkspace(spec, f.Player, f.Enemy, f.Participants, workspace,
                    DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f));
                var closure = new DRPGImmediateReactionLedgerBuilder().Build(identity, "next-fallback-impact", primary,
                    workspace, f.Participants, Array.Empty<IDRPGImmediateReactionSource>(), resolver);
                var frozen = new DRPGBasicAttackFrozenSnapshot(new ActorTurnToken(identity.BattleGeneration, 2, new ParticipantId(9901)),
                    new TargetSelectionToken(identity.BattleGeneration, 2, 2, new ParticipantId(9901)), new ParticipantId(9902), f.Player.Build.BasicAttack).WithIdentity(identity);
                var result = f.Store.TryCommitActionResolution("next-fallback-root", frozen, primary, closure, lease);
                Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && f.Enemy.CurrentHp == 120f &&
                    f.Enemy.CurrentPosture == 60f &&
                    f.Weapon.CurrentDurability == 0 && f.State.BattleRevision == 2 &&
                    f.Inventory.GetWeaponFragmentCount("journal-weapon") == (signature ? 0 : 1),
                    "Next fallback did not commit independently: " + result.Error);
            }
        }
    }

    internal static void BreakClampsResourcesAndPreservesOtherContributions()
    {
        using (var f = new Fixture(boosted: true))
        {
            Check(f.Player.CurrentHp == 300f && f.Player.CurrentPosture == 100f,
                "Boost fixture did not preserve the authored posture default alongside HP equipment contributions.");
            var closure = f.Plan();
            var journal = closure.Workspace.BuildValidatedJournal().ActionResources;
            Check(journal.WeaponCompletions.Count == 1 && journal.WeaponCompletions[0].HpBefore == 300f &&
                journal.WeaponCompletions[0].HpAfter == 150f && journal.WeaponCompletions[0].PostureAfter == 100f,
                "Clamps are absent from the detached completion journal.");
            Check(f.Player.CurrentHp == 300f && f.Player.CurrentPosture == 100f, "Preparing clamps changed live resources.");
            var result = f.Commit(closure);
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && f.Player.CurrentHp == 150f &&
                f.Player.Build.EffectiveMaxHp == 150f && f.Player.CurrentPosture == 100f && f.Equipment.Accessories[0] != null &&
                f.Enemy.CurrentHp == 120f && f.Enemy.CurrentPosture == 80f && f.State.BattleRevision == 1,
                "Break lost the accessory or did not atomically apply resource clamps: " + result.Error);
        }
    }

    internal static void IncompleteResourceJournalCannotPrepare()
    {
        using (var f = new Fixture())
        {
            var closure = f.Plan();
            var full = closure.Workspace.BuildValidatedJournal();
            var journal = full.ActionResources;
            var original = journal.WeaponCompletions[0];
            var variants = new[] {
                new DRPGBattleActionResourceJournal(journal.Snapshot, journal.Identity,
                    journal.ProgressMutations, journal.WeaponUses, journal.WeaponEvents),
                new DRPGBattleActionResourceJournal(journal.Snapshot, journal.Identity,
                    journal.ProgressMutations, journal.WeaponUses, Array.Empty<DRPGBattleWeaponUseEvent>(), journal.WeaponCompletions),
                new DRPGBattleActionResourceJournal(journal.Snapshot, journal.Identity,
                    journal.ProgressMutations, journal.WeaponUses, journal.WeaponEvents, new[] {
                        new DRPGWeaponCompletionMutation(original.Plan, original.HpBefore - 1f, original.ManaBefore, original.PostureBefore) }),
                new DRPGBattleActionResourceJournal(journal.Snapshot, journal.Identity,
                    journal.ProgressMutations, journal.WeaponUses, journal.WeaponEvents, new[] { original, original }) };
            foreach (var invalid in variants)
            {
                var altered = new DRPGBattleMutationJournal(full.BaseRevision, full.ParticipantMutations, full.TimelineMutations,
                    full.ResourceMutations, full.DirtySet, invalid, full.ActionCosts);
                bool rejected = false;
                try { f.Bindings.Prepare(invalid, f.Lease.Owner, altered, closure.Workspace.Snapshot); }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected && f.Weapon.CurrentDurability == 1 && f.Equipment.EquippedWeapon == f.Weapon &&
                    f.Inventory.GetWeaponFragmentCount("journal-weapon") == 0, "Preparation accepted missing, duplicate or inconsistent completion facts.");
            }
            var prepared = f.Bindings.Prepare(journal, f.Lease.Owner, full, closure.Workspace.Snapshot);
            bool noCapability = false;
            try { prepared.Apply(); }
            catch (InvalidOperationException) { noCapability = true; }
            Check(noCapability && f.Enemy.CurrentHp == 120f && f.Weapon.CurrentDurability == 1 && f.ProgressValue == 10f,
                "Prepared equipment commit applied outside its State capability.");
            Check(f.Commit(closure).Status == DRPGPrimaryImpactCommitStatus.Committed, "Rejected forged journal poisoned original completion.");
        }
    }

    internal static void ProductionWeaponKernelRefreshesAfterBreakAndReequip()
    {
        for (int scenario = 0; scenario < 3; scenario++)
        using (var f = new Fixture(signature: scenario == 2, productionSession: true))
        {
            var spec = DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(f.Player.RuntimeId, f.Enemy.RuntimeId, f.Player.Build.BasicAttack), f.Player);
            Check(f.Session.StandardBasicAttackRules.TryPrepare(f.Frozen, spec, f.Lease, out var first, out var error), error);
            DRPGCommittedWeaponFact fact;
            using (first)
            {
                var result = first.ResolveAndCommit(DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f));
                Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && f.Weapon.CurrentDurability == 0 &&
                    result.Receipt.Ledger.Facts.Weapons.Count == 1 && f.Enemy.CurrentHp == 120f &&
                    f.Enemy.CurrentPosture == 80f, "Production weapon break failed: " + result.Error);
                fact = result.Receipt.Ledger.Facts.Weapons[0];
                Check(fact.DurabilityBefore == 1 && fact.DurabilityAfter == 0 && fact.WasRemoved == (scenario != 2) &&
                    fact.FragmentCount == (scenario == 2 ? 0 : 1), "Immutable weapon completion facts differ from the committed break.");
            }
            f.Lease.Dispose();
            var nextIdentity = new ActionIdentity(f.Identity.BattleGeneration, 2, 2);
            Check(f.Arbiter.TryAcquire(BattleTransactionIdentity.ForRootAction(nextIdentity), out var nextLease, out error), error);
            using (nextLease)
            {
                DRPGWeaponInstance replacement = null;
                if (scenario == 1)
                {
                    replacement = DRPGWeaponInstance.Create("replacement-weapon", f.Definition, new MinimumRandom()); replacement.RestoreDurability(3, 3);
                    Check(f.Inventory.TryReserveEquippedWeapon(replacement).Success && f.Equipment.TryReplaceWeapon(null, replacement, string.Empty).Success,
                        "Replacement equipment setup failed.");
                    using (f.State.OpenCompatibilityMutationScope(nextLease))
                        f.Player.ReplaceBuild(new DRPGProgressionService().ResolveBuild(f.Player.Profile,
                            DRPGResolvedEquipmentSnapshot.FromCharacterEquipment(f.Equipment, string.Empty, f.Root.DefinitionRegistry)));
                }
                var actor = new ParticipantId(f.Player.RuntimeId); var target = new ParticipantId(f.Enemy.RuntimeId);
                var skill = f.Player.Build.BasicAttack;
                var frozen = new DRPGBasicAttackFrozenSnapshot(new ActorTurnToken(nextIdentity.BattleGeneration, 2, actor),
                    new TargetSelectionToken(nextIdentity.BattleGeneration, 2, 2, actor), target, skill).WithIdentity(nextIdentity);
                var nextSpec = DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(actor.Value, target.Value, skill), f.Player);
                Check(f.Session.StandardBasicAttackRules.TryPrepare(frozen, nextSpec, nextLease, out var second, out error), error);
                using (second)
                {
                    var result = second.ResolveAndCommit(DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f));
                    Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && f.Enemy.CurrentHp == 120f &&
                        f.Enemy.CurrentPosture == 60f && f.State.BattleRevision == 2 &&
                        f.ProgressValue == 10f && f.Inventory.GetWeaponFragmentCount("journal-weapon") == (scenario == 2 ? 0 : 1),
                        "Next production action retained a broken/stale equipment binding: " + scenario + " " + result.Error);
                    Check(scenario == 1 ? replacement.CurrentDurability == 2 && result.Receipt.Ledger.Facts.Weapons[0].WeaponId == "replacement-weapon" :
                        result.Receipt.Ledger.Facts.Weapons.Count == 0, "Next action charged the wrong weapon or a broken fallback.");
                }
                Check(fact.WeaponId == "journal-player-weapon" && fact.DurabilityBefore == 1 && fact.DurabilityAfter == 0,
                    "Later equipment/build changes altered the first committed weapon event.");
            }
        }
    }

    internal static void ProductionWeaponKernelRejectsLateEquipmentDrift()
    {
        using (var f = new Fixture(durability: 3, productionSession: true))
        {
            var spec = DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(f.Player.RuntimeId, f.Enemy.RuntimeId, f.Player.Build.BasicAttack), f.Player);
            Check(f.Session.StandardBasicAttackRules.TryPrepare(f.Frozen, spec, f.Lease, out var action, out var error), error);
            using (action)
            {
                Check(f.Equipment.TryReplaceWeapon(f.Weapon, null, string.Empty).Success, "Late unequip setup failed.");
                var result = action.ResolveAndCommit(DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f));
                Check(result.Status == DRPGPrimaryImpactCommitStatus.Rejected && f.Enemy.CurrentHp == 120f &&
                    f.Weapon.CurrentDurability == 3 && f.State.BattleRevision == 0 && f.ProgressValue == 10f,
                    "Late equipment drift partially committed a production hit: " + result.Error);
            }
        }
    }

    internal sealed class Fixture : IDisposable
    {
        private static long generation = 710000;
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        internal Fixture(bool signature = false, int durability = 1, bool secondWeapon = false, bool boosted = false,
            float? capacityBonus = null, float baseProgressCapacity = 5f, float progressValue = 18f, bool productionSession = false)
        {
            try
            {
                var fallback = Skill("fallback-basic", 20f);
                var basic = Skill("weapon-basic", 20f);
                var intrinsic = Skill("weapon-intrinsic", 1f);
                var dependent = Skill("weapon-dependent", 1f);
                Set(dependent, "requiresFunctionalWeapon", true);
                var playerProfile = Profile("journal-player", 100f, fallback, dependent);
                var enemyProfile = Profile("journal-enemy", 120f, fallback, dependent);
                Definition = ScriptableObject.CreateInstance<DRPGWeaponDefinitionSO>(); owned.Add(Definition);
                Set(Definition, "weaponId", "journal-weapon"); Set(Definition, "baseDurability", durability);
                Set(Definition, "rarity", DRPGEquipmentRarity.RedGold);
                Set(Definition, "ownershipKind", signature ? DRPGWeaponOwnershipKind.Signature : DRPGWeaponOwnershipKind.General);
                Set(Definition, "signatureCharacterId", signature ? playerProfile.CombatantId : null);
                Set(Definition, "basicAttackOverride", basic); Set(Definition, "intrinsicSkill", intrinsic);
                if (capacityBonus.HasValue)
                {
                    ProgressResource = ScriptableObject.CreateInstance<DRPGProgressResourceSO>(); owned.Add(ProgressResource);
                    Set(ProgressResource, "resourceId", "focus"); Set(ProgressResource, "baseCapacity", baseProgressCapacity);
                    Set(Definition, "progressCapacityModifiers", new List<DRPGProgressCapacityModifierDefinition> {
                        new DRPGProgressCapacityModifierDefinition(ProgressResource, capacityBonus.Value) });
                }
                DRPGAccessoryDefinitionSO accessory = null;
                if (boosted)
                {
                    var weaponPassive = ScriptableObject.CreateInstance<DRPGPassiveModifierSO>(); owned.Add(weaponPassive);
                    Set(weaponPassive, "modifierId", "weapon-boost"); Set(weaponPassive, "maxHpMultiplier", 2f); Set(weaponPassive, "maxPostureMultiplier", 2f);
                    var affix = ScriptableObject.CreateInstance<DRPGWeaponAffixDefinitionSO>(); owned.Add(affix);
                    Set(affix, "passiveModifiers", new List<DRPGPassiveModifierSO> { weaponPassive });
                    Set(Definition, "fixedAffixes", new List<DRPGWeaponAffixDefinitionSO> { affix });
                    var accessoryPassive = ScriptableObject.CreateInstance<DRPGPassiveModifierSO>(); owned.Add(accessoryPassive);
                    Set(accessoryPassive, "modifierId", "accessory-boost"); Set(accessoryPassive, "maxHpMultiplier", 1.5f);
                    accessory = ScriptableObject.CreateInstance<DRPGAccessoryDefinitionSO>(); owned.Add(accessory);
                    Set(accessory, "passiveModifiers", new List<DRPGPassiveModifierSO> { accessoryPassive });
                }
                Weapon = DRPGWeaponInstance.Create("journal-player-weapon", Definition, new MinimumRandom());
                Weapon.RestoreDurability(durability, durability);
                Inventory = new DRPGPartyInventoryState(0, 0f); Root = new DRPGEquipmentRoot(Inventory);
                Equipment = new DRPGCharacterEquipmentState(playerProfile.CombatantId, Weapon);
                if (accessory != null) Check(Equipment.TrySetAccessory(0, accessory).Success, "Accessory setup failed.");
                Check(Root.TryRegisterCharacter(playerProfile, Equipment).Success, "Actor equipment registration failed.");
                var progression = new DRPGProgressionService();
                Player = Actor(9901, playerProfile, DRPGBattleSide.Player, progression, Equipment);
                if (secondWeapon)
                {
                    OtherWeapon = DRPGWeaponInstance.Create("journal-enemy-weapon", Definition, new MinimumRandom());
                    OtherWeapon.RestoreDurability(durability, durability);
                    OtherEquipment = new DRPGCharacterEquipmentState(enemyProfile.CombatantId, OtherWeapon);
                    Check(Root.TryRegisterCharacter(enemyProfile, OtherEquipment).Success, "Second equipment registration failed.");
                }
                Enemy = Actor(9902, enemyProfile, DRPGBattleSide.Enemy, progression, OtherEquipment);
                Participants = new[] { Player, Enemy };
                Progress = new DRPGBattleProgressLedger(); Uses = new DRPGWeaponUseLedger();
                Pipeline = new DRPGBattleProgressPipeline(null, new DRPGProgressStoreRouter(new IDRPGProgressStore[] { Progress }));
                Check(Pipeline.TryRegister("journal-player", ProgressResource != null ? ProgressResource.CreateDefinition(capacityBonus.Value) :
                    new DRPGProgressResourceDefinition("focus", DRPGProgressScope.Battle, 20f, 10f),
                    out _, out string error), error);
                if (ProgressResource != null)
                {
                    Check(Pipeline.TryGain("journal-player", "focus", DRPGProgressScope.Battle, progressValue, out _, out error), error);
                    Check(Pipeline.TryRegister("journal-player", new DRPGProgressResourceDefinition("unrelated", DRPGProgressScope.Battle, 100f, 40f), out _, out error), error);
                    if (OtherWeapon != null)
                    {
                        Check(Pipeline.TryRegister("journal-enemy", ProgressResource.CreateDefinition(capacityBonus.Value), out _, out error), error);
                        Check(Pipeline.TryGain("journal-enemy", "focus", DRPGProgressScope.Battle, progressValue, out _, out error), error);
                    }
                }
                Service = new DRPGBattleWeaponDurabilityService(Root, progression, Uses);
                long gen;
                var trace = new DRPGBattleActionTraceWriter(new DRPGBoundedActionTraceSink(128));
                if (productionSession)
                {
                    Session = new DRPGBattleSession(Participants, weaponDurabilityService: Service, progressPipeline: Pipeline);
                    gen = Session.BattleGeneration; Arbiter = Session.TransactionArbiter; State = Session.TransactionState;
                    Store = Session.ActionResolutionLedgerStore;
                }
                else
                {
                    gen = Interlocked.Increment(ref generation); Arbiter = new DRPGBattleTransactionArbiter(gen, trace);
                    Bindings = Service.CreateActionResourceBindings(Progress, Participants);
                    State = new DRPGBattleSessionState(gen, Participants, new DRPGTurnScheduler(), Arbiter, null, Bindings);
                    Store = new DRPGBattleActionResolutionLedgerStore(State, trace);
                }
                Identity = new ActionIdentity(gen, 1, 1);
                Check(Arbiter.TryAcquire(BattleTransactionIdentity.ForRootAction(Identity), out var lease, out error), error); Lease = lease;
                Frozen = new DRPGBasicAttackFrozenSnapshot(new ActorTurnToken(gen, 1, new ParticipantId(9901)),
                    new TargetSelectionToken(gen, 1, 1, new ParticipantId(9901)), new ParticipantId(9902), basic).WithIdentity(Identity);
            }
            catch { Dispose(); throw; }
        }
        internal DRPGWeaponDefinitionSO Definition { get; }
        internal DRPGBattleSession Session { get; }
        internal DRPGProgressResourceSO ProgressResource { get; }
        internal DRPGWeaponInstance Weapon { get; }
        internal DRPGWeaponInstance OtherWeapon { get; }
        internal DRPGPartyInventoryState Inventory { get; }
        internal DRPGEquipmentRoot Root { get; }
        internal DRPGCharacterEquipmentState Equipment { get; }
        internal DRPGCharacterEquipmentState OtherEquipment { get; }
        internal BattleParticipantRuntime Player { get; }
        internal BattleParticipantRuntime Enemy { get; }
        internal BattleParticipantRuntime[] Participants { get; }
        internal DRPGBattleProgressLedger Progress { get; }
        internal DRPGBattleProgressPipeline Pipeline { get; }
        internal DRPGWeaponUseLedger Uses { get; }
        internal DRPGBattleWeaponDurabilityService Service { get; }
        internal DRPGBattleTransactionArbiter Arbiter { get; }
        internal DRPGBattleSessionState State { get; }
        internal DRPGBattleActionResourceBindings Bindings { get; }
        internal BattleRuleMutationLease Lease { get; }
        internal ActionIdentity Identity { get; }
        internal DRPGBattleActionResolutionLedgerStore Store { get; }
        internal DRPGBasicAttackFrozenSnapshot Frozen { get; }
        internal DRPGWorkspaceResolutionResult Primary { get; private set; }
        internal float ProgressValue { get { Check(Pipeline.TryGetSnapshot("journal-player", "focus", DRPGProgressScope.Battle, out var value), "Progress missing."); return value.CurrentValue; } }
        internal DRPGImmediateReactionClosureResult Plan(DRPGBattleMutationWorkspace input = null, bool spendProgress = true)
        {
            var workspace = input ?? State.CreateWorkspace();
            if (input == null) Check(Service.TryStageRootUse(Player, Identity, "primary-use", DRPGWeaponUseKind.MeleeCommittedSwing,
                workspace, out string error), "Root weapon staging failed: " + error);
            if (spendProgress) Check(workspace.ActionResources.TryStageProgress(Identity, DRPGProgressMutationRequest.Spend("rule", "cost", 0,
                "journal-player", "focus", DRPGProgressScope.Battle, 3f), out _, out string progressError), progressError);
            var resolver = new DRPGActionResolver();
            var spec = DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(Player.RuntimeId, Enemy.RuntimeId, Player.Build.BasicAttack), Player);
            Primary = resolver.ResolveIntoWorkspace(spec, Player, Enemy, Participants, workspace, DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f));
            return new DRPGImmediateReactionLedgerBuilder().Build(Identity, "action-" + Identity.BattleGeneration + "-1-1-impact-0",
                Primary, workspace, Participants, Array.Empty<IDRPGImmediateReactionSource>(), resolver);
        }
        internal DRPGPrimaryImpactCommitResult Commit(DRPGImmediateReactionClosureResult plan) =>
            Store.TryCommitActionResolution("weapon-root", Frozen, Primary, plan, Lease);
        private DRPGSkillSO Skill(string id, float power)
        {
            var skill = ScriptableObject.CreateInstance<DRPGSkillSO>(); owned.Add(skill);
            Set(skill, "skillId", id); Set(skill, "power", power);
            Set(skill, "effects", new List<DRPGActionEffectDefinition> {
                DRPGActionEffectDefinition.Damage(DRPGActionEffectTarget.PrimaryTarget, 1f, DRPGDamageTag.Slash) });
            return skill;
        }
        private DRPGCombatantProfileSO Profile(string id, float hp, DRPGSkillSO basic, DRPGSkillSO dependent)
        {
            var profile = ScriptableObject.CreateInstance<DRPGCombatantProfileSO>(); owned.Add(profile);
            Set(profile, "combatantId", id); Set(profile, "maxHp", hp); Set(profile, "maxMp", 30f); Set(profile, "maxPosture", 100f);
            Set(profile, "skills", new List<DRPGSkillSO> { basic, dependent }); return profile;
        }
        private BattleParticipantRuntime Actor(int id, DRPGCombatantProfileSO profile, DRPGBattleSide side,
            DRPGProgressionService progression, DRPGCharacterEquipmentState equipment) => new BattleParticipantRuntime(id,
                new BattleParticipantSpec(profile, side, side == DRPGBattleSide.Player ? DRPGBattleRosterRole.Protagonist : DRPGBattleRosterRole.Enemy,
                    0, build: progression.ResolveBuild(profile, DRPGResolvedEquipmentSnapshot.FromCharacterEquipment(equipment, "", Root.DefinitionRegistry))));
        public void Dispose() { Lease?.Dispose(); foreach (var item in owned) if (item != null) UnityEngine.Object.DestroyImmediate(item); }
    }
    private sealed class MinimumRandom : IDRPGRandomSource
    { public float Range(float min, float max) => min; public int RangeInclusive(int min, int max) => min; }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
