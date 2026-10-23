/* 脚本：ItemDefinitionSO
 * 功能：共享瓶及真实库存物品的作者定义；SO不保存数量或装备运行payload。
 * 边界：共享Y仍归原服务，普通物品归InventoryLedger；使用由配置映射能力。
 */
using UnityEngine;
namespace Ossastra.Characters
{
    public enum ItemRarity { Gray,Blue,Purple,Gold,RedGold }
    public enum InventoryUseKind { None,Regeneration,Ability }
    [CreateAssetMenu(menuName="OSSASTRA/Items/Healing Definition")]
    public sealed class ItemDefinitionSO:ScriptableObject
    {
        public string definitionId,displayName;
        [Min(1)] public int healAmount=100;
        [TextArea] public string description;
        public string category;
        public ItemRarity rarity;
        public Sprite icon;
        [Tooltip("无图标时的原型字形；由内容配置，不从名称猜测。")] public string inventoryGlyph;
        [Min(1)] public int width=1,height=1,maxStack=99;
        [Min(0)] public int unitWeightGrams;
        [Tooltip("负数表示未定价；本轮只保留元数据，不接交易消费者。")] public int sellValue=-1;
        public EquipmentDefinitionSO equipment;
        public string[] allowedOwners=System.Array.Empty<string>();
        public InventoryUseKind useKind;
        [Min(0)] public float regenerationPerSecond,regenerationSeconds;
        public AbilityDefinitionSO ability;
        public string temporarySocketId;
        [Min(.01f)] public float temporaryVisualScale=.35f,temporaryVisualSeconds=.35f;
        public Ossastra.Inventory.InventoryDefinition FreezeInventory()
            =>new Ossastra.Inventory.InventoryDefinition(definitionId,width,height,equipment!=null?1:maxStack,
                equipment==null?Ossastra.Inventory.InventoryHand.None:equipment.mainHand?Ossastra.Inventory.InventoryHand.Right:Ossastra.Inventory.InventoryHand.Left,unitWeightGrams,allowedOwners);
    }
}
