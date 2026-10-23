#if UNITY_EDITOR
using System;
using System.Collections.Generic;

internal static class DRPGBattleContinuationProbe
{
    // Break caught: Submit calls the next runner inline, or releases input before transferring ownership.
    internal static void SubmissionWaitsForPumpAndTransfersWithoutInputGap()
    {
        using (var test = new Fixture())
        {
            var payload = test.Payload();
            Check(test.Inbox.Submit(payload).Status == ContinuationSubmitStatus.Accepted, "Valid startup completion was rejected.");
            Check(test.Inbox.PendingCount == 1 && test.Runner.Starts == 0 && test.Arbiter.Owns(test.Lease, payload.Owner),
                "Submit dispatched inline or released the startup lease.");
            Check(test.Router.Pump(), "Pump did not consume the queued completion.");
            Check(test.Runner.Starts == 1 && test.Runner.ObservedInputOpen == false &&
                test.Arbiter.CurrentOwner.Kind == BattleTransactionKind.TurnEntry && !test.Lease.IsActive,
                "Startup did not transfer atomically to one TurnEntry owner.");
            Check(test.Inbox.PendingCount == 0 && !test.Router.Pump(), "The same completion dispatched twice.");
        }
    }

    // Break caught: repeated callback creates a second runner; conflicting payload replaces the sealed one.
    internal static void DuplicateIsIdempotentAndConflictingFingerprintIsRejected()
    {
        using (var test = new Fixture())
        {
            var payload = test.Payload();
            Check(test.Inbox.Submit(payload).Status == ContinuationSubmitStatus.Accepted, "Initial completion rejected.");
            Check(test.Inbox.Submit(payload).Status == ContinuationSubmitStatus.Duplicate, "Matching queued replay was not idempotent.");
            Check(!test.Inbox.Submit(test.Payload(32)).IsAccepted, "Conflicting fingerprint replaced the queued payload.");
            Check(test.Inbox.PendingCount == 1, "A replay changed the queue length.");
            Check(test.Router.Pump() && test.Runner.SeenFingerprint == 31, "Pump consumed changed live data instead of the sealed payload.");
            Check(test.Inbox.Submit(payload).Status == ContinuationSubmitStatus.Duplicate && !test.Router.Pump(),
                "Post-dispatch replay restarted a runner.");
        }
    }

    // Break caught: Kind-only dispatch lets another runtime payload type impersonate BattleStartCompletion.
    internal static void UnknownTypeMissingPayloadAndForeignOwnerAreRejected()
    {
        using (var test = new Fixture())
        {
            var valid = test.Payload();
            Check(!test.Inbox.Submit(default(BattleStartCompletion)).IsAccepted, "Default payload was accepted.");
            Check(!test.Inbox.Submit(new Spoof(valid)).IsAccepted, "An unknown struct impersonated the startup kind.");
            var foreignId = new BattleStartIdentity(test.Id.BattleGeneration + 1, 1);
            Check(!test.Inbox.Submit(Payload(foreignId, 31)).IsAccepted, "A foreign battle entered this inbox.");
            Check(test.Inbox.PendingCount == 0 && test.Arbiter.Owns(test.Lease, valid.Owner), "Rejected input changed ownership or the queue.");
            Check(test.Inbox.Submit(valid).IsAccepted && test.Router.Pump() && test.Runner.Starts == 1,
                "Invalid payloads poisoned a later legitimate startup.");
        }
    }

    // Break caught: matching identity alone authorizes a queued callback after release/reacquire of a new lease.
    internal static void ReacquiredOwnerCannotConsumeAnOlderQueuedLease()
    {
        using (var test = new Fixture())
        {
            var payload = test.Payload();
            Check(test.Inbox.Submit(payload).IsAccepted, "Valid completion rejected.");
            test.Lease.Dispose();
            Check(test.Arbiter.TryAcquire(payload.Owner, out var replacement, out string error), error);
            using (replacement)
            {
                Check(!test.Router.Pump() && test.Router.Fault.Length > 0, "A stale queued capability was dispatched.");
                Check(test.Runner.Starts == 0 && test.Arbiter.Owns(replacement, payload.Owner),
                    "Stale queue handling released or replaced the newer owner.");
            }
        }
    }

    // Break caught: a synchronous callback pumps the inbox recursively or duplicates its own delivery.
    internal static void RunnerCallbackCannotReenterThePump()
    {
        using (var test = new Fixture())
        {
            var payload = test.Payload();
            test.Runner.OnStart = () =>
            {
                Check(!test.Router.Pump(), "Runner callback recursively pumped continuation.");
                Check(test.Inbox.Submit(payload).Status == ContinuationSubmitStatus.Duplicate, "Dispatch-time replay enqueued again.");
            };
            Check(test.Inbox.Submit(payload).IsAccepted && test.Router.Pump(), "Valid startup did not dispatch.");
            Check(test.Runner.Starts == 1 && test.Inbox.PendingCount == 0 && test.Router.Fault.Length == 0,
                "Reentry duplicated, faulted, or left a pending delivery.");
        }
    }

    // Break caught: the runner's prepare callback changes owner, but the pump starts anyway.
    internal static void OwnershipIsRecheckedAfterPreparingTheNextRunner()
    {
        using (var test = new Fixture())
        {
            test.Runner.OnPrepare = () => test.Lease.Dispose();
            Check(test.Inbox.Submit(test.Payload()).IsAccepted, "Valid startup rejected.");
            Check(!test.Router.Pump() && test.Router.Fault.Length > 0 && test.Runner.Starts == 0,
                "Preparation released ownership but continuation still started.");
        }
    }

