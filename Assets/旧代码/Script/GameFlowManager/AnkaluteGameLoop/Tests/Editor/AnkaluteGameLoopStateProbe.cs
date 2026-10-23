#if UNITY_EDITOR
using System;

public static class AnkaluteGameLoopStateProbe
{
    public static string RunHappyPath()
    {
        AssertNullHubDependency();

        var initial = new AnkaluteGameLoopState(new StrategicHubFlow());
        AssertEqual(AnkaluteGameLoopPhase.Title, initial.Current.Phase,
            "Game loop must start at Title");
        AssertFalse(initial.Current.HasExpedition,
            "Title must not carry an Expedition context");
        AssertEqual(0L, initial.Current.Revision,
            "Initial game-loop revision must be zero");
        AssertFalse(initial.Current.IsTransitioning,
            "Published top Snapshot must be stable");

        AssertEntryReasonMapping(
            SessionEntryReason.NewGame,
            StrategicHubEntryReason.NewGame);
        AssertEntryReasonMapping(
            SessionEntryReason.LoadedGame,
            StrategicHubEntryReason.LoadedGame);
        AssertEntryReasonMapping(
            SessionEntryReason.ReenterAfterTitle,
            StrategicHubEntryReason.ReenterAfterTitle);

        var hub = new StrategicHubFlow();
        var loop = new AnkaluteGameLoopState(hub);
        int topEvents = 0;
        loop.Transitioned += transition =>
        {
            topEvents++;
            AssertEqual(transition.Before.Revision + 1L,
                transition.After.Revision,
                "Every Applied top transition must increment revision once");
            AssertSame(transition.After, loop.Current,
                "Top Current must commit before event delivery");
            AssertFalse(transition.After.IsTransitioning,
                "Published top Snapshot must be stable");
        };

        ApplyAndAssertCurrent(loop,
            AnkaluteGameLoopIntent.EnterStrategicHub(
                SessionEntryReason.NewGame));
        AssertEqual(AnkaluteGameLoopPhase.StrategicHub,
            loop.Current.Phase,
            "New game must enter StrategicHub before an Expedition");
        AssertEqual(StrategicHubRoute.Home, hub.Current.Route,
            "Entering StrategicHub must reset HubHome");

        hub.Request(StrategicHubIntent.OpenDestination(
            StrategicHubRoute.DungeonSelection));
        ExpeditionStartRequest expedition = hub.Request(
            StrategicHubIntent.ConfirmExpedition(
                new ExpeditionStartRequest(
                    new ExpeditionId("dungeon-a"))))
            .ExpeditionRequest;
        ApplyAndAssertCurrent(loop,
            AnkaluteGameLoopIntent.LaunchExpedition(expedition));
        AssertEqual(AnkaluteGameLoopPhase.ExpeditionExploration,
            loop.Current.Phase,
            "Launch must enter expedition exploration");
        AssertTrue(loop.Current.HasExpedition,
            "Launch must establish an Expedition context");
        AssertEqual(new ExpeditionId("dungeon-a"),
            loop.Current.ExpeditionId,
            "Launch must retain the active ExpeditionId");
        AssertEqual(2, topEvents,
            "Hub entry and launch must publish two top transitions");

        ApplyAndAssertCurrent(loop,
            AnkaluteGameLoopIntent.BeginBattle(
                new EncounterStartRequest(
                    new EncounterId("encounter-a"))));
        AssertEqual(AnkaluteGameLoopPhase.Battle, loop.Current.Phase,
            "Encounter must enter Battle");
        AssertEqual(new ExpeditionId("dungeon-a"),
            loop.Current.ExpeditionId,
            "Beginning battle must preserve the ExpeditionId");
        AssertEqual(3, topEvents,
            "Beginning battle must publish one top transition");

        ApplyAndAssertCurrent(loop,
            AnkaluteGameLoopIntent.ResolveBattle(
                BattleResolution.Victory));
        AssertEqual(AnkaluteGameLoopPhase.BattleSettlement,
            loop.Current.Phase,
            "Victory must enter BattleSettlement");
        AssertEqual(new ExpeditionId("dungeon-a"),
            loop.Current.ExpeditionId,
            "Victory settlement must preserve the ExpeditionId");
        AssertEqual(4, topEvents,
            "Victory must publish one top transition");

        ApplyAndAssertCurrent(loop,
            AnkaluteGameLoopIntent.CompleteBattleSettlement());
        AssertEqual(AnkaluteGameLoopPhase.ExpeditionExploration,
            loop.Current.Phase,
            "Settlement completion must resume exploration");
        AssertEqual(new ExpeditionId("dungeon-a"),
            loop.Current.ExpeditionId,
            "Ordinary battle settlement must preserve ExpeditionId");
        AssertEqual(5, topEvents,
            "Settlement completion must publish one top transition");

        ApplyAndAssertCurrent(loop,
            AnkaluteGameLoopIntent.BeginBattle(
                new EncounterStartRequest(
                    new EncounterId("encounter-b"))));
        ApplyAndAssertCurrent(loop,
            AnkaluteGameLoopIntent.ResolveBattle(
                BattleResolution.Escaped));
        AssertEqual(AnkaluteGameLoopPhase.ExpeditionExploration,
            loop.Current.Phase,
            "Escape must resume the same Expedition");
        AssertEqual(new ExpeditionId("dungeon-a"),
            loop.Current.ExpeditionId,
            "Escape must preserve the ExpeditionId");
        AssertEqual(7, topEvents,
            "Second battle begin and escape must add two transitions");

        ApplyAndAssertCurrent(loop,
            AnkaluteGameLoopIntent.EndExpedition(
                ExpeditionExitReason.Completed));
        AssertEqual(AnkaluteGameLoopPhase.StrategicHub,
            loop.Current.Phase,
            "Completed Expedition must return to StrategicHub");
        AssertFalse(loop.Current.HasExpedition,
            "Returning to Hub must clear the active ExpeditionId");
        AssertEqual(default(ExpeditionId), loop.Current.ExpeditionId,
            "Returning to Hub must clear the ExpeditionId value");
        AssertEqual(StrategicHubRoute.Home, hub.Current.Route,
            "Completed Expedition must reset HubHome");
        AssertEqual(0, hub.Current.History.Count,
            "Completed Expedition must clear stale Hub history");
        AssertEqual(8L, loop.Current.Revision,
            "Happy path must commit exactly eight top transitions");
        AssertEqual(8, topEvents,
            "Happy path must publish exactly one event per transition");

        return "PASS ANKALUTE Title-Hub-Expedition-Battle happy path";
    }

