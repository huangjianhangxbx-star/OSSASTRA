#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;

public static class DRPGBattleStageRuntimeProbe
{
    public static string Run()
    {
        VerifyTemporaryStageRestore();
        VerifyCameraCancellationRestore();
        return "PASS DRPG perspective stage runtime";
    }

    private static void VerifyTemporaryStageRestore()
    {
        var host = new GameObject("StageRuntimeProbe");
        var profileA = ScriptableObject.CreateInstance<BattleStageProfileSO>();
        var profileB = ScriptableObject.CreateInstance<BattleStageProfileSO>();
        try
        {
            SetField(profileA, "profileId", "stage.a");
            SetField(profileB, "profileId", "stage.b");

            BattleStageRoot root = host.AddComponent<BattleStageRoot>();
            BattleStagePresenter presenter = host.AddComponent<BattleStagePresenter>();
            presenter.Configure(root);
            presenter.Activate(profileA);
            BattleStageStateSnapshot snapshot = presenter.Capture();

            presenter.SwitchTemporary(profileB);
            AssertEqual("stage.b", presenter.ActiveProfile.ProfileId,
                "Temporary stage must become the active presentation stage");

            presenter.Restore(snapshot);
            AssertEqual("stage.a", presenter.ActiveProfile.ProfileId,
                "Stage restore must return to the captured stage");
            presenter.Restore(snapshot);
            AssertEqual("stage.a", presenter.ActiveProfile.ProfileId,
                "Stage restore must be idempotent");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(profileA);
            UnityEngine.Object.DestroyImmediate(profileB);
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    private static void VerifyCameraCancellationRestore()
    {
        var host = new GameObject("CameraRuntimeProbe");
        try
        {
            Camera camera = host.AddComponent<Camera>();
            camera.orthographic = true;
            camera.fieldOfView = 37f;
            host.transform.position = new Vector3(2f, 3f, -10f);
            host.transform.eulerAngles = new Vector3(0f, 0f, 4f);

            BattleCameraPresenter presenter =
                host.AddComponent<BattleCameraPresenter>();
            presenter.Configure(camera, null);
            BattleCameraStateSnapshot snapshot = presenter.Capture();

            var shot = new BattleCameraShotDefinition(
                "impact",
                new Vector3(-1f, 1.5f, -4f),
                new Vector3(8f, -12f, 0f),
                62f,
                1f);
            presenter.Play(shot);
            AssertTrue(presenter.IsTransitioning,
                "A timed camera shot must own a cancellable transition");
            AssertFalse(camera.orthographic,
                "Battle camera shots must use perspective projection");

            presenter.Restore(snapshot);
            AssertFalse(presenter.IsTransitioning,
                "Restoring during a tween must cancel the transition");
            AssertTrue(camera.orthographic,
                "Restoring must recover the captured projection mode");
            AssertNear(37f, camera.fieldOfView,
                "Restoring must recover the captured FOV");
            AssertVector(new Vector3(2f, 3f, -10f), host.transform.position,
                "Restoring must recover the captured camera position");
            presenter.Restore(snapshot);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
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

    private static void AssertVector(
        Vector3 expected,
        Vector3 actual,
        string message)
    {
        if ((expected - actual).sqrMagnitude > 0.0001f)
            throw new InvalidOperationException(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
            throw new InvalidOperationException(message);
    }
}
#endif
