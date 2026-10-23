using System;
using System.Collections.Generic;

public enum DRPGWeaponUseKind
{
    FirearmShot = 0,
    FreeStrikeInput = 1,
    MeleeCommittedSwing = 2
}

public sealed class DRPGWeaponUseCompletionResult
{
    private DRPGWeaponUseCompletionResult(
        bool success,
        string message,
        bool alreadyCompleted,
        int appliedUseUnits,
        bool brokeThisCompletion,
        DRPGWeaponBreakDisposition breakDisposition)
    {
        Success = success;
        Message = message ?? string.Empty;
        AlreadyCompleted = alreadyCompleted;
        AppliedUseUnits = appliedUseUnits;
        BrokeThisCompletion = brokeThisCompletion;
        BreakDisposition = breakDisposition;
    }

    public bool Success { get; }
    public string Message { get; }
    public bool AlreadyCompleted { get; }
    public int AppliedUseUnits { get; }
    public bool BrokeThisCompletion { get; }
    public DRPGWeaponBreakDisposition BreakDisposition { get; }
    public bool ProducesWeaponFragments =>
        BrokeThisCompletion &&
        BreakDisposition == DRPGWeaponBreakDisposition.DestroyIntoFragments;

    internal static DRPGWeaponUseCompletionResult Applied(
        int units,
        bool broke,
        DRPGWeaponBreakDisposition disposition)
    {
        return new DRPGWeaponUseCompletionResult(
            true,
            string.Empty,
            false,
            units,
            broke,
            disposition);
    }

    internal static DRPGWeaponUseCompletionResult Duplicate()
    {
        return new DRPGWeaponUseCompletionResult(
            true,
            string.Empty,
            true,
            0,
            false,
            DRPGWeaponBreakDisposition.DestroyIntoFragments);
    }

    internal static DRPGWeaponUseCompletionResult Fail(string message)
    {
        return new DRPGWeaponUseCompletionResult(
            false,
            message,
            false,
            0,
            false,
            DRPGWeaponBreakDisposition.DestroyIntoFragments);
    }
}

public sealed class DRPGWeaponUseLedger
{
    private Dictionary<string, int> pendingUnits =
        new Dictionary<string, int>(StringComparer.Ordinal);
    private HashSet<string> completedActionIds =
            new HashSet<string>(StringComparer.Ordinal);

    internal long Revision { get; private set; }

    public DRPGEquipmentOperationResult TryRecordCommittedUse(
        string battleActionId,
        DRPGWeaponUseKind useKind,
        int useUnitsOverride = -1)
    {
        DRPGEquipmentOperationResult preflight = CanRecordCommittedUse(
            battleActionId,
            useKind,
            useUnitsOverride);
        if (!preflight.Success)
            return preflight;

        int units = ResolveUseUnits(useKind, useUnitsOverride);
        pendingUnits.TryGetValue(battleActionId, out int current);
        pendingUnits[battleActionId] = checked(current + units);
        Revision++;
        return DRPGEquipmentOperationResult.Ok();
    }

    public DRPGEquipmentOperationResult CanRecordCommittedUse(
        string battleActionId,
        DRPGWeaponUseKind useKind,
        int useUnitsOverride = -1)
    {
        if (string.IsNullOrWhiteSpace(battleActionId))
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.InvalidRequest,
                "Battle action ID is required.");
        }
        if (!Enum.IsDefined(typeof(DRPGWeaponUseKind), useKind))
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.InvalidRequest,
                "Unknown weapon use kind.");
        }
        if (completedActionIds.Contains(battleActionId))
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.InvalidInstanceState,
                "Completed action cannot receive more use units.");
        }

        return DRPGEquipmentOperationResult.Ok();
    }

    public int GetPendingUseUnits(string battleActionId)
    {
        return !string.IsNullOrWhiteSpace(battleActionId) &&
            pendingUnits.TryGetValue(battleActionId, out int units)
                ? units
                : 0;
    }

    public DRPGWeaponUseCompletionResult CompleteAction(
        string battleActionId,
        DRPGWeaponInstance weapon)
    {
        if (string.IsNullOrWhiteSpace(battleActionId))
            return DRPGWeaponUseCompletionResult.Fail("Battle action ID is required.");
        if (completedActionIds.Contains(battleActionId))
            return DRPGWeaponUseCompletionResult.Duplicate();
        if (weapon == null)
            return DRPGWeaponUseCompletionResult.Fail("Weapon instance is required.");
        if (!pendingUnits.TryGetValue(battleActionId, out int units))
        {
            return DRPGWeaponUseCompletionResult.Fail(
                "Battle action has no committed weapon use.");
        }

        bool wasBroken = weapon.IsBroken;
        weapon.ApplyDurabilityLoss(units);
        bool brokeThisCompletion = !wasBroken && weapon.IsBroken;
        pendingUnits.Remove(battleActionId);
        completedActionIds.Add(battleActionId);
        Revision++;
        return DRPGWeaponUseCompletionResult.Applied(
            units,
            brokeThisCompletion,
            weapon.Definition.BreakDisposition);
    }

    public bool IsCompleted(string battleActionId)
    {
        return !string.IsNullOrWhiteSpace(battleActionId) &&
            completedActionIds.Contains(battleActionId);
    }

    public void CancelAction(string battleActionId)
    {
        if (string.IsNullOrWhiteSpace(battleActionId) ||
            completedActionIds.Contains(battleActionId))
        {
            return;
        }
        if (pendingUnits.Remove(battleActionId)) Revision++;
    }

    internal DRPGWeaponUseLedger PrepareRootCompletions(IReadOnlyList<string> keys)
    {
        var prepared = new DRPGWeaponUseLedger
        {
            pendingUnits = new Dictionary<string, int>(pendingUnits, StringComparer.Ordinal),
            completedActionIds = new HashSet<string>(completedActionIds, StringComparer.Ordinal)
        };
        foreach (string key in keys)
        {
            if (string.IsNullOrWhiteSpace(key) || prepared.pendingUnits.ContainsKey(key) ||
                !prepared.completedActionIds.Add(key))
                throw new InvalidOperationException("Root weapon usage already exists or was completed.");
        }
        return prepared;
    }

    internal void ApplyPreparedRootCompletions(DRPGWeaponUseLedger prepared)
    {
        pendingUnits = prepared.pendingUnits;
        completedActionIds = prepared.completedActionIds;
        Revision++;
    }

    private static int ResolveDefaultUnits(DRPGWeaponUseKind useKind)
    {
        switch (useKind)
        {
            case DRPGWeaponUseKind.FirearmShot:
            case DRPGWeaponUseKind.FreeStrikeInput:
            case DRPGWeaponUseKind.MeleeCommittedSwing:
                return 1;
            default:
                throw new ArgumentOutOfRangeException(nameof(useKind));
        }
    }

    private static int ResolveUseUnits(
        DRPGWeaponUseKind useKind,
        int useUnitsOverride)
    {
        return useUnitsOverride >= 0
            ? useUnitsOverride
            : ResolveDefaultUnits(useKind);
    }
}
