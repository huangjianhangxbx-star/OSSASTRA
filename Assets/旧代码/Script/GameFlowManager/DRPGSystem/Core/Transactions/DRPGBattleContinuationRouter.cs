using System;
using System.Collections.Generic;

internal interface IBattleStartContinuationRunner
{
    bool TryPrepare(in BattleStartCompletion completion, out BattleTransactionIdentity next, out string error);
    void Start(in BattleStartCompletion completion, BattleRuleMutationLease lease);
}

internal interface IBattleWaveContinuationRunner
{
    bool TryPrepare(in WaveTransitionCompletion completion, out BattleTransactionIdentity next, out string error);
    void Start(in WaveTransitionCompletion completion, BattleRuleMutationLease lease);
}

internal interface IBattleRootContinuationRunner
{
    bool TryPrepare(in RootActionCompletion completion, out BattleTransactionIdentity next, out string error);
    void Start(in RootActionCompletion completion, BattleRuleMutationLease lease);
}

internal interface IBattleTurnEntryContinuationRunner
{
    bool TryPrepare(in DRPGTurnEntrySettlementCompletion completion, out BattleTransactionIdentity next, out string error);
    void Start(in DRPGTurnEntrySettlementCompletion completion, BattleRuleMutationLease lease);
}

internal sealed class DRPGBattleContinuationInbox : IBattleContinuationPort
{
    private readonly Dictionary<BattleTransactionIdentity, Entry> sealedEntries =
        new Dictionary<BattleTransactionIdentity, Entry>();
    private readonly Queue<Entry> pending = new Queue<Entry>();

    internal DRPGBattleContinuationInbox(DRPGBattleTransactionArbiter arbiter)
    { Arbiter = arbiter ?? throw new ArgumentNullException(nameof(arbiter)); }
    internal DRPGBattleTransactionArbiter Arbiter { get; }
    internal int PendingCount => pending.Count;
    internal bool TryGetSealed(BattleTransactionIdentity owner, out Entry entry) => sealedEntries.TryGetValue(owner, out entry);

    public ContinuationSubmitResult Submit<TCompletion>(in TCompletion completion)
        where TCompletion : struct, IBattleTransactionCompletionPayload
    {
        if (typeof(TCompletion) == typeof(LegacyActionCompletion))
            return SubmitLegacy((LegacyActionCompletion)(object)completion);
        if (typeof(TCompletion) == typeof(RootActionCompletion))
            return SubmitRoot((RootActionCompletion)(object)completion);
        if (typeof(TCompletion) == typeof(WaveTransitionCompletion))
            return SubmitWave((WaveTransitionCompletion)(object)completion);
        if (typeof(TCompletion) == typeof(DRPGTurnEntrySettlementCompletion))
            return SubmitTurnEntry((DRPGTurnEntrySettlementCompletion)(object)completion);
        // Register exact payload types, never a kind-only cast.
        if (typeof(TCompletion) != typeof(BattleStartCompletion))
            return Reject("No exact continuation payload type is registered.");
        var value = (BattleStartCompletion)(object)completion;
        if (!value.Identity.IsValid || value.Kind != BattleTransactionCompletionKind.BattleStart ||
            value.Owner.BattleGeneration != Arbiter.BattleGeneration || value.Departures == null ||
            value.Departures.Owner != value.Owner || value.Departures.Members.Count != 0 ||
            value.Fingerprint == 0 || value.Departures.SourceFingerprint != value.Fingerprint)
            return Reject("Startup completion has missing or inconsistent identity/facts.");
        if (sealedEntries.TryGetValue(value.Owner, out var known))
            return !known.IsTurnEntry && !known.IsRoot && !known.IsWave && known.Value.Fingerprint == value.Fingerprint && known.Value.Departures.HasSameValues(value.Departures)
                ? new ContinuationSubmitResult(ContinuationSubmitStatus.Duplicate)
                : Reject("The sealed completion conflicts with this fingerprint or payload.");
        if (!Arbiter.TryCaptureCurrentLease(value.Owner, out var lease))
            return Reject("Only the current transaction owner may submit completion.");
        var entry = new Entry(value, lease);
        sealedEntries.Add(value.Owner, entry);
        pending.Enqueue(entry);
        return new ContinuationSubmitResult(ContinuationSubmitStatus.Accepted);
    }

