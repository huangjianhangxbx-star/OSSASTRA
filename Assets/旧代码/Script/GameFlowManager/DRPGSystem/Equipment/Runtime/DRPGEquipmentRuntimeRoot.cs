using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class DRPGStartingCharacterEquipment
{
    public DRPGCombatantProfileSO profile;
    public string equippedInstanceId;
    public DRPGWeaponDefinitionSO equippedWeapon;
    public DRPGOutfitDefinitionSO outfit;
    public DRPGAccessoryDefinitionSO[] accessories =
        new DRPGAccessoryDefinitionSO[DRPGCharacterEquipmentState.AccessorySlotCount];
}

[Serializable]
public sealed class DRPGStartingBackpackWeapon
{
    public string instanceId;
    public DRPGWeaponDefinitionSO definition;
    public DRPGWeaponAffixDefinitionSO[] affixes;
    public DRPGWeaponCardDefinitionSO[] cards;
    public DRPGWeaponAttachmentDefinitionSO[] attachments;
}

/// <summary>
/// Session owner for DRPG equipment. It bootstraps deterministic test/content
/// instances from authored definitions and exposes one aggregate to menu,
/// battle, recovery, and save systems.
/// </summary>
public sealed class DRPGEquipmentRuntimeRoot : MonoBehaviour
{
    [Header("Capacity")]
    [SerializeField] private int backpackSlotCapacity = 24;
    [SerializeField] private float backpackWeightCapacity = 80f;

    [Header("Starting State")]
    [SerializeField] private List<DRPGStartingCharacterEquipment> characters =
        new List<DRPGStartingCharacterEquipment>();
    [SerializeField] private List<DRPGStartingBackpackWeapon> backpackWeapons =
        new List<DRPGStartingBackpackWeapon>();

    [Header("Save Registry")]
    [SerializeField] private DRPGWeaponDefinitionSO[] weaponDefinitions;
    [SerializeField] private DRPGOutfitDefinitionSO[] outfitDefinitions;
    [SerializeField] private DRPGAccessoryDefinitionSO[] accessoryDefinitions;
    [SerializeField] private DRPGTraitDefinitionSO[] traitDefinitions;
    [SerializeField] private DRPGWeaponAffixDefinitionSO[] affixDefinitions;
    [SerializeField] private DRPGWeaponCardDefinitionSO[] cardDefinitions;
    [SerializeField] private DRPGWeaponAttachmentDefinitionSO[] attachmentDefinitions;
    [SerializeField] private DRPGLootItemDefinitionSO[] lootItemDefinitions;

    private DRPGProgressionService progression;

    public DRPGEquipmentRoot Equipment { get; private set; }
    public bool IsInitialized => Equipment != null;

    public void Initialize(DRPGProgressionService progressionService)
    {
        if (IsInitialized)
            return;
        progression = progressionService ??
            throw new ArgumentNullException(nameof(progressionService));

        var inventory = new DRPGPartyInventoryState(
            Mathf.Max(1, backpackSlotCapacity),
            Mathf.Max(0f, backpackWeightCapacity));
        var registry = new DRPGEquipmentDefinitionRegistry(
            weaponDefinitions,
            outfitDefinitions,
            accessoryDefinitions,
            traitDefinitions,
            affixDefinitions,
            cardDefinitions,
            attachmentDefinitions,
            lootItemDefinitions);
        Equipment = new DRPGEquipmentRoot(inventory, registry);

        for (int i = 0; i < characters.Count; i++)
            RegisterCharacter(characters[i], i);
        for (int i = 0; i < backpackWeapons.Count; i++)
            RegisterBackpackWeapon(backpackWeapons[i], i);
    }

    public IDRPGBattleEquipmentService CreateBattleEquipmentService(
        IEnumerable<BattleParticipantRuntime> participants)
    {
        EnsureInitialized();
        return new DRPGBattleEquipmentAdapter(
            Equipment,
            progression,
            participants);
    }

    public IDRPGBattleWeaponDurabilityService CreateDurabilityService()
    {
        EnsureInitialized();
        return new DRPGBattleWeaponDurabilityService(
            Equipment,
            progression);
    }