    public static string RunTerminalReturns()
    {
        var retreatHub = new StrategicHubFlow();
        var retreatLoop = new AnkaluteGameLoopState(retreatHub);
        retreatLoop.Request(
            AnkaluteGameLoopIntent.EnterStrategicHub(
                SessionEntryReason.NewGame));
        retreatHub.Request(StrategicHubIntent.OpenDestination(
            StrategicHubRoute.DungeonSelection));
        retreatLoop.Request(
            AnkaluteGameLoopIntent.LaunchExpedition(
                new ExpeditionStartRequest(
                    new ExpeditionId("dungeon-retreat"))));
        ApplyAndAssertSingleTransition(retreatLoop,
            AnkaluteGameLoopIntent.EndExpedition(
                ExpeditionExitReason.Retreated));
        AssertEqual(AnkaluteGameLoopPhase.StrategicHub,
            retreatLoop.Current.Phase,
            "Retreat must return to Hub");
        AssertFalse(retreatLoop.Current.HasExpedition,
            "Retreat must clear ExpeditionId");
        AssertEqual(StrategicHubRoute.Home,
            retreatHub.Current.Route,
            "Retreat must reset HubHome");

        var defeatHub = new StrategicHubFlow();
        var defeatLoop = new AnkaluteGameLoopState(defeatHub);
        defeatLoop.Request(
            AnkaluteGameLoopIntent.EnterStrategicHub(
                SessionEntryReason.NewGame));
        defeatHub.Request(StrategicHubIntent.OpenDestination(
            StrategicHubRoute.DungeonSelection));
        defeatLoop.Request(
            AnkaluteGameLoopIntent.LaunchExpedition(
                new ExpeditionStartRequest(
                    new ExpeditionId("dungeon-defeat"))));
        defeatLoop.Request(
            AnkaluteGameLoopIntent.BeginBattle(
                new EncounterStartRequest(
                    new EncounterId("encounter-defeat"))));
        ApplyAndAssertSingleTransition(defeatLoop,
            AnkaluteGameLoopIntent.ResolveBattle(
                BattleResolution.PartyDefeated));
        AssertEqual(AnkaluteGameLoopPhase.StrategicHub,
            defeatLoop.Current.Phase,
            "Party defeat must return to Hub");
        AssertFalse(defeatLoop.Current.HasExpedition,
            "Party defeat must clear ExpeditionId");
        AssertEqual(StrategicHubRoute.Home,
            defeatHub.Current.Route,
            "Party defeat must reset HubHome");

        var abortedLoop = new AnkaluteGameLoopState(
            new StrategicHubFlow());
        abortedLoop.Request(
            AnkaluteGameLoopIntent.EnterStrategicHub(
                SessionEntryReason.NewGame));
        abortedLoop.Request(
            AnkaluteGameLoopIntent.LaunchExpedition(
                new ExpeditionStartRequest(
                    new ExpeditionId("dungeon-aborted"))));
        ExpeditionId expeditionBeforeAbort =
            abortedLoop.Current.ExpeditionId;
        abortedLoop.Request(
            AnkaluteGameLoopIntent.BeginBattle(
                new EncounterStartRequest(
                    new EncounterId("encounter-aborted"))));
        ApplyAndAssertSingleTransition(abortedLoop,
            AnkaluteGameLoopIntent.ResolveBattle(
                BattleResolution.Aborted));
        AssertEqual(AnkaluteGameLoopPhase.ExpeditionExploration,
            abortedLoop.Current.Phase,
            "Aborted battle must resume ExpeditionExploration");
        AssertEqual(new ExpeditionId("dungeon-aborted"),
            expeditionBeforeAbort,
            "Fixture must capture the intended ExpeditionId");
        AssertEqual(expeditionBeforeAbort,
            abortedLoop.Current.ExpeditionId,
            "Aborted battle must preserve the same ExpeditionId");

        return "PASS ANKALUTE terminal returns";
    }

