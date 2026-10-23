using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

internal enum BattleTransactionCompletionKind { None, BattleStart, RootAction, TurnEntry, LegacyAction, WaveTransition }
internal enum ContinuationSubmitStatus { Rejected, Accepted, Duplicate }

internal readonly struct ContinuationSubmitResult
{
    internal ContinuationSubmitResult(ContinuationSubmitStatus status, string error = "")
    { Status = status; Error = error ?? string.Empty; }
    internal ContinuationSubmitStatus Status { get; }
    internal string Error { get; }
    internal bool IsAccepted => Status == ContinuationSubmitStatus.Accepted || Status == ContinuationSubmitStatus.Duplicate;
}

internal interface IBattleTransactionCompletionPayload
{
    BattleTransactionIdentity Owner { get; }
    BattleTransactionCompletionKind Kind { get; }
    DRPGBattleDepartureJournal Departures { get; }
    ulong Fingerprint { get; }
}

internal interface IBattleContinuationPort
{
    ContinuationSubmitResult Submit<TCompletion>(in TCompletion completion)
        where TCompletion : struct, IBattleTransactionCompletionPayload;
}

internal readonly struct BattleStartCompletion : IBattleTransactionCompletionPayload
{
    internal BattleStartCompletion(BattleStartIdentity identity, DRPGBattleDepartureJournal departures, ulong fingerprint)
    {
        if (!identity.IsValid) throw new ArgumentException("Startup identity is required.", nameof(identity));
        var owner = BattleTransactionIdentity.ForBattleStart(identity);
        if (departures == null || departures.Owner != owner || departures.Members.Count != 0 ||
            fingerprint == 0 || departures.SourceFingerprint != fingerprint)
            throw new ArgumentException("Startup requires its own explicit empty departure journal and matching fingerprint.", nameof(departures));
        Identity = identity; Departures = departures; Fingerprint = fingerprint;
    }
    public BattleTransactionCompletionKind Kind => BattleTransactionCompletionKind.BattleStart;
    public BattleTransactionIdentity Owner => Identity.IsValid ? BattleTransactionIdentity.ForBattleStart(Identity) : default;
    public BattleStartIdentity Identity { get; }
    public DRPGBattleDepartureJournal Departures { get; }
    public ulong Fingerprint { get; }
}

public readonly struct ParticipantId : IEquatable<ParticipantId>
{
    public ParticipantId(int value)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
    }

    public int Value { get; }

    public bool Equals(ParticipantId other) => Value == other.Value;
    public override bool Equals(object obj) => obj is ParticipantId other && Equals(other);
    public override int GetHashCode() => Value;
    public override string ToString() => Value.ToString();
    public static bool operator ==(ParticipantId left, ParticipantId right) => left.Equals(right);
    public static bool operator !=(ParticipantId left, ParticipantId right) => !left.Equals(right);
}

public readonly struct BattleStartIdentity : IEquatable<BattleStartIdentity>
{
    public BattleStartIdentity(long battleGeneration, long startSerial)
    {
        RequirePositive(battleGeneration, nameof(battleGeneration));
        RequirePositive(startSerial, nameof(startSerial));
        BattleGeneration = battleGeneration;
        StartSerial = startSerial;
    }

    public long BattleGeneration { get; }
    public long StartSerial { get; }
    public bool IsValid => BattleGeneration > 0L && StartSerial > 0L;
    public bool Equals(BattleStartIdentity other) =>
        BattleGeneration == other.BattleGeneration && StartSerial == other.StartSerial;
    public override bool Equals(object obj) => obj is BattleStartIdentity other && Equals(other);
    public override int GetHashCode() => Combine(BattleGeneration, StartSerial);
    public static bool operator ==(BattleStartIdentity left, BattleStartIdentity right) => left.Equals(right);
    public static bool operator !=(BattleStartIdentity left, BattleStartIdentity right) => !left.Equals(right);

    private static void RequirePositive(long value, string name)
    {
        if (value <= 0L)
            throw new ArgumentOutOfRangeException(name);
    }

    private static int Combine(long first, long second)
    {
        unchecked { return (first.GetHashCode() * 397) ^ second.GetHashCode(); }
    }
}

public readonly struct ActorTurnToken : IEquatable<ActorTurnToken>
{
    public ActorTurnToken(long battleGeneration, long turnSerial, ParticipantId actor)
    {
        RequirePositive(battleGeneration, nameof(battleGeneration));
        RequirePositive(turnSerial, nameof(turnSerial));
        if (actor.Value <= 0)
            throw new ArgumentException("Actor identity is required.", nameof(actor));
        BattleGeneration = battleGeneration;
        TurnSerial = turnSerial;
        Actor = actor;
    }

    public long BattleGeneration { get; }
    public long TurnSerial { get; }
    public ParticipantId Actor { get; }
    public bool IsValid => BattleGeneration > 0L && TurnSerial > 0L && Actor.Value > 0;
    public bool Equals(ActorTurnToken other) =>
        BattleGeneration == other.BattleGeneration &&
        TurnSerial == other.TurnSerial && Actor == other.Actor;
    public override bool Equals(object obj) => obj is ActorTurnToken other && Equals(other);
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = BattleGeneration.GetHashCode();
            hash = (hash * 397) ^ TurnSerial.GetHashCode();
            return (hash * 397) ^ Actor.GetHashCode();
        }
    }
    public static bool operator ==(ActorTurnToken left, ActorTurnToken right) => left.Equals(right);
    public static bool operator !=(ActorTurnToken left, ActorTurnToken right) => !left.Equals(right);

    private static void RequirePositive(long value, string name)
    {
        if (value <= 0L)
            throw new ArgumentOutOfRangeException(name);
    }
}

public readonly struct TargetSelectionToken : IEquatable<TargetSelectionToken>
{
    public TargetSelectionToken(
        long battleGeneration,
        long turnSerial,
        long selectionId,
        ParticipantId actor)
    {
        RequirePositive(battleGeneration, nameof(battleGeneration));
        RequirePositive(turnSerial, nameof(turnSerial));
        RequirePositive(selectionId, nameof(selectionId));
        if (actor.Value <= 0)
            throw new ArgumentException("Actor identity is required.", nameof(actor));
        BattleGeneration = battleGeneration;
        TurnSerial = turnSerial;
        SelectionId = selectionId;
        Actor = actor;
    }

    public long BattleGeneration { get; }
    public long TurnSerial { get; }
    public long SelectionId { get; }
    public ParticipantId Actor { get; }
    public bool IsValid => BattleGeneration > 0L && TurnSerial > 0L &&
        SelectionId > 0L && Actor.Value > 0;
    public bool Equals(TargetSelectionToken other) =>
        BattleGeneration == other.BattleGeneration &&
        TurnSerial == other.TurnSerial &&
        SelectionId == other.SelectionId && Actor == other.Actor;
    public override bool Equals(object obj) => obj is TargetSelectionToken other && Equals(other);
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = BattleGeneration.GetHashCode();
            hash = (hash * 397) ^ TurnSerial.GetHashCode();
            hash = (hash * 397) ^ SelectionId.GetHashCode();
            return (hash * 397) ^ Actor.GetHashCode();
        }
    }
    public static bool operator ==(TargetSelectionToken left, TargetSelectionToken right) => left.Equals(right);
    public static bool operator !=(TargetSelectionToken left, TargetSelectionToken right) => !left.Equals(right);

    private static void RequirePositive(long value, string name)
    {
        if (value <= 0L)
            throw new ArgumentOutOfRangeException(name);
    }
}

