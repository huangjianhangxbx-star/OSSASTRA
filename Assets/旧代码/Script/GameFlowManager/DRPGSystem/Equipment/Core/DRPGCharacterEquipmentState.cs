using System;
using System.Collections.Generic;

public sealed class DRPGCharacterEquipmentState
{
    public const int AccessorySlotCount = 3;

    internal sealed class Snapshot
    {
        public Snapshot(
            DRPGWeaponInstance equippedWeapon,
            DRPGOutfitDefinitionSO outfit,
            DRPGAccessoryDefinitionSO[] accessories,
            DRPGTraitPoolSnapshot traitPool,
            int revision,
            Dictionary<string, string> lastWeaponByProfession)
        {
            EquippedWeapon = equippedWeapon;
            Outfit = outfit;
            Accessories = accessories;
            TraitPool = traitPool;
            Revision = revision;
            LastWeaponByProfession = lastWeaponByProfession;
        }

        public DRPGWeaponInstance EquippedWeapon { get; }
        public DRPGOutfitDefinitionSO Outfit { get; }
        public DRPGAccessoryDefinitionSO[] Accessories { get; }
        public DRPGTraitPoolSnapshot TraitPool { get; }
        public int Revision { get; }
        public Dictionary<string, string> LastWeaponByProfession { get; }
    }

    private readonly Dictionary<string, string> lastWeaponByProfession =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly DRPGAccessoryDefinitionSO[] accessories =
        new DRPGAccessoryDefinitionSO[AccessorySlotCount];
    private readonly IReadOnlyList<DRPGAccessoryDefinitionSO> accessoryView;

    public DRPGCharacterEquipmentState(
        string characterId,
        DRPGWeaponInstance equippedWeapon = null)
    {
        if (string.IsNullOrWhiteSpace(characterId))
            throw new ArgumentException("Character ID is required.", nameof(characterId));
        CharacterId = characterId;
        EquippedWeapon = equippedWeapon;
        accessoryView = Array.AsReadOnly(accessories);
        TraitPool = new DRPGTraitPoolState();
    }

    public string CharacterId { get; }
    public DRPGWeaponInstance EquippedWeapon { get; private set; }
    public DRPGOutfitDefinitionSO CurrentOutfit { get; private set; }
    public IReadOnlyList<DRPGAccessoryDefinitionSO> Accessories => accessoryView;
    public DRPGTraitPoolState TraitPool { get; }
    public IReadOnlyList<string> ResolvedTraitIds => TraitPool.ResolvedTraitIds;
    private int revision;
    // Unlike the save/rollback revision, this epoch never rewinds when a memento
    // restores old values. Captured battle completions must reject such ABA changes.
    internal long MutationEpoch { get; private set; }
    public int Revision
    {
        get => revision;
        private set { revision = value; MutationEpoch = unchecked(MutationEpoch + 1); }
    }

    internal void ApplyPreparedWeaponBreak(int revisionAfter)
    {
        EquippedWeapon = null;
        Revision = revisionAfter;
    }

    public string GetLastWeaponInstanceId(string professionId)
    {
        if (string.IsNullOrWhiteSpace(professionId))
            return null;
        return lastWeaponByProfession.TryGetValue(
            professionId,
            out string instanceId)
            ? instanceId
            : null;
    }

    internal IReadOnlyDictionary<string, string> ExportLastWeaponMemory()
    {
        return new Dictionary<string, string>(
            lastWeaponByProfession,
            StringComparer.Ordinal);
    }

