#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using UnityEngine;

public static class DRPGBattleDamageBrokenProbe
{
    public static string Run()
    {
        DRPGSkillSO attack = ScriptableObject.CreateInstance<DRPGSkillSO>();
        DRPGCombatantProfileSO attackerProfile = CreateProfile(attack);
        DRPGCombatantProfileSO teammateProfile = CreateProfile(attack);
        DRPGCombatantProfileSO protagonistProfile = CreateProfile(attack);
        GameObject presenterHost = null;

        try
        {
            SetField(attack, "power", 60f);
            SetField(teammateProfile, "damageBrokenHpRatio", 0.5f);
            SetField(protagonistProfile, "damageBrokenHpRatio", 0.5f);
            var attacker = CreateParticipant(
                1,
                attackerProfile,
                DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy);
            var teammate = CreateParticipant(
                2,
                teammateProfile,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.PartyMember);
            var protagonist = CreateParticipant(
                3,
                protagonistProfile,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.Protagonist);
            var resolver = new DRPGActionResolver();
            BattleParticipantRuntime[] participants =
                { attacker, teammate, protagonist };
            DRPGBattleSessionState state = CreateState(
                51001L,
                participants,
                out BattleRuleMutationLease lease);
            try
            {
            DRPGBattleHitResult teammateHit = ResolveAndCommit(
                resolver,
                new DRPGBattleCommand(
                    attacker.RuntimeId,
                    teammate.RuntimeId,
                    attack),
                attacker,
                teammate,
                participants,
                state,
                lease);
            AssertTrue(teammateHit.TriggeredDamageBroken,
                "Crossing the configured HP threshold must trigger break once");
            AssertEqual(DRPGLowHpPresentationState.DamageBroken, teammate.LowHpState,
                "A threshold crossing must persist the broken state");

            SetField(attack, "power", 5f);
            DRPGBattleHitResult repeatedHit = ResolveAndCommit(
                resolver,
                new DRPGBattleCommand(
                    attacker.RuntimeId,
                    teammate.RuntimeId,
                    attack),
                attacker,
                teammate,
                participants,
                state,
                lease);
            AssertFalse(repeatedHit.TriggeredDamageBroken,
                "Further damage below the threshold must not retrigger break");

            SetField(attack, "power", 60f);
            DRPGBattleHitResult protagonistHit = ResolveAndCommit(
                resolver,
                new DRPGBattleCommand(
                    attacker.RuntimeId,
                    protagonist.RuntimeId,
                    attack),
                attacker,
                protagonist,
                participants,
                state,
                lease);
            AssertTrue(protagonistHit.TriggeredDamageBroken,
                "The protagonist still owns a real broken state");
            AssertEqual(
                0,
                BattleDamageBrokenCutInPresenter.FilterEligible(
                    new[] { protagonistHit }).Count,
                "The protagonist must never receive a large break cut-in");
            AssertEqual(
                1,
                BattleDamageBrokenCutInPresenter.FilterEligible(
                    new[] { protagonistHit, teammateHit }).Count,
                "A broken teammate must remain eligible for the cut-in");

            IReadOnlyList<float> pairCenters =
                BattleDamageBrokenCutInPresenter.BuildCenteredPositions(
                    2,
                    1600f,
                    440f,
                    20f);
            AssertEqual(2, pairCenters.Count,
                "Two simultaneous breaks need two cut-in columns");
            AssertTrue(Mathf.Approximately(
                    pairCenters[0] + pairCenters[1],
                    0f),
                "Two cut-in columns must remain centered as a group");

            presenterHost = new GameObject(
                "DRPG Break Continuation Probe",
                typeof(RectTransform));
            BattleDamageBrokenCutInPresenter presenter =
                presenterHost.AddComponent<BattleDamageBrokenCutInPresenter>();
            GameObject overlay = new GameObject(
                "Overlay",
                typeof(RectTransform),
                typeof(CanvasGroup));
            overlay.transform.SetParent(presenterHost.transform, false);
            GameObject viewObject = new GameObject(
                "Break View",
                typeof(RectTransform));
            viewObject.transform.SetParent(overlay.transform, false);
            BattleDamageBrokenCutInView view =
                viewObject.AddComponent<BattleDamageBrokenCutInView>();
            SetField(presenter, "visibleRoot", overlay);
            SetField(presenter, "safeArea", overlay.GetComponent<RectTransform>());
            SetField(presenter, "inputBlocker", overlay.GetComponent<CanvasGroup>());
            SetField(
                presenter,
                "views",
                new List<BattleDamageBrokenCutInView> { view });

            BattleParticipantPresentationSnapshot teammateSnapshot =
                BattleParticipantPresentationSnapshot.From(teammate);
            IBattlePresentationHandle completed = presenter.Play(
                new[] { teammateSnapshot });
            AssertTrue(presenter.IsPlaying,
                "A valid teammate break must start the cut-in sequence");
            AssertFalse(completed.IsSettled,
                "A playing break cut-in must expose a pending handle");

            Sequence active = GetField<Sequence>(presenter, "activeSequence");
            active.Complete(true);
            AssertEqual(BattlePresentationOutcome.Completed, completed.Outcome,
                "Completing the cut-in sequence must complete its handle");

            IBattlePresentationHandle cancelled = presenter.Play(
                new[] { teammateSnapshot });
            presenter.Cancel(BattlePresentationCancelReason.ScopeEnded);
            AssertEqual(BattlePresentationOutcome.Cancelled, cancelled.Outcome,
                "Cancelling a blocking cut-in must settle it as cancelled");
            presenter.Cancel(BattlePresentationCancelReason.ScopeEnded);
            AssertEqual(BattlePresentationOutcome.Cancelled, cancelled.Outcome,
                "Repeated cancellation must preserve one terminal outcome");

            IBattlePresentationHandle skipped = presenter.Play(
                Array.Empty<BattleParticipantPresentationSnapshot>());
            AssertEqual(BattlePresentationOutcome.Skipped, skipped.Outcome,
                "A cut-in without eligible snapshots must skip immediately");
            }
            finally
            {
                lease.Dispose();
            }
        }
        finally
        {
            if (presenterHost != null)
                UnityEngine.Object.DestroyImmediate(presenterHost);
            UnityEngine.Object.DestroyImmediate(protagonistProfile);
            UnityEngine.Object.DestroyImmediate(teammateProfile);
            UnityEngine.Object.DestroyImmediate(attackerProfile);
            UnityEngine.Object.DestroyImmediate(attack);
        }

        return "PASS DRPG broken state and cut-in eligibility";
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

    private static DRPGCombatantProfileSO CreateProfile(DRPGSkillSO skill)
    {
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        SetField(profile, "maxHp", 100f);
        SetField(profile, "skills", new List<DRPGSkillSO> { skill });
        return profile;
    }

    private static DRPGBattleSessionState CreateState(
        long battleGeneration,
        IReadOnlyList<BattleParticipantRuntime> participants,
        out BattleRuleMutationLease lease)
    {
        var scheduler = new DRPGTurnScheduler();
        var arbiter = new DRPGBattleTransactionArbiter(
            battleGeneration,
            new DRPGBattleActionTraceWriter(
                new DRPGBoundedActionTraceSink(64)));
        var state = new DRPGBattleSessionState(
            battleGeneration,
            participants,
            scheduler,
            arbiter);
        BattleTransactionIdentity owner =
            BattleTransactionIdentity.ForTestHarness(
                new BattleStartIdentity(battleGeneration, 1L));
        if (!arbiter.TryAcquire(owner, out lease, out string error))
            throw new InvalidOperationException(error);
        return state;
    }

    private static DRPGBattleHitResult ResolveAndCommit(
        DRPGActionResolver resolver,
        DRPGBattleCommand command,
        BattleParticipantRuntime actor,
        BattleParticipantRuntime target,
        IReadOnlyList<BattleParticipantRuntime> participants,
        DRPGBattleSessionState state,
        BattleRuleMutationLease lease)
    {
        DRPGBattleMutationWorkspace workspace = state.CreateWorkspace();
        DRPGWorkspaceResolutionResult resolution =
            resolver.ResolveIntoWorkspace(
                DRPGActionSpecResolver.Resolve(command, actor),
                actor,
                target,
                participants,
                workspace,
                DRPGBattleImpactContext.ForStrike(
                    DRPGStrikeGrade.Body,
                    1f));
        AssertEqual(
            DRPGWorkspaceResolutionStatus.Succeeded,
            resolution.Status,
            "DamageBroken resolution must be staged in the workspace");
        AssertEqual(1, resolution.Batch.Hits.Count,
            "DamageBroken resolution must produce one hit");
        BattleJournalCommitResult committed =
            state.TryCommitJournal(lease, workspace);
        AssertTrue(committed.Succeeded,
            "DamageBroken workspace commit failed: " + committed.Error);
        return resolution.Batch.Hits[0];
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

    private static T GetField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, fieldName);
        return (T)field.GetValue(target);
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
