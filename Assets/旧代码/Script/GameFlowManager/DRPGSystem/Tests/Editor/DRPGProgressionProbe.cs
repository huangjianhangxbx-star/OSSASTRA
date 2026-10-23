#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGProgressionProbe
{
    public static string Run()
    {
        DRPGSkillSO rootSkill = CreateSkill("katana_skill_01", 12f);
        DRPGSkillSO childSkill = CreateSkill("katana_skill_02", 24f);
        DRPGSkillSO branchSkill = CreateSkill("revolver_skill_01", 18f);
        DRPGPassiveModifierSO passive =
            ScriptableObject.CreateInstance<DRPGPassiveModifierSO>();
        DRPGSkillTreeSO katanaTree = ScriptableObject.CreateInstance<DRPGSkillTreeSO>();
        DRPGSkillTreeSO revolverTree = ScriptableObject.CreateInstance<DRPGSkillTreeSO>();
        DRPGProfessionSO katana = ScriptableObject.CreateInstance<DRPGProfessionSO>();
        DRPGProfessionSO revolver = ScriptableObject.CreateInstance<DRPGProfessionSO>();
        DRPGCharacterProgressionDefinitionSO definition =
            ScriptableObject.CreateInstance<DRPGCharacterProgressionDefinitionSO>();
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        DRPGCombatantProfileSO enemy =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        DRPGPartySetupSO party = ScriptableObject.CreateInstance<DRPGPartySetupSO>();
        DRPGEncounterDefinitionSO encounter =
            ScriptableObject.CreateInstance<DRPGEncounterDefinitionSO>();

        try
        {
            ConfigureModifier(passive);
            ConfigureTree(
                katanaTree,
                "katana_tree",
                new List<DRPGSkillTreeNodeDefinition>
                {
                    CreateSkillNode("katana_skill_node_01", 1, rootSkill),
                    CreatePassiveNode(
                        "katana_buff_node_01",
                        1,
                        passive,
                        "katana_skill_node_01"),
                    CreateSkillNode(
                        "katana_skill_node_02",
                        2,
                        childSkill,
                        "katana_buff_node_01")
                });
            ConfigureTree(
                revolverTree,
                "revolver_tree",
                new List<DRPGSkillTreeNodeDefinition>
                {
                    CreateSkillNode("revolver_skill_node_01", 1, branchSkill)
                });
            ConfigureProfession(katana, "katana", rootSkill, katanaTree);
            ConfigureProfession(revolver, "revolver", branchSkill, revolverTree);
            ConfigureDefinition(definition, katana, revolver, 5);
            SetField(profile, "combatantId", "progression_probe_actor");
            SetField(profile, "progressionDefinition", definition);
            SetField(enemy, "combatantId", "progression_probe_enemy");
            SetField(party, "protagonist", profile);
            SetField(
                party,
                "partyMembers",
                new List<DRPGCombatantProfileSO>());
            SetField(
                encounter,
                "enemies",
                new List<DRPGCombatantProfileSO> { enemy });

            var service = new DRPGProgressionService();
            DRPGCharacterProgressionView initial = service.GetView(profile);
            AssertSame(katana, initial.ActiveProfession,
                "The configured default profession must be active");
            AssertEqual(5, initial.AvailablePoints,
                "Starting points must be available before unlocks");

            DRPGProgressionOperationResult childFirst =
                service.TryActivateNode(profile, "katana_skill_node_02");
            AssertFalse(childFirst.Success,
                "A child cannot unlock before every prerequisite");
            AssertEqual(5, service.GetView(profile).AvailablePoints,
                "A failed activation must not spend points");

            AssertTrue(
                service.TryActivateNode(profile, "katana_skill_node_01").Success,
                "A root node with enough points must unlock");
            AssertFalse(
                service.TryActivateNode(profile, "katana_skill_node_01").Success,
                "Activating the same node twice must fail");
            AssertEqual(4, service.GetView(profile).AvailablePoints,
                "Duplicate activation must not spend points twice");

            AssertTrue(
                service.TryActivateNode(profile, "katana_buff_node_01").Success,
                "The first passive must unlock after its skill prerequisite");
            AssertTrue(
                service.TryActivateNode(profile, "katana_skill_node_02").Success,
                "The second skill must unlock after the passive prerequisite");
            AssertEqual(1, service.GetView(profile).AvailablePoints,
                "Literal node costs must consume four shared points");

            AssertTrue(service.TryEquipSkill(profile, rootSkill, 0).Success,
                "An unlocked skill must equip into an empty slot");
            AssertTrue(service.TryEquipSkill(profile, childSkill, 2).Success,
                "Loadout slots may contain gaps");
            AssertFalse(service.TryEquipSkill(profile, rootSkill, 1).Success,
                "The same skill cannot be equipped twice");
            AssertSame(rootSkill, service.GetView(profile).EquippedSkills[0],
                "The failed duplicate equip must preserve the original slot");
            AssertSame(childSkill, service.GetView(profile).EquippedSkills[2],
                "The sparse third slot must remain equipped");

            DRPGResolvedCombatantBuild snapshot = service.ResolveBuild(profile);
            AssertSame(rootSkill, snapshot.BasicAttack,
                "The profession basic attack is separate from four skill slots");
            AssertSame(childSkill, snapshot.EquippedSkills[2],
                "The Build must snapshot the sparse four-slot loadout");
            AssertEqual(1.25f, snapshot.DamageMultiplier,
                "Unlocked passive damage multipliers must affect the Build");

            DRPGBattleSessionCreationResult firstRoster =
                DRPGBattleSessionFactory.CreateRoster(party, encounter, service);
            AssertTrue(firstRoster.IsSuccess,
                "A progression-aware roster must resolve a Build for every participant");
            BattleParticipantRuntime firstBattleActor = firstRoster.Participants[0];
            AssertSame(childSkill, firstBattleActor.Build.EquippedSkills[2],
                "The participant must capture the current progression Build");

            AssertTrue(service.TrySetActiveProfession(profile, revolver).Success,
                "An available profession must be selectable");
            AssertEqual(1, service.GetView(profile).AvailablePoints,
                "A character-owned point pool must remain shared across professions");
            AssertTrue(
                service.TryActivateNode(profile, "revolver_skill_node_01").Success,
                "The second profession must own its own unlock state");
            AssertTrue(service.TryEquipSkill(profile, branchSkill, 0).Success,
                "The second profession owns a separate loadout");

            AssertTrue(service.TrySetActiveProfession(profile, katana).Success,
                "The first profession must remain selectable");
            DRPGCharacterProgressionView restoredKatana = service.GetView(profile);
            AssertEqual(0, restoredKatana.AvailablePoints,
                "Spending in another profession must reduce the same character pool");
            AssertTrue(restoredKatana.IsNodeUnlocked("katana_skill_node_02"),
                "Switching professions must not erase the first tree");
            AssertSame(rootSkill, restoredKatana.EquippedSkills[0],
                "Switching professions must preserve profession-specific loadouts");

            AssertTrue(service.ResetProfession(profile).Success,
                "Reset must succeed for the active profession");
            DRPGCharacterProgressionView reset = service.GetView(profile);
            AssertEqual(4, reset.AvailablePoints,
                "Reset must refund only the active profession's spent points");
            AssertEqual(0, reset.UnlockedNodeIds.Count,
                "Reset must remove every unlocked node");
            AssertNull(reset.EquippedSkills[0],
                "Reset must remove skills no longer present in the pool");

            AssertSame(childSkill, snapshot.EquippedSkills[2],
                "An existing battle Build must remain immutable after reset");
            DRPGResolvedCombatantBuild nextBattle = service.ResolveBuild(profile);
            AssertNull(nextBattle.EquippedSkills[2],
                "A newly resolved Build must see the reset loadout");

            AssertSame(childSkill, firstBattleActor.Build.EquippedSkills[2],
                "A participant Build must remain immutable after later menu changes");
            DRPGBattleSessionCreationResult secondRoster =
                DRPGBattleSessionFactory.CreateRoster(party, encounter, service);
            AssertTrue(secondRoster.IsSuccess,
                "A later battle must still create a valid roster after reset");
            AssertNull(secondRoster.Participants[0].Build.EquippedSkills[2],
                "A later battle must resolve the latest progression state");
        }
        finally
        {
            DestroyAll(
                encounter,
                party,
                enemy,
                profile,
                definition,
                revolver,
                katana,
                revolverTree,
                katanaTree,
                passive,
                branchSkill,
                childSkill,
                rootSkill);
        }

        return "PASS DRPG progression";
    }

    private static DRPGSkillSO CreateSkill(string id, float power)
    {
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        SetField(skill, "skillId", id);
        SetField(skill, "power", power);
        return skill;
    }

    private static void ConfigureModifier(DRPGPassiveModifierSO modifier)
    {
        SetField(modifier, "modifierId", "katana_damage_01");
        SetField(modifier, "damageMultiplier", 1.25f);
    }

    private static void ConfigureTree(
        DRPGSkillTreeSO tree,
        string id,
        List<DRPGSkillTreeNodeDefinition> nodes)
    {
        SetField(tree, "treeId", id);
        SetField(tree, "nodes", nodes);
    }

    private static DRPGSkillTreeNodeDefinition CreateSkillNode(
        string id,
        int cost,
        DRPGSkillSO skill,
        params string[] prerequisites)
    {
        var node = new DRPGSkillTreeNodeDefinition();
        SetField(node, "nodeId", id);
        SetField(node, "pointCost", cost);
        SetField(node, "rewardKind", DRPGSkillTreeRewardKind.Skill);
        SetField(node, "skill", skill);
        SetField(node, "prerequisiteNodeIds", new List<string>(prerequisites));
        return node;
    }

    private static DRPGSkillTreeNodeDefinition CreatePassiveNode(
        string id,
        int cost,
        DRPGPassiveModifierSO modifier,
        params string[] prerequisites)
    {
        var node = new DRPGSkillTreeNodeDefinition();
        SetField(node, "nodeId", id);
        SetField(node, "pointCost", cost);
        SetField(node, "rewardKind", DRPGSkillTreeRewardKind.Passive);
        SetField(node, "passiveModifier", modifier);
        SetField(node, "prerequisiteNodeIds", new List<string>(prerequisites));
        return node;
    }

    private static void ConfigureProfession(
        DRPGProfessionSO profession,
        string id,
        DRPGSkillSO basicAttack,
        DRPGSkillTreeSO tree)
    {
        SetField(profession, "professionId", id);
        SetField(profession, "basicAttack", basicAttack);
        SetField(profession, "skillTree", tree);
    }

    private static void ConfigureDefinition(
        DRPGCharacterProgressionDefinitionSO definition,
        DRPGProfessionSO defaultProfession,
        DRPGProfessionSO secondProfession,
        int points)
    {
        SetField(definition, "progressionId", "progression_probe");
        SetField(definition, "defaultProfession", defaultProfession);
        SetField(
            definition,
            "availableProfessions",
            new List<DRPGProfessionSO> { defaultProfession, secondProfession });
        SetField(
            definition,
            "startingUnlockedProfessions",
            new List<DRPGProfessionSO> { defaultProfession, secondProfession });
        SetField(definition, "startingSkillPoints", points);
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

    private static void AssertNull(object value, string message)
    {
        if (value != null)
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