    internal DRPGEquipmentOperationResult ImportLastWeaponMemory(
        IReadOnlyList<DRPGProfessionWeaponMemorySaveData> entries)
    {
        var restored = new Dictionary<string, string>(StringComparer.Ordinal);
        if (entries != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                DRPGProfessionWeaponMemorySaveData entry = entries[i];
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.professionId) ||
                    string.IsNullOrWhiteSpace(entry.weaponInstanceId) ||
                    restored.ContainsKey(entry.professionId))
                {
                    return DRPGEquipmentOperationResult.Fail(
                        DRPGEquipmentError.InvalidSaveData,
                        "Last-weapon memory contains an invalid or duplicate entry.");
                }
                restored.Add(entry.professionId, entry.weaponInstanceId);
            }
        }

        lastWeaponByProfession.Clear();
        foreach (KeyValuePair<string, string> pair in restored)
            lastWeaponByProfession.Add(pair.Key, pair.Value);
        return DRPGEquipmentOperationResult.Ok();
    }

    public DRPGEquipmentOperationResult TrySetOutfit(
        DRPGOutfitDefinitionSO outfit)
    {
        if (outfit != null && string.IsNullOrWhiteSpace(outfit.OutfitId))
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.InvalidRequest,
                "Outfit ID is required.");
        }

        DRPGTraitPoolSnapshot traitSnapshot = TraitPool.ExportSnapshot();
        if (CurrentOutfit != null &&
            !string.IsNullOrWhiteSpace(CurrentOutfit.GrantedTraitId))
        {
            DRPGEquipmentOperationResult removed =
                TraitPool.TryRemoveSourceTrait(GetOutfitSource(CurrentOutfit));
            if (!removed.Success)
                return removed;
        }
        if (outfit != null && !string.IsNullOrWhiteSpace(outfit.GrantedTraitId))
        {
            DRPGEquipmentOperationResult bound = TraitPool.TryBindSourceTrait(
                GetOutfitSource(outfit),
                outfit.GrantedTraitId);
            if (!bound.Success)
            {
                TraitPool.ImportSnapshot(traitSnapshot);
                return bound;
            }
        }
        CurrentOutfit = outfit;
        Revision++;
        return DRPGEquipmentOperationResult.Ok();
    }

    public DRPGEquipmentOperationResult TrySetAccessory(
        int slotIndex,
        DRPGAccessoryDefinitionSO accessory)
    {
        if (slotIndex < 0 || slotIndex >= AccessorySlotCount)
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.InvalidRequest,
                "Accessory slot index is outside the fixed three-slot range.");
        }
        accessories[slotIndex] = accessory;
        Revision++;
        return DRPGEquipmentOperationResult.Ok();
    }

    internal DRPGEquipmentOperationResult TryReplaceWeapon(
        DRPGWeaponInstance expectedCurrent,
        DRPGWeaponInstance next,
        string professionId)
    {
        if (!ReferenceEquals(EquippedWeapon, expectedCurrent))
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.StateConflict,
                "Equipped weapon changed after validation.");
        }

        EquippedWeapon = next;
        if (next != null && !string.IsNullOrWhiteSpace(professionId))
            lastWeaponByProfession[professionId] = next.InstanceId;
        Revision++;
        return DRPGEquipmentOperationResult.Ok();
    }

    internal Snapshot CaptureSnapshot()
    {
        return new Snapshot(
            EquippedWeapon,
            CurrentOutfit,
            (DRPGAccessoryDefinitionSO[])accessories.Clone(),
            TraitPool.ExportSnapshot(),
            Revision,
            new Dictionary<string, string>(
                lastWeaponByProfession,
                StringComparer.Ordinal));
    }

    internal void RestoreSnapshot(Snapshot snapshot)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));
        EquippedWeapon = snapshot.EquippedWeapon;
        CurrentOutfit = snapshot.Outfit;
        Array.Copy(snapshot.Accessories, accessories, AccessorySlotCount);
        DRPGEquipmentOperationResult traits =
            TraitPool.ImportSnapshot(snapshot.TraitPool);
        if (!traits.Success)
            throw new InvalidOperationException(traits.Message);
        Revision = snapshot.Revision;
        lastWeaponByProfession.Clear();
        foreach (KeyValuePair<string, string> pair in
                 snapshot.LastWeaponByProfession)
        {
            lastWeaponByProfession.Add(pair.Key, pair.Value);
        }
    }

    private static string GetOutfitSource(DRPGOutfitDefinitionSO outfit)
    {
        return "outfit:" + outfit.OutfitId;
    }
}
