using System;
using System.Collections.Generic;

public sealed class DRPGEquipmentEligibilityService
{
    private readonly DRPGProgressionService progression;

    public DRPGEquipmentEligibilityService(
        DRPGProgressionService progression)
    {
        this.progression = progression ??
            throw new ArgumentNullException(nameof(progression));
    }

    public DRPGEquipmentOperationResult Evaluate(
        DRPGCombatantProfileSO profile,
        DRPGCharacterEquipmentState equipment,
        DRPGWeaponInstance weapon,
        DRPGCharacterProgressionView progressionView,
        string requestedProfessionId = null)
    {
        if (profile == null || equipment == null || weapon == null)
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.InvalidRequest,
                "Profile, equipment state, and weapon are required.");
        }
        if (!string.Equals(
                profile.CombatantId,
                equipment.CharacterId,
                StringComparison.Ordinal))
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.CharacterNotFound,
                "Equipment state does not belong to the requested character.");
        }

        DRPGCharacterProgressionDefinitionSO definition =
            profile.ProgressionDefinition;
        if (definition == null)
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.MissingProgression,
                "Character has no progression definition.");
        }

        DRPGWeaponDefinitionSO weaponDefinition = weapon.Definition;
        if (weaponDefinition.OwnershipKind ==
            DRPGWeaponOwnershipKind.Signature)
        {
            if (definition.PersonnelKind == DRPGPersonnelKind.Employee)
            {
                return DRPGEquipmentOperationResult.Fail(
                    DRPGEquipmentError.EmployeeCannotEquipSignature,
                    "Employees may equip general weapons only.");
            }
            if (!string.Equals(
                    weaponDefinition.SignatureCharacterId,
                    profile.CombatantId,
                    StringComparison.Ordinal))
            {
                return DRPGEquipmentOperationResult.Fail(
                    DRPGEquipmentError.SignatureOwnerMismatch,
                    "Signature weapon belongs to another character.");
            }
        }

        if (progressionView == null ||
            !ReferenceEquals(progressionView.Profile, profile))
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.MissingProgression,
                "Eligibility requires the character's permanent progression view.");
        }

        DRPGEquipmentOperationResult professionResult = ResolveProfession(
            profile,
            progressionView.ActiveProfession,
            weaponDefinition,
            requestedProfessionId);
        if (!professionResult.Success)
            return professionResult;

        IReadOnlyList<DRPGAttributeRequirement> requirements =
            weaponDefinition.Requirements;
        for (int i = 0; i < requirements.Count; i++)
        {
            if (!requirements[i].IsMetBy(progressionView.Attributes))
            {
                return DRPGEquipmentOperationResult.Fail(
                    DRPGEquipmentError.AttributeRequirementNotMet,
                    "Permanent attribute requirement is not met: " +
                    requirements[i].Attribute + " " +
                    requirements[i].MinimumValue);
            }
        }

        return professionResult;
    }

    private DRPGEquipmentOperationResult ResolveProfession(
        DRPGCombatantProfileSO profile,
        DRPGProfessionSO activeProfession,
        DRPGWeaponDefinitionSO weapon,
        string requestedProfessionId)
    {
        DRPGCharacterProgressionDefinitionSO definition =
            profile.ProgressionDefinition;
        if (!string.IsNullOrWhiteSpace(requestedProfessionId))
        {
            DRPGProfessionSO requested =
                definition.FindProfession(requestedProfessionId);
            if (requested == null)
            {
                return DRPGEquipmentOperationResult.Fail(
                    DRPGEquipmentError.ProfessionUnavailable,
                    "Requested profession is unavailable.");
            }
            if (!progression.IsProfessionUnlocked(
                    profile,
                    requested.ProfessionId))
            {
                return DRPGEquipmentOperationResult.Fail(
                    DRPGEquipmentError.ProfessionLocked,
                    "Requested profession is locked.");
            }
            if (!weapon.IsCompatibleWith(requested))
            {
                return DRPGEquipmentOperationResult.Fail(
                    DRPGEquipmentError.ProfessionIncompatible,
                    "Weapon is incompatible with the requested profession.");
            }
            return DRPGEquipmentOperationResult.Ok(requested);
        }

        if (activeProfession != null && weapon.IsCompatibleWith(activeProfession))
            return DRPGEquipmentOperationResult.Ok(activeProfession);

        bool hasLockedCompatibleProfession = false;
        IReadOnlyList<DRPGProfessionSO> available =
            definition.AvailableProfessions;
        for (int i = 0; i < available.Count; i++)
        {
            DRPGProfessionSO profession = available[i];
            if (profession == null || !weapon.IsCompatibleWith(profession))
                continue;
            if (progression.IsProfessionUnlocked(profile, profession.ProfessionId))
                return DRPGEquipmentOperationResult.Ok(profession);
            hasLockedCompatibleProfession = true;
        }

        return DRPGEquipmentOperationResult.Fail(
            hasLockedCompatibleProfession
                ? DRPGEquipmentError.ProfessionLocked
                : DRPGEquipmentError.ProfessionIncompatible,
            hasLockedCompatibleProfession
                ? "Every compatible profession is locked."
                : "Weapon is incompatible with this character's professions.");
    }
}
