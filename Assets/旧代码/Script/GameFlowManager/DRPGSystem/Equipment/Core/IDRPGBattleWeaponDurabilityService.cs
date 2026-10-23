using System;

public sealed class DRPGBattleWeaponActionContext
{
    internal DRPGBattleWeaponActionContext(
        long actionId,
        string ledgerActionId,
        string characterId,
        DRPGWeaponInstance weapon)
    {
        if (actionId <= 0)
            throw new ArgumentOutOfRangeException(nameof(actionId));
        if (string.IsNullOrWhiteSpace(ledgerActionId))
            throw new ArgumentException("Ledger action ID is required.", nameof(ledgerActionId));
        if (string.IsNullOrWhiteSpace(characterId))
            throw new ArgumentException("Character ID is required.", nameof(characterId));

        ActionId = actionId;
        LedgerActionId = ledgerActionId;
        CharacterId = characterId;
        Weapon = weapon ?? throw new ArgumentNullException(nameof(weapon));
    }

    public long ActionId { get; }
    public string CharacterId { get; }
    public string WeaponInstanceId => Weapon.InstanceId;
    public int CommittedUseUnits { get; internal set; }
    public bool IsCancelled { get; internal set; }
    public bool IsCompleted { get; internal set; }
    internal string LedgerActionId { get; }
    internal DRPGWeaponInstance Weapon { get; }
}

public sealed class DRPGBattleWeaponActionCompletion
{
    private DRPGBattleWeaponActionCompletion(
        bool success,
        string message,
        bool alreadyCompleted,
        int appliedUseUnits,
        bool brokeThisCompletion,
        DRPGWeaponBreakDisposition disposition,
        DRPGResolvedCombatantBuild replacementBuild)
    {
        Success = success;
        Message = message ?? string.Empty;
        AlreadyCompleted = alreadyCompleted;
        AppliedUseUnits = appliedUseUnits;
        BrokeThisCompletion = brokeThisCompletion;
        Disposition = disposition;
        ReplacementBuild = replacementBuild;
    }

    public bool Success { get; }
    public string Message { get; }
    public bool AlreadyCompleted { get; }
    public int AppliedUseUnits { get; }
    public bool BrokeThisCompletion { get; }
    public DRPGWeaponBreakDisposition Disposition { get; }
    public DRPGResolvedCombatantBuild ReplacementBuild { get; }

    internal static DRPGBattleWeaponActionCompletion NoUse()
    {
        return new DRPGBattleWeaponActionCompletion(
            true,
            string.Empty,
            false,
            0,
            false,
            DRPGWeaponBreakDisposition.DestroyIntoFragments,
            null);
    }

    internal static DRPGBattleWeaponActionCompletion Applied(
        DRPGWeaponUseCompletionResult use,
        DRPGResolvedCombatantBuild replacementBuild)
    {
        return new DRPGBattleWeaponActionCompletion(
            true,
            string.Empty,
            use.AlreadyCompleted,
            use.AppliedUseUnits,
            use.BrokeThisCompletion,
            use.BreakDisposition,
            replacementBuild);
    }

    internal static DRPGBattleWeaponActionCompletion Fail(string message)
    {
        return new DRPGBattleWeaponActionCompletion(
            false,
            message,
            false,
            0,
            false,
            DRPGWeaponBreakDisposition.DestroyIntoFragments,
            null);
    }
}

public sealed class DRPGBattleWeaponBreakEvent
{
    public DRPGBattleWeaponBreakEvent(
        int actorRuntimeId,
        string weaponInstanceId,
        DRPGWeaponBreakDisposition disposition)
    {
        if (actorRuntimeId <= 0)
            throw new ArgumentOutOfRangeException(nameof(actorRuntimeId));
        if (string.IsNullOrWhiteSpace(weaponInstanceId))
            throw new ArgumentException("Weapon instance ID is required.", nameof(weaponInstanceId));
        ActorRuntimeId = actorRuntimeId;
        WeaponInstanceId = weaponInstanceId;
        Disposition = disposition;
    }

    public int ActorRuntimeId { get; }
    public string WeaponInstanceId { get; }
    public DRPGWeaponBreakDisposition Disposition { get; }
}

public interface IDRPGBattleWeaponDurabilityService
{
    DRPGBattleWeaponActionContext CaptureAction(
        BattleParticipantRuntime actor,
        long actionId);

    DRPGEquipmentOperationResult CanRecordCommittedUse(
        DRPGBattleWeaponActionContext context,
        DRPGWeaponUseKind useKind,
        int useUnitsOverride = -1);

    DRPGEquipmentOperationResult TryRecordCommittedUse(
        DRPGBattleWeaponActionContext context,
        DRPGWeaponUseKind useKind,
        int useUnitsOverride = -1);

    DRPGBattleWeaponActionCompletion CompleteAction(
        BattleParticipantRuntime actor,
        DRPGBattleWeaponActionContext context);

    void CancelAction(DRPGBattleWeaponActionContext context);
}
