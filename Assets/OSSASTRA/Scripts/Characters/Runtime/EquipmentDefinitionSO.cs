/* 脚本：EquipmentDefinitionSO
 * 功能：装备内容定义、能力类别与手持表现配置。
 * 边界：不持有实例ID、耐久、冷却；这些仍由原本体装备/副手状态拥有。
 */
using UnityEngine;
using Ossastra.CharacterCore;
namespace Ossastra.Characters
{
    [CreateAssetMenu(menuName="OSSASTRA/Equipment/Equipment Definition")]
    public sealed class EquipmentDefinitionSO:ScriptableObject
    {
        public string definitionId,displayName;
        public bool mainHand;
        public OffhandKind offhandKind;
        public Sprite worldSprite;
        public string boneName;
        public Vector3 offset;
        [Min(.01f)] public float worldScale=.7f;
        public ActionDefinitionSO[] actions;
    }
}
