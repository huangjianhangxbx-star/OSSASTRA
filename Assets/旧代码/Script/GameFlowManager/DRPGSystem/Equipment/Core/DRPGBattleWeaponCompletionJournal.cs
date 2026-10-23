using System;
using System.Collections.Generic;
using System.Threading;

// Captured equipment ownership plus memoized immutable completion plans. No live
// weapon, equipment, backpack or participant is changed while a plan is built.
internal sealed partial class DRPGBattleEquipmentSnapshot
{
    private static long nextId;
    private readonly DRPGBattleWeaponDurabilityService service;
    private readonly DRPGEquipmentRoot root;
    private readonly Dictionary<string, CapturedWeapon> weapons = new Dictionary<string, CapturedWeapon>(StringComparer.Ordinal);
    private readonly Dictionary<(string, int), DRPGWeaponCompletionPlan> plans = new Dictionary<(string, int), DRPGWeaponCompletionPlan>();
    internal DRPGBattleEquipmentSnapshot(DRPGBattleWeaponDurabilityService service, DRPGEquipmentRoot root,
        IReadOnlyList<BattleParticipantRuntime> participants, IEnumerable<DRPGBattleWeaponResourceBinding> bindings)
    {
        Id = Interlocked.Increment(ref nextId); this.service = service; this.root = root;
        Inventory = root.PartyInventory; Ownership = Inventory.CaptureWeaponOwnership();
        foreach (var binding in bindings)
        {
            BattleParticipantRuntime actor = null;
            foreach (var candidate in participants) if (candidate.RuntimeId == binding.Participant.Value) { actor = candidate; break; }
            if (actor == null || actor.Profile == null) throw new InvalidOperationException("Weapon owner is outside this battlefield.");
            if (!root.TryGetCharacter(actor.Profile.CombatantId, out var profile, out var equipment) ||
                !ReferenceEquals(profile, actor.Profile)) throw new InvalidOperationException("Captured equipment owner differs from the battle actor.");
            if (binding.Weapon.IsBroken && equipment.EquippedWeapon == null) continue;
            if (!ReferenceEquals(equipment.EquippedWeapon, binding.Weapon) ||
                actor.Build.Equipment.WeaponInstanceId != binding.Weapon.InstanceId ||
                !Ownership.Contains(binding.Weapon.InstanceId))
                throw new InvalidOperationException("Weapon, equipment and battle build do not share the same captured owner.");
            weapons.Add(binding.Weapon.InstanceId, new CapturedWeapon(actor, equipment, binding.Weapon));
        }
    }
    internal long Id { get; }
    internal DRPGPartyInventoryState Inventory { get; }
    internal DRPGInventoryWeaponOwnershipSnapshot Ownership { get; }
    internal bool IsOwnedBy(DRPGBattleWeaponDurabilityService candidate) => ReferenceEquals(service, candidate);
    internal bool MatchesActor(BattleParticipantRuntime actor, string weaponId) => weapons.TryGetValue(weaponId, out var captured) &&
        ReferenceEquals(captured.Actor, actor) && ReferenceEquals(captured.Build, actor.Build);

    internal DRPGWeaponCompletionPlan Plan(string weaponId, int units)
    {
        if (plans.TryGetValue((weaponId, units), out var plan)) return plan;
        if (!weapons.TryGetValue(weaponId, out var captured) || units < 0 || units > captured.CurrentDurability || captured.CurrentDurability <= 0)
            throw new InvalidOperationException("Weapon completion exceeds its captured durability or owner.");
        int after = captured.CurrentDurability - units;
        bool removes = after == 0 && captured.Disposition == DRPGWeaponBreakDisposition.DestroyIntoFragments;
        DRPGResolvedEquipmentSnapshot equipment;
        if (after > 0)
        {
            var before = captured.Build.Equipment;
            equipment = new DRPGResolvedEquipmentSnapshot(before.WeaponInstanceId, before.WeaponDefinitionId, before.ActiveProfessionId,
                before.BasicAttackOverride, before.WeaponIntrinsicSkills, before.EquipmentGrantedSkills, before.PassiveModifiers,
                after, captured.MaximumDurability, false, before.AttackPresentationKey, before.ActionModifiers, before.ProgressCapacityModifiers);
        }
        else
        {
            var detached = new DRPGCharacterEquipmentState(captured.CharacterId);
            detached.RestoreSnapshot(captured.EquipmentSnapshot);
            DRPGWeaponInstance replacement = null;
            if (!removes)
            {
                replacement = DRPGWeaponInstance.Restore(captured.WeaponData, captured.Weapon.Definition);
                replacement.RestoreDurability(captured.MaximumDurability, 0);
            }
            var replace = detached.TryReplaceWeapon(captured.Weapon, replacement, string.Empty);
            if (!replace.Success) throw new InvalidOperationException(replace.Message);
            equipment = DRPGResolvedEquipmentSnapshot.FromCharacterEquipment(detached, captured.Build.Equipment.ActiveProfessionId, root.DefinitionRegistry);
        }
        var build = captured.Build.WithWeaponCompletion(equipment);
        plan = new DRPGWeaponCompletionPlan(this, captured.Actor, captured.CharacterId, weaponId, captured.DefinitionId,
            units, after, removes, captured.Disposition, build, checked(captured.EquipmentRevision + (removes ? 1 : 0)));
        plans.Add((weaponId, units), plan);
        return plan;
    }

