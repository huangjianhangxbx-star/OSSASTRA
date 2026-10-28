/* 脚本：InventoryEconomicBatch
 * 功能：Ledger内部候选批次与完整恢复；Commerce友元在一个同步门发布库存及店铺。
 * 关键约束：候选不改活payload；最终检查数量/容器/访问/预留及payload时间戳，通知仅在共同发布后派发。
 * 边界：没有公开的“只扣币”或notify=false提交；UI/Runtime拿不到内部批次能力。
 */
using System;
using System.Collections.Generic;
using System.Linq;
namespace Ossastra.Inventory
{
    public sealed partial class InventoryLedger
    {
        private long mutationRevision;
        public long MutationRevision=>mutationRevision;
        internal InventoryDefinition[] RegisteredDefinitions=>definitions.Values.OrderBy(d=>d.Id,StringComparer.Ordinal).ToArray();
        /// <summary>定义本身不可变；宿主可建立隔离验证Ledger，不能经此取得事务提交能力。</summary>
        public InventoryDefinition[] Definitions=>RegisteredDefinitions;
        public int NotificationErrorCount {get;private set;}
        public bool HasReservation(string itemId)=>itemId!=null&&Locked(itemId);

        internal sealed class EconomicBatch
        {
            internal readonly InventoryLedger ledger;
            internal readonly long expectedRevision,accessGeneration;
            internal readonly Dictionary<string,InventoryItem> state;
            internal readonly Dictionary<string,string> equipmentSignatures;
            internal readonly HashSet<string> changed=new HashSet<string>(StringComparer.Ordinal);
            internal bool used;
            internal EconomicBatch(InventoryLedger ledger)
            {
                this.ledger=ledger;expectedRevision=ledger.mutationRevision;accessGeneration=ledger.AccessGeneration;state=ledger.Copy();
                equipmentSignatures=ledger.items.Values.Where(i=>i.Equipment!=null).ToDictionary(i=>i.Id,i=>i.Equipment.PersistentSignature(),StringComparer.Ordinal);
            }
            internal InventoryResult Remove(string itemId,int quantity)
            {
                if(used||!state.TryGetValue(itemId??"",out var item)||quantity<1||quantity>item.Quantity||ledger.Locked(itemId))return InventoryResult.Reject("物品数量或动作预留已变化");
                var container=ledger.Container(item.Location.ContainerId);
                if(container==null||container.Hand!=InventoryHand.None)return InventoryResult.Reject("装备中的物品不能出售");
                changed.Add(item.Location.ContainerId);item.Quantity-=quantity;if(item.Quantity==0)state.Remove(itemId);return InventoryResult.Success(itemId);
            }
            internal InventoryResult Pay(string definitionId,long amount,string[] sourceContainers)
            {
                if(amount<0||sourceContainers==null)return InventoryResult.Reject("货币参数无效");
                foreach(var containerId in sourceContainers.Distinct(StringComparer.Ordinal))
                foreach(var item in state.Values.Where(i=>i.Location.ContainerId==containerId&&i.DefinitionId==definitionId&&i.Equipment==null&&!ledger.Locked(i.Id)).OrderBy(i=>i.Id,StringComparer.Ordinal).ToArray())
                {
                    var count=(int)Math.Min(amount,item.Quantity);if(count==0)continue;
                    item.Quantity-=count;amount-=count;changed.Add(containerId);if(item.Quantity==0)state.Remove(item.Id);
                }
                return amount==0?InventoryResult.Success():InventoryResult.Reject("可访问实体钱币不足");
            }
            internal InventoryResult Grant(string containerId,string definitionId,long quantity)
            {
                if(used||quantity<0||ledger.Definition(definitionId)==null||ledger.Container(containerId)==null)return InventoryResult.Reject("交付容器、定义或数量无效");
                if(quantity==0)return InventoryResult.Success();
                var definition=ledger.Definition(definitionId);if(definition.Hand!=InventoryHand.None)return InventoryResult.Reject("装备商品需要显式实例工厂");
                string last=null;
                foreach(var stack in state.Values.Where(i=>i.Location.ContainerId==containerId&&i.DefinitionId==definitionId&&i.Equipment==null&&!ledger.Locked(i.Id)).OrderBy(i=>i.Id,StringComparer.Ordinal))
                {var n=(int)Math.Min(quantity,definition.MaxStack-stack.Quantity);stack.Quantity+=n;quantity-=n;if(n>0)last=stack.Id;if(quantity==0)break;}
                while(quantity>0)
                {
                    var item=new InventoryItem(Guid.NewGuid().ToString("N"),definitionId,(int)Math.Min(definition.MaxStack,quantity),default);
                    if(!ledger.Place(state,item,containerId))return InventoryResult.Reject("最终背包容量不足，整笔未成交");
                    state.Add(item.Id,item);last=item.Id;quantity-=item.Quantity;
                }
                changed.Add(containerId);return InventoryResult.Success(last);
            }
        }
        internal EconomicBatch PrepareEconomicBatch()=>new EconomicBatch(this);
        internal bool ValidateEconomicBatch(EconomicBatch batch,out string error)
        {
            if(batch==null||!ReferenceEquals(batch.ledger,this)||batch.used||batch.expectedRevision!=mutationRevision||batch.accessGeneration!=AccessGeneration)
            {error="库存或动作预留已变化，请重新确认";return false;}
            foreach(var pair in batch.equipmentSignatures)
                if(!items.TryGetValue(pair.Key,out var item)||item.Equipment==null||item.Equipment.PersistentSignature()!=pair.Value)
                {error="装备资源状态已变化，请重新确认";return false;}
            foreach(var item in batch.state.Values)if(!Fits(batch.state,item)){error="候选物品布局无效";return false;}
            error=null;return true;
        }
        /// <summary>仅友元协调器可调用；publish必须是已经分配好的状态引用切换，不能调用用户业务或通知。</summary>
        internal InventoryResult CommitEconomicBatch(EconomicBatch batch,Func<string> finalAccessCheck,Action publish)
        {
            var denied=finalAccessCheck?.Invoke();if(denied!=null)return InventoryResult.Reject(denied);
            if(!ValidateEconomicBatch(batch,out var error))return InventoryResult.Reject(error);
            var oldItems=items;var oldRevision=mutationRevision;var revisions=batch.changed.ToDictionary(id=>id,id=>containers[id].Revision);
            try
            {
                batch.used=true;items=batch.state;foreach(var id in batch.changed)containers[id].Revision++;mutationRevision++;
                publish();
            }
            catch
            {items=oldItems;mutationRevision=oldRevision;foreach(var pair in revisions)containers[pair.Key].Revision=pair.Value;throw;}
            return InventoryResult.Success();
        }
        internal void NotifyEconomicCommit()
        {
            var handlers=Changed;if(handlers==null)return;
            foreach(Action handler in handlers.GetInvocationList())try{handler();}catch{NotificationErrorCount++;}
        }

