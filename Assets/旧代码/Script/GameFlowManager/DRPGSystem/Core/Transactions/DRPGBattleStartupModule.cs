using System;

internal enum BattleStartupPrepareStatus { Prepared, PresentationUnavailable, Busy, Closed }

internal sealed class DRPGBattleStartupModule
{
    private readonly DRPGBattleIdentityAuthority identities;
    private readonly DRPGBattleTransactionArbiter arbiter;
    private readonly DRPGBattleTransactionPresentationPortSlot ports;
    private readonly IBattleContinuationPort continuation;
    private readonly IDRPGBattlePresentationClock clock;
    private IStandardBasicAttackPresentationPort boundPort;
    private BattleRuleMutationLease lease;
    private PresentationReceipt entryReceipt;
    private PresentationCleanupReceipt cleanup;
    private bool queued, issued, ticking, cancelling;
    private bool invokingPresentation, cleanupStarted;
    private string cancelReason;
    private bool retainLeaseOnClose;

    internal DRPGBattleStartupModule(DRPGBattleIdentityAuthority identities, DRPGBattleTransactionArbiter arbiter,
        DRPGBattleTransactionPresentationPortSlot ports, IBattleContinuationPort continuation, IDRPGBattlePresentationClock clock)
    {
        this.identities = identities ?? throw new ArgumentNullException(nameof(identities));
        this.arbiter = arbiter ?? throw new ArgumentNullException(nameof(arbiter));
        this.ports = ports ?? throw new ArgumentNullException(nameof(ports));
        this.continuation = continuation ?? throw new ArgumentNullException(nameof(continuation));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        if (identities.BattleGeneration != arbiter.BattleGeneration)
            throw new ArgumentException("Startup services must belong to one battle graph.");
    }
    internal BattleStartIdentity Identity { get; private set; }
    internal bool CompletionSubmitted { get; private set; }
    internal string Fault { get; private set; } = string.Empty;
    internal bool IsClosed { get; private set; }

    internal BattleStartupPrepareStatus PrepareStart()
    {
        if (IsClosed || cancelling || Fault.Length != 0) return BattleStartupPrepareStatus.Closed;
        if (Identity.IsValid) return Owns ? BattleStartupPrepareStatus.Prepared : BattleStartupPrepareStatus.Closed;
        if (!ports.TryGet(identities.BattleGeneration, out var port) || !(port is IBattleStartPresentationPort))
            return BattleStartupPrepareStatus.PresentationUnavailable;
        if (arbiter.HasOwner) return BattleStartupPrepareStatus.Busy;
        var identity = identities.AllocateBattleStart();
        if (!arbiter.TryAcquire(BattleTransactionIdentity.ForBattleStart(identity), out var acquired, out _))
            return BattleStartupPrepareStatus.Busy;
        Identity = identity; lease = acquired; boundPort = port;
        return BattleStartupPrepareStatus.Prepared;
    }

    internal bool EnqueuePreparedStartup(BattleStartIdentity identity)
    {
        if (!identity.IsValid || identity != Identity || !Owns || queued || IsClosed || cancelling || Fault.Length != 0) return false;
        queued = true;
        return true;
    }