    internal DRPGBattlePreparedEquipmentCommit Prepare(IReadOnlyList<DRPGWeaponCompletionMutation> completions,
        DRPGBattleStateMutationCapability capability)
    {
        if (!ReferenceEquals(root.PartyInventory, Inventory) || !Inventory.MatchesWeaponOwnership(Ownership))
            throw new InvalidOperationException("The equipment root/backpack changed before weapon completion.");
        foreach (var captured in weapons.Values)
        {
            if (!root.TryGetCharacter(captured.CharacterId, out var profile, out var equipment) ||
                !ReferenceEquals(profile, captured.Actor.Profile) || !ReferenceEquals(equipment, captured.Equipment) ||
                equipment.Revision != captured.EquipmentRevision || equipment.MutationEpoch != captured.EquipmentEpoch ||
                !ReferenceEquals(equipment.EquippedWeapon, captured.Weapon) ||
                !ReferenceEquals(captured.Actor.Build, captured.Build))
                throw new InvalidOperationException("Captured equipment, revision or battle build drifted before completion.");
        }
        foreach (var completion in completions)
        {
            if (completion == null || !ReferenceEquals(completion.Plan, Plan(completion.Plan.WeaponId, completion.Plan.Units)))
                throw new InvalidOperationException("Weapon completion does not belong to this captured plan.");
            completion.Validate();
        }
        var inventory = Inventory.PrepareWeaponBreaks(Ownership, completions);
        var equipmentChanges = new List<(DRPGCharacterEquipmentState Equipment, int Revision)>();
        foreach (var completion in completions)
            if (completion.Plan.RemovesWeapon) equipmentChanges.Add((weapons[completion.Plan.WeaponId].Equipment, completion.Plan.EquipmentRevisionAfter));
        return new DRPGBattlePreparedEquipmentCommit(Inventory, inventory, equipmentChanges, completions, capability);
    }

    private sealed class CapturedWeapon
    {
        internal CapturedWeapon(BattleParticipantRuntime actor, DRPGCharacterEquipmentState equipment, DRPGWeaponInstance weapon)
        {
            Actor = actor; Build = actor.Build; CharacterId = actor.Profile.CombatantId; Equipment = equipment; Weapon = weapon;
            EquipmentRevision = equipment.Revision; EquipmentSnapshot = equipment.CaptureSnapshot(); WeaponData = weapon.ExportSnapshot();
            EquipmentEpoch = equipment.MutationEpoch;
            DefinitionId = weapon.Definition.WeaponId; Disposition = weapon.Definition.BreakDisposition;
            CurrentDurability = weapon.CurrentDurability; MaximumDurability = weapon.CurrentMaxDurability;
        }
        internal BattleParticipantRuntime Actor { get; }
        internal DRPGResolvedCombatantBuild Build { get; }
        internal string CharacterId { get; }
        internal string DefinitionId { get; }
        internal DRPGWeaponBreakDisposition Disposition { get; }
        internal DRPGCharacterEquipmentState Equipment { get; }
        internal DRPGWeaponInstance Weapon { get; }
        internal int EquipmentRevision { get; }
        internal long EquipmentEpoch { get; }
        internal int CurrentDurability { get; }
        internal int MaximumDurability { get; }
        internal DRPGCharacterEquipmentState.Snapshot EquipmentSnapshot { get; }
        internal DRPGWeaponInstanceSaveData WeaponData { get; }
    }
}

