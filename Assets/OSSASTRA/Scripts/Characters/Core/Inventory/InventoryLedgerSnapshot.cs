/* 脚本：InventoryLedgerSnapshot
 * 功能：唯一库存的独立字段快照；容器、物品及装备payload完整校验后才能共同启用。
 * 边界：不保存未释放动作/预留；持久化只保存已生效资源及去重，不恢复半个攻击。
 */
using System;
namespace Ossastra.Inventory
{
    [Serializable] public sealed class InventoryLedgerSnapshot
    {
        public long accessGeneration,mutationRevision;
        public InventoryContainerSnapshot[] containers=Array.Empty<InventoryContainerSnapshot>();
        public InventoryItemSnapshot[] items=Array.Empty<InventoryItemSnapshot>();
        public string[] requests=Array.Empty<string>(),wipes=Array.Empty<string>();
    }
    [Serializable] public sealed class InventoryContainerSnapshot
    {
        public string id,owner;
        public int columns,rows,hand;
        public bool slots;
        public long revision;
    }
    [Serializable] public sealed class InventoryItemSnapshot
    {
        public string id,definitionId,containerId;
        public int quantity,x,y;
        public EquipmentPersistentSnapshot equipment;
    }
}
