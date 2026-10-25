/*
 * 脚本：BgmArbitrator
 * 功能：按照场景快照与 SO 规则的优先级，纯计算最终音乐选择。
 * 负责：过滤不匹配或非法规则；同优先级按配置顺序稳定选择。
 * 不负责：判断玩法交战、读取输入、持有 AudioSource、对话 duck 或启动播放。
 * 协作：BgmContext + BgmRuleSetSO → BgmChoice；具体曲名与优先级全部来自配置。
 */
using System;

namespace Ossastra.Audio
{
    public static class BgmArbitrator
    {
        /// <summary>无合法匹配规则时返回 null；不会以硬编码曲名偷偷补齐缺失配置。</summary>
        public static BgmChoice Choose(BgmContext context, BgmRuleSetSO ruleSet)
        {
            if (ruleSet == null || ruleSet.rules == null) return null;

            BgmRuleBinding selected = null;
            foreach (var rule in ruleSet.rules)
            {
                if (rule == null || string.IsNullOrWhiteSpace(rule.ruleId) || rule.track == null ||
                    !Matches(context, rule.condition) || !rule.track.TryGetIdentity(out _, out _) ||
                    float.IsNaN(rule.intensity) || float.IsInfinity(rule.intensity)) continue;
                if (selected == null || rule.priority > selected.priority) selected = rule;
            }
            if (selected == null) return null;
            float intensity = Math.Max(0, Math.Min(1, selected.intensity));
            return new BgmChoice(selected.ruleId, selected.priority, intensity, selected.track, selected.condition);
        }

        private static bool Matches(BgmContext context, BgmRuleCondition condition)
        {
            switch (condition)
            {
                case BgmRuleCondition.Always: return true;
                case BgmRuleCondition.Far: return context.far;
                case BgmRuleCondition.Title: return context.title;
                case BgmRuleCondition.Engaged: return context.engaged;
                case BgmRuleCondition.Boss: return context.boss;
                default: return false;
            }
        }
    }
}
