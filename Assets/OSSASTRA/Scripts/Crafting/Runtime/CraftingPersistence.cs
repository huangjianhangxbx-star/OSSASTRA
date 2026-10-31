/* 脚本：CraftingPersistence
 * 功能：有界合成回执参加Commerce原共同快照，Main/Transit使用同一全局目录。
 * 边界：不写文件、不补旧档、不授予；恢复仅安装已完全验证的候选引用。
 */
using System;
using System.Linq;
using Newtonsoft.Json;
using Ossastra.Crafting.Core;
using Ossastra.Commerce.Runtime;
namespace Ossastra.Crafting
{
    public sealed partial class CraftingRoot
    {
        [Serializable] sealed class Payload
        {
            // 反序列化缺字段时保持0；不能让初始化默认1把缺schema的载荷伪装成有效版本。
            public int schema;
            public string catalogFingerprint;
            public CraftReceiptRecord[] receipts;
        }
        public CommerceSnapshotSection Capture()=>new CommerceSnapshotSection{key=SnapshotKey,schema=1,
            payload=JsonConvert.SerializeObject(new Payload{schema=1,catalogFingerprint=catalogFingerprint,receipts=journal.Records.ToArray()})};
        public bool TryPrepareRestore(CommerceSnapshotSection section,CommerceRuntimeSnapshot aggregate,out object prepared,out string error)
        {
            prepared=null;error=null;
            try
            {
                if(section==null||section.key!=SnapshotKey||section.schema!=1||aggregate?.economy?.inventory?.items==null)throw new ArgumentException("合成共同快照缺失或版本不兼容");
                var value=JsonConvert.DeserializeObject<Payload>(section.payload,new JsonSerializerSettings{MissingMemberHandling=MissingMemberHandling.Error});
                if(value==null||value.schema!=1||value.catalogFingerprint!=catalogFingerprint||value.receipts==null||value.receipts.Length>128)throw new ArgumentException("合成目录指纹或回执结构不兼容；原档保留");
                var next=CraftReceiptJournal.Restore(value.receipts,catalog);
                foreach(var receipt in next.Records)
                {
                    var station=stationCatalog.SingleOrDefault(s=>s.definitionId==receipt.stationDefinitionId);
                    if(station==null||!station.supported||!catalog.TryGetRecipe(receipt.recipeId,out var recipe)||!station.capabilities.Contains(recipe.StationCapability))throw new ArgumentException("回执工位定义或配方能力不匹配");
                    if(!aggregate.ownerIds.Contains(receipt.actorId))throw new ArgumentException("回执操作者不属于共同档案队伍");
                    // 成品可以已移动、合堆、消费；仍存在的实例必须保持原定义，不能借回执变成另一件物品。
                    foreach(var output in receipt.outputs)
                    {
                        var item=aggregate.economy.inventory.items.SingleOrDefault(i=>i.id==output.itemId);
                        if(item!=null&&item.definitionId!=output.definitionId)throw new ArgumentException("合成回执与共同库存物品身份冲突");
                    }
                }
                prepared=next;return true;
            }
            catch(Exception ex) when(ex is ArgumentException||ex is InvalidOperationException||ex is JsonException||ex is OverflowException)
            {error=ex.Message;return false;}
        }
        public void PublishRestore(object prepared)
        {
            journal=(CraftReceiptJournal)prepared;session=null;ticket=null;rows=Array.Empty<Presentation.CraftingRecipePresentation>();refreshRevision++;
        }
    }
}
