#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGBattleInteractiveCoordinatorProbe
{
    public static string Run()
    {
        var created = new List<UnityEngine.Object>();
        try
        {
            VerifyTelegraphedDefenseTransaction(created);
            VerifyFreeStrikeTransaction(created);
        }
        finally
        {
            for (int i = created.Count - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(created[i]);
        }

        return "PASS DRPG interactive transaction coordinator";
    }

    private static void VerifyTelegraphedDefenseTransaction(
        ICollection<UnityEngine.Object> created)
    {
        DRPGSkillSO skill = CreateSkill(
            created,
            "probe_enemy_special",
            DRPGSkillExecutionMode.TelegraphedSpecial,
            20f);
        SetField(skill, "specialImpactTimes",
            new List<float> { 0.65f, 1.10f, 1.55f });
        SetField(skill, "guardLeadSeconds", 0.42f);
        SetField(skill, "perfectLeadSeconds", 0.12f);
        SetField(skill, "missedGuardPostureDamage", 34f);
        SetField(skill, "guardedPostureDamage", 22f);

        DRPGCombatantProfileSO enemyProfile = CreateProfile(
            created,
            "Special Enemy",
            1000f,
            100f,
            skill);
        DRPGCombatantProfileSO playerProfile = CreateProfile(
            created,
            "Guard Target",
            100f,
            1f,
            skill);
        SetField(playerProfile, "baseTimelineDelay", 300f);
        BattleParticipantRuntime enemy = CreateRuntime(
            1,
            enemyProfile,
            DRPGBattleSide.Enemy,
            DRPGBattleRosterRole.Enemy);
        BattleParticipantRuntime player = CreateRuntime(
            2,
            playerProfile,
            DRPGBattleSide.Player,
            DRPGBattleRosterRole.PartyMember);
        var session = DRPGBattleHeadlessSessionFactory.Create(new[] { enemy, player });
        CoordinatorFixture fixture = CreateCoordinator(created);

        fixture.Coordinator.Bind(session, 101L);
        session.Start();
        AssertSame(enemy, session.CurrentActor,
            "The fast enemy must receive the first turn");

        AssertTrue(session.TryCreateEnemyCommand(out DRPGBattleCommand warning),
            "The enemy must create its warning command");
        AssertEqual(DRPGBattleCommandStage.Telegraph, warning.Stage,
            "The first special command must only telegraph");
        AssertTrue(session.TryPrepareCommand(warning, out _, out string warningError),
            warningError);
        AssertEqual(1, session.PendingTelegraphs.Count,
            "Completing the warning transaction must register one telegraph");
        AssertEqual(1, fixture.Audio.Count(BattleAudioCue.SpecialWarning),
            "The warning transaction must emit one semantic warning cue");
        AssertSame(enemy, session.CurrentActor,
            "The fast enemy must reach its release turn before the slow target");

        float hpBefore = player.CurrentHp;
        AssertTrue(session.TryCreateEnemyCommand(out DRPGBattleCommand release),
            "The telegraph must become a release command");
        AssertEqual(DRPGBattleCommandStage.Release, release.Stage,
            "The pending special must release on the next enemy turn");
        AssertTrue(session.TryPrepareCommand(release, out _, out string releaseError),
            releaseError);
        AssertTrue(fixture.Coordinator.HasActiveInteractiveAction,
            "Release must open the shared defense interaction window");
        AssertTrue(fixture.Director.InputGate.IsBlocked,
            "The defense window must own an input-gate lease");
        AssertEqual(1, fixture.Audio.Count(BattleAudioCue.InteractiveWindowOpen),
            "Opening defense must emit one interaction-open cue");

        fixture.Coordinator.TickInteractive(0.55f);
        AssertTrue(fixture.Coordinator.SubmitGuardInput(),
            "A guard press inside the first perfect window must be accepted");
        fixture.Coordinator.TickInteractive(0.11f);
        fixture.Coordinator.TickInteractive(1f);

        AssertApproximately(hpBefore - 40f, player.CurrentHp, 0.001f,
            "One perfect guard and two misses must apply exactly two full hits");
        AssertApproximately(32f, player.CurrentPosture, 0.001f,
            "Only the two missed impacts must consume posture");
        AssertFalse(fixture.Coordinator.HasActiveInteractiveAction,
            "The defense interaction must settle after its final impact");
        AssertFalse(fixture.Director.InputGate.IsBlocked,
            "Settling the interaction must always release its input lease");
        AssertEqual(0, session.PendingTelegraphs.Count,
            "The released telegraph must be consumed exactly once");
        AssertEqual(1, fixture.Audio.Count(BattleAudioCue.PerfectGuard),
            "The perfect defense must emit one perfect-guard cue");
        AssertEqual(2, fixture.Audio.Count(BattleAudioCue.Miss),
            "The two unguarded impacts must emit two miss cues");
        AssertEqual(1, fixture.Audio.Count(BattleAudioCue.InteractiveWindowClose),
            "Settling defense must emit one interaction-close cue");
    }

    private static void VerifyFreeStrikeTransaction(
        ICollection<UnityEngine.Object> created)
    {
        DRPGSkillSO skill = CreateSkill(
            created,
            "probe_free_strike",
            DRPGSkillExecutionMode.FreeStrike,
            10f);
        SetField(skill, "freeStrikeDuration", 3f);
        SetField(skill, "freeStrikeCadence", 0.18f);
        SetField(skill, "freeStrikePostureCost", 12f);

        DRPGCombatantProfileSO playerProfile = CreateProfile(
            created,
            "Free Striker",
            100f,
            100f,
            skill);
        DRPGCombatantProfileSO enemyProfile = CreateProfile(
            created,
            "Strike Target",
            100f,
            1f,
            skill);
        BattleParticipantRuntime player = CreateRuntime(
            11,
            playerProfile,
            DRPGBattleSide.Player,
            DRPGBattleRosterRole.PartyMember);
        BattleParticipantRuntime enemy = CreateRuntime(
            12,
            enemyProfile,
            DRPGBattleSide.Enemy,
            DRPGBattleRosterRole.Enemy);
        var session = DRPGBattleHeadlessSessionFactory.Create(new[] { player, enemy });
        CoordinatorFixture fixture = CreateCoordinator(created);

        fixture.Coordinator.Bind(session, 102L);
        session.Start();
        var command = new DRPGBattleCommand(
            player.RuntimeId,
            enemy.RuntimeId,
            skill);
        AssertTrue(session.TryPrepareCommand(command, out _, out string error),
            error);
        AssertTrue(fixture.Coordinator.HasActiveInteractiveAction,
            "A free-strike skill must open the shared interaction window");
        AssertEqual(1, fixture.Audio.Count(BattleAudioCue.InteractiveWindowOpen),
            "Opening free strike must emit one interaction-open cue");

        AssertTrue(fixture.Coordinator.SubmitFreeStrike(
                DRPGStrikeGrade.Body),
            "The first full-body strike must be accepted");
        AssertFalse(fixture.Coordinator.SubmitFreeStrike(
                DRPGStrikeGrade.Body),
            "The cadence gate must reject an immediate second strike");
        fixture.Coordinator.TickInteractive(0.19f);
        AssertTrue(fixture.Coordinator.SubmitFreeStrike(
                DRPGStrikeGrade.Body),
            "A body strike after the cadence interval must be accepted");
        fixture.Coordinator.TickInteractive(0.19f);
        AssertTrue(fixture.Coordinator.SubmitFreeStrike(
                DRPGStrikeGrade.Body),
            "A later body strike must be accepted");

        AssertApproximately(70f, enemy.CurrentHp, 0.001f,
            "Three body strikes must resolve through the session");
        AssertApproximately(64f, player.CurrentPosture, 0.001f,
            "Each accepted strike must spend its configured posture cost");
        AssertEqual(3, fixture.Audio.Count(BattleAudioCue.Attack),
            "Each accepted free strike must emit an attack cue");
        AssertEqual(0, fixture.Audio.Count(BattleAudioCue.WeakHit),
            "Body-only free strikes must not emit spatial weak-point accents");

        fixture.Coordinator.TickInteractive(3f);
        AssertFalse(fixture.Coordinator.HasActiveInteractiveAction,
            "The free-strike transaction must settle when its timer expires");
        AssertFalse(fixture.Director.InputGate.IsBlocked,
            "The free-strike input lease must be released after settlement");
        AssertEqual(1, fixture.Audio.Count(BattleAudioCue.InteractiveWindowClose),
            "Settling free strike must emit one interaction-close cue");
    }

    private static CoordinatorFixture CreateCoordinator(
        ICollection<UnityEngine.Object> created)
    {
        BattlePresentationRecipeSetSO recipeSet =
            ScriptableObject.CreateInstance<BattlePresentationRecipeSetSO>();
        created.Add(recipeSet);
        var director = new BattlePresentationDirector(
            new BattlePresentationRecipeResolver(),
            recipeSet,
            Array.Empty<IBattlePresentationCueExecutor>(),
            new BattleInputGate(),
            new BattlePresentationTraceBuffer(64));
        var host = new GameObject("InteractiveCoordinatorProbe");
        created.Add(host);
        DRPGBattleActionTransactionCoordinator coordinator =
            host.AddComponent<DRPGBattleActionTransactionCoordinator>();
        coordinator.ConfigureDirector(director);
        var audio = new RecordingAudioSink();
        coordinator.ConfigureInteractiveView(null, null, audio);
        return new CoordinatorFixture(coordinator, director, audio);
    }

    private static DRPGSkillSO CreateSkill(
        ICollection<UnityEngine.Object> created,
        string id,
        DRPGSkillExecutionMode executionMode,
        float power)
    {
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        created.Add(skill);
        SetField(skill, "skillId", id);
        SetField(skill, "power", power);
        SetField(skill, "recoveryDelay", 0f);
        SetField(skill, "executionMode", executionMode);
        return skill;
    }

    private static DRPGCombatantProfileSO CreateProfile(
        ICollection<UnityEngine.Object> created,
        string displayName,
        float hp,
        float agility,
        DRPGSkillSO skill)
    {
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        created.Add(profile);
        SetField(profile, "displayName", displayName);
        SetField(profile, "maxHp", hp);
        SetField(profile, "agility", agility);
        SetField(profile, "baseTimelineDelay", 100f);
        SetField(profile, "baseRecoveryDelay", 0f);
        SetField(profile, "maxPosture", 100f);
        SetField(profile, "postureRecoveryPerTurn", 0f);
        SetField(profile, "skills", new List<DRPGSkillSO> { skill });
        return profile;
    }

    private static BattleParticipantRuntime CreateRuntime(
        int id,
        DRPGCombatantProfileSO profile,
        DRPGBattleSide side,
        DRPGBattleRosterRole role)
    {
        return new BattleParticipantRuntime(
            id,
            new BattleParticipantSpec(profile, side, role, 0));
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, name);
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
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception(
                $"{message}. Expected={expected}, Actual={actual}");
        }
    }

    private static void AssertApproximately(
        float expected,
        float actual,
        float tolerance,
        string message)
    {
        if (Math.Abs(expected - actual) > tolerance)
        {
            throw new Exception(
                $"{message}. Expected={expected}, Actual={actual}");
        }
    }

    private readonly struct CoordinatorFixture
    {
        public CoordinatorFixture(
            DRPGBattleActionTransactionCoordinator coordinator,
            BattlePresentationDirector director,
            RecordingAudioSink audio)
        {
            Coordinator = coordinator;
            Director = director;
            Audio = audio;
        }

        public DRPGBattleActionTransactionCoordinator Coordinator { get; }
        public BattlePresentationDirector Director { get; }
        public RecordingAudioSink Audio { get; }
    }

    private sealed class RecordingAudioSink : IBattleAudioCueSink
    {
        private readonly List<BattleAudioCueRequest> requests =
            new List<BattleAudioCueRequest>();

        public void Begin(DRPGBattleAudioPaletteSO palette)
        {
        }

        public void Play(BattleAudioCueRequest request)
        {
            requests.Add(request);
        }

        public void End()
        {
        }

        public int Count(BattleAudioCue cue)
        {
            int count = 0;
            for (int i = 0; i < requests.Count; i++)
            {
                if (requests[i].Cue == cue)
                    count++;
            }
            return count;
        }
    }
}
#endif
