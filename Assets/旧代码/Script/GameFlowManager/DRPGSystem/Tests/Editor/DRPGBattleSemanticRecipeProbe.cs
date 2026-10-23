#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGBattleSemanticRecipeProbe
{
    public static string Run()
    {
        var created = new List<ScriptableObject>();
        try
        {
            BattleSkillPresentationRecipeSO parent = CreateRecipe(
                created,
                "parent",
                null,
                BattlePresentationOverrideMode.Override,
                BattleHudVisibilityPolicy.HideCommand,
                BattlePresentationOverrideMode.Inherit,
                0f,
                BattlePresentationOverrideMode.Override,
                "parent-impact");
            BattleSkillPresentationRecipeSO child = CreateRecipe(
                created,
                "child",
                parent,
                BattlePresentationOverrideMode.Inherit,
                BattleHudVisibilityPolicy.KeepAll,
                BattlePresentationOverrideMode.Override,
                43f,
                BattlePresentationOverrideMode.Disabled,
                "child-impact");

            var resolver = new BattleSemanticRecipeResolver(
                new[] { parent, child },
                child);
            AssertTrue(
                resolver.TryResolve("child", out ResolvedBattleSkillPresentation resolved, out string error),
                "Child semantic recipe must resolve: " + error);
            AssertTrue(resolved.Hud.Enabled, "Inherited HUD module must stay enabled");
            AssertEqual(
                BattleHudVisibilityPolicy.HideCommand,
                resolved.Hud.Definition.Visibility,
                "Child HUD must inherit the parent policy");
            AssertTrue(resolved.Camera.Enabled, "Overridden camera module must be enabled");
            AssertEqual(43f, resolved.Camera.Definition.FieldOfView,
                "Child camera override must win");
            AssertTrue(!resolved.Feedback.Enabled,
                "Disabled feedback module must be explicitly absent");
            AssertEqual(
                BattlePresentationOverrideMode.Inherit,
                resolved.Hud.RequestedMode,
                "Resolved output must retain that HUD was inherited");
            AssertEqual(
                BattlePresentationOverrideMode.Override,
                resolved.Camera.RequestedMode,
                "Resolved output must retain that camera was overridden");
            AssertEqual(
                BattlePresentationOverrideMode.Disabled,
                resolved.Feedback.RequestedMode,
                "Resolved output must retain that feedback was disabled");

            VerifyMissingKeyFallsBack(resolver);
            VerifyCycleRejected(created);
            VerifyDuplicateKeyRejected(created);
        }
        finally
        {
            for (int i = created.Count - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(created[i]);
        }

        return "PASS DRPG semantic presentation recipes";
    }

    private static void VerifyMissingKeyFallsBack(
        BattleSemanticRecipeResolver resolver)
    {
        AssertTrue(
            resolver.TryResolve("missing", out ResolvedBattleSkillPresentation fallback, out string error),
            "A missing key must resolve through the configured default: " + error);
        AssertEqual("child", fallback.PresentationKey,
            "Fallback must use the configured default semantic recipe");
    }

    private static void VerifyCycleRejected(ICollection<ScriptableObject> created)
    {
        BattleSkillPresentationRecipeSO first = CreateRecipe(
            created, "cycle-a", null,
            BattlePresentationOverrideMode.Override,
            BattleHudVisibilityPolicy.KeepAll,
            BattlePresentationOverrideMode.Inherit,
            0f,
            BattlePresentationOverrideMode.Inherit,
            string.Empty);
        BattleSkillPresentationRecipeSO second = CreateRecipe(
            created, "cycle-b", first,
            BattlePresentationOverrideMode.Inherit,
            BattleHudVisibilityPolicy.KeepAll,
            BattlePresentationOverrideMode.Inherit,
            0f,
            BattlePresentationOverrideMode.Inherit,
            string.Empty);
        SetField(first, "parent", second);

        var resolver = new BattleSemanticRecipeResolver(
            new[] { first, second },
            first);
        AssertTrue(
            !resolver.TryResolve("cycle-a", out _, out string error),
            "An inheritance cycle must be rejected");
        AssertContains("cycle-a -> cycle-b -> cycle-a", error,
            "Cycle diagnostics must be deterministic");
    }

    private static void VerifyDuplicateKeyRejected(
        ICollection<ScriptableObject> created)
    {
        BattleSkillPresentationRecipeSO first = CreateRecipe(
            created, "duplicate", null,
            BattlePresentationOverrideMode.Inherit,
            BattleHudVisibilityPolicy.KeepAll,
            BattlePresentationOverrideMode.Inherit,
            0f,
            BattlePresentationOverrideMode.Inherit,
            string.Empty);
        BattleSkillPresentationRecipeSO second = CreateRecipe(
            created, "duplicate", null,
            BattlePresentationOverrideMode.Inherit,
            BattleHudVisibilityPolicy.KeepAll,
            BattlePresentationOverrideMode.Inherit,
            0f,
            BattlePresentationOverrideMode.Inherit,
            string.Empty);

        var resolver = new BattleSemanticRecipeResolver(
            new[] { first, second },
            first);
        AssertTrue(
            !resolver.TryResolve("duplicate", out _, out string error),
            "Duplicate presentation keys must be rejected");
        AssertContains("Duplicate PresentationKey 'duplicate'", error,
            "Duplicate diagnostics must identify the key");
    }

    private static BattleSkillPresentationRecipeSO CreateRecipe(
        ICollection<ScriptableObject> created,
        string key,
        BattleSkillPresentationRecipeSO parent,
        BattlePresentationOverrideMode hudMode,
        BattleHudVisibilityPolicy hudVisibility,
        BattlePresentationOverrideMode cameraMode,
        float fieldOfView,
        BattlePresentationOverrideMode feedbackMode,
        string impactMarker)
    {
        BattleSkillPresentationRecipeSO recipe =
            ScriptableObject.CreateInstance<BattleSkillPresentationRecipeSO>();
        created.Add(recipe);
        SetField(recipe, "presentationKey", key);
        SetField(recipe, "parent", parent);

        SetNestedModule(recipe, "hud", hudMode, definition =>
            SetField(definition, "visibility", hudVisibility));
        SetNestedModule(recipe, "camera", cameraMode, definition =>
            SetField(definition, "fieldOfView", fieldOfView));
        SetNestedModule(recipe, "feedback", feedbackMode, definition =>
            SetField(definition, "impactMarker", impactMarker));
        return recipe;
    }

    private static void SetNestedModule(
        object owner,
        string fieldName,
        BattlePresentationOverrideMode mode,
        Action<object> configureDefinition)
    {
        object module = GetField(owner, fieldName);
        SetField(module, "mode", mode);
        object definition = GetField(module, "definition");
        configureDefinition(definition);
    }

    private static object GetField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, fieldName);
        return field.GetValue(target);
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

    private static void AssertContains(string expected, string actual, string message)
    {
        if ((actual ?? string.Empty).IndexOf(expected, StringComparison.Ordinal) < 0)
            throw new Exception(message + ". Actual=" + (actual ?? "<null>"));
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"{message}. Expected={expected}, Actual={actual}");
    }
}
#endif