    internal bool TryTake(out Entry entry)
    {
        entry = pending.Count == 0 ? null : pending.Dequeue();
        return entry != null;
    }

    private ContinuationSubmitResult SubmitLegacy(in LegacyActionCompletion value)
    {
        if (!value.Identity.IsValid || value.Owner.BattleGeneration != Arbiter.BattleGeneration || value.Lease == null ||
            value.Departures == null || value.Departures.Owner != value.Owner || value.Fingerprint == 0)
            return Reject("Legacy completion has missing or inconsistent facts.");
        if (sealedEntries.TryGetValue(value.Owner, out var known))
            return known.IsLegacy && known.Legacy.HasSameValues(value) ? new ContinuationSubmitResult(ContinuationSubmitStatus.Duplicate) : Reject("Conflicting Legacy completion.");
        if (!Arbiter.Owns(value.Lease, value.Owner)) return Reject("Legacy completion lost its original lease.");
        var entry = new Entry(value, value.Lease); sealedEntries.Add(value.Owner, entry); pending.Enqueue(entry);
        return new ContinuationSubmitResult(ContinuationSubmitStatus.Accepted);
    }

    private ContinuationSubmitResult SubmitWave(in WaveTransitionCompletion value)
    {
        if (!value.Identity.IsValid || value.Owner.BattleGeneration != Arbiter.BattleGeneration ||
            value.Lease == null || value.ActivatedMembers == null || value.ActivatedMembers.Count == 0 ||
            value.Departures == null || value.Departures.Owner != value.Owner || value.Departures.Members.Count != 0 ||
            value.Fingerprint == 0 || value.Departures.SourceFingerprint != value.Fingerprint)
            return Reject("Wave completion has missing or inconsistent sealed facts.");
        if (sealedEntries.TryGetValue(value.Owner, out var known))
            return known.IsWave && known.Wave.HasSameValues(value)
                ? new ContinuationSubmitResult(ContinuationSubmitStatus.Duplicate) : Reject("Conflicting sealed Wave completion.");
        if (!Arbiter.TryCaptureCurrentLease(value.Owner, out var lease) || !ReferenceEquals(lease, value.Lease))
            return Reject("Wave completion lost its original physical lease.");
        var entry = new Entry(value, lease);
        sealedEntries.Add(value.Owner, entry); pending.Enqueue(entry);
        return new ContinuationSubmitResult(ContinuationSubmitStatus.Accepted);
    }

    private ContinuationSubmitResult SubmitRoot(in RootActionCompletion value)
    {
        if (!value.Identity.IsValid || value.Owner.BattleGeneration != Arbiter.BattleGeneration ||
            value.Lease == null || value.Departures == null || value.Departures.Owner != value.Owner ||
            value.Fingerprint == 0 || value.Terminal.Kind == RootActionTerminalKind.Faulted)
            return Reject("Root completion has missing or inconsistent sealed facts.");
        if (sealedEntries.TryGetValue(value.Owner, out var known))
            return known.IsRoot && known.Root.HasSameValues(value)
                ? new ContinuationSubmitResult(ContinuationSubmitStatus.Duplicate) : Reject("Conflicting sealed Root completion.");
        if (!Arbiter.TryCaptureCurrentLease(value.Owner, out var lease) || !ReferenceEquals(lease, value.Lease))
            return Reject("Root completion lost its original physical lease.");
        var entry = new Entry(value, lease);
        sealedEntries.Add(value.Owner, entry); pending.Enqueue(entry);
        return new ContinuationSubmitResult(ContinuationSubmitStatus.Accepted);
    }