    public static string RunReturnToTitleAndValidation()
    {
        var invalidLoop = new AnkaluteGameLoopState(
            new StrategicHubFlow());
        int invalidEvents = 0;
        invalidLoop.Transitioned += _ => invalidEvents++;
        AnkaluteGameLoopSnapshot initial = invalidLoop.Current;
        AnkaluteGameLoopRequestResult defaultIntentResult =
            invalidLoop.Request(default(AnkaluteGameLoopIntent));
        AssertRejected(AnkaluteGameLoopRequestError.InvalidIntent,
            defaultIntentResult);
        AssertSame(initial, defaultIntentResult.Current,
            "Rejected result.Current must preserve Snapshot identity");
        AssertSame(initial, invalidLoop.Current,
            "Default intent must not replace Snapshot");
        AssertRejected(AnkaluteGameLoopRequestError.InvalidPayload,
            invalidLoop.Request(
                AnkaluteGameLoopIntent.EnterStrategicHub(
                    SessionEntryReason.Invalid)));
        AssertRejected(AnkaluteGameLoopRequestError.InvalidPayload,
            invalidLoop.Request(
                AnkaluteGameLoopIntent.EnterStrategicHub(
                    (SessionEntryReason)999)));
        AssertSame(initial, invalidLoop.Current,
            "Invalid session reasons must not replace Snapshot");
        AssertRejected(AnkaluteGameLoopRequestError.InvalidPhase,
            invalidLoop.Request(AnkaluteGameLoopIntent.BeginBattle(
                new EncounterStartRequest(
                    new EncounterId("encounter-a")))));
        AssertSame(initial, invalidLoop.Current,
            "Wrong-phase battle must not replace Snapshot");
        AssertEqual(0, invalidEvents,
            "Rejected requests must not publish transitions");

        AnkaluteGameLoopRequestResult titleNoOp = invalidLoop.Request(
            AnkaluteGameLoopIntent.ReturnToTitle());
        AssertEqual(AnkaluteGameLoopRequestStatus.NoOp,
            titleNoOp.Status,
            "ReturnToTitle at Title must be NoOp");
        AssertEqual(AnkaluteGameLoopRequestError.None,
            titleNoOp.Error,
            "NoOp must not carry a rejection reason");
        AssertSame(initial, titleNoOp.Current,
            "NoOp result.Current must preserve Snapshot identity");
        AssertSame(initial, invalidLoop.Current,
            "NoOp must preserve the exact Snapshot");
        AssertEqual(0, invalidEvents,
            "NoOp must not publish a top Transition");

        var invalidPayloadLoop = new AnkaluteGameLoopState(
            new StrategicHubFlow());
        AssertApplied(invalidPayloadLoop.Request(
            AnkaluteGameLoopIntent.EnterStrategicHub(
                SessionEntryReason.NewGame)));
        int invalidPayloadEvents = 0;
        invalidPayloadLoop.Transitioned += _ => invalidPayloadEvents++;
        AnkaluteGameLoopSnapshot hubBeforeInvalidPayload =
            invalidPayloadLoop.Current;
        AssertRejected(AnkaluteGameLoopRequestError.InvalidPayload,
            invalidPayloadLoop.Request(
                AnkaluteGameLoopIntent.LaunchExpedition(
                    new ExpeditionStartRequest(
                        new ExpeditionId(string.Empty)))));
        AssertSame(hubBeforeInvalidPayload, invalidPayloadLoop.Current,
            "Invalid ExpeditionId must not replace the Hub Snapshot");
        AssertEqual(0, invalidPayloadEvents,
            "Invalid ExpeditionId must not publish a transition");

        AnkaluteGameLoopSnapshot hubBeforeWrongEntry =
            invalidPayloadLoop.Current;
        AssertRejected(AnkaluteGameLoopRequestError.InvalidPhase,
            invalidPayloadLoop.Request(
                AnkaluteGameLoopIntent.EnterStrategicHub(
                    SessionEntryReason.NewGame)));
        AssertSame(hubBeforeWrongEntry, invalidPayloadLoop.Current,
            "Known EnterStrategicHub in the wrong phase must not mutate");

        ApplyAndAssertSingleTransition(invalidPayloadLoop,
            AnkaluteGameLoopIntent.ReturnToTitle());
        AssertEqual(AnkaluteGameLoopPhase.Title,
            invalidPayloadLoop.Current.Phase,
            "ReturnToTitle from StrategicHub must enter Title");

        AnkaluteGameLoopPhase[] blockedReturnPhases =
        {
            AnkaluteGameLoopPhase.ExpeditionExploration,
            AnkaluteGameLoopPhase.Battle,
            AnkaluteGameLoopPhase.BattleSettlement
        };
        foreach (AnkaluteGameLoopPhase blockedPhase in blockedReturnPhases)
        {
            AnkaluteGameLoopState blockedReturnLoop =
                CreateLoopAtPhase(blockedPhase);
            AnkaluteGameLoopSnapshot beforeReturn =
                blockedReturnLoop.Current;
            AssertRejected(AnkaluteGameLoopRequestError.InvalidPhase,
                blockedReturnLoop.Request(
                    AnkaluteGameLoopIntent.ReturnToTitle()));
            AssertSame(beforeReturn, blockedReturnLoop.Current,
                "ReturnToTitle must not abandon phase " + blockedPhase);
        }

        var invalidEncounterLoop = CreateLoopAtPhase(
            AnkaluteGameLoopPhase.ExpeditionExploration);
        AnkaluteGameLoopSnapshot beforeInvalidEncounter =
            invalidEncounterLoop.Current;
        AssertRejected(AnkaluteGameLoopRequestError.InvalidPayload,
            invalidEncounterLoop.Request(
                AnkaluteGameLoopIntent.BeginBattle(
                    new EncounterStartRequest(
                        new EncounterId(string.Empty)))));
        AssertSame(beforeInvalidEncounter, invalidEncounterLoop.Current,
            "Blank EncounterId must not replace Snapshot");

        var invalidResolutionLoop = CreateLoopAtPhase(
            AnkaluteGameLoopPhase.Battle);
        AnkaluteGameLoopSnapshot beforeInvalidResolution =
            invalidResolutionLoop.Current;
        AssertRejected(AnkaluteGameLoopRequestError.InvalidPayload,
            invalidResolutionLoop.Request(
                AnkaluteGameLoopIntent.ResolveBattle(
                    BattleResolution.Invalid)));
        AssertRejected(AnkaluteGameLoopRequestError.InvalidPayload,
            invalidResolutionLoop.Request(
                AnkaluteGameLoopIntent.ResolveBattle(
                    (BattleResolution)999)));
        AssertSame(beforeInvalidResolution,
            invalidResolutionLoop.Current,
            "Invalid battle resolution must not replace Snapshot");

        var invalidExitLoop = CreateLoopAtPhase(
            AnkaluteGameLoopPhase.ExpeditionExploration);
        AnkaluteGameLoopSnapshot beforeInvalidExit =
            invalidExitLoop.Current;
        AssertRejected(AnkaluteGameLoopRequestError.InvalidPayload,
            invalidExitLoop.Request(
                AnkaluteGameLoopIntent.EndExpedition(
                    ExpeditionExitReason.Invalid)));
        AssertRejected(AnkaluteGameLoopRequestError.InvalidPayload,
            invalidExitLoop.Request(
                AnkaluteGameLoopIntent.EndExpedition(
                    (ExpeditionExitReason)999)));
        AssertSame(beforeInvalidExit, invalidExitLoop.Current,
            "Invalid ExpeditionExitReason must not replace Snapshot");

        return "PASS ANKALUTE title return and validation";
    }

