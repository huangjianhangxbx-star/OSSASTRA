using System;

public readonly struct ExpeditionId : IEquatable<ExpeditionId>
{
    public string Value { get; }
    public bool IsValid => !string.IsNullOrWhiteSpace(Value);

    public ExpeditionId(string value)
    {
        Value = value ?? string.Empty;
    }

    public bool Equals(ExpeditionId other) => string.Equals(
        Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object obj) =>
        obj is ExpeditionId other && Equals(other);
    public override int GetHashCode() =>
        StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
}

public readonly struct ExpeditionStartRequest
{
    public ExpeditionId ExpeditionId { get; }
    public bool IsValid => ExpeditionId.IsValid;

    public ExpeditionStartRequest(ExpeditionId expeditionId)
    {
        ExpeditionId = expeditionId;
    }
}

public enum AnkaluteGameLoopPhase
{
    Invalid = 0,
    Title = 1,
    StrategicHub = 2,
    ExpeditionExploration = 3,
    Battle = 4,
    BattleSettlement = 5
}

public enum SessionEntryReason
{
    Invalid = 0,
    NewGame = 1,
    LoadedGame = 2,
    ReenterAfterTitle = 3
}

public enum BattleResolution
{
    Invalid = 0,
    Victory = 1,
    Escaped = 2,
    Aborted = 3,
    PartyDefeated = 4
}

public enum ExpeditionExitReason
{
    Invalid = 0,
    Completed = 1,
    Retreated = 2
}

public enum AnkaluteGameLoopIntentKind
{
    Invalid = 0,
    EnterStrategicHub = 1,
    LaunchExpedition = 2,
    BeginBattle = 3,
    ResolveBattle = 4,
    CompleteBattleSettlement = 5,
    EndExpedition = 6,
    ReturnToTitle = 7
}

public enum AnkaluteGameLoopRequestStatus
{
    Invalid = 0,
    Applied = 1,
    NoOp = 2,
    Rejected = 3
}

public enum AnkaluteGameLoopRequestError
{
    None = 0,
    InvalidIntent = 1,
    InvalidPhase = 2,
    InvalidPayload = 3,
    Busy = 4,
    StrategicHubRejected = 5
}

public readonly struct EncounterId : IEquatable<EncounterId>
{
    public string Value { get; }
    public bool IsValid => !string.IsNullOrWhiteSpace(Value);

    public EncounterId(string value)
    {
        Value = value ?? string.Empty;
    }

    public bool Equals(EncounterId other) => string.Equals(
        Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object obj) =>
        obj is EncounterId other && Equals(other);
    public override int GetHashCode() =>
        StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
}

public readonly struct EncounterStartRequest
{
    public EncounterId EncounterId { get; }
    public bool IsValid => EncounterId.IsValid;

    public EncounterStartRequest(EncounterId encounterId)
    {
        EncounterId = encounterId;
    }
}

public readonly struct AnkaluteGameLoopIntent
{
    public AnkaluteGameLoopIntentKind Kind { get; }
    public SessionEntryReason SessionEntryReason { get; }
    public ExpeditionStartRequest ExpeditionRequest { get; }
    public EncounterStartRequest EncounterRequest { get; }
    public BattleResolution Resolution { get; }
    public ExpeditionExitReason ExitReason { get; }

    private AnkaluteGameLoopIntent(
        AnkaluteGameLoopIntentKind kind,
        SessionEntryReason sessionEntryReason,
        ExpeditionStartRequest expeditionRequest,
        EncounterStartRequest encounterRequest,
        BattleResolution resolution,
        ExpeditionExitReason exitReason)
    {
        Kind = kind;
        SessionEntryReason = sessionEntryReason;
        ExpeditionRequest = expeditionRequest;
        EncounterRequest = encounterRequest;
        Resolution = resolution;
        ExitReason = exitReason;
    }

    public static AnkaluteGameLoopIntent EnterStrategicHub(
        SessionEntryReason reason) => new AnkaluteGameLoopIntent(
            AnkaluteGameLoopIntentKind.EnterStrategicHub,
            reason, default, default, default, default);
    public static AnkaluteGameLoopIntent LaunchExpedition(
        ExpeditionStartRequest request) => new AnkaluteGameLoopIntent(
            AnkaluteGameLoopIntentKind.LaunchExpedition,
            default, request, default, default, default);
    public static AnkaluteGameLoopIntent BeginBattle(
        EncounterStartRequest request) => new AnkaluteGameLoopIntent(
            AnkaluteGameLoopIntentKind.BeginBattle,
            default, default, request, default, default);
    public static AnkaluteGameLoopIntent ResolveBattle(
        BattleResolution resolution) => new AnkaluteGameLoopIntent(
            AnkaluteGameLoopIntentKind.ResolveBattle,
            default, default, default, resolution, default);
    public static AnkaluteGameLoopIntent CompleteBattleSettlement() =>
        new AnkaluteGameLoopIntent(
            AnkaluteGameLoopIntentKind.CompleteBattleSettlement,
            default, default, default, default, default);
    public static AnkaluteGameLoopIntent EndExpedition(
        ExpeditionExitReason reason) => new AnkaluteGameLoopIntent(
            AnkaluteGameLoopIntentKind.EndExpedition,
            default, default, default, default, reason);
    public static AnkaluteGameLoopIntent ReturnToTitle() =>
        new AnkaluteGameLoopIntent(
            AnkaluteGameLoopIntentKind.ReturnToTitle,
            default, default, default, default, default);
}

public sealed class AnkaluteGameLoopSnapshot
{
    public AnkaluteGameLoopPhase Phase { get; }
    public bool HasExpedition { get; }
    public ExpeditionId ExpeditionId { get; }
    public bool IsTransitioning { get; }
    public long Revision { get; }

    internal AnkaluteGameLoopSnapshot(
        AnkaluteGameLoopPhase phase,
        bool hasExpedition,
        ExpeditionId expeditionId,
        bool isTransitioning,
        long revision)
    {
        Phase = phase;
        HasExpedition = hasExpedition;
        ExpeditionId = expeditionId;
        IsTransitioning = isTransitioning;
        Revision = revision;
    }
}

public readonly struct AnkaluteGameLoopRequestResult
{
    public AnkaluteGameLoopRequestStatus Status { get; }
    public AnkaluteGameLoopRequestError Error { get; }
    public AnkaluteGameLoopSnapshot Current { get; }

    internal AnkaluteGameLoopRequestResult(
        AnkaluteGameLoopRequestStatus status,
        AnkaluteGameLoopRequestError error,
        AnkaluteGameLoopSnapshot current)
    {
        Status = status;
        Error = error;
        Current = current;
    }
}

public readonly struct AnkaluteGameLoopTransition
{
    public AnkaluteGameLoopSnapshot Before { get; }
    public AnkaluteGameLoopSnapshot After { get; }
    public AnkaluteGameLoopIntent Intent { get; }

    internal AnkaluteGameLoopTransition(
        AnkaluteGameLoopSnapshot before,
        AnkaluteGameLoopSnapshot after,
        AnkaluteGameLoopIntent intent)
    {
        Before = before;
        After = after;
        Intent = intent;
    }
}

public interface IAnkaluteGameLoop
{
    AnkaluteGameLoopSnapshot Current { get; }
    AnkaluteGameLoopRequestResult Request(AnkaluteGameLoopIntent intent);
    event Action<AnkaluteGameLoopTransition> Transitioned;
}
