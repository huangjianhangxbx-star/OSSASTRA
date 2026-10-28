/* 脚本：CommerceEconomy
 * 功能：统一实体币交易、独立商店状态、模拟补货钟与完整经济快照的纯组装服务。
 * 负责：签发只读报价、重新验证可信访问、准备候选并共同发布Ledger/货架、成功去重和恢复。
 * 不负责：角色更新、世界查询、文件I/O、UI或声音；这些能力只通过显式宿主端口组合。
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Ossastra.Inventory;
namespace Ossastra.Commerce
{
    public sealed class CommerceEconomy : IDisposable
    {
        private sealed class MerchantState
        {
            internal string id;
            internal MerchantDefinition definition;
            internal Dictionary<string,int> stock;
            internal long revision;
            internal double nextRefresh;
        }
        private readonly InventoryLedger ledger;
        private readonly ICommerceAccessPolicy access;
        private readonly string currencyDefinitionId;
        private readonly Guid issuer=Guid.NewGuid();
        private readonly Dictionary<string,MerchantDefinition> definitions=new Dictionary<string,MerchantDefinition>(StringComparer.Ordinal);
        private Dictionary<string,MerchantState> merchants=new Dictionary<string,MerchantState>(StringComparer.Ordinal);
        private Dictionary<Guid,TradeReceiptSnapshot> receipts=new Dictionary<Guid,TradeReceiptSnapshot>();
        private readonly Dictionary<Guid,string> attempts=new Dictionary<Guid,string>();
        private long stateRevision,restoreGeneration;
        private bool publishing,disposed;
        public double SimulationTime {get;private set;}
        public long StateRevision=>stateRevision;
        public InventoryLedger Ledger=>ledger;
        public int NotificationErrorCount {get;private set;}
        public event Action Changed;
        public CommerceEconomy(InventoryLedger ledger,ICommerceAccessPolicy access,string currencyDefinitionId="trade-coin")
        {
            this.ledger=ledger??throw new ArgumentNullException(nameof(ledger));this.access=access??throw new ArgumentNullException(nameof(access));
            this.currencyDefinitionId=currencyDefinitionId;
            var currency=ledger.Definition(currencyDefinitionId);if(currency==null||currency.Hand!=InventoryHand.None)throw new ArgumentException("实体货币定义缺失");
            ledger.Changed+=OnInventoryChanged;
        }
        public void Dispose(){if(disposed)return;disposed=true;ledger.Changed-=OnInventoryChanged;}
        private void OnInventoryChanged(){if(publishing||disposed)return;stateRevision++;Notify();}
        private void Notify()
        {var handlers=Changed;if(handlers==null)return;foreach(Action handler in handlers.GetInvocationList())try{handler();}catch{NotificationErrorCount++;}}
        public void RegisterDefinition(MerchantDefinition definition)
        {
            if(definition==null)throw new ArgumentNullException(nameof(definition));
            if(definition.Goods.Any(g=>ledger.Definition(g.DefinitionId)==null))throw new ArgumentException("商店引用未注册物品");
            if(definitions.TryGetValue(definition.Id,out var old))
            {if(DefinitionKey(old)!=DefinitionKey(definition))throw new ArgumentException("同名商店模板配置冲突");return;}
            definitions.Add(definition.Id,definition);
        }
        public void RegisterMerchant(MerchantDefinition definition,string instanceId)
        {RegisterDefinition(definition);RegisterMerchant(definition.Id,instanceId);}
        public void RegisterMerchant(string definitionId,string instanceId)
        {
            if(string.IsNullOrWhiteSpace(instanceId)||merchants.ContainsKey(instanceId)||!definitions.TryGetValue(definitionId??"",out var definition))throw new ArgumentException("商人实例ID重复或目录缺失");
            merchants.Add(instanceId,new MerchantState{id=instanceId,definition=definition,stock=definition.Goods.ToDictionary(g=>g.DefinitionId,g=>g.TargetStock,StringComparer.Ordinal),nextRefresh=SimulationTime+definition.RefreshSeconds});stateRevision++;
        }
        public bool HasMerchant(string instanceId)=>instanceId!=null&&merchants.ContainsKey(instanceId);
        public MerchantDefinition Definition(string definitionId)=>definitionId!=null&&definitions.TryGetValue(definitionId,out var value)?value:null;
        public MerchantSnapshot Merchant(string instanceId)
        {return instanceId!=null&&merchants.TryGetValue(instanceId,out var state)?Snapshot(state):null;}
        public MerchantSnapshot[] Merchants=>merchants.Values.OrderBy(m=>m.id,StringComparer.Ordinal).Select(Snapshot).ToArray();
        private static MerchantSnapshot Snapshot(MerchantState state)=>new MerchantSnapshot
        {instanceId=state.id,definitionId=state.definition.Id,revision=state.revision,nextRefresh=state.nextRefresh,stock=state.stock.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>new MerchantStockSnapshot{definitionId=p.Key,quantity=p.Value}).ToArray()};
        public long Balance(string actorId,string merchantId,long sessionGeneration)
        {var context=access.Check(actorId,merchantId,sessionGeneration);return ValidAccess(context)?Balance(context):0;}
        private long Balance(CommerceAccess context)
        {var allowed=new HashSet<string>(context.PaymentContainerIds,StringComparer.Ordinal);return ledger.Items.Where(i=>i.DefinitionId==currencyDefinitionId&&allowed.Contains(i.Location.ContainerId)&&!ledger.HasReservation(i.Id)).Sum(i=>(long)i.Quantity);}
        private bool ValidAccess(CommerceAccess context)=>context!=null&&context.Allowed&&context.AccessGeneration>=0&&
            context.PaymentContainerIds.All(id=>ledger.Container(id)!=null)&&context.SellContainerIds.All(id=>ledger.Container(id)!=null)&&
            ledger.Container(context.PurchaseContainerId)!=null&&ledger.Container(context.SaleProceedsContainerId)!=null;
        public TradeQuoteResult QuotePurchase(string actorId,string merchantId,string definitionId,int quantity,long sessionGeneration)
            =>Quote(actorId,merchantId,TradeDirection.Purchase,definitionId,null,quantity,sessionGeneration);
        public TradeQuoteResult QuoteSale(string actorId,string merchantId,string itemId,int quantity,long sessionGeneration)
        {var item=ledger.Find(itemId);return Quote(actorId,merchantId,TradeDirection.Sale,item?.DefinitionId,itemId,quantity,sessionGeneration);}
        private TradeQuoteResult Quote(string actorId,string merchantId,TradeDirection direction,string definitionId,string itemId,int quantity,long sessionGeneration)
        {
            if(disposed||string.IsNullOrWhiteSpace(actorId)||quantity<1||sessionGeneration<0||!merchants.TryGetValue(merchantId??"",out var merchant))return new TradeQuoteResult(null,"商人、角色或数量无效");
            var context=access.Check(actorId,merchantId,sessionGeneration);if(!ValidAccess(context))return new TradeQuoteResult(null,context?.Reason??"当前无法访问商人");
            var goods=merchant.definition.Goods.FirstOrDefault(g=>g.DefinitionId==definitionId);if(goods==null)return new TradeQuoteResult(null,"商人未配置该物品的价格");
            long unit=direction==TradeDirection.Purchase?goods.BuyPrice:goods.SellPrice;
            if(unit<0||unit==0&&!goods.AllowZeroPrice)return new TradeQuoteResult(null,"商人不接受该交易");
            long total;try{total=checked(unit*quantity);}catch(OverflowException){return new TradeQuoteResult(null,"交易金额溢出");}
            if(direction==TradeDirection.Purchase)
            {
                if(merchant.stock[definitionId]<quantity)return new TradeQuoteResult(null,"货架库存不足");
                if(ledger.Definition(definitionId).Hand!=InventoryHand.None)return new TradeQuoteResult(null,"装备商品尚无显式实例配置");
            }
            else
            {
                var item=ledger.Find(itemId);
                if(item==null||item.Quantity<quantity||!context.SellContainerIds.Contains(item.Location.ContainerId)||ledger.Container(item.Location.ContainerId).Hand!=InventoryHand.None||ledger.HasReservation(item.Id))return new TradeQuoteResult(null,"所选物品已装备、预留、移走或数量不足");
                if(item.DefinitionId==currencyDefinitionId)return new TradeQuoteResult(null,"交易钱币不能自售套利");
            }
            return new TradeQuoteResult(new TradeQuote(issuer,restoreGeneration,actorId,merchantId,direction,definitionId,itemId,quantity,unit,total,
                merchant.revision,ledger.MutationRevision,context.AccessGeneration,sessionGeneration));
        }
        public TradeReceipt Execute(TradeCommand command)
        {
            var q=command?.Quote;var request=command?.RequestId??Guid.Empty;
            TradeReceipt Reject(string reason)=>new TradeReceipt(false,reason,request,stateRevision,q?.MerchantId,q?.ItemId,q?.Quantity??0,q?.TotalPrice??0);
            if(disposed||request==Guid.Empty||q==null||q.issuer!=issuer||q.restoreGeneration!=restoreGeneration)return Reject("交易请求或会话已失效");
            if(attempts.TryGetValue(request,out var attempted))
            {if(attempted!=q.Fingerprint)return Reject("相同请求ID包含不同内容");}
            else
            {
                // 失败尝试也绑定内容；有界缓存满时整代报价失效，不能清缓存后重新受理旧请求。
                if(attempts.Count>=1024){attempts.Clear();restoreGeneration++;return Reject("交易会话已更新，请重新选择并确认");}
                attempts.Add(request,q.Fingerprint);
            }
            if(receipts.TryGetValue(request,out var replay))
                return replay.fingerprint==q.Fingerprint?Receipt(replay):Reject("相同请求ID包含不同内容");
            if(!merchants.TryGetValue(q.MerchantId,out var merchant)||merchant.revision!=q.MerchantRevision||ledger.MutationRevision!=q.LedgerRevision)return Reject("报价、货架或背包已变化，请重新确认");
            var context=access.Check(q.ActorId,q.MerchantId,q.SessionGeneration);
            if(!ValidAccess(context)||context.AccessGeneration!=q.AccessGeneration||ledger.MutationRevision!=q.LedgerRevision)return Reject(context?.Reason??"访问资格已变化");
            var goods=merchant.definition.Goods.FirstOrDefault(g=>g.DefinitionId==q.DefinitionId);
            if(goods==null||(q.Direction==TradeDirection.Purchase?goods.BuyPrice:goods.SellPrice)!=q.UnitPrice)return Reject("当前价格已变化，请重新确认");
            var batch=ledger.PrepareEconomicBatch();InventoryResult prepared;
            string delivered=q.ItemId;
            var nextStock=new Dictionary<string,int>(merchant.stock,StringComparer.Ordinal);
            if(q.Direction==TradeDirection.Purchase)
            {
                if(!nextStock.TryGetValue(q.DefinitionId,out var stock)||stock<q.Quantity)return Reject("货架库存不足");
                prepared=batch.Pay(currencyDefinitionId,q.TotalPrice,context.PaymentContainerIds);if(!prepared.Ok)return Reject(prepared.Reason);
                prepared=batch.Grant(context.PurchaseContainerId,q.DefinitionId,q.Quantity);if(!prepared.Ok)return Reject(prepared.Reason);
                delivered=prepared.ItemId;nextStock[q.DefinitionId]=stock-q.Quantity;
            }
            else
            {
                var item=ledger.Find(q.ItemId);
                if(item==null||item.DefinitionId!=q.DefinitionId||!context.SellContainerIds.Contains(item.Location.ContainerId))return Reject("所选出售来源已失效");
                prepared=batch.Remove(q.ItemId,q.Quantity);if(!prepared.Ok)return Reject(prepared.Reason);
                prepared=batch.Grant(context.SaleProceedsContainerId,currencyDefinitionId,q.TotalPrice);if(!prepared.Ok)return Reject(prepared.Reason);
            }
            var expectedEconomyRevision=stateRevision;
            var nextRevision=checked(stateRevision+1);
            var row=new TradeReceiptSnapshot{requestId=request.ToString("N"),fingerprint=q.Fingerprint,merchantId=q.MerchantId,itemId=delivered,quantity=q.Quantity,totalPrice=q.TotalPrice,stateRevision=nextRevision};
            // 所有分配先完成；提交区只切换预先构造的引用，不能调用UI或写盘。
            var nextReceipts=new Dictionary<Guid,TradeReceiptSnapshot>(receipts){[request]=row};
            if(nextReceipts.Count>256)foreach(var key in nextReceipts.OrderBy(p=>p.Value.stateRevision).Take(nextReceipts.Count-256).Select(p=>p.Key).ToArray())nextReceipts.Remove(key);
            var nextMerchant=new MerchantState{id=merchant.id,definition=merchant.definition,revision=checked(merchant.revision+1),nextRefresh=merchant.nextRefresh,stock=nextStock};
            var nextMerchants=new Dictionary<string,MerchantState>(merchants,StringComparer.Ordinal){[merchant.id]=nextMerchant};
            var result=ledger.CommitEconomicBatch(batch,()=>
            {
                var final=access.Check(q.ActorId,q.MerchantId,q.SessionGeneration);
                if(!ValidAccess(final)||final.AccessGeneration!=q.AccessGeneration||!SameAccess(context,final))return final?.Reason??"访问资格已变化";
                if(stateRevision!=expectedEconomyRevision||!ReferenceEquals(merchants[q.MerchantId],merchant)||merchant.revision!=q.MerchantRevision)return "商店或模拟状态已变化";
                return null;
            },()=>{merchants=nextMerchants;receipts=nextReceipts;stateRevision=nextRevision;});
            if(!result.Ok)return Reject(result.Reason);
            publishing=true;try{ledger.NotifyEconomicCommit();}finally{publishing=false;}Notify();return Receipt(row);
        }
        private static bool SameAccess(CommerceAccess a,CommerceAccess b)=>a.PurchaseContainerId==b.PurchaseContainerId&&a.SaleProceedsContainerId==b.SaleProceedsContainerId&&a.PaymentContainerIds.SequenceEqual(b.PaymentContainerIds)&&a.SellContainerIds.SequenceEqual(b.SellContainerIds);
        private static TradeReceipt Receipt(TradeReceiptSnapshot row)=>new TradeReceipt(true,null,Guid.ParseExact(row.requestId,"N"),row.stateRevision,row.merchantId,row.itemId,row.quantity,row.totalPrice);
        /// <summary>由宿主唯一模拟钟调用；查询商店不会推进时间或隐式补货，关进程期间没有现实时间补偿。</summary>
        public void Advance(double delta)
        {
            if(disposed||double.IsNaN(delta)||double.IsInfinity(delta)||delta<0||double.IsInfinity(SimulationTime+delta))throw new ArgumentException("模拟时间无效");
            if(delta==0)return;SimulationTime+=delta;stateRevision++;
            bool changed=false;
            foreach(var merchant in merchants.Values)
            {
                if(SimulationTime<merchant.nextRefresh)continue;
                foreach(var goods in merchant.definition.Goods)merchant.stock[goods.DefinitionId]=goods.TargetStock;
                // 跨多轮只补目标量；维持原定时基准，不把每次打开商店作为刷新起点。
                merchant.nextRefresh+=Math.Floor((SimulationTime-merchant.nextRefresh)/merchant.definition.RefreshSeconds+1)*merchant.definition.RefreshSeconds;
                merchant.revision++;changed=true;
            }
            if(changed)Notify();
        }
        private static string DefinitionKey(MerchantDefinition definition)=>definition.Id+":"+definition.RefreshSeconds.ToString("R",CultureInfo.InvariantCulture)+":"+
            string.Join(";",definition.Goods.OrderBy(g=>g.DefinitionId,StringComparer.Ordinal).Select(g=>g.DefinitionId+","+g.BuyPrice+","+g.SellPrice+","+g.TargetStock+","+g.AllowZeroPrice));
        public string DefinitionFingerprint
        {
            get
            {
                var source=currencyDefinitionId+"|"+string.Join(";",ledger.RegisteredDefinitions.Select(d=>string.Join(",",d.Id,d.Width,d.Height,d.MaxStack,(int)d.Hand,d.WeightGrams,string.Join("/",d.AllowedOwners.OrderBy(x=>x,StringComparer.Ordinal)))))+"|"+
                    string.Join(";",definitions.Values.OrderBy(d=>d.Id,StringComparer.Ordinal).Select(DefinitionKey));
                using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(source))).Replace("-","").ToLowerInvariant();
            }
        }
        public CommerceEconomySnapshot Capture()=>new CommerceEconomySnapshot
        {definitionFingerprint=DefinitionFingerprint,stateRevision=stateRevision,simulationTime=SimulationTime,inventory=ledger.Capture(),merchants=Merchants,receipts=receipts.Values.OrderBy(r=>r.stateRevision).Select(r=>new TradeReceiptSnapshot{requestId=r.requestId,fingerprint=r.fingerprint,merchantId=r.merchantId,itemId=r.itemId,quantity=r.quantity,totalPrice=r.totalPrice,stateRevision=r.stateRevision}).ToArray()};
        public InventoryResult Restore(CommerceEconomySnapshot snapshot)
        {
            if(disposed||snapshot==null||snapshot.schema!=1||snapshot.definitionFingerprint!=DefinitionFingerprint||snapshot.stateRevision<0||snapshot.stateRevision==long.MaxValue||
                double.IsNaN(snapshot.simulationTime)||double.IsInfinity(snapshot.simulationTime)||snapshot.simulationTime<0)return InventoryResult.Reject("经济快照版本、定义或时钟不匹配");
            if(!ledger.PrepareRestore(snapshot.inventory,out var inventory,out var error))return InventoryResult.Reject(error);
            try
            {
                if(snapshot.merchants==null||snapshot.receipts==null||snapshot.worldContainers==null||snapshot.references==null||snapshot.dialogueFlags==null||snapshot.ownerKeys==null)throw new ArgumentException("经济快照字段缺失");
                var next=new Dictionary<string,MerchantState>(StringComparer.Ordinal);
                foreach(var row in snapshot.merchants)
                {
                    if(row==null||string.IsNullOrWhiteSpace(row.instanceId)||!definitions.TryGetValue(row.definitionId??"",out var definition)||row.revision<0||row.revision==long.MaxValue||row.stock==null||double.IsNaN(row.nextRefresh)||double.IsInfinity(row.nextRefresh)||row.nextRefresh<snapshot.simulationTime)throw new ArgumentException("商店快照定义、刷新或身份损坏");
                    var stock=new Dictionary<string,int>(StringComparer.Ordinal);foreach(var item in row.stock){if(item==null||item.quantity<0||!definition.Goods.Any(g=>g.DefinitionId==item.definitionId)||item.quantity>definition.Goods.Single(g=>g.DefinitionId==item.definitionId).TargetStock)throw new ArgumentException("货架数量损坏");stock.Add(item.definitionId,item.quantity);}
                    if(stock.Count!=definition.Goods.Length)throw new ArgumentException("货架条目不完整");
                    next.Add(row.instanceId,new MerchantState{id=row.instanceId,definition=definition,revision=row.revision,nextRefresh=row.nextRefresh,stock=stock});
                }
                var nextReceipts=new Dictionary<Guid,TradeReceiptSnapshot>();
                if(snapshot.receipts.Length>256)throw new ArgumentException("回执记录超限");
                foreach(var row in snapshot.receipts)
                {
                    if(row==null||!Guid.TryParseExact(row.requestId,"N",out var id)||id==Guid.Empty||string.IsNullOrWhiteSpace(row.fingerprint)||!next.ContainsKey(row.merchantId??"")||row.quantity<1||row.totalPrice<0||row.stateRevision<0||row.stateRevision>snapshot.stateRevision)throw new ArgumentException("交易回执损坏");
                    nextReceipts.Add(id,new TradeReceiptSnapshot{requestId=row.requestId,fingerprint=row.fingerprint,merchantId=row.merchantId,itemId=row.itemId,quantity=row.quantity,totalPrice=row.totalPrice,stateRevision=row.stateRevision});
                }
                if(snapshot.dialogueFlags.Any(string.IsNullOrWhiteSpace)||snapshot.dialogueFlags.Distinct().Count()!=snapshot.dialogueFlags.Length||snapshot.ownerKeys.Any(string.IsNullOrWhiteSpace)||snapshot.ownerKeys.Distinct().Count()!=snapshot.ownerKeys.Length)throw new ArgumentException("对话或所有者身份重复");
                var worldIds=new HashSet<string>(StringComparer.Ordinal);
                foreach(var world in snapshot.worldContainers)
                    if(world==null||string.IsNullOrWhiteSpace(world.id)||!worldIds.Add(world.id)||!inventory.containers.ContainsKey(world.id)||string.IsNullOrWhiteSpace(world.mapId)||new[]{world.x,world.y,world.z}.Any(v=>float.IsNaN(v)||float.IsInfinity(v)))throw new ArgumentException("世界容器身份或位置损坏");
                var keys=new HashSet<string>(StringComparer.Ordinal);
                foreach(var reference in snapshot.references)
                    if(reference==null||string.IsNullOrWhiteSpace(reference.key)||!keys.Add(reference.key)||reference.slot<0||!string.IsNullOrEmpty(reference.itemId)&&!inventory.items.ContainsKey(reference.itemId)||!string.IsNullOrEmpty(reference.definitionId)&&ledger.Definition(reference.definitionId)==null)throw new ArgumentException("快捷/优先引用损坏");
                // 没有可失败的配置查找留在发布区；旧UI报价同时整代失效。
                ledger.PublishRestore(inventory,()=>{merchants=next;receipts=nextReceipts;attempts.Clear();SimulationTime=snapshot.simulationTime;stateRevision=checked(snapshot.stateRevision+1);restoreGeneration++;});
                publishing=true;try{ledger.NotifyEconomicCommit();}finally{publishing=false;}Notify();return InventoryResult.Success();
            }
            catch(Exception ex) when(ex is ArgumentException||ex is InvalidOperationException||ex is OverflowException){return InventoryResult.Reject(ex.Message);}
        }
    }
}
