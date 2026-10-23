using System;
using System.Threading;
using System.Threading.Tasks;

internal readonly struct BattleStartEntryCommand
{
    internal BattleStartEntryCommand(BattleStartIdentity identity, double issuedAt)
    {
        if (!identity.IsValid || double.IsNaN(issuedAt) || double.IsInfinity(issuedAt) || issuedAt < 0)
            throw new ArgumentException("Startup entry requires a valid identity and finite nonnegative issue time.");
        Identity = identity; IssuedAt = issuedAt;
    }
    internal BattleStartIdentity Identity { get; }
    internal BattleTransactionIdentity Owner => BattleTransactionIdentity.ForBattleStart(Identity);
    internal double IssuedAt { get; }
}

internal interface IBattleStartPresentationPort
{
    PresentationReceipt PresentEntry(in BattleStartEntryCommand command);
}

internal readonly struct AnnouncementCommand
{
    internal AnnouncementCommand(ActionIdentity identity)
    {
        Identity = identity;
        Snapshot = default;
        IssuedAt = 0d;
        ActorName = string.Empty;
    }

    internal AnnouncementCommand(in DRPGBasicAttackFrozenSnapshot snapshot, double issuedAt, string actorName)
    { Identity = snapshot.Identity; Snapshot = snapshot; IssuedAt = issuedAt; ActorName = actorName ?? string.Empty; }

    internal ActionIdentity Identity { get; }
    internal DRPGBasicAttackFrozenSnapshot Snapshot { get; }
    internal double IssuedAt { get; }
    internal string ActorName { get; }
}

internal readonly struct ConflictCompositionCommand
{
    internal ConflictCompositionCommand(ActionIdentity identity)
    {
        Identity = identity;
        Snapshot = default;
        IssuedAt = 0d;
    }

    internal ConflictCompositionCommand(in DRPGBasicAttackFrozenSnapshot snapshot, double issuedAt)
    { Identity = snapshot.Identity; Snapshot = snapshot; IssuedAt = issuedAt; }

    internal ActionIdentity Identity { get; }
    internal DRPGBasicAttackFrozenSnapshot Snapshot { get; }
    internal double IssuedAt { get; }
}

internal readonly struct BasicAttackWindupCommand
{
    internal BasicAttackWindupCommand(ActionIdentity identity)
    {
        Identity = identity;
        Snapshot = default;
        IssuedAt = 0d;
    }

    internal BasicAttackWindupCommand(in DRPGBasicAttackFrozenSnapshot snapshot, double issuedAt)
    { Identity = snapshot.Identity; Snapshot = snapshot; IssuedAt = issuedAt; }

    internal ActionIdentity Identity { get; }
    internal DRPGBasicAttackFrozenSnapshot Snapshot { get; }
    internal double IssuedAt { get; }
}

internal readonly struct ImpactPresentationCommand
{
    internal ImpactPresentationCommand(ActionIdentity identity)
    {
        Identity = identity;
        Ledger = null; IssuedAt = 0d;
    }

    internal ImpactPresentationCommand(DRPGBattleActionResolutionLedger ledger, double issuedAt)
    { Ledger = ledger ?? throw new ArgumentNullException(nameof(ledger)); Identity = ledger.Identity; IssuedAt = issuedAt; }

    internal ActionIdentity Identity { get; }
    internal DRPGBattleActionResolutionLedger Ledger { get; }
    internal double IssuedAt { get; }
}

internal readonly struct ReactionPresentationCommand
{
    internal ReactionPresentationCommand(ActionIdentity identity, int stepOrdinal)
    {
        Identity = identity;
        StepOrdinal = stepOrdinal;
        Ledger = null; IssuedAt = 0d;
    }

    internal ReactionPresentationCommand(DRPGBattleActionResolutionLedger ledger, int stepOrdinal, double issuedAt)
    { Ledger = ledger ?? throw new ArgumentNullException(nameof(ledger)); Identity = ledger.Identity; StepOrdinal = stepOrdinal; IssuedAt = issuedAt; }

    internal ActionIdentity Identity { get; }
    internal int StepOrdinal { get; }
    internal DRPGBattleActionResolutionLedger Ledger { get; }
    internal double IssuedAt { get; }
}

internal readonly struct ReturnPresentationCommand
{
    internal ReturnPresentationCommand(ActionIdentity identity)
    {
        Identity = identity;
    }

    internal ActionIdentity Identity { get; }
}

