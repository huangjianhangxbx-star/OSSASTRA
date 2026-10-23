#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class DRPGFormationSystemProbe
{
    public static string Run()
    {
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();

        try
        {
            SetField(skill, "sourceFormationRows", DRPGFormationRowMask.Front);
            SetField(
                skill,
                "targetFormationRows",
                DRPGFormationRowMask.Front | DRPGFormationRowMask.Middle);

            AssertTrue(skill.CanUseFrom(DRPGFormationRow.Front),
                "A front-only skill must be usable from the front row");
            AssertFalse(skill.CanUseFrom(DRPGFormationRow.Rear),
                "A front-only skill must be rejected from the rear row");
            AssertTrue(skill.CanTarget(DRPGFormationRow.Middle),
                "The configured middle target row must be legal");
            AssertFalse(skill.CanTarget(DRPGFormationRow.Rear),
                "An omitted rear target row must be illegal");

            var exposedRear = Create(
                1,
                profile,
                DRPGFormationRow.Rear,
                DRPGFormationRow.Rear);
            DRPGFormationNormalizer.CompactOccupiedRows(
                new[] { exposedRear },
                DRPGBattleSide.Enemy);
            AssertEqual(DRPGFormationRow.Front, exposedRear.FormationRow,
                "A lone rear-preferring monster must be exposed as front row");
            AssertEqual(DRPGFormationRow.Rear, exposedRear.PreferredFormationRow,
                "Automatic exposure must not erase the monster's preference");

            var front = Create(
                2,
                profile,
                DRPGFormationRow.Front,
                DRPGFormationRow.Front);
            var rear = Create(
                3,
                profile,
                DRPGFormationRow.Rear,
                DRPGFormationRow.Rear);
            DRPGFormationNormalizer.CompactOccupiedRows(
                new[] { front, rear },
                DRPGBattleSide.Enemy);
            AssertEqual(DRPGFormationRow.Middle, rear.FormationRow,
                "Front plus rear with an empty middle must compact to front plus middle");

            var rules = new DRPGFormationRules(
                initialEnemyLimit: 6,
                activeEnemyLimit: 6,
                perRowLimit: 2,
                reinforcementDelayActions: 1);
            AssertEqual(6, rules.InitialEnemyLimit,
                "Default initial deployment cap must remain configurable at six");
            AssertEqual(6, rules.ActiveEnemyLimit,
                "The DD1-style stage must cap active enemies at six");
            AssertEqual(2, rules.PerRowLimit,
                "Each row must enforce its own configurable capacity");

            AssertEqual(6, DRPGFormationRules.Default.InitialEnemyLimit,
                "The default encounter must deploy at most six enemies");
            AssertEqual(6, DRPGFormationRules.Default.ActiveEnemyLimit,
                "The default battle stage must never display more than six enemies");
            AssertEqual(2, DRPGFormationRules.Default.PerRowLimit,
                "The default three-row stage must expose two enemy slots per row");

            IReadOnlyList<DRPGFormationLayoutPlacement> placements =
                DRPGFormationLayoutCalculator.Calculate(
                    new[]
                    {
                        new DRPGFormationLayoutItem(10, 1f),
                        new DRPGFormationLayoutItem(11, 5f)
                    },
                    availableWidth: 1000f,
                    baseWidth: 120f,
                    spacing: 20f);
            AssertEqual(2, placements.Count,
                "The layout must preserve every participant");
            AssertTrue(placements[0].CenterX < placements[1].CenterX,
                "The authored order must remain left-to-right");
            AssertTrue(
                placements[1].CenterX - placements[0].CenterX >=
                (placements[0].Width + placements[1].Width) * 0.5f - 0.01f,
                "Different visual widths must not overlap while space is available");
            float left = placements[0].CenterX - placements[0].Width * 0.5f;
            float right = placements[1].CenterX + placements[1].Width * 0.5f;
            AssertTrue(Mathf.Abs(left + right) < 0.01f,
                "A mixed-width group must still be centered as a whole");

            VerifyInstalledEncounterDeployment();
            VerifyPreferredRepositionConsumesTurn();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(skill);
        }

        return "PASS DRPG formation system";
    }

    private static void VerifyInstalledEncounterDeployment()
    {
        DRPGPartySetupSO party = AssetDatabase.LoadAssetAtPath<DRPGPartySetupSO>(
            "Assets/GameResources/DRPG/Config/Test/DRPGParty_Test3.asset");
        DRPGEncounterDefinitionSO encounter =
            AssetDatabase.LoadAssetAtPath<DRPGEncounterDefinitionSO>(
                "Assets/GameResources/DRPG/Config/Test/DRPGEncounter_Test2.asset");
        AssertTrue(party != null && encounter != null,
            "The installed formation test assets must exist");

        DRPGBattleSessionCreationResult created =
            DRPGBattleSessionFactory.CreateRoster(party, encounter);
        AssertTrue(created.IsSuccess, created.ErrorMessage);

        int activeEnemies = CountEnemies(created.Participants);
        AssertEqual(6, activeEnemies,
            "The test encounter must deploy at most six enemies initially");
        AssertEqual(4, created.ReserveEnemies.Count,
            "Remaining configured enemies must stay in the reserve queue");
        AssertRowsWithinLimit(created.Participants, 2);

        var session = DRPGBattleHeadlessSessionFactory.Create(
            created.Participants,
            reserveEnemies: created.ReserveEnemies,
            formationRules: created.FormationRules);
        session.Start();
        AssertTrue(session.CurrentActor != null,
            "The installed formation session must begin a turn");
        AssertTrue(session.TrySubmitCommand(
                DRPGBattleCommand.Wait(session.CurrentActor.RuntimeId),
                out string error),
            error);
        AssertTrue(!session.HasIncomingEnemyWarning,
            "A full six-slot stage must not announce reinforcements until a " +
            "visible enemy slot becomes available");
        AssertEqual(6, session.ActiveEnemyCount,
            "The warning action must not deploy a reserve immediately");

        AssertTrue(session.TrySubmitCommand(
                DRPGBattleCommand.Wait(session.CurrentActor.RuntimeId),
                out error),
            error);
        AssertEqual(6, session.ActiveEnemyCount,
            "A full six-slot stage must keep reinforcements in reserve");
        AssertEqual(4, session.ReserveEnemyCount,
            "The reserve queue must remain intact while every active slot is occupied");
        AssertRowsWithinLimit(session.Participants, 2);
    }

    private static void VerifyPreferredRepositionConsumesTurn()
    {
        DRPGSkillSO movementSkill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        DRPGCombatantProfileSO moverProfile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        DRPGCombatantProfileSO shieldProfile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        DRPGCombatantProfileSO middleAnchorProfile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        DRPGCombatantProfileSO playerProfile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        try
        {
            SetField(
                movementSkill,
                "sourceFormationRows",
                DRPGFormationRowMask.All);
            SetField(
                movementSkill,
                "targetFormationRows",
                DRPGFormationRowMask.All);
            SetField(
                moverProfile,
                "skills",
                new List<DRPGSkillSO> { movementSkill });
            SetField(
                shieldProfile,
                "skills",
                new List<DRPGSkillSO> { movementSkill });
            SetField(
                middleAnchorProfile,
                "skills",
                new List<DRPGSkillSO> { movementSkill });
            SetField(moverProfile, "agility", 100f);
            SetField(shieldProfile, "agility", 1f);
            SetField(middleAnchorProfile, "agility", 1f);
            SetField(playerProfile, "agility", 1f);
            SetField(moverProfile, "baseTimelineDelay", 100f);
            SetField(shieldProfile, "baseTimelineDelay", 100f);
            SetField(middleAnchorProfile, "baseTimelineDelay", 100f);
            SetField(playerProfile, "baseTimelineDelay", 100f);

            BattleParticipantRuntime shield = Create(
                100,
                shieldProfile,
                DRPGFormationRow.Front,
                DRPGFormationRow.Front);
            BattleParticipantRuntime middleAnchor = Create(
                104,
                middleAnchorProfile,
                DRPGFormationRow.Middle,
                DRPGFormationRow.Middle);
            BattleParticipantRuntime mover = Create(
                101,
                moverProfile,
                DRPGFormationRow.Middle,
                DRPGFormationRow.Rear);
            var player = new BattleParticipantRuntime(
                102,
                new BattleParticipantSpec(
                    playerProfile,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.PartyMember,
                    0));
            var session = DRPGBattleHeadlessSessionFactory.Create(
                new[] { shield, middleAnchor, mover, player });
            int completedActions = 0;
            session.ActionCompleted += _ => completedActions++;
            session.Start();

            AssertEqual(mover.RuntimeId, session.CurrentActor.RuntimeId,
                "The fast rear-preferring monster must own the test turn");
            AssertTrue(session.TryCreateEnemyCommand(
                    out DRPGBattleCommand reposition),
                "A shielded monster must be able to move toward its preferred row");
            AssertEqual(DRPGBattleCommandKind.Reposition, reposition.Kind,
                "Preferred-row movement must be represented by an explicit command");
            AssertEqual(
                DRPGFormationRow.Rear,
                reposition.DestinationFormationRow.Value,
                "The monster must move exactly one row toward its preference");
            AssertTrue(session.TrySubmitCommand(reposition, out string error),
                error);
            AssertEqual(DRPGFormationRow.Rear, mover.FormationRow,
                "A completed reposition command must update the current row");
            AssertEqual(1, completedActions,
                "Repositioning must consume one complete battle action");

            var exposed = Create(
                103,
                moverProfile,
                DRPGFormationRow.Front,
                DRPGFormationRow.Rear);
            var exposedPlayer = new BattleParticipantRuntime(
                105,
                new BattleParticipantSpec(
                    playerProfile,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.PartyMember,
                    0));
            var exposedSession = DRPGBattleHeadlessSessionFactory.Create(
                new[] { exposed, exposedPlayer });
            exposedSession.Start();
            AssertEqual(DRPGBattleSide.Player,
                exposedSession.CurrentActor.Side,
                "An exposed ordinary attacker must first grant warning-zero response");
            AssertTrue(exposedSession.TrySubmitCommand(
                    DRPGBattleCommand.Wait(
                        exposedSession.CurrentActor.RuntimeId),
                    out error),
                error);
            AssertTrue(exposedSession.TryCreateEnemyCommand(
                    out DRPGBattleCommand exposedCommand),
                "An exposed monster must still choose an action");
            AssertFalse(
                exposedCommand.Kind == DRPGBattleCommandKind.Reposition,
                "A lone monster cannot retreat behind an empty front row");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(movementSkill);
            UnityEngine.Object.DestroyImmediate(moverProfile);
            UnityEngine.Object.DestroyImmediate(shieldProfile);
            UnityEngine.Object.DestroyImmediate(middleAnchorProfile);
            UnityEngine.Object.DestroyImmediate(playerProfile);
        }
    }

    private static int CountEnemies(
        IReadOnlyList<BattleParticipantRuntime> participants)
    {
        int count = 0;
        for (int i = 0; i < participants.Count; i++)
        {
            if (participants[i].Side == DRPGBattleSide.Enemy &&
                participants[i].IsDeployed)
            {
                count++;
            }
        }
        return count;
    }

    private static void AssertRowsWithinLimit(
        IReadOnlyList<BattleParticipantRuntime> participants,
        int limit)
    {
        for (int row = 0; row < 3; row++)
        {
            int count = 0;
            for (int i = 0; i < participants.Count; i++)
            {
                BattleParticipantRuntime participant = participants[i];
                if (participant.Side == DRPGBattleSide.Enemy &&
                    participant.IsAlive && participant.IsDeployed &&
                    (int)participant.FormationRow == row)
                {
                    count++;
                }
            }
            AssertTrue(count <= limit,
                "An enemy row exceeded its configured capacity");
        }
    }

    private static BattleParticipantRuntime Create(
        int id,
        DRPGCombatantProfileSO profile,
        DRPGFormationRow current,
        DRPGFormationRow preferred)
    {
        return new BattleParticipantRuntime(
            id,
            new BattleParticipantSpec(
                profile,
                DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy,
                id,
                initialFormationRow: current,
                preferredFormationRow: preferred));
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
}
#endif
