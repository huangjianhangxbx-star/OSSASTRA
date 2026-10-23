#if UNITY_EDITOR
using System;
using UnityEngine;

internal static class DRPGTurnEntryAdapterProbe
{
    // Catches live/caller-authored homes or repeated slots within one side.
    private static void HomesComeFromFrozenUniqueField()
    {
        using (var f = new DRPGTurnEntryPresentationProbe.Fixture(createPresenter: false, fullBattlefield: true, deferToModule: true))
        using (var port = Port(f))
        {
            Check(port.TryCaptureHomes(f.RuleLease.Owner, out var homes) && homes.Count == 6 &&
                homes[new ParticipantId(7101)] == 0 && homes[new ParticipantId(7111)] == 1 &&
                homes[new ParticipantId(7112)] == 2 && homes[new ParticipantId(7102)] == 0 &&
                homes[new ParticipantId(7121)] == 1 && homes[new ParticipantId(7122)] == 2,
                "Production entry did not capture the six unique frozen homes.");
            var foreign = BattleTransactionIdentity.ForTurnEntry(new TurnEntryIdentity(f.Entry.BattleGeneration,
                f.Entry.CandidateSerial + 1, f.Entry.Actor));
            Check(!port.TryCaptureHomes(foreign, out _), "Foreign candidate captured this field.");
            f.CandidateView.transform.position += Vector3.right;
            Check(!port.TryCaptureHomes(f.RuleLease.Owner, out _), "A repeated home query hid a displaced actor.");
        }
    }

    // Catches omission/early projection of recovery and reuse of lifecycle's ordinal by departure.
    private static void SerialRecoveryThenDistinctFinalGates() => RunLifecycle(false, 1);
    private static void EnemyRecoveryUsesSameCursorAndGates() => RunLifecycle(true, 1);
    private static void SkipHasReadableFeedbackWithoutPublishingActor() => RunLifecycle(false, 2);
    private static void ExposureRecoveryProjectsHalfPosture() => RunLifecycle(false, 3);
    private static void RunLifecycle(bool enemy, int lifecycle)
    {
        using (var f = new DRPGTurnEntryPresentationProbe.Fixture(enemy, false, false, true, false, true, lifecycle, true))
        using (var port = Port(f))
        using (var module = new DRPGBattleTurnEntryModule(f.Domain.Session.TransactionState, f.RuleLease, f.Reservation,
            port, f.Clock, () => f.Projection.IsActive))
        {
            float before = f.Candidate.CurrentPosture; var actor = f.Domain.Session.CurrentActor;
            var widgets = enemy ? f.Widgets.Enemy : f.Widgets.Party;
            module.Tick(); Check(module.Fault.Length == 0 && module.Ledger != null, "Entry adapter was unavailable: " + module.Fault);
            Check(f.Projection.GetPresented(f.Candidate.RuntimeId).Posture == before && f.Visuals.ActiveNumbers().Length == 1,
                "Recovery leaked before serial status receipts.");
            Step(f, module, .451); Check(f.Visuals.ActiveNumbers().Length == 1, "Second status is not serial.");
            Step(f, module, .451);
            float expectedPosture = lifecycle == 1 ? 55f : lifecycle == 3 ? 50f : 0f;
            Check(f.Projection.GetPresented(f.Candidate.RuntimeId).Posture == expectedPosture &&
                f.Visuals.ActiveNumbers().Length == 1 && !module.ReadyForFinalization,
                "Lifecycle cursor or its separate visible label is missing.");
            Check(widgets.Posture.text == "架势 " + expectedPosture + " / 100" && Mathf.Abs(widgets.Fill.fillAmount - expectedPosture / 100f) < .000001f,
                "Lifecycle posture widget: text=" + widgets.Posture.text + "; fill=" + widgets.Fill.fillAmount.ToString("R") +
                "; expected=" + expectedPosture + "/100; ratio=" + (expectedPosture / 100f).ToString("R"));
            if (enemy) Check(f.Widgets.World.Posture.text == widgets.Posture.text, "World status plate disagrees with the enemy cursor.");
            string caption = f.Visuals.ActiveNumbers()[0].GetComponent<TMPro.TMP_Text>().text;
            Check(lifecycle == 2 ? caption.Contains("跳过") : caption.Contains(lifecycle == 3 ? "+50" : "+15"),
                "Lifecycle feedback does not communicate its frozen result.");
            double started = f.Clock.Now; Step(f, module, .449);
            Check(!module.ReadyForFinalization && f.Visuals.ActiveNumbers().Length == 1, "Lifecycle label ended before minimum dwell.");
            for (int i = 0; i < 20 && !module.ReadyForFinalization; i++) Step(f, module, .05);
            Check(module.ReadyForFinalization && module.CandidateStillEligible == (lifecycle != 2) &&
                f.Clock.Now - started >= .75 && f.Completed == 0 && f.RuleLease.IsActive &&
                ReferenceEquals(actor, f.Domain.Session.CurrentActor) && f.Visuals.ActiveNumbers().Length == 0,
                "Final gates lost lifecycle ordinal, dwell, cleanup or held candidate ownership: " + module.Fault);
        }
    }

