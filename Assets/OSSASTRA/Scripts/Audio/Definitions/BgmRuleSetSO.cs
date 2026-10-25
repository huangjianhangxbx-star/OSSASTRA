/*
 * 脚本：BgmRuleSetSO
 * 功能：配置场景条件、选曲优先级、曲目和诊断强度。
 * 边界：规则 ID 与曲目身份独立；远室与普通探索可引用同一曲目而不重播。
 * 协作：BgmArbitrator 只读此配置；没有普通对话换曲规则。
 */
using System;
using UnityEngine;

namespace Ossastra.Audio
{
    [Serializable]
    public sealed class BgmRuleBinding
    {
        public string ruleId;
        public int priority;
        public BgmRuleCondition condition;
        public BgmTrackSO track;
        [Range(0, 1)] public float intensity;
    }

    [CreateAssetMenu(menuName = "OSSASTRA/Audio/BGM Rule Set", fileName = "BgmRuleSet")]
    public sealed class BgmRuleSetSO : ScriptableObject
    {
        [Tooltip("最高优先级获选；同优先级按本数组顺序稳定选第一项。")]
        public BgmRuleBinding[] rules = Array.Empty<BgmRuleBinding>();
    }
}
