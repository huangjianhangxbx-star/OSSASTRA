using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// Candidate-only composition of the shared Unity leaves. No Session, rule
// mutation, candidate selection or actor publication belongs to this adapter.
internal sealed class BattleTurnEntryPresentationAdapter : IBattleTurnEntryPresentationPort, IDisposable
{
    private readonly DRPGBattleActionProjectionLease projection;
    private readonly BattleImpactFeedbackPresenter feedback;
    private readonly Camera camera;
    private readonly BattleDefeatedActorRoster defeated;
    private readonly BattleActorMotionController motion;
    private IReadOnlyDictionary<ParticipantId, int> homes;
    private DRPGSettlementPresentationPlan plan;
    private BattleSettlementStepPresenter settlement;
    private PresentationReceipt lifecycle;
    private IBattlePresentationHandle label;
    private DRPGBattleDeparturePresentation departure;
    private BattleFormationReconcileBarrier formation;
    private DRPGBattlePostReturnResult formationRules;
    private double lifecycleAt;
    private long lifecycleFrame;
    private bool disposed;

    internal BattleTurnEntryPresentationAdapter(DRPGBattleActionProjectionLease projection,
        BattleImpactFeedbackPresenter feedback, Camera camera, BattleDefeatedActorRoster defeated,
        IDRPGBattlePresentationClock clock)
    {
        this.projection = projection ?? throw new ArgumentNullException(nameof(projection));
        Clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.feedback = feedback; this.camera = camera; this.defeated = defeated;
        if (projection.Owner.Kind != BattleTransactionKind.TurnEntry || !projection.TryOwnPresentation(this))
            throw new InvalidOperationException("TurnEntry adapter requires its live candidate projection.");
        try { motion = new BattleActorMotionController(projection, clock); }
        catch { Dispose(); throw; }
    }
    public IDRPGBattlePresentationClock Clock { get; }
    private bool IsActive => !disposed && projection.IsActive;
    private int ItemCount => Math.Max(1, plan.Steps.Count);
    private bool NeedsLifecycle => plan.Ledger.TurnEntryLifecycle.CandidateAlive &&
        (plan.Ledger.TurnEntryLifecycle.ClearedDefend || plan.Ledger.TurnEntryLifecycle.SkipAction ||
        !plan.Ledger.TurnEntryLifecycle.Before.Same(plan.Ledger.TurnEntryLifecycle.After) ||
        plan.Ledger.TurnEntryLifecycle.PostureBefore != plan.Ledger.TurnEntryLifecycle.PostureAfter);
    private int DepartureOrdinal => ItemCount + (NeedsLifecycle ? 1 : 0);

