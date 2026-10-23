using System;
using System.Collections.Generic;
using UnityEngine;

// One ledger-bound owner dispatches HP, number and recoil at one timestamp.
// It never resolves rules or reads live HP through a bound view model.
internal sealed class BattleHitOutcomePresenter : IDisposable
{
    private sealed class PendingOutcome
    {
        internal DRPGBattleHitResult Hit;
        internal IReadOnlyList<DRPGActionEffectResult> Effects;
        internal List<BattleProjectionCursor> Cursors;
        internal int Actor, Target;
    }
    private readonly DRPGBattleActionResolutionLedger ledger;
    private readonly DRPGBattleActionProjectionLease lease;
    private readonly BattleActorMotionController motion;
    private readonly BattleImpactFeedbackPresenter feedback;
    private readonly Camera camera;
    private readonly IDRPGBattlePresentationClock clock;
    private readonly IBattleAudioCueSink audio;
    private readonly BattleDefeatPresentationLayer defeats;
    private readonly Action<DRPGReactionLedgerEntry> reactionBegan;
    private readonly Dictionary<int, PresentationReceipt> receipts = new Dictionary<int, PresentationReceipt>();
    private PresentationReceipt active, recoil;
    private IBattlePresentationHandle label;
    private BattleWorldHitFlash flash;
    private double started;
    private long frame;
    private int nextStep;
    private bool disposed;
    private bool audioDegraded;
    private PendingOutcome pending;
    internal bool ReadyForReturn => !disposed && nextStep > ledger.Reactions.Count &&
        active != null && active.Completion.IsCompleted && active.Completion.Result.Kind != PresentationReceiptTerminalKind.Faulted;

    internal BattleHitOutcomePresenter(DRPGBattleActionResolutionLedger ledger, DRPGBattleActionProjectionLease lease,
        BattleActorMotionController motion, BattleImpactFeedbackPresenter feedback, Camera camera,
        IDRPGBattlePresentationClock clock, IBattleAudioCueSink audio = null,
        Action<DRPGReactionLedgerEntry> reactionBegan = null)
    {
        this.ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        this.lease = lease ?? throw new ArgumentNullException(nameof(lease));
        this.motion = motion ?? throw new ArgumentNullException(nameof(motion));
        this.feedback = feedback; this.camera = camera;
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.audio = audio ?? NullBattleAudioCueSink.Instance;
        this.reactionBegan = reactionBegan;
        defeats = new BattleDefeatPresentationLayer(lease, feedback, camera);
        if (!lease.TryOwnPresentation(this)) throw new InvalidOperationException("Hit outcome requires a live projection scope.");
    }

