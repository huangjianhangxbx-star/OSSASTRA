#if UNITY_EDITOR
using System;

public static class AnkaluteGameLoopRuntimeCoordinatorProbe
{
    public static string RunInitialHubEntry()
    {
        var coordinator = new AnkaluteGameLoopRuntimeCoordinator();
        AssertEqual(AnkaluteGameLoopPhase.Title,
            coordinator.Current.Phase,
            "Runtime coordinator must begin at Title");
        AssertEqual(StrategicHubRoute.Home,
            coordinator.HubCurrent.Route,
            "Runtime coordinator must begin with a stable HubHome host");

        AnkaluteGameLoopRequestResult entered =
            coordinator.EnterStrategicHub(SessionEntryReason.NewGame);
        AssertEqual(AnkaluteGameLoopRequestStatus.Applied,
            entered.Status,
            "NewGame must enter StrategicHub");
        AssertEqual(AnkaluteGameLoopPhase.StrategicHub,
            coordinator.Current.Phase,
            "NewGame must not skip directly into exploration");
        AssertEqual(StrategicHubRoute.Home,
            coordinator.HubCurrent.Route,
            "StrategicHub entry must reset navigation to HubHome");

        return "PASS runtime coordinator initial state";
    }

    public static string RunTypedExpeditionLaunch()
    {
        var coordinator = new AnkaluteGameLoopRuntimeCoordinator();
        coordinator.EnterStrategicHub(SessionEntryReason.NewGame);
        AnkaluteGameLoopSnapshot hubBefore = coordinator.Current;
        var request = new ExpeditionStartRequest(
            new ExpeditionId("dungeon-a"));

        bool launchedFromHome = coordinator.TryLaunchExpedition(
            request,
            out StrategicHubRequestResult homeConfirmation,
            out _);
        AssertFalse(launchedFromHome,
            "HubHome must not bypass DungeonSelection confirmation");
        AssertEqual(StrategicHubRequestError.InvalidRoute,
            homeConfirmation.Error,
            "HubHome launch must expose the Hub route rejection");
        AssertSame(hubBefore, coordinator.Current,
            "Rejected Hub confirmation must not mutate top-level state");

        StrategicHubRequestResult opened = coordinator.OpenDestination(
            StrategicHubRoute.DungeonSelection);
        AssertEqual(StrategicHubRequestStatus.Applied, opened.Status,
            "HubHome must open DungeonSelection");
        AssertEqual(StrategicHubRoute.DungeonSelection,
            coordinator.HubCurrent.Route,
            "Coordinator must expose the committed selection route");

        bool launched = coordinator.TryLaunchExpedition(
            request,
            out StrategicHubRequestResult confirmation,
            out AnkaluteGameLoopRequestResult transition);
        AssertTrue(launched,
            "A typed request confirmed from DungeonSelection must launch");
        AssertEqual(StrategicHubRequestStatus.Applied, confirmation.Status,
            "Launch must preserve the real Hub confirmation result");
        AssertTrue(confirmation.HasExpeditionRequest,
            "Hub confirmation must carry the typed request");
        AssertEqual(new ExpeditionId("dungeon-a"),
            confirmation.ExpeditionRequest.ExpeditionId,
            "Confirmation must preserve the exact ExpeditionId");
        AssertEqual(AnkaluteGameLoopRequestStatus.Applied,
            transition.Status,
            "Confirmed launch must commit the top-level transition");
        AssertEqual(AnkaluteGameLoopPhase.ExpeditionExploration,
            coordinator.Current.Phase,
            "Confirmed launch must enter ExpeditionExploration");
        AssertEqual(new ExpeditionId("dungeon-a"),
            coordinator.Current.ExpeditionId,
            "Active expedition context must retain the selected ID");

        return "PASS runtime coordinator typed expedition launch";
    }

