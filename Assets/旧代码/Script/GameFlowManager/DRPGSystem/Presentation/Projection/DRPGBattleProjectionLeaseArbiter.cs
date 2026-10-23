using System;
using System.Collections.Generic;

internal readonly struct ProjectionCursorResult
{
    internal ProjectionCursorResult(bool accepted, string reason)
    { Accepted = accepted; Reason = reason ?? string.Empty; }
    internal bool Accepted { get; }
    internal string Reason { get; }
}

internal readonly struct ProjectionLeaseAcquireResult
{
    internal ProjectionLeaseAcquireResult(DRPGBattleActionProjectionLease lease, string reason)
    { Lease = lease; Reason = reason ?? string.Empty; }
    internal DRPGBattleActionProjectionLease Lease { get; }
    internal bool Accepted => Lease != null;
    internal string Reason { get; }
}

internal interface IDRPGBattleProjectionLeaseArbiter
{
    ProjectionLeaseAcquireResult TryAcquire(in BattleTransactionIdentity owner);
}

// Explicit capability passed only down the canonical call stack. It is never
// installed as a global "allow writes" flag, so Unity callbacks cannot inherit it.
internal sealed class BattleCanonicalProjectionPermit { }

internal enum ProjectionRejectionKind { Acquire, Canonical, Cursor }
internal readonly struct BattleProjectionRejection
{
    internal BattleProjectionRejection(ProjectionRejectionKind kind, BattleTransactionIdentity owner,
        BattleTransactionIdentity requestedOwner, string operation, int runtimeId, string reason)
    { Kind = kind; Owner = owner; RequestedOwner = requestedOwner; Operation = operation; RuntimeId = runtimeId; Reason = reason; }
    internal ProjectionRejectionKind Kind { get; }
    internal BattleTransactionIdentity Owner { get; }
    internal BattleTransactionIdentity RequestedOwner { get; }
    internal string Operation { get; }
    internal int RuntimeId { get; }
    internal string Reason { get; }
}

internal sealed class DRPGBattleProjectionLeaseArbiter : IDRPGBattleProjectionLeaseArbiter
{
    private readonly DRPGBattleUGUISurface surface;
    private readonly Queue<string> trace = new Queue<string>();
    private readonly Queue<BattleProjectionRejection> rejections = new Queue<BattleProjectionRejection>();
    private bool closing;
    private BattleCanonicalProjectionPermit canonicalPermit;
    internal DRPGBattleActionProjectionLease Active { get; private set; }
    internal bool IsLeased => Active != null || closing;
    internal int CanonicalFlushCount { get; private set; }
    internal IReadOnlyCollection<string> Trace => Array.AsReadOnly(trace.ToArray());
    internal IReadOnlyList<BattleProjectionRejection> Rejections => Array.AsReadOnly(rejections.ToArray());
    internal DRPGBattleProjectionLeaseArbiter(DRPGBattleUGUISurface surface)
    { this.surface = surface ?? throw new ArgumentNullException(nameof(surface)); }

    public ProjectionLeaseAcquireResult TryAcquire(in BattleTransactionIdentity owner)
    {
        if (IsLeased) return RejectAcquire(owner, "ProjectionBusy");
        if (!owner.IsValid || owner.BattleGeneration != surface.BattleGeneration)
            return RejectAcquire(owner, "WrongGeneration");
        if (surface.LastSnapshot == null || surface.WorldRosterPresenter == null)
            return RejectAcquire(owner, "ProjectionNotReady");
        if (!surface.HasCompleteBattlefieldProjection())
            return RejectAcquire(owner, "IncompleteBattlefield");
        Active = new DRPGBattleActionProjectionLease(this, surface, owner);
        surface.InstallProjectionGuard(this);
        Record("Acquired:" + owner);
        return new ProjectionLeaseAcquireResult(Active, null);
    }

    internal bool DeferCanonical(string operation, int runtimeId = 0,
        BattleCanonicalProjectionPermit permit = null)
    {
        if (permit != null && closing && ReferenceEquals(canonicalPermit, permit)) return false;
        if (!IsLeased) return false;
        Active?.MarkDirty(runtimeId);
        RecordRejection(ProjectionRejectionKind.Canonical, default, operation, runtimeId, "ProjectionLeased");
        Record("RejectedCanonical:" + operation + ":" + runtimeId);
        return true;
    }

    private ProjectionLeaseAcquireResult RejectAcquire(BattleTransactionIdentity requested, string reason)
    {
        RecordRejection(ProjectionRejectionKind.Acquire, requested, "Acquire", 0, reason);
        Record("RejectedAcquire:" + reason);
        return new ProjectionLeaseAcquireResult(null, reason);
    }

    internal ProjectionCursorResult Reject(string reason, BattleTransactionIdentity requested, int runtimeId)
    {
        RecordRejection(ProjectionRejectionKind.Cursor, requested, "ApplyCursor", runtimeId, reason);
        Record("RejectedCursor:" + reason);
        return new ProjectionCursorResult(false, reason);
    }

    private void RecordRejection(ProjectionRejectionKind kind, BattleTransactionIdentity requested,
        string operation, int runtimeId, string reason)
    {
        if (rejections.Count >= 256) rejections.Dequeue();
        rejections.Enqueue(new BattleProjectionRejection(kind, Active != null ? Active.Owner : default,
            requested, operation, runtimeId, reason));
    }

    internal void Close(DRPGBattleActionProjectionLease lease, bool reconcile)
    {
        if (!ReferenceEquals(Active, lease) || closing) return;
        closing = true;
        try
        {
            // Invalidate old queued events/callback epochs BEFORE reconciliation.
            // Events raised during the flush belong to the new epoch.
            surface.BeginCanonicalProjectionClose();
            if (reconcile)
            {
                canonicalPermit = new BattleCanonicalProjectionPermit();
                surface.ReconcileCanonicalProjection(canonicalPermit);
                CanonicalFlushCount++;
            }
            Record(reconcile ? "CanonicalFlushed" : "Abandoned");
        }
        finally
        {
            surface.InstallProjectionGuard(null);
            canonicalPermit = null;
            Active = null;
            closing = false;
        }
    }

    internal void Abandon() => Active?.Abandon();
    internal void Record(string value)
    {
        if (trace.Count >= 256) trace.Dequeue();
        trace.Enqueue(value);
    }
}
