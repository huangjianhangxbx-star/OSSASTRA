using System;
using System.Collections.Generic;

public sealed class StrategicHubFlow : IStrategicHubFlow
{
    private readonly List<StrategicHubRoute> history =
        new List<StrategicHubRoute>();
    private bool hasActivity;
    private StrategicHubActivityRequest activity;
    private StrategicHubActivityToken activityToken;
    private long lastActivityToken;
    private bool isProcessing;

    public StrategicHubSnapshot Current { get; private set; }
    public event Action<StrategicHubTransition> Transitioned;

    public StrategicHubFlow()
    {
        Current = new StrategicHubSnapshot(
            StrategicHubRoute.Home,
            Array.AsReadOnly(new StrategicHubRoute[0]),
            false,
            default,
            default,
            false,
            0L);
    }

    public StrategicHubRequestResult Request(StrategicHubIntent intent)
    {
        if (isProcessing)
        {
            return StrategicHubRequestResult.Reject(
                StrategicHubRequestError.Busy,
                Current);
        }

        isProcessing = true;
        try
        {
            return Apply(intent);
        }
        finally
        {
            isProcessing = false;
        }
    }

    private StrategicHubRequestResult Apply(StrategicHubIntent intent)
    {
        if (intent.Kind == StrategicHubIntentKind.ResetForEntry)
        {
            if (!IsValidEntryReason(intent.EntryReason))
                return Reject(StrategicHubRequestError.InvalidPayload);
            if (Current.Route.Equals(StrategicHubRoute.Home) &&
                history.Count == 0 &&
                !hasActivity)
            {
                return NoOp();
            }
            return Reset(intent);
        }

        if (hasActivity &&
            (intent.Kind == StrategicHubIntentKind.Back ||
             intent.Kind == StrategicHubIntentKind.BeginActivity ||
             intent.Kind == StrategicHubIntentKind.OpenDestination ||
             intent.Kind == StrategicHubIntentKind.ConfirmExpedition))
        {
            return Reject(StrategicHubRequestError.Busy);
        }

        if (intent.Kind == StrategicHubIntentKind.CompleteActivity)
        {
            if (intent.ActivityCompletion !=
                    StrategicHubActivityCompletion.Failed &&
                intent.ActivityCompletion !=
                    StrategicHubActivityCompletion.Completed)
            {
                return Reject(StrategicHubRequestError.InvalidPayload);
            }
            if (!hasActivity ||
                !intent.ActivityToken.IsValid ||
                !intent.ActivityToken.Equals(activityToken))
            {
                return Reject(StrategicHubRequestError.StaleCompletion);
            }
            return CompleteActivity(intent);
        }

        if (intent.Kind == StrategicHubIntentKind.BeginActivity)
        {
            if (!intent.ActivityRequest.IsValid)
                return Reject(StrategicHubRequestError.InvalidPayload);
            return BeginActivity(intent);
        }

        if (intent.Kind == StrategicHubIntentKind.ConfirmExpedition)
        {
            if (Current.Route.Kind !=
                StrategicHubRouteKind.DungeonSelection)
            {
                return Reject(StrategicHubRequestError.InvalidRoute);
            }
            if (!intent.ExpeditionRequest.IsValid)
                return Reject(StrategicHubRequestError.InvalidPayload);
            return new StrategicHubRequestResult(
                StrategicHubRequestStatus.Applied,
                StrategicHubRequestError.None,
                Current,
                true,
                intent.ExpeditionRequest);
        }

        if (intent.Kind == StrategicHubIntentKind.Back)
        {
            if (history.Count == 0)
                return NoOp();
            return Back(intent);
        }

        if (intent.Kind != StrategicHubIntentKind.OpenDestination)
            return Reject(StrategicHubRequestError.InvalidIntent);

        StrategicHubRoute destination = intent.Destination;
        if (destination.Kind == StrategicHubRouteKind.Invalid ||
            !Enum.IsDefined(typeof(StrategicHubRouteKind), destination.Kind))
        {
            return Reject(StrategicHubRequestError.UnknownDestination);
        }

        if (!destination.IsValid)
            return Reject(StrategicHubRequestError.InvalidPayload);

        if (destination.Equals(Current.Route))
            return NoOp();

        if (Current.Route.Kind == StrategicHubRouteKind.HubHome &&
            (destination.Kind == StrategicHubRouteKind.CityMap ||
             destination.Kind == StrategicHubRouteKind.DungeonSelection))
        {
            return Open(destination, intent);
        }

        if (Current.Route.Kind == StrategicHubRouteKind.CityMap &&
            destination.Kind == StrategicHubRouteKind.Building)
        {
            return Open(destination, intent);
        }

        return Reject(StrategicHubRequestError.InvalidRoute);
    }

