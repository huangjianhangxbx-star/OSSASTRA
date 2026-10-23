using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// Only displays the already-committed settlement plan. In particular, a lethal
// item retains its actor/X until the separate whole-stage defeat barrier.
internal sealed class BattleSettlementStepPresenter : IDisposable, IBattleSettlementPresentationPort, IDRPGBattlePresentationPump
{
    private readonly DRPGSettlementPresentationPlan plan;
    private readonly DRPGBattleActionProjectionLease lease;
    private readonly BattleImpactFeedbackPresenter feedback;
    private readonly Camera camera;
    private readonly IDRPGBattlePresentationClock clock;
    private readonly BattleDefeatPresentationLayer defeats;
    private readonly Dictionary<int, PresentationReceipt> receipts = new Dictionary<int, PresentationReceipt>();
    private PresentationReceipt active;
    private IBattlePresentationHandle label;
    private int nextStep;
    private double startedAt;
    private long startedFrame;
    private bool disposed;
    private BattleSettlementDefeatBarrier finalization;
    public IDRPGBattlePresentationClock Clock => clock;
    public PresentationReceipt PresentSettlement(in SettlementPresentationCommand command) => Present(command);
    void IDRPGBattlePresentationPump.Tick() => Tick();
    void IDRPGBattlePresentationPump.CancelReceipt(PresentationReceipt receipt, string reason) => Cancel(receipt);
    internal bool IsComplete => !disposed && nextStep == Math.Max(1, plan.Steps.Count) && active != null &&
        active.Completion.IsCompleted && active.Completion.Result.Kind != PresentationReceiptTerminalKind.Faulted;

    internal BattleSettlementStepPresenter(DRPGSettlementPresentationPlan plan, DRPGBattleActionProjectionLease lease,
        BattleImpactFeedbackPresenter feedback, Camera camera, IDRPGBattlePresentationClock clock)
    {
        this.plan = plan ?? throw new ArgumentNullException(nameof(plan));
        this.lease = lease ?? throw new ArgumentNullException(nameof(lease));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.feedback = feedback; this.camera = camera;
        if (lease.Owner != plan.Ledger.Owner || !lease.TryOwnPresentation(this))
            throw new InvalidOperationException("Settlement requires the matching live projection owner.");
        defeats = new BattleDefeatPresentationLayer(lease, feedback, camera);
    }

