#if UNITY_EDITOR
using System;

public static class StrategicHubFlowProbe
{
    public static string RunRouteStack()
    {
        var flow = new StrategicHubFlow();
        AssertEqual(StrategicHubRoute.Home, flow.Current.Route,
            "Hub must start at HubHome");
        AssertEqual(0, flow.Current.History.Count,
            "HubHome must have no back history");
        AssertEqual(0L, flow.Current.Revision,
            "Initial Hub revision must be zero");
        AssertFalse(flow.Current.IsTransitioning,
            "Published synchronous Snapshots must be stable");

        StrategicHubTransition last = default(StrategicHubTransition);
        int eventCount = 0;
        flow.Transitioned += transition =>
        {
            eventCount++;
            last = transition;
            AssertSame(transition.After, flow.Current,
                "Current must be committed before Transitioned fires");
            AssertFalse(flow.Current.IsTransitioning,
                "Published Hub Snapshot must remain a stable state");
        };

        StrategicHubSnapshot home = flow.Current;
        AssertRejected(StrategicHubRequestError.InvalidRoute,
            flow.Request(StrategicHubIntent.OpenDestination(
                StrategicHubRoute.Building("building-a"))));
        AssertSame(home, flow.Current,
            "Building cannot be opened without first entering CityMap");

        StrategicHubRequestResult openedCity = flow.Request(
            StrategicHubIntent.OpenDestination(
                StrategicHubRoute.CityMap("city-a")));
        AssertStatus(StrategicHubRequestStatus.Applied, openedCity);
        StrategicHubSnapshot citySnapshot = flow.Current;
        AssertSame(citySnapshot, openedCity.Current,
            "Applied navigation result.Current must be committed Snapshot");
        AssertEqual(StrategicHubRoute.CityMap("city-a"), citySnapshot.Route,
            "City route must retain the selected CityId");
        AssertEqual(1, citySnapshot.History.Count,
            "Opening city must push HubHome");
        AssertEqual(1, eventCount,
            "Opening city must publish one transition");
        AssertSame(home, last.Before,
            "City transition.Before must be the exact prior HubHome Snapshot");
        AssertSame(citySnapshot, last.After,
            "City transition must expose the committed Snapshot");

        AssertInvalidRoutePreserves(flow,
            StrategicHubRoute.DungeonSelection,
            "CityMap must not jump directly to DungeonSelection");
        AssertInvalidRoutePreserves(flow,
            StrategicHubRoute.CityMap("city-b"),
            "CityMap must not replace its context without returning Back");
        AssertInvalidRoutePreserves(flow,
            StrategicHubRoute.Home,
            "CityMap must return Home through Back, not OpenDestination");
        AssertEqual(1, eventCount,
            "Rejected CityMap exits must not publish transitions");

        StrategicHubSnapshot beforeBuilding = flow.Current;
        AssertStatus(StrategicHubRequestStatus.Applied,
            flow.Request(StrategicHubIntent.OpenDestination(
                StrategicHubRoute.Building("building-a"))));
        AssertEqual(StrategicHubRoute.Building("building-a"),
            flow.Current.Route,
            "Building route must retain the selected BuildingId");
        AssertEqual(2, flow.Current.History.Count,
            "Opening building must push the city route");
        AssertEqual(2, eventCount,
            "Opening building must publish one additional transition");
        AssertSame(beforeBuilding, last.Before,
            "Building transition.Before must be the exact CityMap Snapshot");
        AssertEqual(1, citySnapshot.History.Count,
            "Published history must remain immutable after later navigation");

        AssertInvalidRoutePreserves(flow,
            StrategicHubRoute.CityMap("city-b"),
            "Building must not jump to another CityMap");
        AssertInvalidRoutePreserves(flow,
            StrategicHubRoute.DungeonSelection,
            "Building must not jump directly to DungeonSelection");
        AssertInvalidRoutePreserves(flow,
            StrategicHubRoute.Building("building-b"),
            "Building must not replace its context without returning Back");
        AssertInvalidRoutePreserves(flow,
            StrategicHubRoute.Home,
            "Building must unwind through Back, not OpenDestination");
        AssertEqual(2, eventCount,
            "Rejected Building exits must not publish transitions");

        StrategicHubSnapshot beforeFirstBack = flow.Current;
        AssertStatus(StrategicHubRequestStatus.Applied,
            flow.Request(StrategicHubIntent.Back()));
        AssertEqual(StrategicHubRoute.CityMap("city-a"), flow.Current.Route,
            "First Back must restore the city route");
        AssertEqual(1, flow.Current.History.Count,
            "First Back must pop the city ancestor");
        AssertEqual(3, eventCount,
            "First Back must publish one additional transition");
        AssertSame(beforeFirstBack, last.Before,
            "First Back transition.Before must be the Building Snapshot");

        StrategicHubSnapshot beforeSecondBack = flow.Current;
        AssertStatus(StrategicHubRequestStatus.Applied,
            flow.Request(StrategicHubIntent.Back()));
        AssertEqual(StrategicHubRoute.Home, flow.Current.Route,
            "Second Back must restore HubHome");
        AssertEqual(0, flow.Current.History.Count,
            "Second Back must empty the route history");
        AssertSame(beforeSecondBack, last.Before,
            "Second Back transition.Before must be the returned CityMap Snapshot");

        StrategicHubSnapshot root = flow.Current;
        StrategicHubRequestResult rootBack = flow.Request(
            StrategicHubIntent.Back());
        AssertStatus(StrategicHubRequestStatus.NoOp, rootBack);
        AssertSame(root, rootBack.Current,
            "NoOp navigation result.Current must preserve Snapshot identity");
        AssertSame(root, flow.Current,
            "Root Back must not replace the Snapshot");
        AssertEqual(4, eventCount,
            "Root Back must not publish another transition");
        AssertSame(flow.Current, last.After,
            "Last transition must describe the committed HubHome Snapshot");
        AssertEqual(StrategicHubIntentKind.Back, last.Intent.Kind,
            "Hub Transition must retain the triggering Back Intent");

        StrategicHubRequestResult sameHome = flow.Request(
            StrategicHubIntent.OpenDestination(StrategicHubRoute.Home));
        AssertStatus(StrategicHubRequestStatus.NoOp, sameHome);
        AssertSame(root, sameHome.Current,
            "Opening the current route must preserve Snapshot identity");
        AssertEqual(4, eventCount,
            "Opening the current route must not publish a transition");

        StrategicHubRequestResult cleanReset = flow.Request(
            StrategicHubIntent.ResetForEntry(
                StrategicHubEntryReason.NewGame));
        AssertStatus(StrategicHubRequestStatus.NoOp, cleanReset);
        AssertSame(root, cleanReset.Current,
            "A clean Hub entry reset must preserve Snapshot identity");
        AssertEqual(4, eventCount,
            "A clean Hub entry reset must not publish a transition");
        AssertRejected(StrategicHubRequestError.InvalidIntent,
            flow.Request(default(StrategicHubIntent)));
        AssertRejected(StrategicHubRequestError.UnknownDestination,
            flow.Request(StrategicHubIntent.OpenDestination(default)));
        AssertRejected(StrategicHubRequestError.InvalidPayload,
            flow.Request(StrategicHubIntent.OpenDestination(
                StrategicHubRoute.CityMap(string.Empty))));
        AssertRejected(StrategicHubRequestError.InvalidPayload,
            flow.Request(StrategicHubIntent.OpenDestination(
                StrategicHubRoute.Building(" "))));
        AssertRejected(StrategicHubRequestError.InvalidPayload,
            flow.Request(StrategicHubIntent.ResetForEntry(
                StrategicHubEntryReason.Invalid)));
        AssertRejected(StrategicHubRequestError.InvalidPayload,
            flow.Request(StrategicHubIntent.ResetForEntry(
                (StrategicHubEntryReason)999)));
        AssertSame(root, flow.Current,
            "Rejected route requests must preserve Snapshot identity");
        AssertEqual(4L, flow.Current.Revision,
            "Only four real route changes may increment revision");
        AssertEqual(4, eventCount,
            "Only four real route changes may publish transitions");

        var dungeonFlow = new StrategicHubFlow();
        AssertStatus(StrategicHubRequestStatus.Applied,
            dungeonFlow.Request(StrategicHubIntent.OpenDestination(
                StrategicHubRoute.DungeonSelection)));
        StrategicHubSnapshot dungeonSelection = dungeonFlow.Current;
        AssertInvalidRoutePreserves(dungeonFlow,
            StrategicHubRoute.CityMap("city-from-dungeon"),
            "DungeonSelection must not jump to CityMap");
        AssertInvalidRoutePreserves(dungeonFlow,
            StrategicHubRoute.Building("building-from-dungeon"),
            "DungeonSelection must not jump to Building");
        AssertInvalidRoutePreserves(dungeonFlow,
            StrategicHubRoute.Home,
            "DungeonSelection must return Home through Back");
        AssertSame(dungeonSelection, dungeonFlow.Current,
            "Every rejected DungeonSelection exit must preserve Snapshot identity");
        AssertEqual(1L, dungeonFlow.Current.Revision,
            "Rejected DungeonSelection exits must not increment revision");

        var resetFlow = new StrategicHubFlow();
        AssertStatus(StrategicHubRequestStatus.Applied,
            resetFlow.Request(StrategicHubIntent.OpenDestination(
                StrategicHubRoute.CityMap("city-reset"))));
        AssertStatus(StrategicHubRequestStatus.Applied,
            resetFlow.Request(StrategicHubIntent.OpenDestination(
                StrategicHubRoute.Building("building-reset"))));
        StrategicHubRequestResult dirtyReset = resetFlow.Request(
            StrategicHubIntent.ResetForEntry(
                StrategicHubEntryReason.ReenterAfterTitle));
        AssertStatus(StrategicHubRequestStatus.Applied, dirtyReset);
        AssertEqual(StrategicHubRoute.Home, resetFlow.Current.Route,
            "Entry reset must return a deep route to HubHome");
        AssertEqual(0, resetFlow.Current.History.Count,
            "Entry reset must clear the complete route history");
        AssertEqual(3L, resetFlow.Current.Revision,
            "Dirty entry reset must be one committed transition");

        return "PASS Strategic Hub route stack";
    }

