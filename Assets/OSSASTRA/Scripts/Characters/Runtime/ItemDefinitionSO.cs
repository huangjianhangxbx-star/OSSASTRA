/* 脚本：ItemDefinitionSO
 * 功能：本轮共享治疗瓶的内容定义；数量仍由场景唯一服务持有。
 * 边界：不是每角色物品实例，不保存余额或首次补给标记。
 */
using UnityEngine;
namespace Ossastra.Characters
{
    [CreateAssetMenu(menuName="OSSASTRA/Items/Healing Definition")]
    public sealed class ItemDefinitionSO:ScriptableObject
    {
        public string definitionId,displayName;
        [Min(1)] public int healAmount=100;
    }
}
