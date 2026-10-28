/* 脚本：CommerceRuntimeSnapshot
 * 功能：完整经济领域与库存宿主的值快照；仅字段，不含活Unity对象或保存回调。
 * 边界：schema 2采用保留null的编码，普通物品不会在恢复时被伪造为默认装备。
 */
using System;
using Ossastra.Inventory;
namespace Ossastra.Commerce.Runtime
{
    [Serializable] public sealed class CommerceRuntimeSnapshot
    {
        public int schema=2;
        public long revision;
        public CommerceEconomySnapshot economy;
        public InventoryRuntimeState inventoryRuntime;
        public string[] dialogueFlags=Array.Empty<string>(),merchantIds=Array.Empty<string>(),ownerIds=Array.Empty<string>();
    }
}