    private ContinuationSubmitResult SubmitTurnEntry(in DRPGTurnEntrySettlementCompletion value)
    {
        if (!value.Owner.IsValid || value.Owner.Kind != BattleTransactionKind.TurnEntry ||
            value.Owner.BattleGeneration != Arbiter.BattleGeneration || value.Departures == null ||
            value.Departures.Owner != value.Owner || value.Departures.Stage != DRPGBattleActionStage.TurnBegin ||
            value.Fingerprint == 0 || value.Departures.SourceFingerprint == 0 || value.Lease == null)
            return Reject("TurnEntry completion has inconsistent identity or departure facts.");
        bool valid;
        switch (value.Kind)
        {
            case DRPGTurnEntrySettlementCompletionKind.ActorStillEligible:
                valid = value.Turn.IsValid && value.Turn.Actor == value.Owner.TurnEntry.Actor &&
                    value.Turn.BattleGeneration == value.Owner.BattleGeneration &&
                    value.Turn.TurnSerial == value.Owner.TurnEntry.CandidateSerial && value.Outcome == DRPGBattleOutcome.None;
                break;
            case DRPGTurnEntrySettlementCompletionKind.SelectAgain:
            case DRPGTurnEntrySettlementCompletionKind.WavePending:
                valid = !value.Turn.IsValid && value.Outcome == DRPGBattleOutcome.None; break;
            case DRPGTurnEntrySettlementCompletionKind.BattleResult:
                valid = !value.Turn.IsValid && (value.Outcome == DRPGBattleOutcome.Defeat || value.Outcome == DRPGBattleOutcome.Victory); break;
            default: valid = false; break;
        }
        if (!valid) return Reject("TurnEntry completion result conflicts with its actor token or outcome.");
        if (sealedEntries.TryGetValue(value.Owner, out var known))
            return known.IsTurnEntry && known.TurnEntry.HasSameValues(value)
                ? new ContinuationSubmitResult(ContinuationSubmitStatus.Duplicate)
                : Reject("The sealed TurnEntry completion conflicts with the submitted payload.");
        if (!Arbiter.TryCaptureCurrentLease(value.Owner, out var lease) || !ReferenceEquals(lease, value.Lease))
            return Reject("TurnEntry completion lost its original physical lease.");
        var entry = new Entry(value, lease);
        sealedEntries.Add(value.Owner, entry); pending.Enqueue(entry);
        return new ContinuationSubmitResult(ContinuationSubmitStatus.Accepted);
    }

    private static ContinuationSubmitResult Reject(string error) =>
        new ContinuationSubmitResult(ContinuationSubmitStatus.Rejected, error);

    internal sealed class Entry
    {
        internal Entry(LegacyActionCompletion value, BattleRuleMutationLease lease)
        { Legacy = value; Lease = lease; IsLegacy = true; }
        internal bool IsLegacy { get; }
        internal LegacyActionCompletion Legacy { get; }
        internal Entry(WaveTransitionCompletion value, BattleRuleMutationLease lease)
        { Wave = value; Lease = lease; IsWave = true; }
        internal bool IsWave { get; }
        internal WaveTransitionCompletion Wave { get; }
        internal Entry(BattleStartCompletion value, BattleRuleMutationLease lease)
        { Value = value; Lease = lease; }
        internal Entry(DRPGTurnEntrySettlementCompletion value, BattleRuleMutationLease lease)
        { TurnEntry = value; Lease = lease; IsTurnEntry = true; }
        internal Entry(RootActionCompletion value, BattleRuleMutationLease lease)
        { Root = value; Lease = lease; IsRoot = true; }
        internal bool IsRoot { get; }
        internal RootActionCompletion Root { get; }
        internal bool IsTurnEntry { get; }
        internal DRPGTurnEntrySettlementCompletion TurnEntry { get; }
        internal BattleStartCompletion Value { get; }
        internal BattleRuleMutationLease Lease { get; }
    }
}

internal sealed class DRPGBattleContinuationRouter
{
    private readonly DRPGBattleContinuationInbox inbox;
    private readonly IBattleStartContinuationRunner startRunner;
    private readonly IBattleTurnEntryContinuationRunner turnEntryRunner;
    private readonly IBattleRootContinuationRunner rootRunner;
    private readonly IBattleWaveContinuationRunner waveRunner;
    private readonly IBattleLegacyContinuationRunner legacyRunner;
    private bool pumping;