internal sealed class DRPGWeaponCompletionPlan
{
    internal DRPGWeaponCompletionPlan(DRPGBattleEquipmentSnapshot source, BattleParticipantRuntime actor, string characterId,
        string weaponId, string definitionId, int units, int durabilityAfter, bool removesWeapon,
        DRPGWeaponBreakDisposition disposition, DRPGResolvedCombatantBuild replacementBuild, int equipmentRevisionAfter)
    {
        Source = source; Actor = actor; CharacterId = characterId; WeaponId = weaponId; DefinitionId = definitionId;
        Units = units; DurabilityAfter = durabilityAfter; RemovesWeapon = removesWeapon; Disposition = disposition;
        ReplacementBuild = replacementBuild; EquipmentRevisionAfter = equipmentRevisionAfter;
    }
    internal DRPGBattleEquipmentSnapshot Source { get; }
    internal BattleParticipantRuntime Actor { get; }
    internal string CharacterId { get; }
    internal string WeaponId { get; }
    internal string DefinitionId { get; }
    internal int Units { get; }
    internal int DurabilityAfter { get; }
    internal bool RemovesWeapon { get; }
    internal DRPGWeaponBreakDisposition Disposition { get; }
    internal DRPGResolvedCombatantBuild ReplacementBuild { get; }
    internal int EquipmentRevisionAfter { get; }
}

internal sealed class DRPGWeaponCompletionMutation
{
    internal DRPGWeaponCompletionMutation(DRPGWeaponCompletionPlan plan, float hp, float mana, float posture)
    {
        Plan = plan; HpBefore = hp; ManaBefore = mana; PostureBefore = posture;
        HpAfter = Math.Min(hp, plan.ReplacementBuild.EffectiveMaxHp);
        ManaAfter = Math.Min(mana, plan.ReplacementBuild.EffectiveMaxMp);
        PostureAfter = Math.Min(posture, plan.ReplacementBuild.EffectiveMaxPosture);
    }
    internal DRPGWeaponCompletionPlan Plan { get; }
    internal float HpBefore { get; }
    internal float ManaBefore { get; }
    internal float PostureBefore { get; }
    internal float HpAfter { get; }
    internal float ManaAfter { get; }
    internal float PostureAfter { get; }
    internal void Validate()
    {
        if (Plan.ReplacementBuild == null || Plan.ReplacementBuild.Profile.CombatantId != Plan.CharacterId ||
            !Finite(HpBefore) || !Finite(ManaBefore) || !Finite(PostureBefore) ||
            !Finite(HpAfter) || !Finite(ManaAfter) || !Finite(PostureAfter))
            throw new InvalidOperationException("Weapon build reconciliation has invalid resources or actor identity.");
    }
    private static bool Finite(float value) => value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
}

internal sealed class DRPGBattlePreparedEquipmentCommit
{
    private readonly DRPGPartyInventoryState inventory;
    private readonly DRPGPreparedInventoryWeaponBreaks preparedInventory;
    private readonly IReadOnlyList<(DRPGCharacterEquipmentState Equipment, int Revision)> equipment;
    private readonly IReadOnlyList<DRPGWeaponCompletionMutation> completions;
    private readonly DRPGBattleStateMutationCapability capability;
    internal DRPGBattlePreparedEquipmentCommit(DRPGPartyInventoryState inventory, DRPGPreparedInventoryWeaponBreaks preparedInventory,
        IReadOnlyList<(DRPGCharacterEquipmentState Equipment, int Revision)> equipment,
        IReadOnlyList<DRPGWeaponCompletionMutation> completions, DRPGBattleStateMutationCapability capability)
    { this.inventory = inventory; this.preparedInventory = preparedInventory; this.equipment = equipment; this.completions = completions; this.capability = capability; }
    internal void Apply(DRPGBattleStateMutationCapability supplied = null)
    {
        if (capability == null || !ReferenceEquals(capability, supplied)) throw new InvalidOperationException("Equipment commit requires its State's private capability.");
        inventory.ApplyPreparedWeaponBreaks(preparedInventory);
        foreach (var change in equipment) change.Equipment.ApplyPreparedWeaponBreak(change.Revision);
        foreach (var completion in completions)
            completion.Plan.Actor.ApplyCommittedWeaponBuild(capability, completion.Plan.ReplacementBuild,
                completion.HpAfter, completion.ManaAfter, completion.PostureAfter);
    }
}