public readonly struct ActionIdentity : IEquatable<ActionIdentity>
{
    public ActionIdentity(long battleGeneration, long turnSerial, long actionId)
    {
        RequirePositive(battleGeneration, nameof(battleGeneration));
        RequirePositive(turnSerial, nameof(turnSerial));
        RequirePositive(actionId, nameof(actionId));
        BattleGeneration = battleGeneration;
        TurnSerial = turnSerial;
        ActionId = actionId;
    }

    public long BattleGeneration { get; }
    public long TurnSerial { get; }
    public long ActionId { get; }
    public bool IsValid => BattleGeneration > 0L && TurnSerial > 0L && ActionId > 0L;
    public bool Equals(ActionIdentity other) =>
        BattleGeneration == other.BattleGeneration &&
        TurnSerial == other.TurnSerial && ActionId == other.ActionId;
    public override bool Equals(object obj) => obj is ActionIdentity other && Equals(other);
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = BattleGeneration.GetHashCode();
            hash = (hash * 397) ^ TurnSerial.GetHashCode();
            return (hash * 397) ^ ActionId.GetHashCode();
        }
    }
    public override string ToString() => BattleGeneration + ":" + TurnSerial + ":" + ActionId;
    public static bool operator ==(ActionIdentity left, ActionIdentity right) => left.Equals(right);
    public static bool operator !=(ActionIdentity left, ActionIdentity right) => !left.Equals(right);

    private static void RequirePositive(long value, string name)
    {
        if (value <= 0L)
            throw new ArgumentOutOfRangeException(name);
    }
}

public readonly struct TurnEntryIdentity : IEquatable<TurnEntryIdentity>
{
    public TurnEntryIdentity(long battleGeneration, long candidateSerial, ParticipantId actor)
    {
        RequirePositive(battleGeneration, nameof(battleGeneration));
        RequirePositive(candidateSerial, nameof(candidateSerial));
        if (actor.Value <= 0)
            throw new ArgumentException("Candidate actor identity is required.", nameof(actor));
        BattleGeneration = battleGeneration;
        CandidateSerial = candidateSerial;
        Actor = actor;
    }

    public long BattleGeneration { get; }
    public long CandidateSerial { get; }
    public ParticipantId Actor { get; }
    public bool IsValid => BattleGeneration > 0L && CandidateSerial > 0L && Actor.Value > 0;
    public bool Equals(TurnEntryIdentity other) =>
        BattleGeneration == other.BattleGeneration &&
        CandidateSerial == other.CandidateSerial && Actor == other.Actor;
    public override bool Equals(object obj) => obj is TurnEntryIdentity other && Equals(other);
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = BattleGeneration.GetHashCode();
            hash = (hash * 397) ^ CandidateSerial.GetHashCode();
            return (hash * 397) ^ Actor.GetHashCode();
        }
    }
    public static bool operator ==(TurnEntryIdentity left, TurnEntryIdentity right) => left.Equals(right);
    public static bool operator !=(TurnEntryIdentity left, TurnEntryIdentity right) => !left.Equals(right);

    private static void RequirePositive(long value, string name)
    {
        if (value <= 0L)
            throw new ArgumentOutOfRangeException(name);
    }
}

public readonly struct WaveTransitionIdentity : IEquatable<WaveTransitionIdentity>
{
    public WaveTransitionIdentity(long battleGeneration, long transitionId, long waveId)
    {
        RequirePositive(battleGeneration, nameof(battleGeneration));
        RequirePositive(transitionId, nameof(transitionId));
        RequirePositive(waveId, nameof(waveId));
        BattleGeneration = battleGeneration;
        TransitionId = transitionId;
        WaveId = waveId;
    }

    public long BattleGeneration { get; }
    public long TransitionId { get; }
    public long WaveId { get; }
    public bool IsValid => BattleGeneration > 0L && TransitionId > 0L && WaveId > 0L;
    public bool Equals(WaveTransitionIdentity other) =>
        BattleGeneration == other.BattleGeneration &&
        TransitionId == other.TransitionId && WaveId == other.WaveId;
    public override bool Equals(object obj) => obj is WaveTransitionIdentity other && Equals(other);
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = BattleGeneration.GetHashCode();
            hash = (hash * 397) ^ TransitionId.GetHashCode();
            return (hash * 397) ^ WaveId.GetHashCode();
        }
    }
    public static bool operator ==(WaveTransitionIdentity left, WaveTransitionIdentity right) => left.Equals(right);
    public static bool operator !=(WaveTransitionIdentity left, WaveTransitionIdentity right) => !left.Equals(right);

    private static void RequirePositive(long value, string name)
    {
        if (value <= 0L)
            throw new ArgumentOutOfRangeException(name);
    }
}

public enum BattleTransactionKind
{
    None,
    BattleStart,
    RootAction,
    TurnEntry,
    LegacyAction,
    WaveTransition,
    TestHarness
}