    internal DRPGBattleContinuationRouter(DRPGBattleContinuationInbox inbox, IBattleStartContinuationRunner startRunner,
        IBattleTurnEntryContinuationRunner turnEntryRunner = null, IBattleRootContinuationRunner rootRunner = null,
        IBattleWaveContinuationRunner waveRunner = null, IBattleLegacyContinuationRunner legacyRunner = null)
    {
        this.inbox = inbox ?? throw new ArgumentNullException(nameof(inbox));
        this.startRunner = startRunner;
        this.turnEntryRunner = turnEntryRunner;
        this.rootRunner = rootRunner;
        this.waveRunner = waveRunner;
        this.legacyRunner = legacyRunner;
    }
    internal string Fault { get; private set; } = string.Empty;

    // One delivery per externally driven pump; never drain completions produced
    // by the callback on this stack. The old lease remains held until transfer.
    internal bool Pump()
    {
        if (pumping || Fault.Length != 0) return false;
        pumping = true;
        try
        {
            if (!inbox.TryTake(out var entry)) return false;
            if (entry.IsLegacy) return DeliverLegacy(entry);
            if (entry.IsWave) return DeliverWave(entry);
            if (entry.IsRoot) return DeliverRoot(entry);
            if (entry.IsTurnEntry) return DeliverTurnEntry(entry);
            var completion = entry.Value;
            if (startRunner == null) return Fail("No startup continuation runner is bound.");
            if (!inbox.Arbiter.Owns(entry.Lease, completion.Owner))
                return Fail("Queued completion lost its exact mutation lease.");
            if (!startRunner.TryPrepare(completion, out var next, out string error))
                return Fail(string.IsNullOrEmpty(error) ? "Continuation preparation was rejected." : error);
            if (!next.IsValid || (next.Kind != BattleTransactionKind.TurnEntry && next.Kind != BattleTransactionKind.WaveTransition) ||
                next.BattleGeneration != completion.Owner.BattleGeneration)
                return Fail("Startup may continue only to a matching-battle Wave or TurnEntry.");
            if (!inbox.Arbiter.Owns(entry.Lease, completion.Owner))
                return Fail("Continuation preparation changed the active mutation owner.");
            if (!inbox.Arbiter.TransferToContinuation(entry.Lease, next, out var lease, out error))
                return Fail(error);
            startRunner.Start(completion, lease);
            return true;
        }
        catch (Exception error)
        {
            // Keep the current owner closed for its explicit fault cleanup;
            // never release input or retry a partially started runner here.
            return Fail(error.ToString());
        }
        finally { pumping = false; }
    }

    private bool Fail(string error)
    { Fault = string.IsNullOrEmpty(error) ? "Continuation failed." : error; return false; }

    private bool DeliverLegacy(DRPGBattleContinuationInbox.Entry entry)
    {
        var value = entry.Legacy;
        if (legacyRunner == null || !inbox.Arbiter.Owns(entry.Lease, value.Owner)) return Fail("Legacy continuation owner is unavailable.");
        if (!legacyRunner.TryPrepare(value, out var next, out var error)) return Fail(error);
        if (!inbox.Arbiter.Owns(entry.Lease, value.Owner)) return Fail("Legacy preparation changed mutation ownership.");
        var lease = entry.Lease;
        if (value.Outcome == DRPGBattleOutcome.None)
        {
            var expected = value.WavePending ? BattleTransactionKind.WaveTransition : BattleTransactionKind.TurnEntry;
            if (!next.IsValid || next.BattleGeneration != value.Owner.BattleGeneration || next.Kind != expected)
                return Fail("Legacy continuation requires a matching candidate.");
            if (!inbox.Arbiter.TransferToContinuation(lease, next, out lease, out error)) return Fail(error);
        }
        else if (next != value.Owner) return Fail("Legacy result must retain its notification owner.");
        legacyRunner.Start(value, lease); return true;
    }

