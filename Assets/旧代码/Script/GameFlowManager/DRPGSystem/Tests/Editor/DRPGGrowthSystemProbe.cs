#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGGrowthSystemProbe
{
    public static string Run()
    {
        AssertEqual(160, DRPGGrowthRules.ExperienceToNextLevel(1),
            "Level-one XP follows the approved curve");
        AssertNear(0.10f, DRPGGrowthRules.EvaluateAttribute(10),
            "The attribute curve must pass through its authored knots");
        AssertNear(0.88f, DRPGRelativeSpeedPolicy.ResolveDelayMultiplier(20f, 10f),
            "A large speed lead is capped to a delay band");

        DRPGSkillTreeSO tree = ScriptableObject.CreateInstance<DRPGSkillTreeSO>();
        DRPGProfessionSO profession = ScriptableObject.CreateInstance<DRPGProfessionSO>();
        DRPGCharacterProgressionDefinitionSO definition =
            ScriptableObject.CreateInstance<DRPGCharacterProgressionDefinitionSO>();
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        try
        {
            SetField(tree, "treeId", "growth_probe_tree");
            SetField(tree, "nodes", new List<DRPGSkillTreeNodeDefinition>());
            SetField(profession, "professionId", "growth_probe_profession");
            SetField(profession, "skillTree", tree);
            SetField(definition, "progressionId", "growth_probe_definition");
            SetField(definition, "defaultProfession", profession);
            SetField(
                definition,
                "availableProfessions",
                new List<DRPGProfessionSO> { profession });
            SetField(definition, "startingSkillPoints", 3);
            SetField(definition, "startingAttributePoints", 0);
            SetField(definition, "allowsLevelBreakthrough", true);
            SetField(
                definition,
                "initialAttributes",
                new DRPGCoreAttributes(10, 10, 10, 10, 10, 10));
            SetField(profile, "combatantId", "growth_probe_actor");
            SetField(profile, "maxHp", 100f);
            SetField(profile, "maxMp", 20f);
            SetField(profile, "maxPosture", 80f);
            SetField(profile, "expeditionEnergy", 100f);
            SetField(profile, "agility", 10f);
            SetField(profile, "progressionDefinition", definition);

            var service = new DRPGProgressionService();
            DRPGCharacterProgressionView initial = service.GetView(profile);
            AssertEqual(1, initial.Level, "A fresh character starts at configured level");
            AssertEqual(3, initial.AvailableSkillPoints,
                "Skill points belong to the character pool");

            DRPGLevelGainResult gain = service.GainExperience(
                profile,
                DRPGGrowthRules.ExperienceToNextLevel(1));
            AssertEqual(1, gain.LevelsGained, "Enough XP must level exactly once");
            DRPGCharacterProgressionView levelTwo = service.GetView(profile);
            AssertEqual(2, levelTwo.Level, "Level must advance");
            AssertEqual(1, levelTwo.AvailableAttributePoints,
                "Levels through 80 award one attribute point");
            AssertEqual(4, levelTwo.AvailableSkillPoints,
                "Levels through 80 award one personal skill point");

            DRPGAttributeAllocationDraft draft = service.BeginAttributeDraft(profile);
            AssertTrue(draft.TryAdd(DRPGCoreAttributeId.Vitality),
                "A draft can spend an available point");
            AssertTrue(service.TryCommitAttributeDraft(profile, draft).Success,
                "A current draft commits atomically");
            AssertFalse(service.TryCommitAttributeDraft(profile, draft).Success,
                "A committed draft cannot be replayed");
            DRPGCharacterProgressionView allocated = service.GetView(profile);
            AssertEqual(11, allocated.Attributes.Vitality,
                "Committed allocation updates the six-dimensional state");
            AssertTrue(allocated.DerivedStats.MaxHp > profile.MaxHp,
                "The same resolver used by battle must preview derived growth");

            var capState = new DRPGCharacterProgressionState(
                "growth_probe_cap",
                definition);
            capState.RestoreGrowth(
                DRPGGrowthRules.NormalLevelCap,
                0L,
                false,
                definition.InitialAttributes,
                3,
                10);
            int breakthroughCost = DRPGGrowthRules.ExperienceToNextLevel(
                DRPGGrowthRules.NormalLevelCap);
            DRPGLevelGainResult banked = capState.GainExperience(breakthroughCost);
            AssertEqual(0, banked.LevelsGained,
                "A locked level-80 character banks XP without breaking through");
            AssertEqual((long)breakthroughCost, capState.CurrentExperience,
                "Cap XP remains character-owned for later systems");
            AssertTrue(capState.UnlockBreakthrough(),
                "An eligible character can explicitly unlock breakthrough");
            AssertEqual(81, capState.Level,
                "Unlocking breakthrough consumes already banked XP");
            AssertEqual(3, capState.AvailableAttributePoints,
                "Levels 81-100 must not award ordinary attribute points");
            AssertEqual(10, capState.TotalSkillPoints,
                "Levels 81-100 must not award ordinary skill points");

            DRPGProgressionSaveData save = service.CaptureSaveData(
                new[] { profile });
            string json = JsonUtility.ToJson(save);
            DRPGProgressionSaveData roundTrip =
                JsonUtility.FromJson<DRPGProgressionSaveData>(json);
            var restoredService = new DRPGProgressionService();
            AssertTrue(
                restoredService.RestoreSaveData(new[] { profile }, roundTrip).Success,
                "Stable-ID DTO data must restore into a fresh service");
            DRPGCharacterProgressionView restored = restoredService.GetView(profile);
            AssertEqual(2, restored.Level, "Save round-trip preserves level");
            AssertEqual(11, restored.Attributes.Vitality,
                "Save round-trip preserves allocated attributes");
            AssertFalse(restored.BreakthroughUnlocked,
                "Save round-trip preserves a locked breakthrough state");

            roundTrip.characters[0].characterId = "unknown_growth_actor";
            AssertFalse(
                new DRPGProgressionService()
                    .RestoreSaveData(new[] { profile }, roundTrip).Success,
                "Unknown stable IDs must fail migration instead of being dropped");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(definition);
            UnityEngine.Object.DestroyImmediate(profession);
            UnityEngine.Object.DestroyImmediate(tree);
        }

        return "PASS DRPG growth system";
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

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    private static void AssertFalse(bool value, string message)
    {
        AssertTrue(!value, message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
            throw new Exception($"{message}. Expected={expected}, Actual={actual}");
    }

    private static void AssertNear(float expected, float actual, string message)
    {
        if (Mathf.Abs(expected - actual) > 0.001f)
            throw new Exception($"{message}. Expected={expected}, Actual={actual}");
    }
}
#endif