    public bool TryCaptureHomes(BattleTransactionIdentity owner, out IReadOnlyDictionary<ParticipantId, int> result)
    {
        result = null;
        if (!IsActive || owner != projection.Owner || plan != null) return false;
        try
        {
            // Recheck even on a repeated query; a displaced field is not a
            // valid entry boundary merely because an earlier query succeeded.
            var captured = projection.FrozenFormation.CaptureEntryHomeSlots(projection);
            if (homes == null) homes = captured;
            result = homes; return true;
        }
        catch (InvalidOperationException error)
        {
            Debug.LogError("[DRPG TurnEntry Home Capture] " + error.Message);
            return false;
        }
    }
    public PresentationReceipt PresentSettlement(in SettlementPresentationCommand command)
    {
        if (!IsActive || homes == null || command.Owner != projection.Owner || command.Plan == null ||
            command.Plan.Ledger.TurnEntryLifecycle == null || command.Stage != DRPGBattleActionStage.TurnBegin ||
            lifecycle != null || departure != null || plan != null && !ReferenceEquals(plan, command.Plan))
            return Reject(command.Owner, command.StepOrdinal, "TurnEntry settlement binding/plan conflict.");
        if (settlement == null)
        {
            plan = command.Plan;
            settlement = new BattleSettlementStepPresenter(plan, projection, feedback, camera, Clock);
        }
        return settlement.Present(command);
    }
    public PresentationReceipt PresentLifecycle(DRPGBattleSettlementLedger ledger, int ordinal)
    {
        if (!IsActive || plan == null || !ReferenceEquals(plan.Ledger, ledger) || !settlement.IsComplete ||
            !NeedsLifecycle || ordinal != ItemCount || departure != null)
            return Reject(ledger == null ? projection.Owner : ledger.Owner, ordinal, "TurnEntry lifecycle is not the next sealed step.");
        if (lifecycle != null) return lifecycle;
        lifecycle = new PresentationReceipt(projection.Owner, DRPGBattleActionStage.TurnBegin, ordinal);
        lifecycleAt = Clock.Now; lifecycleFrame = Clock.RenderedFrame;
        try
        {
            var builder = new BattleOutcomeProjectionBuilder(projection);
            builder.AppendTurnEntryLifecycle(ledger.TurnEntryLifecycle);
            if (!projection.TryGetView(ledger.TurnEntryLifecycle.Actor.Value, out var target))
                throw new InvalidOperationException("Lifecycle candidate view is no longer pinned.");
            foreach (var cursor in builder.Cursors)
            {
                var applied = projection.Apply(cursor);
                if (!applied.Accepted) throw new InvalidOperationException(applied.Reason);
            }
            label = feedback == null ? null : feedback.PresentStandardLabel(LifecycleLabel(ledger.TurnEntryLifecycle),
                target.PresentationAnchor.position, camera, Clock, lifecycleAt);
        }
        catch (Exception error) { lifecycle.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, error.Message)); }
        return lifecycle;
    }
    public DRPGBattleDeparturePresentation CompleteDefeats(DRPGBattleDepartureJournal facts, int ordinal)
    {
        if (!IsActive || plan == null || !settlement.IsComplete || ordinal != DepartureOrdinal ||
            !facts.HasSameValues(DRPGSettlementDepartureFacts.Describe(plan.Ledger)) ||
            NeedsLifecycle && (!SuccessfulLifecycle() || Clock.Now - lifecycleAt < .45d || Clock.RenderedFrame <= lifecycleFrame))
        {
            var rejected = new DRPGBattleDeparturePresentation(facts, ordinal);
            rejected.Receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted,
                "TurnEntry departure requires all status/lifecycle receipts and their exact ordinal.")); return rejected;
        }
        if (departure == null) departure = settlement.CompleteDefeats(facts, defeated, ordinal);
        return departure;
    }
    public PresentationReceipt ReconcileFormation(DRPGBattleSettlementLedger ledger, DRPGBattlePostReturnResult rules, int ordinal)
    {
        if (!IsActive || plan == null || !ReferenceEquals(plan.Ledger, ledger) || rules == null ||
            ordinal != DepartureOrdinal + 1 || departure == null || !departure.Receipt.Completion.IsCompleted ||
            departure.Receipt.Completion.Result.Kind != PresentationReceiptTerminalKind.Completed ||
            !rules.Departures.HasSameValues(departure.Departures) || rules.CommittedRevision != ledger.CommittedRevision + 1 ||
            formation != null && !ReferenceEquals(formationRules, rules))
            return Reject(ledger == null ? projection.Owner : ledger.Owner, ordinal, "TurnEntry formation lacks its exact rendered departure.");
        if (formation == null)
        {
            formationRules = rules;
            formation = new BattleFormationReconcileBarrier(projection, motion, Clock, ledger, rules, ordinal);
        }
        return formation.Receipt;
    }
    public void Tick()
    {
        if (disposed) return;
        if (!projection.IsActive) { Dispose(); return; }
        settlement?.Tick(); motion.Tick(); formation?.Tick();
        if (lifecycle == null || lifecycle.Completion.IsCompleted) return;
        feedback?.TickStandardOutcomes();
        if (Clock.Now - lifecycleAt < .45d || Clock.RenderedFrame <= lifecycleFrame || label != null && !label.IsSettled) return;
        bool rendered = label != null && label.Outcome == BattlePresentationOutcome.Completed;
        lifecycle.TryComplete(new PresentationReceiptTerminal(rendered ? PresentationReceiptTerminalKind.Completed :
            PresentationReceiptTerminalKind.Degraded, rendered ? "TurnEntry lifecycle rendered." : "TurnEntry lifecycle feedback unavailable."));
    }
    public void CancelReceipt(PresentationReceipt receipt, string reason)
    {
        if (receipt == null || receipt.Owner != projection.Owner) return;
        if (ReferenceEquals(receipt, lifecycle)) ClearLabel();
        else if (formation != null && ReferenceEquals(receipt, formation.Receipt)) formation.Dispose();
        else settlement?.Cancel(receipt);
    }
    private bool SuccessfulLifecycle() => lifecycle != null && lifecycle.Completion.IsCompleted &&
        (lifecycle.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed || lifecycle.Completion.Result.Kind == PresentationReceiptTerminalKind.Degraded);
    private void ClearLabel()
    { if (label != null && !label.IsSettled) label.Cancel(BattlePresentationCancelReason.TimedOut); label = null; }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        ClearLabel(); lifecycle?.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, "TurnEntry projection closed."));
        formation?.Dispose(); settlement?.Dispose(); motion?.Dispose();
    }
    private static PresentationReceipt Reject(BattleTransactionIdentity owner, int ordinal, string reason)
    {
        var receipt = new PresentationReceipt(owner, DRPGBattleActionStage.TurnBegin, ordinal);
        receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, reason)); return receipt;
    }
    private static string LifecycleLabel(DRPGTurnEntryLifecycle life)
    {
        string text = life.SkipAction ? "失衡：跳过行动" : !life.Before.Same(life.After) ? "架势恢复" : string.Empty;
        float posture = life.PostureAfter - life.PostureBefore;
        if (posture != 0f) text += (text.Length == 0 ? string.Empty : "\n") + "架势 " + posture.ToString("+0.##;-0.##;0", CultureInfo.InvariantCulture);
        if (life.ClearedDefend) text += (text.Length == 0 ? string.Empty : "\n") + "防御结束";
        return text;
    }
}
