using System;
using System.Collections.Generic;

public sealed class DRPGBattleWeaponCandidate
{
    public DRPGBattleWeaponCandidate(
        string weaponInstanceId,
        bool isEligible,
        string rejectionReason)
        : this(
            weaponInstanceId,
            null,
            isEligible,
            rejectionReason,
            null,
            false)
    {
    }

    public DRPGBattleWeaponCandidate(
        string weaponInstanceId,
        DRPGWeaponInstance weapon,
        bool isEligible,
        string rejectionReason,
        DRPGProfessionSO resolvedProfession,
        bool returnsPreviousBeyondCapacity)
    {
        if (string.IsNullOrWhiteSpace(weaponInstanceId))
            throw new ArgumentException("Weapon instance ID is required.", nameof(weaponInstanceId));
        WeaponInstanceId = weaponInstanceId;
        Weapon = weapon;
        IsEligible = isEligible;
        RejectionReason = rejectionReason ?? string.Empty;
        ResolvedProfession = resolvedProfession;
        ReturnsPreviousBeyondCapacity = returnsPreviousBeyondCapacity;
    }

    public string WeaponInstanceId { get; }
    public DRPGWeaponInstance Weapon { get; }
    public bool IsEligible { get; }
    public string RejectionReason { get; }
    public DRPGProfessionSO ResolvedProfession { get; }
    public bool ReturnsPreviousBeyondCapacity { get; }
}

public interface IDRPGBattleEquipmentService
{
    IReadOnlyList<DRPGBattleWeaponCandidate> GetCandidates(int actorRuntimeId);

    DRPGEquipmentOperationResult TrySwitchWeapon(
        BattleParticipantRuntime actor,
        string weaponInstanceId,
        out DRPGResolvedCombatantBuild replacementBuild);
}