    public static string RunActivityLifecycle()
    {
        var flow = new StrategicHubFlow();
        int eventCount = 0;
        flow.Transitioned += transition =>
        {
            eventCount++;
            AssertSame(transition.After, flow.Current,
                "Activity transition must publish after Current commit");
        };

        AssertStatus(StrategicHubRequestStatus.Applied,
            flow.Request(StrategicHubIntent.OpenDestination(
                StrategicHubRoute.CityMap("city-a"))));
        StrategicHubRoute host = flow.Current.Route;

        StrategicHubRequestResult begin = flow.Request(
            StrategicHubIntent.BeginActivity(
                StrategicHubActivityRequest.Gal("facility-story-a")));
        AssertStatus(StrategicHubRequestStatus.Applied, begin);
        AssertSame(flow.Current, begin.Current,
            "Applied Activity result.Current must be committed Snapshot");
        AssertEqual(host, flow.Current.Route,
            "Activity must not replace its host route");
        AssertTrue(flow.Current.HasActivity,
            "A successful BeginActivity must expose an active activity");
        AssertEqual(StrategicHubActivityKind.Gal,
            flow.Current.Activity.Kind,
            "Gal activity kind must be preserved");
        AssertEqual("facility-story-a", flow.Current.Activity.ActivityId,
            "Gal activity id must be preserved");
        AssertTrue(flow.Current.ActivityToken.IsValid,
            "A successful BeginActivity must allocate a valid token");
        StrategicHubActivityToken token = flow.Current.ActivityToken;
        AssertEqual(2L, flow.Current.Revision,
            "BeginActivity must commit exactly one new Snapshot");
        AssertEqual(2, eventCount,
            "Route open and BeginActivity must publish two transitions");

        StrategicHubSnapshot busySnapshot = flow.Current;
        AssertRejected(StrategicHubRequestError.Busy,
            flow.Request(StrategicHubIntent.Back()));
        AssertSame(busySnapshot, flow.Current,
            "Busy Back must not replace the Snapshot");
        AssertEqual(2L, flow.Current.Revision,
            "Busy Back must not increment revision");
        AssertEqual(2, eventCount,
            "Busy Back must not publish an event");
        AssertRejected(StrategicHubRequestError.Busy,
            flow.Request(StrategicHubIntent.BeginActivity(
                StrategicHubActivityRequest.Shop("shop-a"))));
        AssertSame(busySnapshot, flow.Current,
            "Busy second Activity must not replace the Snapshot");
        AssertEqual(2L, flow.Current.Revision,
            "Busy second Activity must not increment revision");
        AssertEqual(2, eventCount,
            "Busy second Activity must not publish an event");
        AssertRejected(StrategicHubRequestError.Busy,
            flow.Request(StrategicHubIntent.OpenDestination(
                StrategicHubRoute.Building("building-a"))));
        AssertSame(busySnapshot, flow.Current,
            "Busy navigation must not replace the Snapshot");
        AssertEqual(2L, flow.Current.Revision,
            "Busy navigation must not increment revision");
        AssertEqual(2, eventCount,
            "Busy navigation must not publish an event");

        AssertRejected(StrategicHubRequestError.StaleCompletion,
            flow.Request(StrategicHubIntent.CompleteActivity(
                new StrategicHubActivityToken(token.Value + 1L),
                StrategicHubActivityCompletion.Completed)));
        AssertSame(busySnapshot, flow.Current,
            "Stale completion must preserve Snapshot identity");
        AssertTrue(flow.Current.HasActivity,
            "A stale token must not close the active activity");
        AssertEqual(2L, flow.Current.Revision,
            "Stale completion must not increment revision");
        AssertEqual(2, eventCount,
            "Stale completion must not publish an event");

        AssertStatus(StrategicHubRequestStatus.Applied,
            flow.Request(StrategicHubIntent.CompleteActivity(
                token, StrategicHubActivityCompletion.Failed)));
        AssertFalse(flow.Current.HasActivity,
            "Matching completion must clear the activity");
        AssertEqual(host, flow.Current.Route,
            "Completing activity must preserve the same host route");
        AssertEqual(1, flow.Current.History.Count,
            "Completing activity must preserve host route history");
        AssertEqual(3L, flow.Current.Revision,
            "Matching completion must commit exactly one Snapshot");
        AssertEqual(3, eventCount,
            "Matching completion must publish one transition");

        var resetFlow = new StrategicHubFlow();
        AssertStatus(StrategicHubRequestStatus.Applied,
            resetFlow.Request(StrategicHubIntent.OpenDestination(
                StrategicHubRoute.CityMap("city-reset"))));
        AssertStatus(StrategicHubRequestStatus.Applied,
            resetFlow.Request(StrategicHubIntent.OpenDestination(
                StrategicHubRoute.Building("building-reset"))));
        AssertStatus(StrategicHubRequestStatus.Applied,
            resetFlow.Request(StrategicHubIntent.BeginActivity(
                StrategicHubActivityRequest.Gal("facility-reset"))));
        StrategicHubActivityToken oldToken =
            resetFlow.Current.ActivityToken;
        AssertStatus(StrategicHubRequestStatus.Applied,
            resetFlow.Request(StrategicHubIntent.ResetForEntry(
                StrategicHubEntryReason.ExpeditionCompleted)));
        AssertEqual(StrategicHubRoute.Home, resetFlow.Current.Route,
            "ResetForEntry must force HubHome");
        AssertEqual(0, resetFlow.Current.History.Count,
            "ResetForEntry must clear route history");
        AssertFalse(resetFlow.Current.HasActivity,
            "ResetForEntry must clear a hosted activity");
        AssertEqual(4L, resetFlow.Current.Revision,
            "Privileged reset must be one committed transition");
        StrategicHubSnapshot cleanHome = resetFlow.Current;
        AssertStatus(StrategicHubRequestStatus.NoOp,
            resetFlow.Request(StrategicHubIntent.ResetForEntry(
                StrategicHubEntryReason.ExpeditionCompleted)));
        AssertSame(cleanHome, resetFlow.Current,
            "Repeated clean reset must be NoOp");

        AssertStatus(StrategicHubRequestStatus.Applied,
            resetFlow.Request(StrategicHubIntent.BeginActivity(
                StrategicHubActivityRequest.Shop("shop-a"))));
        StrategicHubActivityToken newToken =
            resetFlow.Current.ActivityToken;
        AssertEqual(StrategicHubActivityKind.Shop,
            resetFlow.Current.Activity.Kind,
            "Shop factory must preserve the typed Shop activity kind");
        AssertEqual("shop-a", resetFlow.Current.Activity.ActivityId,
            "Shop factory must preserve the typed Shop activity id");
        AssertTrue(newToken.Value != oldToken.Value,
            "ResetForEntry must not reuse activity tokens");
        AssertRejected(StrategicHubRequestError.StaleCompletion,
            resetFlow.Request(StrategicHubIntent.CompleteActivity(
                oldToken, StrategicHubActivityCompletion.Completed)));

        var dialogueFlow = new StrategicHubFlow();
        AssertStatus(StrategicHubRequestStatus.Applied,
            dialogueFlow.Request(StrategicHubIntent.BeginActivity(
                StrategicHubActivityRequest.DialogueEvent(
                    "dialogue-event-a"))));
        AssertEqual(StrategicHubActivityKind.DialogueEvent,
            dialogueFlow.Current.Activity.Kind,
            "DialogueEvent must be a legal hosted Activity kind");
        AssertStatus(StrategicHubRequestStatus.Applied,
            dialogueFlow.Request(StrategicHubIntent.CompleteActivity(
                dialogueFlow.Current.ActivityToken,
                StrategicHubActivityCompletion.Completed)));

        var invalidActivityFlow = new StrategicHubFlow();
        int invalidEventCount = 0;
        invalidActivityFlow.Transitioned += transition =>
            invalidEventCount++;
        StrategicHubSnapshot invalidActivityBefore =
            invalidActivityFlow.Current;
        AssertRejected(StrategicHubRequestError.InvalidPayload,
            invalidActivityFlow.Request(StrategicHubIntent.BeginActivity(
                StrategicHubActivityRequest.Gal(string.Empty))));
        AssertRejected(StrategicHubRequestError.InvalidPayload,
            invalidActivityFlow.Request(StrategicHubIntent.ResetForEntry(
                StrategicHubEntryReason.Invalid)));
        AssertRejected(StrategicHubRequestError.InvalidPayload,
            invalidActivityFlow.Request(StrategicHubIntent.ResetForEntry(
                (StrategicHubEntryReason)999)));
        AssertRejected(StrategicHubRequestError.StaleCompletion,
            invalidActivityFlow.Request(StrategicHubIntent.CompleteActivity(
                new StrategicHubActivityToken(1),
                StrategicHubActivityCompletion.Completed)));
        AssertSame(invalidActivityBefore, invalidActivityFlow.Current,
            "Invalid activity/reset requests must preserve Snapshot identity");
        AssertEqual(0L, invalidActivityFlow.Current.Revision,
            "Invalid activity/reset requests must not increment revision");
        AssertEqual(0, invalidEventCount,
            "Invalid activity/reset requests must not publish events");

        AssertStatus(StrategicHubRequestStatus.Applied,
            invalidActivityFlow.Request(StrategicHubIntent.BeginActivity(
                StrategicHubActivityRequest.Shop(
                    "shop-invalid-completion"))));
        StrategicHubSnapshot beforeInvalidCompletion =
            invalidActivityFlow.Current;
        AssertRejected(StrategicHubRequestError.InvalidPayload,
            invalidActivityFlow.Request(StrategicHubIntent.CompleteActivity(
                invalidActivityFlow.Current.ActivityToken,
                StrategicHubActivityCompletion.Invalid)));
        AssertRejected(StrategicHubRequestError.InvalidPayload,
            invalidActivityFlow.Request(StrategicHubIntent.CompleteActivity(
                invalidActivityFlow.Current.ActivityToken,
                (StrategicHubActivityCompletion)999)));
        AssertSame(beforeInvalidCompletion, invalidActivityFlow.Current,
            "Invalid completion must not close the active Activity");
        AssertEqual(1L, invalidActivityFlow.Current.Revision,
            "Invalid completion must not increment revision");
        AssertEqual(1, invalidEventCount,
            "Invalid completion must not publish an event");

        var busyPrecedenceFlow = new StrategicHubFlow();
        AssertStatus(StrategicHubRequestStatus.Applied,
            busyPrecedenceFlow.Request(StrategicHubIntent.BeginActivity(
                StrategicHubActivityRequest.Gal("busy-precedence"))));
        StrategicHubSnapshot busyPrecedenceSnapshot =
            busyPrecedenceFlow.Current;
        AssertRejected(StrategicHubRequestError.Busy,
            busyPrecedenceFlow.Request(StrategicHubIntent.BeginActivity(
                default(StrategicHubActivityRequest))));
        AssertRejected(StrategicHubRequestError.Busy,
            busyPrecedenceFlow.Request(
                StrategicHubIntent.OpenDestination(default)));
        AssertSame(busyPrecedenceSnapshot, busyPrecedenceFlow.Current,
            "Busy must take precedence over hosted command payload checks");

        var homeActivityResetFlow = new StrategicHubFlow();
        AssertStatus(StrategicHubRequestStatus.Applied,
            homeActivityResetFlow.Request(
                StrategicHubIntent.BeginActivity(
                    StrategicHubActivityRequest.Shop("home-shop"))));
        AssertStatus(StrategicHubRequestStatus.Applied,
            homeActivityResetFlow.Request(
                StrategicHubIntent.ResetForEntry(
                    StrategicHubEntryReason.PartyDefeated)));
        AssertFalse(homeActivityResetFlow.Current.HasActivity,
            "HubHome reset must still clear an active hosted Activity");
        AssertEqual(2L, homeActivityResetFlow.Current.Revision,
            "Clearing a HubHome Activity must be a committed reset");

        return "PASS Strategic Hub activity lifecycle";
    }

