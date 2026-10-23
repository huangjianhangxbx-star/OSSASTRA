#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGBattlePresentationTransactionCoordinatorProbe
{
    public static string Run()
    {
        var created = new List<UnityEngine.Object>();
        try
        {
            VerifyStagedTransactionFailsOpen(created);
            VerifySingleConsumerOwnership(created);
            VerifyStaleGenerationCannotAdvance(created);
            VerifyUnbindAbortsIncompleteAction(created);
            VerifyOrdinaryEnemyWaitSkipsPresentation(created);
            VerifyRequestFactoryBreakEligibility(created);
            VerifyLongScopeIdentity();
        }
        finally
        {
            for (int i = created.Count - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(created[i]);
        }

        return "PASS DRPG presentation transaction coordinator";
    }

    private static void VerifyStagedTransactionFailsOpen(
        ICollection<UnityEngine.Object> created)
    {
        TransactionFixture fixture = CreateFixture(
            created,
            DRPGBattleRosterRole.PartyMember,
            playerHp: 100f,
            attackPower: 20f,
            damageBrokenHpRatio: 0.9f);
        StartOnEnemyTurn(fixture);
        fixture.Coordinator.Bind(fixture.Session, 9L);
        int completedEvents = 0;
        fixture.Session.ActionCompleted += _ => completedEvents++;
        BattleParticipantRuntime actor = fixture.Session.CurrentActor;
        BattleParticipantRuntime target = fixture.Player;
        float hpBefore = target.CurrentHp;
        var command = new DRPGBattleCommand(
            actor.RuntimeId,
            target.RuntimeId,
            fixture.Skill);

        AssertTrue(
            fixture.Session.TryPrepareCommand(
                command,
                out DRPGBattlePreparedAction action,
                out string error),
            error);
        AssertEqual(hpBefore, target.CurrentHp,
            "Preparing an action must not apply damage before the windup gate");
        AssertEqual(DRPGBattlePhase.PreparingAction, fixture.Session.Phase,
            "The session must wait in PreparingAction during windup");
        AssertTrue(fixture.Coordinator.HasActiveAction,
            "The coordinator must own the staged action");

        StartedCue windup = fixture.Executor.FindLast(
            BattlePresentationMoment.ActionWindup);
        AssertEqual(9L, windup.Request.Scope.BattleGeneration,
            "Windup must retain the battle generation");
        AssertEqual(1L, windup.Request.Scope.TurnSerial,
            "The first current-actor notification must produce turn serial one");
        AssertEqual(action.ActionId, windup.Request.Scope.ActionId,
            "Windup must retain the action identity");

        windup.Handle.TrySettle(BattlePresentationOutcome.Completed);
        fixture.Director.Tick(0f);
        AssertTrue(target.CurrentHp < hpBefore,
            "Releasing windup must resolve impact exactly once");
        AssertEqual(DRPGBattlePhase.ExecutingAction, fixture.Session.Phase,
            "The session must wait in ExecutingAction during impact feedback");

        StartedCue impact = fixture.Executor.FindLast(
            BattlePresentationMoment.ImpactResolved);
        AssertTrue(impact.Request.Facts.AnyHitTriggeredDamageBroken,
            "A newly broken party member must be captured by immutable impact facts");
        impact.Handle.Cancel(BattlePresentationCancelReason.TimedOut);
        fixture.Director.Tick(0f);

        AssertEqual(0, completedEvents,
            "Impact settlement may commit domain facts but must not complete the action early");
        AssertTrue(fixture.Coordinator.HasActiveAction,
            "The coordinator must retain ownership through the settlement run");
        AssertSame(actor, fixture.Session.CurrentActor,
            "The acting participant must remain current until settlement finishes");
        AssertEqual(DRPGBattlePhase.ActionEnd, fixture.Session.Phase,
            "The committed action must wait at ActionEnd during settlement presentation");
        AssertEqual(1, fixture.Executor.Count(
                BattlePresentationMoment.ActionSettled),
            "ActionSettled must be emitted exactly once after domain commit");

        StartedCue settled = fixture.Executor.FindLast(
            BattlePresentationMoment.ActionSettled);
        settled.Handle.TrySettle(BattlePresentationOutcome.Completed);
        fixture.Director.Tick(0f);

        AssertEqual(1, completedEvents,
            "The action must complete exactly once after the settlement run finishes");
        AssertFalse(fixture.Coordinator.HasActiveAction,
            "The coordinator must release ownership before finalization rotates the turn");
        AssertFalse(ReferenceEquals(actor, fixture.Session.CurrentActor),
            "Finalization must advance the turn after visual settlement");

        impact.Handle.TrySettle(BattlePresentationOutcome.Completed);
        fixture.Director.Tick(0f);
        AssertEqual(1, completedEvents,
            "Repeated presentation settlement must not complete an action twice");
    }

    private static void VerifySingleConsumerOwnership(
        ICollection<UnityEngine.Object> created)
    {
        TransactionFixture fixture = CreateFixture(
            created,
            DRPGBattleRosterRole.PartyMember,
            100f,
            20f,
            0.35f);
        var otherConsumer = new object();
        AssertTrue(fixture.Session.TryAttachStagedActionConsumer(otherConsumer),
            "The fixture must reserve staged ownership for another consumer");

        bool threw = false;
        try
        {
            fixture.Coordinator.Bind(fixture.Session, 12L);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        AssertTrue(threw,
            "Bind must fail loudly when another staged consumer owns the session");
        AssertTrue(fixture.Session.HasStagedActionConsumer,
            "A failed bind must not detach the existing staged consumer");
        fixture.Session.DetachStagedActionConsumer(otherConsumer);
    }

    private static void VerifyStaleGenerationCannotAdvance(
        ICollection<UnityEngine.Object> created)
    {
        TransactionFixture oldFixture = CreateFixture(
            created,
            DRPGBattleRosterRole.PartyMember,
            100f,
            20f,
            0.35f);
        TransactionFixture newFixture = CreateFixture(
            created,
            DRPGBattleRosterRole.PartyMember,
            100f,
            20f,
            0.35f,
            reuseCoordinator: oldFixture.Coordinator,
            reuseDirector: oldFixture.Director,
            reuseExecutor: oldFixture.Executor);

        StartOnEnemyTurn(oldFixture);
        oldFixture.Coordinator.Bind(oldFixture.Session, 8L);
        var oldCommand = new DRPGBattleCommand(
            oldFixture.Enemy.RuntimeId,
            oldFixture.Player.RuntimeId,
            oldFixture.Skill);
        AssertTrue(oldFixture.Session.TryPrepareCommand(
                oldCommand,
                out _,
                out string oldError),
            oldError);
        StartedCue staleWindup = oldFixture.Executor.FindLast(
            BattlePresentationMoment.ActionWindup);
        float oldHp = oldFixture.Player.CurrentHp;

        oldFixture.Coordinator.Bind(newFixture.Session, 9L);
        newFixture.Session.Start();
        staleWindup.Handle.TrySettle(BattlePresentationOutcome.Completed);
        oldFixture.Director.Tick(0f);

        AssertEqual(oldHp, oldFixture.Player.CurrentHp,
            "A callback from generation eight must not resolve after generation nine binds");
        AssertEqual(DRPGBattlePhase.PreparingAction, oldFixture.Session.Phase,
            "The stale session must not be advanced by a detached coordinator");
        oldFixture.Session.Abort();
        newFixture.Session.Abort();
    }

    private static void VerifyUnbindAbortsIncompleteAction(
        ICollection<UnityEngine.Object> created)
    {
        TransactionFixture fixture = CreateFixture(
            created,
            DRPGBattleRosterRole.PartyMember,
            100f,
            20f,
            0.35f);
        StartOnEnemyTurn(fixture);
        fixture.Coordinator.Bind(fixture.Session, 14L);
        var command = new DRPGBattleCommand(
            fixture.Enemy.RuntimeId,
            fixture.Player.RuntimeId,
            fixture.Skill);
        AssertTrue(fixture.Session.TryPrepareCommand(
                command,
                out _,
                out string error),
            error);

        fixture.Coordinator.Unbind(true);
        AssertEqual(DRPGBattleOutcome.Aborted, fixture.Session.Outcome,
            "Unbinding an incomplete owned action must abort its session");
        AssertFalse(fixture.Session.HasStagedActionConsumer,
            "Unbind must release staged-action ownership");
        AssertFalse(fixture.Coordinator.HasActiveAction,
            "Unbind must clear coordinator action state");
    }

    private static void VerifyOrdinaryEnemyWaitSkipsPresentation(
        ICollection<UnityEngine.Object> created)
    {
        TransactionFixture fixture = CreateFixture(
            created,
            DRPGBattleRosterRole.PartyMember,
            100f,
            20f,
            0.35f);
        SetField(
            fixture.Skill,
            "targetFormationRows",
            DRPGFormationRowMask.None);
        fixture.Session.Start();
        AssertSame(fixture.Enemy, fixture.Session.CurrentActor,
            "The no-target enemy must still receive its scheduled turn");
        fixture.Coordinator.Bind(fixture.Session, 15L);
        AssertTrue(fixture.Session.TryCreateEnemyCommand(
                out DRPGBattleCommand command),
            "The explicit idle plan must materialize a command");
        AssertEqual(DRPGBattleCommandKind.Wait, command.Kind,
            "The no-target enemy must materialize a Wait command");

        int completedEvents = 0;
        fixture.Session.ActionCompleted += _ => completedEvents++;
        AssertTrue(fixture.Session.TryPrepareCommand(
                command,
                out _,
                out string error),
            error);

        AssertEqual(1, completedEvents,
            "A silent enemy Wait must settle synchronously");
        AssertEqual(0, fixture.Executor.Started.Count,
            "A silent enemy Wait must not create windup, impact, or frame cues");
        AssertFalse(fixture.Coordinator.HasActiveAction,
            "A silent enemy Wait must not retain presentation ownership");
        AssertTrue(fixture.Session.IsActive,
            "A silent enemy Wait must return the battle to the scheduler");
    }

    private static void VerifyRequestFactoryBreakEligibility(
        ICollection<UnityEngine.Object> created)
    {
        TransactionFixture partyFixture = CreateFixture(
            created,
            DRPGBattleRosterRole.PartyMember,
            100f,
            20f,
            0.9f);
        StartOnEnemyTurn(partyFixture);
        DRPGBattleActionResolution partyResolution = ResolveOneHit(partyFixture);
        var factory = new BattlePresentationRequestFactory();
        BattlePresentationRequest partyRequest = factory.CreateImpact(
            101L,
            20L,
            30L,
            partyResolution,
            partyFixture.Session);
        AssertEqual(1, partyRequest.Facts.DamageBrokenCutInTargetIds.Count,
            "Only a broken ordinary party member must enter the large break cut-in list");

        TransactionFixture protagonistFixture = CreateFixture(
            created,
            DRPGBattleRosterRole.Protagonist,
            100f,
            20f,
            0.9f);
        StartOnEnemyTurn(protagonistFixture);
        DRPGBattleActionResolution protagonistResolution =
            ResolveOneHit(protagonistFixture);
        BattlePresentationRequest protagonistRequest = factory.CreateImpact(
            102L,
            20L,
            31L,
            protagonistResolution,
            protagonistFixture.Session);
        AssertEqual(0, protagonistRequest.Facts.DamageBrokenCutInTargetIds.Count,
            "The protagonist can break but must not trigger the large break cut-in");
    }

    private static void VerifyLongScopeIdentity()
    {
        const long generation = (long)int.MaxValue + 9L;
        const long turn = (long)int.MaxValue + 17L;
        var scope = new BattlePresentationScope(generation, turn, 3L);
        AssertEqual(generation, scope.BattleGeneration,
            "Battle generation must not truncate to Int32");
        AssertEqual(turn, scope.TurnSerial,
            "Turn serial must not truncate to Int32");
    }

    private static DRPGBattleActionResolution ResolveOneHit(
        TransactionFixture fixture)
    {
        var command = new DRPGBattleCommand(
            fixture.Enemy.RuntimeId,
            fixture.Player.RuntimeId,
            fixture.Skill);
        AssertTrue(fixture.Session.TryPrepareCommand(
                command,
                out DRPGBattlePreparedAction action,
                out string prepareError),
            prepareError);
        AssertTrue(fixture.Session.TryResolveImpact(
                action,
                out DRPGBattleActionResolution resolution,
                out string resolveError),
            resolveError);
        return resolution;
    }

    private static void StartOnEnemyTurn(TransactionFixture fixture)
    {
        fixture.Session.Start();

        // Ordinary enemy attacks now stop at warning zero and guarantee one
        // player response. This presentation probe is specifically about an
        // enemy-owned staged transaction, so consume that response without a
        // presentation owner before binding the coordinator.
        if (fixture.Session.CurrentActor != null &&
            fixture.Session.CurrentActor.Side == DRPGBattleSide.Player)
        {
            AssertTrue(
                fixture.Session.TrySubmitCommand(
                    DRPGBattleCommand.Wait(
                        fixture.Session.CurrentActor.RuntimeId),
                    out string waitError),
                waitError);
        }

        AssertSame(fixture.Enemy, fixture.Session.CurrentActor,
            "The fixture must reach the committed warning-zero enemy turn");
    }

    private static TransactionFixture CreateFixture(
        ICollection<UnityEngine.Object> created,
        DRPGBattleRosterRole playerRole,
        float playerHp,
        float attackPower,
        float damageBrokenHpRatio,
        DRPGBattleActionTransactionCoordinator reuseCoordinator = null,
        BattlePresentationDirector reuseDirector = null,
        FakeCueExecutor reuseExecutor = null)
    {
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        created.Add(skill);
        SetField(skill, "skillId", "probe_attack");
        SetField(skill, "power", attackPower);
        SetField(skill, "recoveryDelay", 10000f);

        DRPGCombatantProfileSO playerProfile = CreateProfile(
            created,
            "Probe Player",
            playerHp,
            10f,
            damageBrokenHpRatio,
            skill);
        DRPGCombatantProfileSO enemyProfile = CreateProfile(
            created,
            "Probe Enemy",
            999f,
            100f,
            0.35f,
            skill);
        var player = new BattleParticipantRuntime(
            1,
            new BattleParticipantSpec(
                playerProfile,
                DRPGBattleSide.Player,
                playerRole,
                0));
        var enemy = new BattleParticipantRuntime(
            2,
            new BattleParticipantSpec(
                enemyProfile,
                DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy,
                0));
        var session = DRPGBattleHeadlessSessionFactory.Create(new[] { player, enemy });

        FakeCueExecutor executor = reuseExecutor ?? new FakeCueExecutor();
        BattlePresentationDirector director = reuseDirector ??
            CreateDirector(created, executor);
        DRPGBattleActionTransactionCoordinator coordinator = reuseCoordinator;
        if (coordinator == null)
        {
            var host = new GameObject("TransactionCoordinatorProbe");
            created.Add(host);
            coordinator = host.AddComponent<DRPGBattleActionTransactionCoordinator>();
            coordinator.ConfigureDirector(director);
        }

        return new TransactionFixture(
            session,
            player,
            enemy,
            skill,
            coordinator,
            director,
            executor);
    }

    private static DRPGCombatantProfileSO CreateProfile(
        ICollection<UnityEngine.Object> created,
        string displayName,
        float maxHp,
        float agility,
        float damageBrokenHpRatio,
        DRPGSkillSO skill)
    {
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        created.Add(profile);
        SetField(profile, "displayName", displayName);
        SetField(profile, "maxHp", maxHp);
        SetField(profile, "agility", agility);
        SetField(profile, "damageBrokenHpRatio", damageBrokenHpRatio);
        SetField(profile, "skills", new List<DRPGSkillSO> { skill });
        return profile;
    }

    private static BattlePresentationDirector CreateDirector(
        ICollection<UnityEngine.Object> created,
        FakeCueExecutor executor)
    {
        BattlePresentationRecipeSetSO set =
            ScriptableObject.CreateInstance<BattlePresentationRecipeSetSO>();
        created.Add(set);
        var recipes = new List<BattlePresentationRecipeSO>
        {
            CreateRecipe(created, BattlePresentationMoment.ActionWindup,
                BattlePresentationGateKind.ActionFlow),
            CreateRecipe(created, BattlePresentationMoment.ImpactResolved,
                BattlePresentationGateKind.ActionFlow),
            CreateRecipe(created, BattlePresentationMoment.ActionSettled,
                BattlePresentationGateKind.None)
        };
        SetField(set, "baseRecipes", recipes);
        return new BattlePresentationDirector(
            new BattlePresentationRecipeResolver(),
            set,
            new[] { executor },
            new BattleInputGate(),
            new BattlePresentationTraceBuffer(64));
    }

    private static BattlePresentationRecipeSO CreateRecipe(
        ICollection<UnityEngine.Object> created,
        BattlePresentationMoment moment,
        BattlePresentationGateKind gateKind)
    {
        TestCueSO cue = ScriptableObject.CreateInstance<TestCueSO>();
        created.Add(cue);
        SetField(cue, "cueId", moment.ToString());
        SetField(cue, "kind", BattlePresentationCueKind.ScreenFlash);

        var entry = new BattlePresentationCueEntry();
        SetField(entry, "cue", cue);
        SetField(entry, "condition", BattlePresentationCondition.Always);
        SetField(entry, "lane", BattlePresentationLaneKind.None);
        SetField(entry, "conflictPolicy", BattlePresentationConflictPolicy.Blend);
        SetField(entry, "blocksBeat", true);

        var beat = new BattlePresentationBeat();
        SetField(beat, "beatId", moment + "_beat");
        SetField(beat, "releasesGate",
            gateKind != BattlePresentationGateKind.None);
        SetField(beat, "cues", new List<BattlePresentationCueEntry> { entry });

        BattlePresentationRecipeSO recipe =
            ScriptableObject.CreateInstance<BattlePresentationRecipeSO>();
        created.Add(recipe);
        SetField(recipe, "recipeId", moment + "_recipe");
        SetField(recipe, "moment", moment);
        SetField(recipe, "gateKind", gateKind);
        SetField(recipe, "maximumGateSeconds", 5f);
        SetField(recipe, "beats", new List<BattlePresentationBeat> { beat });
        return recipe;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        Type type = target.GetType();
        while (type != null)
        {
            FieldInfo field = type.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }
            type = type.BaseType;
        }
        throw new MissingFieldException(target.GetType().Name, fieldName);
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
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception(
                $"{message}. Expected={expected}, Actual={actual}");
        }
    }

    private static void AssertSame<T>(T expected, T actual, string message)
        where T : class
    {
        if (!ReferenceEquals(expected, actual))
        {
            throw new Exception(
                $"{message}. Expected={expected}, Actual={actual}");
        }
    }

    private sealed class TestCueSO : BattlePresentationCueSO
    {
    }

    private sealed class FakeCueExecutor : IBattlePresentationCueExecutor
    {
        public BattlePresentationCueKind Kind =>
            BattlePresentationCueKind.ScreenFlash;
        public List<StartedCue> Started { get; } = new List<StartedCue>();

        public IBattlePresentationHandle Play(
            BattlePresentationCueSO cue,
            BattlePresentationRequest request)
        {
            var handle = new BattlePresentationHandle();
            Started.Add(new StartedCue(request, handle));
            return handle;
        }

        public StartedCue FindLast(BattlePresentationMoment moment)
        {
            for (int i = Started.Count - 1; i >= 0; i--)
            {
                if (Started[i].Request.Moment == moment)
                    return Started[i];
            }
            throw new Exception("No cue started for " + moment);
        }

        public int Count(BattlePresentationMoment moment)
        {
            int count = 0;
            for (int i = 0; i < Started.Count; i++)
            {
                if (Started[i].Request.Moment == moment)
                    count++;
            }
            return count;
        }
    }

    private sealed class StartedCue
    {
        public StartedCue(
            BattlePresentationRequest request,
            BattlePresentationHandle handle)
        {
            Request = request;
            Handle = handle;
        }

        public BattlePresentationRequest Request { get; }
        public BattlePresentationHandle Handle { get; }
    }

    private readonly struct TransactionFixture
    {
        public TransactionFixture(
            DRPGBattleSession session,
            BattleParticipantRuntime player,
            BattleParticipantRuntime enemy,
            DRPGSkillSO skill,
            DRPGBattleActionTransactionCoordinator coordinator,
            BattlePresentationDirector director,
            FakeCueExecutor executor)
        {
            Session = session;
            Player = player;
            Enemy = enemy;
            Skill = skill;
            Coordinator = coordinator;
            Director = director;
            Executor = executor;
        }

        public DRPGBattleSession Session { get; }
        public BattleParticipantRuntime Player { get; }
        public BattleParticipantRuntime Enemy { get; }
        public DRPGSkillSO Skill { get; }
        public DRPGBattleActionTransactionCoordinator Coordinator { get; }
        public BattlePresentationDirector Director { get; }
        public FakeCueExecutor Executor { get; }
    }
}
#endif