    internal DRPGBattleDeparturePresentation CompleteDefeats(DRPGBattleDepartureJournal facts, BattleReturnBarrier barrier,
        BattleDefeatedActorRoster roster)
    {
        if (facts.SourceFingerprint != ledger.Fingerprint || !facts.Owner.Equals(lease.Owner) || !ReadyForReturn)
        {
            var rejected = new DRPGBattleDeparturePresentation(facts);
            rejected.Receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, "Departure ledger provenance mismatch."));
            return rejected;
        }
        return defeats.Transfer(facts, barrier, roster, clock);
    }
    internal void TickDepartures() => defeats.TickDeparture(clock);

    internal PresentationReceipt Present(DRPGBattleActionResolutionLedger source, int step, double issuedAt)
    {
        if (!ReferenceEquals(source, ledger) || !lease.IsActive || disposed || lease.Owner.Action != ledger.Identity)
            return Rejected(source?.Identity ?? ledger.Identity, step, "Hit outcome ledger/lease conflict.");
        if (receipts.TryGetValue(step, out var prior)) return prior;
        if (step < 0 || step > ledger.Reactions.Count || step != nextStep || active != null && !active.Completion.IsCompleted ||
            double.IsNaN(issuedAt) || double.IsInfinity(issuedAt) || issuedAt != clock.Now)
            return Rejected(ledger.Identity, step, "Hit outcome is out of order or its timestamp is stale.");
        var hit = step == 0 ? ledger.PrimaryImpact.Hit : ledger.Reactions[step - 1].Batch.Hits.Count == 1
            ? ledger.Reactions[step - 1].Batch.Hits[0] : null;
        var effects = step == 0 ? ledger.PrimaryImpact.Effects : ledger.Reactions[step - 1].Batch.Effects;
        int targetId = hit != null ? hit.TargetRuntimeId : ledger.Reactions[step - 1].TargetParticipant.Value;
        int actorId = hit != null ? hit.ActorRuntimeId : ledger.Reactions[step - 1].SourceParticipant.Value;
        if (!lease.TryGetView(targetId, out var target) || !lease.TryGetView(actorId, out var actor))
            return Rejected(ledger.Identity, step, "Hit outcome actor/target is no longer pinned.");
        if (!TryBuildCursors(step, hit, effects, out var cursors))
            return Rejected(ledger.Identity, step, "Outcome semantic cursor disagrees with the immutable ledger.");
        if (step > 0 && hit != null && !motion.StageReaction(ledger.Reactions[step - 1], issuedAt))
            return Rejected(ledger.Identity, step, "Reaction motion could not acquire its pinned participants.");
        if (step > 0) reactionBegan?.Invoke(ledger.Reactions[step - 1]);
        active = new PresentationReceipt(ledger.Identity, DRPGBattleActionStage.ImpactAndReactions, step);
        receipts.Add(step, active); started = issuedAt; frame = clock.RenderedFrame;
        recoil = null;
        audioDegraded = false;
        pending = new PendingOutcome { Hit = hit, Effects = effects, Cursors = cursors, Actor = actorId, Target = targetId };
        if (step > 0)
        {
            label = feedback == null ? null : feedback.PresentStandardLabel(ReactionName(ledger.Reactions[step - 1].Category),
                actor.PresentationAnchor.position, camera, clock, issuedAt);
        }
        else RevealOutcome(true);
        return active;
    }

    private void RevealOutcome(bool visible)
    {
        var outcome = pending;
        pending = null;
        if (outcome == null) return;
        if (!lease.IsActive || !lease.TryGetView(outcome.Target, out var target) || !lease.TryGetView(outcome.Actor, out var actor))
        {
            active.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, "Reaction lost its pinned actor before outcome."));
            return;
        }
        // The compact reaction name has its own short preparation. HP and all
        // outcome cues are still issued together after that rendered interval.
        started = clock.Now; frame = clock.RenderedFrame;
        foreach (var cursor in outcome.Cursors)
        {
            var applied = lease.Apply(cursor);
            if (!applied.Accepted) { active.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, applied.Reason)); return; }
            if (cursor.After.DefeatPending && !defeats.Reveal(new ParticipantId(cursor.After.RuntimeId)))
            {
                active.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, "Defeat marker or world dim binding is unavailable."));
                return;
            }
        }
        var hit = outcome.Hit;
        recoil = null;
        if (visible && hit != null)
        {
            flash?.Dispose();
            flash = hit.Outcome == DRPGHitOutcome.Evaded ? null : new BattleWorldHitFlash(target, started);
            int direction = target.MotionRoot.position.x >= actor.MotionRoot.position.x ? 1 : -1;
            var beat = new HitOutcomeBeatId(ledger.Identity, active.StepOrdinal);
            recoil = hit.Outcome == DRPGHitOutcome.Evaded
                ? motion.Dodge(beat, new ParticipantId(hit.TargetRuntimeId), direction, started)
                : motion.Recoil(new RecoilMotionCommand(beat, new ParticipantId(hit.TargetRuntimeId), direction, .45f, started));
        }
        label = !visible || feedback == null ? null : hit != null
            ? feedback.PresentStandardOutcome(hit, target.PresentationAnchor.position, camera, clock, started)
            : feedback.PresentStandardLabel(EffectLabel(outcome.Effects), target.PresentationAnchor.position, camera, clock, started);
        if (visible && hit != null)
        {
            string beat = "StandardOutcome/" + ledger.Identity.BattleGeneration + "/" + ledger.Identity.TurnSerial + "/" +
                ledger.Identity.ActionId + "/" + active.StepOrdinal;
            if (hit.Outcome != DRPGHitOutcome.Evaded)
                PlayAudio(new BattleAudioCueRequest(BattleAudioCue.Attack, hit.ActorRuntimeId, hit.TargetRuntimeId, hit.SkillId, beat));
            var cue = hit.Outcome == DRPGHitOutcome.Evaded ? BattleAudioCue.Miss :
                hit.Outcome == DRPGHitOutcome.Guarded ? BattleAudioCue.Guard : BattleAudioCue.Hit;
            PlayAudio(new BattleAudioCueRequest(cue, hit.ActorRuntimeId, hit.TargetRuntimeId, hit.SkillId, beat));
        }
        do { nextStep++; }
        while (nextStep <= ledger.Reactions.Count && !ledger.Reactions[nextStep - 1].RequiresPresentation);
    }

    private void PlayAudio(BattleAudioCueRequest request)
    {
        // Playback is a cue-local boundary: do not retry the cue or replay the
        // immutable damage result when an external audio service fails.
        try { audio.Play(request); }
        catch (Exception) { audioDegraded = true; }
    }

    private static string ReactionName(DRPGImmediateReactionCategory category)
    {
        switch (category)
        {
            case DRPGImmediateReactionCategory.Counterattack: return "反击";
            case DRPGImmediateReactionCategory.Reflected: return "反伤";
            case DRPGImmediateReactionCategory.Pursuit: return "追击";
            case DRPGImmediateReactionCategory.DefenseOrEvade: return "应对";
            default: return "即时反应";
        }
    }

    private bool TryBuildCursors(int step, DRPGBattleHitResult hit, IReadOnlyList<DRPGActionEffectResult> effects,
        out List<BattleProjectionCursor> cursors)
    {
        cursors = null;
        try
        {
            var projection = new BattleOutcomeProjectionBuilder(lease);
            if (step == 0) projection.AppendCosts(ledger.MutationJournal.ActionCosts?.Resources);
            projection.AppendOutcome(hit, effects);
            bool hasLaterOutcome = false;
            for (int i = step; i < ledger.Reactions.Count; i++)
                if (ledger.Reactions[i].RequiresPresentation) { hasLaterOutcome = true; break; }
            // Equipment completion is after the entire reaction closure. It
            // shares the last result beat, never the earlier primary cursor.
            if (!hasLaterOutcome) projection.AppendWeaponCompletions(ledger.Facts.Weapons);
            cursors = projection.Cursors;
            return true;
        }
        catch (Exception) { return false; }
    }

    internal static string EffectLabel(IReadOnlyList<DRPGActionEffectResult> effects)
    {
        var labels = new List<string>();
        foreach (var effect in effects)
        {
            if (!effect.WasApplied) continue;
            switch (effect.Kind)
            {
                case DRPGActionEffectKind.Heal: labels.Add("治疗 +" + Mathf.CeilToInt(effect.AppliedAmount)); break;
                case DRPGActionEffectKind.Shield: labels.Add("护盾 +" + Mathf.CeilToInt(effect.AppliedAmount)); break;
                case DRPGActionEffectKind.PostureRestore: labels.Add("架势恢复"); break;
                case DRPGActionEffectKind.PostureDamage: labels.Add("架势受损"); break;
                case DRPGActionEffectKind.ApplyStatus: labels.Add("状态变化"); break;
                case DRPGActionEffectKind.TimelineShift: labels.Add("行动顺序变化"); break;
                case DRPGActionEffectKind.FormationShift: labels.Add("阵型变化"); break;
            }
        }
        return string.Join("\n", labels);
    }

    internal void Tick()
    {
        if (disposed || active == null || active.Completion.IsCompleted) return;
        if (!lease.IsActive) { Dispose(); return; }
        flash?.Tick(clock.Now);
        feedback?.TickStandardOutcomes();
        if (pending != null)
        {
            if (clock.Now - started < .12d || clock.RenderedFrame <= frame) return;
            if (label != null && !label.IsSettled) label.Cancel(BattlePresentationCancelReason.ScopeEnded);
            label = null;
            RevealOutcome(true);
            return;
        }
        if (clock.Now - started < .45d || clock.RenderedFrame <= frame ||
            recoil != null && !recoil.Completion.IsCompleted || label != null && !label.IsSettled) return;
        if (recoil != null && recoil.Completion.Result.Kind == PresentationReceiptTerminalKind.Faulted)
        { active.TryComplete(recoil.Completion.Result); return; }
        bool complete = label != null && label.Outcome == BattlePresentationOutcome.Completed && !audioDegraded;
        active.TryComplete(new PresentationReceiptTerminal(complete ? PresentationReceiptTerminalKind.Completed : PresentationReceiptTerminalKind.Degraded,
            complete ? "Single world outcome rendered." : audioDegraded ? "Outcome audio playback failed; visual result retained." : "World outcome label unavailable."));
    }
    internal void TickDefeatMarkers() => defeats.Tick();
    internal bool DefeatsAreVisibleAtHome() => defeats.IsVisibleAtHome();

    internal void Cancel(PresentationReceipt receipt)
    {
        if (!ReferenceEquals(receipt, active)) return;
        flash?.Dispose(); flash = null;
        if (label != null && !label.IsSettled) label.Cancel(BattlePresentationCancelReason.TimedOut);
        label = null;
        // Timeout advances only the semantic presentation cursor, never rules;
        // a scope disposal must not expose any future result while closing.
        if (!disposed && pending != null) RevealOutcome(false);
        if (recoil != null && !recoil.Completion.IsCompleted) motion.CancelOwnedReceipt(recoil);
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        defeats.Dispose();
        Cancel(active);
        active?.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, "Hit outcome owner closed."));
    }
    private static PresentationReceipt Rejected(ActionIdentity id, int step, string reason)
    {
        var result = new PresentationReceipt(id, DRPGBattleActionStage.ImpactAndReactions, step);
        result.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, reason)); return result;
    }
}
