public enum DRPGDiscardConfirmationMode
{
    AskEveryTime = 0,
    NeverAsk = 1
}

public sealed class DRPGInventoryInteractionSettings
{
    public static DRPGInventoryInteractionSettings Session { get; } =
        new DRPGInventoryInteractionSettings();

    public DRPGDiscardConfirmationMode DiscardConfirmationMode { get; private set; } =
        DRPGDiscardConfirmationMode.AskEveryTime;

    public void SetDiscardConfirmationMode(
        DRPGDiscardConfirmationMode mode)
    {
        DiscardConfirmationMode = mode;
    }
}

public interface IDRPGDiscardPolicy
{
    DRPGInventoryOperationResult CanDiscard(DRPGInventoryItemStack item);
}

public sealed class DRPGDefaultDiscardPolicy : IDRPGDiscardPolicy
{
    public static DRPGDefaultDiscardPolicy Instance { get; } =
        new DRPGDefaultDiscardPolicy();

    private DRPGDefaultDiscardPolicy()
    {
    }

    public DRPGInventoryOperationResult CanDiscard(
        DRPGInventoryItemStack item)
    {
        if (item == null)
        {
            return DRPGInventoryOperationResult.Fail(
                DRPGInventoryError.InvalidRequest,
                "Discard item is missing.");
        }
        if (item.Kind == DRPGInventoryItemKind.Weapon &&
            item.Weapon != null && item.Weapon.Definition != null &&
            item.Weapon.Definition.OwnershipKind ==
                DRPGWeaponOwnershipKind.Signature)
        {
            return DRPGInventoryOperationResult.Fail(
                DRPGInventoryError.DiscardDenied,
                "Signature weapons cannot be discarded.");
        }
        return DRPGInventoryOperationResult.Ok();
    }
}

public sealed class DRPGInventoryDiscardRequest
{
    public DRPGInventoryDiscardRequest(
        IDRPGInventoryContainer source,
        int sourceSlot,
        IDRPGDiscardPolicy policy = null)
    {
        Source = source;
        SourceSlot = sourceSlot;
        SourceRevision = source != null ? source.Revision : -1;
        Item = source != null && sourceSlot >= 0 &&
            sourceSlot < source.Capacity
                ? source.GetSlot(sourceSlot).Item
                : null;
        Policy = policy ?? DRPGDefaultDiscardPolicy.Instance;
    }

    public IDRPGInventoryContainer Source { get; }
    public int SourceSlot { get; }
    public int SourceRevision { get; }
    public DRPGInventoryItemStack Item { get; }
    public IDRPGDiscardPolicy Policy { get; }

    public DRPGInventoryOperationResult Execute()
    {
        if (Source == null || Item == null ||
            Source.Revision != SourceRevision ||
            !ReferenceEquals(Source.GetSlot(SourceSlot).Item, Item))
        {
            return DRPGInventoryOperationResult.Fail(
                DRPGInventoryError.StateConflict,
                "Inventory changed before discard confirmation.");
        }
        return DRPGInventoryTransferService.Discard(
            Source,
            SourceSlot,
            Policy);
    }
}

