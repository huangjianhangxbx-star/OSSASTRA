#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGProtagonistInvariantProbe
{
    public static string Run()
    {
        DRPGCombatantProfileSO protagonist = CreateProfile("protagonist");
        DRPGCombatantProfileSO memberA = CreateProfile("member-a");
        DRPGCombatantProfileSO memberB = CreateProfile("member-b");
        DRPGCombatantProfileSO memberC = CreateProfile("member-c");
        DRPGCombatantProfileSO memberD = CreateProfile("member-d");
        DRPGCombatantProfileSO overflow = CreateProfile("member-overflow");

        try
        {
            VerifyOneToFiveInvariant(
                protagonist,
                memberA,
                memberB,
                memberC,
                memberD,
                overflow);
            VerifyImmutableSnapshot(protagonist, memberA, memberB);
            VerifyFactoryUsesPartyOrder(protagonist, memberA, memberB);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(overflow);
            UnityEngine.Object.DestroyImmediate(memberD);
            UnityEngine.Object.DestroyImmediate(memberC);
            UnityEngine.Object.DestroyImmediate(memberB);
            UnityEngine.Object.DestroyImmediate(memberA);
            UnityEngine.Object.DestroyImmediate(protagonist);
        }

        return "PASS DRPG protagonist invariant";
    }

    private static void VerifyOneToFiveInvariant(
        DRPGCombatantProfileSO protagonist,
        DRPGCombatantProfileSO memberA,
        DRPGCombatantProfileSO memberB,
        DRPGCombatantProfileSO memberC,
        DRPGCombatantProfileSO memberD,
        DRPGCombatantProfileSO overflow)
    {
        var solo = new DRPGPartyCompositionState(
            protagonist,
            Array.Empty<DRPGCombatantProfileSO>());
        AssertEqual(1, solo.Count,
            "The protagonist alone is a valid one-member party");
        AssertSame(protagonist, solo.CreateSnapshot().OrderedProfiles[0],
            "The protagonist must occupy party index zero");

        var full = new DRPGPartyCompositionState(
            protagonist,
            new[] { memberA, memberB, memberC, memberD });
        AssertEqual(5, full.Count,
            "The five-member cap must include the protagonist");

        AssertThrows<ArgumentException>(() =>
            new DRPGPartyCompositionState(
                null,
                Array.Empty<DRPGCombatantProfileSO>()),
            "A party without its required protagonist must be rejected");
        AssertThrows<ArgumentException>(() =>
            new DRPGPartyCompositionState(
                protagonist,
                new[] { memberA, memberB, memberC, memberD, overflow }),
            "A protagonist plus five teammates must exceed the party cap");
        AssertThrows<ArgumentException>(() =>
            new DRPGPartyCompositionState(
                protagonist,
                new[] { memberA, memberA }),
            "Duplicate stable character IDs must be rejected");
    }

    private static void VerifyImmutableSnapshot(
        DRPGCombatantProfileSO protagonist,
        DRPGCombatantProfileSO memberA,
        DRPGCombatantProfileSO memberB)
    {
        var state = new DRPGPartyCompositionState(
            protagonist,
            new[] { memberA, memberB });
        DRPGPartyRosterSnapshot before = state.CreateSnapshot();

        bool changed = state.TrySetOrderedProfiles(
            new[] { protagonist, memberB, memberA },
            out string error);
        AssertTrue(changed, error);

        AssertSame(memberA, before.OrderedProfiles[1],
            "An existing battle snapshot must not observe menu reordering");
        DRPGPartyRosterSnapshot after = state.CreateSnapshot();
        AssertSame(protagonist, after.OrderedProfiles[0],
            "Reordering must never move the protagonist from index zero");
        AssertSame(memberB, after.OrderedProfiles[1],
            "A valid teammate reorder must be applied atomically");
    }

    private static void VerifyFactoryUsesPartyOrder(
        DRPGCombatantProfileSO protagonist,
        DRPGCombatantProfileSO memberA,
        DRPGCombatantProfileSO memberB)
    {
        DRPGCombatantProfileSO enemy = CreateProfile("enemy");
        DRPGEncounterDefinitionSO encounter =
            ScriptableObject.CreateInstance<DRPGEncounterDefinitionSO>();
        try
        {
            SetField(
                encounter,
                "enemies",
                new List<DRPGCombatantProfileSO> { enemy });
            var state = new DRPGPartyCompositionState(
                protagonist,
                new[] { memberB, memberA });

            DRPGBattleSessionCreationResult result =
                DRPGBattleSessionFactory.CreateRoster(
                    state.CreateSnapshot(),
                    encounter);
            AssertTrue(result.IsSuccess, result.ErrorMessage);

            var players = new List<BattleParticipantRuntime>();
            for (int i = 0; i < result.Participants.Count; i++)
            {
                BattleParticipantRuntime participant = result.Participants[i];
                if (participant.Side == DRPGBattleSide.Player)
                    players.Add(participant);
            }

            AssertEqual(3, players.Count,
                "Every configured player must be deployed");
            AssertEqual(DRPGBattleRosterRole.Protagonist, players[0].RosterRole,
                "Party order zero must use the protagonist role");
            AssertEqual(0, players[0].PartyOrderIndex,
                "The protagonist must have battle party order zero");
            AssertEqual(1, players[1].PartyOrderIndex,
                "The first teammate must have battle party order one");
            AssertSame(memberB, players[1].Profile,
                "Battle creation must consume the frozen snapshot order");
            AssertTrue(players[0].AppearsInPartyHud,
                "The protagonist must always consume the first HUD slot");
            AssertTrue(players[0].IsOrdinaryEnemyTarget,
                "The protagonist must be an ordinary enemy target");

            for (int i = 0; i < players.Count; i++)
            {
                DRPGBattleStateMutationFixture.PrepareHitPoints(
                    players[i],
                    0f);
            }
            var session = DRPGBattleHeadlessSessionFactory.Create(result.Participants);
            session.EvaluateOutcome();
            AssertEqual(DRPGBattleOutcome.Defeat, session.Outcome,
                "An incapacitated protagonist must count toward party defeat");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(encounter);
            UnityEngine.Object.DestroyImmediate(enemy);
        }
    }

    private static DRPGCombatantProfileSO CreateProfile(string stableId)
    {
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        SetField(profile, "combatantId", stableId);
        return profile;
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

    private static void AssertThrows<T>(Action action, string message)
        where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new Exception(message);
    }

    private static void AssertSame(object expected, object actual, string message)
    {
        if (!ReferenceEquals(expected, actual))
            throw new Exception(message);
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
        {
            throw new Exception(
                $"{message}. Expected={expected}, Actual={actual}");
        }
    }
}
#endif
