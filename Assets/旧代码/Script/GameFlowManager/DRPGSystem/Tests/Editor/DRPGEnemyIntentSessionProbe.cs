#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGEnemyIntentSessionProbe
{
    public static string Run()
    {
        DRPGSkillSO attack = CreateAttack();
        DRPGSkillSO unusableAttack = CreateUnusableAttack();
        DRPGSkillSO selfHeal = CreateSelfHeal();
        var createdProfiles = new List<DRPGCombatantProfileSO>();
        try
        {
            VerifyFastEnemyStillGrantsOnePlayerResponse(
                attack,
                createdProfiles);
            VerifyOneResponseReleasesEveryReadyEnemy(
                attack,
                createdProfiles);
            VerifyInvalidCommittedTargetFizzlesWithoutRetarget(
                attack,
                createdProfiles);
            VerifyEnemyWithoutLegalActionOwnsSilentWaitPlan(
                attack,
                unusableAttack,
                createdProfiles);
            VerifyEnemyUsesLegalSupportBeforeWaiting(
                attack,
                unusableAttack,
                selfHeal,
                createdProfiles);
            VerifyIdleReinforcementReceivesExplicitPlan(
                attack,
                unusableAttack,
                createdProfiles);
        }
        finally
        {
            for (int i = 0; i < createdProfiles.Count; i++)
                UnityEngine.Object.DestroyImmediate(createdProfiles[i]);
            UnityEngine.Object.DestroyImmediate(attack);
            UnityEngine.Object.DestroyImmediate(unusableAttack);
            UnityEngine.Object.DestroyImmediate(selfHeal);
        }

        return "PASS DRPG enemy intent session";
    }

    private static void VerifyEnemyWithoutLegalActionOwnsSilentWaitPlan(
        DRPGSkillSO playerAttack,
        DRPGSkillSO unusableAttack,
        ICollection<DRPGCombatantProfileSO> profiles)
    {
        BattleParticipantRuntime player = CreateParticipant(
            30,
            CreateProfile(playerAttack, 5f, 500f, profiles),
            DRPGBattleSide.Player,
            DRPGBattleRosterRole.Protagonist,
            0);
        BattleParticipantRuntime idleEnemy = CreateParticipant(
            31,
            CreateProfile(unusableAttack, 30f, 50f, profiles),
            DRPGBattleSide.Enemy,
            DRPGBattleRosterRole.Enemy,
            0,
            DRPGFormationRow.Rear,
            DRPGFormationRow.Rear);
        var session = DRPGBattleHeadlessSessionFactory.Create(new[] { idleEnemy, player });
        session.Start();

        AssertSame(idleEnemy, session.CurrentActor,
            "The faster idle enemy must still receive a normal turn");
        AssertTrue(session.IsCurrentEnemyCommandFromOrdinaryIntent,
            "An enemy with no legal action must use an explicit ordinary plan instead of the legacy fallback");
        DRPGEnemyActionPlanSnapshot snapshot = FindPlan(
            session,
            idleEnemy.RuntimeId);
        AssertEqual(DRPGEnemyPlannedActionKind.Wait, snapshot.ActionKind,
            "An enemy with no legal attack, support or movement must plan Wait");
        AssertFalse(snapshot.HasEnemyWarning,
            "A silent Wait plan must never display an attack countdown");
        AssertTrue(session.TryCreateEnemyCommand(out DRPGBattleCommand command),
            "The silent Wait plan must materialize a command");
        AssertEqual(DRPGBattleCommandKind.Wait, command.Kind,
            "The silent plan must materialize exactly one Wait command");
        AssertTrue(session.TrySubmitCommand(command, out string error), error);
        AssertEqual(DRPGBattleOutcome.None, session.Outcome,
            "Completing a silent Wait must keep the battle alive");
        AssertTrue(session.CurrentActor != null,
            "Completing the last currently actionable enemy must not empty the scheduler");
    }

    private static void VerifyEnemyUsesLegalSupportBeforeWaiting(
        DRPGSkillSO playerAttack,
        DRPGSkillSO unusableAttack,
        DRPGSkillSO selfHeal,
        ICollection<DRPGCombatantProfileSO> profiles)
    {
        BattleParticipantRuntime player = CreateParticipant(
            40,
            CreateProfile(playerAttack, 5f, 500f, profiles),
            DRPGBattleSide.Player,
            DRPGBattleRosterRole.Protagonist,
            0);
        BattleParticipantRuntime healer = CreateParticipant(
            41,
            CreateProfile(
                new[] { unusableAttack, selfHeal },
                30f,
                50f,
                profiles),
            DRPGBattleSide.Enemy,
            DRPGBattleRosterRole.Enemy,
            0);
        DRPGBattleStateMutationFixture.PrepareHitPoints(
            healer,
            healer.CurrentHp - 40f);
        float hpBefore = healer.CurrentHp;
        var session = DRPGBattleHeadlessSessionFactory.Create(new[] { healer, player });
        session.Start();

        AssertSame(healer, session.CurrentActor,
            "The support-capable enemy must own the first test turn");
        AssertTrue(session.IsCurrentEnemyCommandFromOrdinaryIntent,
            "A legal standard support skill must use the explicit enemy-plan path");
        AssertTrue(session.TryCreateEnemyCommand(out DRPGBattleCommand command),
            "The support plan must materialize a command");
        AssertEqual(DRPGBattleCommandKind.Skill, command.Kind,
            "A wounded enemy must use its legal heal before falling back to Wait");
        AssertSame(selfHeal, command.Skill,
            "The selected support command must retain the planned skill");
        AssertEqual(healer.RuntimeId, command.TargetRuntimeId,
            "A self-heal must target its acting enemy");
        AssertTrue(session.TrySubmitCommand(command, out string error), error);
        AssertTrue(healer.CurrentHp > hpBefore,
            "The planned support action must resolve its healing effect");
    }

    private static void VerifyIdleReinforcementReceivesExplicitPlan(
        DRPGSkillSO playerAttack,
        DRPGSkillSO unusableAttack,
        ICollection<DRPGCombatantProfileSO> profiles)
    {
        BattleParticipantRuntime player = CreateParticipant(
            50,
            CreateProfile(playerAttack, 10f, 100f, profiles),
            DRPGBattleSide.Player,
            DRPGBattleRosterRole.Protagonist,
            0);
        BattleParticipantRuntime activeEnemy = CreateParticipant(
            51,
            CreateProfile(unusableAttack, 10f, 100f, profiles),
            DRPGBattleSide.Enemy,
            DRPGBattleRosterRole.Enemy,
            0);
        BattleParticipantRuntime reinforcement = CreateParticipant(
            52,
            CreateProfile(unusableAttack, 10f, 100f, profiles),
            DRPGBattleSide.Enemy,
            DRPGBattleRosterRole.Enemy,
            1,
            DRPGFormationRow.Rear,
            DRPGFormationRow.Rear);
        var rules = new DRPGFormationRules(
            initialEnemyLimit: 1,
            activeEnemyLimit: 3,
            perRowLimit: 5,
            reinforcementDelayActions: 0);
        var session = DRPGBattleHeadlessSessionFactory.Create(
            new[] { player, activeEnemy },
            reserveEnemies: new[] { reinforcement },
            formationRules: rules);
        session.Start();

        for (int i = 0; i < 6 && !reinforcement.IsDeployed; i++)
            SubmitCurrentAction(session);

        AssertTrue(reinforcement.IsDeployed,
            "The test reinforcement must enter after the configured delay");
        DRPGEnemyActionPlanSnapshot snapshot = FindPlan(
            session,
            reinforcement.RuntimeId);
        AssertEqual(DRPGEnemyPlannedActionKind.Wait, snapshot.ActionKind,
            "A newly joined enemy with no legal action must immediately receive an explicit Wait plan");
        AssertFalse(snapshot.HasEnemyWarning,
            "An idle reinforcement must not display a countdown");
    }

    private static void VerifyFastEnemyStillGrantsOnePlayerResponse(
        DRPGSkillSO attack,
        ICollection<DRPGCombatantProfileSO> profiles)
    {
        BattleParticipantRuntime player = CreateParticipant(
            1,
            CreateProfile(attack, 10f, 1000f, profiles),
            DRPGBattleSide.Player,
            DRPGBattleRosterRole.Protagonist,
            0);
        BattleParticipantRuntime enemy = CreateParticipant(
            2,
            CreateProfile(attack, 20f, 100f, profiles),
            DRPGBattleSide.Enemy,
            DRPGBattleRosterRole.Enemy,
            0);
        var session = DRPGBattleHeadlessSessionFactory.Create(new[] { enemy, player });
        session.Start();

        AssertSame(player, session.CurrentActor,
            "A ready enemy must stop at warning zero and yield one player turn");
        AssertEqual(1, session.ReadyEnemyPlans.Count,
            "The ready enemy must be visible through the sanitized ready queue");
        AssertEqual(0, session.ReadyEnemyPlans[0].WarningValue,
            "A committed ordinary enemy attack must display warning zero");
        AssertFalse(session.IsCurrentEnemyCommandFromOrdinaryIntent,
            "The ordinary enemy plan must not become current before a player response");

        SubmitWait(session, player);
        AssertSame(enemy, session.CurrentActor,
            "One completed allied action must release the warning-zero enemy");
        AssertTrue(session.IsCurrentEnemyCommandFromOrdinaryIntent,
            "The released enemy command must identify its ordinary-plan origin");
        AssertTrue(session.TryCreateEnemyCommand(out DRPGBattleCommand command),
            "The released plan must materialize one committed command");
        AssertEqual(player.RuntimeId, command.TargetRuntimeId,
            "The warning-zero commit must retain its selected target");
    }

    private static void VerifyOneResponseReleasesEveryReadyEnemy(
        DRPGSkillSO attack,
        ICollection<DRPGCombatantProfileSO> profiles)
    {
        BattleParticipantRuntime player = CreateParticipant(
            10,
            CreateProfile(attack, 10f, 1000f, profiles),
            DRPGBattleSide.Player,
            DRPGBattleRosterRole.Protagonist,
            0);
        BattleParticipantRuntime firstEnemy = CreateParticipant(
            11,
            CreateProfile(attack, 25f, 80f, profiles),
            DRPGBattleSide.Enemy,
            DRPGBattleRosterRole.Enemy,
            0);
        BattleParticipantRuntime secondEnemy = CreateParticipant(
            12,
            CreateProfile(attack, 20f, 100f, profiles),
            DRPGBattleSide.Enemy,
            DRPGBattleRosterRole.Enemy,
            1);
        var session = DRPGBattleHeadlessSessionFactory.Create(
            new[] { secondEnemy, player, firstEnemy });
        session.Start();

        AssertSame(player, session.CurrentActor,
            "Both faster enemies must wait behind the same player response gate");
        AssertSequence(
            new[] { firstEnemy.RuntimeId, secondEnemy.RuntimeId },
            session.ReadyEnemyPlans,
            "Ready plans must retain scheduler order");

        SubmitWait(session, player);
        AssertSame(firstEnemy, session.CurrentActor,
            "The earliest ready enemy must execute first");
        SubmitCurrentEnemyAction(session);
        AssertSame(secondEnemy, session.CurrentActor,
            "The second ready enemy must execute immediately after the first");
    }

    private static void VerifyInvalidCommittedTargetFizzlesWithoutRetarget(
        DRPGSkillSO attack,
        ICollection<DRPGCombatantProfileSO> profiles)
    {
        BattleParticipantRuntime lockedTarget = CreateParticipant(
            20,
            CreateProfile(attack, 8f, 400f, profiles),
            DRPGBattleSide.Player,
            DRPGBattleRosterRole.PartyMember,
            1);
        DRPGStatusDefinitionSO fatalTurnStart =
            ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>();
        SetField(fatalTurnStart, "statusId", "intent_probe_fatal_turn_start");
        SetField(fatalTurnStart, "turnStartHpDelta", -1000f);
        DRPGBattleStateMutationFixture.PrepareStatus(
            lockedTarget,
            DRPGResolvedStatusSpec.From(fatalTurnStart));
        UnityEngine.Object.DestroyImmediate(fatalTurnStart);
        BattleParticipantRuntime actingPlayer = CreateParticipant(
            21,
            CreateProfile(attack, 12f, 500f, profiles),
            DRPGBattleSide.Player,
            DRPGBattleRosterRole.Protagonist,
            0);
        BattleParticipantRuntime enemy = CreateParticipant(
            22,
            CreateProfile(attack, 30f, 50f, profiles),
            DRPGBattleSide.Enemy,
            DRPGBattleRosterRole.Enemy,
            0);
        var session = DRPGBattleHeadlessSessionFactory.Create(
            new[] { lockedTarget, actingPlayer, enemy },
            randomSource: new FixedRandom(0));
        int fizzleEvents = 0;
        session.EnemyPlanFizzled += _ => fizzleEvents++;
        session.Start();

        AssertSame(actingPlayer, session.CurrentActor,
            "The surviving ally must own the response after the locked target dies on its real turn-start tick");
        AssertTrue(!lockedTarget.IsAlive,
            "The committed target must become stale through the normal turn-start status path");
        SubmitWait(session, actingPlayer);
        AssertSame(enemy, session.CurrentActor,
            "The committed enemy must still consume its action opportunity");
        AssertTrue(session.TryCreateEnemyCommand(out DRPGBattleCommand command),
            "A stale ordinary plan must materialize a safe fizzle command");
        AssertEqual(DRPGBattleCommandKind.Wait, command.Kind,
            "A dead committed target must fizzle as Wait instead of retargeting");
        AssertEqual(1, fizzleEvents,
            "The stale commit must publish one and only one fizzle event");
    }

    private static void SubmitCurrentEnemyAction(DRPGBattleSession session)
    {
        AssertTrue(session.TryCreateEnemyCommand(out DRPGBattleCommand command),
            "The current enemy plan could not create a command");
        AssertTrue(session.TrySubmitCommand(command, out string error), error);
    }

    private static void SubmitCurrentAction(DRPGBattleSession session)
    {
        AssertTrue(session.CurrentActor != null,
            "The battle scheduler unexpectedly produced no current actor");
        if (session.CurrentActor.Side == DRPGBattleSide.Enemy)
        {
            SubmitCurrentEnemyAction(session);
            return;
        }

        SubmitWait(session, session.CurrentActor);
    }

    private static void SubmitWait(
        DRPGBattleSession session,
        BattleParticipantRuntime actor)
    {
        AssertTrue(session.TrySubmitCommand(
            DRPGBattleCommand.Wait(actor.RuntimeId),
            out string error), error);
    }

    private static DRPGSkillSO CreateAttack()
    {
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        SetField(skill, "skillId", "intent_probe_attack");
        SetField(skill, "displayName", "Intent Probe Attack");
        SetField(skill, "power", 5f);
        SetField(skill, "prepareDelay", 0f);
        SetField(skill, "recoveryDelay", 0f);
        SetField(skill, "enemyWarningSteps", 2);
        SetField(skill, "enemyTargetDisclosureMode",
            DRPGEnemyTargetDisclosureMode.Hidden);
        SetField(skill, "executionMode", DRPGSkillExecutionMode.Standard);
        return skill;
    }

    private static DRPGSkillSO CreateUnusableAttack()
    {
        DRPGSkillSO skill = CreateAttack();
        SetField(skill, "skillId", "intent_probe_unusable_attack");
        SetField(skill, "displayName", "Unusable Probe Attack");
        SetField(skill, "targetFormationRows", DRPGFormationRowMask.None);
        return skill;
    }

    private static DRPGSkillSO CreateSelfHeal()
    {
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        SetField(skill, "skillId", "intent_probe_self_heal");
        SetField(skill, "displayName", "Intent Probe Self Heal");
        SetField(skill, "power", 25f);
        SetField(skill, "prepareDelay", 0f);
        SetField(skill, "recoveryDelay", 0f);
        SetField(skill, "purpose", DRPGSkillPurpose.Healing);
        SetField(skill, "targetRule", DRPGSkillTargetRule.Self);
        SetField(skill, "executionMode", DRPGSkillExecutionMode.Standard);
        return skill;
    }

    private static DRPGCombatantProfileSO CreateProfile(
        DRPGSkillSO attack,
        float agility,
        float baseDelay,
        ICollection<DRPGCombatantProfileSO> profiles)
    {
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        SetField(profile, "maxHp", 1000f);
        SetField(profile, "agility", agility);
        SetField(profile, "baseTimelineDelay", baseDelay);
        SetField(profile, "baseRecoveryDelay", 0f);
        SetField(profile, "skills", new List<DRPGSkillSO> { attack });
        profiles.Add(profile);
        return profile;
    }

    private static DRPGCombatantProfileSO CreateProfile(
        IReadOnlyList<DRPGSkillSO> skills,
        float agility,
        float baseDelay,
        ICollection<DRPGCombatantProfileSO> profiles)
    {
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        SetField(profile, "maxHp", 1000f);
        SetField(profile, "agility", agility);
        SetField(profile, "baseTimelineDelay", baseDelay);
        SetField(profile, "baseRecoveryDelay", 0f);
        SetField(profile, "skills", new List<DRPGSkillSO>(skills));
        profiles.Add(profile);
        return profile;
    }

    private static BattleParticipantRuntime CreateParticipant(
        int runtimeId,
        DRPGCombatantProfileSO profile,
        DRPGBattleSide side,
        DRPGBattleRosterRole role,
        int slot,
        DRPGFormationRow initialRow = DRPGFormationRow.Front,
        DRPGFormationRow preferredRow = DRPGFormationRow.Front)
    {
        return new BattleParticipantRuntime(
            runtimeId,
            new BattleParticipantSpec(
                profile,
                side,
                role,
                slot,
                initialFormationRow: initialRow,
                preferredFormationRow: preferredRow));
    }

    private static DRPGEnemyActionPlanSnapshot FindPlan(
        DRPGBattleSession session,
        int actorRuntimeId)
    {
        IReadOnlyList<DRPGEnemyActionPlanSnapshot> plans = session.EnemyPlans;
        for (int i = 0; i < plans.Count; i++)
        {
            if (plans[i].ActorRuntimeId == actorRuntimeId)
                return plans[i];
        }

        throw new Exception(
            $"Enemy {actorRuntimeId} has no explicit action plan.");
    }

    private static void AssertSequence(
        IReadOnlyList<int> expected,
        IReadOnlyList<DRPGEnemyActionPlanSnapshot> actual,
        string message)
    {
        if (expected.Count != actual.Count)
        {
            throw new Exception(
                $"{message}. Expected count={expected.Count}, " +
                $"Actual count={actual.Count}");
        }
        for (int i = 0; i < expected.Count; i++)
        {
            if (expected[i] != actual[i].ActorRuntimeId)
            {
                throw new Exception(
                    $"{message}. Index={i}, Expected={expected[i]}, " +
                    $"Actual={actual[i].ActorRuntimeId}");
            }
        }
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
        {
            throw new Exception(
                $"{message}. Expected={expected}, Actual={actual}");
        }
    }

    private sealed class FixedRandom : IDRPGBattleRandom
    {
        private readonly int value;

        public FixedRandom(int value)
        {
            this.value = value;
        }

        public int Next(int exclusiveMax)
        {
            return Math.Min(value, Math.Max(0, exclusiveMax - 1));
        }
    }
}
#endif