internal readonly struct SettlementPresentationCommand
{
    internal SettlementPresentationCommand(ActionIdentity identity, int stepOrdinal)
    {
        Identity = identity;
        StepOrdinal = stepOrdinal;
        Plan = null; IssuedAt = 0d;
    }

    internal SettlementPresentationCommand(DRPGSettlementPresentationPlan plan, int stepOrdinal, double issuedAt)
    {
        Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        if (stepOrdinal < 0 || stepOrdinal >= Math.Max(1, plan.Steps.Count))
            throw new ArgumentOutOfRangeException(nameof(stepOrdinal));
        Identity = plan.Ledger.Identity; StepOrdinal = stepOrdinal; IssuedAt = issuedAt;
    }

    internal ActionIdentity Identity { get; }
    internal int StepOrdinal { get; }
    internal DRPGSettlementPresentationPlan Plan { get; }
    internal BattleTransactionIdentity Owner => Plan != null ? Plan.Ledger.Owner :
        Identity.IsValid ? BattleTransactionIdentity.ForRootAction(Identity) : default;
    internal DRPGBattleActionStage Stage => Owner.Kind == BattleTransactionKind.TurnEntry
        ? DRPGBattleActionStage.TurnBegin : DRPGBattleActionStage.Settlement;
    internal double IssuedAt { get; }
    internal DRPGSettlementPresentationStep Step => Plan == null || Plan.IsEmpty ? null : Plan.Steps[StepOrdinal];
}

internal readonly struct BattlePresentationCleanupCommand
{
    internal BattlePresentationCleanupCommand(
        BattleTransactionIdentity owner,
        string reason)
    {
        Owner = owner;
        Reason = reason ?? string.Empty;
    }

    internal BattleTransactionIdentity Owner { get; }
    internal string Reason { get; }
}

internal enum PresentationReceiptTerminalKind
{
    Completed,
    Degraded,
    Faulted
}

internal readonly struct PresentationReceiptTerminal
{
    internal PresentationReceiptTerminal(
        PresentationReceiptTerminalKind kind,
        string detail)
    {
        Kind = kind;
        Detail = detail ?? string.Empty;
    }

    internal PresentationReceiptTerminalKind Kind { get; }
    internal string Detail { get; }
}

internal sealed class PresentationReceipt
{
    private static long nextReceiptId;
    // Reserve recovery before invoking user/Unity cleanup callbacks: they can
    // synchronously re-enter the normal completion path.
    private int terminalState;
    private readonly TaskCompletionSource<PresentationReceiptTerminal> completion =
        new TaskCompletionSource<PresentationReceiptTerminal>(
            TaskCreationOptions.RunContinuationsAsynchronously);

    internal PresentationReceipt(
        ActionIdentity identity,
        DRPGBattleActionStage stage,
        int stepOrdinal = 0)
        : this(BattleTransactionIdentity.ForRootAction(identity), stage, stepOrdinal)
    { }

    internal PresentationReceipt(BattleTransactionIdentity owner, DRPGBattleActionStage stage, int stepOrdinal = 0)
    {
        if (!owner.IsValid)
            throw new ArgumentException("Transaction identity is required.", nameof(owner));
        if (stage == DRPGBattleActionStage.None)
            throw new ArgumentOutOfRangeException(nameof(stage));
        if (stepOrdinal < 0)
            throw new ArgumentOutOfRangeException(nameof(stepOrdinal));
        Owner = owner;
        Identity = owner.Action;
        Stage = stage;
        StepOrdinal = stepOrdinal;
        ReceiptId = new PresentationReceiptId(
            Interlocked.Increment(ref nextReceiptId));
    }

    internal ActionIdentity Identity { get; }
    internal BattleTransactionIdentity Owner { get; }
    internal DRPGBattleActionStage Stage { get; }
    internal int StepOrdinal { get; }
    internal PresentationReceiptId ReceiptId { get; }
    internal Task<PresentationReceiptTerminal> Completion => completion.Task;

    internal bool TryComplete(PresentationReceiptTerminal terminal)
    {
        if (Interlocked.CompareExchange(ref terminalState, 1, 0) != 0) return false;
        return completion.TrySetResult(terminal);
    }

    internal bool TryBeginRecovery() =>
        Interlocked.CompareExchange(ref terminalState, 2, 0) == 0;

