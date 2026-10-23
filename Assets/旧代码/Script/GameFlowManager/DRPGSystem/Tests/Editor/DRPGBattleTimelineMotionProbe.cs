#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGBattleTimelineMotionProbe
{
    public static string Run()
    {
        VerifyTransitionDiff();
        VerifyTrackedTimelineCancellation();
        return "PASS DRPG timeline runtime-ID transition diff";
    }

    private static void VerifyTransitionDiff()
    {
        IReadOnlyList<BattleTimelineTransitionMove> moves =
            BattleTimelineTransitionPresenter.BuildMoves(
                new[] { 10, 20, 30 },
                new[] { 20, 30, 10 });

        BattleTimelineTransitionMove actorB = Find(moves, 20);
        AssertEqual(1, actorB.FromIndex,
            "Runtime-ID 20 must preserve its previous slot");
        AssertEqual(0, actorB.ToIndex,
            "Runtime-ID 20 must move into the current-actor slot");

        BattleTimelineTransitionMove completing = Find(moves, 10);
        AssertEqual(0, completing.FromIndex,
            "The completing actor must originate at the current slot");
        AssertEqual(2, completing.ToIndex,
            "The completing actor must move to its newly scheduled slot");
        AssertTrue(completing.IsCompletingActor,
            "Only the old current actor receives the completion hop");

        int completingCount = 0;
        for (int i = 0; i < moves.Count; i++)
        {
            if (moves[i].IsCompletingActor)
                completingCount++;
        }
        AssertEqual(1, completingCount,
            "One timeline rotation must identify one completing actor");
    }

    private static void VerifyTrackedTimelineCancellation()
    {
        GameObject host = new GameObject(
            "Timeline Handle Probe",
            typeof(RectTransform));
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        try
        {
            BattleTimelineView timeline = host.AddComponent<BattleTimelineView>();
            var entries = new List<BattleTimelineEntryView>
            {
                CreateEntry(host.transform, "Entry A"),
                CreateEntry(host.transform, "Entry B")
            };
            SetField(timeline, "entries", entries);

            BattleParticipantRuntime first = CreateParticipant(1, profile);
            BattleParticipantRuntime second = CreateParticipant(2, profile);
            timeline.BindImmediate(new[]
            {
                new DRPGBattleParticipantViewModel(first, 0f, true),
                new DRPGBattleParticipantViewModel(second, 1f, false)
            });

            IBattlePresentationHandle playback = timeline.PlayTransition(
                new[]
                {
                    new DRPGBattleParticipantViewModel(second, 0f, true),
                    new DRPGBattleParticipantViewModel(first, 1f, false)
                },
                0.32f);
            AssertFalse(playback.IsSettled,
                "A timeline transition must remain pending while tweening");
            timeline.KillActiveTransition(
                BattlePresentationCancelReason.Replaced);
            AssertEqual(BattlePresentationOutcome.Cancelled, playback.Outcome,
                "Replacing a timeline transition must cancel its handle");

            IBattlePresentationHandle disabledPlayback =
                timeline.PlayTransition(
                    new[]
                    {
                        new DRPGBattleParticipantViewModel(first, 0f, true),
                        new DRPGBattleParticipantViewModel(second, 1f, false)
                    },
                    0.32f);
            InvokePrivate(timeline, "OnDisable");
            AssertEqual(
                BattlePresentationOutcome.Cancelled,
                disabledPlayback.Outcome,
                "Disabling the timeline must cancel its active transition");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    private static BattleTimelineEntryView CreateEntry(
        Transform parent,
        string name)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child.AddComponent<BattleTimelineEntryView>();
    }

    private static BattleParticipantRuntime CreateParticipant(
        int runtimeId,
        DRPGCombatantProfileSO profile)
    {
        return new BattleParticipantRuntime(
            runtimeId,
            new BattleParticipantSpec(
                profile,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.PartyMember,
                runtimeId - 1));
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

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
            throw new MissingMethodException(target.GetType().Name, methodName);
        method.Invoke(target, null);
    }

    private static BattleTimelineTransitionMove Find(
        IReadOnlyList<BattleTimelineTransitionMove> moves,
        int runtimeId)
    {
        for (int i = 0; i < moves.Count; i++)
        {
            if (moves[i].RuntimeId == runtimeId)
                return moves[i];
        }

        throw new Exception("Missing timeline move for " + runtimeId);
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
}
#endif
