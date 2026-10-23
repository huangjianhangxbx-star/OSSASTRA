/* 脚本：InventorySkinSO
 * 功能：冻结原型背包边框、圆角与字形资源入口；真实尺寸按inventory-layout.ts单独迁移。
 * 边界：只含可共享表现资源，不保存物品或窗口运行状态。
 */
using UnityEngine;
namespace Ossastra.UserInterface
{
    [CreateAssetMenu(menuName="OSSASTRA/UI/Inventory Skin")]
    public sealed class InventorySkinSO:ScriptableObject
    {
        public Sprite frame,slotFill,slotEdge;
        public string fontName="Microsoft YaHei";
    }
}
