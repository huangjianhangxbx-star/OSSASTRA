using System;
using System.Collections.Generic;

public enum DRPGEnemyActionSelectionPath
{
    None = 0,
    OrdinaryStandardAttack = 1,
    Legacy = 2,
    OrdinaryStandardSupport = 3
}

public readonly struct DRPGEnemyActionSelection
{
    public DRPGEnemyActionSelection(
        DRPGEnemyActionSelectionPath path,
        DRPGSkillSO skill,
        bool isBasicAttack,
        int targetRuntimeId = 0)
    {
        Path = path;
        Skill = skill;
        IsBasicAttack = isBasicAttack;
        TargetRuntimeId = targetRuntimeId;
    }

    public DRPGEnemyActionSelectionPath Path { get; }
    public DRPGSkillSO Skill { get; }
    public bool IsBasicAttack { get; }
    public int TargetRuntimeId { get; }
}

public sealed class DRPGEnemyActionPlanner
{
    public DRPGEnemyActionSelection SelectNextAction(
        BattleParticipantRuntime actor,
        Func<DRPGSkillSO, bool> canAfford,
        Func<DRPGSkillSO, bool> hasLegalHostileTarget,
        Func<DRPGSkillSO, int> selectLegalSupportTarget)
    {
        if (actor == null)
            throw new ArgumentNullException(nameof(actor));
        if (canAfford == null)
            throw new ArgumentNullException(nameof(canAfford));
        if (hasLegalHostileTarget == null)
        {
            throw new ArgumentNullException(
                nameof(hasLegalHostileTarget));
        }
        if (selectLegalSupportTarget == null)
        {
            throw new ArgumentNullException(
                nameof(selectLegalSupportTarget));
        }

        DRPGEnemyActionSelection selection = InspectHostileCandidate(
            actor,
            actor.Build.BasicAttack,
            true,
            canAfford,
            hasLegalHostileTarget);
        if (selection.Path != DRPGEnemyActionSelectionPath.None)
            return selection;

        IReadOnlyList<DRPGSkillSO> skills = actor.Build.BattleSkills;
        for (int i = 0; i < skills.Count; i++)
        {
            DRPGSkillSO skill = skills[i];
            if (skill == null || ReferenceEquals(skill, actor.Build.BasicAttack))
                continue;

            selection = InspectHostileCandidate(
                actor,
                skill,
                false,
                canAfford,
                hasLegalHostileTarget);
            if (selection.Path != DRPGEnemyActionSelectionPath.None)
                return selection;
        }

        for (int i = 0; i < skills.Count; i++)
        {
            DRPGSkillSO skill = skills[i];
            if (skill == null ||
                ReferenceEquals(skill, actor.Build.BasicAttack))
            {
                continue;
            }

            selection = InspectSupportCandidate(
                actor,
                skill,
                canAfford,
                selectLegalSupportTarget);
            if (selection.Path != DRPGEnemyActionSelectionPath.None)
                return selection;
        }

        return default;
    }

    private static DRPGEnemyActionSelection InspectHostileCandidate(
        BattleParticipantRuntime actor,
        DRPGSkillSO skill,
        bool isBasicAttack,
        Func<DRPGSkillSO, bool> canAfford,
        Func<DRPGSkillSO, bool> hasLegalTarget)
    {
        if (skill == null || !IsHostileTargetRule(skill.TargetRule) ||
            !skill.CanUseFrom(actor.FormationRow) ||
            !canAfford(skill) ||
            !hasLegalTarget(skill))
        {
            return default;
        }

        if (!DRPGBattleSkillModePolicy.IsAllowed(skill))
            return default;
        return new DRPGEnemyActionSelection(
            DRPGEnemyActionSelectionPath.OrdinaryStandardAttack,
            skill,
            isBasicAttack);
    }

    private static DRPGEnemyActionSelection InspectSupportCandidate(
        BattleParticipantRuntime actor,
        DRPGSkillSO skill,
        Func<DRPGSkillSO, bool> canAfford,
        Func<DRPGSkillSO, int> selectLegalTarget)
    {
        if (skill == null || !IsSupportTargetRule(skill.TargetRule) ||
            !DRPGBattleSkillModePolicy.IsAllowed(skill) ||
            !skill.CanUseFrom(actor.FormationRow) ||
            !canAfford(skill))
        {
            return default;
        }

        int targetRuntimeId = selectLegalTarget(skill);
        return targetRuntimeId > 0
            ? new DRPGEnemyActionSelection(
                DRPGEnemyActionSelectionPath.OrdinaryStandardSupport,
                skill,
                false,
                targetRuntimeId)
            : default;
    }

    private static bool IsHostileTargetRule(DRPGSkillTargetRule targetRule)
    {
        return targetRule == DRPGSkillTargetRule.SingleEnemy ||
            targetRule == DRPGSkillTargetRule.AllEnemies ||
            targetRule == DRPGSkillTargetRule.BattlefieldPoint;
    }

    private static bool IsSupportTargetRule(DRPGSkillTargetRule targetRule)
    {
        return targetRule == DRPGSkillTargetRule.SingleAlly ||
            targetRule == DRPGSkillTargetRule.AllAllies ||
            targetRule == DRPGSkillTargetRule.Self;
    }
}
