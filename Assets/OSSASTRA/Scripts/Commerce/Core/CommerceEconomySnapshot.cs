/* 脚本：CommerceEconomySnapshot
 * 功能：经济模块字段DTO；捕获后与活Ledger、商店、装备实例脱离，后台保存只接收编码字节。
 * 边界：不保存角色动作/HP/动画/音频；世界代理、快捷引用及稳定所有者映射由宿主填写和恢复。
 */
using System;
using Ossastra.Inventory;
namespace Ossastra.Commerce
{
    [Serializable] public sealed class CommerceEconomySnapshot
    {
        public int schema=1;
        public string definitionFingerprint="";
        public long stateRevision;
        public double simulationTime;
        public InventoryLedgerSnapshot inventory;
        public MerchantSnapshot[] merchants=Array.Empty<MerchantSnapshot>();
        public TradeReceiptSnapshot[] receipts=Array.Empty<TradeReceiptSnapshot>();
        public string[] dialogueFlags=Array.Empty<string>();
        public CommerceWorldContainerSnapshot[] worldContainers=Array.Empty<CommerceWorldContainerSnapshot>();
        public CommerceReferenceSnapshot[] references=Array.Empty<CommerceReferenceSnapshot>();
        public string[] ownerKeys=Array.Empty<string>();
    }
    [Serializable] public sealed class MerchantSnapshot
    {
        public string instanceId,definitionId;
        public long revision;
        public double nextRefresh;
        public MerchantStockSnapshot[] stock=Array.Empty<MerchantStockSnapshot>();
    }
    [Serializable] public sealed class MerchantStockSnapshot { public string definitionId;public int quantity; }
    [Serializable] public sealed class TradeReceiptSnapshot
    {
        public string requestId,fingerprint,merchantId,itemId;
        public long stateRevision,totalPrice;
        public int quantity;
    }
    [Serializable] public sealed class CommerceWorldContainerSnapshot
    {
        public string id,mapId,kind;
        public float x,y,z;
        public bool emptied;
    }
    [Serializable] public sealed class CommerceReferenceSnapshot
    {
        public string key,ownerKey,definitionId,itemId;
        public int slot;
    }
}
