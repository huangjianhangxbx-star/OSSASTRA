using System;
using System.Collections.ObjectModel;

public enum StrategicHubRouteKind
{
    Invalid = 0,
    HubHome = 1,
    DungeonSelection = 2,
    CityMap = 3,
    Building = 4
}

public enum StrategicHubEntryReason
{
    Invalid = 0,
    NewGame = 1,
    LoadedGame = 2,
    ReenterAfterTitle = 3,
    ExpeditionCompleted = 4,
    ExpeditionRetreated = 5,
    PartyDefeated = 6
}

public enum StrategicHubActivityKind
{
    Invalid = 0,
    Gal = 1,
    Shop = 2,
    DialogueEvent = 3
}

public enum StrategicHubActivityCompletion
{
    Invalid = 0,
    Completed = 1,
    Failed = 2
}

public enum StrategicHubIntentKind
{
    Invalid = 0,
    ResetForEntry = 1,
    OpenDestination = 2,
    Back = 3,
    BeginActivity = 4,
    CompleteActivity = 5,
    ConfirmExpedition = 6
}

public enum StrategicHubRequestStatus
{
    Invalid = 0,
    Applied = 1,
    NoOp = 2,
    Rejected = 3
}

public enum StrategicHubRequestError
{
    None = 0,
    InvalidIntent = 1,
    InvalidPayload = 2,
    UnknownDestination = 3,
    InvalidRoute = 4,
    Busy = 5,
    StaleCompletion = 6
}

public readonly struct StrategicHubRoute : IEquatable<StrategicHubRoute>
{
    public static StrategicHubRoute Home =>
        new StrategicHubRoute(StrategicHubRouteKind.HubHome, string.Empty);
    public static StrategicHubRoute DungeonSelection =>
        new StrategicHubRoute(
            StrategicHubRouteKind.DungeonSelection, string.Empty);

    public StrategicHubRouteKind Kind { get; }
    public string ContextId { get; }
    public bool IsValid => Kind == StrategicHubRouteKind.HubHome ||
        Kind == StrategicHubRouteKind.DungeonSelection ||
        ((Kind == StrategicHubRouteKind.CityMap ||
          Kind == StrategicHubRouteKind.Building) &&
         !string.IsNullOrWhiteSpace(ContextId));

    private StrategicHubRoute(StrategicHubRouteKind kind, string contextId)
    {
        Kind = kind;
        ContextId = contextId ?? string.Empty;
    }

    public static StrategicHubRoute CityMap(string cityId) =>
        new StrategicHubRoute(StrategicHubRouteKind.CityMap, cityId);
    public static StrategicHubRoute Building(string buildingId) =>
        new StrategicHubRoute(StrategicHubRouteKind.Building, buildingId);

    public bool Equals(StrategicHubRoute other) =>
        Kind == other.Kind && string.Equals(
            ContextId, other.ContextId, StringComparison.Ordinal);
    public override bool Equals(object obj) =>
        obj is StrategicHubRoute other && Equals(other);
    public override int GetHashCode() =>
        ((int)Kind * 397) ^ StringComparer.Ordinal.GetHashCode(
            ContextId ?? string.Empty);
}

public readonly struct StrategicHubActivityRequest
{
    public StrategicHubActivityKind Kind { get; }
    public string ActivityId { get; }
    public bool IsValid =>
        (Kind == StrategicHubActivityKind.Gal ||
         Kind == StrategicHubActivityKind.Shop ||
         Kind == StrategicHubActivityKind.DialogueEvent) &&
        !string.IsNullOrWhiteSpace(ActivityId);

    private StrategicHubActivityRequest(
        StrategicHubActivityKind kind, string activityId)
    {
        Kind = kind;
        ActivityId = activityId ?? string.Empty;
    }

    public static StrategicHubActivityRequest Gal(string id) =>
        new StrategicHubActivityRequest(StrategicHubActivityKind.Gal, id);
    public static StrategicHubActivityRequest Shop(string id) =>
        new StrategicHubActivityRequest(StrategicHubActivityKind.Shop, id);
    public static StrategicHubActivityRequest DialogueEvent(string id) =>
        new StrategicHubActivityRequest(
            StrategicHubActivityKind.DialogueEvent, id);
}

public readonly struct StrategicHubActivityToken :
    IEquatable<StrategicHubActivityToken>
{
    public long Value { get; }
    public bool IsValid => Value > 0;

    public StrategicHubActivityToken(long value)
    {
        Value = value;
    }

    public bool Equals(StrategicHubActivityToken other) =>
        Value == other.Value;
    public override bool Equals(object obj) =>
        obj is StrategicHubActivityToken other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
}

