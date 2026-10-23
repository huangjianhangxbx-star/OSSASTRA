#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGBattleSpineMarkerCueProbe
{
    public static string Run()
    {
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        DRPGCombatantProfileSO playerProfile = null;
        DRPGCombatantProfileSO enemyProfile = null;
        BattleSpineMarkerCueSO impactCue = null;
        BattleSpineMarkerCueSO endCue = null;
        GameObject host = null;
        try
        {
            SetField(skill, "power", 40f);
            SetField(skill, "impactDamageMultipliers",
                new List<float> { 0.25f, 0.25f, 0.5f });
            playerProfile = CreateProfile(skill, 30f);
            enemyProfile = CreateProfile(skill, 10f);
            var player = CreateParticipant(
                1,
                playerProfile,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.Protagonist);
            var enemy = CreateParticipant(
                2,
                enemyProfile,
                DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy);
            var session = DRPGBattleHeadlessSessionFactory.Create(new[] { player, enemy });
            session.Start();
            AssertTrue(session.TryPrepareCommand(
                new DRPGBattleCommand(1, 2, skill),
                out DRPGBattlePreparedAction action,
                out string error), error);

            var gateway = new FakeGateway();
            host = new GameObject("Spine Marker Cue Probe");
            BattleSpineMarkerCueExecutor executor =
                host.AddComponent<BattleSpineMarkerCueExecutor>();
            executor.Bind(gateway, session);
            impactCue = ScriptableObject.CreateInstance<BattleSpineMarkerCueSO>();
            SetField(impactCue, "markerName", BattleSpineMarkerNames.Impact);
            SetField(impactCue, "markerIndex", 0);
            SetField(impactCue, "timeoutSeconds", 1f);

            BattleParticipantRuntime secondPlayer = CreateParticipant(
                3,
                playerProfile,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.Protagonist);
            BattleParticipantRuntime secondEnemy = CreateParticipant(
                4,
                enemyProfile,
                DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy);
            var secondSession = DRPGBattleHeadlessSessionFactory.Create(
                new[] { secondPlayer, secondEnemy });
            secondSession.Start();
            AssertTrue(secondSession.TryPrepareCommand(
                new DRPGBattleCommand(3, 4, skill),
                out DRPGBattlePreparedAction secondAction,
                out error), error);
            AssertEqual(action.ActionId, secondAction.ActionId,
                "The stale-marker fixture requires local ActionId reuse.");
            float secondHpBeforeStaleMarker = secondEnemy.CurrentHp;
            IBattlePresentationHandle staleHandle = executor.Play(
                impactCue,
                CreateRequest(action));
            executor.Bind(gateway, secondSession);
            gateway.Match();
            AssertTrue(staleHandle.IsSettled,
                "The stale marker fixture did not settle its leaf handle.");
            AssertNear(secondHpBeforeStaleMarker, secondEnemy.CurrentHp,
                "A marker from another battle advanced the reused ActionId.");
            executor.Bind(gateway, session);

            float hpBefore = enemy.CurrentHp;
            IBattlePresentationHandle impactHandle = executor.Play(
                impactCue,
                CreateRequest(action));
            AssertFalse(impactHandle.IsSettled,
                "An available Spine marker must hold its cue gate");
            gateway.Match();
            gateway.Match();
            AssertTrue(impactHandle.IsSettled,
                "The matching marker must release its cue gate");
            AssertNear(hpBefore - 10f, enemy.CurrentHp,
                "Impact:0 must commit exactly one partition");

            endCue = ScriptableObject.CreateInstance<BattleSpineMarkerCueSO>();
            SetField(endCue, "markerName",
                BattleSpineMarkerNames.PresentationEnd);
            SetField(endCue, "markerIndex", 0);
            SetField(endCue, "timeoutSeconds", 1f);
            IBattlePresentationHandle endHandle = executor.Play(
                endCue,
                CreateRequest(action));
            gateway.Timeout();
            AssertTrue(endHandle.IsSettled,
                "A missing presentation-end marker must fail open");
            AssertNear(hpBefore - 40f, enemy.CurrentHp,
                "Presentation-end fallback must flush pending impacts");
            AssertEqual(0, action.ImpactLedger.PendingIndices.Count,
                "Fallback completion cannot leave a battle-stalling partition");

            return "PASS DRPG Spine marker cue execution";
        }
        finally
        {
            if (host != null)
                UnityEngine.Object.DestroyImmediate(host);
            if (endCue != null)
                UnityEngine.Object.DestroyImmediate(endCue);
            if (impactCue != null)
                UnityEngine.Object.DestroyImmediate(impactCue);
            if (enemyProfile != null)
                UnityEngine.Object.DestroyImmediate(enemyProfile);
            if (playerProfile != null)
                UnityEngine.Object.DestroyImmediate(playerProfile);
            UnityEngine.Object.DestroyImmediate(skill);
        }
    }

    private static BattlePresentationRequest CreateRequest(
        DRPGBattlePreparedAction action)
    {
        return new BattlePresentationRequest(
            BattlePresentationRequestIds.Next(),
            BattlePresentationMoment.ActionWindup,
            new BattlePresentationScope(
                action.Identity.BattleGeneration,
                action.Identity.TurnSerial,
                action.Identity.ActionId),
            null,
            Array.Empty<BattleParticipantPresentationSnapshot>(),
            Array.Empty<int>(),
            action.Spec.PresentationKey,
            action.Spec.AnimationIntent,
            Array.Empty<BattleHitPresentationSnapshot>(),
            BattlePresentationFacts.Empty);
    }

    private static BattleParticipantRuntime CreateParticipant(
        int id,
        DRPGCombatantProfileSO profile,
        DRPGBattleSide side,
        DRPGBattleRosterRole role)
    {
        return new BattleParticipantRuntime(
            id,
            new BattleParticipantSpec(profile, side, role, 0));
    }

    private static DRPGCombatantProfileSO CreateProfile(
        DRPGSkillSO skill,
        float agility)
    {
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        SetField(profile, "agility", agility);
        SetField(profile, "skills", new List<DRPGSkillSO> { skill });
        return profile;
    }

    private static void SetField(object target, string name, object value)
    {
        Type type = target.GetType();
        while (type != null)
        {
            FieldInfo field = type.GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }
            type = type.BaseType;
        }
        throw new MissingFieldException(target.GetType().Name, name);
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
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException(message);
    }

    private sealed class FakeGateway : IBattleSpineMarkerGateway
    {
        private Action matched;
        private Action timedOut;
        private BattlePresentationHandle handle;

        public IBattlePresentationHandle WaitFor(
            BattlePresentationScope scope,
            int actorRuntimeId,
            BattleSpineMarker expected,
            float timeoutSeconds,
            Action onMatched,
            Action onTimedOut)
        {
            matched = onMatched;
            timedOut = onTimedOut;
            handle = new BattlePresentationHandle();
            return handle;
        }

        public void Match()
        {
            matched?.Invoke();
            handle?.TrySettle(BattlePresentationOutcome.Completed);
        }

        public void Timeout()
        {
            timedOut?.Invoke();
            handle?.TrySettle(BattlePresentationOutcome.TimedOut);
        }
    }
}
#endif
