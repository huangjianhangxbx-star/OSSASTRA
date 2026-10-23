using System;

public sealed class DRPGWeaponEquipService
{
    private readonly DRPGProgressionService progression;
    private readonly DRPGPartyInventoryState inventory;
    private readonly DRPGEquipmentEligibilityService eligibility;
    private readonly DRPGEquipmentRoot root;

    public DRPGWeaponEquipService(
        DRPGProgressionService progression,
        DRPGPartyInventoryState inventory)
    {
        this.progression = progression ??
            throw new ArgumentNullException(nameof(progression));
        this.inventory = inventory ??
            throw new ArgumentNullException(nameof(inventory));
        eligibility = new DRPGEquipmentEligibilityService(progression);
    }

    public DRPGWeaponEquipService(
        DRPGProgressionService progression,
        DRPGEquipmentRoot root)
    {
        this.progression = progression ??
            throw new ArgumentNullException(nameof(progression));
        this.root = root ?? throw new ArgumentNullException(nameof(root));
        inventory = root.PartyInventory;
        eligibility = new DRPGEquipmentEligibilityService(progression);
    }

    public DRPGEquipmentOperationResult TryEquipWeapon(
        string characterId,
        string candidateInstanceId,
        string requestedProfessionId = null)
    {
        if (root == null ||
            !root.TryGetCharacter(
                characterId,
                out DRPGCombatantProfileSO profile,
                out DRPGCharacterEquipmentState equipment))
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.CharacterNotFound,
                "Character is not registered in the equipment root.");
        }
        return TryEquipWeapon(
            profile,
            equipment,
            candidateInstanceId,
            requestedProfessionId);
    }

    public DRPGEquipmentOperationResult TryEquipWeapon(
        DRPGCombatantProfileSO profile,
        DRPGCharacterEquipmentState equipment,
        string candidateInstanceId,
        string requestedProfessionId = null)
    {
        DRPGWeaponInstance candidate = inventory.FindWeapon(candidateInstanceId);
        if (candidate == null)
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.WeaponNotFound,
                "Candidate weapon is not in the backpack.");
        }

        DRPGCharacterProgressionView progressionView;
        try
        {
            progressionView = progression.GetView(profile);
        }
        catch (InvalidOperationException exception)
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.MissingProgression,
                exception.Message);
        }

        DRPGEquipmentOperationResult eligible = eligibility.Evaluate(
            profile,
            equipment,
            candidate,
            progressionView,
            requestedProfessionId);
        if (!eligible.Success)
            return eligible;

        DRPGWeaponInstance previous = equipment.EquippedWeapon;
        DRPGInventoryReplacementPreview preview =
            inventory.PreviewWeaponReplacement(
                candidate.InstanceId,
                previous);
        if (!preview.Success)
        {
            return DRPGEquipmentOperationResult.Fail(
                preview.Error,
                preview.Message);
        }

        DRPGCharacterEquipmentState.Snapshot equipmentSnapshot =
            equipment.CaptureSnapshot();
        string previousProfessionId =
            progressionView.ActiveProfession.ProfessionId;
        bool inventoryCommitted = false;
        try
        {
            DRPGEquipmentOperationResult inventoryResult = inventory.Commit(preview);
            if (!inventoryResult.Success)
                return inventoryResult;
            inventoryCommitted = true;

            DRPGEquipmentOperationResult equipmentResult =
                equipment.TryReplaceWeapon(
                    previous,
                    candidate,
                    eligible.ResolvedProfession.ProfessionId);
            if (!equipmentResult.Success)
            {
                inventory.RollbackCommittedPreview(preview);
                return equipmentResult;
            }

            DRPGProgressionOperationResult professionResult =
                progression.TrySetActiveProfession(
                    profile,
                    eligible.ResolvedProfession.ProfessionId);
            if (!professionResult.Success)
            {
                equipment.RestoreSnapshot(equipmentSnapshot);
                inventory.RollbackCommittedPreview(preview);
                return DRPGEquipmentOperationResult.Fail(
                    DRPGEquipmentError.CommitFailed,
                    professionResult.Message);
            }

            return DRPGEquipmentOperationResult.Ok(
                eligible.ResolvedProfession);
        }
        catch (Exception exception)
        {
            equipment.RestoreSnapshot(equipmentSnapshot);
            if (inventoryCommitted)
                inventory.RollbackCommittedPreview(preview);
            TryRestoreProfession(profile, previousProfessionId);
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.CommitFailed,
                "Equipment transaction rolled back: " + exception.Message);
        }
    }

    public DRPGEquipmentOperationResult TryRestoreLastWeaponForProfession(
        string characterId,
        string professionId)
    {
        if (root == null ||
            !root.TryGetCharacter(
                characterId,
                out DRPGCombatantProfileSO profile,
                out DRPGCharacterEquipmentState equipment))
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.CharacterNotFound,
                "Character is not registered in the equipment root.");
        }
        string instanceId = equipment.GetLastWeaponInstanceId(professionId);
        if (string.IsNullOrWhiteSpace(instanceId) ||
            !inventory.ContainsWeapon(instanceId))
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.WeaponNotInBackpack,
                "Recorded profession weapon is no longer in the backpack.");
        }
        return TryEquipWeapon(
            profile,
            equipment,
            instanceId,
            professionId);
    }

    private void TryRestoreProfession(
        DRPGCombatantProfileSO profile,
        string professionId)
    {
        try
        {
            DRPGCharacterProgressionView current = progression.GetView(profile);
            if (current.ActiveProfession.ProfessionId != professionId)
                progression.TrySetActiveProfession(profile, professionId);
        }
        catch (Exception)
        {
            // The caller already receives CommitFailed. Rollback is best-effort
            // only after an unexpected exception escaped a validated transaction.
        }
    }
}
