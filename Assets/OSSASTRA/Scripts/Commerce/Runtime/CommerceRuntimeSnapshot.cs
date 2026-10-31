/* 脚本：CommerceRuntimeSnapshot
 * 功能：完整经济、库存宿主及显式登记领域的同修订值快照；不含活Unity对象或保存回调。
 * 边界：schema 2保留null；旧档缺section只对无参与者兼容，已登记领域不能据缺失载荷重播种。
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
        public CommerceSnapshotSection[] sections=Array.Empty<CommerceSnapshotSection>();
        public string[] dialogueFlags=Array.Empty<string>(),merchantIds=Array.Empty<string>(),ownerIds=Array.Empty<string>();
    }
}
