/* 脚本：CraftingStationDefinitionSO
 * 功能：工位类型与能力作者配置；定义ID和场景实例ID分离。
 * 边界：固定不可破坏工位，不持有材料、成品队列或独立库存。
 */
using System;
using System.Linq;
using UnityEngine;
namespace Ossastra.Crafting
{
    [CreateAssetMenu(menuName="OSSASTRA/Crafting/Station Definition")]
    public sealed class CraftingStationDefinitionSO:ScriptableObject
    {
        [Header("工位类型身份 / 同定义可有多个独立实例")]
        public string definitionId,displayName;
        public string[] capabilities=Array.Empty<string>();
        [Tooltip("关闭后展示拒绝理由；不会开放任何配方")]
        public bool supported=true;
        public string unavailableReason="该设备尚未接入合成";
        public string Stamp()
        {
            if(string.IsNullOrWhiteSpace(definitionId)||string.IsNullOrWhiteSpace(displayName)||capabilities==null||capabilities.Length>32||capabilities.Any(string.IsNullOrWhiteSpace)||capabilities.Distinct(StringComparer.Ordinal).Count()!=capabilities.Length)
                throw new ArgumentException("工位类型身份/能力缺失或重复");
            return definitionId+"|"+supported+"|"+string.Join("|",capabilities.OrderBy(v=>v,StringComparer.Ordinal));
        }
    }
}