    internal void Tick()
    {
        if (IsClosed || ticking) return;
        ticking = true;
        try
        {
            if (cancelling) { PollCleanup(); return; }
            if (!queued || CompletionSubmitted || Fault.Length != 0) return;
            if (!Owns) { Fail("Startup mutation ownership was lost."); return; }
            if (!HasBoundPort)
            { Fail("Startup presentation binding changed after preparation."); return; }
            if (!issued)
            {
                issued = true;
                invokingPresentation = true;
                try { entryReceipt = ((IBattleStartPresentationPort)boundPort).PresentEntry(new BattleStartEntryCommand(Identity, clock.Now)); }
                finally { invokingPresentation = false; if (cancelling) BeginCleanup(); }
                if (cancelling || IsClosed) return;
                if (!Owns) { Fail("Entry issue callback changed startup ownership."); return; }
                if (!HasBoundPort) { Fail("Entry issue callback unbound startup presentation."); return; }
                if (entryReceipt == null || entryReceipt.Owner != lease.Owner || entryReceipt.Stage != DRPGBattleActionStage.TurnBegin || entryReceipt.StepOrdinal != 0)
                { Fail("Entry returned a missing or unrelated startup receipt."); return; }
            }
            invokingPresentation = true;
            try { (boundPort as IDRPGBattlePresentationPump)?.Tick(); }
            finally { invokingPresentation = false; if (cancelling) BeginCleanup(); }
            if (cancelling || IsClosed) return;
            if (!Owns) { Fail("Entry presentation pump changed startup ownership."); return; }
            if (!HasBoundPort) { Fail("Entry presentation pump unbound startup presentation."); return; }
            if (!entryReceipt.Completion.IsCompleted) return;
            var terminal = entryReceipt.Completion.Result;
            if (terminal.Kind == PresentationReceiptTerminalKind.Faulted)
            { Fail("Startup presentation fault: " + terminal.Detail); return; }
            if (terminal.Kind != PresentationReceiptTerminalKind.Completed && terminal.Kind != PresentationReceiptTerminalKind.Degraded)
            { Fail("Unknown startup presentation terminal."); return; }
            ulong fingerprint = CompletionFingerprint(terminal.Kind);
            var departures = new DRPGBattleDepartureJournal(lease.Owner, fingerprint, Array.Empty<DRPGBattleDepartureMember>());
            var result = continuation.Submit(new BattleStartCompletion(Identity, departures, fingerprint));
            if (!result.IsAccepted) { Fail(result.Error); return; }
            CompletionSubmitted = true;
        }
        catch (Exception error) { Fail(error.ToString()); }
        finally { ticking = false; }
    }

    internal void Cancel(string reason, bool retainLeaseUntilOwnerCloses = false)
    {
        if (IsClosed || cancelling) return;
        retainLeaseOnClose = retainLeaseUntilOwnerCloses;
        cancelling = true;
        cancelReason = reason ?? "Startup cancelled.";
        if (!invokingPresentation) BeginCleanup();
    }

    private void BeginCleanup()
    {
        if (cleanupStarted || IsClosed) return;
        cleanupStarted = true;
        if (!Identity.IsValid || !issued || CompletionSubmitted && !Owns) { Close(); return; }
        try
        {
            cleanup = boundPort.EmergencyCleanup(new BattlePresentationCleanupCommand(
                BattleTransactionIdentity.ForBattleStart(Identity), cancelReason));
            if (cleanup == null) { Fail("Startup cleanup returned no receipt."); return; }
            PollCleanup();
        }
        catch (Exception error) { Fail("Startup cleanup failed: " + error); }
    }

    private void PollCleanup()
    {
        if (cleanup == null || !cleanup.Completion.IsCompleted) return;
        if (!cleanup.Completion.Result) { Fail("Startup cleanup did not confirm that owned handles were cleared."); return; }
        Close();
    }
    private void Close()
    {
        if (Owns && !retainLeaseOnClose) lease.Dispose();
        IsClosed = true;
    }
    private bool Owns => lease != null && arbiter.Owns(lease, lease.Owner);
    private bool HasBoundPort => ports.TryGet(Identity.BattleGeneration, out var currentPort) && ReferenceEquals(currentPort, boundPort);
    private void Fail(string error) { if (Fault.Length == 0) Fault = string.IsNullOrEmpty(error) ? "Startup failed." : error; }
    private ulong CompletionFingerprint(PresentationReceiptTerminalKind kind)
    {
        unchecked
        {
            ulong hash = 14695981039346656037UL;
            hash = (hash ^ (ulong)Identity.BattleGeneration) * 1099511628211UL;
            hash = (hash ^ (ulong)Identity.StartSerial) * 1099511628211UL;
            hash = (hash ^ (ulong)entryReceipt.ReceiptId.Value) * 1099511628211UL;
            hash = (hash ^ (ulong)kind) * 1099511628211UL;
            return hash == 0 ? 1UL : hash;
        }
    }
}
