/* 脚本：MerchantDefinitionSO
 * 功能：编辑商店目录、明确买卖价及目标库存；模板与各商人运行存货分离。
 * 边界：测试价格不代表最终平衡；零价必须显式允许，不隐式按售价乘系数。
 */
using System;
using System.Linq;
using UnityEngine;
using Ossastra.Inventory;
using Ossastra.Characters;
namespace Ossastra.Commerce.Runtime
{
    using ItemDefinitionSO=Ossastra.Characters.ItemDefinitionSO;
    [Serializable] public sealed class MerchantGoodsConfig
    {
        public ItemDefinitionSO item;
        public long buyPrice=10,sellPrice=5;
        [Min(0)] public int targetStock=10;
        public bool allowZeroPrice;
    }
    [CreateAssetMenu(menuName="OSSASTRA/Commerce/商店目录")]
    public sealed class MerchantDefinitionSO:ScriptableObject
    {
        public string definitionId="sample-general";
        [Min(1)] public float refreshSeconds=300;
        public MerchantGoodsConfig[] goods=Array.Empty<MerchantGoodsConfig>();
        public MerchantDefinition Freeze()=>new MerchantDefinition(definitionId,goods.Select(g=>
            new MerchantGoods(g.item==null?null:g.item.definitionId,g.buyPrice,g.sellPrice,g.targetStock,g.allowZeroPrice)).ToArray(),refreshSeconds);
    }
}
