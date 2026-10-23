/* 脚本：EquipmentState
 * 功能：装备能力只读门面；B装配读取物品服务的唯一手槽与payload，旧场保留原兼容实例。
 * 边界：绑定库存后不再自行切装或生成实例；不启动动作、不管理位置。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using Ossastra.CharacterCore.Abilities;
namespace Ossastra.CharacterCore
{
    public enum OffhandKind { None, Shield, Bomb, Other }
    public sealed class EquipmentState
    {
        private sealed class Item
        {
            public readonly string id=Guid.NewGuid().ToString("N");
            public OffhandKind kind;
            public int durability;
            public readonly HashSet<Guid> wear=new HashSet<Guid>();
            public readonly EquipmentAbilityState runtime=new EquipmentAbilityState();
        }
        private readonly Dictionary<string,Item> items=new Dictionary<string,Item>(StringComparer.Ordinal);
        private IEquipmentSlots slots;
        private readonly string legacyRightId;
        private readonly EquipmentAbilityState legacyRightRuntime=new EquipmentAbilityState();
        // 空手提供中立可读视图以兼容只读投影；不能以此获得任何来源资格。
        private readonly EquipmentAbilityState emptyRuntime=new EquipmentAbilityState();
        public bool InventoryBound=>slots!=null;
        public void BindSlots(IEquipmentSlots source)
        {if(source==null||slots!=null&&!ReferenceEquals(slots,source))throw new InvalidOperationException("Equipment inventory already bound");slots=source;}
        public string OwnerId {get;}
        public string RightInstanceId=>InventoryBound?slots.Right?.Id??"none":legacyRightId;
        public EquipmentAbilityState RightRuntime=>InventoryBound?slots.Right?.Runtime??emptyRuntime:legacyRightRuntime;
        private string legacyLeftDefinitionId;
        public string LeftDefinitionId=>InventoryBound?slots.Left?.DefinitionId:legacyLeftDefinitionId;
        private Item Current=>legacyLeftDefinitionId!=null?items[legacyLeftDefinitionId]:null;
        public OffhandKind LeftKind=>InventoryBound?slots.Left?.Kind??OffhandKind.None:Current==null?OffhandKind.None:Current.kind;
        public string LeftInstanceId=>InventoryBound?slots.Left?.Id??"none":Current==null?"none":OwnerId+":left:"+Current.id;
        public int Durability=>InventoryBound?slots.Left?.Durability??0:Current==null?0:Current.durability;
        public EquipmentAbilityState LeftRuntime=>InventoryBound?slots.Left?.Runtime:Current?.runtime;
        public EquipmentAbilityState RuntimeFor(string instanceId)
        {if(InventoryBound)return slots.Right?.Id==instanceId?slots.Right.Runtime:slots.Left?.Id==instanceId?slots.Left.Runtime:null;return instanceId==RightInstanceId?RightRuntime:items.Values.FirstOrDefault(i=>OwnerId+":left:"+i.id==instanceId)?.runtime;}
        public EquipmentState(string actorId,int durability)
        {
            if(string.IsNullOrWhiteSpace(actorId)||durability<=0)throw new ArgumentException("Invalid equipment owner/durability");
            OwnerId=actorId;legacyRightId=actorId+":right:"+Guid.NewGuid().ToString("N");
            RegisterDefinition("legacy:Shield",OffhandKind.Shield,durability);RegisterDefinition("legacy:Bomb",OffhandKind.Bomb,durability);
        }
        /// <summary>装配时登记持有定义，重复登记不刷新耐久/身份；实际流转将在唯一物品服务中接续。</summary>
        public void RegisterDefinition(string definitionId,OffhandKind kind,int durability)
        {
            if(string.IsNullOrWhiteSpace(definitionId)||kind==OffhandKind.None||!Enum.IsDefined(typeof(OffhandKind),kind)||durability<=0)
                throw new ArgumentException("Invalid equipment definition");
            if(items.TryGetValue(definitionId,out var prior))
            {if(prior.kind!=kind)throw new ArgumentException("Equipment definition category changed");return;}
            items.Add(definitionId,new Item{kind=kind,durability=durability});
        }
        public bool TryEquipDefinition(string definitionId,bool idle)
        {if(InventoryBound||!idle||definitionId==null||!items.ContainsKey(definitionId))return false;legacyLeftDefinitionId=definitionId;return true;}
        // 旧验证场的枚举入口只访问其既有兼容实例；新候选通过定义ID装配，不能用类别充当实例ID。
        public bool TryEquip(OffhandKind kind,bool idle)
        {
            if(InventoryBound||!idle||!Enum.IsDefined(typeof(OffhandKind),kind))return false;
            var id=kind==OffhandKind.None?null:"legacy:"+kind;
            if(id!=null&&!items.ContainsKey(id))return false;legacyLeftDefinitionId=id;return true;
        }
        public bool Wear(Guid token)
            =>WearInstance(LeftInstanceId,token);
        public bool WearInstance(string instanceId,Guid token)
        {
            if(instanceId==null||token==Guid.Empty)return false;
            if(InventoryBound)return slots.Left?.Id==instanceId?slots.Left.Wear(token):slots.Right?.Id==instanceId&&slots.Right.Wear(token);
            var item=items.Values.FirstOrDefault(i=>OwnerId+":left:"+i.id==instanceId);
            if(item==null||item.durability<=0||!item.wear.Add(token))return false;
            item.durability--;return true;
        }
    }
}
