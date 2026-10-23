#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGResonanceBattleProbe
{
    public static string Run()
    {
        DRPGCombatantProfileSO protagonist = CreateProfile("protagonist", 50f);
        DRPGCombatantProfileSO friend = CreateProfile("friend", 10f);
        DRPGCombatantProfileSO secondFriend = CreateProfile("friend-2", 9f);
        DRPGCombatantProfileSO enemyProfile = CreateProfile("enemy", 1f);
        DRPGSkillSO skill = CreateSkill("resonance-strike", 15f);
        DRPGSkillSO secondSkill = CreateSkill("resonance-burst", 12f);
        DRPGResonanceBranchSO branch = CreateBranch(
            friend,
            skill,
            "friend-branch",
            "active");
        DRPGResonanceBranchSO secondBranch = CreateBranch(
            secondFriend,
            secondSkill,
            "friend-2-branch",
            "active-2");
        DRPGResonanceDefinitionSO definition = CreateDefinition(
            protagonist,
            branch,
            secondBranch);

        try
        {
            var party = new DRPGPartyCompositionState(
                protagonist,
                new[] { friend, secondFriend });
            var service = new DRPGResonanceService(
                definition,
                new FixedConditionProvider());
            AssertTrue(service.TrySelectPartner(friend, party).Success,
                "Partner selection should succeed");
            AssertTrue(service.TryUnlockNode("friend", "active").Success,
                "The resonance active should unlock");
            AssertTrue(service.TryEquipActiveSkill("friend", skill).Success,
                "The resonance active should equip");

            var expedition = new DRPGResonanceExpeditionState();
            var access = new DRPGBattleResonanceAccess(
                service,
                expedition,
                party);
            BattleParticipantRuntime protagonistRuntime = CreateRuntime(
                1,
                protagonist,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.Protagonist,
                0);
            BattleParticipantRuntime friendRuntime = CreateRuntime(
                2,
                friend,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.PartyMember,
                1);
            BattleParticipantRuntime enemyRuntime = CreateRuntime(
                4,
                enemyProfile,
                DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy,
                0);
            BattleParticipantRuntime secondFriendRuntime = CreateRuntime(
                3,
                secondFriend,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.PartyMember,
                2);
            BattleParticipantRuntime[] participants =
            {
                protagonistRuntime,
                friendRuntime,
                secondFriendRuntime,
                enemyRuntime
            };

            DRPGResonanceSkillAvailability locked =
                access.GetAvailability(protagonistRuntime, skill, participants);
            AssertFalse(locked.IsAvailable,
                "An uncharged resonance active must remain visible but unavailable");

            access.ObserveBattleEvents(
                new[] { CreatePartnerImpactBatch(friendRuntime.RuntimeId, 1L) },
                participants);
            AssertEqual(1, expedition.GetProgress("friend", skill.SkillId),
                "Only the current partner's matching events should charge the skill");
            access.ObserveBattleEvents(
                new[] { CreatePartnerImpactBatch(friendRuntime.RuntimeId, 2L) },
                participants);
            DRPGResonanceSkillAvailability ready =
                access.GetAvailability(protagonistRuntime, skill, participants);
            AssertTrue(ready.IsAvailable,
                "A fully charged resonance active should become available");

            var session = DRPGBattleHeadlessSessionFactory.Create(
                participants,
                resonanceAccess: access);
            session.Start();
            AssertSame(protagonistRuntime, session.CurrentActor,
                "The protagonist should receive the first test turn");
            AssertTrue(session.TryBeginResonanceTargetSelection(skill, out string error),
                error);
            DRPGBattleCommand command = DRPGBattleCommand.ResonanceSkill(
                protagonistRuntime.RuntimeId,
                enemyRuntime.RuntimeId,
                skill);
            AssertTrue(session.TrySubmitCommand(command, out error), error);
            AssertEqual(0, expedition.GetProgress("friend", skill.SkillId),
                "Using one resonance skill must clear only that skill's progress");
            AssertEqual(DRPGBattleCommandKind.ResonanceSkill, command.Kind,
                "The action must retain its dedicated command kind");

            expedition.AddProgress("friend", skill.SkillId, 2);
            AssertTrue(service.TrySelectPartner(friend, party).Success,
                "Selecting the same partner should be a no-op success");
            AssertEqual(2, expedition.GetProgress("friend", skill.SkillId),
                "Re-selecting the same partner must preserve expedition progress");

            BattleParticipantRuntime[] nextBattleParticipants =
            {
                CreateRuntime(
                    11,
                    protagonist,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.Protagonist,
                    0),
                CreateRuntime(
                    12,
                    friend,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.PartyMember,
                    1),
                CreateRuntime(
                    13,
                    secondFriend,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.PartyMember,
                    2),
                CreateRuntime(
                    14,
                    enemyProfile,
                    DRPGBattleSide.Enemy,
                    DRPGBattleRosterRole.Enemy,
                    0)
            };
            var nextBattle = DRPGBattleHeadlessSessionFactory.Create(
                nextBattleParticipants,
                resonanceAccess: access);
            nextBattle.Start();
            AssertEqual(2, expedition.GetProgress("friend", skill.SkillId),
                "Progress must survive between battles in one expedition");
            AssertTrue(service.TryUnlockNode("friend-2", "active-2").Success,
                "The second friend active should unlock");
            AssertTrue(service.TryEquipActiveSkill(
                "friend-2",
                secondSkill).Success,
                "The second friend active should equip");
            AssertTrue(nextBattle.TrySelectResonancePartner(
                secondFriend,
                out error),
                error);
            AssertEqual(0, expedition.GetProgress("friend", skill.SkillId),
                "Switching partner must clear the previous partner's progress");
            AssertEqual("friend-2", service.State.ResonancePartnerCharacterId,
                "The new partner must become authoritative");

            IReadOnlyList<DRPGBattleParticipantViewModel> hud =
                new DRPGBattleViewModel(nextBattle).PartyMembers;
            AssertSame(nextBattleParticipants[0], hud[0].Participant,
                "The protagonist must remain first in the battle HUD");
            AssertSame(nextBattleParticipants[2], hud[1].Participant,
                "The selected resonance partner must appear second in the HUD");

            expedition.AddProgress("friend-2", secondSkill.SkillId, 2);
            AssertTrue(nextBattle.TryCommitTestHarnessMutation(
                    workspace => workspace.For(new ParticipantId(
                            nextBattleParticipants[2].RuntimeId))
                        .SetHitPoints(0f),
                    out _,
                    out error),
                error);
            DRPGResonanceSkillAvailability incapacitated =
                access.GetAvailability(
                    nextBattleParticipants[0],
                    secondSkill,
                    nextBattleParticipants);
            AssertFalse(incapacitated.IsAvailable,
                "An incapacitated partner must disable the active skill");
            AssertEqual("friend-2", service.State.ResonancePartnerCharacterId,
                "Partner incapacitation must not auto-switch the partner");
            expedition.ClearAll();
            AssertEqual(0,
                expedition.GetProgress("friend-2", secondSkill.SkillId),
                "Leaving an expedition must clear all resonance progress");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(definition);
            UnityEngine.Object.DestroyImmediate(secondBranch);
            UnityEngine.Object.DestroyImmediate(branch);
            UnityEngine.Object.DestroyImmediate(secondSkill);
            UnityEngine.Object.DestroyImmediate(skill);
            DestroyProfile(enemyProfile);
            DestroyProfile(secondFriend);
            DestroyProfile(friend);
            DestroyProfile(protagonist);
        }

        return "PASS DRPG resonance battle";
    }

    private static DRPGBattleEventBatch CreatePartnerImpactBatch(
        int partnerRuntimeId,
        long actionId)
    {
        return new DRPGBattleEventBatch(
            actionId,
            0,
            new[]
            {
                new DRPGBattleEvent(
                    actionId,
                    actionId,
                    0,
                    DRPGBattleEventKind.ActionImpactResolved,
                    partnerRuntimeId,
                    3,
                    "partner-action")
            });
    }

    private static BattleParticipantRuntime CreateRuntime(
        int id,
        DRPGCombatantProfileSO profile,
        DRPGBattleSide side,
        DRPGBattleRosterRole role,
        int order)
    {
        DRPGResolvedCombatantBuild build =
            DRPGResolvedCombatantBuild.CreateLegacy(profile);
        return new BattleParticipantRuntime(
            id,
            new BattleParticipantSpec(profile, side, role, order, build));
    }

    private static DRPGCombatantProfileSO CreateProfile(string id, float agility)
    {
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        DRPGCharacterProgressionDefinitionSO progression =
            ScriptableObject.CreateInstance<DRPGCharacterProgressionDefinitionSO>();
        SetField(profile, "combatantId", id);
        SetField(profile, "agility", agility);
        SetField(profile, "progressionDefinition", progression);
        return profile;
    }

    private static DRPGSkillSO CreateSkill(string id, float power)
    {
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        SetField(skill, "skillId", id);
        SetField(skill, "power", power);
        return skill;
    }

    private static DRPGResonanceBranchSO CreateBranch(
        DRPGCombatantProfileSO friend,
        DRPGSkillSO skill,
        string branchId,
        string nodeId)
    {
        DRPGResonanceBranchSO branch =
            ScriptableObject.CreateInstance<DRPGResonanceBranchSO>();
        var node = new DRPGResonanceNodeDefinition(
            nodeId,
            0,
            DRPGResonanceNodeRewardKind.ActiveSkill,
            skill,
            null);
        SetField(node, "requiredBattleProgress", 2);
        SetField(node, "progressSources", new List<DRPGResonanceProgressSource>
        {
            new DRPGResonanceProgressSource(
                DRPGBattleEventKind.ActionImpactResolved,
                DRPGResonanceProgressActorRule.Partner,
                1)
        });
        SetField(branch, "branchId", branchId);
        SetField(branch, "friendProfile", friend);
        SetField(branch, "nodes", new List<DRPGResonanceNodeDefinition> { node });
        return branch;
    }

    private static DRPGResonanceDefinitionSO CreateDefinition(
        DRPGCombatantProfileSO protagonist,
        params DRPGResonanceBranchSO[] branches)
    {
        DRPGResonanceDefinitionSO definition =
            ScriptableObject.CreateInstance<DRPGResonanceDefinitionSO>();
        SetField(definition, "protagonistProfile", protagonist);
        SetField(definition, "branches",
            new List<DRPGResonanceBranchSO>(branches));
        return definition;
    }

    private static void DestroyProfile(DRPGCombatantProfileSO profile)
    {
        if (profile == null)
            return;
        UnityEngine.Object.DestroyImmediate(profile.ProgressionDefinition);
        UnityEngine.Object.DestroyImmediate(profile);
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

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    private static void AssertFalse(bool value, string message)
    {
        AssertTrue(!value, message);
    }

    private static void AssertSame(object expected, object actual, string message)
    {
        if (!ReferenceEquals(expected, actual))
            throw new Exception(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
            throw new Exception($"{message}. Expected={expected}, Actual={actual}");
    }

    private sealed class FixedConditionProvider : IDRPGResonanceConditionProvider
    {
        public int GetConditionValue(string protagonistCharacterId, string friendCharacterId)
        {
            return 999;
        }
    }
}
#endif