public readonly struct StrategicHubIntent
{
    public StrategicHubIntentKind Kind { get; }
    public StrategicHubEntryReason EntryReason { get; }
    public StrategicHubRoute Destination { get; }
    public StrategicHubActivityRequest ActivityRequest { get; }
    public StrategicHubActivityToken ActivityToken { get; }
    public StrategicHubActivityCompletion ActivityCompletion { get; }
    public ExpeditionStartRequest ExpeditionRequest { get; }

    private StrategicHubIntent(
        StrategicHubIntentKind kind,
        StrategicHubEntryReason entryReason,
        StrategicHubRoute destination,
        StrategicHubActivityRequest activityRequest,
        StrategicHubActivityToken activityToken,
        StrategicHubActivityCompletion activityCompletion,
        ExpeditionStartRequest expeditionRequest)
    {
        Kind = kind;
        EntryReason = entryReason;
        Destination = destination;
        ActivityRequest = activityRequest;
        ActivityToken = activityToken;
        ActivityCompletion = activityCompletion;
        ExpeditionRequest = expeditionRequest;
    }

    public static StrategicHubIntent ResetForEntry(
        StrategicHubEntryReason reason) =>
        new StrategicHubIntent(
            StrategicHubIntentKind.ResetForEntry,
            reason, default, default, default, default, default);
    public static StrategicHubIntent OpenDestination(
        StrategicHubRoute destination) =>
        new StrategicHubIntent(
            StrategicHubIntentKind.OpenDestination,
            default, destination, default, default, default, default);
    public static StrategicHubIntent Back() =>
        new StrategicHubIntent(
            StrategicHubIntentKind.Back,
            default, default, default, default, default, default);
    public static StrategicHubIntent BeginActivity(
        StrategicHubActivityRequest request) =>
        new StrategicHubIntent(
            StrategicHubIntentKind.BeginActivity,
            default, default, request, default, default, default);
    public static StrategicHubIntent CompleteActivity(
        StrategicHubActivityToken token,
        StrategicHubActivityCompletion completion) =>
        new StrategicHubIntent(
            StrategicHubIntentKind.CompleteActivity,
            default, default, default, token, completion, default);
    public static StrategicHubIntent ConfirmExpedition(
        ExpeditionStartRequest request) =>
        new StrategicHubIntent(
            StrategicHubIntentKind.ConfirmExpedition,
            default, default, default, default, default, request);
}

public sealed class StrategicHubSnapshot
{
    public StrategicHubRoute Route { get; }
    public ReadOnlyCollection<StrategicHubRoute> History { get; }
    public bool HasActivity { get; }
    public StrategicHubActivityRequest Activity { get; }
    public StrategicHubActivityToken ActivityToken { get; }
    public bool IsTransitioning { get; }
    public long Revision { get; }

    internal StrategicHubSnapshot(
        StrategicHubRoute route,
        ReadOnlyCollection<StrategicHubRoute> history,
        bool hasActivity,
        StrategicHubActivityRequest activity,
        StrategicHubActivityToken activityToken,
        bool isTransitioning,
        long revision)
    {
        Route = route;
        History = history;
        HasActivity = hasActivity;
        Activity = activity;
        ActivityToken = activityToken;
        IsTransitioning = isTransitioning;
        Revision = revision;
    }
}

public readonly struct StrategicHubRequestResult
{
    public StrategicHubRequestStatus Status { get; }
    public StrategicHubRequestError Error { get; }
    public StrategicHubSnapshot Current { get; }
    public bool HasExpeditionRequest { get; }
    public ExpeditionStartRequest ExpeditionRequest { get; }

    internal StrategicHubRequestResult(
        StrategicHubRequestStatus status,
        StrategicHubRequestError error,
        StrategicHubSnapshot current,
        bool hasExpeditionRequest,
        ExpeditionStartRequest expeditionRequest)
    {
        Status = status;
        Error = error;
        Current = current;
        HasExpeditionRequest = hasExpeditionRequest;
        ExpeditionRequest = expeditionRequest;
    }

    public static StrategicHubRequestResult Reject(
        StrategicHubRequestError error,
        StrategicHubSnapshot current)
    {
        if (current == null)
            throw new ArgumentNullException(nameof(current));
        switch (error)
        {
            case StrategicHubRequestError.InvalidIntent:
            case StrategicHubRequestError.InvalidPayload:
            case StrategicHubRequestError.UnknownDestination:
            case StrategicHubRequestError.InvalidRoute:
            case StrategicHubRequestError.Busy:
            case StrategicHubRequestError.StaleCompletion:
                return new StrategicHubRequestResult(
                    StrategicHubRequestStatus.Rejected,
                    error, current, false, default);
            default:
                throw new ArgumentOutOfRangeException(nameof(error));
        }
    }
}

public readonly struct StrategicHubTransition
{
    public StrategicHubSnapshot Before { get; }
    public StrategicHubSnapshot After { get; }
    public StrategicHubIntent Intent { get; }

    internal StrategicHubTransition(
        StrategicHubSnapshot before,
        StrategicHubSnapshot after,
        StrategicHubIntent intent)
    {
        Before = before;
        After = after;
        Intent = intent;
    }
}

public interface IStrategicHubFlow
{
    StrategicHubSnapshot Current { get; }
    StrategicHubRequestResult Request(StrategicHubIntent intent);
    event Action<StrategicHubTransition> Transitioned;
}
