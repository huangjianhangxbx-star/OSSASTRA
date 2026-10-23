#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;

public static class DRPGBattleEntryPresentationProbe
{
    public static string Run()
    {
        var host = new GameObject("BattleEntryProbe");
        BattleEntryPresentationRecipeSO recipe =
            ScriptableObject.CreateInstance<BattleEntryPresentationRecipeSO>();
        try
        {
            CanvasGroup hud = host.AddComponent<CanvasGroup>();
            BattleEntryPresentationCoordinator coordinator =
                host.AddComponent<BattleEntryPresentationCoordinator>();
            coordinator.Configure(hud, null, null);

            coordinator.PrepareHidden();
            AssertNear(0f, hud.alpha,
                "Entry preparation must hide the HUD before initial rendering");
            AssertFalse(hud.interactable,
                "Hidden entry HUD must not accept input");

            SetField(recipe, "spawnDuration", 0f);
            SetField(recipe, "holdDuration", 0f);
            SetField(recipe, "hudRevealDuration", 0f);
            IBattlePresentationHandle immediate = coordinator.Play(recipe);
            AssertTrue(immediate.IsSettled,
                "A zero-duration entry must settle immediately");
            AssertNear(1f, hud.alpha,
                "A completed entry must reveal the HUD");
            AssertTrue(hud.interactable,
                "A completed entry must restore HUD input");

            SetField(recipe, "spawnDuration", 1f);
            SetField(recipe, "holdDuration", 1f);
            coordinator.PrepareHidden();
            IBattlePresentationHandle cancellable = coordinator.Play(recipe);
            AssertFalse(cancellable.IsSettled,
                "A timed entry must own a live presentation handle");
            cancellable.Cancel(BattlePresentationCancelReason.BattleHidden);
            AssertTrue(cancellable.IsSettled,
                "Cancelling battle entry must settle exactly once");
            AssertNear(1f, hud.alpha,
                "Cancellation must not leave a stale invisible HUD state");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(recipe);
            UnityEngine.Object.DestroyImmediate(host);
        }

        return "PASS DRPG fixed battle entry presentation";
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
            throw new InvalidOperationException(message);
    }

    private static void AssertFalse(bool value, string message) =>
        AssertTrue(!value, message);

    private static void AssertNear(float expected, float actual, string message)
    {
        if (Mathf.Abs(expected - actual) > 0.001f)
            throw new InvalidOperationException(message);
    }
}
#endif