    public static string RunExpeditionConfirmationAndEventSafety()
    {
        var flow = new StrategicHubFlow();
        var request = new ExpeditionStartRequest(
            new ExpeditionId("dungeon-a"));
        StrategicHubSnapshot home = flow.Current;

        AssertRejected(StrategicHubRequestError.InvalidRoute,
            flow.Request(StrategicHubIntent.ConfirmExpedition(request)));
        AssertSame(home, flow.Current,
            "HubHome confirmation rejection must preserve Snapshot identity");
        AssertEqual(0L, flow.Current.Revision,
            "Wrong-route confirmation must not increment revision");

        AssertStatus(StrategicHubRequestStatus.Applied,
            flow.Request(StrategicHubIntent.OpenDestination(
                StrategicHubRoute.DungeonSelection)));
        AssertEqual(StrategicHubRoute.DungeonSelection,
            flow.Current.Route,
            "HubHome must open the DungeonSelection route");
        AssertEqual(1, flow.Current.History.Count,
            "DungeonSelection must push HubHome");

        StrategicHubSnapshot selection = flow.Current;
        int events = 0;
        flow.Transitioned += _ => events++;
        StrategicHubRequestResult confirmed = flow.Request(
            StrategicHubIntent.ConfirmExpedition(request));
        AssertStatus(StrategicHubRequestStatus.Applied, confirmed);
        AssertTrue(confirmed.HasExpeditionRequest,
            "Confirmation must return one typed expedition request");
        AssertEqual(request, confirmed.ExpeditionRequest,
            "Confirmation must return the original typed request value");
        AssertEqual(new ExpeditionId("dungeon-a"),
            confirmed.ExpeditionRequest.ExpeditionId,
            "Confirmation must preserve the selected ExpeditionId");
        AssertSame(selection, confirmed.Current,
            "Confirmation result.Current must be the exact current Snapshot");
        AssertSame(selection, flow.Current,
            "Confirmation output must not mutate Hub navigation");
        AssertEqual(1L, flow.Current.Revision,
            "Confirmation output must not increment Hub revision");
        AssertEqual(0, events,
            "Confirmation output must not publish a route transition");

        StrategicHubRequestResult invalidConfirmation = flow.Request(
            StrategicHubIntent.ConfirmExpedition(
                new ExpeditionStartRequest(
                    new ExpeditionId(string.Empty))));
        AssertRejected(StrategicHubRequestError.InvalidPayload,
            invalidConfirmation);
        AssertSame(flow.Current, invalidConfirmation.Current,
            "Rejected result.Current must be the exact current Snapshot");
        AssertEqual(1L, flow.Current.Revision,
            "Rejected confirmation must not increment revision");
        AssertEqual(0, events,
            "Rejected confirmation must not publish a transition");

        StrategicHubSnapshot beforeSelectionNoOp = flow.Current;
        StrategicHubRequestResult selectionNoOp = flow.Request(
            StrategicHubIntent.OpenDestination(
                StrategicHubRoute.DungeonSelection));
        AssertStatus(StrategicHubRequestStatus.NoOp, selectionNoOp);
        AssertFalse(selectionNoOp.HasExpeditionRequest,
            "NoOp must not carry an Expedition request");
        AssertSame(beforeSelectionNoOp, selectionNoOp.Current,
            "NoOp result.Current must preserve Snapshot identity");

        var busyFlow = new StrategicHubFlow();
        AssertStatus(StrategicHubRequestStatus.Applied,
            busyFlow.Request(StrategicHubIntent.OpenDestination(
                StrategicHubRoute.DungeonSelection)));
        AssertStatus(StrategicHubRequestStatus.Applied,
            busyFlow.Request(StrategicHubIntent.BeginActivity(
                StrategicHubActivityRequest.Gal("facility-story-a"))));
        StrategicHubSnapshot busyBefore = busyFlow.Current;
        AssertRejected(StrategicHubRequestError.Busy,
            busyFlow.Request(StrategicHubIntent.ConfirmExpedition(request)));
        AssertSame(busyBefore, busyFlow.Current,
            "Activity must block expedition confirmation without mutation");

        var reentrant = new StrategicHubFlow();
        StrategicHubRequestResult nested =
            default(StrategicHubRequestResult);
        bool attempted = false;
        reentrant.Transitioned += _ =>
        {
            if (attempted)
                return;
            attempted = true;
            nested = reentrant.Request(StrategicHubIntent.Back());
        };
        AssertStatus(StrategicHubRequestStatus.Applied,
            reentrant.Request(StrategicHubIntent.OpenDestination(
                StrategicHubRoute.CityMap("city-a"))));
        AssertRejected(StrategicHubRequestError.Busy, nested);
        AssertEqual(StrategicHubRoute.CityMap("city-a"),
            reentrant.Current.Route,
            "Nested request must not overwrite the outer transition");

        var exceptionFlow = new StrategicHubFlow();
        Action<StrategicHubTransition> throwing = _ =>
            throw new InvalidOperationException("probe-subscriber-failure");
        exceptionFlow.Transitioned += throwing;
        try
        {
            exceptionFlow.Request(StrategicHubIntent.OpenDestination(
                StrategicHubRoute.CityMap("city-a")));
            throw new Exception("Expected subscriber exception was not exposed");
        }
        catch (InvalidOperationException error)
        {
            AssertEqual("probe-subscriber-failure", error.Message,
                "The exact subscriber failure must remain diagnosable");
        }
        exceptionFlow.Transitioned -= throwing;
        AssertEqual(StrategicHubRoute.CityMap("city-a"),
            exceptionFlow.Current.Route,
            "Subscriber failure must not roll back committed state");
        AssertStatus(StrategicHubRequestStatus.Applied,
            exceptionFlow.Request(StrategicHubIntent.Back()));
        AssertEqual(StrategicHubRoute.Home, exceptionFlow.Current.Route,
            "finally must restore future Hub request usability");

        return "PASS Strategic Hub confirmation and event safety";
    }