public readonly struct BattleTransactionIdentity : IEquatable<BattleTransactionIdentity>
{
    private BattleTransactionIdentity(
        BattleTransactionKind kind,
        BattleStartIdentity battleStart,
        ActionIdentity action,
        TurnEntryIdentity turnEntry,
        WaveTransitionIdentity wave)
    {
        Kind = kind;
        BattleStart = battleStart;
        Action = action;
        TurnEntry = turnEntry;
        WaveTransition = wave;
    }

    public BattleTransactionKind Kind { get; }
    public BattleStartIdentity BattleStart { get; }
    public ActionIdentity Action { get; }
    public TurnEntryIdentity TurnEntry { get; }
    public WaveTransitionIdentity WaveTransition { get; }
    public bool IsValid
    {
        get
        {
            switch (Kind)
            {
                case BattleTransactionKind.BattleStart:
                case BattleTransactionKind.TestHarness:
                    return BattleStart.IsValid;
                case BattleTransactionKind.RootAction:
                case BattleTransactionKind.LegacyAction:
                    return Action.IsValid;
                case BattleTransactionKind.TurnEntry:
                    return TurnEntry.IsValid;
                case BattleTransactionKind.WaveTransition:
                    return WaveTransition.IsValid;
                default:
                    return false;
            }
        }
    }

    public long BattleGeneration
    {
        get
        {
            switch (Kind)
            {
                case BattleTransactionKind.BattleStart:
                case BattleTransactionKind.TestHarness:
                    return BattleStart.BattleGeneration;
                case BattleTransactionKind.RootAction:
                case BattleTransactionKind.LegacyAction:
                    return Action.BattleGeneration;
                case BattleTransactionKind.TurnEntry:
                    return TurnEntry.BattleGeneration;
                case BattleTransactionKind.WaveTransition:
                    return WaveTransition.BattleGeneration;
                default:
                    return 0L;
            }
        }
    }

    public static BattleTransactionIdentity ForBattleStart(
        BattleStartIdentity identity)
    {
        if (!identity.IsValid)
            throw new ArgumentException(
                "Battle-start identity is required.",
                nameof(identity));
        return new BattleTransactionIdentity(
            BattleTransactionKind.BattleStart,
            identity,
            default,
            default,
            default);
    }

    public static BattleTransactionIdentity ForRootAction(ActionIdentity identity)
    {
        if (!identity.IsValid)
            throw new ArgumentException(
                "Root-action identity is required.",
                nameof(identity));
        return new BattleTransactionIdentity(
            BattleTransactionKind.RootAction,
            default,
            identity,
            default,
            default);
    }

    public static BattleTransactionIdentity ForLegacyAction(ActionIdentity identity)
    {
        if (!identity.IsValid)
            throw new ArgumentException(
                "Legacy-action identity is required.",
                nameof(identity));
        return new BattleTransactionIdentity(
            BattleTransactionKind.LegacyAction,
            default,
            identity,
            default,
            default);
    }

    public static BattleTransactionIdentity ForTurnEntry(TurnEntryIdentity identity)
    {
        if (!identity.IsValid)
            throw new ArgumentException(
                "Turn-entry identity is required.",
                nameof(identity));
        return new BattleTransactionIdentity(
            BattleTransactionKind.TurnEntry,
            default,
            default,
            identity,
            default);
    }

    public static BattleTransactionIdentity ForWave(WaveTransitionIdentity identity)
    {
        if (!identity.IsValid)
            throw new ArgumentException(
                "Wave-transition identity is required.",
                nameof(identity));
        return new BattleTransactionIdentity(
            BattleTransactionKind.WaveTransition,
            default,
            default,
            default,
            identity);
    }

    internal static BattleTransactionIdentity ForTestHarness(
        BattleStartIdentity identity)
    {
        if (!identity.IsValid)
        {
            throw new ArgumentException(
                "Test-harness identity is required.",
                nameof(identity));
        }
        return new BattleTransactionIdentity(
            BattleTransactionKind.TestHarness,
            identity,
            default,
            default,
            default);
    }

    public bool Equals(BattleTransactionIdentity other) =>
        Kind == other.Kind && BattleStart == other.BattleStart &&
        Action == other.Action && TurnEntry == other.TurnEntry &&
        WaveTransition == other.WaveTransition;
    public override bool Equals(object obj) => obj is BattleTransactionIdentity other && Equals(other);
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = (int)Kind;
            hash = (hash * 397) ^ BattleStart.GetHashCode();
            hash = (hash * 397) ^ Action.GetHashCode();
            hash = (hash * 397) ^ TurnEntry.GetHashCode();
            return (hash * 397) ^ WaveTransition.GetHashCode();
        }
    }
    public override string ToString() => Kind + "@" + BattleGeneration;
    public static bool operator ==(BattleTransactionIdentity left, BattleTransactionIdentity right) => left.Equals(right);
    public static bool operator !=(BattleTransactionIdentity left, BattleTransactionIdentity right) => !left.Equals(right);
}

public readonly struct BasicAttackSelection
{
    public BasicAttackSelection(TargetSelectionToken selection, ParticipantId target)
    {
        if (!selection.IsValid)
            throw new ArgumentException("Selection identity is required.", nameof(selection));
        if (target.Value <= 0)
            throw new ArgumentException("Target identity is required.", nameof(target));
        Selection = selection;
        Target = target;
    }

    public TargetSelectionToken Selection { get; }
    public ParticipantId Target { get; }
}

public enum BasicAttackRejection
{
    None,
    Busy,
    StaleTurnToken,
    StaleSelection,
    NotCurrentActor,
    WrongSide,
    IllegalTarget,
    BasicAttackUnavailable,
    BattleEnded,
    PresentationUnavailable
}

public readonly struct TargetSelectionOpenResult
{
    private TargetSelectionOpenResult(
        bool accepted,
        TargetSelectionToken selection,
        BasicAttackRejection rejection)
    {
        IsAccepted = accepted;
        Selection = selection;
        Rejection = rejection;
    }

    public bool IsAccepted { get; }
    public TargetSelectionToken Selection { get; }
    public BasicAttackRejection Rejection { get; }

    public static TargetSelectionOpenResult Accepted(
        TargetSelectionToken selection)
    {
        if (!selection.IsValid)
            throw new ArgumentException(
                "A valid target-selection token is required.",
                nameof(selection));
        return new TargetSelectionOpenResult(
            true,
            selection,
            BasicAttackRejection.None);
    }

    public static TargetSelectionOpenResult Rejected(
        BasicAttackRejection rejection)
    {
        if (rejection == BasicAttackRejection.None)
            throw new ArgumentOutOfRangeException(nameof(rejection));
        return new TargetSelectionOpenResult(false, default, rejection);
    }
}

public readonly struct TargetSelectionCancelResult
{
    private TargetSelectionCancelResult(
        bool cancelled,
        BasicAttackRejection rejection)
    {
        IsCancelled = cancelled;
        Rejection = rejection;
    }

    public bool IsCancelled { get; }
    public BasicAttackRejection Rejection { get; }

    public static TargetSelectionCancelResult Cancelled()
    {
        return new TargetSelectionCancelResult(
            true,
            BasicAttackRejection.None);
    }

    public static TargetSelectionCancelResult Rejected(
        BasicAttackRejection rejection)
    {
        if (rejection == BasicAttackRejection.None)
            throw new ArgumentOutOfRangeException(nameof(rejection));
        return new TargetSelectionCancelResult(false, rejection);
    }
}

public enum RootActionTerminalKind
{
    Completed,
    DegradedCompleted,
    Faulted
}

public readonly struct RootActionTerminal
{
    public RootActionTerminal(RootActionTerminalKind kind, string detail)
    {
        Kind = kind;
        Detail = detail ?? string.Empty;
    }

    public RootActionTerminalKind Kind { get; }
    public string Detail { get; }
}

public readonly struct RootActionStartResult
{
    private RootActionStartResult(
        bool accepted,
        RootActionReceipt receipt,
        BasicAttackRejection rejection)
    {
        IsAccepted = accepted;
        Receipt = receipt;
        Rejection = rejection;
    }

    public bool IsAccepted { get; }
    public RootActionReceipt Receipt { get; }
    public BasicAttackRejection Rejection { get; }

    public static RootActionStartResult Accepted(RootActionReceipt receipt)
    {
        if (receipt == null)
            throw new ArgumentNullException(nameof(receipt));
        return new RootActionStartResult(true, receipt, BasicAttackRejection.None);
    }

    public static RootActionStartResult Rejected(BasicAttackRejection rejection)
    {
        if (rejection == BasicAttackRejection.None)
            throw new ArgumentOutOfRangeException(nameof(rejection));
        return new RootActionStartResult(false, null, rejection);
    }
}

public interface IStandardBasicAttackTransactions
{
    TargetSelectionOpenResult TryOpenBasicAttackSelection(
        in ActorTurnToken actorTurn);
    TargetSelectionCancelResult TryCancel(
        in TargetSelectionToken selection);
    RootActionStartResult TryStart(in BasicAttackSelection selection);
}

public enum DRPGBattleActionStage
{
    None,
    TurnBegin,
    Selection,
    Locked,
    Announcement,
    ConflictComposition,
    Windup,
    ImpactAndReactions,
    Return,
    Settlement,
    Finalized
}

