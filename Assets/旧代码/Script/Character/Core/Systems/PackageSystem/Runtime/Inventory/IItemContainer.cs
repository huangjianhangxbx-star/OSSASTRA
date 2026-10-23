/// <summary>
/// Abstraction for any container that holds ItemInstances.
/// Backpack, WeaponSlots, StorageContainer, ShopContainer, WorldDropContainer all implement this.
/// </summary>
public interface IItemContainer
{
    int Capacity { get; }
    ItemInstance GetItem(int slotIndex);
    bool CanAccept(ItemInstance item, int slotIndex);
}