    private static void AssertInvalidRoutePreserves(
        StrategicHubFlow flow,
        StrategicHubRoute destination,
        string message)
    {
        StrategicHubSnapshot before = flow.Current;
        AssertRejected(StrategicHubRequestError.InvalidRoute,
            flow.Request(StrategicHubIntent.OpenDestination(destination)));
        AssertSame(before, flow.Current, message);
    }

    private static void AssertStatus(
        StrategicHubRequestStatus expected,
        StrategicHubRequestResult actual)
    {
        AssertEqual(expected, actual.Status, "Unexpected request status");
        if (expected == StrategicHubRequestStatus.Applied ||
            expected == StrategicHubRequestStatus.NoOp)
        {
            AssertEqual(StrategicHubRequestError.None, actual.Error,
                "Applied/NoOp Hub result must not carry an error");
        }
        AssertTrue(actual.Current != null,
            "Every Hub result must carry Current");
    }

    private static void AssertRejected(
        StrategicHubRequestError expected,
        StrategicHubRequestResult actual)
    {
        AssertEqual(StrategicHubRequestStatus.Rejected, actual.Status,
            "Request must be rejected");
        AssertEqual(expected, actual.Error,
            "Unexpected rejection reason");
        AssertTrue(actual.Current != null,
            "Rejected Hub result must carry Current");
        AssertFalse(actual.HasExpeditionRequest,
            "Rejected Hub result must not carry Expedition output");
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