    private bool DeliverWave(DRPGBattleContinuationInbox.Entry entry)
    {
        var value = entry.Wave;
        if (waveRunner == null) return Fail("No Wave continuation runner is bound.");
        if (!inbox.Arbiter.Owns(entry.Lease, value.Owner) || !ReferenceEquals(entry.Lease, value.Lease))
            return Fail("Queued Wave lost its original physical lease.");
        if (!waveRunner.TryPrepare(value, out var next, out var error)) return Fail(error);
        if (!next.IsValid || next.Kind != BattleTransactionKind.TurnEntry || next.BattleGeneration != value.Owner.BattleGeneration)
            return Fail("Completed Wave may continue only to a matching-battle TurnEntry.");
        if (!inbox.Arbiter.Owns(entry.Lease, value.Owner)) return Fail("Wave preparation changed its physical mutation owner.");
        if (!inbox.Arbiter.TransferToContinuation(entry.Lease, next, out var lease, out error)) return Fail(error);
        waveRunner.Start(value, lease);
        return true;
    }

    private bool DeliverRoot(DRPGBattleContinuationInbox.Entry entry)
    {
        var value = entry.Root;
        if (rootRunner == null) return Fail("No Root continuation runner is bound.");
        if (!inbox.Arbiter.Owns(entry.Lease, value.Owner) || !ReferenceEquals(entry.Lease, value.Lease))
            return Fail("Queued Root lost its original physical lease.");
        if (!rootRunner.TryPrepare(value, out var next, out var error)) return Fail(error);
        if (!inbox.Arbiter.Owns(entry.Lease, value.Owner)) return Fail("Root preparation changed ownership.");
        var lease = entry.Lease;
        bool result = value.Outcome == DRPGBattleOutcomeCandidate.Victory || value.Outcome == DRPGBattleOutcomeCandidate.Defeat;
        if (result)
        {
            // Result receiver retains the original owner through all terminal
            // notifications, then releases it after cleanup. No new actor.
            if (next != value.Owner) return Fail("Root result must retain its notification lease.");
        }
        else
        {
            var kind = value.Outcome == DRPGBattleOutcomeCandidate.WavePending
                ? BattleTransactionKind.WaveTransition : BattleTransactionKind.TurnEntry;
            if (!next.IsValid || next.BattleGeneration != value.Owner.BattleGeneration || next.Kind != kind)
                return Fail("Root outcome does not match its continuation identity.");
            if (!inbox.Arbiter.TransferToContinuation(entry.Lease, next, out lease, out error)) return Fail(error);
        }
        rootRunner.Start(value, lease);
        return true;
    }

    private bool DeliverTurnEntry(DRPGBattleContinuationInbox.Entry entry)
    {
        var completion = entry.TurnEntry;
        if (!inbox.Arbiter.Owns(entry.Lease, completion.Owner) || !ReferenceEquals(entry.Lease, completion.Lease))
            return Fail("Queued TurnEntry lost its original physical lease.");
        if (turnEntryRunner == null) return Fail("No TurnEntry continuation runner is bound.");
        if (!turnEntryRunner.TryPrepare(completion, out var next, out var error))
            return Fail(string.IsNullOrEmpty(error) ? "TurnEntry continuation preparation was rejected." : error);
        if (!inbox.Arbiter.Owns(entry.Lease, completion.Owner))
            return Fail("TurnEntry continuation preparation changed the active owner.");
        var lease = entry.Lease;
        if (completion.Kind == DRPGTurnEntrySettlementCompletionKind.SelectAgain || completion.Kind == DRPGTurnEntrySettlementCompletionKind.WavePending)
        {
            var expectedKind = completion.Kind == DRPGTurnEntrySettlementCompletionKind.WavePending ? BattleTransactionKind.WaveTransition : BattleTransactionKind.TurnEntry;
            if (!next.IsValid || next == completion.Owner || next.Kind != expectedKind ||
                next.BattleGeneration != completion.Owner.BattleGeneration)
                return Fail("SelectAgain requires a new matching-battle candidate identity.");
            if (!inbox.Arbiter.TransferToContinuation(entry.Lease, next, out lease, out error)) return Fail(error);
        }
        else if (next != completion.Owner)
            return Fail("Actor publication or result handling must retain the completed entry owner.");
        // The receiver publishes Session events or handles the final result
        // before releasing this lease. There is no command-open gap here.
        turnEntryRunner.Start(completion, lease);
        return true;
    }
}