    public static string RunEventRecovery()
    {
        var reentrant = new AnkaluteGameLoopState(
            new StrategicHubFlow());
        AnkaluteGameLoopRequestResult nested =
            default(AnkaluteGameLoopRequestResult);
        bool attempted = false;
        int outerEvents = 0;
        reentrant.Transitioned += transition =>
        {
            outerEvents++;
            AssertSame(transition.After, reentrant.Current,
                "Top Current must be committed before Transitioned fires");
            AssertEqual(1L, reentrant.Current.Revision,
                "Handler must observe the committed revision");
            AssertFalse(reentrant.Current.IsTransitioning,
                "Published top Snapshot must remain a stable state");
            if (attempted)
                return;
            attempted = true;
            nested = reentrant.Request(
                AnkaluteGameLoopIntent.ReturnToTitle());
        };
        AssertApplied(reentrant.Request(
            AnkaluteGameLoopIntent.EnterStrategicHub(
                SessionEntryReason.NewGame)));
        AssertRejected(AnkaluteGameLoopRequestError.Busy, nested);
        AssertSame(reentrant.Current, nested.Current,
            "Busy result.Current must be the committed outer Snapshot");
        AssertEqual(1, outerEvents,
            "Nested request must not publish another transition");
        AssertEqual(AnkaluteGameLoopPhase.StrategicHub,
            reentrant.Current.Phase,
            "Nested request must not overwrite the outer phase");

        var exceptionLoop = new AnkaluteGameLoopState(
            new StrategicHubFlow());
        Action<AnkaluteGameLoopTransition> throwing = _ =>
            throw new InvalidOperationException(
                "top-probe-subscriber-failure");
        exceptionLoop.Transitioned += throwing;
        try
        {
            exceptionLoop.Request(
                AnkaluteGameLoopIntent.EnterStrategicHub(
                    SessionEntryReason.NewGame));
            throw new Exception(
                "Expected top subscriber failure was not exposed");
        }
        catch (InvalidOperationException error)
        {
            AssertEqual("top-probe-subscriber-failure", error.Message,
                "The exact subscriber failure must remain diagnosable");
        }
        exceptionLoop.Transitioned -= throwing;
        AssertEqual(AnkaluteGameLoopPhase.StrategicHub,
            exceptionLoop.Current.Phase,
            "Subscriber failure must not roll back the phase");
        AssertApplied(exceptionLoop.Request(
            AnkaluteGameLoopIntent.ReturnToTitle()));

        var hubExceptionFlow = new StrategicHubFlow();
        hubExceptionFlow.Request(
            StrategicHubIntent.OpenDestination(
                StrategicHubRoute.CityMap("hub-exception-city")));
        var hubExceptionLoop = new AnkaluteGameLoopState(
            hubExceptionFlow);
        Action<StrategicHubTransition> hubThrowing = _ =>
            throw new InvalidOperationException(
                "hub-reset-subscriber-failure");
        hubExceptionFlow.Transitioned += hubThrowing;
        try
        {
            hubExceptionLoop.Request(
                AnkaluteGameLoopIntent.EnterStrategicHub(
                    SessionEntryReason.NewGame));
            throw new Exception("Expected Hub reset subscriber failure");
        }
        catch (InvalidOperationException error)
        {
            AssertEqual("hub-reset-subscriber-failure", error.Message,
                "Hub reset exception must remain diagnosable");
        }
        AssertEqual(StrategicHubRoute.Home,
            hubExceptionFlow.Current.Route,
            "Hub reset commits before its subscriber exception");
        AssertEqual(AnkaluteGameLoopPhase.Title,
            hubExceptionLoop.Current.Phase,
            "Top phase must not commit when Hub coordination throws");
        hubExceptionFlow.Transitioned -= hubThrowing;
        AssertApplied(hubExceptionLoop.Request(
            AnkaluteGameLoopIntent.EnterStrategicHub(
                SessionEntryReason.NewGame)));
        AssertEqual(AnkaluteGameLoopPhase.StrategicHub,
            hubExceptionLoop.Current.Phase,
            "Retry after committed Hub reset NoOp must enter StrategicHub");

        return "PASS ANKALUTE top event recovery";
    }