    private StrategicHubRequestResult Open(
        StrategicHubRoute destination,
        StrategicHubIntent intent)
    {
        StrategicHubSnapshot before = Current;
        history.Add(before.Route);
        Current = CreateSnapshot(destination, before.Revision + 1L);
        Transitioned?.Invoke(new StrategicHubTransition(before, Current, intent));
        return new StrategicHubRequestResult(
            StrategicHubRequestStatus.Applied,
            StrategicHubRequestError.None,
            Current,
            false,
            default);
    }

    private StrategicHubRequestResult Back(StrategicHubIntent intent)
    {
        StrategicHubSnapshot before = Current;
        int lastIndex = history.Count - 1;
        StrategicHubRoute destination = history[lastIndex];
        history.RemoveAt(lastIndex);
        Current = CreateSnapshot(destination, before.Revision + 1L);
        Transitioned?.Invoke(new StrategicHubTransition(before, Current, intent));
        return new StrategicHubRequestResult(
            StrategicHubRequestStatus.Applied,
            StrategicHubRequestError.None,
            Current,
            false,
            default);
    }

    private StrategicHubRequestResult BeginActivity(StrategicHubIntent intent)
    {
        StrategicHubSnapshot before = Current;
        lastActivityToken++;
        hasActivity = true;
        activity = intent.ActivityRequest;
        activityToken = new StrategicHubActivityToken(lastActivityToken);
        Current = CreateSnapshot(before.Route, before.Revision + 1L);
        Transitioned?.Invoke(new StrategicHubTransition(before, Current, intent));
        return new StrategicHubRequestResult(
            StrategicHubRequestStatus.Applied,
            StrategicHubRequestError.None,
            Current,
            false,
            default);
    }

    private StrategicHubRequestResult CompleteActivity(
        StrategicHubIntent intent)
    {
        StrategicHubSnapshot before = Current;
        hasActivity = false;
        activity = default;
        activityToken = default;
        Current = CreateSnapshot(before.Route, before.Revision + 1L);
        Transitioned?.Invoke(new StrategicHubTransition(before, Current, intent));
        return new StrategicHubRequestResult(
            StrategicHubRequestStatus.Applied,
            StrategicHubRequestError.None,
            Current,
            false,
            default);
    }

    private StrategicHubRequestResult Reset(StrategicHubIntent intent)
    {
        StrategicHubSnapshot before = Current;
        history.Clear();
        hasActivity = false;
        activity = default;
        activityToken = default;
        Current = CreateSnapshot(
            StrategicHubRoute.Home,
            before.Revision + 1L);
        Transitioned?.Invoke(new StrategicHubTransition(before, Current, intent));
        return new StrategicHubRequestResult(
            StrategicHubRequestStatus.Applied,
            StrategicHubRequestError.None,
            Current,
            false,
            default);
    }

    private StrategicHubSnapshot CreateSnapshot(
        StrategicHubRoute route,
        long revision)
    {
        return new StrategicHubSnapshot(
            route,
            Array.AsReadOnly(history.ToArray()),
            hasActivity,
            activity,
            activityToken,
            false,
            revision);
    }

    private StrategicHubRequestResult Reject(StrategicHubRequestError error)
    {
        return StrategicHubRequestResult.Reject(error, Current);
    }

    private StrategicHubRequestResult NoOp()
    {
        return new StrategicHubRequestResult(
            StrategicHubRequestStatus.NoOp,
            StrategicHubRequestError.None,
            Current,
            false,
            default);
    }

    private static bool IsValidEntryReason(StrategicHubEntryReason reason)
    {
        switch (reason)
        {
            case StrategicHubEntryReason.NewGame:
            case StrategicHubEntryReason.LoadedGame:
            case StrategicHubEntryReason.ReenterAfterTitle:
            case StrategicHubEntryReason.ExpeditionCompleted:
            case StrategicHubEntryReason.ExpeditionRetreated:
            case StrategicHubEntryReason.PartyDefeated:
                return true;
            default:
                return false;
        }
    }
}
