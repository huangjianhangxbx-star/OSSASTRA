/* 脚本：EquipmentState
 * 功能：按定义身份持有本体左右手实例及左手耐久；同类不同盾保持不同实例，切换不重建状态。
 * 边界：不启动动作、不管理CD或背包；调用方验证空闲后才允许切装。
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
        public string OwnerId {get;}
        public string RightInstanceId {get;}
        public EquipmentAbilityState RightRuntime {get;}=new EquipmentAbilityState();
        public string LeftDefinitionId {get;private set;}
        private Item Current=>LeftDefinitionId!=null?items[LeftDefinitionId]:null;
        public OffhandKind LeftKind=>Current==null?OffhandKind.None:Current.kind;
        public string LeftInstanceId=>Current==null?"none":OwnerId+":left:"+Current.id;
        public int Durability=>Current==null?0:Current.durability;
        public EquipmentAbilityState LeftRuntime=>Current?.runtime;
        public EquipmentAbilityState RuntimeFor(string instanceId)
        {return instanceId==RightInstanceId?RightRuntime:items.Values.FirstOrDefault(i=>OwnerId+":left:"+i.id==instanceId)?.runtime;}
        public EquipmentState(string actorId,int durability)
        {
            if(string.IsNullOrWhiteSpace(actorId)||durability<=0)throw new ArgumentException("Invalid equipment owner/durability");
            OwnerId=actorId;RightInstanceId=actorId+":right:"+Guid.NewGuid().ToString("N");
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
        {if(!idle||definitionId==null||!items.ContainsKey(definitionId))return false;LeftDefinitionId=definitionId;return true;}
        // 旧验证场的枚举入口只访问其既有兼容实例；新候选通过定义ID装配，不能用类别充当实例ID。
        public bool TryEquip(OffhandKind kind,bool idle)
        {
            if(!idle||!Enum.IsDefined(typeof(OffhandKind),kind))return false;
            var id=kind==OffhandKind.None?null:"legacy:"+kind;
            if(id!=null&&!items.ContainsKey(id))return false;LeftDefinitionId=id;return true;
        }
        public bool Wear(Guid token)
            =>WearInstance(LeftInstanceId,token);
        public bool WearInstance(string instanceId,Guid token)
        {
            if(instanceId==null||token==Guid.Empty)return false;
            var item=items.Values.FirstOrDefault(i=>OwnerId+":left:"+i.id==instanceId);
            if(item==null||item.durability<=0||!item.wear.Add(token))return false;
            item.durability--;return true;
        }
    }
}
