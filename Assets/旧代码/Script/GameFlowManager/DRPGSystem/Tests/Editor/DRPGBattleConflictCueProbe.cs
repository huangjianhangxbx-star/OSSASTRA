#if UNITY_EDITOR
using System;
using UnityEngine;

public static class DRPGBattleConflictCueProbe
{
    public static string Run()
    {
        var root = new GameObject("ConflictCueProbe");
        try
        {
            BattleHudGroupView command = CreateHudGroup(root.transform);
            BattleHudPresenter hud = root.AddComponent<BattleHudPresenter>();
            hud.Configure(new[] { command });

            var cameraObject = new GameObject("BattleCamera");
            cameraObject.transform.SetParent(root.transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            BattleCameraPresenter cameraPresenter =
                cameraObject.AddComponent<BattleCameraPresenter>();
            cameraPresenter.Configure(camera, root.transform);

            BattleStagePresenter stage = root.AddComponent<BattleStagePresenter>();
            BattleWorldActorPool pool = root.AddComponent<BattleWorldActorPool>();
            pool.Configure(null, root.transform);
            BattleWorldActorView attacker = pool.Acquire(1);
            BattleWorldActorView target = pool.Acquire(2);
            Vector3 attackerScale = attacker.transform.localScale;
            Vector3 targetPosition = target.transform.localPosition;

            BattleConflictPresentationContext context =
                root.AddComponent<BattleConflictPresentationContext>();
            context.Configure(hud, cameraPresenter, stage, pool, null);

            BattleHudPolicyCueExecutor hudExecutor =
                root.AddComponent<BattleHudPolicyCueExecutor>();
            BattleActorStagingCueExecutor actorExecutor =
                root.AddComponent<BattleActorStagingCueExecutor>();
            BattleConflictRestoreCueExecutor restoreExecutor =
                root.AddComponent<BattleConflictRestoreCueExecutor>();
            hudExecutor.Configure(context);
            actorExecutor.Configure(context);
            restoreExecutor.Configure(context);

            BattleHudPolicyCueSO hudCue =
                ScriptableObject.CreateInstance<BattleHudPolicyCueSO>();
            BattleActorStagingCueSO actorCue =
                ScriptableObject.CreateInstance<BattleActorStagingCueSO>();
            BattleConflictRestoreCueSO restoreCue =
                ScriptableObject.CreateInstance<BattleConflictRestoreCueSO>();
            try
            {
                BattlePresentationScope scope =
                    new BattlePresentationScope(3L, 4L, 5L);
                hudExecutor.Play(hudCue, CreateRequest(10L, scope));
                actorExecutor.Play(actorCue, CreateRequest(11L, scope));

                AssertFalse(command.IsVisible,
                    "HUD policy cues must affect the grouped command HUD");
                AssertEqual(1, context.ActiveScopeCount,
                    "Different presentation requests in one action must share one conflict scope");
                AssertFalse(attacker.transform.localScale == attackerScale,
                    "Actor staging must alter the acting world actor");

                restoreExecutor.Play(restoreCue, CreateRequest(12L, scope));
                AssertTrue(command.IsVisible,
                    "Restore cues must recover the exact HUD state");
                AssertEqual(0, context.ActiveScopeCount,
                    "Restore cues must close the shared action scope");
                AssertEqual(attackerScale, attacker.transform.localScale,
                    "Restore cues must recover actor scale");
                AssertEqual(targetPosition, target.transform.localPosition,
                    "Restore cues must recover target staging position");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hudCue);
                UnityEngine.Object.DestroyImmediate(actorCue);
                UnityEngine.Object.DestroyImmediate(restoreCue);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }

        return "PASS DRPG conflict presentation cues";
    }

    private static BattleHudGroupView CreateHudGroup(Transform parent)
    {
        var child = new GameObject("CommandHud");
        child.transform.SetParent(parent, false);
        CanvasGroup group = child.AddComponent<CanvasGroup>();
        BattleHudGroupView view = child.AddComponent<BattleHudGroupView>();
        view.Configure(BattleHudGroupKind.CommandPanel, group);
        return view;
    }

    private static BattlePresentationRequest CreateRequest(
        long requestId,
        BattlePresentationScope scope)
    {
        var actor = new BattleParticipantPresentationSnapshot(
            1, "Actor", DRPGBattleSide.Player,
            DRPGBattleRosterRole.PartyMember, 100f, 100f,
            DRPGLowHpPresentationState.Normal, null, null, null);
        var target = new BattleParticipantPresentationSnapshot(
            2, "Target", DRPGBattleSide.Enemy,
            DRPGBattleRosterRole.Enemy, 100f, 100f,
            DRPGLowHpPresentationState.Normal, null, null, null);
        return new BattlePresentationRequest(
            requestId,
            BattlePresentationMoment.ActionWindup,
            scope,
            actor,
            new[] { target },
            new[] { 1, 2 },
            "probe",
            EAnimationLogicState.Skill_1,
            Array.Empty<BattleHitPresentationSnapshot>(),
            BattlePresentationFacts.Empty);
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException(message);
    }

    private static void AssertFalse(bool value, string message) =>
        AssertTrue(!value, message);

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
        {
            throw new InvalidOperationException(
                message + $". Expected={expected}, Actual={actual}");
        }
    }
}
#endif
