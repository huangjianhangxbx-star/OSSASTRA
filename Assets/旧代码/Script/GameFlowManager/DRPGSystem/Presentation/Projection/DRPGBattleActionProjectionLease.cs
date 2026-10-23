using System;
using System.Collections.Generic;

// A per-view capability, never a global guard bypass. Only permits registered
// by the owning projection after its owned return barrier may reach the pool.
internal sealed class BattleDepartureProjectionPermit
{
    internal BattleDepartureProjectionPermit(DRPGBattleActionProjectionLease lease, int runtimeId, int viewId)
    { Lease = lease; RuntimeId = runtimeId; ViewId = viewId; }
    internal DRPGBattleActionProjectionLease Lease { get; }
    internal int RuntimeId { get; }
    internal int ViewId { get; }
}

internal interface IDRPGBattleProjectionLease : IDisposable
{
    BattleTransactionIdentity Owner { get; }
    DRPGBattleProjectionCheckpoint Capture(DRPGBattleProjectionCheckpointKind kind);
    ProjectionCursorResult Apply(in BattleProjectionCursor command);
}

internal readonly struct BattleProjectionCursor
{
    internal BattleProjectionCursor(BattleTransactionIdentity owner, int ordinal,
        BattleProjectedParticipant before, BattleProjectedParticipant after)
    { Owner = owner; Ordinal = ordinal; Before = before; After = after; }
    internal BattleTransactionIdentity Owner { get; }
    internal int Ordinal { get; }
    internal BattleProjectedParticipant Before { get; }
    internal BattleProjectedParticipant After { get; }
}