    // Catches lifecycle issuing before status completion and foreign/duplicate-plan projection.
    private static void LifecycleCannotOvertakeStatusOrChangePlan()
    {
        using (var f = new DRPGTurnEntryPresentationProbe.Fixture(createPresenter: false, deferToModule: true, lifecycle: 1))
        using (var port = Port(f))
        {
            Check(port.TryCaptureHomes(f.RuleLease.Owner, out var homes), "Missing actual homes.");
            Check(f.Domain.Session.TransactionState.TryCommitTurnEntry(f.RuleLease, f.Reservation, homes, out var ledger, out var error), error);
            float before = f.Projection.GetPresented(f.Candidate.RuntimeId).Posture;
            Check(Faulted(port.PresentLifecycle(ledger, 2)) && f.Visuals.ActiveNumbers().Length == 0 &&
                f.Projection.GetPresented(f.Candidate.RuntimeId).Posture == before, "Early lifecycle changed projection.");
            var plan = new DRPGSettlementPresentationPlan(ledger);
            port.PresentSettlement(new SettlementPresentationCommand(plan, 0, f.Clock.Now));
            Check(Faulted(port.PresentLifecycle(ledger, 2)) && f.Visuals.ActiveNumbers().Length == 1,
                "Lifecycle overlapped an active status.");
            Check(Faulted(port.PresentSettlement(new SettlementPresentationCommand(new DRPGSettlementPresentationPlan(ledger), 0, f.Clock.Now))),
                "A second plan acquired the cursor.");
        }
    }

    private static void ScopeCloseCleansLifecycleWithoutReleasingDomain()
    {
        using (var f = new DRPGTurnEntryPresentationProbe.Fixture(createPresenter: false, statuses: false, deferToModule: true, lifecycle: 2))
        using (var port = Port(f))
        using (var module = new DRPGBattleTurnEntryModule(f.Domain.Session.TransactionState, f.RuleLease, f.Reservation, port, f.Clock, () => f.Projection.IsActive))
        {
            module.Tick(); Step(f, module, .301);
            Check(f.Visuals.ActiveNumbers().Length == 1, "No lifecycle feedback to test cleanup.");
            f.Projection.Dispose();
            Check(f.Visuals.ActiveNumbers().Length == 0 && f.RuleLease.IsActive && f.Completed == 0,
                "Projection close leaked its label or released rule authority.");
        }
    }

