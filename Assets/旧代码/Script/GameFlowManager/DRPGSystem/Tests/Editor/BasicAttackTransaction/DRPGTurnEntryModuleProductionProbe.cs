#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Actual module and shared Unity leaves on real rendered frames. Old Session
// constructs the battlefield only; this is not formal startup/Flow/F8 coverage.
public static class DRPGTurnEntryModuleProductionProbe
{
    public static string Status { get; private set; } = "NotRun";
    public static string StartProduction()
    {
        if (!Application.isPlaying || Status == "Running") throw new InvalidOperationException("Fresh Play Mode required.");
        Status = "Running"; new GameObject("TurnEntryModuleRealFrameProbe").AddComponent<Host>().Begin(); return Status;
    }
    public static string StartFlowPublication()
    {
        if (!Application.isPlaying || Status == "Running") throw new InvalidOperationException("Fresh Play Mode required.");
        Status = "Running"; new GameObject("TurnEntryFlowRealFrameProbe").AddComponent<Host>().Begin(true); return Status;
    }
    private sealed class Host : MonoBehaviour
    {
        private IEnumerator work;
        private bool flowPublication;
        private string loggedError;
        internal void Begin(bool flow = false)
        {
            flowPublication = flow; work = flow ? RunFlowPublication() : Run();
            Application.logMessageReceived += OnLog; StartCoroutine(Drive());
        }
        private void OnLog(string condition, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) loggedError = condition; }
        private IEnumerator Drive()
        {
            while (true)
            {
                bool next;
                try
                {
                    next = work.MoveNext();
                    if (loggedError != null) throw new InvalidOperationException("Console error during real-frame probe: " + loggedError);
                }
                catch (Exception error) { Status = "FAIL: " + error; break; }
                if (!next) { Status = flowPublication ? "PASS: 3 real-frame Flow/Session publications (player, enemy, delayed defeat)" :
                    "PASS: 9 TurnEntry production-adapter real-frame cases (empty, serial player/enemy, full-roster deaths, recovery/skip)"; break; }
                yield return work.Current;
            }
            (work as IDisposable)?.Dispose(); work = null;
            Application.logMessageReceived -= OnLog; Destroy(gameObject);
        }
        private void OnDestroy()
        { Application.logMessageReceived -= OnLog; (work as IDisposable)?.Dispose(); work = null; if (Status == "Running") Status = "Interrupted: host destroyed"; }
    }

    // Real flow update + real adapter, starting at an explicit turn-begin
    // boundary. This does not claim BattleStart or the F8 asset graph is wired.
    private static IEnumerator RunFlowPublication()
    {
        for (int index = 0; index < 3; index++)
        using (var f = new DRPGTurnEntryPresentationProbe.Fixture(index == 1, index == 2, false, true, false, true))
        using (var directorBinding = new PublicationDirectorBinding(f.Domain.Root.GetComponent<DRPGBattlePresentationDirector>()))
        {
            bool lethal = index == 2;
            var session = f.Domain.Session;
            var fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var identities = (DRPGBattleIdentityAuthority)typeof(DRPGBattleSession).GetField("transactionIdentityAuthority", fields).GetValue(session);
            var flow = (DRPGBattleFlowOrchestrator)typeof(DRPGBattleSession).GetField("flow", fields).GetValue(session);
            flow.Reset();
            Check(flow.TryStartBattle(out var error) && flow.TryBeginTurn(out error), index, error);
            var allocated = identities.AllocateTurnEntry(f.Entry.Actor);
            Check(allocated == f.Entry && identities.TryBeginTurnEntry(allocated, f.RuleLease), index, "Wrong actual Session identity boundary.");
            Check(session.TransactionState.TurnCandidates.TryReserve(out var reservation, out error), index, error);
            var live = f.Domain.Root.AddComponent<UnityRenderFrameFence>(); f.Clock.UseLiveClock(live);
            var oldToken = session.CurrentActorTurnToken;
            var driver = new DRPGBattleFlowStateMachine();
            typeof(DRPGBattleFlowStateMachine).GetField("session", fields).SetValue(driver, session);
            typeof(DRPGBattleFlowStateMachine).GetField("isRunning", fields).SetValue(driver, true);
            int published = 0; bool inputOpenedInEvent = false;
            int ended = 0;
            session.CurrentActorChanged += _ => { published++; inputOpenedInEvent |= session.TransactionArbiter.IsCommandInputOpen; };
            session.BattleEnded += _ => { ended++; inputOpenedInEvent |= session.TransactionArbiter.IsCommandInputOpen; };
            session.BattleEnded += (Action<DRPGBattleResult>)Delegate.CreateDelegate(typeof(Action<DRPGBattleResult>), driver,
                typeof(DRPGBattleFlowStateMachine).GetMethod("OnBattleEnded", fields));
            using (var port = new RealLeaves(f))
            using (var module = new DRPGBattleTurnEntryModule(session.TransactionState, f.RuleLease, reservation, port, f.Clock, () => f.Projection.IsActive))
            {
                Check(session.TryEnqueueTurnEntry(module, f.RuleLease, f.Projection, out error), index, error);
                double began = f.Clock.Now, deadline = began + 10;
                int frames = 0;
                while (published == 0 && ended == 0 && f.Clock.Now < deadline)
                {
                    yield return null;
                    driver.Update(Time.unscaledDeltaTime); frames++;
                    Check(driver.LastError.Length == 0, index, driver.LastError);
                    f.Domain.Bridge.FlushQueuedSignals();
                    Check(f.Visuals.ActiveNumbers().Length <= 1, index, "Status numbers overlapped in real flow.");
                    if (published == 0 && ended == 0)
                        Check(session.CurrentActorTurnToken == oldToken && f.RuleLease.IsActive && !session.TransactionArbiter.IsCommandInputOpen,
                            index, "Real frame published a candidate before its final render gate.");
                }
                if (lethal)
                {
                    Check(ended == 1 && published == 0 && !inputOpenedInEvent && !f.RuleLease.IsActive &&
                        !f.Projection.IsActive && session.Outcome == DRPGBattleOutcome.Defeat && session.CurrentActor == null &&
                        !session.CurrentActorTurnToken.IsValid && driver.Session == null && f.Completed == 0 &&
                        f.Defeated.Count == 1 && f.Domain.Pool.ActiveCount == 1 && f.Clock.Now - began >= .75 && frames >= 4,
                        index, "Lethal real flow skipped visible death/transfer or failed final result delivery.");
                    continue;
                }
                Check(ended == 0 && published == 1 && !inputOpenedInEvent && session.CurrentActor == f.Candidate &&
                    session.CurrentActorTurnToken.TurnSerial == allocated.CandidateSerial && !f.RuleLease.IsActive &&
                    !f.Projection.IsActive && session.TransactionArbiter.IsCommandInputOpen && f.Completed == 0 &&
                    f.Candidate.CurrentHp == 93 && f.Clock.Now - began >= 1.20 && frames >= 4,
                    index, "Real flow failed actor publication, serial dwell, cleanup or input ordering.");
            }
        }
    }
    // Actor publication emits the real legacy TurnActivated signal. Bind its
    // director even though this fixture supplies no legacy animation recipes;
    // the transaction's status/departure leaves above remain the real adapter.
    private sealed class PublicationDirectorBinding : IDisposable
    {
        private readonly DRPGBattlePresentationDirector director;
        private readonly BattlePresentationRecipeSetSO recipes;
        internal PublicationDirectorBinding(DRPGBattlePresentationDirector director)
        {
            this.director = director;
            recipes = ScriptableObject.CreateInstance<BattlePresentationRecipeSetSO>();
            director.Bind(recipes);
        }
        public void Dispose()
        {
            director.Unbind(); UnityEngine.Object.DestroyImmediate(recipes);
        }
    }
    private static IEnumerator Run()
    {
        var cases = new[] { new[] { false, false, false }, new[] { false, false, true }, new[] { true, false, true },
            new[] { false, true, true }, new[] { true, true, true }, new[] { false, false, true },
            new[] { true, false, true }, new[] { false, false, true }, new[] { false, false, true } };
        for (int index = 0; index < cases.Length; index++)
        {
            var c = cases[index];
            int lifecycle = index < 5 ? 0 : index < 7 ? 1 : index == 7 ? 2 : 3;
            using (var f = new DRPGTurnEntryPresentationProbe.Fixture(c[0], c[1], false, c[2], c[1], true, lifecycle, lifecycle != 0))
            {
                var live = f.Domain.Root.AddComponent<UnityRenderFrameFence>(); f.Clock.UseLiveClock(live);
                var previousActor = f.Domain.Session.CurrentActor;
                long revision = f.Domain.Session.TransactionState.BattleRevision;
                double began = f.Clock.Now, deadline = began + 10d;
                int initialCount = c[1] ? 6 : 2;
                var candidates = f.Domain.Session.TransactionState.TurnCandidates;
                Check(candidates.TryReserve(out var reservation, out var reservationError), index, reservationError);
                Check(ReferenceEquals(reservation.Candidate.Participant, f.Candidate), index, "State reserved another candidate.");
                using (var port = new RealLeaves(f))
                using (var module = new DRPGBattleTurnEntryModule(f.Domain.Session.TransactionState, f.RuleLease, reservation,
                    port, f.Clock, () => f.Projection.IsActive))
                {
                    Check(module.Ledger == null && f.Ledger == null, index, "Fixture precommitted the module's work.");
                    Check(!candidates.TryPrepareFinalization(module, out _, out _), index, "Unstarted real module authorized finalization.");
                    module.Tick();
                    Check(module.Fault.Length == 0 && module.Ledger != null, index, module.Fault);
                    while (!module.ReadyForFinalization && f.Clock.Now < deadline)
                    {
                        yield return null; module.Tick();
                        f.Domain.Surface.RenderCombatants(DRPGBattlePresentationSnapshot.Capture(f.Domain.Session));
                        f.Domain.Bridge.FlushQueuedSignals();
                        Check(module.Fault.Length == 0 && f.Visuals.ActiveNumbers().Length <= 1, index, "Module/serial feedback: " + module.Fault);
                        if (!module.ReadyForFinalization)
                            Check(!candidates.TryPrepareFinalization(module, out _, out _), index, "Real render gate was bypassed.");
                        Check(f.RuleLease.IsActive && f.Completed == 0 && ReferenceEquals(previousActor, f.Domain.Session.CurrentActor),
                            index, "Module published an actor, ActionCompleted or released input.");
                        if (port.Departure == null && c[1])
                            Check(f.CandidateView.GetInstanceID() == f.ViewId && f.CandidateView.gameObject.activeInHierarchy &&
                                f.Visuals.ActiveDefeatMarkers().Length == 1 && f.Body.color.r <= .401f && f.Defeated.Count == 0 &&
                                f.Domain.Pool.ActiveCount == initialCount, index, "Lethal candidate lost X/dim/original view before finalization.");
                        if (port.Departure == null)
                            Check(f.Domain.Session.TransactionState.BattleRevision == revision + 1, index, "Rules reconciled before departure issuance.");
                    }
                    Check(module.ReadyForFinalization && module.CandidateStillEligible == (!c[1] && lifecycle != 2) &&
                        f.Domain.Session.TransactionState.BattleRevision == revision + 2 &&
                        f.Domain.Session.TransactionState.CaptureRuleSnapshot().CurrentLogicalTime == (float)reservation.ReservedLogicalTime,
                        index, "Module never reached its held terminal or repeated logical time/rule commits.");
                    Check(candidates.TryPrepareFinalization(module, out var finalization, out var finalizationError), index, finalizationError);
                    Check(finalization.Reservation.Actor == f.Entry.Actor && finalization.Lifecycle.CandidateAlive == !c[1] &&
                        finalization.Lifecycle.SkipAction == (lifecycle == 2) && candidates.HasReservation && f.RuleLease.IsActive &&
                        ReferenceEquals(previousActor, f.Domain.Session.CurrentActor), index, "Real preflight consumed or published a candidate.");
                    Check(f.Clock.Now - began >= (c[2] ? c[1] ? .45 : .90 : .30) + .30 + (lifecycle == 0 ? 0 : .45) && port.Departure != null &&
                        port.Departure.Receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed,
                        index, "Real items/finalization did not satisfy rendered dwell.");
                    Check(f.Domain.Pool.ActiveCount == initialCount - (c[1] ? 1 : 0), index, "Module did not complete actual view departure.");
                    if (lifecycle != 0)
                    {
                        Check(f.Projection.GetPresented(f.Candidate.RuntimeId).Posture == (lifecycle == 1 ? 55 : lifecycle == 3 ? 50 : 0) &&
                            port.SawLifecycle && port.Departure.Receipt.StepOrdinal == 3,
                            index, "Production lifecycle did not reveal posture/label or used the old death ordinal.");
                        var widgets = c[0] ? f.Widgets.Enemy : f.Widgets.Party;
                        Check(widgets.Posture.text == "架势 " + (lifecycle == 1 ? 55 : lifecycle == 3 ? 50 : 0) + " / 100" &&
                            widgets.Posture.isActiveAndEnabled && widgets.Posture.rectTransform.rect.width > 0,
                            index, "Actual posture widget did not render the candidate lifecycle.");
                    }
                    if (c[1])
                    {
                        var view = port.Departure.Views[0];
                        Check(view.OriginalViewInstanceId == f.ViewId && (c[0] ? view.IncapacitatedModelInstanceId == 0 :
                            f.Defeated.IsVisible(view.Participant, view.IncapacitatedModelInstanceId)), index, "Wrong final defeated view/model.");
                        Check(port.SawMotion, index, "Whole-roster return teleported or never moved.");
                    }
                    var checkpoint = f.Projection.Capture(DRPGBattleProjectionCheckpointKind.PostSettlementReconcileCheckpoint);
                    for (int a = 0; a < checkpoint.Actors.Count; a++)
                    for (int b = a + 1; b < checkpoint.Actors.Count; b++)
                        Check(checkpoint.Actors[a].Home.Position != checkpoint.Actors[b].Home.Position, index, "Final Home Slots overlap.");
                }
            }
        }
    }

    // Observation-only wrapper around the actual production adapter; it does
    // not implement a substitute leaf or synthesize any receipt.
    private sealed class RealLeaves : IBattleTurnEntryPresentationPort, IDisposable
    {
        private readonly DRPGTurnEntryPresentationProbe.Fixture f;
        private readonly BattleTurnEntryPresentationAdapter adapter;
        private bool formation;
        private readonly Dictionary<int, Vector3> origins = new Dictionary<int, Vector3>();
        internal DRPGBattleDeparturePresentation Departure;
        internal bool SawMotion;
        internal bool SawLifecycle;
        internal RealLeaves(DRPGTurnEntryPresentationProbe.Fixture f)
        { this.f = f; adapter = new BattleTurnEntryPresentationAdapter(f.Projection, f.Visuals.Feedback, f.Visuals.Camera, f.Defeated, f.Clock); }
        public IDRPGBattlePresentationClock Clock => f.Clock;
        public bool TryCaptureHomes(BattleTransactionIdentity owner, out IReadOnlyDictionary<ParticipantId, int> homes)
        { return adapter.TryCaptureHomes(owner, out homes); }
        public PresentationReceipt PresentSettlement(in SettlementPresentationCommand command)
        {
            return adapter.PresentSettlement(command);
        }
        public PresentationReceipt PresentLifecycle(DRPGBattleSettlementLedger ledger, int ordinal)
        {
            var receipt = adapter.PresentLifecycle(ledger, ordinal);
            SawLifecycle = f.Visuals.ActiveNumbers().Length == 1;
            return receipt;
        }
        public DRPGBattleDeparturePresentation CompleteDefeats(DRPGBattleDepartureJournal facts, int ordinal)
        { Departure = adapter.CompleteDefeats(facts, ordinal); return Departure; }
        public PresentationReceipt ReconcileFormation(DRPGBattleSettlementLedger ledger, DRPGBattlePostReturnResult rules, int ordinal)
        {
            foreach (var view in f.Domain.Roster.ActiveViews) origins.Add(view.RuntimeId, view.transform.position);
            formation = true;
            return adapter.ReconcileFormation(ledger, rules, ordinal);
        }
        public void Tick()
        {
            adapter.Tick();
            if (formation) foreach (var view in f.Domain.Roster.ActiveViews)
                SawMotion |= view.transform.position != origins[view.RuntimeId];
        }
        public void CancelReceipt(PresentationReceipt receipt, string reason)
        {
            adapter.CancelReceipt(receipt, reason);
        }
        public void Dispose() { adapter.Dispose(); }
    }
    private static void Check(bool value, int index, string detail)
    { if (!value) throw new InvalidOperationException("TurnEntry module real-frame case " + index + ": " + detail); }
}
#endif