internal sealed class DRPGBattleActionProjectionLease : IDRPGBattleProjectionLease,
    IStandardBasicAttackProjectionLifetime
{
    private sealed class Pin
    {
        internal BattleWorldActorView View;
        internal int InstanceId;
        internal UnityEngine.Transform MotionRoot;
        internal UnityEngine.Transform MotionParent;
        internal BattleProjectionPose Home;
        internal BattleProjectedParticipant Presented;
        internal bool Departed;
        internal BattleActorVisualRestore VisualRestore;
    }

    private readonly DRPGBattleProjectionLeaseArbiter arbiter;
    private readonly DRPGBattleUGUISurface surface;
    private readonly Dictionary<int, Pin> pins = new Dictionary<int, Pin>();
    private readonly HashSet<int> dirtyIds = new HashSet<int>();
    private readonly Dictionary<int, BattleDepartureProjectionPermit> departurePermits = new Dictionary<int, BattleDepartureProjectionPermit>();
    private long checkpointVersion;
    private int nextCursor;
    private bool disposed;
    private bool applying;
    private IDisposable motionOwner;
    private readonly BattleEnvironmentRestore environmentRestore;
    private readonly List<IDisposable> ownedPresentation = new List<IDisposable>();

    internal DRPGBattleActionProjectionLease(DRPGBattleProjectionLeaseArbiter arbiter,
        DRPGBattleUGUISurface surface, BattleTransactionIdentity owner)
    {
        this.arbiter = arbiter; this.surface = surface; Owner = owner;
        foreach (BattleWorldActorView view in surface.WorldRosterPresenter.ActiveViews)
        {
            if (view == null || view.BoundActor == null || !view.gameObject.activeInHierarchy)
                throw new InvalidOperationException("The full battlefield must be bound before lock.");
            pins.Add(view.RuntimeId, new Pin { View = view, InstanceId = view.GetInstanceID(),
                MotionRoot = view.MotionRoot,
                MotionParent = view.MotionRoot.parent,
                Home = new BattleProjectionPose(view.MotionRoot), VisualRestore = new BattleActorVisualRestore(view),
                Presented = view.BoundActor.ProjectionBaseline });
        }
        environmentRestore = new BattleEnvironmentRestore(surface.transform, surface.ProjectionWorldRoot, surface.WorldRosterPresenter.ActiveViews);
        FrozenFormation = surface.WorldRosterPresenter.CaptureFrozenLayout();
        LockBaseline = Capture(DRPGBattleProjectionCheckpointKind.LockBaseline);
    }

    public BattleTransactionIdentity Owner { get; }
    internal DRPGBattleProjectionCheckpoint LockBaseline { get; }
    internal BattleFrozenFormationLayout FrozenFormation { get; }
    internal int Count => pins.Count;
    internal int NextCursorOrdinal => nextCursor;
    internal bool IsDisposed => disposed;
    public bool IsActive
    {
        get
        {
            if (disposed || !ReferenceEquals(arbiter.Active, this)) return false;
            foreach (int runtimeId in pins.Keys)
            {
                if (pins[runtimeId].Departed) continue;
                if (!TryGetView(runtimeId, out var view) || !view.gameObject.activeInHierarchy) return false;
            }
            return true;
        }
    }
    internal bool IsDirty => dirtyIds.Count > 0;
    internal bool TryClaimMotion(IDisposable owner)
    {
        if (owner == null || !IsActive || motionOwner != null) return false;
        motionOwner = owner;
        ownedPresentation.Add(owner);
        return true;
    }
    internal bool TryOwnPresentation(IDisposable owner)
    {
        if (!IsActive || owner == null || ownedPresentation.Contains(owner)) return false;
        ownedPresentation.Add(owner); return true;
    }
    internal bool OwnsMotion(IDisposable owner) => IsActive && ReferenceEquals(motionOwner, owner);
    internal bool OwnsPresentation(IDisposable owner) => IsActive && ownedPresentation.Contains(owner);
    internal BattleEnvironmentRestore.Transition BeginEnvironmentReturn(IDisposable owner) =>
        OwnsPresentation(owner) ? environmentRestore.Begin(this, owner) : null;
    internal bool Contains(int runtimeId) => pins.ContainsKey(runtimeId);
    internal void MarkDirty(int runtimeId) => dirtyIds.Add(runtimeId);

    internal bool TryGetView(int runtimeId, out BattleWorldActorView view)
    {
        view = null;
        if (disposed || !pins.TryGetValue(runtimeId, out var pin) || pin.Departed ||
            pin.View == null || pin.View.RuntimeId != runtimeId ||
            pin.View.GetInstanceID() != pin.InstanceId || pin.MotionRoot == null || pin.View.MotionRoot != pin.MotionRoot ||
            pin.MotionRoot.parent != pin.MotionParent)
            return false;
        view = pin.View;
        return true;
    }

    internal BattleProjectedParticipant GetPresented(int runtimeId) => pins[runtimeId].Presented;
    internal bool IsAtCurrentHome(int runtimeId) => TryGetView(runtimeId, out var view) &&
        (view.MotionRoot.position - pins[runtimeId].Home.Position).sqrMagnitude <= .000001f;
    internal float CurrentHomeDistanceSquared(int runtimeId) =>
        TryGetView(runtimeId, out var view)
            ? (view.MotionRoot.position - pins[runtimeId].Home.Position).sqrMagnitude
            : float.PositiveInfinity;
    internal BattleActorVisualRestore.Transition BeginVisualReturn(IDisposable owner, int runtimeId)
    {
        if (!OwnsMotion(owner) || !TryGetView(runtimeId, out _)) return null;
        return pins[runtimeId].VisualRestore.Begin(this, owner, pins[runtimeId].Presented.DefeatPending);
    }
    internal void UpdateFormationHome(IDisposable owner, int runtimeId)
    {
        if (!OwnsMotion(owner) || !TryGetView(runtimeId, out var view))
            throw new InvalidOperationException("Only the motion owner may publish a reconciled home.");
        pins[runtimeId].Home = new BattleProjectionPose(view.MotionRoot);
        surface.WorldRosterPresenter.ActorPool.CommitBorrowedFormationHome(this, view);
    }

    internal bool CanDepart(BattleReturnBarrier barrier, int runtimeId) => IsActive && barrier != null &&
        barrier.IncludesDefeat(runtimeId) &&
        ownedPresentation.Contains(barrier) && barrier.Receipt.Owner.Equals(Owner) &&
        barrier.Receipt.Completion.IsCompleted && barrier.Receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed &&
        TryGetView(runtimeId, out _) && GetPresented(runtimeId).DefeatPending;

    internal BattleDepartureProjectionPermit AuthorizeDeparture(BattleReturnBarrier barrier, int runtimeId)
    {
        if (!CanDepart(barrier, runtimeId)) throw new InvalidOperationException("Departure requires this projection's rendered return.");
        return IssueDeparturePermit(runtimeId);
    }

    internal bool CanDepart(BattleSettlementDefeatBarrier barrier, int runtimeId) => IsActive && barrier != null &&
        ownedPresentation.Contains(barrier) && barrier.CanAuthorize(Owner, runtimeId) &&
        TryGetView(runtimeId, out _) && GetPresented(runtimeId).DefeatPending;

    internal BattleDepartureProjectionPermit AuthorizeDeparture(BattleSettlementDefeatBarrier barrier, int runtimeId)
    {
        if (!CanDepart(barrier, runtimeId)) throw new InvalidOperationException("Departure requires this settlement's independent rendered home.");
        return IssueDeparturePermit(runtimeId);
    }

    private BattleDepartureProjectionPermit IssueDeparturePermit(int runtimeId)
    {
        if (departurePermits.TryGetValue(runtimeId, out var existing)) return existing;
        var permit = new BattleDepartureProjectionPermit(this, runtimeId, pins[runtimeId].InstanceId);
        departurePermits.Add(runtimeId, permit); return permit;
    }

    internal bool OwnsDeparturePermit(BattleDepartureProjectionPermit permit, BattleWorldActorView view) =>
        !disposed && ReferenceEquals(arbiter.Active, this) && permit != null && ReferenceEquals(permit.Lease, this) &&
        departurePermits.TryGetValue(permit.RuntimeId, out var issued) && ReferenceEquals(issued, permit) &&
        view != null && view.GetInstanceID() == permit.ViewId && view.RuntimeId == permit.RuntimeId;

    internal void DepartView(BattleDepartureProjectionPermit permit)
    {
        if (!pins.TryGetValue(permit.RuntimeId, out var pin) || !OwnsDeparturePermit(permit, pin.View))
            throw new InvalidOperationException("Departure view capability was lost.");
        // Exclude the identity before OnDisable/driver callbacks, without
        // dropping the guard on any survivor or the retained semantic cursor.
        pin.Departed = true;
        surface.WorldRosterPresenter.DepartView(pin.View, permit);
    }

    public DRPGBattleProjectionCheckpoint Capture(DRPGBattleProjectionCheckpointKind kind)
    {
        if (disposed) throw new ObjectDisposedException(nameof(DRPGBattleActionProjectionLease));
        var actors = new List<BattleLeasedActorCheckpoint>();
        foreach (var pair in pins)
        {
            if (pair.Value.Departed) continue;
            if (!TryGetView(pair.Key, out var view))
                throw new InvalidOperationException("Leased view identity lost.");
            actors.Add(new BattleLeasedActorCheckpoint(view, pair.Value.Home, pair.Value.Presented));
        }
        actors.Sort((a, b) => a.RuntimeId.CompareTo(b.RuntimeId));
        return new DRPGBattleProjectionCheckpoint(Owner, kind, ++checkpointVersion,
            actors, surface.transform, surface.ProjectionWorldRoot, surface.ProjectionCamera);
    }

    public ProjectionCursorResult Apply(in BattleProjectionCursor command)
    {
        if (applying) return arbiter.Reject("CursorReentrant", command.Owner, command.After.RuntimeId);
        if (disposed || !ReferenceEquals(arbiter.Active, this))
            return arbiter.Reject("LeaseClosed", command.Owner, command.After.RuntimeId);
        if (!command.Owner.Equals(Owner)) return arbiter.Reject("WrongOwner", command.Owner, command.After.RuntimeId);
        if (command.Ordinal != nextCursor) return arbiter.Reject("CursorOutOfOrder", command.Owner, command.After.RuntimeId);
        if (command.Before.RuntimeId != command.After.RuntimeId ||
            !pins.TryGetValue(command.After.RuntimeId, out var pin) ||
            !TryGetView(command.After.RuntimeId, out var view))
            return arbiter.Reject("ViewIdentityLost", command.Owner, command.After.RuntimeId);
        if (!pin.Presented.MatchesBefore(command.Before) ||
            command.After.HasSettlementState && !command.Before.HasSettlementState)
            return arbiter.Reject("BeforeSnapshotMismatch", command.Owner, command.After.RuntimeId);
        var after = command.After.HasSettlementState ? command.After :
            pin.Presented.WithHp(command.After.Hp, command.After.MaxHp);
        // This is the sole semantic write path; it never binds live DTOs,
        // recalculates formation, restores poses, or releases a pooled actor.
        applying = true;
        try
        {
            surface.ApplyLeasedParticipant(after);
            if (disposed || !ReferenceEquals(arbiter.Active, this))
                return arbiter.Reject("LeaseLostDuringCursor", command.Owner, command.After.RuntimeId);
            pin.Presented = after;
            nextCursor++;
            arbiter.Record("Cursor:" + command.Ordinal + ":" + command.After.RuntimeId);
            return new ProjectionCursorResult(true, null);
        }
        finally { applying = false; }
    }

    public void Dispose() => Close(true);
    internal void Abandon() => Close(false);
    private void Close(bool reconcile)
    {
        if (disposed) return;
        disposed = true;
        List<Exception> faults = null;
        foreach (var owned in ownedPresentation)
        {
            try { owned.Dispose(); }
            catch (Exception error) { if (faults == null) faults = new List<Exception>(); faults.Add(error); }
        }
        ownedPresentation.Clear();
        try { arbiter.Close(this, reconcile); }
        finally { if (faults != null) throw new AggregateException("Owned projection cleanup failed.", faults); }
    }
}
