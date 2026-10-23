using System;

internal sealed class BattleRuleMutationLease : IDisposable
{
    private bool disposeRequested;

    internal BattleRuleMutationLease(
        DRPGBattleTransactionArbiter arbiter,
        BattleTransactionIdentity owner,
        long leaseSerial)
    {
        Arbiter = arbiter ?? throw new ArgumentNullException(nameof(arbiter));
        Owner = owner;
        LeaseSerial = leaseSerial;
    }

    internal DRPGBattleTransactionArbiter Arbiter { get; }
    internal long LeaseSerial { get; }
    internal BattleTransactionIdentity Owner { get; }
    internal bool IsActive { get; private set; } = true;

    public void Dispose()
    {
        if (disposeRequested)
            return;
        disposeRequested = true;
        Arbiter.Release(this);
    }

    internal void Invalidate()
    {
        IsActive = false;
    }
}

internal sealed class DRPGBattleTransactionArbiter
{
    private readonly object ownershipGate = new object();
    private BattleRuleMutationLease currentLease;
    private long nextLeaseSerial;
    private bool promotionInProgress;

    internal DRPGBattleTransactionArbiter(
        long battleGeneration,
        DRPGBattleActionTraceWriter trace)
    {
        if (battleGeneration <= 0L)
            throw new ArgumentOutOfRangeException(nameof(battleGeneration));
        BattleGeneration = battleGeneration;
        Trace = trace ?? throw new ArgumentNullException(nameof(trace));
    }

    internal long BattleGeneration { get; }
    internal DRPGBattleActionTraceWriter Trace { get; }
    internal bool HasOwner
    {
        get
        {
            lock (ownershipGate)
                return HasOwnerCore;
        }
    }
    internal BattleTransactionIdentity CurrentOwner
    {
        get
        {
            lock (ownershipGate)
                return HasOwnerCore ? currentLease.Owner : default;
        }
    }
    internal bool IsCommandInputOpen
    {
        get
        {
            lock (ownershipGate)
                return !HasOwnerCore && !promotionInProgress;
        }
    }

    private bool HasOwnerCore =>
        currentLease != null && currentLease.IsActive;

    internal bool TryAcquire(
        BattleTransactionIdentity owner,
        out BattleRuleMutationLease lease,
        out string error)
    {
        lock (ownershipGate)
        {
            lease = null;
            if (!owner.IsValid)
            {
                error = "A valid transaction identity is required.";
                return false;
            }
            if (owner.BattleGeneration != BattleGeneration)
            {
                error = "The transaction identity belongs to another battle.";
                return false;
            }
            if (promotionInProgress || HasOwnerCore)
            {
                error = "Another battle transaction already owns mutation and input.";
                return false;
            }

            lease = AcquireCore(owner);
            error = string.Empty;
            return true;
        }
    }

    internal RootActionOwnershipResult TryPromoteSelection(
        in TargetSelectionToken selection,
        Func<ActionIdentity> allocateIdentity)
    {
        if (allocateIdentity == null)
            throw new ArgumentNullException(nameof(allocateIdentity));
        lock (ownershipGate)
        {
            if (!selection.IsValid ||
                selection.BattleGeneration != BattleGeneration)
            {
                return RootActionOwnershipResult.Rejected(
                    "The target selection belongs to another battle.");
            }
            if (promotionInProgress || HasOwnerCore)
            {
                return RootActionOwnershipResult.Rejected(
                    "Another battle transaction already owns mutation and input.");
            }

            promotionInProgress = true;
            try
            {
                ActionIdentity identity = allocateIdentity();
                if (!identity.IsValid ||
                    identity.BattleGeneration != BattleGeneration ||
                    identity.TurnSerial != selection.TurnSerial)
                {
                    throw new InvalidOperationException(
                        "Selection promotion allocated an unrelated action identity.");
                }
                BattleRuleMutationLease lease = AcquireCore(
                    BattleTransactionIdentity.ForRootAction(identity));
                return RootActionOwnershipResult.Accepted(identity, lease);
            }
            finally
            {
                promotionInProgress = false;
            }
        }
    }

    internal bool Owns(
        BattleRuleMutationLease lease,
        BattleTransactionIdentity owner)
    {
        lock (ownershipGate)
        {
            return lease != null && ReferenceEquals(lease, currentLease) &&
                lease.IsActive && lease.Owner == owner;
        }
    }

    internal bool TryCaptureCurrentLease(BattleTransactionIdentity owner, out BattleRuleMutationLease lease)
    {
        lock (ownershipGate)
        {
            lease = null;
            if (!owner.IsValid || !HasOwnerCore || currentLease.Owner != owner) return false;
            lease = currentLease;
            return true;
        }
    }

    internal bool TransferToContinuation(
        BattleRuleMutationLease current,
        BattleTransactionIdentity continuation,
        out BattleRuleMutationLease next,
        out string error)
    {
        lock (ownershipGate)
        {
            next = null;
            if (current == null || !Owns(current, current.Owner))
            {
                RecordStale("A non-owner attempted continuation transfer.");
                error = "Only the active owner can transfer continuation.";
                return false;
            }
            if (!continuation.IsValid ||
                continuation.BattleGeneration != BattleGeneration ||
                continuation.BattleGeneration != current.Owner.BattleGeneration)
            {
                RecordStale("Continuation identity does not belong to the active battle.");
                error = "Continuation identity does not match the active battle.";
                return false;
            }

            current.Invalidate();
            currentLease = new BattleRuleMutationLease(
                this,
                continuation,
                ++nextLeaseSerial);
            next = currentLease;
            error = string.Empty;
            return true;
        }
    }

    internal void Release(BattleRuleMutationLease lease)
    {
        lock (ownershipGate)
        {
            if (lease == null)
                return;
            if (!ReferenceEquals(lease, currentLease) || !lease.IsActive)
            {
                RecordStale("A stale lease attempted to release the active owner.");
                return;
            }

            lease.Invalidate();
            currentLease = null;
        }
    }

    private BattleRuleMutationLease AcquireCore(
        BattleTransactionIdentity owner)
    {
        currentLease = new BattleRuleMutationLease(
            this,
            owner,
            ++nextLeaseSerial);
        return currentLease;
    }

    private void RecordStale(string detail)
    {
        lock (ownershipGate)
        {
            if (!HasOwnerCore)
                return;
            Trace.Record(
                0d,
                0d,
                currentLease.Owner,
                ActionTraceEventKind.StaleCallbackDropped,
                DRPGBattleActionStage.None,
                0,
                detail: detail);
        }
    }
}

internal readonly struct RootActionOwnershipResult
{
    private RootActionOwnershipResult(
        bool accepted,
        ActionIdentity identity,
        BattleRuleMutationLease lease,
        string error)
    {
        IsAccepted = accepted;
        Identity = identity;
        Lease = lease;
        Error = error ?? string.Empty;
    }

    internal bool IsAccepted { get; }
    internal ActionIdentity Identity { get; }
    internal BattleRuleMutationLease Lease { get; }
    internal string Error { get; }

    internal static RootActionOwnershipResult Accepted(
        ActionIdentity identity,
        BattleRuleMutationLease lease)
    {
        if (!identity.IsValid)
            throw new ArgumentException("Action identity is required.", nameof(identity));
        return new RootActionOwnershipResult(
            true,
            identity,
            lease ?? throw new ArgumentNullException(nameof(lease)),
            string.Empty);
    }

    internal static RootActionOwnershipResult Rejected(string error)
    {
        return new RootActionOwnershipResult(false, default, null, error);
    }
}