        public InventoryLedgerSnapshot Capture()
        {
            return new InventoryLedgerSnapshot
            {
                accessGeneration=AccessGeneration,mutationRevision=mutationRevision,
                containers=containers.Values.OrderBy(c=>c.Id,StringComparer.Ordinal).Select(c=>new InventoryContainerSnapshot{id=c.Id,owner=c.Owner,columns=c.Columns,rows=c.Rows,slots=c.Slots,hand=(int)c.Hand,revision=c.Revision}).ToArray(),
                items=items.Values.OrderBy(i=>i.Id,StringComparer.Ordinal).Select(i=>new InventoryItemSnapshot{id=i.Id,definitionId=i.DefinitionId,quantity=i.Quantity,containerId=i.Location.ContainerId,x=i.Location.X,y=i.Location.Y,equipment=i.Equipment?.CapturePersistent()}).ToArray(),
                requests=requests.Select(g=>g.ToString("N")).OrderBy(x=>x,StringComparer.Ordinal).ToArray(),wipes=wipes.Select(g=>g.ToString("N")).OrderBy(x=>x,StringComparer.Ordinal).ToArray()
            };
        }
        internal sealed class RestoreCandidate
        {
            internal readonly Dictionary<string,InventoryContainer> containers=new Dictionary<string,InventoryContainer>(StringComparer.Ordinal);
            internal readonly Dictionary<string,InventoryItem> items=new Dictionary<string,InventoryItem>(StringComparer.Ordinal);
            internal readonly HashSet<Guid> requests=new HashSet<Guid>(),wipes=new HashSet<Guid>();
            internal long revision,accessGeneration;
        }
        internal bool PrepareRestore(InventoryLedgerSnapshot snapshot,out RestoreCandidate candidate,out string error)
        {
            candidate=null;error=null;
            try
            {
                if(snapshot==null||snapshot.containers==null||snapshot.items==null||snapshot.requests==null||snapshot.wipes==null||snapshot.accessGeneration<0||snapshot.accessGeneration==long.MaxValue||snapshot.mutationRevision<0||snapshot.mutationRevision==long.MaxValue)throw new ArgumentException("库存快照缺失或版本无效");
                var prepared=new RestoreCandidate{revision=snapshot.mutationRevision,accessGeneration=snapshot.accessGeneration};
                foreach(var row in snapshot.containers)
                {
                    if(row==null||row.revision<0||row.revision==long.MaxValue)throw new ArgumentException("容器快照损坏");
                    var container=new InventoryContainer(row.id,row.columns,row.rows,row.slots,row.owner,(InventoryHand)row.hand){Revision=row.revision};prepared.containers.Add(container.Id,container);
                }
                foreach(var row in snapshot.items)
                {
                    if(row==null||string.IsNullOrWhiteSpace(row.id)||!definitions.ContainsKey(row.definitionId??""))throw new ArgumentException("物品定义缺失或实例无效");
                    var equipment=row.equipment==null?null:EquipmentPersistentSnapshot.Restore(row.equipment);
                    var item=new InventoryItem(row.id,row.definitionId,row.quantity,new InventoryLocation(row.containerId,row.x,row.y),equipment);prepared.items.Add(item.Id,item);
                }
                // 使用相同布局规则验证候选，临时Ledger不对外发布，也不调用Grant/初始播种。
                var validator=new InventoryLedger();foreach(var definition in definitions.Values)validator.Register(definition);
                foreach(var container in prepared.containers.Values)validator.AddContainer(container);validator.items=prepared.items;
                if(!validator.Validate(out error))return false;
                foreach(var raw in snapshot.requests){if(!Guid.TryParseExact(raw,"N",out var id)||id==Guid.Empty||!prepared.requests.Add(id))throw new ArgumentException("库存请求去重损坏");}
                foreach(var raw in snapshot.wipes){if(!Guid.TryParseExact(raw,"N",out var id)||id==Guid.Empty||!prepared.wipes.Add(id))throw new ArgumentException("全灭去重记录损坏");}
                candidate=prepared;return true;
            }
            catch(Exception ex) when(ex is ArgumentException||ex is InvalidOperationException||ex is OverflowException){error=ex.Message;return false;}
        }
        internal void PublishRestore(RestoreCandidate candidate,Action publish)
        {
            // 候选已构建好全部字典/集合；发布不再分配或逐条修改活账本，计数溢出在验证期拒绝。
            containers=candidate.containers;items=candidate.items;requests=candidate.requests;wipes=candidate.wipes;reservations.Clear();
            AccessGeneration=candidate.accessGeneration+1;mutationRevision=candidate.revision+1;publish();
        }
    }
}