    public static string RunHubResetRejection()
    {
        var busyResetLoop = new AnkaluteGameLoopState(
            new RejectingStrategicHubFlow(
                StrategicHubRequestError.Busy));
        AnkaluteGameLoopSnapshot busyResetBefore =
            busyResetLoop.Current;
        AssertRejected(AnkaluteGameLoopRequestError.Busy,
            busyResetLoop.Request(
                AnkaluteGameLoopIntent.EnterStrategicHub(
                    SessionEntryReason.NewGame)));
        AssertSame(busyResetBefore, busyResetLoop.Current,
            "Busy Hub reset must not commit the top phase");

        var rejectedResetLoop = new AnkaluteGameLoopState(
            new RejectingStrategicHubFlow(
                StrategicHubRequestError.InvalidPayload));
        AnkaluteGameLoopSnapshot rejectedResetBefore =
            rejectedResetLoop.Current;
        AssertRejected(
            AnkaluteGameLoopRequestError.StrategicHubRejected,
            rejectedResetLoop.Request(
                AnkaluteGameLoopIntent.EnterStrategicHub(
                    SessionEntryReason.NewGame)));
        AssertSame(rejectedResetBefore, rejectedResetLoop.Current,
            "Rejected Hub reset must not commit the top phase");

        var retreatResetHub = new ToggleRejectingStrategicHubFlow(
            StrategicHubRequestError.Busy);
        var retreatResetLoop = new AnkaluteGameLoopState(
            retreatResetHub);
        retreatResetLoop.Request(
            AnkaluteGameLoopIntent.EnterStrategicHub(
                SessionEntryReason.NewGame));
        retreatResetLoop.Request(
            AnkaluteGameLoopIntent.LaunchExpedition(
                new ExpeditionStartRequest(
                    new ExpeditionId("dungeon-reset-rejected"))));
        retreatResetHub.RejectReset = true;
        AnkaluteGameLoopSnapshot beforeRejectedRetreat =
            retreatResetLoop.Current;
        int rejectedRetreatEvents = 0;
        retreatResetLoop.Transitioned += _ => rejectedRetreatEvents++;
        AnkaluteGameLoopRequestResult rejectedRetreat =
            retreatResetLoop.Request(
                AnkaluteGameLoopIntent.EndExpedition(
                    ExpeditionExitReason.Retreated));
        AssertRejected(AnkaluteGameLoopRequestError.Busy,
            rejectedRetreat);
        AssertSame(beforeRejectedRetreat, retreatResetLoop.Current,
            "Rejected retreat reset must preserve exploration Snapshot");
        AssertSame(beforeRejectedRetreat, rejectedRetreat.Current,
            "Rejected retreat result.Current must preserve Snapshot identity");
        AssertTrue(retreatResetLoop.Current.HasExpedition,
            "Rejected retreat reset must retain Expedition context");
        AssertEqual(0, rejectedRetreatEvents,
            "Rejected retreat reset must not publish top Transition");

        var defeatResetHub = new ToggleRejectingStrategicHubFlow(
            StrategicHubRequestError.InvalidPayload);
        var defeatResetLoop = new AnkaluteGameLoopState(
            defeatResetHub);
        defeatResetLoop.Request(
            AnkaluteGameLoopIntent.EnterStrategicHub(
                SessionEntryReason.NewGame));
        defeatResetLoop.Request(
            AnkaluteGameLoopIntent.LaunchExpedition(
                new ExpeditionStartRequest(
                    new ExpeditionId(
                        "dungeon-defeat-reset-rejected"))));
        defeatResetLoop.Request(
            AnkaluteGameLoopIntent.BeginBattle(
                new EncounterStartRequest(
                    new EncounterId(
                        "encounter-defeat-reset-rejected"))));
        defeatResetHub.RejectReset = true;
        AnkaluteGameLoopSnapshot beforeRejectedDefeat =
            defeatResetLoop.Current;
        int rejectedDefeatEvents = 0;
        defeatResetLoop.Transitioned += _ => rejectedDefeatEvents++;
        AnkaluteGameLoopRequestResult rejectedDefeat =
            defeatResetLoop.Request(
                AnkaluteGameLoopIntent.ResolveBattle(
                    BattleResolution.PartyDefeated));
        AssertRejected(
            AnkaluteGameLoopRequestError.StrategicHubRejected,
            rejectedDefeat);
        AssertSame(beforeRejectedDefeat, defeatResetLoop.Current,
            "Rejected defeat reset must preserve Battle Snapshot");
        AssertSame(beforeRejectedDefeat, rejectedDefeat.Current,
            "Rejected defeat result.Current must preserve Snapshot identity");
        AssertTrue(defeatResetLoop.Current.HasExpedition,
            "Rejected defeat reset must retain Expedition context");
        AssertEqual(0, rejectedDefeatEvents,
            "Rejected defeat reset must not publish top Transition");

        return "PASS ANKALUTE Hub reset rejection";
    }

