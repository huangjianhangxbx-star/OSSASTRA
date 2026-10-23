using System;

public enum DRPGEquipmentError
{
    None = 0,
    InvalidRequest = 1,
    CharacterNotFound = 2,
    WeaponNotInBackpack = 3,
    WeaponNotFound = WeaponNotInBackpack,
    WeaponAlreadyOwned = 4,
    BackpackSlotCapacityExceeded = 5,
    InventorySlotCapacityExceeded = BackpackSlotCapacityExceeded,
    BackpackWeightCapacityExceeded = 6,
    InventoryWeightCapacityExceeded = BackpackWeightCapacityExceeded,
    MissingProgression = 7,
    ProfessionUnavailable = 8,
    ProfessionLocked = 9,
    IncompatibleProfession = 10,
    ProfessionIncompatible = IncompatibleProfession,
    PermanentRequirementNotMet = 11,
    AttributeRequirementNotMet = PermanentRequirementNotMet,
    SignatureOwnerMismatch = 12,
    EmployeeCannotEquipSignature = 13,
    StateConflict = 14,
    CommitFailed = 15,
    NoWeaponEquipped = 16,
    InvalidInstanceState = 17,
    TraitCapacityExceeded = 18,
    TraitDuplicate = 19,
    TraitNotFound = 20,
    SourceTraitProtected = 21,
    InvalidSaveData = 22,
    MissingDefinition = 23,
    RecoveryBagNotFound = 24,
    RecoveryItemNotFound = 25
}

public sealed class DRPGEquipmentOperationResult
{
    private DRPGEquipmentOperationResult(
        bool success,
        DRPGEquipmentError error,
        string message,
        DRPGProfessionSO resolvedProfession)
    {
        Success = success;
        Error = error;
        Message = message ?? string.Empty;
        ResolvedProfession = resolvedProfession;
    }

    public bool Success { get; }
    public DRPGEquipmentError Error { get; }
    public string Message { get; }
    public DRPGProfessionSO ResolvedProfession { get; }

    public static DRPGEquipmentOperationResult Ok(
        DRPGProfessionSO resolvedProfession = null)
    {
        return new DRPGEquipmentOperationResult(
            true,
            DRPGEquipmentError.None,
            string.Empty,
            resolvedProfession);
    }

    public static DRPGEquipmentOperationResult Fail(
        DRPGEquipmentError error,
        string message)
    {
        if (error == DRPGEquipmentError.None)
            throw new ArgumentOutOfRangeException(nameof(error));
        return new DRPGEquipmentOperationResult(false, error, message, null);
    }
}

public sealed class DRPGInventoryReplacementPreview
{
    internal DRPGInventoryReplacementPreview(
        bool success,
        DRPGEquipmentError error,
        string message,
        DRPGWeaponInstance candidate,
        DRPGWeaponInstance returningWeapon,
        bool returningWasReserved,
        int sourceRevision,
        float sourceWeight,
        int finalSlots,
        float finalWeight,
        int candidateSlotIndex = -1,
        int returningSlotIndex = -1)
    {
        Success = success;
        Error = error;
        Message = message ?? string.Empty;
        Candidate = candidate;
        ReturningWeapon = returningWeapon;
        ReturningWasReserved = returningWasReserved;
        SourceRevision = sourceRevision;
        SourceWeight = sourceWeight;
        FinalSlots = finalSlots;
        FinalWeight = finalWeight;
        CandidateSlotIndex = candidateSlotIndex;
        ReturningSlotIndex = returningSlotIndex;
    }

    public bool Success { get; }
    public DRPGEquipmentError Error { get; }
    public string Message { get; }
    public int FinalSlots { get; }
    public float FinalWeight { get; }
    internal DRPGWeaponInstance Candidate { get; }
    internal DRPGWeaponInstance ReturningWeapon { get; }
    internal bool ReturningWasReserved { get; }
    internal int SourceRevision { get; }
    internal float SourceWeight { get; }
    internal int CandidateSlotIndex { get; }
    internal int ReturningSlotIndex { get; }
}