    public static string RunActivationFailureCompensation()
    {
        var coordinator = new AnkaluteGameLoopRuntimeCoordinator();
        AssertApplied(coordinator.EnterStrategicHub(SessionEntryReason.NewGame));
        AssertEqual(StrategicHubRequestStatus.Applied,
            coordinator.OpenDestination(
                StrategicHubRoute.DungeonSelection).Status,
            "Fixture must open DungeonSelection");

        int activationAttempts = 0;
        bool launched = coordinator.TryLaunchExpeditionWithActivation(
            new ExpeditionStartRequest(
                new ExpeditionId("activation-failure")),
            () =>
            {
                activationAttempts++;
                return false;
            },
            out StrategicHubRequestResult confirmation,
            out AnkaluteGameLoopRequestResult transition);

        AssertFalse(launched,
            "A failed host activation must reject expedition launch");
        AssertEqual(1, activationAttempts,
            "Host activation must be attempted exactly once");
        AssertEqual(StrategicHubRequestStatus.Applied,
            confirmation.Status,
            "Failure fixture must first confirm the typed Hub request");
        AssertEqual(AnkaluteGameLoopRequestStatus.Applied,
            transition.Status,
            "Failure fixture must expose the compensated launch transition");
        AssertEqual(AnkaluteGameLoopPhase.StrategicHub,
            coordinator.Current.Phase,
            "Activation failure must compensate back to StrategicHub");
        AssertFalse(coordinator.Current.HasExpedition,
            "Activation failure must clear expedition context");
        AssertEqual(StrategicHubRoute.Home,
            coordinator.HubCurrent.Route,
            "Activation failure must reset the Hub host to HubHome");

        AssertEqual(StrategicHubRequestStatus.Applied,
            coordinator.OpenDestination(
                StrategicHubRoute.DungeonSelection).Status,
            "Fixture must reopen DungeonSelection after compensation");
        int throwingAttempts = 0;
        bool throwingLaunch = coordinator.TryLaunchExpeditionWithActivation(
            new ExpeditionStartRequest(
                new ExpeditionId("activation-exception")),
            () =>
            {
                throwingAttempts++;
                throw new InvalidOperationException("expected fault injection");
            },
            out _,
            out AnkaluteGameLoopRequestResult throwingTransition);
        AssertFalse(throwingLaunch,
            "A throwing host activation must be compensated as a failure");
        AssertEqual(1, throwingAttempts,
            "Throwing host activation must be attempted exactly once");
        AssertEqual(AnkaluteGameLoopRequestStatus.Applied,
            throwingTransition.Status,
            "Throwing activation must expose the compensated launch transition");
        AssertEqual(AnkaluteGameLoopPhase.StrategicHub,
            coordinator.Current.Phase,
            "Throwing activation must compensate back to StrategicHub");
        AssertFalse(coordinator.Current.HasExpedition,
            "Throwing activation must clear expedition context");
        AssertEqual(StrategicHubRoute.Home,
            coordinator.HubCurrent.Route,
            "Throwing activation must restore HubHome");

        return "PASS runtime coordinator activation failure compensation";
    }

