/* 脚本：InventoryLedger
 * 功能：唯一物品注册/数量/位置权威；试算副本、校验修订后一次提交，失败保持原账本。
 * 边界：不读设备或世界、不创建能力；Unity访问适配在提交门检查身份/距离/LOS。
 * 协作：装备payload引用原实例，预留锁定来源堆，释放只消费一次；全灭是同场原子批次。
 * 经济扩展：InventoryEconomicBatch partial仅向Commerce友元开放共同提交，预留/容器变化推进事务修订。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using Ossastra.CharacterCore;
namespace Ossastra.Inventory
{
    public sealed partial class InventoryLedger
    {
        private readonly Dictionary<string,InventoryDefinition> definitions=new Dictionary<string,InventoryDefinition>(StringComparer.Ordinal);
        private Dictionary<string,InventoryContainer> containers=new Dictionary<string,InventoryContainer>(StringComparer.Ordinal);
        private Dictionary<string,InventoryItem> items=new Dictionary<string,InventoryItem>(StringComparer.Ordinal);
        private HashSet<Guid> requests=new HashSet<Guid>();
        private HashSet<Guid> wipes=new HashSet<Guid>();
        private sealed class Reservation {public string item;public int quantity;}
        private readonly Dictionary<Guid,Reservation> reservations=new Dictionary<Guid,Reservation>();
        public long AccessGeneration {get;private set;}
        public event Action Changed;
        public InventoryItem[] Items=>items.Values.Select(i=>i.Copy()).ToArray();
        public InventoryContainer[] Containers=>containers.Values.ToArray();
        public InventoryDefinition Definition(string id)=>id!=null&&definitions.TryGetValue(id,out var d)?d:null;
        public InventoryItem Find(string id)=>id!=null&&items.TryGetValue(id,out var i)?i.Copy():null;
        /// <summary>装备热路径只读同一payload，不为每帧的每次属性查询复制整本库存。</summary>
        public EquipmentItemState EquipmentAt(string containerId)
            =>items.Values.FirstOrDefault(i=>i.Location.ContainerId==containerId)?.Equipment;
        public InventoryContainer Container(string id)=>id!=null&&containers.TryGetValue(id,out var c)?c:null;
        public void Register(InventoryDefinition definition)
        {if(definition==null||definitions.ContainsKey(definition.Id))throw new ArgumentException("Duplicate/missing item definition");definitions.Add(definition.Id,definition);mutationRevision++;}
        public void AddContainer(InventoryContainer container)
        {if(container==null||containers.ContainsKey(container.Id))throw new ArgumentException("Duplicate/missing container");containers.Add(container.Id,container);mutationRevision++;}
        public bool RemoveEmptyContainer(string id)
        {if(id==null||items.Values.Any(i=>i.Location.ContainerId==id))return false;var removed=containers.Remove(id);if(removed)mutationRevision++;return removed;}
        public void InvalidateAccess(){AccessGeneration++;mutationRevision++;NotifyEconomicCommit();}
        public long WeightGrams(params string[] ids)=>items.Values.Where(i=>ids.Contains(i.Location.ContainerId)).Sum(i=>(long)definitions[i.DefinitionId].WeightGrams*i.Quantity);
        private Dictionary<string,InventoryItem> Copy()=>items.ToDictionary(p=>p.Key,p=>p.Value.Copy(),StringComparer.Ordinal);
        private bool Locked(string id)=>reservations.Values.Any(r=>r.item==id);
        private bool Fits(Dictionary<string,InventoryItem> state,InventoryItem item)
        {
            var c=Container(item.Location.ContainerId);var d=Definition(item.DefinitionId);
            if(c==null||d==null||item.Quantity<1||item.Quantity>d.MaxStack||item.Equipment!=null&&(item.Quantity!=1||item.Equipment.DefinitionId!=d.Id||item.Equipment.Id!=item.Id)||d.Hand!=InventoryHand.None&&item.Equipment==null)return false;
            if(c.Hand!=InventoryHand.None&&(d.Hand!=c.Hand||!d.AllowsOwner(c.Owner)||item.Equipment==null))return false;
            int w=c.Slots?1:d.Width,h=c.Slots?1:d.Height;var l=item.Location;
            // 存档/显式落点可带任意int；端点用long计算，避免正溢出绕过越界与相交检查。
            if(l.X<0||l.Y<0||(long)l.X+w>c.Columns||(long)l.Y+h>c.Rows)return false;
            return !state.Values.Any(other=>other.Id!=item.Id&&Intersects(item,other,c));
        }
        private bool Intersects(InventoryItem a,InventoryItem b,InventoryContainer c)
        {
            if(a.Location.ContainerId!=b.Location.ContainerId)return false;
            var da=definitions[a.DefinitionId];var db=definitions[b.DefinitionId];
            return (long)a.Location.X<(long)b.Location.X+(c.Slots?1:db.Width)&&(long)a.Location.X+(c.Slots?1:da.Width)>b.Location.X&&
                (long)a.Location.Y<(long)b.Location.Y+(c.Slots?1:db.Height)&&(long)a.Location.Y+(c.Slots?1:da.Height)>b.Location.Y;
        }
        private bool Place(Dictionary<string,InventoryItem> state,InventoryItem item,string to,int? x=null,int? y=null)
        {
            var c=Container(to);if(c==null)return false;
            if(x.HasValue!=y.HasValue)return false;
            if(x.HasValue){item.Location=new InventoryLocation(to,x.Value,y.Value);return Fits(state,item);}
            for(var row=0;row<c.Rows;row++)for(var col=0;col<c.Columns;col++)
            {item.Location=new InventoryLocation(to,col,row);if(Fits(state,item))return true;}
            return false;
        }
        private void Commit(Dictionary<string,InventoryItem> state,params string[] changed)
        {items=state;foreach(var id in changed.Distinct())containers[id].Revision++;mutationRevision++;NotifyEconomicCommit();}
        public InventoryResult Grant(string to,string definitionId,int quantity,EquipmentItemState equipment=null)
        {
            var d=Definition(definitionId);if(d==null||Container(to)==null||quantity<1||equipment!=null&&(quantity!=1||equipment.DefinitionId!=definitionId||items.ContainsKey(equipment.Id))||d.Hand!=InventoryHand.None&&equipment==null)return InventoryResult.Reject("物品定义或数量无效");
            var state=Copy();string last=null;
            if(equipment==null)foreach(var stack in state.Values.Where(i=>i.Location.ContainerId==to&&i.DefinitionId==definitionId&&i.Equipment==null&&!Locked(i.Id)).OrderBy(i=>i.Id))
            {var n=Math.Min(quantity,d.MaxStack-stack.Quantity);stack.Quantity+=n;quantity-=n;if(n>0)last=stack.Id;if(quantity==0)break;}
            while(quantity>0)
            {
                var item=new InventoryItem(equipment?.Id??Guid.NewGuid().ToString("N"),definitionId,Math.Min(d.MaxStack,quantity),default,equipment);
                if(!Place(state,item,to))return InventoryResult.Reject("背包容量不足，物品未领取");
                state.Add(item.Id,item);last=item.Id;quantity-=item.Quantity;
            }
            Commit(state,to);return InventoryResult.Success(last);
        }
        public InventoryMove Plan(string id,string to,int quantity=0,int? x=null,int? y=null)
        {
            if(!items.TryGetValue(id??"",out var item)||Container(to)==null)return null;
            return new InventoryMove(item,containers[item.Location.ContainerId],containers[to],AccessGeneration,quantity==0?item.Quantity:quantity,x,y);
        }
        private InventoryResult Move(InventoryMove command,bool commit,Func<InventoryMove,string> access)
        {
            if(command==null)return InventoryResult.Reject("物品或目标已失效");
            if(requests.Contains(command.RequestId))return InventoryResult.Reject("重复操作");
            if(command.AccessGeneration!=AccessGeneration||Container(command.From)?.Revision!=command.FromRevision||Container(command.To)?.Revision!=command.ToRevision)return InventoryResult.Reject("物品内容或访问已变化，请重试");
            if(!items.TryGetValue(command.ItemId,out var source)||source.Location.ContainerId!=command.From)return InventoryResult.Reject("物品已移动");
            var denied=access?.Invoke(command);if(denied!=null)return InventoryResult.Reject(denied);
            if(Locked(source.Id))return InventoryResult.Reject("道具已预留给动作，请稍后");
            if(command.Quantity<1||command.Quantity>source.Quantity||source.Equipment!=null&&command.Quantity!=1)return InventoryResult.Reject("非法拆分数量");
            if(!Validate(out var error))return InventoryResult.Reject(error);
            var state=Copy();var moving=state[source.Id];var original=moving.Location;
            bool split=command.Quantity<source.Quantity;
            if(split){moving.Quantity-=command.Quantity;moving=new InventoryItem(Guid.NewGuid().ToString("N"),source.DefinitionId,command.Quantity,original);}
            else state.Remove(moving.Id);
            var target=containers[command.To];var d=definitions[moving.DefinitionId];
            moving.Location=new InventoryLocation(command.To,command.X??0,command.Y??0);
            var explicitTarget=command.X.HasValue?state.Values.FirstOrDefault(i=>Intersects(moving,i,target)):null;
            var merge=command.X.HasValue?explicitTarget:state.Values.OrderBy(i=>i.Id).FirstOrDefault(i=>i.Location.ContainerId==command.To&&i.Equipment==null&&moving.Equipment==null&&i.DefinitionId==moving.DefinitionId&&(long)i.Quantity+moving.Quantity<=d.MaxStack&&!Locked(i.Id));
            if(merge!=null&&(merge.Equipment!=null||moving.Equipment!=null||merge.DefinitionId!=moving.DefinitionId))merge=null;
            if(merge!=null)
            {
                // MaxStack允许int上限；先以long验完整合并，自动转移仍可跳过满堆寻找空位。
                if(Locked(merge.Id)||(long)merge.Quantity+moving.Quantity>d.MaxStack)return InventoryResult.Reject("超过合法堆叠上限或目标已预留");
                merge.Quantity=checked(merge.Quantity+moving.Quantity);moving.Quantity=0;
            }
            else
            {
                // 手槽换装：被替换的唯一实例必须能完整放回来源；失败仍只影响试算副本。
                var displaced=explicitTarget??(target.Hand!=InventoryHand.None?state.Values.FirstOrDefault(i=>i.Location.ContainerId==command.To):null);
                if(displaced!=null)
                {
                    if(split)return InventoryResult.Reject("拆分物不能交换");
                    if(Locked(displaced.Id))return InventoryResult.Reject("原装备有未完成动作");
                    state.Remove(displaced.Id);
                    if(!Place(state,moving,command.To,command.X,command.Y))return InventoryResult.Reject("装备资格不符");
                    state.Add(moving.Id,moving);
                    if(!Place(state,displaced,command.From,original.X,original.Y)&&!(target.Hand!=InventoryHand.None&&Place(state,displaced,command.From)))return InventoryResult.Reject("双方尺寸或资格不允许交换");
                    state.Add(displaced.Id,displaced);
                }
                else {if(!Place(state,moving,command.To,command.X,command.Y))return InventoryResult.Reject("容量不足或目标位置不合法");state.Add(moving.Id,moving);}
            }
            if(commit){requests.Add(command.RequestId);Commit(state,command.From,command.To);}
            return InventoryResult.Success(moving.Quantity>0?moving.Id:merge?.Id);
        }
        public InventoryResult Query(InventoryMove command,Func<InventoryMove,string> access=null)=>Move(command,false,access);
        public InventoryResult Transfer(InventoryMove command,Func<InventoryMove,string> access=null)=>Move(command,true,access);
        public InventoryResult Reserve(string id,int quantity,Guid execution)
        {
            if(execution==Guid.Empty||reservations.ContainsKey(execution)||!items.TryGetValue(id??"",out var item)||item.Equipment!=null||quantity<1||quantity>item.Quantity||Locked(id))return InventoryResult.Reject("道具来源不可用");
            reservations.Add(execution,new Reservation{item=id,quantity=quantity});mutationRevision++;return InventoryResult.Success(id);
        }
        public InventoryResult CommitConsumption(Guid execution)
        {
            if(!reservations.TryGetValue(execution,out var reservation)||!items.TryGetValue(reservation.item,out var item)||item.Quantity<reservation.quantity)return InventoryResult.Reject("道具预留已失效");
            reservations.Remove(execution);var state=Copy();var consumed=state[item.Id];consumed.Quantity-=reservation.quantity;if(consumed.Quantity==0)state.Remove(item.Id);
            Commit(state,item.Location.ContainerId);return InventoryResult.Success(item.Id);
        }
        public void CancelReservation(Guid execution){if(reservations.Remove(execution))mutationRevision++;}
        public InventoryResult DropAll(Guid wipe,string[] sources,string dropId)
        {
            if(wipe==Guid.Empty||wipes.Contains(wipe)||Container(dropId)!=null||sources==null||sources.Any(s=>Container(s)==null))return InventoryResult.Reject("无效或重复全灭事件");
            var victims=items.Values.Where(i=>sources.Contains(i.Location.ContainerId)).ToArray();
            // 死亡终结先撤销尚未释放的预留；全灭转移本身永不消费、克隆或重置payload。
            var columns=Math.Max(5,victims.Select(i=>definitions[i.DefinitionId].Width).DefaultIfEmpty(1).Max());
            var rows=Math.Max(1,victims.Sum(i=>definitions[i.DefinitionId].Height));
            var drop=new InventoryContainer(dropId,columns,rows);AddContainer(drop);
            var state=Copy();foreach(var item in victims)state.Remove(item.Id);
            foreach(var item in victims)
            {var copy=item.Copy();if(!Place(state,copy,dropId)){containers.Remove(dropId);return InventoryResult.Reject("掉落包无法容纳完整批次");}state.Add(copy.Id,copy);}
            foreach(var token in reservations.Where(p=>victims.Any(i=>i.Id==p.Value.item)).Select(p=>p.Key).ToArray())reservations.Remove(token);
            wipes.Add(wipe);Commit(state,sources.Concat(new[]{dropId}).ToArray());return InventoryResult.Success(dropId);
        }
        public InventorySortPlan PlanSort(string containerId,int budget=1000)
        {
            var c=Container(containerId);if(c==null)throw new ArgumentException("Missing sort container");
            var state=Copy();var original=state.Values.Where(i=>i.Location.ContainerId==containerId).OrderBy(i=>i.Id,StringComparer.Ordinal).ToArray();
            var packed=new List<InventoryItem>();
            // 合并仅在计划副本中进行；保留稳定ID前缀，装备与已预留条目永不合堆。
            foreach(var group in original.Where(i=>i.Equipment==null).GroupBy(i=>i.DefinitionId))
            {
                long count=group.Sum(i=>(long)i.Quantity);var maximum=definitions[group.Key].MaxStack;
                foreach(var entry in group){if(count==0)break;entry.Quantity=(int)Math.Min(count,maximum);count-=entry.Quantity;packed.Add(entry);}
            }
            packed.AddRange(original.Where(i=>i.Equipment!=null));
            var selected=packed.OrderByDescending(i=>definitions[i.DefinitionId].Width*definitions[i.DefinitionId].Height).ThenBy(i=>i.DefinitionId,StringComparer.Ordinal).ThenBy(i=>i.Id,StringComparer.Ordinal).ToArray();
            foreach(var item in original)state.Remove(item.Id);
            var map=new Dictionary<string,InventoryLocation>();var feasible=true;
            if(original.Any(i=>Locked(i.Id)))return new InventorySortPlan(containerId,c.Revision,AccessGeneration,false,map,Array.Empty<InventoryItem>());
            foreach(var item in selected)
            {
                bool placed=false;
                if(Locked(item.Id)){feasible=false;break;}
                for(var y=0;y<c.Rows&&!placed;y++)for(var x=0;x<c.Columns&&!placed;x++)
                {if(--budget<0)break;item.Location=new InventoryLocation(containerId,x,y);if(Fits(state,item)){placed=true;state.Add(item.Id,item);map.Add(item.Id,item.Location);}}
                if(!placed){feasible=false;break;}
            }
            return new InventorySortPlan(containerId,c.Revision,AccessGeneration,feasible,map,selected.Select(i=>i.Copy()).ToArray());
        }
        public InventoryResult CommitSort(InventorySortPlan plan)
        {
            if(plan==null||!plan.Feasible)return InventoryResult.Reject("预算内未找到整理方案，仍可手动整理");
            if(plan.AccessGeneration!=AccessGeneration||Container(plan.ContainerId)?.Revision!=plan.Revision||items.Values.Any(i=>i.Location.ContainerId==plan.ContainerId&&Locked(i.Id)))return InventoryResult.Reject("物品已变化，请重新整理");
            var current=items.Values.Where(i=>i.Location.ContainerId==plan.ContainerId).ToArray();
            if(current.Any(i=>i.Equipment!=null&&!plan.entries.Any(p=>p.Id==i.Id&&ReferenceEquals(p.Equipment,i.Equipment)))||
                current.Select(i=>i.DefinitionId).Union(plan.entries.Select(i=>i.DefinitionId)).Any(d=>current.Where(i=>i.DefinitionId==d).Sum(i=>(long)i.Quantity)!=plan.entries.Where(i=>i.DefinitionId==d).Sum(i=>(long)i.Quantity)))return InventoryResult.Reject("整理数量或装备身份失效");
            var state=Copy();foreach(var item in current)state.Remove(item.Id);
            foreach(var entry in plan.entries){if(!items.TryGetValue(entry.Id,out var item)||item.Location.ContainerId!=plan.ContainerId)return InventoryResult.Reject("整理计划已失效");state.Add(entry.Id,entry.Copy());}
            if(state.Values.Any(i=>i.Location.ContainerId==plan.ContainerId&&!Fits(state,i)))return InventoryResult.Reject("整理布局无效");
            Commit(state,plan.ContainerId);return InventoryResult.Success();
        }
        public bool Validate(out string error)
        {
            foreach(var item in items.Values)if(!Fits(items,item)){error="物品布局或数量损坏："+item.Id;return false;}
            error=null;return true;
        }
    }
}
