#if UNITY_EDITOR
using System;
using UnityEngine;

public static class DRPGBattleHudPresenterProbe
{
    public static string Run()
    {
        var host = new GameObject("HudPresenterProbe");
        try
        {
            BattleHudGroupView command = CreateGroup(
                host.transform,
                BattleHudGroupKind.CommandPanel,
                0.7f,
                true,
                false);
            BattleHudGroupView party = CreateGroup(
                host.transform,
                BattleHudGroupKind.PartyStatus,
                1f,
                true,
                true);
            BattleHudGroupView feedback = CreateGroup(
                host.transform,
                BattleHudGroupKind.PresentationFeedback,
                0.8f,
                false,
                false);
            BattleHudPresenter presenter = host.AddComponent<BattleHudPresenter>();
            presenter.Configure(new[] { command, party, feedback });

            BattleHudStateSnapshot snapshot = presenter.Capture();
            presenter.Apply(BattleHudVisibilityPolicy.HideCommand);
            AssertFalse(command.IsVisible,
                "HideCommand must hide the command panel");
            AssertTrue(party.IsVisible,
                "HideCommand must preserve party status");
            AssertTrue(feedback.IsVisible,
                "Presentation feedback must remain visible during conflict UI");

            presenter.Apply(BattleHudVisibilityPolicy.HideAll);
            AssertFalse(party.IsVisible,
                "HideAll must hide ordinary battle HUD groups");
            AssertTrue(feedback.IsVisible,
                "HideAll must never suppress presentation feedback");

            presenter.Restore(snapshot);
            AssertNear(0.7f, command.CanvasGroup.alpha,
                "Restore must recover the actual pre-run alpha");
            AssertTrue(command.CanvasGroup.interactable,
                "Restore must recover interactable state");
            AssertFalse(command.CanvasGroup.blocksRaycasts,
                "Restore must recover raycast state");
            AssertNear(0.8f, feedback.CanvasGroup.alpha,
                "Feedback restore must recover its prior alpha");
            AssertFalse(feedback.CanvasGroup.interactable,
                "Feedback restore must recover its prior input state");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }

        return "PASS DRPG grouped HUD presenter";
    }

    private static BattleHudGroupView CreateGroup(
        Transform parent,
        BattleHudGroupKind kind,
        float alpha,
        bool interactable,
        bool blocksRaycasts)
    {
        var child = new GameObject(kind.ToString());
        child.transform.SetParent(parent, false);
        CanvasGroup group = child.AddComponent<CanvasGroup>();
        group.alpha = alpha;
        group.interactable = interactable;
        group.blocksRaycasts = blocksRaycasts;
        BattleHudGroupView view = child.AddComponent<BattleHudGroupView>();
        view.Configure(kind, group);
        return view;
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
        if (Math.Abs(expected - actual) > 0.001f)
            throw new InvalidOperationException(message);
    }
}
#endif
