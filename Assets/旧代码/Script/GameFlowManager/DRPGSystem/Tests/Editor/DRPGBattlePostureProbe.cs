#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;

public static class DRPGBattlePostureProbe
{
    public static string Run()
    {
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        try
        {
            SetField(profile, "maxPosture", 100f);
            SetField(profile, "postureRecoveryPerTurn", 15f);
            SetField(profile, "postureBreakRecoveryRatio", 0.5f);
            SetField(profile, "postureBreakStabilityOwnActions", 3);
            SetField(
                profile,
                "postureBreakStabilityDamageMultiplier",
                0.5f);
            SetField(profile, "postureBreakExposedDamageMultiplier", 1.25f);
            SetField(profile, "postureBreakAilmentBuildUpMultiplier", 1.25f);

            var participant = new BattleParticipantRuntime(
                1,
                new BattleParticipantSpec(
                    profile,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.PartyMember,
                    0));

            AssertNear(100f, participant.CurrentPosture,
                "A new participant must begin at maximum posture");
            participant.BeginDefend();
            AssertTrue(participant.IsDefending,
                "The fixture must enter ordinary defend before breaking");

            AssertNear(100f, participant.ReducePosture(140f),
                "Incoming posture loss must clamp at zero");
            DRPGPostureBreakTransition entered =
                participant.EnterDeferredPostureBreak();
            AssertTrue(entered.EnteredBreak,
                "Deferred zero crossing must enter PostureBreak once");
            AssertEqual(
                DRPGPostureBreakState.ExposedPendingSkip,
                participant.PostureBreakState,
                "The first zero crossing must enter explicit PostureBreak");
            AssertTrue(participant.IsPostureBroken,
                "Explicit exposure, not a zero-value shortcut, defines break");
            AssertFalse(participant.IsDefending,
                "Entering PostureBreak must immediately clear ordinary defend");
            AssertNear(1f, participant.IncomingDefenseMultiplier,
                "Simplified posture depletion must not amplify HP damage");

            DRPGPostureBreakTransition skipped =
                participant.ResolvePostureBreakOwnTurnStart();
            AssertTrue(skipped.SkipAction,
                "The next own scheduling opportunity must be skipped");
            AssertNear(0f, participant.CurrentPosture,
                "The skipped opportunity must not restore posture");
            AssertNear(0f, participant.RecoverPostureAtTurnStart(),
                "Normal turn recovery must stay disabled while exposed");

            DRPGPostureBreakTransition recovered =
                participant.ResolvePostureBreakOwnTurnStart();
            AssertTrue(recovered.CanAct,
                "The opportunity after the skip must be actionable");
            AssertNear(50f, participant.CurrentPosture,
                "Recovery must restore exactly the configured maximum ratio");
            AssertEqual(
                DRPGPostureBreakState.RecoveryStability,
                participant.PostureBreakState,
                "Recovery must enter temporary stability");

            AssertNear(20f, participant.ReducePosture(20f),
                "Simplified posture uses the same direct subtraction in every state");
            AssertNear(30f, participant.CurrentPosture,
                "Direct posture loss must be applied to runtime state");
            AssertNear(10f, participant.SpendPosture(10f),
                "Self posture costs must not receive incoming-damage stability");

            participant.CompletePostureBreakOwnAction();
            participant.CompletePostureBreakOwnAction();
            participant.CompletePostureBreakOwnAction();
            AssertEqual(
                DRPGPostureBreakState.Stable,
                participant.PostureBreakState,
                "Three completed own actions must end recovery stability");
            AssertFalse(participant.IsPostureBroken,
                "Recovery stability completion must return to normal state");
            AssertNear(100f, profile.MaxPosture,
                "Runtime posture changes must not mutate profile configuration");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(profile);
        }

        return "PASS DRPG battle posture";
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

    private static void AssertNear(float expected, float actual, string message)
    {
        if (Mathf.Abs(expected - actual) > 0.001f)
            throw new Exception($"{message}. Expected={expected}, Actual={actual}");
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
            throw new Exception($"{message}. Expected={expected}, Actual={actual}");
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
}
#endif