    public static string RunBattleAndTerminalMapping()
    {
        AnkaluteGameLoopRuntimeCoordinator victory = CreateExpedition(
            "victory-expedition");
        AssertApplied(victory.BeginBattle(
            new EncounterStartRequest(new EncounterId("encounter-victory"))));
        AssertApplied(victory.ResolveBattle(BattleResolution.Victory));
        AssertEqual(AnkaluteGameLoopPhase.BattleSettlement,
            victory.Current.Phase,
            "Victory must enter BattleSettlement");
        AssertEqual(new ExpeditionId("victory-expedition"),
            victory.Current.ExpeditionId,
            "Victory settlement must retain expedition context");
        AssertApplied(victory.CompleteBattleSettlement());
        AssertEqual(AnkaluteGameLoopPhase.ExpeditionExploration,
            victory.Current.Phase,
            "Settlement completion must return to the same expedition");

        BattleResolution[] directReturns =
        {
            BattleResolution.Escaped,
            BattleResolution.Aborted
        };
        for (int i = 0; i < directReturns.Length; i++)
        {
            AnkaluteGameLoopRuntimeCoordinator direct = CreateExpedition(
                "direct-" + i);
            AssertApplied(direct.BeginBattle(
                new EncounterStartRequest(new EncounterId("encounter-" + i))));
            AssertApplied(direct.ResolveBattle(directReturns[i]));
            AssertEqual(AnkaluteGameLoopPhase.ExpeditionExploration,
                direct.Current.Phase,
                directReturns[i] + " must return directly to exploration");
            AssertTrue(direct.Current.HasExpedition,
                directReturns[i] + " must preserve expedition context");
        }

        AnkaluteGameLoopRuntimeCoordinator defeat = CreateExpedition(
            "defeat-expedition");
        defeat.OpenDestination(StrategicHubRoute.Home);
        AssertApplied(defeat.BeginBattle(
            new EncounterStartRequest(new EncounterId("encounter-defeat"))));
        AssertApplied(defeat.ResolveBattle(BattleResolution.PartyDefeated));
        AssertEqual(AnkaluteGameLoopPhase.StrategicHub,
            defeat.Current.Phase,
            "PartyDefeated must return to StrategicHub");
        AssertFalse(defeat.Current.HasExpedition,
            "PartyDefeated must clear expedition context");
        AssertEqual(StrategicHubRoute.Home, defeat.HubCurrent.Route,
            "PartyDefeated must reset Hub navigation to HubHome");

        ExpeditionExitReason[] exits =
        {
            ExpeditionExitReason.Completed,
            ExpeditionExitReason.Retreated
        };
        for (int i = 0; i < exits.Length; i++)
        {
            AnkaluteGameLoopRuntimeCoordinator terminal = CreateExpedition(
                "terminal-" + i);
            AssertApplied(terminal.EndExpedition(exits[i]));
            AssertEqual(AnkaluteGameLoopPhase.StrategicHub,
                terminal.Current.Phase,
                exits[i] + " must return to StrategicHub");
            AssertFalse(terminal.Current.HasExpedition,
                exits[i] + " must clear expedition context");
            AssertEqual(StrategicHubRoute.Home, terminal.HubCurrent.Route,
                exits[i] + " must reset Hub navigation");
        }

        return "PASS runtime coordinator battle and terminal mapping";
    }

    private static AnkaluteGameLoopRuntimeCoordinator CreateExpedition(
        string expeditionId)
    {
        var coordinator = new AnkaluteGameLoopRuntimeCoordinator();
        AssertApplied(coordinator.EnterStrategicHub(SessionEntryReason.NewGame));
        AssertEqual(StrategicHubRequestStatus.Applied,
            coordinator.OpenDestination(
                StrategicHubRoute.DungeonSelection).Status,
            "Fixture must open DungeonSelection");
        bool launched = coordinator.TryLaunchExpedition(
            new ExpeditionStartRequest(new ExpeditionId(expeditionId)),
            out _,
            out AnkaluteGameLoopRequestResult result);
        AssertTrue(launched, "Fixture must launch an expedition");
        AssertApplied(result);
        return coordinator;
    }

    private static void AssertApplied(AnkaluteGameLoopRequestResult result)
    {
        AssertEqual(AnkaluteGameLoopRequestStatus.Applied,
            result.Status,
            "Expected an applied top-level request");
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
        {
            throw new System.Exception(
                message + ". Expected=" + expected + ", Actual=" + actual);
        }
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new System.Exception(message);
    }

    private static void AssertFalse(bool value, string message)
    {
        AssertTrue(!value, message);
    }

    private static void AssertSame(object expected, object actual, string message)
    {
        if (!ReferenceEquals(expected, actual))
            throw new System.Exception(message);
    }
}
#endif