    public void ApplyEquipmentBuilds(
        IEnumerable<BattleParticipantRuntime> participants)
    {
        EnsureInitialized();
        if (participants == null)
            return;
        foreach (BattleParticipantRuntime participant in participants)
        {
            if (participant == null || participant.Side != DRPGBattleSide.Player ||
                participant.Profile == null ||
                !Equipment.TryGetCharacter(
                    participant.Profile.CombatantId,
                    out DRPGCombatantProfileSO profile,
                    out DRPGCharacterEquipmentState state))
            {
                continue;
            }
            DRPGCharacterProgressionView view = progression.GetView(profile);
            participant.ReplaceBuild(progression.ResolveBuild(
                profile,
                DRPGResolvedEquipmentSnapshot.FromCharacterEquipment(
                    state,
                    view.ActiveProfession != null
                        ? view.ActiveProfession.ProfessionId
                        : string.Empty,
                    Equipment.DefinitionRegistry)));
        }
    }

    public bool TryGetCharacterEquipment(
        DRPGCombatantProfileSO profile,
        out DRPGCharacterEquipmentState equipment)
    {
        equipment = null;
        return profile != null && IsInitialized &&
            Equipment.TryGetCharacter(profile.CombatantId, out _, out equipment);
    }

    private void RegisterCharacter(
        DRPGStartingCharacterEquipment entry,
        int index)
    {
        if (entry?.profile == null)
            return;
        DRPGWeaponInstance equipped = entry.equippedWeapon != null
            ? CreateWeapon(
                string.IsNullOrWhiteSpace(entry.equippedInstanceId)
                    ? entry.profile.CombatantId + ":equipped"
                    : entry.equippedInstanceId,
                entry.equippedWeapon,
                null,
                null,
                null)
            : null;
        var state = new DRPGCharacterEquipmentState(
            entry.profile.CombatantId,
            equipped);
        if (entry.outfit != null)
            Require(state.TrySetOutfit(entry.outfit), "starting outfit", index);
        if (entry.accessories != null)
        {
            int count = Mathf.Min(
                entry.accessories.Length,
                DRPGCharacterEquipmentState.AccessorySlotCount);
            for (int slot = 0; slot < count; slot++)
            {
                if (entry.accessories[slot] != null)
                {
                    Require(
                        state.TrySetAccessory(slot, entry.accessories[slot]),
                        "starting accessory",
                        index);
                }
            }
        }
        Require(
            Equipment.TryRegisterCharacter(entry.profile, state),
            "character registration",
            index);
    }

    private void RegisterBackpackWeapon(
        DRPGStartingBackpackWeapon entry,
        int index)
    {
        if (entry?.definition == null)
            return;
        string instanceId = string.IsNullOrWhiteSpace(entry.instanceId)
            ? entry.definition.WeaponId + ":backpack:" + index
            : entry.instanceId;
        DRPGWeaponInstance weapon = CreateWeapon(
            instanceId,
            entry.definition,
            entry.affixes,
            entry.cards,
            entry.attachments);
        Require(
            Equipment.PartyInventory.TryAddWeapon(weapon),
            "backpack weapon",
            index);
    }

    private static DRPGWeaponInstance CreateWeapon(
        string instanceId,
        DRPGWeaponDefinitionSO definition,
        IReadOnlyList<DRPGWeaponAffixDefinitionSO> affixes,
        IReadOnlyList<DRPGWeaponCardDefinitionSO> cards,
        IReadOnlyList<DRPGWeaponAttachmentDefinitionSO> attachments)
    {
        return DRPGWeaponInstance.Create(
            instanceId,
            definition,
            FixedRandomSource.Instance,
            affixes,
            cards,
            attachments);
    }

    private void EnsureInitialized()
    {
        if (!IsInitialized)
            throw new InvalidOperationException(
                "DRPGEquipmentRuntimeRoot is not initialized.");
    }

    private static void Require(
        DRPGEquipmentOperationResult result,
        string label,
        int index)
    {
        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Failed to bootstrap {label} #{index}: {result.Message}");
        }
    }

    private sealed class FixedRandomSource : IDRPGRandomSource
    {
        public static readonly FixedRandomSource Instance =
            new FixedRandomSource();

        public float Range(float minimumInclusive, float maximumInclusive)
        {
            return maximumInclusive;
        }

        public int RangeInclusive(int minimumInclusive, int maximumInclusive)
        {
            return maximumInclusive;
        }
    }
}
