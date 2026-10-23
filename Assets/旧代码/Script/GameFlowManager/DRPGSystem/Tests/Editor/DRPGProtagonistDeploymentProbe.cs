#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGProtagonistDeploymentProbe
{
    public static string Run()
    {
        DRPGCombatantProfileSO protagonist = CreateProfile("protagonist");
        DRPGCombatantProfileSO memberA = CreateProfile("member-a");
        DRPGCombatantProfileSO memberB = CreateProfile("member-b");
        DRPGCombatantProfileSO replacement = CreateProfile("replacement");
        DRPGCombatantProfileSO enemy = CreateProfile("enemy");
        DRPGPartySetupSO party = ScriptableObject.CreateInstance<DRPGPartySetupSO>();
        DRPGEncounterDefinitionSO encounter =
            ScriptableObject.CreateInstance<DRPGEncounterDefinitionSO>();

        try
        {
            SetField(party, "protagonist", protagonist);
            SetField(
                party,
                "partyMembers",
                new List<DRPGCombatantProfileSO> { memberA, memberB });
            SetField(
                encounter,
                "enemies",
                new List<DRPGCombatantProfileSO> { enemy });

            VerifyRequestFreezesParty(party, encounter, memberA, replacement);
            VerifyEveryPlayerIsBattlefieldMember(party, encounter);
            VerifyHudUsesPartyOrder(party, encounter, protagonist, memberA, memberB);
            VerifyProtagonistIdentitySurvivesPhysicalReorder(
                party, encounter, protagonist, memberA, memberB);
            VerifyLegacySupportSurfaceRemoved();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(encounter);
            UnityEngine.Object.DestroyImmediate(party);
            UnityEngine.Object.DestroyImmediate(enemy);
            UnityEngine.Object.DestroyImmediate(replacement);
            UnityEngine.Object.DestroyImmediate(memberB);
            UnityEngine.Object.DestroyImmediate(memberA);
            UnityEngine.Object.DestroyImmediate(protagonist);
        }

        return "PASS DRPG protagonist identity and movable physical deployment";
    }

    private static void VerifyProtagonistIdentitySurvivesPhysicalReorder(
        DRPGPartySetupSO party,
        DRPGEncounterDefinitionSO encounter,
        DRPGCombatantProfileSO protagonist,
        DRPGCombatantProfileSO memberA,
        DRPGCombatantProfileSO memberB)
    {
        DRPGPartyRosterSnapshot identity =
            DRPGPartyCompositionState.FromSetup(party).CreateSnapshot();
        var physical = new DRPGNodePartyFormation(new[]
        {
            memberB.CombatantId,
            memberA.CombatantId,
            protagonist.CombatantId
        });
        DRPGBattleSessionCreationResult result =
            DRPGBattleSessionFactory.CreateRoster(
                identity.WithPhysicalOrder(physical.Capture()), encounter);
        AssertTrue(result.IsSuccess, result.ErrorMessage);

        BattleParticipantRuntime protagonistRuntime = null;
        for (int i = 0; i < result.Participants.Count; i++)
        {
            BattleParticipantRuntime participant = result.Participants[i];
            if (ReferenceEquals(participant.Profile, protagonist))
                protagonistRuntime = participant;
        }
        AssertTrue(protagonistRuntime != null,
            "Reordered roster must retain the protagonist participant");
        AssertEqual(DRPGBattleRosterRole.Protagonist,
            protagonistRuntime.RosterRole,
            "Protagonist role must follow stable identity, not physical index");
        AssertEqual(2, protagonistRuntime.FormationSlot,
            "Protagonist must be allowed to occupy the third physical slot");
    }

    private static void VerifyLegacySupportSurfaceRemoved()
    {
        AssertTrue(
            typeof(DRPGPartySetupSO).GetField(
                "supportSlotUnlocked",
                BindingFlags.Instance | BindingFlags.NonPublic) == null,
            "Party setup must not retain the removed support-slot flag");
        AssertTrue(
            typeof(DRPGPartySetupSO).GetField(
                "protagonistDeploymentMode",
                BindingFlags.Instance | BindingFlags.NonPublic) == null,
            "Party setup must not retain the removed protagonist deployment mode");
        AssertTrue(
            typeof(DRPGBattleViewModel).GetProperty("SupportProtagonist") == null,
            "Battle presentation must not expose a separate support protagonist");
        AssertTrue(
            typeof(DRPGBattlePresentationSnapshot).GetProperty(
                "SupportProtagonist") == null,
            "Presentation snapshots must contain only the ordinary party roster");
    }

    private static void VerifyRequestFreezesParty(
        DRPGPartySetupSO party,
        DRPGEncounterDefinitionSO encounter,
        DRPGCombatantProfileSO originalFirstMember,
        DRPGCombatantProfileSO replacement)
    {
        var request = new DRPGBattleStartRequest(party, encounter, default, 42);
        SetField(
            party,
            "partyMembers",
            new List<DRPGCombatantProfileSO> { replacement });

        AssertEqual(3, request.RosterSnapshot.Count,
            "A battle request must freeze the roster at request creation");
        AssertSame(originalFirstMember, request.RosterSnapshot.OrderedProfiles[1],
            "Mutating the menu party must not rewrite an in-flight battle request");

        SetField(
            party,
            "partyMembers",
            new List<DRPGCombatantProfileSO>
            {
                originalFirstMember,
                request.RosterSnapshot.OrderedProfiles[2]
            });
    }

    private static void VerifyEveryPlayerIsBattlefieldMember(
        DRPGPartySetupSO party,
        DRPGEncounterDefinitionSO encounter)
    {
        DRPGBattleSessionCreationResult result =
            DRPGBattleSessionFactory.CreateRoster(party, encounter);
        AssertTrue(result.IsSuccess, result.ErrorMessage);

        int playerCount = 0;
        for (int i = 0; i < result.Participants.Count; i++)
        {
            BattleParticipantRuntime participant = result.Participants[i];
            if (participant.Side != DRPGBattleSide.Player)
                continue;
            playerCount++;
            AssertTrue(participant.IsBattlefieldMember,
                "Every player roster role must be a battlefield member");
            AssertTrue(participant.AppearsInPartyHud,
                "Every deployed player must appear in the ordinary HUD");
            AssertTrue(participant.IsOrdinaryEnemyTarget,
                "Every living deployed player must be an ordinary enemy target");
        }
        AssertEqual(3, playerCount,
            "The protagonist and both teammates must all be deployed");
    }

    private static void VerifyHudUsesPartyOrder(
        DRPGPartySetupSO party,
        DRPGEncounterDefinitionSO encounter,
        DRPGCombatantProfileSO protagonist,
        DRPGCombatantProfileSO memberA,
        DRPGCombatantProfileSO memberB)
    {
        DRPGBattleSessionCreationResult result =
            DRPGBattleSessionFactory.CreateRoster(party, encounter);
        AssertTrue(result.IsSuccess, result.ErrorMessage);

        BattleParticipantRuntime protagonistRuntime = null;
        BattleParticipantRuntime memberARuntime = null;
        BattleParticipantRuntime memberBRuntime = null;
        BattleParticipantRuntime enemyRuntime = null;
        for (int i = 0; i < result.Participants.Count; i++)
        {
            BattleParticipantRuntime participant = result.Participants[i];
            if (ReferenceEquals(participant.Profile, protagonist))
                protagonistRuntime = participant;
            else if (ReferenceEquals(participant.Profile, memberA))
                memberARuntime = participant;
            else if (ReferenceEquals(participant.Profile, memberB))
                memberBRuntime = participant;
            else if (participant.Side == DRPGBattleSide.Enemy)
                enemyRuntime = participant;
        }

        var scrambled = new List<BattleParticipantRuntime>
        {
            memberBRuntime,
            enemyRuntime,
            memberARuntime,
            protagonistRuntime
        };
        var viewModel = new DRPGBattleViewModel(DRPGBattleHeadlessSessionFactory.Create(scrambled));
        IReadOnlyList<DRPGBattleParticipantViewModel> hud = viewModel.PartyMembers;

        AssertEqual(3, hud.Count, "The HUD must contain all three players");
        AssertSame(protagonist, hud[0].Profile,
            "The protagonist must remain the left-most HUD member");
        AssertSame(memberA, hud[1].Profile,
            "The first teammate must follow the protagonist");
        AssertSame(memberB, hud[2].Profile,
            "The second teammate must remain in party order");
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