public readonly struct PresentationReceiptId : IEquatable<PresentationReceiptId>
{
    public PresentationReceiptId(long value)
    {
        if (value <= 0L)
            throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
    }

    public long Value { get; }
    public bool IsValid => Value > 0L;
    public bool Equals(PresentationReceiptId other) => Value == other.Value;
    public override bool Equals(object obj) => obj is PresentationReceiptId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public static bool operator ==(PresentationReceiptId left, PresentationReceiptId right) => left.Equals(right);
    public static bool operator !=(PresentationReceiptId left, PresentationReceiptId right) => !left.Equals(right);
}

public enum ActionTraceEventKind
{
    SelectionOpened,
    SelectionConfirmed,
    ActionLocked,
    PhaseEntered,
    ReceiptIssued,
    ReceiptCompleted,
    ImpactCommitted,
    ReactionQueued,
    ReactionCompleted,
    DefeatDeferred,
    ReturnStarted,
    ReturnBarrierCompleted,
    RosterReconciled,
    SettlementItem,
    WatchdogExpired,
    Finalized,
    StaleCallbackDropped
}

public enum ActionTraceIdentityKind
{
    None,
    ActorTurn,
    TargetSelection,
    Transaction
}

public readonly struct ActionTraceIdentity
{
    private ActionTraceIdentity(
        ActionTraceIdentityKind kind,
        ActorTurnToken turn,
        TargetSelectionToken selection,
        BattleTransactionIdentity transaction)
    {
        Kind = kind;
        Turn = turn;
        Selection = selection;
        Transaction = transaction;
    }

    public ActionTraceIdentityKind Kind { get; }
    public ActorTurnToken Turn { get; }
    public TargetSelectionToken Selection { get; }
    public BattleTransactionIdentity Transaction { get; }
    public bool IsValid =>
        Kind == ActionTraceIdentityKind.ActorTurn && Turn.IsValid ||
        Kind == ActionTraceIdentityKind.TargetSelection && Selection.IsValid ||
        Kind == ActionTraceIdentityKind.Transaction && Transaction.IsValid;

    public static ActionTraceIdentity ForTurn(ActorTurnToken turn)
    {
        if (!turn.IsValid)
            throw new ArgumentException("Turn trace identity is required.", nameof(turn));
        return new ActionTraceIdentity(
            ActionTraceIdentityKind.ActorTurn,
            turn,
            default,
            default);
    }

    public static ActionTraceIdentity ForSelection(TargetSelectionToken selection)
    {
        if (!selection.IsValid)
        {
            throw new ArgumentException(
                "Selection trace identity is required.",
                nameof(selection));
        }
        return new ActionTraceIdentity(
            ActionTraceIdentityKind.TargetSelection,
            default,
            selection,
            default);
    }

    public static ActionTraceIdentity ForTransaction(
        BattleTransactionIdentity transaction)
    {
        if (!transaction.IsValid)
        {
            throw new ArgumentException(
                "Transaction trace identity is required.",
                nameof(transaction));
        }
        return new ActionTraceIdentity(
            ActionTraceIdentityKind.Transaction,
            default,
            default,
            transaction);
    }
}

public readonly struct ActionTracePosition
{
    public ActionTracePosition(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
        IsSpecified = true;
    }

    public float X { get; }
    public float Y { get; }
    public float Z { get; }
    public bool IsSpecified { get; }
}

public readonly struct ActionTraceProjection
{
    public ActionTraceProjection(
        int hp,
        int posture,
        bool isAlive,
        bool isDeployed)
    {
        HP = hp;
        Posture = posture;
        IsAlive = isAlive;
        IsDeployed = isDeployed;
        IsSpecified = true;
    }

    public int HP { get; }
    public int Posture { get; }
    public bool IsAlive { get; }
    public bool IsDeployed { get; }
    public bool IsSpecified { get; }
}

public enum ActionTraceHitOutcome
{
    None,
    Hit,
    Guarded,
    Evaded
}

public enum ActionTraceReceiptStatus
{
    None,
    Issued,
    Completed,
    Degraded,
    Faulted
}

public readonly struct ActionTraceContext
{
    public ActionTraceContext(
        ActorTurnToken precursorTurn = default,
        TargetSelectionToken precursorSelection = default,
        ParticipantId actor = default,
        ParticipantId target = default,
        ParticipantId source = default,
        DRPGBattleSide? side = null,
        ActionTracePosition homePosition = default,
        ActionTracePosition conflictPosition = default,
        string fingerprint = null,
        ActionTraceHitOutcome hitOutcome = ActionTraceHitOutcome.None,
        ActionTraceProjection beforeProjection = default,
        ActionTraceProjection afterProjection = default,
        PresentationReceiptId receiptId = default,
        ActionTraceReceiptStatus receiptStatus =
            ActionTraceReceiptStatus.None,
        string leaseId = null,
        string barrierId = null,
        string watchdogId = null,
        string staleReason = null,
        string detail = null)
    {
        PrecursorTurn = precursorTurn;
        PrecursorSelection = precursorSelection;
        Actor = actor;
        Target = target;
        Source = source;
        Side = side;
        HomePosition = homePosition;
        ConflictPosition = conflictPosition;
        Fingerprint = fingerprint ?? string.Empty;
        HitOutcome = hitOutcome;
        BeforeProjection = beforeProjection;
        AfterProjection = afterProjection;
        ReceiptId = receiptId;
        ReceiptStatus = receiptStatus;
        LeaseId = leaseId ?? string.Empty;
        BarrierId = barrierId ?? string.Empty;
        WatchdogId = watchdogId ?? string.Empty;
        StaleReason = staleReason ?? string.Empty;
        Detail = detail ?? string.Empty;
    }

    public ActorTurnToken PrecursorTurn { get; }
    public TargetSelectionToken PrecursorSelection { get; }
    public ParticipantId Actor { get; }
    public ParticipantId Target { get; }
    public ParticipantId Source { get; }
    public DRPGBattleSide? Side { get; }
    public ActionTracePosition HomePosition { get; }
    public ActionTracePosition ConflictPosition { get; }
    public string Fingerprint { get; }
    public ActionTraceHitOutcome HitOutcome { get; }
    public ActionTraceProjection BeforeProjection { get; }
    public ActionTraceProjection AfterProjection { get; }
    public PresentationReceiptId ReceiptId { get; }
    public ActionTraceReceiptStatus ReceiptStatus { get; }
    public string LeaseId { get; }
    public string BarrierId { get; }
    public string WatchdogId { get; }
    public string StaleReason { get; }
    public string Detail { get; }
}

