#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGBattleWeaponSwitchProbe
{
    public static string Run()
    {
        DRPGSkillSO oldSkill = CreateSkill("old_skill");
        DRPGSkillSO newSkill = CreateSkill("new_skill");
        DRPGCombatantProfileSO playerProfile = CreateProfile(
            "switch_player",
            0f,
            100f);
        DRPGCombatantProfileSO enemyProfile = CreateProfile(
            "switch_enemy",
            50f,
            100f);
        DRPGPassiveModifierSO lowerCaps =
            ScriptableObject.CreateInstance<DRPGPassiveModifierSO>();

        try
        {
            SetField(lowerCaps, "modifierId", "lower_switch_caps");
            SetField(lowerCaps, "maxHpMultiplier", 0.25f);
            SetField(lowerCaps, "maxPostureMultiplier", 0.25f);

            DRPGResolvedCombatantBuild oldBuild = CreateBuild(
                playerProfile,
                oldSkill,
                Array.Empty<DRPGPassiveModifierSO>());
            DRPGResolvedCombatantBuild replacementBuild = CreateBuild(
                playerProfile,
                newSkill,
                new[] { lowerCaps });
            var player = new BattleParticipantRuntime(
                1,
                new BattleParticipantSpec(
                    playerProfile,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.Protagonist,
                    0,
                    build: oldBuild));
            var enemy = new BattleParticipantRuntime(
                2,
                new BattleParticipantSpec(
                    enemyProfile,
                    DRPGBattleSide.Enemy,
                    DRPGBattleRosterRole.Enemy,
                    0));

            player.ApplyDamage(player.EffectiveMaxHp * 0.5f);
            player.ReducePosture(player.EffectiveMaxPosture * 0.5f);
            float hpBefore = player.CurrentHp;
            float postureBefore = player.CurrentPosture;
            var successAdapter = new FakeBattleEquipmentService(
                "weapon_new",
                replacementBuild,
                DRPGEquipmentOperationResult.Ok());
            var session = DRPGBattleHeadlessSessionFactory.Create(
                new[] { player, enemy },
                equipmentService: successAdapter);
            int buildEvents = 0;
            session.ParticipantBuildChanged += changed =>
            {
                if (ReferenceEquals(changed, player))
                    buildEvents++;
            };
            session.Start();
            AssertSame(player, session.CurrentActor,
                "The switch probe requires the player to act first");

            DRPGBattleCommand command = DRPGBattleCommand.SwitchWeapon(
                player.RuntimeId,
                "weapon_new");
            AssertTrue(session.TryPrepareCommand(command, out var action, out var error),
                error);
            AssertTrue(session.TryResolveImpact(action, out _, out error), error);
            AssertSame(replacementBuild, player.Build,
                "Switch resolution must replace the current immutable Build");
            AssertEqual(
                Mathf.Min(hpBefore, replacementBuild.EffectiveMaxHp),
                player.CurrentHp,
                "Build replacement must preserve HP and only clamp to a lower maximum");
            AssertEqual(
                Mathf.Min(postureBefore, replacementBuild.EffectiveMaxPosture),
                player.CurrentPosture,
                "Build replacement must preserve posture and only clamp to a lower maximum");
            AssertEqual(1, buildEvents,
                "A successful switch must publish one build-change event");
            AssertTrue(session.TryCompleteAction(action, out error), error);
            AssertFalse(ReferenceEquals(player, session.CurrentActor),
                "Weapon switching must consume the actor's action opportunity");
            AssertEqual(1, successAdapter.SwitchCalls,
                "The equipment adapter must commit exactly once");

            var failedPlayer = new BattleParticipantRuntime(
                3,
                new BattleParticipantSpec(
                    playerProfile,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.Protagonist,
                    0,
                    build: oldBuild));
            var failedEnemy = new BattleParticipantRuntime(
                4,
                new BattleParticipantSpec(
                    enemyProfile,
                    DRPGBattleSide.Enemy,
                    DRPGBattleRosterRole.Enemy,
                    0));
            var failureAdapter = new FakeBattleEquipmentService(
                "weapon_rejected",
                replacementBuild,
                DRPGEquipmentOperationResult.Fail(
                    DRPGEquipmentError.CommitFailed,
                    "switch rejected"));
            var failureSession = DRPGBattleHeadlessSessionFactory.Create(
                new[] { failedPlayer, failedEnemy },
                equipmentService: failureAdapter);
            failureSession.Start();
            AssertTrue(
                failureSession.TryPrepareCommand(
                    DRPGBattleCommand.SwitchWeapon(
                        failedPlayer.RuntimeId,
                        "weapon_rejected"),
                    out var failedAction,
                    out error),
                error);
            AssertFalse(
                failureSession.TryResolveImpact(
                    failedAction,
                    out _,
                    out error),
                "A rejected equipment transaction must fail resolution");
            AssertSame(oldBuild, failedPlayer.Build,
                "A rejected switch must preserve the old Build");
            AssertSame(failedPlayer, failureSession.CurrentActor,
                "A rejected switch must return control to the same actor");
            AssertEqual(DRPGBattlePhase.AwaitingCommand, failureSession.Phase,
                "A rejected switch must not strand the session in action preparation");
            AssertNull(failureSession.ActiveAction,
                "A rejected switch must clear the failed prepared action");
        }
        finally
        {
            DestroyAll(lowerCaps, enemyProfile, playerProfile, newSkill, oldSkill);
        }

        return "PASS DRPG battle weapon switch";
    }

    private sealed class FakeBattleEquipmentService : IDRPGBattleEquipmentService
    {
        private readonly IReadOnlyList<DRPGBattleWeaponCandidate> candidates;
        private readonly DRPGResolvedCombatantBuild replacementBuild;
        private readonly DRPGEquipmentOperationResult result;

        public FakeBattleEquipmentService(
            string weaponInstanceId,
            DRPGResolvedCombatantBuild replacementBuild,
            DRPGEquipmentOperationResult result)
        {
            candidates = new[]
            {
                new DRPGBattleWeaponCandidate(weaponInstanceId, true, string.Empty)
            };
            this.replacementBuild = replacementBuild;
            this.result = result;
        }

        public int SwitchCalls { get; private set; }

        public IReadOnlyList<DRPGBattleWeaponCandidate> GetCandidates(
            int actorRuntimeId)
        {
            return candidates;
        }

        public DRPGEquipmentOperationResult TrySwitchWeapon(
            BattleParticipantRuntime actor,
            string weaponInstanceId,
            out DRPGResolvedCombatantBuild resolvedBuild)
        {
            SwitchCalls++;
            resolvedBuild = result.Success ? replacementBuild : null;
            return result;
        }
    }

    private static DRPGResolvedCombatantBuild CreateBuild(
        DRPGCombatantProfileSO profile,
        DRPGSkillSO skill,
        IReadOnlyList<DRPGPassiveModifierSO> passives)
    {
        return new DRPGResolvedCombatantBuild(
            profile,
            null,
            skill,
            new[] { skill },
            new[] { skill },
            passives);
    }

    private static DRPGSkillSO CreateSkill(string id)
    {
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        SetField(skill, "skillId", id);
        return skill;
    }

    private static DRPGCombatantProfileSO CreateProfile(
        string id,
        float timelineDelay,
        float recoveryDelay)
    {
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        SetField(profile, "combatantId", id);
        SetField(profile, "maxHp", 100f);
        SetField(profile, "maxPosture", 100f);
        SetField(profile, "baseTimelineDelay", timelineDelay);
        SetField(profile, "baseRecoveryDelay", recoveryDelay);
        return profile;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, fieldName);
        field.SetValue(target, value);
    }

    private static void DestroyAll(params UnityEngine.Object[] objects)
    {
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null)
                UnityEngine.Object.DestroyImmediate(objects[i]);
        }
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    private static void AssertFalse(bool value, string message)
    {
        AssertTrue(!value, message);
    }

    private static void AssertNull(object value, string message)
    {
        if (value != null)
            throw new Exception(message);
    }

    private static void AssertSame(object expected, object actual, string message)
    {
        if (!ReferenceEquals(expected, actual))
            throw new Exception(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
        {
            throw new Exception(
                message + ". Expected=" + expected + ", Actual=" + actual);
        }
    }
}
#endif