    internal PresentationReceipt Present(in SettlementPresentationCommand command)
    {
        int step = command.StepOrdinal;
        if (disposed || !lease.IsActive || !ReferenceEquals(command.Plan, plan) || command.Owner != lease.Owner)
            return Reject(command, "Settlement plan/projection identity conflict.");
        if (receipts.TryGetValue(step, out var prior)) return prior;
        if (step != nextStep || active != null && !active.Completion.IsCompleted || command.IssuedAt != clock.Now ||
            double.IsNaN(command.IssuedAt) || double.IsInfinity(command.IssuedAt))
            return Reject(command, "Settlement item is out of order or its timestamp is stale.");
        var builder = new BattleOutcomeProjectionBuilder(lease);
        BattleWorldActorView target = null;
        DRPGBattleHitResult hit = null;
        string caption = string.Empty;
        try
        {
            if (!plan.IsEmpty)
            {
                var item = plan.Steps[step];
                int targetId;
                if (item.Status != null)
                {
                    builder.AppendSettlement(item.Status); targetId = item.Status.Owner.Value;
                    caption = StatusLabel(item.Status);
                }
                else
                {
                    var reaction = item.Reaction;
                    hit = reaction.Batch.Hits.Count == 1 ? reaction.Batch.Hits[0] : null;
                    builder.AppendOutcome(hit, reaction.Batch.Effects); targetId = reaction.TargetParticipant.Value;
                    caption = BattleHitOutcomePresenter.EffectLabel(reaction.Batch.Effects);
                }
                if (!lease.TryGetView(targetId, out target)) throw new InvalidOperationException("Settlement target is no longer pinned.");
            }
        }
        catch (Exception exception) { return Reject(command, exception.Message); }
        active = new PresentationReceipt(command.Owner, command.Stage, step);
        receipts.Add(step, active); startedAt = command.IssuedAt; startedFrame = clock.RenderedFrame;
        try
        {
            foreach (var cursor in builder.Cursors)
            {
                var result = lease.Apply(cursor);
                if (!result.Accepted) throw new InvalidOperationException(result.Reason);
                if (cursor.After.DefeatPending && !defeats.Reveal(new ParticipantId(cursor.After.RuntimeId)))
                    throw new InvalidOperationException("Settlement defeat marker or dim binding is unavailable.");
            }
            label = target == null || feedback == null ? null : hit != null
                ? feedback.PresentStandardOutcome(hit, target.PresentationAnchor.position, camera, clock, startedAt)
                : feedback.PresentStandardLabel(caption, target.PresentationAnchor.position, camera, clock, startedAt);
            nextStep++;
        }
        catch (Exception exception)
        { active.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, exception.Message)); }
        return active;
    }

    internal void Tick()
    {
        if (disposed) return;
        if (!lease.IsActive) { Dispose(); return; }
        defeats.Tick();
        finalization?.Tick();
        if (active == null || active.Completion.IsCompleted) return;
        feedback?.TickStandardOutcomes();
        if (clock.Now - startedAt < (plan.IsEmpty ? 0d : .45d) || clock.RenderedFrame <= startedFrame ||
            label != null && !label.IsSettled) return;
        bool rendered = plan.IsEmpty || label != null && label.Outcome == BattlePresentationOutcome.Completed;
        active.TryComplete(new PresentationReceiptTerminal(rendered ? PresentationReceiptTerminalKind.Completed : PresentationReceiptTerminalKind.Degraded,
            rendered ? "Settlement item rendered." : "Settlement feedback binding unavailable."));
    }
    internal void Cancel(PresentationReceipt receipt)
    {
        if (finalization != null && ReferenceEquals(receipt, finalization.Result.Receipt))
        {
            finalization.Dispose();
            if (!receipt.Completion.IsCompleted) throw new InvalidOperationException("Settlement cleanup cannot substitute for rendered defeat finalization.");
            return;
        }
        if (!ReferenceEquals(active, receipt)) return;
        if (label != null && !label.IsSettled) label.Cancel(BattlePresentationCancelReason.TimedOut);
        label = null;
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; Cancel(active); finalization?.Dispose(); defeats.Dispose();
        active?.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, "Settlement projection closed."));
    }
    private static PresentationReceipt Reject(in SettlementPresentationCommand command, string reason)
    {
        var result = new PresentationReceipt(command.Owner, command.Stage, command.StepOrdinal);
        result.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, reason)); return result;
    }
    internal DRPGBattleDeparturePresentation CompleteDefeats(DRPGBattleDepartureJournal facts, BattleDefeatedActorRoster roster,
        int? stepOrdinal = null)
    {
        int baseline = Math.Max(1, plan.Steps.Count), ordinal = stepOrdinal ?? baseline;
        if (!IsComplete || !lease.IsActive || !facts.HasSameValues(DRPGSettlementDepartureFacts.Describe(plan.Ledger)) ||
            ordinal != baseline && !(facts.Owner.Kind == BattleTransactionKind.TurnEntry && ordinal == baseline + 1) ||
            finalization != null && finalization.Result.Receipt.StepOrdinal != ordinal)
        {
            var rejected = new DRPGBattleDeparturePresentation(facts, ordinal);
            rejected.Receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted,
                "Settlement departure requires every lane and matching sealed death facts.")); return rejected;
        }
        if (finalization == null) finalization = new BattleSettlementDefeatBarrier(plan, facts, lease, defeats, roster, clock, ordinal);
        return finalization.Result;
    }
    private static string StatusLabel(DRPGSettlementStatusStep step)
    {
        var status = step.Status.Before;
        string text = status.Spec.DisplayName + " ×" + status.Stacks;
        float hp = step.HpAfter - step.HpBefore, posture = step.PostureAfter - step.PostureBefore;
        if (hp != 0f) text += "\nHP " + hp.ToString("+0.##;-0.##;0", CultureInfo.InvariantCulture);
        if (posture != 0f) text += "\n架势 " + posture.ToString("+0.##;-0.##;0", CultureInfo.InvariantCulture);
        if (step.Status.After.RemainingTurns == 0) text += "\n状态结束";
        return text;
    }
}