public readonly struct ActionTraceEntry
{
    public ActionTraceEntry(
        long sequence,
        double logicalTimestamp,
        double presentationTimestamp,
        BattleTransactionIdentity owner,
        ActionTraceEventKind eventKind,
        DRPGBattleActionStage stage,
        int stepOrdinal,
        PresentationReceiptId receiptId,
        string eventId,
        string parentEventId,
        string fingerprint,
        string detail)
        : this(
            sequence,
            logicalTimestamp,
            presentationTimestamp,
            ActionTraceIdentity.ForTransaction(owner),
            eventKind,
            stage,
            stepOrdinal,
            eventId,
            parentEventId,
            new ActionTraceContext(
                fingerprint: fingerprint,
                receiptId: receiptId,
                receiptStatus: InferReceiptStatus(eventKind),
                staleReason: eventKind ==
                    ActionTraceEventKind.StaleCallbackDropped
                        ? detail
                        : null,
                detail: detail))
    {
    }

    public ActionTraceEntry(
        long sequence,
        double logicalTimestamp,
        double presentationTimestamp,
        ActionTraceIdentity traceIdentity,
        ActionTraceEventKind eventKind,
        DRPGBattleActionStage stage,
        int stepOrdinal,
        string eventId,
        string parentEventId,
        ActionTraceContext context)
    {
        if (sequence <= 0L)
            throw new ArgumentOutOfRangeException(nameof(sequence));
        if (!traceIdentity.IsValid)
        {
            throw new ArgumentException(
                "Trace identity is required.",
                nameof(traceIdentity));
        }
        if (stepOrdinal < 0)
            throw new ArgumentOutOfRangeException(nameof(stepOrdinal));
        Sequence = sequence;
        LogicalTimestamp = logicalTimestamp;
        PresentationTimestamp = presentationTimestamp;
        TraceIdentity = traceIdentity;
        EventKind = eventKind;
        Stage = stage;
        StepOrdinal = stepOrdinal;
        PrecursorTurn = context.PrecursorTurn;
        PrecursorSelection = context.PrecursorSelection;
        Actor = context.Actor;
        Target = context.Target;
        Source = context.Source;
        Side = context.Side;
        HomePosition = context.HomePosition;
        ConflictPosition = context.ConflictPosition;
        HitOutcome = context.HitOutcome;
        BeforeProjection = context.BeforeProjection;
        AfterProjection = context.AfterProjection;
        ReceiptId = context.ReceiptId;
        ReceiptStatus = context.ReceiptStatus;
        LeaseId = context.LeaseId;
        BarrierId = context.BarrierId;
        WatchdogId = context.WatchdogId;
        StaleReason = context.StaleReason;
        EventId = eventId ?? string.Empty;
        ParentEventId = parentEventId ?? string.Empty;
        Fingerprint = context.Fingerprint;
        Detail = context.Detail;
    }

    public long Sequence { get; }
    public double LogicalTimestamp { get; }
    public double PresentationTimestamp { get; }
    public ActionTraceIdentity TraceIdentity { get; }
    public BattleTransactionIdentity Owner =>
        TraceIdentity.Kind == ActionTraceIdentityKind.Transaction
            ? TraceIdentity.Transaction
            : default;
    public ActionTraceEventKind EventKind { get; }
    public DRPGBattleActionStage Stage { get; }
    public int StepOrdinal { get; }
    public ActorTurnToken PrecursorTurn { get; }
    public TargetSelectionToken PrecursorSelection { get; }
    public ParticipantId Actor { get; }
    public ParticipantId Target { get; }
    public ParticipantId Source { get; }
    public DRPGBattleSide? Side { get; }
    public ActionTracePosition HomePosition { get; }
    public ActionTracePosition ConflictPosition { get; }
    public ActionTraceHitOutcome HitOutcome { get; }
    public ActionTraceProjection BeforeProjection { get; }
    public ActionTraceProjection AfterProjection { get; }
    public PresentationReceiptId ReceiptId { get; }
    public ActionTraceReceiptStatus ReceiptStatus { get; }
    public string LeaseId { get; }
    public string BarrierId { get; }
    public string WatchdogId { get; }
    public string StaleReason { get; }
    public string EventId { get; }
    public string ParentEventId { get; }
    public string Fingerprint { get; }
    public string Detail { get; }

    private static ActionTraceReceiptStatus InferReceiptStatus(
        ActionTraceEventKind eventKind)
    {
        if (eventKind == ActionTraceEventKind.ReceiptIssued)
            return ActionTraceReceiptStatus.Issued;
        if (eventKind == ActionTraceEventKind.ReceiptCompleted)
            return ActionTraceReceiptStatus.Completed;
        return ActionTraceReceiptStatus.None;
    }
}

internal interface IActionTraceSink
{
    void Append(ActionTraceEntry entry);
    IReadOnlyList<ActionTraceEntry> Snapshot();
}

internal sealed class DRPGBoundedActionTraceSink : IActionTraceSink
{
    private readonly int capacity;
    private readonly Queue<ActionTraceEntry> entries;
    private long lastSequence;

    internal DRPGBoundedActionTraceSink(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        this.capacity = capacity;
        entries = new Queue<ActionTraceEntry>(capacity);
    }

    public void Append(ActionTraceEntry entry)
    {
        if (entry.Sequence <= lastSequence)
            throw new InvalidOperationException(
                "Trace sequence must be strictly monotonic.");
        lastSequence = entry.Sequence;
        if (entries.Count == capacity)
            entries.Dequeue();
        entries.Enqueue(entry);
    }

    public IReadOnlyList<ActionTraceEntry> Snapshot() =>
        new List<ActionTraceEntry>(entries).AsReadOnly();
}

internal sealed class DRPGBattleActionTraceWriter
{
    private readonly IActionTraceSink sink;
    private readonly object writeGate = new object();
    private long nextSequence;

    internal DRPGBattleActionTraceWriter(IActionTraceSink sink)
    {
        this.sink = sink ?? throw new ArgumentNullException(nameof(sink));
    }

    internal ActionTraceEntry Record(
        double logicalTimestamp,
        double presentationTimestamp,
        BattleTransactionIdentity owner,
        ActionTraceEventKind eventKind,
        DRPGBattleActionStage stage,
        int stepOrdinal,
        PresentationReceiptId receiptId = default,
        string eventId = null,
        string parentEventId = null,
        string fingerprint = null,
        string detail = null)
    {
        lock (writeGate)
        {
            var entry = new ActionTraceEntry(
                ++nextSequence,
                logicalTimestamp,
                presentationTimestamp,
                owner,
                eventKind,
                stage,
                stepOrdinal,
                receiptId,
                eventId,
                parentEventId,
                fingerprint,
                detail);
            sink.Append(entry);
            return entry;
        }
    }

    internal ActionTraceEntry Record(
        double logicalTimestamp,
        double presentationTimestamp,
        ActionTraceIdentity traceIdentity,
        ActionTraceEventKind eventKind,
        DRPGBattleActionStage stage,
        int stepOrdinal,
        string eventId,
        string parentEventId,
        ActionTraceContext context)
    {
        lock (writeGate)
        {
            var entry = new ActionTraceEntry(
                ++nextSequence,
                logicalTimestamp,
                presentationTimestamp,
                traceIdentity,
                eventKind,
                stage,
                stepOrdinal,
                eventId,
                parentEventId,
                context);
            sink.Append(entry);
            return entry;
        }
    }

    internal IReadOnlyList<ActionTraceEntry> Snapshot()
    {
        lock (writeGate)
            return sink.Snapshot();
    }

