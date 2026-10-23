#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGBattleFlowProbe
{
    public static string Run()
    {
        DRPGSkillSO attack = ScriptableObject.CreateInstance<DRPGSkillSO>();
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        DRPGPartySetupSO party = ScriptableObject.CreateInstance<DRPGPartySetupSO>();
        DRPGEncounterDefinitionSO encounter =
            ScriptableObject.CreateInstance<DRPGEncounterDefinitionSO>();

        try
        {
            SetField(profile, "skills", new List<DRPGSkillSO> { attack });
            SetField(party, "protagonist", profile);
            SetField(encounter, "enemies", new List<DRPGCombatantProfileSO> { profile });

            var controller = new FakeFlowController();
            var stateMachine = new DRPGBattleFlowStateMachine(controller);
            stateMachine.Initialize(null, null);
            var request = new DRPGBattleStartRequest(party, encounter, default, 42);

            AssertTrue(stateMachine.RequestState(request),
                "A valid battle start must be accepted");
            AssertTrue(stateMachine.IsActive,
                "Accepted DRPG flow must become active at priority 30");
            AssertEqual(30, stateMachine.Priority,
                "DRPG flow must pre-empt priority-20 Hunt flow");
            AssertFalse(stateMachine.RequestState(request),
                "A repeated start must be rejected while a session is active");

            controller.Session.Abort();
            AssertFalse(stateMachine.IsActive,
                "A completed battle must release DRPG flow arbitration");
            AssertEqual(1, controller.CompleteCount,
                "Battle cleanup must execute exactly once");

            controller.AllowStart = false;
            AssertFalse(stateMachine.RequestState(request),
                "An invalid start must not activate DRPG flow");
            AssertFalse(stateMachine.IsActive,
                "Rejected starts must leave exploration arbitration untouched");

            DRPGCombatantProfileSO slowPlayer =
                ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
            DRPGCombatantProfileSO fastEnemy =
                ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
            try
            {
                SetField(slowPlayer, "agility", 10f);
                SetField(slowPlayer, "skills", new List<DRPGSkillSO> { attack });
                SetField(fastEnemy, "agility", 100f);
                SetField(fastEnemy, "skills", new List<DRPGSkillSO> { attack });

                var player = new BattleParticipantRuntime(
                    100,
                    new BattleParticipantSpec(
                        slowPlayer,
                        DRPGBattleSide.Player,
                        DRPGBattleRosterRole.Protagonist,
                        0));
                var enemy = new BattleParticipantRuntime(
                    101,
                    new BattleParticipantSpec(
                        fastEnemy,
                        DRPGBattleSide.Enemy,
                        DRPGBattleRosterRole.Enemy,
                        0));
                var previewController = new FakeFlowController
                {
                    SessionOverride = DRPGBattleHeadlessSessionFactory.Create(new[] { player, enemy })
                };
                var previewState = new DRPGBattleFlowStateMachine(previewController);
                previewState.Initialize(null, null);
                AssertTrue(previewState.RequestState(request),
                    "Enemy-preview flow must accept the prepared session");
                AssertEqual(DRPGBattleSide.Player,
                    previewController.Session.CurrentActor.Side,
                    "The ordinary fast enemy must first expose warning zero to a player");
                AssertTrue(previewController.Session.TrySubmitCommand(
                    DRPGBattleCommand.Wait(
                        previewController.Session.CurrentActor.RuntimeId),
                    out string responseError),
                    responseError);
                AssertSame(enemy, previewController.Session.CurrentActor,
                    "The preview fixture must release the enemy after one response");

                float hpBefore = player.CurrentHp;
                int previewEvents = 0;
                previewController.Session.CommandPreviewed += _ => previewEvents++;
                previewState.Update(0.1f);
                AssertEqual(hpBefore, player.CurrentHp,
                    "An ordinary intent must not bypass a missing staged-action owner");
                AssertTrue(!string.IsNullOrEmpty(previewState.LastError),
                    "A missing staged-action owner must leave a diagnosable flow error");
                AssertEqual(0, previewEvents,
                    "A hidden ordinary intent must not publish the legacy target preview");

                var stagedConsumer = new object();
                AssertTrue(previewController.Session.TryAttachStagedActionConsumer(
                        stagedConsumer),
                    "Production enemy flow requires one staged-action owner");
                previewController.Session.ActionPrepared += action =>
                {
                    AssertTrue(previewController.Session.TryResolveImpact(
                            action,
                            out _,
                            out string resolveError),
                        resolveError);
                    AssertTrue(previewController.Session.TryCompleteAction(
                            action,
                            out string completeError),
                        completeError);
                };

                previewState.Update(0f);
                AssertTrue(player.CurrentHp < hpBefore,
                    "A committed ordinary intent must prepare immediately without the 0.45-second legacy delay");
                AssertEqual(0, previewEvents,
                    "Ordinary intent execution must remain outside legacy target preview");
                previewController.Session.Abort();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fastEnemy);
                UnityEngine.Object.DestroyImmediate(slowPlayer);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(encounter);
            UnityEngine.Object.DestroyImmediate(party);
            UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(attack);
        }

        return "PASS DRPG battle flow";
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

    private static void AssertSame(object expected, object actual, string message)
    {
        if (!ReferenceEquals(expected, actual))
            throw new Exception(message);
    }

    private sealed class FakeFlowController : IDRPGBattleFlowController
    {
        public bool AllowStart { get; set; } = true;
        public int CompleteCount { get; private set; }
        public DRPGBattleSession Session { get; private set; }
        public DRPGBattleSession SessionOverride { get; set; }

        public bool TryBeginBattle(
            DRPGBattleStartRequest request,
            out DRPGBattleSession session,
            out string error)
        {
            if (!AllowStart)
            {
                session = null;
                error = "Rejected by fake controller.";
                return false;
            }

            if (SessionOverride != null)
            {
                Session = SessionOverride;
            }
            else
            {
                DRPGBattleSessionCreationResult roster =
                    DRPGBattleSessionFactory.CreateRoster(request.Party, request.Encounter);
                Session = DRPGBattleHeadlessSessionFactory.Create(roster.Participants);
            }
            Session.Start();
            session = Session;
            error = string.Empty;
            return true;
        }

        public void CompleteBattle(
            DRPGBattleSession session,
            DRPGBattleResult result)
        {
            CompleteCount++;
        }
    }
}
#endif
