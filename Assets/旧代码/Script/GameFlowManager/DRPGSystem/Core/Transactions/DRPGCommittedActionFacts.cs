using System;
using System.Collections.Generic;

// Notification surface: no workspace, reservation, equipment build, runtime
// participant, service or mutable resource snapshot escapes through this type.
internal sealed class DRPGCommittedActionFacts
{
    internal DRPGCommittedActionFacts(DRPGBattleActionResolutionLedger ledger)
    {
        Identity = ledger.Identity; Fingerprint = ledger.Fingerprint; Events = ledger.Events;
        Costs = ledger.MutationJournal.ActionCosts;
        var resources = ledger.MutationJournal.ActionResources;
        Progress = resources?.ProgressMutations ?? Array.Empty<DRPGProgressMutationResult>();
        Resonance = resources?.ResonanceMutations ?? Array.Empty<DRPGActionResonanceMutation>();
        WeaponEvents = resources?.WeaponEvents ?? Array.Empty<DRPGBattleWeaponUseEvent>();
        var weapons = new List<DRPGCommittedWeaponFact>(); var capacity = new List<DRPGProgressMutationResult>();
        if (resources != null)
        {
            foreach (var use in resources.WeaponUses)
            {
                if (!resources.Snapshot.TryGetWeapon(use.WeaponId, out var before))
                    throw new InvalidOperationException("Committed weapon fact has no captured weapon.");
                DRPGWeaponCompletionMutation completion = null;
                foreach (var candidate in resources.WeaponCompletions)
                    if (candidate.Plan.WeaponId == use.WeaponId) { completion = candidate; break; }
                weapons.Add(new DRPGCommittedWeaponFact(before, use, completion));
            }
            foreach (var reconciliation in resources.WeaponProgressReconciliations) capacity.Add(reconciliation.Mutation);
        }
        Weapons = Array.AsReadOnly(weapons.ToArray()); WeaponProgress = Array.AsReadOnly(capacity.ToArray());
    }
    internal ActionIdentity Identity { get; }
    internal ulong Fingerprint { get; }
    internal IReadOnlyList<DRPGCommittedActionEvent> Events { get; }
    internal DRPGActionCostReceipt Costs { get; }
    internal IReadOnlyList<DRPGProgressMutationResult> Progress { get; }
    internal IReadOnlyList<DRPGActionResonanceMutation> Resonance { get; }
    internal IReadOnlyList<DRPGBattleWeaponUseEvent> WeaponEvents { get; }
    internal IReadOnlyList<DRPGCommittedWeaponFact> Weapons { get; }
    internal IReadOnlyList<DRPGProgressMutationResult> WeaponProgress { get; }
}

internal sealed class DRPGCommittedWeaponFact
{
    internal DRPGCommittedWeaponFact(DRPGBattleWeaponResourceSnapshot before, DRPGBattleWeaponUseMutation use,
        DRPGWeaponCompletionMutation completion)
    {
        Participant = before.Participant; WeaponId = before.WeaponId; Units = use.Units;
        DurabilityBefore = before.CurrentDurability; DurabilityAfter = use.DurabilityAfter;
        BreakDisposition = before.BreakDisposition; DefinitionId = completion?.Plan.DefinitionId ?? string.Empty;
        WasRemoved = completion?.Plan.RemovesWeapon ?? false; FragmentCount = WasRemoved ? 1 : 0;
        BuildWasReconciled = completion != null;
        HpBefore = completion?.HpBefore ?? 0f; HpAfter = completion?.HpAfter ?? 0f;
        ManaBefore = completion?.ManaBefore ?? 0f; ManaAfter = completion?.ManaAfter ?? 0f;
        PostureBefore = completion?.PostureBefore ?? 0f; PostureAfter = completion?.PostureAfter ?? 0f;
        MaxHpAfter = completion?.Plan.ReplacementBuild.EffectiveMaxHp ?? 0f;
        MaxPostureAfter = completion?.Plan.ReplacementBuild.EffectiveMaxPosture ?? 0f;
    }
    internal ParticipantId Participant { get; }
    internal string WeaponId { get; }
    internal string DefinitionId { get; }
    internal int Units { get; }
    internal int DurabilityBefore { get; }
    internal int DurabilityAfter { get; }
    internal DRPGWeaponBreakDisposition BreakDisposition { get; }
    internal bool WasRemoved { get; }
    internal int FragmentCount { get; }
    internal bool BuildWasReconciled { get; }
    internal float HpBefore { get; }
    internal float HpAfter { get; }
    internal float ManaBefore { get; }
    internal float ManaAfter { get; }
    internal float PostureBefore { get; }
    internal float PostureAfter { get; }
    internal float MaxHpAfter { get; }
    internal float MaxPostureAfter { get; }
}
