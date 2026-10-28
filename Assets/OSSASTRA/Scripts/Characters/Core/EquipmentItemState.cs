/* 脚本：EquipmentItemState
 * 功能：一件装备的稳定身份、耐久与能力payload；随唯一物品实例跨容器流转。
 * 边界：不拥有位置/角色HP，不因卸装、返场、HUD重建而重置资源。
 * 持久化：Inventory/EquipmentPersistentSnapshot中的partial只导出/校验恢复字段，不改变下方能力算法。
 */
using System;
using System.Collections.Generic;
using Ossastra.CharacterCore.Abilities;
namespace Ossastra.CharacterCore
{
    public sealed partial class EquipmentItemState
    {
        private readonly HashSet<Guid> wear=new HashSet<Guid>();
        public string Id {get;}
        public string DefinitionId {get;}
        public OffhandKind Kind {get;}
        public int Durability {get;private set;}
        public EquipmentAbilityState Runtime {get;}
        public EquipmentItemState(string id,string definitionId,OffhandKind kind,int durability,EquipmentAbilityState runtime=null)
        {
            if(string.IsNullOrWhiteSpace(id)||string.IsNullOrWhiteSpace(definitionId)||durability<1)throw new ArgumentException("Invalid equipment instance");
            Id=id;DefinitionId=definitionId;Kind=kind;Durability=durability;Runtime=runtime??new EquipmentAbilityState();
        }
        public bool Wear(Guid execution)
        {if(execution==Guid.Empty||Durability<=0||!wear.Add(execution))return false;Durability--;return true;}
    }
    /// <summary>物品服务唯一拥有手槽位置；装备门面只读这一契约，不复制手槽。</summary>
    public interface IEquipmentSlots
    {
        EquipmentItemState Right {get;}
        EquipmentItemState Left {get;}
    }
}