    // Break caught: an unsupported next owner, or a throwing runner, leaks an open command interval.
    internal static void BadRouteAndStartFailureRemainFailClosed()
    {
        using (var test = new Fixture())
        {
            test.Runner.InvalidNext = true;
            Check(test.Inbox.Submit(test.Payload()).IsAccepted, "Valid startup rejected.");
            Check(!test.Router.Pump() && test.Router.Fault.Length > 0 && test.Runner.Starts == 0 &&
                test.Arbiter.Owns(test.Lease, test.Lease.Owner), "Invalid route changed ownership or ran a module.");
        }
        using (var test = new Fixture())
        {
            test.Runner.OnStart = () => throw new InvalidOperationException("controlled runner failure");
            Check(test.Inbox.Submit(test.Payload()).IsAccepted, "Valid startup rejected.");
            Check(!test.Router.Pump() && test.Router.Fault.Contains("controlled runner failure") &&
                !test.Arbiter.IsCommandInputOpen && test.Arbiter.CurrentOwner.Kind == BattleTransactionKind.TurnEntry,
                "Start failure escaped, reopened input, or lost the new cleanup owner.");
            Check(!test.Router.Pump() && test.Runner.Starts == 1, "Faulted dispatch restarted itself.");
        }
    }

    internal static void DepartureMetadataCannotLieAboutStartupCompletion()
    {
        using (var test = new Fixture())
        {
            var owner = BattleTransactionIdentity.ForBattleStart(test.Id);
            foreach (var invalid in new[] { (DRPGBattleDepartureJournal)null,
                new DRPGBattleDepartureJournal(owner, 32, Array.Empty<DRPGBattleDepartureMember>()),
                new DRPGBattleDepartureJournal(owner, 31, new[] { new DRPGBattleDepartureMember(new ParticipantId(1), DRPGBattleSide.Player) }) })
            {
                bool rejected = false;
                try { new BattleStartCompletion(test.Id, invalid, 31); }
                catch (ArgumentException) { rejected = true; }
                Check(rejected, "Startup accepted missing/mismatched/nonempty departure metadata.");
            }
            Check(test.Inbox.Submit(test.Payload()).IsAccepted && test.Router.Pump(), "A valid explicit empty journal failed after malformed input.");
        }
    }

    private sealed class Fixture : IDisposable
    {
        internal Fixture()
        {
            Id = new BattleStartIdentity(820, 1);
            Arbiter = new DRPGBattleTransactionArbiter(Id.BattleGeneration,
                new DRPGBattleActionTraceWriter(new DRPGBoundedActionTraceSink(32)));
            Check(Arbiter.TryAcquire(BattleTransactionIdentity.ForBattleStart(Id), out var lease, out string error), error);
            Lease = lease; Inbox = new DRPGBattleContinuationInbox(Arbiter);
            Runner = new Runner(Arbiter);
            Router = new DRPGBattleContinuationRouter(Inbox, Runner);
        }
        internal readonly BattleStartIdentity Id;
        internal readonly DRPGBattleTransactionArbiter Arbiter;
        internal readonly BattleRuleMutationLease Lease;
        internal readonly DRPGBattleContinuationInbox Inbox;
        internal readonly Runner Runner;
        internal readonly DRPGBattleContinuationRouter Router;
        internal BattleStartCompletion Payload(ulong fingerprint = 31) => DRPGBattleContinuationProbe.Payload(Id, fingerprint);
        public void Dispose() { Runner.NextLease?.Dispose(); Lease.Dispose(); }
    }

    // A typed runner boundary, not a replacement for the real inbox/arbiter/identity capability.
    private sealed class Runner : IBattleStartContinuationRunner
    {
        private readonly DRPGBattleTransactionArbiter arbiter;
        internal Runner(DRPGBattleTransactionArbiter arbiter) { this.arbiter = arbiter; }
        internal int Starts;
        internal bool ObservedInputOpen;
        internal ulong SeenFingerprint;
        internal bool InvalidNext;
        internal Action OnStart;
        internal Action OnPrepare;
        internal BattleRuleMutationLease NextLease;
        public bool TryPrepare(in BattleStartCompletion completion, out BattleTransactionIdentity next, out string error)
        {
            Check(!arbiter.IsCommandInputOpen && arbiter.CurrentOwner == completion.Owner, "Preparing continuation ran outside startup ownership.");
            OnPrepare?.Invoke();
            next = InvalidNext ? completion.Owner : BattleTransactionIdentity.ForTurnEntry(
                new TurnEntryIdentity(completion.Identity.BattleGeneration, 1, new ParticipantId(5)));
            error = string.Empty; return true;
        }
        public void Start(in BattleStartCompletion completion, BattleRuleMutationLease lease)
        {
            NextLease = lease; Starts++; SeenFingerprint = completion.Fingerprint;
            ObservedInputOpen = arbiter.IsCommandInputOpen;
            Check(arbiter.Owns(lease, lease.Owner), "Runner did not receive the real active continuation lease.");
            OnStart?.Invoke();
        }
    }

    private readonly struct Spoof : IBattleTransactionCompletionPayload
    {
        private readonly BattleStartCompletion source;
        internal Spoof(BattleStartCompletion source) { this.source = source; }
        public BattleTransactionIdentity Owner => source.Owner;
        public BattleTransactionCompletionKind Kind => source.Kind;
        public DRPGBattleDepartureJournal Departures => source.Departures;
        public ulong Fingerprint => source.Fingerprint;
    }

    private static BattleStartCompletion Payload(BattleStartIdentity id, ulong fingerprint) =>
        new BattleStartCompletion(id, new DRPGBattleDepartureJournal(BattleTransactionIdentity.ForBattleStart(id),
            fingerprint, Array.Empty<DRPGBattleDepartureMember>()), fingerprint);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