    private sealed class RejectingStrategicHubFlow : IStrategicHubFlow
    {
        private readonly StrategicHubFlow observable =
            new StrategicHubFlow();
        private readonly StrategicHubRequestError error;

        public RejectingStrategicHubFlow(
            StrategicHubRequestError error)
        {
            this.error = error;
        }

        public StrategicHubSnapshot Current => observable.Current;
        public event Action<StrategicHubTransition> Transitioned
        {
            add { }
            remove { }
        }

        public StrategicHubRequestResult Request(
            StrategicHubIntent intent) =>
            StrategicHubRequestResult.Reject(error, Current);
    }

    private sealed class ToggleRejectingStrategicHubFlow :
        IStrategicHubFlow
    {
        private readonly StrategicHubFlow inner =
            new StrategicHubFlow();
        private readonly StrategicHubRequestError rejection;

        public ToggleRejectingStrategicHubFlow(
            StrategicHubRequestError rejection)
        {
            this.rejection = rejection;
        }

        public bool RejectReset { get; set; }
        public StrategicHubSnapshot Current => inner.Current;
        public event Action<StrategicHubTransition> Transitioned
        {
            add { inner.Transitioned += value; }
            remove { inner.Transitioned -= value; }
        }

        public StrategicHubRequestResult Request(StrategicHubIntent intent)
        {
            if (RejectReset &&
                intent.Kind == StrategicHubIntentKind.ResetForEntry)
            {
                return StrategicHubRequestResult.Reject(
                    rejection, Current);
            }
            return inner.Request(intent);
        }
    }