    // Catches a missing optional number binding being treated as a missing
    // numeric cursor or bypassing the rendered-frame/dwell gate.
    private static void MissingFeedbackDegradesOnlyAfterLifecycleWindow()
    {
        using (var f = new DRPGTurnEntryPresentationProbe.Fixture(createPresenter: false, statuses: false, deferToModule: true, lifecycle: 1))
        using (var port = new BattleTurnEntryPresentationAdapter(f.Projection, null, f.Visuals.Camera, f.Defeated, f.Clock))
        using (var module = new DRPGBattleTurnEntryModule(f.Domain.Session.TransactionState, f.RuleLease, f.Reservation, port, f.Clock, () => f.Projection.IsActive))
        {
            module.Tick(); Step(f, module, .301);
            Check(f.Projection.GetPresented(f.Candidate.RuntimeId).Posture == 55, "Missing feedback also removed the real cursor.");
            f.Clock.Now += .50; module.Tick();
            Check(!module.ReadyForFinalization && !module.Degraded, "Elapsed time replaced lifecycle's new rendered frame.");
            Step(f, module, .01);
            for (int i = 0; i < 12 && !module.ReadyForFinalization; i++) Step(f, module, .05);
            Check(module.ReadyForFinalization && module.Degraded && f.RuleLease.IsActive, "Missing optional feedback stalled or released the candidate.");
        }
    }

    private static void DepartureRejectsEarlyAndConflictingOrdinalWithoutChangingOriginalReceipt()
    {
        using (var f = new DRPGTurnEntryPresentationProbe.Fixture(createPresenter: false, statuses: false, deferToModule: true, lifecycle: 2))
        using (var port = Port(f))
        {
            Check(port.TryCaptureHomes(f.RuleLease.Owner, out var homes), "Missing frozen homes.");
            Check(f.Domain.Session.TransactionState.TryCommitTurnEntry(f.RuleLease, f.Reservation, homes, out var ledger, out var error), error);
            var plan = new DRPGSettlementPresentationPlan(ledger);
            port.PresentSettlement(new SettlementPresentationCommand(plan, 0, f.Clock.Now));
            var facts = DRPGSettlementDepartureFacts.Describe(ledger);
            f.Clock.Now += .301; f.Clock.RenderedFrame++; port.Tick();
            Check(Faulted(port.CompleteDefeats(facts, 2).Receipt), "Departure skipped lifecycle.");
            var life = port.PresentLifecycle(ledger, 1);
            Check(ReferenceEquals(life, port.PresentLifecycle(ledger, 1)) && f.Visuals.ActiveNumbers().Length == 1,
                "Lifecycle replay issued a second number.");
            f.Clock.Now += .451; f.Clock.RenderedFrame++; port.Tick();
            Check(Faulted(port.CompleteDefeats(facts, 1).Receipt), "Departure reused lifecycle ordinal.");
            var departure = port.CompleteDefeats(facts, 2);
            Check(!departure.Receipt.Completion.IsCompleted && ReferenceEquals(departure, port.CompleteDefeats(facts, 2)) &&
                Faulted(port.CompleteDefeats(facts, 3).Receipt) && !departure.Receipt.Completion.IsCompleted,
                "Replay replaced, prematurely completed or cancelled the original death barrier.");
        }
    }
    private static BattleTurnEntryPresentationAdapter Port(DRPGTurnEntryPresentationProbe.Fixture f) =>
        new BattleTurnEntryPresentationAdapter(f.Projection, f.Visuals.Feedback, f.Visuals.Camera, f.Defeated, f.Clock);
    private static void Step(DRPGTurnEntryPresentationProbe.Fixture f, DRPGBattleTurnEntryModule module, double seconds)
    { f.Clock.Now += seconds; f.Clock.RenderedFrame++; module.Tick(); Check(module.Fault.Length == 0, module.Fault); }
    private static bool Faulted(PresentationReceipt r) => r != null && r.Completion.IsCompleted && r.Completion.Result.Kind == PresentationReceiptTerminalKind.Faulted;
    private static void Check(bool value, string detail) { if (!value) throw new InvalidOperationException(detail); }
}
#endif
