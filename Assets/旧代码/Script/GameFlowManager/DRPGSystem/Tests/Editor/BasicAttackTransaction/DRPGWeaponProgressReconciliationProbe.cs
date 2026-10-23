#if UNITY_EDITOR
using System;

internal static partial class DRPGWeaponCompletionJournalProbe
{
    internal static void RunProgressCapacityChecks()
    {
        BreakReconcilesProgressAfterCosts();
        HealthyWeaponPreservesProgressCapacity();
        CapacityGrowthDoesNotRefillProgress();
        MultipleActorsReconcileProgressSeparately();
        CapacityFailureDiscardsEntireCompletion();
        AuthoredCapacityChangesDoNotAlterLockedAction();
        CapacityFactsFollowTheFinalForkAndRemainFrozen();
        ForgedCapacityFactsRejectBeforeApply();
        FrozenBaseDefinitionCannotBeReplacedInsideAction();
        CapacityOnlyCompletionStillUpdatesTheRealStore();
        ZeroCapacityAndLateFailureRemainAtomic();
    }

    internal static void BreakReconcilesProgressAfterCosts()
    {
        foreach (bool signature in new[] { false, true })
        using (var f = new Fixture(signature: signature, capacityBonus: 15f))
        {
            var plan = f.Plan();
            Check(Focus(f).Capacity == 20f && f.ProgressValue == 18f && f.Enemy.CurrentHp == 120f,
                "Planning a capacity change modified live resources.");
            var result = f.Commit(plan);
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed, result.Error);
            Check(Focus(f).Capacity == 5f && f.ProgressValue == 5f && f.Enemy.CurrentHp == 120f &&
                f.Enemy.CurrentPosture == 80f &&
                f.Weapon.CurrentDurability == 0 && f.State.BattleRevision == 1,
                "Weapon break did not clamp progress with damage and durability in one commit.");
            Check(ReferenceEquals(f.Commit(plan).Receipt, result.Receipt) && f.ProgressValue == 5f &&
                f.State.BattleRevision == 1, "Capacity replay reapplied costs or break effects.");
            Check(f.Pipeline.TryGetSnapshot("journal-player", "unrelated", DRPGProgressScope.Battle, out var unrelated) &&
                unrelated.CurrentValue == 40f && unrelated.Capacity == 100f, "Weapon break changed an unrelated progress resource.");
        }
    }

    internal static void HealthyWeaponPreservesProgressCapacity()
    {
        using (var f = new Fixture(durability: 3, capacityBonus: 15f))
        {
            Check(f.Commit(f.Plan()).Status == DRPGPrimaryImpactCommitStatus.Committed, "Healthy use did not commit.");
            Check(Focus(f).Capacity == 20f && f.ProgressValue == 15f && f.Weapon.CurrentDurability == 2,
                "Healthy use discarded its still-functional capacity modifier.");
        }
    }

    internal static void CapacityGrowthDoesNotRefillProgress()
    {
        using (var f = new Fixture(capacityBonus: -15f, baseProgressCapacity: 20f, progressValue: 4f))
        {
            Check(Focus(f).Capacity == 5f, "Negative capacity fixture is invalid.");
            var result = f.Commit(f.Plan());
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed, result.Error);
            Check(Focus(f).Capacity == 20f && f.ProgressValue == 1f,
                "Removing a negative modifier failed to restore base capacity or refilled the bar.");
        }
    }

    internal static void MultipleActorsReconcileProgressSeparately()
    {
        using (var f = new Fixture(secondWeapon: true, capacityBonus: 15f))
        {
            var plan = f.Plan();
            Check(f.Service.TryStageRootUse(f.Enemy, f.Identity, "counter-use", DRPGWeaponUseKind.MeleeCommittedSwing,
                plan.Workspace, out string error), error);
            var result = f.Commit(plan);
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed, result.Error);
            Check(Focus(f).Capacity == 5f && f.ProgressValue == 5f &&
                Focus(f, "journal-enemy").Capacity == 5f && Focus(f, "journal-enemy").CurrentValue == 5f &&
                f.Inventory.GetWeaponFragmentCount("journal-weapon") == 2 && f.State.BattleRevision == 1,
                "Two weapon completions overwrote or crossed their owners' progress reconciliation.");
        }
    }

    internal static void CapacityFailureDiscardsEntireCompletion()
    {
        using (var f = new Fixture(capacityBonus: 15f))
        {
            var plan = f.Plan();
            var invalid = plan.Workspace.Fork();
            invalid.For(new ParticipantId(f.Enemy.RuntimeId)).SetHitPoints(float.NaN);
            var rejected = f.Commit(new DRPGImmediateReactionClosureResult(invalid, plan.Entries, null));
            Check(rejected.Status == DRPGPrimaryImpactCommitStatus.Rejected && Focus(f).Capacity == 20f &&
                f.ProgressValue == 18f && f.Weapon.CurrentDurability == 1 && f.Enemy.CurrentHp == 120f,
                "Rejected action partially applied capacity/cost/damage.");
            Check(f.Commit(plan).Status == DRPGPrimaryImpactCommitStatus.Committed && Focus(f).Capacity == 5f && f.ProgressValue == 5f,
                "Rejected descendant poisoned the valid capacity reconciliation.");
        }
    }

    internal static void AuthoredCapacityChangesDoNotAlterLockedAction()
    {
        using (var f = new Fixture(capacityBonus: 15f))
        {
            var plan = f.Plan();
            Set(f.ProgressResource, "baseCapacity", 999f);
            var result = f.Commit(plan);
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && Focus(f).Capacity == 5f && f.ProgressValue == 5f,
                "Completion re-read authored capacity instead of its captured definition: " + result.Error);
        }
    }

    internal static void CapacityFactsFollowTheFinalForkAndRemainFrozen()
    {
        using (var f = new Fixture(capacityBonus: 15f))
        {
            var original = f.Plan();
            var before = original.Workspace.BuildValidatedJournal().ActionResources.WeaponProgressReconciliations[0];
            var child = original.Workspace.Fork();
            Check(child.ActionResources.TryStageProgress(f.Identity, DRPGProgressMutationRequest.Gain("reaction", "gain", 1,
                "journal-player", "focus", DRPGProgressScope.Battle, 2f, true), out _, out string error), error);
            var closure = new DRPGImmediateReactionClosureResult(child, original.Entries, null);
            var fact = child.BuildValidatedJournal().ActionResources.WeaponProgressReconciliations[0];
            Check(before.Before.CurrentValue == 15f && before.After.CurrentValue == 5f && before.Mutation.OverflowAmount == 10f &&
                fact.Before.CurrentValue == 17f && fact.After.CurrentValue == 5f && fact.Mutation.OverflowAmount == 12f &&
                fact.Identity == f.Identity && fact.WeaponId == f.Weapon.InstanceId && fact.Before.OwnerStableId == "journal-player" &&
                fact.Before.Definition.BaseCapacity == 5f && fact.Before.Definition.EquipmentCapacityModifier == 15f &&
                fact.After.Definition.BaseCapacity == 5f && fact.After.Definition.EquipmentCapacityModifier == 0f,
                "Capacity facts lost root/provenance/order or used the primary fork's old progress value.");
            var result = f.Commit(closure);
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed && f.Commit(original).Status == DRPGPrimaryImpactCommitStatus.FingerprintConflict &&
                ReferenceEquals(f.Commit(closure).Receipt, result.Receipt), "Capacity-changing fork replay did not preserve the exact original request.");
            f.Lease.Dispose();
            Check(f.Pipeline.TrySpend("journal-player", "focus", DRPGProgressScope.Battle, 4f, out _, out error), error);
            Check(f.Pipeline.TryGain("journal-player", "focus", DRPGProgressScope.Battle, 100f, out _, out error), error);
            Check(f.ProgressValue == 5f && fact.Before.CurrentValue == 17f && before.Before.CurrentValue == 15f,
                "Later progress either ignored its new capacity or mutated old ledger facts.");
        }
    }

    internal static void ForgedCapacityFactsRejectBeforeApply()
    {
        using (var f = new Fixture(capacityBonus: 15f))
        {
            var closure = f.Plan();
            var full = closure.Workspace.BuildValidatedJournal();
            var journal = full.ActionResources;
            var fact = journal.WeaponProgressReconciliations[0];
            var request = fact.Mutation.Request;
            var alteredRequest = DRPGProgressMutationRequest.Register(request.SourceId, request.DeduplicationKey,
                request.Priority - 1, request.OwnerStableId, request.Definition);
            var alteredMutation = new DRPGProgressMutationResult(alteredRequest, fact.Mutation.State,
                fact.Mutation.ValueBefore, fact.Mutation.ValueAfter, fact.Mutation.Capacity, fact.Mutation.AppliedAmount, fact.Mutation.OverflowAmount);
            var variants = new[] {
                Array.Empty<DRPGWeaponProgressReconciliation>(),
                new[] { fact, fact },
                new[] { new DRPGWeaponProgressReconciliation(f.Identity, fact.WeaponId,
                    new DRPGProgressSnapshot("journal-enemy", fact.Before.Definition, fact.Before.CurrentValue), fact.After, fact.Mutation) },
                new[] { new DRPGWeaponProgressReconciliation(f.Identity, fact.WeaponId,
                    new DRPGProgressSnapshot(fact.Before.OwnerStableId, fact.Before.Definition, 14f), fact.After, fact.Mutation) },
                new[] { new DRPGWeaponProgressReconciliation(f.Identity, fact.WeaponId, fact.Before,
                    new DRPGProgressSnapshot(fact.After.OwnerStableId, new DRPGProgressResourceDefinition("focus", DRPGProgressScope.Battle, 6f), 5f), fact.Mutation) },
                new[] { new DRPGWeaponProgressReconciliation(f.Identity, fact.WeaponId, fact.Before, fact.After, alteredMutation) } };
            foreach (var variant in variants)
            {
                var invalid = new DRPGBattleActionResourceJournal(journal.Snapshot, journal.Identity, journal.ProgressMutations,
                    journal.WeaponUses, journal.WeaponEvents, journal.WeaponCompletions, variant);
                var altered = new DRPGBattleMutationJournal(full.BaseRevision, full.ParticipantMutations, full.TimelineMutations,
                    full.ResourceMutations, full.DirtySet, invalid, full.ActionCosts);
                bool rejected = false;
                try { f.Bindings.Prepare(invalid, f.Lease.Owner, altered, closure.Workspace.Snapshot); }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected && f.ProgressValue == 18f && Focus(f).Capacity == 20f && f.Weapon.CurrentDurability == 1 &&
                    f.Enemy.CurrentHp == 120f && f.State.BattleRevision == 0, "Forged capacity facts partially prepared/applied the action.");
                var key = new DRPGPrimaryImpactKey(f.Identity, 0);
                Check(DRPGActionResolutionFingerprint.ComputeComplete(key, "weapon-root", f.Frozen, f.Primary.Batch.Hits[0],
                    f.Primary.Batch.Effects, closure.Entries, full) != DRPGActionResolutionFingerprint.ComputeComplete(key, "weapon-root",
                    f.Frozen, f.Primary.Batch.Hits[0], f.Primary.Batch.Effects, closure.Entries, altered),
                    "Different capacity mutation requests alias the same complete-ledger fingerprint.");
            }
            Check(f.Commit(closure).Status == DRPGPrimaryImpactCommitStatus.Committed, "Rejected facts poisoned the original capacity journal.");
        }
    }

    internal static void FrozenBaseDefinitionCannotBeReplacedInsideAction()
    {
        using (var f = new Fixture(capacityBonus: 15f))
        {
            var plan = f.Plan();
            var child = plan.Workspace.Fork();
            // Same displayed capacity, different base/weapon provenance.
            Check(child.ActionResources.TryStageProgress(f.Identity, DRPGProgressMutationRequest.Register("rule", "replace", 1,
                "journal-player", new DRPGProgressResourceDefinition("focus", DRPGProgressScope.Battle, 20f)), out _, out string error), error);
            var result = f.Commit(new DRPGImmediateReactionClosureResult(child, plan.Entries, null));
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Rejected && Focus(f).Capacity == 20f &&
                f.ProgressValue == 18f && f.Weapon.CurrentDurability == 1 && f.State.BattleRevision == 0,
                "A capacity registration replaced the locked weapon's base definition.");
            Check(f.Commit(plan).Status == DRPGPrimaryImpactCommitStatus.Committed && Focus(f).Capacity == 5f,
                "Invalid provenance prevented a valid retry.");
        }
    }

    internal static void CapacityOnlyCompletionStillUpdatesTheRealStore()
    {
        using (var f = new Fixture(capacityBonus: 15f))
        {
            var plan = f.Plan(spendProgress: false);
            Check(plan.Workspace.BuildValidatedJournal().ActionResources.ProgressMutations.Count == 0,
                "Capacity-only fixture included unrelated progress mutations.");
            Check(f.Commit(plan).Status == DRPGPrimaryImpactCommitStatus.Committed && f.ProgressValue == 5f &&
                Focus(f).Capacity == 5f && f.Enemy.CurrentHp == 120f && f.Enemy.CurrentPosture == 80f &&
                f.State.BattleRevision == 1,
                "Capacity-only completion did not prepare and commit the real progress store.");
        }
    }

    internal static void ZeroCapacityAndLateFailureRemainAtomic()
    {
        using (var f = new Fixture(capacityBonus: 15f, baseProgressCapacity: 0f, progressValue: 12f))
        {
            var plan = f.Plan();
            var fact = plan.Workspace.BuildValidatedJournal().ActionResources.WeaponProgressReconciliations[0];
            Check(fact.Before.CurrentValue == 9f && fact.After.CurrentValue == 0f && fact.After.Capacity == 0f &&
                fact.Mutation.OverflowAmount == 9f, "Zero-capacity reconciliation has invalid clamping facts.");
            Check(f.Commit(plan).Status == DRPGPrimaryImpactCommitStatus.Committed && f.ProgressValue == 0f && Focus(f).Capacity == 0f,
                "Removing all capacity failed to empty the progress resource.");
        }
        using (var f = new Fixture(capacityBonus: 15f))
        {
            Check(f.Inventory.TryRestoreWeaponFragments(new[] { new DRPGWeaponFragmentSaveData {
                weaponDefinitionId = "journal-weapon", count = int.MaxValue } }).Success, "Late failure setup failed.");
            var plan = f.Plan();
            Check(plan.Workspace.BuildValidatedJournal().ActionResources.WeaponProgressReconciliations.Count == 1,
                "Late failure fixture never reached prepared capacity facts.");
            Check(f.Commit(plan).Status == DRPGPrimaryImpactCommitStatus.Rejected && f.ProgressValue == 18f && Focus(f).Capacity == 20f &&
                f.Weapon.CurrentDurability == 1 && f.Equipment.EquippedWeapon == f.Weapon && f.Enemy.CurrentHp == 120f &&
                f.State.BattleRevision == 0 && f.Inventory.GetWeaponFragmentCount("journal-weapon") == int.MaxValue,
                "Fragment overflow applied part of the progress/equipment/damage journal.");
        }
    }

    private static DRPGProgressSnapshot Focus(Fixture f, string owner = "journal-player")
    {
        Check(f.Pipeline.TryGetSnapshot(owner, "focus", DRPGProgressScope.Battle, out var snapshot), "Focus is missing.");
        return snapshot;
    }
}
#endif
