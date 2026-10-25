/*
 * 脚本：BgmContracts
 * 功能：集中声明音乐条件和最终选曲结果，区分规则原因与播放内容。
 * 边界：结果不拥有音源或播放进度；同内容的规则切换由 Transport 保持连续。
 * 协作：BgmRuleSetSO → BgmArbitrator → GameAudioRoot。
 */
namespace Ossastra.Audio
{
    public enum BgmRuleCondition
    {
        Always,
        Far,
        Title,
        Engaged,
        Boss
    }

    /// <summary>一次仲裁的只读结果；规则身份用于诊断，不作为是否重新播放的依据。</summary>
    public sealed class BgmChoice
    {
        public string RuleId { get; }
        public BgmRuleCondition Condition { get; }
        public int Priority { get; }
        public float Intensity { get; }
        public BgmTrackSO Track { get; }
        public string Reason { get; }

        public BgmChoice(string ruleId, int priority, float intensity, BgmTrackSO track, BgmRuleCondition condition)
        {
            RuleId = ruleId;
            // 下游以条件语义处理标题的hold例外，不能依赖作者可重命名的ruleId。
            Condition = condition;
            Priority = priority;
            Intensity = intensity;
            Track = track;
            Reason = "命中音乐规则：" + ruleId;
        }
    }
}
