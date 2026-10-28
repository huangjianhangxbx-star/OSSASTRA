/* 脚本：CommerceContracts
 * 功能：纯交易定义、可信访问、报价及回执；目录配置不可变，实例存货另由CommerceEconomy持有。
 * 边界：不读Unity对象、角色控制或音频；UI只能确认服务签发的报价，不能自行提交余额或访问资格。
 */
using System;
using System.Linq;

namespace Ossastra.Commerce
{
    public enum TradeDirection { Purchase, Sale }
    /// <summary>宿主每次检查当前角色、商人活性、距离、同层、视线及控制会话，返回真正可访问容器。</summary>
    public interface ICommerceAccessPolicy
    { CommerceAccess Check(string actorId,string merchantId,long sessionGeneration); }

    public sealed class CommerceAccess
    {
        public bool Allowed {get;}
        public string Reason {get;}
        public long AccessGeneration {get;}
        private readonly string[] paymentContainerIds,sellContainerIds;
        public string[] PaymentContainerIds=>(string[])paymentContainerIds.Clone();
        public string[] SellContainerIds=>(string[])sellContainerIds.Clone();
        public string PurchaseContainerId {get;}
        public string SaleProceedsContainerId {get;}
        public CommerceAccess(bool allowed,string reason,long accessGeneration,string[] paymentContainerIds,
            string purchaseContainerId,string saleProceedsContainerId,string[] sellContainerIds)
        {
            Allowed=allowed;Reason=reason;AccessGeneration=accessGeneration;
            this.paymentContainerIds=(string[])(paymentContainerIds??Array.Empty<string>()).Clone();
            this.sellContainerIds=(string[])(sellContainerIds??Array.Empty<string>()).Clone();
            PurchaseContainerId=purchaseContainerId;SaleProceedsContainerId=saleProceedsContainerId;
        }
    }

    public sealed class MerchantGoods
    {
        public string DefinitionId {get;}
        public long BuyPrice {get;}
        public long SellPrice {get;}
        public int TargetStock {get;}
        public bool AllowZeroPrice {get;}
        public MerchantGoods(string definitionId,long buyPrice,long sellPrice,int targetStock,bool allowZeroPrice=false)
        {
            if(string.IsNullOrWhiteSpace(definitionId)||buyPrice< -1||sellPrice< -1||targetStock<0||
                !allowZeroPrice&&(buyPrice==0||sellPrice==0))throw new ArgumentException("商品价格、库存或定义无效；零价须明确允许");
            DefinitionId=definitionId;BuyPrice=buyPrice;SellPrice=sellPrice;TargetStock=targetStock;AllowZeroPrice=allowZeroPrice;
        }
    }
    public sealed class MerchantDefinition
    {
        private readonly MerchantGoods[] goods;
        public string Id {get;}
        public double RefreshSeconds {get;}
        public MerchantGoods[] Goods=>(MerchantGoods[])goods.Clone();
        public MerchantDefinition(string id,MerchantGoods[] goods,double refreshSeconds=300)
        {
            if(string.IsNullOrWhiteSpace(id)||goods==null||goods.Any(g=>g==null)||goods.Select(g=>g.DefinitionId).Distinct().Count()!=goods.Length||
                double.IsNaN(refreshSeconds)||double.IsInfinity(refreshSeconds)||refreshSeconds<=0)throw new ArgumentException("商店目录配置无效");
            Id=id;this.goods=(MerchantGoods[])goods.Clone();RefreshSeconds=refreshSeconds;
        }
    }
    public sealed class TradeQuote
    {
        internal readonly Guid issuer;
        internal readonly long restoreGeneration;
        public Guid QuoteId {get;}
        public string ActorId {get;}
        public string MerchantId {get;}
        public TradeDirection Direction {get;}
        public string DefinitionId {get;}
        public string ItemId {get;}
        public int Quantity {get;}
        public long UnitPrice {get;}
        public long TotalPrice {get;}
        public long MerchantRevision {get;}
        public long LedgerRevision {get;}
        public long AccessGeneration {get;}
        public long SessionGeneration {get;}
        internal TradeQuote(Guid issuer,long restoreGeneration,string actorId,string merchantId,TradeDirection direction,string definitionId,string itemId,
            int quantity,long unitPrice,long totalPrice,long merchantRevision,long ledgerRevision,long accessGeneration,long sessionGeneration)
        {
            this.issuer=issuer;this.restoreGeneration=restoreGeneration;QuoteId=Guid.NewGuid();ActorId=actorId;MerchantId=merchantId;Direction=direction;
            DefinitionId=definitionId;ItemId=itemId;Quantity=quantity;UnitPrice=unitPrice;TotalPrice=totalPrice;MerchantRevision=merchantRevision;
            LedgerRevision=ledgerRevision;AccessGeneration=accessGeneration;SessionGeneration=sessionGeneration;
        }
        internal string Fingerprint=>string.Join("|",QuoteId.ToString("N"),ActorId,MerchantId,Direction.ToString(),DefinitionId,ItemId,
            Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture),TotalPrice.ToString(System.Globalization.CultureInfo.InvariantCulture),
            SessionGeneration.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
    public readonly struct TradeQuoteResult
    {
        public bool Ok=>Quote!=null;
        public string Reason {get;}
        public TradeQuote Quote {get;}
        internal TradeQuoteResult(TradeQuote quote,string reason=null){Quote=quote;Reason=reason;}
    }
    public sealed class TradeCommand
    {
        public Guid RequestId {get;}
        public TradeQuote Quote {get;}
        public TradeCommand(Guid requestId,TradeQuote quote){RequestId=requestId;Quote=quote;}
    }
    public sealed class TradeReceipt
    {
        public bool Ok {get;}
        public string Reason {get;}
        public Guid RequestId {get;}
        public long StateRevision {get;}
        public string MerchantId {get;}
        public string ItemId {get;}
        public int Quantity {get;}
        public long TotalPrice {get;}
        internal TradeReceipt(bool ok,string reason,Guid requestId,long revision,string merchantId=null,string itemId=null,int quantity=0,long totalPrice=0)
        {Ok=ok;Reason=reason;RequestId=requestId;StateRevision=revision;MerchantId=merchantId;ItemId=itemId;Quantity=quantity;TotalPrice=totalPrice;}
    }
}