    private static AnkaluteGameLoopState CreateLoopAtPhase(
        AnkaluteGameLoopPhase phase)
    {
        var loop = new AnkaluteGameLoopState(new StrategicHubFlow());
        loop.Request(AnkaluteGameLoopIntent.EnterStrategicHub(
            SessionEntryReason.NewGame));
        loop.Request(AnkaluteGameLoopIntent.LaunchExpedition(
            new ExpeditionStartRequest(
                new ExpeditionId("dungeon-return-guard"))));
        if (phase != AnkaluteGameLoopPhase.ExpeditionExploration)
        {
            loop.Request(AnkaluteGameLoopIntent.BeginBattle(
                new EncounterStartRequest(
                    new EncounterId("encounter-return-guard"))));
        }
        if (phase == AnkaluteGameLoopPhase.BattleSettlement)
        {
            loop.Request(AnkaluteGameLoopIntent.ResolveBattle(
                BattleResolution.Victory));
        }
        else if (phase != AnkaluteGameLoopPhase.ExpeditionExploration &&
                 phase != AnkaluteGameLoopPhase.Battle)
        {
            throw new ArgumentOutOfRangeException(nameof(phase));
        }

        AssertEqual(phase, loop.Current.Phase,
            "Fixture must reach the requested phase");
        return loop;
    }

    private static void ApplyAndAssertSingleTransition(
        AnkaluteGameLoopState loop,
        AnkaluteGameLoopIntent intent)
    {
        AnkaluteGameLoopSnapshot before = loop.Current;
        AnkaluteGameLoopTransition observed =
            default(AnkaluteGameLoopTransition);
        int eventCount = 0;
        Action<AnkaluteGameLoopTransition> handler = transition =>
        {
            observed = transition;
            eventCount++;
            AssertSame(transition.After, loop.Current,
                "Current must commit before terminal Transition event");
        };
        loop.Transitioned += handler;
        AnkaluteGameLoopRequestResult result;
        try
        {
            result = loop.Request(intent);
        }
        finally
        {
            loop.Transitioned -= handler;
        }

        AssertApplied(result);
        AssertEqual(AnkaluteGameLoopRequestError.None, result.Error,
            "Applied terminal result must not carry an error");
        AssertSame(loop.Current, result.Current,
            "Applied terminal result.Current must be committed Snapshot");
        AssertEqual(1, eventCount,
            "Applied terminal request must publish exactly one Transition");
        AssertSame(before, observed.Before,
            "Transition.Before must preserve the pre-request Snapshot");
        AssertSame(loop.Current, observed.After,
            "Transition.After must be the committed Snapshot");
        AssertEqual(intent, observed.Intent,
            "Terminal Transition must retain the triggering Intent");
        AssertEqual(before.Revision + 1L, loop.Current.Revision,
            "Applied terminal request must increment revision once");
    }