    internal IReadOnlyList<ActionTraceEntry> SnapshotForAction(
        ActionIdentity identity)
    {
        if (!identity.IsValid)
        {
            throw new ArgumentException(
                "Action identity is required.",
                nameof(identity));
        }

        lock (writeGate)
        {
            IReadOnlyList<ActionTraceEntry> allEntries = sink.Snapshot();
            BattleTransactionIdentity owner =
                BattleTransactionIdentity.ForRootAction(identity);
            ActorTurnToken precursorTurn = default;
            TargetSelectionToken precursorSelection = default;
            for (int i = 0; i < allEntries.Count; i++)
            {
                ActionTraceEntry candidate = allEntries[i];
                if (candidate.EventKind == ActionTraceEventKind.ActionLocked &&
                    candidate.Owner == owner)
                {
                    precursorTurn = candidate.PrecursorTurn;
                    precursorSelection = candidate.PrecursorSelection;
                    break;
                }
            }

            var scoped = new List<ActionTraceEntry>();
            for (int i = 0; i < allEntries.Count; i++)
            {
                ActionTraceEntry entry = allEntries[i];
                bool belongsToAction =
                    entry.TraceIdentity.Kind ==
                        ActionTraceIdentityKind.Transaction &&
                        entry.Owner == owner ||
                    precursorTurn.IsValid &&
                        entry.TraceIdentity.Kind ==
                            ActionTraceIdentityKind.ActorTurn &&
                        entry.TraceIdentity.Turn == precursorTurn ||
                    precursorSelection.IsValid &&
                        entry.TraceIdentity.Kind ==
                            ActionTraceIdentityKind.TargetSelection &&
                        entry.TraceIdentity.Selection == precursorSelection;
                if (belongsToAction)
                    scoped.Add(entry);
            }
            return scoped.AsReadOnly();
        }
    }
}

