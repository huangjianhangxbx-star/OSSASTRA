using System;
using System.Collections.Generic;

/// <summary>
/// Applies the default anti-focus-fire rule only while a new hostile
/// single-target action is choosing its target. It never mutates a committed
/// enemy plan and never changes area target sets.
/// </summary>
public static class DRPGPostureBreakTargetPolicy
{
    public static void ApplyNewSelectionPolicy(
        DRPGSkillSO skill,
        List<BattleParticipantRuntime> legalCandidates)
    {
        if (skill == null)
            throw new ArgumentNullException(nameof(skill));
        if (legalCandidates == null)
            throw new ArgumentNullException(nameof(legalCandidates));
        if (legalCandidates.Count <= 1 ||
            skill.TargetRule != DRPGSkillTargetRule.SingleEnemy ||
            skill.IgnorePostureBreakProtection)
        {
            return;
        }

        bool hasStableAlternative = false;
        for (int i = 0; i < legalCandidates.Count; i++)
        {
            BattleParticipantRuntime candidate = legalCandidates[i];
            if (candidate != null && !candidate.PostureBreak.IsExposed)
            {
                hasStableAlternative = true;
                break;
            }
        }

        if (!hasStableAlternative)
            return;

        for (int i = legalCandidates.Count - 1; i >= 0; i--)
        {
            BattleParticipantRuntime candidate = legalCandidates[i];
            if (candidate != null && candidate.PostureBreak.IsExposed)
                legalCandidates.RemoveAt(i);
        }
    }
}
