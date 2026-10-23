#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGPostureBreakTargetingProbe
{
    public static string Run()
    {
        DRPGSkillSO single = CreateSkill(
            "probe_break_target_single",
            DRPGSkillTargetRule.SingleEnemy);
        SetField(
            single,
            "enemyTargetDisclosureMode",
            DRPGEnemyTargetDisclosureMode.RevealAtCommit);
        DRPGSkillSO area = CreateSkill(
            "probe_break_target_area",
            DRPGSkillTargetRule.AllEnemies);
        DRPGSkillSO optOut = CreateSkill(
            "probe_break_target_opt_out",
            DRPGSkillTargetRule.SingleEnemy);
        SetField(optOut, "ignorePostureBreakProtection", true);
        DRPGCombatantProfileSO profile = CreateProfile(single);
        try
        {
            BattleParticipantRuntime exposed = CreateParticipant(701, profile);
            BattleParticipantRuntime stable = CreateParticipant(702, profile);
            exposed.ReducePosture(exposed.CurrentPosture);

            var candidates = new List<BattleParticipantRuntime>
            {
                exposed,
                stable
            };
            DRPGPostureBreakTargetPolicy.ApplyNewSelectionPolicy(
                single,
                candidates);
            AssertEqual(2, candidates.Count,
                "Simplified posture must not remove broken actors from ordinary targeting");
            AssertSame(exposed, candidates[0],
                "Posture state must preserve the original candidate order");
            AssertSame(stable, candidates[1],
                "Stable targets must remain selectable alongside broken targets");

            candidates.Clear();
            candidates.Add(exposed);
            DRPGPostureBreakTargetPolicy.ApplyNewSelectionPolicy(
                single,
                candidates);
            AssertEqual(1, candidates.Count,
                "The only legal exposed target must remain selectable");

            candidates.Clear();
            candidates.Add(exposed);
            candidates.Add(stable);
            DRPGPostureBreakTargetPolicy.ApplyNewSelectionPolicy(
                area,
                candidates);
            AssertEqual(2, candidates.Count,
                "Area skills must not alter their target set");

            DRPGPostureBreakTargetPolicy.ApplyNewSelectionPolicy(
                optOut,
                candidates);
            AssertEqual(2, candidates.Count,
                "Explicit opt-out skills must retain exposed candidates");

            DRPGEnemyActionPlan committed =
                DRPGEnemyActionPlan.CreateAttack(
                        77L,
                        799,
                        single,
                        0f,
                        10f,
                        1)
                    .CommitTarget(exposed.RuntimeId, 0L);
            candidates.Clear();
            candidates.Add(stable);
            DRPGPostureBreakTargetPolicy.ApplyNewSelectionPolicy(
                single,
                candidates);
            AssertEqual(
                exposed.RuntimeId,
                committed.CreatePublicSnapshot().DisclosedTargetRuntimeId,
                "A target committed at warning zero must never be retargeted by later protection changes");

            var exposedModel = new DRPGBattleParticipantViewModel(
                exposed,
                0f,
                false);
            AssertEqual(
                DRPGPostureBreakState.Stable,
                exposedModel.PostureBreakState,
                "Simplified posture must not expose the legacy skip state");
            AssertEqual(false, exposedModel.IsPostureBreakExposed,
                "Simplified posture must not expose a vulnerability flag");
            AssertNear(
                1f,
                exposedModel.PostureBreakIncomingDamageMultiplier,
                "Simplified posture must not publish a damage multiplier");

            exposed.ResolvePostureBreakOwnTurnStart();
            exposed.ResolvePostureBreakOwnTurnStart();
            var stabilityModel = new DRPGBattleParticipantViewModel(
                exposed,
                0f,
                false);
            AssertEqual(
                DRPGPostureBreakState.Stable,
                stabilityModel.PostureBreakState,
                "Legacy recovery calls must leave simplified posture stable");
            AssertEqual(
                0,
                stabilityModel.PostureBreakStabilityOwnActionsRemaining,
                "Simplified posture must not expose legacy stability actions");

            return "PASS DRPG posture-break targeting policy";
        }
        finally
        {
            Destroy(profile, optOut, area, single);
        }
    }

    private static DRPGSkillSO CreateSkill(
        string id,
        DRPGSkillTargetRule targetRule)
    {
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        SetField(skill, "skillId", id);
        SetField(skill, "targetRule", targetRule);
        return skill;
    }

    private static DRPGCombatantProfileSO CreateProfile(DRPGSkillSO skill)
    {
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        SetField(profile, "combatantId", "probe_break_target_profile");
        SetField(profile, "skills", new List<DRPGSkillSO> { skill });
        return profile;
    }

    private static BattleParticipantRuntime CreateParticipant(
        int runtimeId,
        DRPGCombatantProfileSO profile)
    {
        return new BattleParticipantRuntime(
            runtimeId,
            new BattleParticipantSpec(
                profile,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.PartyMember,
                runtimeId));
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, name);
        field.SetValue(target, value);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                message + $" Expected={expected}, Actual={actual}");
        }
    }

    private static void AssertSame(object expected, object actual, string message)
    {
        if (!ReferenceEquals(expected, actual))
            throw new InvalidOperationException(message);
    }

    private static void AssertNear(float expected, float actual, string message)
    {
        if (Math.Abs(expected - actual) > 0.0001f)
        {
            throw new InvalidOperationException(
                message + $" Expected={expected}, Actual={actual}");
        }
    }

    private static void Destroy(params UnityEngine.Object[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] != null)
                UnityEngine.Object.DestroyImmediate(values[i]);
        }
    }
}
#endif