    private static void AssertNullHubDependency()
    {
        try
        {
            new AnkaluteGameLoopState(null);
            throw new Exception("Expected null Hub dependency rejection");
        }
        catch (ArgumentNullException error)
        {
            AssertEqual("strategicHub", error.ParamName,
                "Null Hub rejection must name the constructor dependency");
        }
    }

    private static void AssertEntryReasonMapping(
        SessionEntryReason sessionReason,
        StrategicHubEntryReason expectedHubReason)
    {
        var hub = new StrategicHubFlow();
        hub.Request(StrategicHubIntent.OpenDestination(
            StrategicHubRoute.CityMap("entry-reason-city")));
        StrategicHubTransition reset = default(StrategicHubTransition);
        int resetEvents = 0;
        hub.Transitioned += transition =>
        {
            reset = transition;
            resetEvents++;
        };

        var loop = new AnkaluteGameLoopState(hub);
        ApplyAndAssertCurrent(loop,
            AnkaluteGameLoopIntent.EnterStrategicHub(sessionReason));
        AssertEqual(1, resetEvents,
            "Dirty Hub entry must publish exactly one reset transition");
        AssertEqual(StrategicHubIntentKind.ResetForEntry,
            reset.Intent.Kind,
            "Top-level Hub entry must coordinate through ResetForEntry");
        AssertEqual(expectedHubReason, reset.Intent.EntryReason,
            "Session entry reason must map without collapsing semantics");
        AssertEqual(StrategicHubRoute.Home, hub.Current.Route,
            "Entering StrategicHub must reset HubHome");
    }

    private static void ApplyAndAssertCurrent(
        AnkaluteGameLoopState loop,
        AnkaluteGameLoopIntent intent)
    {
        AnkaluteGameLoopSnapshot before = loop.Current;
        AnkaluteGameLoopTransition observed =
            default(AnkaluteGameLoopTransition);
        int eventCount = 0;
        Action<AnkaluteGameLoopTransition> handler = transition =>
        {
            observed = transition;
            eventCount++;
        };
        loop.Transitioned += handler;
        AnkaluteGameLoopRequestResult result;
        try
        {
            result = loop.Request(intent);
        }
        finally
        {
            loop.Transitioned -= handler;
        }

        AssertApplied(result);
        AssertEqual(AnkaluteGameLoopRequestError.None, result.Error,
            "Applied result must not carry a rejection reason");
        AssertSame(loop.Current, result.Current,
            "Applied result.Current must be the committed Snapshot");
        AssertEqual(1, eventCount,
            "Applied request must publish exactly one top Transition");
        AssertSame(before, observed.Before,
            "Top Transition.Before must preserve pre-request Snapshot");
        AssertSame(loop.Current, observed.After,
            "Top Transition.After must be committed Snapshot");
        AssertEqual(intent, observed.Intent,
            "Top Transition must retain the triggering Intent");
        AssertEqual(before.Revision + 1L, loop.Current.Revision,
            "Applied top request must increment Revision once");
    }

    private static void AssertApplied(AnkaluteGameLoopRequestResult result)
    {
        AssertEqual(AnkaluteGameLoopRequestStatus.Applied, result.Status,
            "Top-level request must be applied");
    }

    private static void AssertRejected(
        AnkaluteGameLoopRequestError expected,
        AnkaluteGameLoopRequestResult result)
    {
        AssertEqual(AnkaluteGameLoopRequestStatus.Rejected, result.Status,
            "Top-level request must be rejected");
        AssertEqual(expected, result.Error,
            "Unexpected top-level rejection reason");
        AssertTrue(result.Current != null,
            "Rejected top result must carry Current");
    }

    private static void AssertSame(object expected, object actual, string message)
    {
        if (!ReferenceEquals(expected, actual))
            throw new Exception(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
            throw new Exception(message + ". Expected=" + expected +
                ", Actual=" + actual);
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
}
#endif
