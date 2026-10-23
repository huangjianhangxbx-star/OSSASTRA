using System;
using System.Collections.Generic;

public sealed class DRPGEquipmentRoot
{
    private readonly Dictionary<string, DRPGCombatantProfileSO> profiles =
        new Dictionary<string, DRPGCombatantProfileSO>(StringComparer.Ordinal);
    private readonly Dictionary<string, DRPGCharacterEquipmentState> states =
        new Dictionary<string, DRPGCharacterEquipmentState>(StringComparer.Ordinal);
    private readonly List<DRPGQuarantinedWeaponEntry>
        quarantinedWeaponEntries = new List<DRPGQuarantinedWeaponEntry>();
    private readonly List<string> lastImportWarnings = new List<string>();

    public DRPGEquipmentRoot(DRPGPartyInventoryState partyInventory)
        : this(partyInventory, null)
    {
    }

    public DRPGEquipmentRoot(
        DRPGPartyInventoryState partyInventory,
        DRPGEquipmentDefinitionRegistry definitionRegistry)
    {
        PartyInventory = partyInventory ??
            throw new ArgumentNullException(nameof(partyInventory));
        DefinitionRegistry = definitionRegistry;
        FieldRecovery = new DRPGFieldRecoveryService(this);
    }

    public DRPGPartyInventoryState PartyInventory { get; private set; }
    public DRPGEquipmentDefinitionRegistry DefinitionRegistry { get; }
    public DRPGFieldRecoveryService FieldRecovery { get; private set; }
    public IReadOnlyList<DRPGQuarantinedWeaponEntry>
        QuarantinedWeaponEntries => quarantinedWeaponEntries.AsReadOnly();
    public IReadOnlyList<string> LastImportWarnings =>
        lastImportWarnings.AsReadOnly();
    internal IReadOnlyDictionary<string, DRPGCombatantProfileSO> Profiles =>
        profiles;
    internal IReadOnlyDictionary<string, DRPGCharacterEquipmentState> States =>
        states;

    public DRPGEquipmentSaveData ExportSnapshot()
    {
        return DRPGEquipmentSaveCodec.Export(this);
    }

    public DRPGEquipmentOperationResult ImportSnapshot(
        DRPGEquipmentSaveData snapshot)
    {
        return DRPGEquipmentSaveCodec.Import(this, snapshot);
    }

    public DRPGEquipmentOperationResult TryRegisterCharacter(
        DRPGCombatantProfileSO profile,
        DRPGCharacterEquipmentState equipment)
    {
        if (profile == null || equipment == null ||
            string.IsNullOrWhiteSpace(profile.CombatantId) ||
            !string.Equals(
                profile.CombatantId,
                equipment.CharacterId,
                StringComparison.Ordinal))
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.CharacterNotFound,
                "Profile and equipment state must identify the same character.");
        }
        if (profiles.ContainsKey(profile.CombatantId) ||
            states.ContainsKey(profile.CombatantId))
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.StateConflict,
                "Character is already registered in the equipment root.");
        }

        DRPGEquipmentOperationResult ownership =
            PartyInventory.TryReserveEquippedWeapon(equipment.EquippedWeapon);
        if (!ownership.Success)
            return ownership;

        profiles.Add(profile.CombatantId, profile);
        states.Add(profile.CombatantId, equipment);
        return DRPGEquipmentOperationResult.Ok();
    }

    public bool TryGetCharacter(
        string characterId,
        out DRPGCombatantProfileSO profile,
        out DRPGCharacterEquipmentState equipment)
    {
        profile = null;
        equipment = null;
        return !string.IsNullOrWhiteSpace(characterId) &&
            profiles.TryGetValue(characterId, out profile) &&
            states.TryGetValue(characterId, out equipment);
    }

    public DRPGEquipmentOperationResult
        TryConvertBrokenEquippedWeaponToFragments(
            string characterId,
            DRPGWeaponInstance expectedWeapon,
            int fragmentCount)
    {
        if (!TryGetCharacter(characterId, out _, out var equipment))
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.CharacterNotFound,
                "Character is not registered in the equipment root.");
        }
        if (expectedWeapon == null ||
            !ReferenceEquals(equipment.EquippedWeapon, expectedWeapon) ||
            !expectedWeapon.IsBroken)
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.InvalidInstanceState,
                "The expected broken weapon is no longer equipped.");
        }

        DRPGCharacterEquipmentState.Snapshot snapshot =
            equipment.CaptureSnapshot();
        DRPGEquipmentOperationResult removed = equipment.TryReplaceWeapon(
            expectedWeapon,
            null,
            string.Empty);
        if (!removed.Success)
            return removed;

        DRPGEquipmentOperationResult fragments =
            PartyInventory.TryConvertReservedBrokenWeaponToFragments(
                expectedWeapon,
                fragmentCount);
        if (!fragments.Success)
            equipment.RestoreSnapshot(snapshot);
        return fragments;
    }

    internal void ReplaceImportedState(
        DRPGEquipmentRoot imported,
        IReadOnlyList<DRPGQuarantinedWeaponEntry> quarantined,
        IReadOnlyList<string> warnings)
    {
        PartyInventory = imported.PartyInventory;
        profiles.Clear();
        foreach (KeyValuePair<string, DRPGCombatantProfileSO> pair in
                 imported.profiles)
        {
            profiles.Add(pair.Key, pair.Value);
        }
        states.Clear();
        foreach (KeyValuePair<string, DRPGCharacterEquipmentState> pair in
                 imported.states)
        {
            states.Add(pair.Key, pair.Value);
        }
        FieldRecovery = imported.FieldRecovery.CloneFor(this);
        quarantinedWeaponEntries.Clear();
        if (quarantined != null)
            quarantinedWeaponEntries.AddRange(quarantined);
        lastImportWarnings.Clear();
        if (warnings != null)
            lastImportWarnings.AddRange(warnings);
    }
}