internal static class DRPGActionTraceContractValidator
{
    internal static bool TryValidate(
        IReadOnlyList<ActionTraceEntry> entries,
        out string error)
    {
        if (entries == null || entries.Count == 0)
            return Reject("Action trace is empty.", out error);

        error = string.Empty;

        var eventsById = new Dictionary<string, ActionTraceEntry>(
            StringComparer.Ordinal);
        var issuedReceipts =
            new Dictionary<PresentationReceiptId, ActionTraceEntry>();
        var terminalReceipts =
            new Dictionary<PresentationReceiptId, ActionTraceEntry>();
        long previousSequence = 0L;
        double previousLogical = double.MinValue;
        double previousPresentation = double.MinValue;
        BattleTransactionIdentity actionOwner = default;
        bool turnBeginObserved = false;
        bool actionLocked = false;
        bool returnStarted = false;
        bool returnCompleted = false;
        bool settlementObserved = false;
        bool finalized = false;
        bool faultedTerminalObserved = false;
        string returnBarrierId = string.Empty;

        for (int i = 0; i < entries.Count; i++)
        {
            ActionTraceEntry entry = entries[i];
            if (entry.Sequence <= previousSequence)
            {
                return Reject(
                    "Trace sequence is not strictly monotonic at index " + i + ".",
                    out error);
            }
            if (entry.LogicalTimestamp < previousLogical ||
                entry.PresentationTimestamp < previousPresentation)
            {
                return Reject(
                    "Trace timestamps moved backwards at sequence " +
                    entry.Sequence + ".",
                    out error);
            }
            if (string.IsNullOrEmpty(entry.EventId) ||
                eventsById.ContainsKey(entry.EventId))
            {
                return Reject(
                    "Trace event IDs must be non-empty and unique.",
                    out error);
            }
            ActionTraceEntry parentEntry = default;
            bool hasParent = !string.IsNullOrEmpty(entry.ParentEventId);
            if (hasParent &&
                !eventsById.TryGetValue(entry.ParentEventId, out parentEntry))
            {
                return Reject(
                    "Trace parent must precede its child: " + entry.EventId + ".",
                    out error);
            }
            eventsById.Add(entry.EventId, entry);

            if (entry.EventKind == ActionTraceEventKind.PhaseEntered &&
                entry.Stage == DRPGBattleActionStage.TurnBegin)
            {
                if (turnBeginObserved || actionLocked || hasParent ||
                    !ValidateTurnBegin(entry, out error))
                {
                    if (string.IsNullOrEmpty(error))
                    {
                        error = "TurnBegin must be the unique root of an action trace.";
                    }
                    return false;
                }
                turnBeginObserved = true;
            }
            else if (entry.EventKind == ActionTraceEventKind.SelectionOpened)
            {
                if (!turnBeginObserved || !hasParent ||
                    !ValidateSelection(entry, parentEntry, out error))
                {
                    if (string.IsNullOrEmpty(error))
                    {
                        error = "SelectionOpened must reference its TurnBegin event.";
                    }
                    return false;
                }
            }
            else if (entry.EventKind == ActionTraceEventKind.SelectionConfirmed)
            {
                if (!turnBeginObserved || !hasParent ||
                    !ValidateSelectionConfirmation(entry, parentEntry, out error))
                {
                    if (string.IsNullOrEmpty(error))
                    {
                        error = "SelectionConfirmed must reference its " +
                            "SelectionOpened event.";
                    }
                    return false;
                }
            }
            else if (entry.EventKind == ActionTraceEventKind.ActionLocked)
            {
                if (actionLocked)
                {
                    error = "ActionLocked must occur exactly once.";
                    return false;
                }
                if (!turnBeginObserved || !hasParent ||
                    !ValidateLock(entry, parentEntry, out error))
                {
                    if (string.IsNullOrEmpty(error))
                    {
                        error = "ActionLocked must reference its SelectionConfirmed event.";
                    }
                    return false;
                }
                actionLocked = true;
                actionOwner = entry.Owner;
            }
            else
            {
                if (!actionLocked || entry.Owner != actionOwner)
                {
                    return Reject(
                        "Post-lock trace event does not carry the locked " +
                        "ActionIdentity: " + entry.EventId + ".",
                        out error);
                }
            }

            switch (entry.EventKind)
            {
                case ActionTraceEventKind.PhaseEntered:
                    if (entry.Stage == DRPGBattleActionStage.Settlement)
                    {
                        if (!returnCompleted)
                        {
                            return Reject(
                                "Settlement phase entered before Return completed.",
                                out error);
                        }
                        settlementObserved = true;
                    }
                    break;
                case ActionTraceEventKind.ReceiptIssued:
                    if (!hasParent || !entry.ReceiptId.IsValid ||
                        entry.ReceiptStatus != ActionTraceReceiptStatus.Issued ||
                        issuedReceipts.ContainsKey(entry.ReceiptId) ||
                        parentEntry.EventKind != ActionTraceEventKind.ActionLocked ||
                        parentEntry.Owner != entry.Owner)
                    {
                        return Reject(
                            "Receipt issuance must reference the matching ActionLocked " +
                            "event exactly once.",
                            out error);
                    }
                    issuedReceipts.Add(entry.ReceiptId, entry);
                    break;
                case ActionTraceEventKind.ReceiptCompleted:
                    if (!hasParent || !entry.ReceiptId.IsValid ||
                        entry.ReceiptStatus == ActionTraceReceiptStatus.None ||
                        entry.ReceiptStatus == ActionTraceReceiptStatus.Issued ||
                        !issuedReceipts.TryGetValue(
                            entry.ReceiptId,
                            out ActionTraceEntry issuedReceipt) ||
                        terminalReceipts.ContainsKey(entry.ReceiptId) ||
                        parentEntry.EventKind != ActionTraceEventKind.ReceiptIssued ||
                        parentEntry.EventId != issuedReceipt.EventId ||
                        parentEntry.ReceiptId != entry.ReceiptId ||
                        parentEntry.Owner != entry.Owner)
                    {
                        return Reject(
                            "Receipt terminal must reference its matching issuance " +
                            "exactly once.",
                            out error);
                    }
                    terminalReceipts.Add(entry.ReceiptId, entry);
                    if (entry.ReceiptStatus == ActionTraceReceiptStatus.Faulted)
                        faultedTerminalObserved = true;
                    break;
                case ActionTraceEventKind.ImpactCommitted:
                    if (string.IsNullOrEmpty(entry.Fingerprint) ||
                        entry.HitOutcome == ActionTraceHitOutcome.None ||
                        !entry.BeforeProjection.IsSpecified ||
                        !entry.AfterProjection.IsSpecified)
                    {
                        return Reject(
                            "Impact trace lacks fingerprint, hit outcome or projection delta.",
                            out error);
                    }
                    break;
                case ActionTraceEventKind.ReturnStarted:
                    if (returnStarted || string.IsNullOrEmpty(entry.BarrierId))
                    {
                        return Reject(
                            "Return start must identify one barrier.",
                            out error);
                    }
                    returnStarted = true;
                    returnBarrierId = entry.BarrierId;
                    break;
                case ActionTraceEventKind.ReturnBarrierCompleted:
                    if (!returnStarted || returnCompleted ||
                        !string.Equals(
                            returnBarrierId,
                            entry.BarrierId,
                            StringComparison.Ordinal))
                    {
                        return Reject(
                            "Return barrier completed without its matching start.",
                            out error);
                    }
                    returnCompleted = true;
                    break;
                case ActionTraceEventKind.SettlementItem:
                    if (!returnCompleted ||
                        string.IsNullOrEmpty(entry.Fingerprint) ||
                        !entry.BeforeProjection.IsSpecified ||
                        !entry.AfterProjection.IsSpecified)
                    {
                        return Reject(
                            "Settlement occurred before Return or lacks its projection delta.",
                            out error);
                    }
                    settlementObserved = true;
                    break;
                case ActionTraceEventKind.WatchdogExpired:
                    if (string.IsNullOrEmpty(entry.WatchdogId))
                    {
                        return Reject(
                            "Watchdog trace lacks watchdog identity.",
                            out error);
                    }
                    break;
                case ActionTraceEventKind.StaleCallbackDropped:
                    if (string.IsNullOrEmpty(entry.StaleReason))
                    {
                        return Reject(
                            "Stale callback trace lacks a structured reason.",
                            out error);
                    }
                    break;
                case ActionTraceEventKind.Finalized:
                    if (finalized ||
                        !faultedTerminalObserved &&
                            (!returnCompleted || !settlementObserved) ||
                        string.IsNullOrEmpty(entry.Fingerprint) ||
                        !hasParent ||
                        parentEntry.EventKind !=
                            ActionTraceEventKind.ReceiptCompleted ||
                        !entry.ReceiptId.IsValid ||
                        parentEntry.ReceiptId != entry.ReceiptId ||
                        parentEntry.Owner != entry.Owner ||
                        !string.Equals(
                            parentEntry.Fingerprint,
                            entry.Fingerprint,
                            StringComparison.Ordinal))
                    {
                        return Reject(
                            "Finalization must follow Return and Settlement exactly once.",
                            out error);
                    }
                    finalized = true;
                    break;
            }

            previousSequence = entry.Sequence;
            previousLogical = entry.LogicalTimestamp;
            previousPresentation = entry.PresentationTimestamp;
        }

        if (!turnBeginObserved || !actionLocked || !finalized ||
            !faultedTerminalObserved &&
                (!returnCompleted || !settlementObserved))
        {
            return Reject(
                "Trace cannot reconstruct Lock, Return, Settlement and Finalize.",
                out error);
        }
        if (issuedReceipts.Count != terminalReceipts.Count)
        {
            return Reject(
                "Every issued receipt must have exactly one terminal.",
                out error);
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidateTurnBegin(
        ActionTraceEntry entry,
        out string error)
    {
        ActorTurnToken turn = entry.TraceIdentity.Turn;
        if (entry.TraceIdentity.Kind != ActionTraceIdentityKind.ActorTurn ||
            entry.Stage != DRPGBattleActionStage.TurnBegin ||
            !turn.IsValid || entry.Actor != turn.Actor ||
            !entry.Side.HasValue || !entry.HomePosition.IsSpecified)
        {
            return Reject(
                "TurnBegin trace does not describe its full ActorTurnToken.",
                out error);
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidateSelection(
        ActionTraceEntry entry,
        ActionTraceEntry parent,
        out string error)
    {
        TargetSelectionToken selection = entry.TraceIdentity.Selection;
        ActorTurnToken turn = entry.PrecursorTurn;
        if (entry.TraceIdentity.Kind != ActionTraceIdentityKind.TargetSelection ||
            parent.EventKind != ActionTraceEventKind.PhaseEntered ||
            parent.Stage != DRPGBattleActionStage.TurnBegin ||
            parent.TraceIdentity.Kind != ActionTraceIdentityKind.ActorTurn ||
            !selection.IsValid || !turn.IsValid ||
            turn != parent.TraceIdentity.Turn ||
            selection.BattleGeneration != turn.BattleGeneration ||
            selection.TurnSerial != turn.TurnSerial ||
            selection.Actor != turn.Actor || entry.Actor != turn.Actor ||
            entry.Actor != parent.Actor ||
            !entry.Side.HasValue || entry.Side != parent.Side)
        {
            return Reject(
                "Selection trace does not correlate to its exact TurnBegin parent.",
                out error);
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidateSelectionConfirmation(
        ActionTraceEntry entry,
        ActionTraceEntry parent,
        out string error)
    {
        TargetSelectionToken selection = entry.TraceIdentity.Selection;
        ActorTurnToken turn = entry.PrecursorTurn;
        if (entry.TraceIdentity.Kind != ActionTraceIdentityKind.TargetSelection ||
            parent.EventKind != ActionTraceEventKind.SelectionOpened ||
            parent.TraceIdentity.Kind !=
                ActionTraceIdentityKind.TargetSelection ||
            selection != parent.TraceIdentity.Selection ||
            turn != parent.PrecursorTurn || entry.Actor != parent.Actor ||
            entry.Side != parent.Side || entry.Target.Value <= 0)
        {
            return Reject(
                "Selection confirmation does not correlate to its open token.",
                out error);
        }
        error = string.Empty;
        return true;
    }

    private static bool ValidateLock(
        ActionTraceEntry entry,
        ActionTraceEntry parent,
        out string error)
    {
        ActorTurnToken turn = entry.PrecursorTurn;
        TargetSelectionToken selection = entry.PrecursorSelection;
        ActionIdentity action = entry.Owner.Action;
        bool isActionOwner = entry.TraceIdentity.Kind ==
                ActionTraceIdentityKind.Transaction &&
            (entry.Owner.Kind == BattleTransactionKind.RootAction ||
             entry.Owner.Kind == BattleTransactionKind.LegacyAction);
        if (!isActionOwner ||
            parent.EventKind != ActionTraceEventKind.SelectionConfirmed ||
            parent.TraceIdentity.Kind !=
                ActionTraceIdentityKind.TargetSelection ||
            !turn.IsValid || !selection.IsValid ||
            selection != parent.TraceIdentity.Selection ||
            turn != parent.PrecursorTurn ||
            action.BattleGeneration != turn.BattleGeneration ||
            action.TurnSerial != turn.TurnSerial ||
            selection.BattleGeneration != turn.BattleGeneration ||
            selection.TurnSerial != turn.TurnSerial ||
            selection.Actor != turn.Actor || entry.Actor != turn.Actor ||
            entry.Actor != parent.Actor || entry.Target.Value <= 0 ||
            entry.Target != parent.Target || !entry.Side.HasValue ||
            entry.Side != parent.Side ||
            !entry.HomePosition.IsSpecified ||
            !entry.ConflictPosition.IsSpecified ||
            string.IsNullOrEmpty(entry.LeaseId))
        {
            return Reject(
                "Action lock cannot be correlated to selection, turn and " +
                "frozen participant positions.",
                out error);
        }

        error = string.Empty;
        return true;
    }

    private static bool Reject(string message, out string error)
    {
        error = message;
        return false;
    }
}

public sealed class RootActionReceipt
{
    private readonly TaskCompletionSource<RootActionTerminal> completion =
        new TaskCompletionSource<RootActionTerminal>(
            TaskCreationOptions.RunContinuationsAsynchronously);
    private static long nextReceiptId;
    private readonly object terminalGate = new object();
    private readonly DRPGBattleActionTraceWriter trace;
    private string terminalFingerprint;
    private RootActionTerminal terminalValue;
    private bool hasTerminal;
    private long callbackTraceSerial;

    internal RootActionReceipt(
        ActionIdentity identity,
        DRPGBattleActionTraceWriter trace,
        string actionLockedEventId)
    {
        if (!identity.IsValid)
            throw new ArgumentException("Action identity is required.", nameof(identity));
        Identity = identity;
        this.trace = trace ?? throw new ArgumentNullException(nameof(trace));
        if (string.IsNullOrEmpty(actionLockedEventId))
        {
            throw new ArgumentException(
                "The root receipt requires its ActionLocked trace parent.",
                nameof(actionLockedEventId));
        }
        ReceiptId = new PresentationReceiptId(
            Interlocked.Increment(ref nextReceiptId));
        trace.Record(
            0d,
            0d,
            ActionTraceIdentity.ForTransaction(
                BattleTransactionIdentity.ForRootAction(Identity)),
            ActionTraceEventKind.ReceiptIssued,
            DRPGBattleActionStage.Locked,
            0,
            ReceiptEventId("issued"),
            actionLockedEventId,
            new ActionTraceContext(
                receiptId: ReceiptId,
                receiptStatus: ActionTraceReceiptStatus.Issued));
    }

    public ActionIdentity Identity { get; }
    public PresentationReceiptId ReceiptId { get; }
    public Task<RootActionTerminal> Completion => completion.Task;
    public IReadOnlyList<ActionTraceEntry> SnapshotTrace() =>
        trace.SnapshotForAction(Identity);

    internal bool TryComplete(
        ActionIdentity callbackIdentity,
        RootActionTerminal terminal,
        string fingerprint,
        out string error)
    {
        if (!callbackIdentity.IsValid || callbackIdentity != Identity)
        {
            trace.Record(
                0d,
                0d,
                ActionTraceIdentity.ForTransaction(
                    BattleTransactionIdentity.ForRootAction(Identity)),
                ActionTraceEventKind.StaleCallbackDropped,
                DRPGBattleActionStage.Finalized,
                0,
                NextCallbackEventId("stale-callback"),
                ReceiptEventId("issued"),
                new ActionTraceContext(
                    receiptId: ReceiptId,
                    staleReason: "Rejected root terminal callback for " +
                        callbackIdentity + "."));
            error = "Root receipt callback identity is stale.";
            return false;
        }
        if (string.IsNullOrEmpty(fingerprint))
            throw new ArgumentException(
                "Terminal fingerprint is required.",
                nameof(fingerprint));
        lock (terminalGate)
        {
            if (hasTerminal)
            {
                if (string.Equals(
                        terminalFingerprint,
                        fingerprint,
                        StringComparison.Ordinal) &&
                    terminalValue.Kind == terminal.Kind &&
                    string.Equals(
                        terminalValue.Detail,
                        terminal.Detail,
                        StringComparison.Ordinal))
                {
                    error = string.Empty;
                    return true;
                }

                trace.Record(
                    0d,
                    0d,
                    ActionTraceIdentity.ForTransaction(
                        BattleTransactionIdentity.ForRootAction(Identity)),
                    ActionTraceEventKind.StaleCallbackDropped,
                    DRPGBattleActionStage.Finalized,
                    0,
                    NextCallbackEventId("terminal-conflict"),
                    ReceiptEventId("issued"),
                    new ActionTraceContext(
                        fingerprint: fingerprint,
                        receiptId: ReceiptId,
                        staleReason:
                            "Receipt terminal payload or fingerprint conflict."));
                error = "Receipt terminal payload or fingerprint conflict.";
                return false;
            }

            string completedEventId = ReceiptEventId("completed");
            trace.Record(
                0d,
                0d,
                ActionTraceIdentity.ForTransaction(
                    BattleTransactionIdentity.ForRootAction(Identity)),
                ActionTraceEventKind.ReceiptCompleted,
                DRPGBattleActionStage.Finalized,
                0,
                completedEventId,
                ReceiptEventId("issued"),
                new ActionTraceContext(
                    fingerprint: fingerprint,
                    receiptId: ReceiptId,
                    receiptStatus: ToTraceStatus(terminal.Kind),
                    detail: terminal.Detail));
            trace.Record(
                0d,
                0d,
                ActionTraceIdentity.ForTransaction(
                    BattleTransactionIdentity.ForRootAction(Identity)),
                ActionTraceEventKind.Finalized,
                DRPGBattleActionStage.Finalized,
                0,
                ReceiptEventId("finalized"),
                completedEventId,
                new ActionTraceContext(
                    fingerprint: fingerprint,
                    receiptId: ReceiptId,
                    detail: terminal.Kind.ToString()));
            terminalFingerprint = fingerprint;
            terminalValue = terminal;
            hasTerminal = true;
            if (!completion.TrySetResult(terminal))
                throw new InvalidOperationException(
                    "The root action receipt terminal gate diverged from its task.");
            error = string.Empty;
            return true;
        }
    }

    private string ReceiptEventId(string suffix) =>
        "root-receipt-" + ReceiptId.Value + "-" + suffix;

    private string NextCallbackEventId(string suffix) =>
        ReceiptEventId(
            suffix + "-" + Interlocked.Increment(ref callbackTraceSerial));

    private static ActionTraceReceiptStatus ToTraceStatus(
        RootActionTerminalKind terminalKind)
    {
        switch (terminalKind)
        {
            case RootActionTerminalKind.Completed:
                return ActionTraceReceiptStatus.Completed;
            case RootActionTerminalKind.DegradedCompleted:
                return ActionTraceReceiptStatus.Degraded;
            case RootActionTerminalKind.Faulted:
                return ActionTraceReceiptStatus.Faulted;
            default:
                throw new ArgumentOutOfRangeException(nameof(terminalKind));
        }
    }
}
