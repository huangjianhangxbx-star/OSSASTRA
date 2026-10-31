/* 脚本：InventoryContainerGrant
 * 功能：新逻辑容器首次注册的整批授予；可把宿主记录和既定奖励回执纳入同一发布段，再通知观察者。
 * 边界：只包含纯定义ID/数量/payload，不引用运行时SO，也不形成第二本库存。
 */
using System;
using System.Collections.Generic;
using Ossastra.CharacterCore;
namespace Ossastra.Inventory
{
    public sealed class InventoryContainerGrant
    {
        public string DefinitionId {get;}
        public int Quantity {get;}
        public EquipmentItemState Equipment {get;}
        public InventoryContainerGrant(string definitionId,int quantity,EquipmentItemState equipment=null)
        {DefinitionId=definitionId;Quantity=quantity;Equipment=equipment;}
    }
    public sealed partial class InventoryLedger
    {
        /// <summary>只能为不存在的稳定ID创建逻辑容器；所有授予试算完成后一次提交，重复注册不再赠送。</summary>
        public InventoryResult CreateContainerWithItems(InventoryContainer container,InventoryContainerGrant[] grants)
            =>CreateContainerWithItems(container,grants,null);

        /// <summary>publish只切换预分配状态，不得分配、重入账本或发送通知；异常时账本恢复到提交前。</summary>
        public InventoryResult CreateContainerWithItems(InventoryContainer container,InventoryContainerGrant[] grants,Action publish)
        {
            if(container==null||grants==null||Container(container.Id)!=null)return InventoryResult.Reject("容器身份重复或初始授予缺失");
            if(!Validate(out var error))return InventoryResult.Reject(error);
            var expected=mutationRevision;
            var candidate=new InventoryLedger();foreach(var definition in definitions.Values)candidate.Register(definition);
            foreach(var current in containers.Values)candidate.AddContainer(current);
            var added=new InventoryContainer(container.Id,container.Columns,container.Rows,container.Slots,container.Owner,container.Hand);
            candidate.AddContainer(added);candidate.items=Copy();
            foreach(var grant in grants)
            {
                if(grant==null)return InventoryResult.Reject("初始授予条目缺失");
                var result=candidate.Grant(added.Id,grant.DefinitionId,grant.Quantity,grant.Equipment);
                if(!result.Ok)return result;
            }
            if(expected!=mutationRevision)return InventoryResult.Reject("库存已变化，容器未创建");
            // 所有容量/ID/payload检查已在独立候选完成；没有逐项发布或通知可观察的半箱。
            var nextContainers=new Dictionary<string,InventoryContainer>(containers,StringComparer.Ordinal){[added.Id]=added};added.Revision=1;
            var nextRevision=checked(mutationRevision+1);
            var oldContainers=containers;var oldItems=items;var oldRevision=mutationRevision;
            try
            {
                containers=nextContainers;items=candidate.items;mutationRevision=nextRevision;
                publish?.Invoke();
            }
            catch
            {containers=oldContainers;items=oldItems;mutationRevision=oldRevision;throw;}
            // 宿主世界记录和破坏回执此时也已就位；同步保存/UI观察者看不到半批奖励。
            NotifyEconomicCommit();return InventoryResult.Success(added.Id);
        }
    }
}
