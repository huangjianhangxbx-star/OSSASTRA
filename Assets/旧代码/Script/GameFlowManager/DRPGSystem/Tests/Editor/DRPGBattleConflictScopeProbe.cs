#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;

public static class DRPGBattleConflictScopeProbe
{
    public static string Run()
    {
        var host = new GameObject("ConflictScopeProbe");
        var stageA = ScriptableObject.CreateInstance<BattleStageProfileSO>();
        var stageB = ScriptableObject.CreateInstance<BattleStageProfileSO>();
        try
        {
            SetField(stageA, "profileId", "scope.a");
            SetField(stageB, "profileId", "scope.b");

            BattleStageRoot stageRoot = host.AddComponent<BattleStageRoot>();
            BattleStagePresenter stage = host.AddComponent<BattleStagePresenter>();
            stage.Configure(stageRoot);
            stage.Activate(stageA);

            var cameraObject = new GameObject("BattleCamera");
            cameraObject.transform.SetParent(host.transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.fieldOfView = 41f;
            BattleCameraPresenter cameraPresenter =
                cameraObject.AddComponent<BattleCameraPresenter>();
            cameraPresenter.Configure(camera, null);

            BattleHudGroupView group = CreateGroup(
                host.transform,
                BattleHudGroupKind.CommandPanel);
            BattleHudGroupView feedback = CreateGroup(
                host.transform,
                BattleHudGroupKind.PresentationFeedback);
            BattleHudPresenter hud = host.AddComponent<BattleHudPresenter>();
            hud.Configure(new[] { group, feedback });

            var scope = new BattleConflictPresentationScope(
                hud,
                cameraPresenter,
                stage);
            scope.Enter(
                BattleHudVisibilityPolicy.HideAll,
                stageB,
                new BattleCameraShotDefinition(
                    "scope.shot",
                    new Vector3(0f, 1f, -5f),
                    Vector3.zero,
                    55f,
                    1f));
            AssertFalse(group.IsVisible,
                "Conflict entry must apply the requested HUD policy");
            AssertTrue(feedback.IsVisible,
                "Presentation feedback remains visible inside a conflict");
            AssertEqual("scope.b", stage.ActiveProfile.ProfileId,
                "Conflict entry must activate its temporary stage");
            AssertTrue(BattleConflictPresentationScope.ActiveScopeCount > 0,
                "Debug audit must observe active conflict ownership");

            scope.Restore();
            AssertTrue(group.IsVisible,
                "Restore must recover the original HUD state");
            AssertTrue(camera.orthographic,
                "Restore must cancel camera motion and recover projection");
            AssertNear(41f, camera.fieldOfView,
                "Restore must recover the original camera FOV");
            AssertEqual("scope.a", stage.ActiveProfile.ProfileId,
                "Restore must recover the original stage");
            AssertEqual(0, BattleConflictPresentationScope.ActiveScopeCount,
                "Restored scopes must leave no ownership leak");
            scope.Restore();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(stageA);
            UnityEngine.Object.DestroyImmediate(stageB);
            UnityEngine.Object.DestroyImmediate(host);
        }
        return "PASS DRPG conflict presentation scope";
    }

    private static BattleHudGroupView CreateGroup(
        Transform parent,
        BattleHudGroupKind kind)
    {
        var child = new GameObject(kind.ToString());
        child.transform.SetParent(parent, false);
        CanvasGroup canvasGroup = child.AddComponent<CanvasGroup>();
        BattleHudGroupView view = child.AddComponent<BattleHudGroupView>();
        view.Configure(kind, canvasGroup);
        return view;
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
            throw new InvalidOperationException(message);
    }

    private static void AssertFalse(bool value, string message) =>
        AssertTrue(!value, message);

    private static void AssertNear(float expected, float actual, string message)
    {
        if (Math.Abs(expected - actual) > 0.001f)
            throw new InvalidOperationException(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
            throw new InvalidOperationException(message);
    }
}
#endif
