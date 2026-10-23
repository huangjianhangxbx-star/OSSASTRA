#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGBattleDomainProbe
{
    public static string Run()
    {
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();

        try
        {
            var protagonist = new BattleParticipantRuntime(
                1,
                new BattleParticipantSpec(
                    profile,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.Protagonist,
                    0));
            var member = new BattleParticipantRuntime(
                2,
                new BattleParticipantSpec(
                    profile,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.PartyMember,
                    1));
            var enemy = new BattleParticipantRuntime(
                3,
                new BattleParticipantSpec(
                    profile,
                    DRPGBattleSide.Enemy,
                    DRPGBattleRosterRole.Enemy,
                    0));

            AssertEqual(DRPGBattleSide.Player, protagonist.Side,
                "The protagonist must belong to the player side");
            AssertTrue(protagonist.AppearsInPartyHud,
                "A battlefield protagonist must consume a party HUD slot");
            AssertTrue(member.AppearsInPartyHud,
                "Ordinary party members must appear in the party HUD");
            AssertFalse(enemy.AppearsInPartyHud,
                "Enemies must not appear in the player party HUD");
            AssertSame(profile, protagonist.Profile,
                "All roster roles must use the shared combatant profile type");
            AssertSame(profile, enemy.Profile,
                "Enemies must not require a separate EnemySO subtype");

            float configuredHp = profile.MaxHp;
            protagonist.ApplyDamage(25f);
            AssertEqual(configuredHp - 25f, protagonist.CurrentHp,
                "Runtime damage must change only runtime HP");
            AssertEqual(configuredHp, profile.MaxHp,
                "Runtime damage must never mutate source configuration");

            VerifySessionFactory(profile);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(profile);
        }

        return "PASS DRPG battle domain";
    }

    private static void VerifySessionFactory(DRPGCombatantProfileSO sharedProfile)
    {
        DRPGPartySetupSO party = ScriptableObject.CreateInstance<DRPGPartySetupSO>();
        DRPGEncounterDefinitionSO encounter =
            ScriptableObject.CreateInstance<DRPGEncounterDefinitionSO>();
        DRPGCombatantProfileSO memberA =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        DRPGCombatantProfileSO memberB =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        DRPGCombatantProfileSO enemyA =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        DRPGCombatantProfileSO enemyB =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();

        try
        {
            SetField(sharedProfile, "combatantId", "domain-protagonist");
            SetField(memberA, "combatantId", "domain-member-a");
            SetField(memberB, "combatantId", "domain-member-b");
            SetField(enemyA, "combatantId", "domain-enemy-a");
            SetField(enemyB, "combatantId", "domain-enemy-b");
            SetField(party, "protagonist", sharedProfile);
            SetField(
                party,
                "partyMembers",
                new List<DRPGCombatantProfileSO> { memberA, memberB });
            SetField(
                encounter,
                "enemies",
                new List<DRPGCombatantProfileSO> { enemyA, enemyB });

            DRPGBattleSessionCreationResult result =
                DRPGBattleSessionFactory.CreateRoster(party, encounter);

            AssertTrue(result.IsSuccess, result.ErrorMessage);
            AssertEqual(5, result.Participants.Count,
                "The first battle fixture must create three player actors and two enemies");
            AssertEqual(3, CountSide(result.Participants, DRPGBattleSide.Player),
                "The roster must contain protagonist plus two party members");
            AssertEqual(2, CountSide(result.Participants, DRPGBattleSide.Enemy),
                "The roster must contain two enemies");
            AssertEqual(5, CountUniqueRuntimeIds(result.Participants),
                "Every runtime participant must receive a unique ID");

            SetField(party, "protagonist", null);
            DRPGBattleSessionCreationResult missingProtagonist =
                DRPGBattleSessionFactory.CreateRoster(party, encounter);
            AssertFalse(missingProtagonist.IsSuccess,
                "A party without the required protagonist must be rejected");

            SetField(party, "protagonist", sharedProfile);
            SetField(encounter, "enemies", new List<DRPGCombatantProfileSO>());
            DRPGBattleSessionCreationResult missingEnemies =
                DRPGBattleSessionFactory.CreateRoster(party, encounter);
            AssertFalse(missingEnemies.IsSuccess,
                "An encounter without enemies must be rejected");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(enemyB);
            UnityEngine.Object.DestroyImmediate(enemyA);
            UnityEngine.Object.DestroyImmediate(memberB);
            UnityEngine.Object.DestroyImmediate(memberA);
            UnityEngine.Object.DestroyImmediate(party);
            UnityEngine.Object.DestroyImmediate(encounter);
        }
    }

    private static int CountSide(
        IReadOnlyList<BattleParticipantRuntime> participants,
        DRPGBattleSide side)
    {
        int count = 0;
        for (int i = 0; i < participants.Count; i++)
        {
            if (participants[i].Side == side)
                count++;
        }

        return count;
    }

    private static int CountUniqueRuntimeIds(
        IReadOnlyList<BattleParticipantRuntime> participants)
    {
        var ids = new HashSet<int>();
        for (int i = 0; i < participants.Count; i++)
            ids.Add(participants[i].RuntimeId);
        return ids.Count;
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

    private static void AssertSame(object expected, object actual, string message)
    {
        if (!ReferenceEquals(expected, actual))
            throw new Exception(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
            throw new Exception($"{message}. Expected={expected}, Actual={actual}");
    }
}
#endif
