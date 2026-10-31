/* 脚本：CraftingRecipeSO
 * 功能：配方作者数据冻结为不可变领域定义；材料与产物明确引用原物品目录。
 * 边界：只有普通材料、数量和新产物，不克隆旧装备，不决定库存位置或保存。
 */
using System;
using System.Linq;
using UnityEngine;
using Ossastra.Characters;
using Ossastra.Crafting.Core;
namespace Ossastra.Crafting
{
    using ItemDefinitionSO=Ossastra.Characters.ItemDefinitionSO;
    [Serializable] public sealed class CraftingIngredientAuthor
    {
        [Tooltip("原库存普通物品；装备不能作为本轮材料")]
        public ItemDefinitionSO item;
        [Min(1)] public int quantity=1;
    }
    [Serializable] public sealed class CraftingOutputAuthor
    {
        public ItemDefinitionSO item;
        [Min(1)] public int quantity=1;
        [Tooltip("仅新装备使用；不会继承角色当前装备耐久、资源或冷却")]
        [Min(1)] public int initialDurability=100;
    }
    [CreateAssetMenu(menuName="OSSASTRA/Crafting/Recipe")]
    public sealed class CraftingRecipeSO:ScriptableObject
    {
        [Header("稳定身份 / 改变结算内容请递增版本")]
        public string recipeId;
        [Min(1)] public int version=1;
        public string displayName,category="装备",stationCapability="equipment";
        [TextArea] public string description;
        public Sprite icon;
        [Header("SAMPLE配方 / 不自动扫描或解锁其他配方")]
        [Range(1,99)] public int maxBatch=8;
        public CraftingIngredientAuthor[] ingredients=Array.Empty<CraftingIngredientAuthor>();
        public CraftingOutputAuthor[] outputs=Array.Empty<CraftingOutputAuthor>();
        public CraftRecipe Freeze()
        {
            if(string.IsNullOrWhiteSpace(displayName)||ingredients==null||outputs==null||ingredients.Length>32||outputs.Length>32||
                ingredients.Any(r=>r==null||r.item==null||r.item.equipment!=null)||
                outputs.Any(r=>r==null||r.item==null||r.initialDurability<1||r.initialDurability>100000||r.item.equipment!=null&&r.item.definitionId!=r.item.equipment.definitionId))
                throw new ArgumentException("配方名称、普通材料、输出目录或初始耐久无效："+recipeId);
            return new CraftRecipe(recipeId,version,stationCapability,category,
                ingredients.Select(r=>new CraftIngredient(r.item.definitionId,r.quantity)),outputs.Select(r=>new CraftOutput(r.item.definitionId,r.quantity)),maxBatch);
        }
    }
}
