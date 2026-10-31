/* 脚本：CraftingTransactions
 * 功能：只读材料/最终容量预览与原Ledger同步提交，成功事实与库存同一发布。
 * 边界：已签发票据绑定会话和完整来源；旧回执可重读，不重新扣料/生成。无任意请求ID准入口。
 */
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Ossastra.CharacterCore;
using Ossastra.Inventory;
using Ossastra.Crafting.Core;
using Ossastra.Crafting.Presentation;
namespace Ossastra.Crafting
{
    public sealed partial class CraftingRoot
    {
        public CraftingPreviewPresentation Preview(string recipeId,int quantity)
        {
            ticket=null;
            var error=SessionError();
            if(error!=null)return DeniedPreview(error,quantity);
            if(!session.station.definition.supported)return DeniedPreview(session.station.definition.unavailableReason,quantity);
            if(!catalog.TryGetRecipe(recipeId,out var recipe)||!session.station.definition.capabilities.Contains(recipe.StationCapability))return DeniedPreview("该工位不支持此配方",quantity);
            if(quantity<1||quantity>recipe.MaxBatch)return new CraftingPreviewPresentation(false,"份数必须在1至"+recipe.MaxBatch+"之间",null,quantity,recipe.MaxBatch,"","");
            if(!ConfigurationMatches())return DeniedPreview("作者配置已变化，请重新载入样板",quantity);
            var materials=inventory.Ledger.Items.Where(i=>i.Location.ContainerId=="team").Select(i=>new CraftMaterialStack(i.Id,i.DefinitionId,i.Quantity,inventory.Ledger.HasReservation(i.Id),i.Equipment!=null)).ToArray();
            var plan=CraftPlanner.Plan(recipe,quantity,materials);
            string detail=string.Join("\n",plan.Required.Select(p=>{
                plan.Available.TryGetValue(p.Key,out var count);return Name(p.Key)+"  "+count+" / "+p.Value+(count<p.Value?"  缺 "+(p.Value-count):"  ✓");}));
            if(!plan.Ok)return new CraftingPreviewPresentation(false,"团队可用材料不足或数量无效（"+plan.Reason+"）",null,quantity,recipe.MaxBatch,Summary(recipe,Math.Max(1,quantity)),detail);
            try
            {
                var grants=BuildGrants(recipe,quantity);
                var consumes=plan.Consumptions.Select(c=>new InventoryConsumption(c.ItemId,c.DefinitionId,c.Quantity)).ToArray();
                long revision=inventory.Ledger.MutationRevision,access=inventory.Ledger.AccessGeneration;
                var candidate=inventory.PrepareTeamConversion(consumes,grants,revision,access,out error);
                if(candidate==null)return new CraftingPreviewPresentation(false,error,null,quantity,recipe.MaxBatch,Summary(recipe,quantity),detail);
                var id=Guid.NewGuid().ToString("N");
                var fingerprint=CraftIdentity.Fingerprint(recipe.Id,recipe.Version,session.stationDefinitionId,session.stationInstanceId,mapId,session.actorId,
                    session.controlRevision,session.id,id,quantity,revision,access,plan.Consumptions);
                ticket=new Ticket{id=id,fingerprint=fingerprint,recipe=recipe,quantity=quantity,revision=revision,access=access,conversion=candidate,consumes=plan.Consumptions.ToArray()};
                return new CraftingPreviewPresentation(true,"将整批制作并放入团队背包；不会自动装备",id,quantity,recipe.MaxBatch,Summary(recipe,quantity),detail);
            }
            catch(Exception ex) when(ex is ArgumentException||ex is OverflowException||ex is InvalidOperationException){return DeniedPreview("预览拒绝："+ex.Message,quantity);}
        }
        CraftingPreviewPresentation DeniedPreview(string error,int quantity)=>new CraftingPreviewPresentation(false,error,null,quantity,1,"","");
        InventoryContainerGrant[] BuildGrants(CraftRecipe recipe,int quantity)
        {
            var grants=new List<InventoryContainerGrant>();
            foreach(var row in authors[recipe.Id].outputs)
            {
                int count=checked(row.quantity*quantity);var item=row.item;
                if(item.equipment==null)grants.Add(new InventoryContainerGrant(item.definitionId,count));
                else for(int i=0;i<count;i++)grants.Add(new InventoryContainerGrant(item.definitionId,1,new EquipmentItemState(Guid.NewGuid().ToString("N"),item.definitionId,item.equipment.offhandKind,row.initialDurability)));
            }
            return grants.ToArray();
        }
        string Name(string id)=>inventory.Definition(id)==null?id:inventory.Definition(id).displayName;
        string Summary(CraftRecipe recipe,int quantity)=>string.Join("、",recipe.Outputs.Select(o=>Name(o.DefinitionId)+" ×"+checked(o.Quantity*quantity)));
        public CraftingResultPresentation Confirm(string requestId)
        {
            var stored=journal.Records.FirstOrDefault(r=>r.requestId==requestId);
            return ConfirmCommand(requestId,stored==null?ticket?.fingerprint:stored.fingerprint);
        }
        /// <summary>仅当前签发preview可准入；显式hash端口用于确认输送与冲突验证，不接受重建/任意ID命令。</summary>
        public CraftingResultPresentation ConfirmCommand(string requestId,string fingerprint)
        {
            if(journal.Find(requestId,fingerprint,out var old,out var conflict))return Result(old,true);
            if(conflict!=null)return Reject(conflict);
            var current=ticket;
            if(current==null||string.IsNullOrWhiteSpace(requestId)||requestId!=current.id||fingerprint!=current.fingerprint)return Reject("制作票据已失效，请重新预览");
            var denied=FinalCheck(current);if(denied!=null){ticket=null;return Reject(denied);}
            // 世界不暂停，原装备时钟会逐帧变化。预览只证明布局，不能跨帧保留经济批次的装备签名。
            // 仍使用票据固定的精确材料实例/数量与revision；确认时重新准备候选并在本同步段提交，绝不换料。
            current.conversion=inventory.PrepareTeamConversion(current.consumes.Select(c=>new InventoryConsumption(c.ItemId,c.DefinitionId,c.Quantity)).ToArray(),
                BuildGrants(current.recipe,current.quantity),current.revision,current.access,out var prepareError);
            if(current.conversion==null){ticket=null;refreshRevision++;return Reject(prepareError);}
            // 这里先分配回执、输出事实和有界候选journal；发布段不会调用工厂/分配/物理查询/通知。
            var receipt=new CraftReceiptRecord{requestId=current.id,fingerprint=current.fingerprint,recipeId=current.recipe.Id,version=current.recipe.Version,quantity=current.quantity,
                stationDefinitionId=session.stationDefinitionId,stationInstanceId=session.stationInstanceId,mapId=mapId,actorId=session.actorId,controlRevision=session.controlRevision,sessionId=session.id,
                ledgerRevision=current.revision,accessGeneration=current.access,
                consumptions=current.consumes.Select(c=>new CraftReceiptConsumption{itemId=c.ItemId,definitionId=c.DefinitionId,quantity=c.Quantity}).ToArray(),
                outputs=current.conversion.Outputs.Select(o=>new CraftReceiptOutput{itemId=o.ItemId,definitionId=o.DefinitionId,quantity=o.QuantityAdded}).ToArray()};
            CraftReceiptJournal next;
            try{next=journal.PrepareAppend(receipt);}catch(ArgumentException ex){ticket=null;return Reject(ex.Message);}
            var result=inventory.CommitTeamConversion(current.conversion,()=>FinalCheck(current),()=>{journal=next;ticket=null;});
            if(!result.Ok){ticket=null;refreshRevision++;return Reject(result.Reason);}
            refreshRevision++;Diagnostic="已制作并记入共同快照";Notify();
            PlaySound(successSoundEvent,"success:"+receipt.requestId,session==null?transform.position:session.anchor);
            return Result(receipt,false);
        }
        string FinalCheck(Ticket current)
        {
            var denied=SessionError();if(denied!=null)return denied;
            if(ticket!=current||current.revision!=inventory.Ledger.MutationRevision||current.access!=inventory.Ledger.AccessGeneration)return "库存、访问或制作票据已变化";
            if(!ConfigurationMatches()||!catalog.TryGetRecipe(current.recipe.Id,out var recipe)||recipe.Version!=current.recipe.Version||!session.station.definition.supported||!session.station.definition.capabilities.Contains(recipe.StationCapability))return "配方版本、目录或工位能力已变化";
            var fingerprint=CraftIdentity.Fingerprint(recipe.Id,recipe.Version,session.stationDefinitionId,session.stationInstanceId,mapId,session.actorId,session.controlRevision,session.id,current.id,current.quantity,current.revision,current.access,current.consumes);
            return fingerprint==current.fingerprint?null:"制作请求指纹不一致";
        }
        CraftingResultPresentation Reject(string reason)
        {
            Diagnostic=reason;PlaySound(rejectSoundEvent,"reject:"+Time.frameCount,session==null?transform.position:session.anchor);
            return new CraftingResultPresentation(false,reason,"");
        }
        CraftingResultPresentation Result(CraftReceiptRecord record,bool repeated)
        {
            string output=string.Join("\n",record.outputs.Select(o=>Name(o.definitionId)+" ×"+o.quantity+" → 团队背包\n实例 "+o.itemId));
            return new CraftingResultPresentation(true,repeated?"已完成请求的原回执；未重复扣料或交付":"制作成功；在团队背包中选择成品装备",output);
        }
    }
}
