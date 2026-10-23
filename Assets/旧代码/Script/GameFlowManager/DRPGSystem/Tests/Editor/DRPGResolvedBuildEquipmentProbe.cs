#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGResolvedBuildEquipmentProbe
{
    public static string Run()
    {
        var configured = new DRPGSkillSO[4];
        for (int i = 0; i < configured.Length; i++)
            configured[i] = CreateSkill("configured_" + (i + 1), "skill_key_" + i);

        DRPGSkillSO duplicateConfigured = CreateSkill("configured_2", "duplicate");
        DRPGSkillSO intrinsic = CreateSkill("weapon_intrinsic", "intrinsic_key");
        DRPGSkillSO professionBasic = CreateSkill("profession_basic", "basic_key");
        DRPGSkillSO weaponBasic = CreateSkill("weapon_basic", "weapon_basic_key");
        DRPGProfessionSO profession = ScriptableObject.CreateInstance<DRPGProfessionSO>();
        DRPGSkillTreeSO tree = ScriptableObject.CreateInstance<DRPGSkillTreeSO>();
        DRPGCharacterProgressionDefinitionSO definition =
            ScriptableObject.CreateInstance<DRPGCharacterProgressionDefinitionSO>();
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();

        try
        {
            ConfigureProgression(
                profile,
                definition,
                profession,
                tree,
                professionBasic,
                configured);

            var equipment = new DRPGResolvedEquipmentSnapshot(
                "weapon_instance_01",
                "weapon_definition_01",
                profession.ProfessionId,
                weaponBasic,
                new[] { intrinsic },
                new[] { duplicateConfigured },
                Array.Empty<DRPGPassiveModifierSO>(),
                37,
                50,
                false,
                "weapon_presentation");

            var service = new DRPGProgressionService();
            DRPGResolvedCombatantBuild build = service.ResolveBuild(profile, equipment);

            AssertEqual(4, build.ConfiguredSkills.Count,
                "The configured loadout must remain exactly four slots");
            AssertEqual(4, build.EquippedSkills.Count,
                "The compatibility alias must remain the four-slot loadout");
            AssertEqual(1, build.GrantedSkills.Count,
                "A granted skill that duplicates a configured SkillId must collapse");
            AssertEqual(5, build.BattleSkills.Count,
                "Four configured skills plus one intrinsic skill must be battle-usable");
            for (int i = 0; i < configured.Length; i++)
            {
                AssertSame(configured[i], build.BattleSkills[i],
                    "Configured skill order must remain slot order at index " + i);
            }
            AssertSame(intrinsic, build.BattleSkills[4],
                "Weapon intrinsic skill must follow configured slots");
            AssertTrue(build.ContainsBattleSkill("weapon_intrinsic"),
                "Battle-skill lookup must include weapon intrinsic skills");
            AssertFalse(build.ContainsBattleSkill("missing"),
                "Battle-skill lookup must reject unknown stable IDs");
            AssertSame(weaponBasic, build.BasicAttack,
                "A configured weapon basic attack must replace the profession basic attack");
            AssertEqual(
                "weapon_presentation",
                DRPGAttackPresentationResolver.ResolveAttackPresentationKey(
                    build,
                    configured[0]),
                "Weapon presentation must override the selected skill presentation");

            var noEquipment = new DRPGResolvedEquipmentSnapshot(
                string.Empty,
                string.Empty,
                profession.ProfessionId,
                null,
                Array.Empty<DRPGSkillSO>(),
                Array.Empty<DRPGSkillSO>(),
                Array.Empty<DRPGPassiveModifierSO>(),
                0,
                0,
                false,
                string.Empty);
            DRPGResolvedCombatantBuild fallback =
                service.ResolveBuild(profile, noEquipment);
            AssertSame(professionBasic, fallback.BasicAttack,
                "Missing weapon override must preserve the profession basic attack");
            AssertEqual(
                configured[0].PresentationKey,
                DRPGAttackPresentationResolver.ResolveAttackPresentationKey(
                    fallback,
                    configured[0]),
                "Missing weapon presentation must fall back to the selected skill");
        }
        finally
        {
            DestroyAll(
                profile,
                definition,
                tree,
                profession,
                weaponBasic,
                professionBasic,
                intrinsic,
                duplicateConfigured,
                configured[3],
                configured[2],
                configured[1],
                configured[0]);
        }

        return "PASS DRPG resolved equipment build";
    }

    private static void ConfigureProgression(
        DRPGCombatantProfileSO profile,
        DRPGCharacterProgressionDefinitionSO definition,
        DRPGProfessionSO profession,
        DRPGSkillTreeSO tree,
        DRPGSkillSO basicAttack,
        IReadOnlyList<DRPGSkillSO> configured)
    {
        var nodes = new List<DRPGSkillTreeNodeDefinition>();
        var grantedNodeIds = new List<string>();
        for (int i = 0; i < configured.Count; i++)
        {
            string nodeId = "configured_node_" + i;
            var node = new DRPGSkillTreeNodeDefinition();
            SetField(node, "nodeId", nodeId);
            SetField(node, "pointCost", 1);
            SetField(node, "rewardKind", DRPGSkillTreeRewardKind.Skill);
            SetField(node, "skill", configured[i]);
            nodes.Add(node);
            grantedNodeIds.Add(nodeId);
        }

        SetField(tree, "treeId", "resolved_build_tree");
        SetField(tree, "nodes", nodes);
        SetField(profession, "professionId", "resolved_build_profession");
        SetField(profession, "basicAttack", basicAttack);
        SetField(profession, "skillTree", tree);
        SetField(profession, "grantedNodeIds", grantedNodeIds);
        SetField(profession, "initialLoadout", new List<DRPGSkillSO>(configured));
        SetField(definition, "progressionId", "resolved_build_progression");
        SetField(definition, "defaultProfession", profession);
        SetField(
            definition,
            "availableProfessions",
            new List<DRPGProfessionSO> { profession });
        SetField(
            definition,
            "startingUnlockedProfessions",
            new List<DRPGProfessionSO> { profession });
        SetField(profile, "combatantId", "resolved_build_actor");
        SetField(profile, "progressionDefinition", definition);
    }

    private static DRPGSkillSO CreateSkill(string id, string presentationKey)
    {
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        SetField(skill, "skillId", id);
        SetField(skill, "presentationKey", presentationKey);
        return skill;
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
