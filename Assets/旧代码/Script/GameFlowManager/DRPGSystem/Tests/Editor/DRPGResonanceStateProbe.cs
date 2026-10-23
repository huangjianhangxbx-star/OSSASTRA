#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGResonanceStateProbe
{
    public static string Run()
    {
        DRPGCombatantProfileSO protagonist = CreateProfile(
            "protagonist",
            DRPGPersonnelKind.Friend);
        DRPGCombatantProfileSO friend = CreateProfile(
            "friend",
            DRPGPersonnelKind.Friend);
        DRPGCombatantProfileSO employee = CreateProfile(
            "employee",
            DRPGPersonnelKind.Employee);
        DRPGSkillSO activeSkill = CreateSkill("resonance-active");
        DRPGPassiveModifierSO passive = CreatePassive("resonance-passive");
        DRPGResonanceBranchSO branch = CreateBranch(friend, activeSkill, passive);
        DRPGResonanceDefinitionSO definition = CreateDefinition(
            protagonist,
            branch);

        try
        {
            var party = new DRPGPartyCompositionState(
                protagonist,
                new[] { employee, friend });
            var conditions = new FixedConditionProvider(10);
            var service = new DRPGResonanceService(definition, conditions);

            DRPGResonanceOperationResult employeeResult =
                service.TrySelectPartner(employee, party);
            AssertFalse(employeeResult.Success,
                "Employees must never become resonance partners");
            AssertSame(employee, party.CreateSnapshot().OrderedProfiles[1],
                "A rejected partner transaction must not mutate party order");

            DRPGResonanceOperationResult partnerResult =
                service.TrySelectPartner(friend, party);
            AssertTrue(partnerResult.Success, partnerResult.Message);
            AssertEqual("friend", service.State.ResonancePartnerCharacterId,
                "The selected friend ID must be authoritative");
            AssertSame(friend, party.CreateSnapshot().OrderedProfiles[1],
                "The selected partner must be canonicalized to party slot one");

            AssertTrue(service.TryUnlockNode("friend", "passive-node").Success,
                "A relationship-qualified passive node should unlock");
            AssertTrue(service.TryUnlockNode("friend", "active-node").Success,
                "A relationship-qualified active node should unlock");
            AssertTrue(service.TryEquipActiveSkill(
                "friend",
                activeSkill).Success,
                "An unlocked active skill should equip for its own branch");
            AssertEqual(
                activeSkill.SkillId,
                service.State.GetEquippedActiveSkillId("friend"),
                "Each friend branch should retain exactly one equipped active");

            DRPGResolvedIdentityContribution contribution =
                service.ResolveIdentityContribution(protagonist);
            AssertEqual(1, contribution.PassiveModifiers.Count,
                "Unlocked resonance passives must contribute to the protagonist");
            AssertEqual(0, contribution.BattleSkills.Count,
                "Resonance active skills must remain outside the career battle skill list");

            DRPGResolvedCombatantBuild build = new DRPGResolvedCombatantBuild(
                protagonist,
                null,
                null,
                Array.Empty<DRPGSkillSO>(),
                Array.Empty<DRPGSkillSO>(),
                Array.Empty<DRPGPassiveModifierSO>(),
                null,
                DRPGResolvedEquipmentSnapshot.Empty(string.Empty),
                Array.Empty<DRPGSkillSO>(),
                contribution);
            AssertFalse(build.ContainsBattleSkill(activeSkill.SkillId),
                "Identity active skills must not leak into BattleSkills");
            AssertEqual(1, build.IdentityContribution.PassiveModifiers.Count,
                "The resolved build should expose its separate identity contribution");

            DRPGResonanceSaveData save = service.CaptureSaveData();
            var restoredParty = new DRPGPartyCompositionState(
                protagonist,
                new[] { employee, friend });
            var restored = new DRPGResonanceService(definition, conditions);
            DRPGResonanceOperationResult restoreResult = restored.RestoreSaveData(
                save,
                restoredParty,
                new[] { protagonist, friend, employee });
            AssertTrue(restoreResult.Success, restoreResult.Message);
            AssertSame(friend, restoredParty.CreateSnapshot().OrderedProfiles[1],
                "Save restore must revalidate and restore the partner slot invariant");
            AssertEqual(activeSkill.SkillId,
                restored.State.GetEquippedActiveSkillId("friend"),
                "The equipped branch active must survive a save round trip");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(definition);
            UnityEngine.Object.DestroyImmediate(branch);
            UnityEngine.Object.DestroyImmediate(passive);
            UnityEngine.Object.DestroyImmediate(activeSkill);
            UnityEngine.Object.DestroyImmediate(employee.ProgressionDefinition);
            UnityEngine.Object.DestroyImmediate(friend.ProgressionDefinition);
            UnityEngine.Object.DestroyImmediate(protagonist.ProgressionDefinition);
            UnityEngine.Object.DestroyImmediate(employee);
            UnityEngine.Object.DestroyImmediate(friend);
            UnityEngine.Object.DestroyImmediate(protagonist);
        }

        return "PASS DRPG resonance state";
    }

    private static DRPGCombatantProfileSO CreateProfile(
        string id,
        DRPGPersonnelKind kind)
    {
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        DRPGCharacterProgressionDefinitionSO progression =
            ScriptableObject.CreateInstance<DRPGCharacterProgressionDefinitionSO>();
        SetField(profile, "combatantId", id);
        SetField(progression, "personnelKind", kind);
        SetField(profile, "progressionDefinition", progression);
        return profile;
    }

    private static DRPGSkillSO CreateSkill(string id)
    {
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        SetField(skill, "skillId", id);
        return skill;
    }

    private static DRPGPassiveModifierSO CreatePassive(string id)
    {
        DRPGPassiveModifierSO passive =
            ScriptableObject.CreateInstance<DRPGPassiveModifierSO>();
        SetField(passive, "modifierId", id);
        return passive;
    }

    private static DRPGResonanceBranchSO CreateBranch(
        DRPGCombatantProfileSO friend,
        DRPGSkillSO active,
        DRPGPassiveModifierSO passive)
    {
        DRPGResonanceBranchSO branch =
            ScriptableObject.CreateInstance<DRPGResonanceBranchSO>();
        SetField(branch, "branchId", "friend-branch");
        SetField(branch, "friendProfile", friend);
        SetField(branch, "nodes", new List<DRPGResonanceNodeDefinition>
        {
            new DRPGResonanceNodeDefinition(
                "passive-node",
                5,
                DRPGResonanceNodeRewardKind.Passive,
                null,
                passive),
            new DRPGResonanceNodeDefinition(
                "active-node",
                10,
                DRPGResonanceNodeRewardKind.ActiveSkill,
                active,
                null)
        });
        return branch;
    }

    private static DRPGResonanceDefinitionSO CreateDefinition(
        DRPGCombatantProfileSO protagonist,
        DRPGResonanceBranchSO branch)
    {
        DRPGResonanceDefinitionSO definition =
            ScriptableObject.CreateInstance<DRPGResonanceDefinitionSO>();
        SetField(definition, "protagonistProfile", protagonist);
        SetField(definition, "branches", new List<DRPGResonanceBranchSO> { branch });
        return definition;
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
        private readonly int value;

        public FixedConditionProvider(int value)
        {
            this.value = value;
        }

        public int GetConditionValue(
            string protagonistCharacterId,
            string friendCharacterId)
        {
            return value;
        }
    }
}
#endif
