using System;
using System.Collections.Generic;

/// <summary>
/// Adapts the persistent equipment aggregate to one battle roster. Runtime IDs
/// never leak into save data; the stable combatant ID remains authoritative.
/// </summary>
public sealed class DRPGBattleEquipmentAdapter : IDRPGBattleEquipmentService
{
    private readonly DRPGEquipmentRoot equipmentRoot;
    private readonly DRPGProgressionService progression;
    private readonly DRPGWeaponEquipService equipService;
    private readonly DRPGEquipmentEligibilityService eligibility;
    private readonly Dictionary<int, string> characterByRuntimeId =
        new Dictionary<int, string>();

    public DRPGBattleEquipmentAdapter(
        DRPGEquipmentRoot root,
        DRPGProgressionService progressionService,
        IEnumerable<BattleParticipantRuntime> participants)
    {
        equipmentRoot = root ?? throw new ArgumentNullException(nameof(root));
        progression = progressionService ??
            throw new ArgumentNullException(nameof(progressionService));
        equipService = new DRPGWeaponEquipService(progression, equipmentRoot);
        eligibility = new DRPGEquipmentEligibilityService(progression);

        if (participants == null)
            throw new ArgumentNullException(nameof(participants));
        foreach (BattleParticipantRuntime participant in participants)
        {
            if (participant == null || participant.Side != DRPGBattleSide.Player ||
                participant.Profile == null)
            {
                continue;
            }
            characterByRuntimeId[participant.RuntimeId] =
                participant.Profile.CombatantId;
        }
    }

    public IReadOnlyList<DRPGBattleWeaponCandidate> GetCandidates(
        int actorRuntimeId)
    {
        var result = new List<DRPGBattleWeaponCandidate>();
        if (!TryResolveCharacter(
                actorRuntimeId,
                out DRPGCombatantProfileSO profile,
                out DRPGCharacterEquipmentState equipment))
        {
            return result.AsReadOnly();
        }

        DRPGCharacterProgressionView progressionView;
        try
        {
            progressionView = progression.GetView(profile);
        }
        catch (InvalidOperationException)
        {
            return result.AsReadOnly();
        }

        foreach (DRPGWeaponInstance weapon in
                 equipmentRoot.PartyInventory.BackpackWeapons)
        {
            if (weapon == null)
                continue;
            DRPGEquipmentOperationResult evaluation = eligibility.Evaluate(
                profile,
                equipment,
                weapon,
                progressionView);
            if (evaluation.Success)
            {
                DRPGInventoryReplacementPreview capacity =
                    equipmentRoot.PartyInventory.PreviewWeaponReplacement(
                        weapon.InstanceId,
                        equipment.EquippedWeapon);
                if (!capacity.Success)
                    evaluation = DRPGEquipmentOperationResult.Fail(
                        capacity.Error,
                        capacity.Message);
            }

            bool capacityExemptReturn = equipment.EquippedWeapon != null &&
                equipmentRoot.PartyInventory.UsedSlots >=
                equipmentRoot.PartyInventory.SlotCapacity;
            result.Add(new DRPGBattleWeaponCandidate(
                weapon.InstanceId,
                weapon,
                evaluation.Success,
                evaluation.Success ? string.Empty : evaluation.Message,
                evaluation.ResolvedProfession,
                capacityExemptReturn));
        }

        result.Sort(CompareCandidates);
        return result.AsReadOnly();
    }

    public DRPGEquipmentOperationResult TrySwitchWeapon(
        BattleParticipantRuntime actor,
        string weaponInstanceId,
        out DRPGResolvedCombatantBuild replacementBuild)
    {
        replacementBuild = null;
        if (actor == null || !TryResolveCharacter(
                actor.RuntimeId,
                out DRPGCombatantProfileSO profile,
                out DRPGCharacterEquipmentState equipment))
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.CharacterNotFound,
                "Battle actor is not registered in the equipment root.");
        }

        DRPGEquipmentOperationResult result = equipService.TryEquipWeapon(
            profile,
            equipment,
            weaponInstanceId);
        if (!result.Success)
            return result;

        DRPGWeaponInstance equipped = equipment.EquippedWeapon;
        string professionId = result.ResolvedProfession != null
            ? result.ResolvedProfession.ProfessionId
            : string.Empty;
        replacementBuild = progression.ResolveBuild(
            profile,
            DRPGResolvedEquipmentSnapshot.FromCharacterEquipment(
                equipment,
                professionId,
                equipmentRoot.DefinitionRegistry));
        return result;
    }

    private bool TryResolveCharacter(
        int actorRuntimeId,
        out DRPGCombatantProfileSO profile,
        out DRPGCharacterEquipmentState equipment)
    {
        profile = null;
        equipment = null;
        return characterByRuntimeId.TryGetValue(
                actorRuntimeId,
                out string characterId) &&
            equipmentRoot.TryGetCharacter(characterId, out profile, out equipment);
    }

    private static int CompareCandidates(
        DRPGBattleWeaponCandidate left,
        DRPGBattleWeaponCandidate right)
    {
        int eligibilityOrder = right.IsEligible.CompareTo(left.IsEligible);
        if (eligibilityOrder != 0)
            return eligibilityOrder;
        string leftName = left.Weapon != null
            ? left.Weapon.Definition.DisplayName
            : left.WeaponInstanceId;
        string rightName = right.Weapon != null
            ? right.Weapon.Definition.DisplayName
            : right.WeaponInstanceId;
        return string.Compare(leftName, rightName, StringComparison.Ordinal);
    }
}