    internal bool CompleteRecovery(PresentationReceiptTerminal terminal)
    {
        if (Interlocked.CompareExchange(ref terminalState, 1, 2) != 2) return false;
        return completion.TrySetResult(terminal);
    }
}

internal sealed class PresentationCleanupReceipt
{
    private readonly TaskCompletionSource<bool> completion =
        new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

    internal Task<bool> Completion => completion.Task;

    internal bool TryComplete(bool cleaned)
    {
        return completion.TrySetResult(cleaned);
    }
}

internal interface IBattleSettlementPresentationPort
{
    PresentationReceipt PresentSettlement(in SettlementPresentationCommand command);
}

internal interface IStandardBasicAttackPresentationPort : IBattleSettlementPresentationPort
{
    bool TryCaptureFrozenSnapshot(
        in DRPGBasicAttackPresentationFreezeRequest request,
        out DRPGBasicAttackPresentationFreezeInput snapshot);
    PresentationReceipt Announce(in AnnouncementCommand command);
    PresentationReceipt EnterConflict(in ConflictCompositionCommand command);
    PresentationReceipt PlayWindup(in BasicAttackWindupCommand command);
    PresentationReceipt PresentImpact(in ImpactPresentationCommand command);
    PresentationReceipt PresentReaction(in ReactionPresentationCommand command);
    PresentationReceipt Return(in ReturnPresentationCommand command);
    PresentationCleanupReceipt EmergencyCleanup(
        in BattlePresentationCleanupCommand command);
}

internal interface IStandardBasicAttackProjectionScope
{
    IStandardBasicAttackProjectionLifetime OpenProjection(in DRPGBasicAttackFrozenSnapshot snapshot);
}

// Sequential Stage8 gates. A formation receipt must not be placed in the
// return barrier that is itself required before formation can start.
internal interface IStandardBasicAttackReturnPort
{
    DRPGBattleDeparturePresentation CompleteDefeats(DRPGBattleDepartureJournal departures);
    PresentationReceipt ReconcileFormation(DRPGBattlePostReturnResult rules);
}

internal interface IStandardBasicAttackSettlementPort
{
    bool TryCaptureSettlementHomes(ActionIdentity identity, out System.Collections.Generic.IReadOnlyDictionary<ParticipantId, int> homes);
    DRPGBattleDeparturePresentation CompleteSettlementDefeats(DRPGBattleDepartureJournal departures);
    PresentationReceipt ReconcileSettlementFormation(DRPGBattlePostReturnResult rules);
}

internal interface IStandardBasicAttackProjectionLifetime : IDisposable
{
    bool IsActive { get; }
}

internal enum PresentationPortBindResult
{
    Bound,
    AlreadyBoundToSamePort,
    InvalidGeneration,
    Busy
}

internal enum PresentationPortUnbindResult
{
    Unbound,
    NotBound,
    InvalidGeneration,
    StalePort
}

internal sealed class DRPGBattleTransactionPresentationPortSlot
{
    private readonly long battleGeneration;
    private IStandardBasicAttackPresentationPort port;

    internal DRPGBattleTransactionPresentationPortSlot(long battleGeneration)
    {
        if (battleGeneration <= 0L)
            throw new ArgumentOutOfRangeException(nameof(battleGeneration));
        this.battleGeneration = battleGeneration;
    }

    internal PresentationPortBindResult TryBind(
        long generation,
        IStandardBasicAttackPresentationPort candidate)
    {
        if (generation != battleGeneration || candidate == null)
            return PresentationPortBindResult.InvalidGeneration;
        if (port == null)
        {
            port = candidate;
            return PresentationPortBindResult.Bound;
        }
        return ReferenceEquals(port, candidate)
            ? PresentationPortBindResult.AlreadyBoundToSamePort
            : PresentationPortBindResult.Busy;
    }

    internal bool TryGet(
        long generation,
        out IStandardBasicAttackPresentationPort boundPort)
    {
        boundPort = generation == battleGeneration ? port : null;
        return boundPort != null;
    }

    internal PresentationPortUnbindResult TryUnbind(
        long generation,
        IStandardBasicAttackPresentationPort candidate)
    {
        if (generation != battleGeneration)
            return PresentationPortUnbindResult.InvalidGeneration;
        if (port == null)
            return PresentationPortUnbindResult.NotBound;
        if (!ReferenceEquals(port, candidate))
            return PresentationPortUnbindResult.StalePort;
        port = null;
        return PresentationPortUnbindResult.Unbound;
    }
}
