/* 脚本：InventoryContracts
 * 功能：物品事务的不可变定义、位置、条目快照与命令契约。
 * 边界：无Unity依赖；实例payload只引用唯一装备状态，位置修改权仅在Ledger。
 */
using System;
using Ossastra.CharacterCore;
namespace Ossastra.Inventory
{
    public enum InventoryHand { None,Left,Right }
    public sealed class InventoryDefinition
    {
        private readonly string[] owners;
        public string Id {get;}
        public int Width {get;}
        public int Height {get;}
        public int MaxStack {get;}
        public InventoryHand Hand {get;}
        public int WeightGrams {get;}
        public InventoryDefinition(string id,int width,int height,int maxStack,InventoryHand hand=InventoryHand.None,int weightGrams=0,string[] owners=null)
        {
            if(string.IsNullOrWhiteSpace(id)||width<1||height<1||maxStack<1||weightGrams<0||!Enum.IsDefined(typeof(InventoryHand),hand)||hand!=InventoryHand.None&&maxStack!=1)throw new ArgumentException("Invalid inventory definition");
            Id=id;Width=width;Height=height;MaxStack=maxStack;Hand=hand;WeightGrams=weightGrams;this.owners=owners==null?Array.Empty<string>():(string[])owners.Clone();
        }
        public bool AllowsOwner(string id)=>owners.Length==0||Array.IndexOf(owners,id)>=0;
        /// <summary>纯交易指纹包含装备拥有资格；不暴露可修改的定义数组。</summary>
        internal string[] AllowedOwners=>(string[])owners.Clone();
    }
    public sealed class InventoryContainer
    {
        public string Id {get;}
        public int Columns {get;}
        public int Rows {get;}
        public bool Slots {get;}
        public string Owner {get;}
        public InventoryHand Hand {get;}
        public long Revision {get;internal set;}
        public InventoryContainer(string id,int columns,int rows,bool slots=false,string owner=null,InventoryHand hand=InventoryHand.None)
        {
            if(string.IsNullOrWhiteSpace(id)||columns<1||rows<1||!Enum.IsDefined(typeof(InventoryHand),hand)||hand!=InventoryHand.None&&(!slots||(long)columns*rows!=1||string.IsNullOrWhiteSpace(owner)))throw new ArgumentException("Invalid inventory container");
            Id=id;Columns=columns;Rows=rows;Slots=slots;Owner=owner;Hand=hand;
        }
    }
    public readonly struct InventoryLocation
    {
        public string ContainerId {get;}
        public int X {get;}
        public int Y {get;}
        public InventoryLocation(string containerId,int x,int y){ContainerId=containerId;X=x;Y=y;}
    }
    public sealed class InventoryItem
    {
        public string Id {get;}
        public string DefinitionId {get;}
        public int Quantity {get;internal set;}
        public InventoryLocation Location {get;internal set;}
        public EquipmentItemState Equipment {get;}
        internal InventoryItem(string id,string definitionId,int quantity,InventoryLocation location,EquipmentItemState equipment=null)
        {Id=id;DefinitionId=definitionId;Quantity=quantity;Location=location;Equipment=equipment;}
        internal InventoryItem Copy()=>new InventoryItem(Id,DefinitionId,Quantity,Location,Equipment);
    }
    public readonly struct InventoryResult
    {
        public bool Ok {get;}
        public string Reason {get;}
        public string ItemId {get;}
        private InventoryResult(bool ok,string reason,string itemId){Ok=ok;Reason=reason;ItemId=itemId;}
        public static InventoryResult Success(string id=null)=>new InventoryResult(true,null,id);
        public static InventoryResult Reject(string reason)=>new InventoryResult(false,reason,null);
    }
    public sealed class InventoryMove
    {
        public Guid RequestId {get;}=Guid.NewGuid();
        public string ItemId {get;}
        public string From {get;}
        public string To {get;}
        public long FromRevision {get;}
        public long ToRevision {get;}
        public long AccessGeneration {get;}
        public int Quantity {get;}
        public int? X {get;}
        public int? Y {get;}
        internal InventoryMove(InventoryItem item,InventoryContainer from,InventoryContainer to,long generation,int quantity,int? x,int? y)
        {ItemId=item.Id;From=from.Id;To=to.Id;FromRevision=from.Revision;ToRevision=to.Revision;AccessGeneration=generation;Quantity=quantity;X=x;Y=y;}
    }
    public sealed class InventorySortPlan
    {
        public string ContainerId {get;}
        public long Revision {get;}
        public long AccessGeneration {get;}
        public bool Feasible {get;}
        public bool Optimal=>false;
        internal readonly System.Collections.Generic.Dictionary<string,InventoryLocation> locations;
        internal readonly InventoryItem[] entries;
        internal InventorySortPlan(string id,long revision,long generation,bool feasible,System.Collections.Generic.Dictionary<string,InventoryLocation> locations,InventoryItem[] entries)
        {ContainerId=id;Revision=revision;AccessGeneration=generation;Feasible=feasible;this.locations=locations;this.entries=entries;}
    }
}
