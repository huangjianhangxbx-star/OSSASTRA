#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGBattlePresentationProbe
{
    public static string Run()
    {
        Texture2D texture = new Texture2D(8, 8);
        Sprite encounterBackground = Sprite.Create(
            texture, new Rect(0, 0, 8, 8), Vector2.one * 0.5f);
        Sprite regionBackground = Sprite.Create(
            texture, new Rect(0, 0, 8, 8), Vector2.one * 0.5f);
        Sprite portrait = Sprite.Create(
            texture, new Rect(0, 0, 8, 8), Vector2.one * 0.5f);
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        DRPGBattlePresentationSO presentation =
            ScriptableObject.CreateInstance<DRPGBattlePresentationSO>();
        BattlePresentationRecipeSetSO recipeSet =
            ScriptableObject.CreateInstance<BattlePresentationRecipeSetSO>();
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        DRPGEncounterDefinitionSO encounter =
            ScriptableObject.CreateInstance<DRPGEncounterDefinitionSO>();

        try
        {
            SetField(profile, "fullPortrait", portrait);
            SetField(profile, "skills", new List<DRPGSkillSO> { skill });
            SetField(encounter, "battleBackground", encounterBackground);
            SetField(presentation, "recipeSet", recipeSet);

            AssertSame(recipeSet, presentation.RecipeSet,
                "Presentation profile must expose its recipe set");
            AssertEqual("basic_attack", skill.PresentationKey,
                "A skill without an override must fall back to its skill id");

            var environment = new DRPGBattleEnvironmentContext(
                "test-region",
                regionBackground);
            AssertSame(
                encounterBackground,
                DRPGBattleBackgroundResolver.Resolve(encounter, environment),
                "Encounter background must win");

            SetField(encounter, "battleBackground", null);
            AssertSame(
                regionBackground,
                DRPGBattleBackgroundResolver.Resolve(encounter, environment),
                "Region default must be used when encounter background is absent");
            AssertSame(
                null,
                DRPGBattleBackgroundResolver.Resolve(encounter, default),
                "A null background means frozen exploration remains visible");

            var participants = new List<BattleParticipantRuntime>
            {
                Create(1, profile, DRPGBattleSide.Player, DRPGBattleRosterRole.Protagonist, 0),
                Create(2, profile, DRPGBattleSide.Player, DRPGBattleRosterRole.PartyMember, 0),
                Create(3, profile, DRPGBattleSide.Player, DRPGBattleRosterRole.PartyMember, 1),
                Create(4, profile, DRPGBattleSide.Enemy, DRPGBattleRosterRole.Enemy, 0),
                Create(5, profile, DRPGBattleSide.Enemy, DRPGBattleRosterRole.Enemy, 1)
            };
            var session = DRPGBattleHeadlessSessionFactory.Create(participants);
            session.Start();
            var viewModel = new DRPGBattleViewModel(session);

            AssertSame(session.CurrentActor, viewModel.CurrentActor.Participant,
                "Right-side actor must always represent the current participant");
            AssertEqual(3, viewModel.PartyMembers.Count,
                "The battlefield protagonist and two ordinary members belong in the HUD");
            AssertEqual(5, viewModel.Timeline.Count,
                "Current actor plus all upcoming actors must appear in the timeline");
            AssertSame(portrait, viewModel.CurrentActor.FullPortrait,
                "Battle UI must keep the original full portrait Sprite reference");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(encounter);
            UnityEngine.Object.DestroyImmediate(recipeSet);
            UnityEngine.Object.DestroyImmediate(presentation);
            UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(skill);
            UnityEngine.Object.DestroyImmediate(encounterBackground);
            UnityEngine.Object.DestroyImmediate(regionBackground);
            UnityEngine.Object.DestroyImmediate(portrait);
            UnityEngine.Object.DestroyImmediate(texture);
        }

        return "PASS DRPG battle presentation model";
    }

    private static BattleParticipantRuntime Create(
        int id,
        DRPGCombatantProfileSO profile,
        DRPGBattleSide side,
        DRPGBattleRosterRole role,
        int slot)
    {
        return new BattleParticipantRuntime(
            id,
            new BattleParticipantSpec(profile, side, role, slot));
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
}
#endif
