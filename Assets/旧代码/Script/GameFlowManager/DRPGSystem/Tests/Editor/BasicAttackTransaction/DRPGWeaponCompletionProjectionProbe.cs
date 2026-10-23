#if UNITY_EDITOR
using System;
using UnityEngine;

internal static class DRPGWeaponCompletionProjectionProbe
{
    internal static void WeaponClampWaitsForTheLastReactionOutcome()
    {
        using (var f = new Fixture(reaction: true))
        {
            f.Commit();
            Check(f.Ledger.Reactions.Count == 1 && f.Ledger.Reactions[0].Batch.Hits[0].HpBefore == 300 &&
                f.Ledger.Reactions[0].Batch.Hits[0].HpAfter == 300 &&
                f.Ledger.Reactions[0].Batch.Hits[0].PostureBefore == 100 &&
                f.Ledger.Reactions[0].Batch.Hits[0].PostureAfter == 80,
                "Reaction did not precede the real weapon clamp.");
            using (var sequence = new DRPGBasicAttackImpactSequence(f.Ledger, f.Adapter, f.Clock, f.Rules.Lease))
            {
                Check(f.Projection.GetPresented(9901).Hp == 300 && f.Projection.GetPresented(9901).MaxHp == 300,
                    "Primary prematurely projected the end-of-chain weapon clamp.");
                f.Clock.Now += .46; f.Clock.RenderedFrame++; sequence.Tick();
                Check(f.Projection.GetPresented(9901).Hp == 300, "Reaction preparation exposed its final resources.");
                f.Clock.Now += .13; f.Clock.RenderedFrame++; sequence.Tick();
                var shown = f.Projection.GetPresented(9901);
                Check(sequence.Fault.Length == 0 && shown.Hp == 150 && shown.MaxHp == 150 && shown.Posture == 80 &&
                    shown.MaxPosture == 100, "Last reaction did not reconcile the frozen weapon completion.");
                f.Clock.Now += .46; f.Clock.RenderedFrame++; sequence.Tick();
                Check(sequence.ReadyForReturn && f.Rules.State.BattleRevision == 1, "Weapon projection did not close the existing Stage7 sequence.");
            }
        }
    }

    // Break caught: a real weapon break commits smaller capacity, but the
    // Stage7 projection leaves the old values and makes the next ledger stale.
    internal static void WeaponCompletionProjectsItsCommittedClampsBeforeReturn()
    {
        using (var f = new Fixture())
        {
            f.Commit();
            Check(f.Rules.Player.CurrentHp == 150 && f.Rules.Player.CurrentPosture == 100 &&
                f.Projection.GetPresented(9901).Hp == 300, "Fixture did not freeze pre-break presentation.");
            var receipt = f.Adapter.PresentImpact(new ImpactPresentationCommand(f.Ledger, f.Clock.Now));
            var shown = f.Projection.GetPresented(9901);
            Check(shown.Hp == 150 && shown.MaxHp == 150 && shown.Posture == 100 && shown.MaxPosture == 100,
                "Committed weapon clamps/capacities were omitted from the final Stage7 outcome.");
            f.Clock.Now += .46; f.Clock.RenderedFrame++; f.Adapter.Tick();
            Check(receipt.Completion.IsCompleted && receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed &&
                f.Rules.State.BattleRevision == 1 && f.Rules.Lease.IsActive && f.Visuals.ActiveNumbers().Length == 0,
                "Projecting weapon completion re-resolved rules or left the primary feedback open.");
        }
    }

    // Break caught: a late weapon before-state mismatch writes primary damage
    // first, then faults and leaves an unexplained partial visual beat.
    internal static void StaleWeaponCompletionRejectsTheEntireOutcomeBeforeWriting()
    {
        using (var f = new Fixture())
        {
            f.Commit(); var actor = f.Projection.GetPresented(9901);
            Check(f.Projection.Apply(new BattleProjectionCursor(f.Projection.Owner, 0, actor, actor.WithHp(299, 300))).Accepted,
                "Could not arrange stale weapon HP.");
            long ordinal = f.Projection.NextCursorOrdinal;
            var receipt = f.Adapter.PresentImpact(new ImpactPresentationCommand(f.Ledger, f.Clock.Now));
            Check(receipt.Completion.IsCompleted && receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Faulted &&
                f.Projection.GetPresented(9902).Hp == 120 && f.Projection.NextCursorOrdinal == ordinal && f.Visuals.ActiveNumbers().Length == 0,
                "Stale weapon clamp allowed a partial primary projection.");
        }
    }

    internal sealed class Fixture : IDisposable
    {
        internal readonly DRPGWeaponCompletionJournalProbe.Fixture Rules;
        internal readonly GameObject Root;
        internal readonly DRPGBasicAttackPreludeProbe.ProbeClock Clock = new DRPGBasicAttackPreludeProbe.ProbeClock();
        internal readonly DRPGBasicAttackHitOutcomeProbe.Visuals Visuals;
        internal readonly DRPGBattleUGUISurface Surface;
        internal readonly DRPGBattleTransactionPresentationAdapter Adapter;
        internal readonly DRPGBattleActionProjectionLease Projection;
        internal readonly DRPGPreparedBasicAttackRuleAction Prepared;
        internal DRPGBattleActionResolutionLedger Ledger;
        internal Fixture(bool reaction = false)
        {
            Rules = new DRPGWeaponCompletionJournalProbe.Fixture(boosted: true, productionSession: true);
            try
            {
                Root = new GameObject("WeaponProjectionProbe");
                var pool = Root.AddComponent<BattleWorldActorPool>();
                var roster = Root.AddComponent<BattleWorldRosterPresenter>(); roster.Configure(pool, null, null);
                Surface = Root.AddComponent<DRPGBattleUGUISurface>(); Surface.ConfigureWorldPresentation(null, roster, null);
                Surface.Bind(Rules.Session); Surface.RenderInitial(DRPGBattlePresentationSnapshot.Capture(Rules.Session));
                var spec = DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(9901, 9902, Rules.Player.Build.BasicAttack), Rules.Player);
                var counter = DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(9902, 9901, Rules.Enemy.Build.BasicAttack), Rules.Enemy);
                Check(Rules.Session.StandardBasicAttackRules.TryPrepare(Rules.Frozen, spec, Rules.Lease, out Prepared, out var error,
                    reaction ? new[] { new Counter(counter) } : null), error);
                Visuals = new DRPGBasicAttackHitOutcomeProbe.Visuals(Root, Clock);
                Adapter = new DRPGBattleTransactionPresentationAdapter(Clock, Visuals.Announcement,
                    new BattleAnimationPlaybackRouter(Clock, (_, __) => null),
                    _ => { var h = new BattlePresentationHandle(); h.TrySettle(BattlePresentationOutcome.Completed); return h; },
                    projectionArbiter: Surface.ProjectionArbiter, impactFeedback: Visuals.Feedback, battleCamera: Visuals.Camera);
                Projection = (DRPGBattleActionProjectionLease)Adapter.OpenProjection(Rules.Frozen);
                var conflict = Adapter.EnterConflict(new ConflictCompositionCommand(Rules.Frozen, Clock.Now));
                Clock.Now += .13; Clock.RenderedFrame++; Adapter.Tick();
                Check(conflict.Completion.IsCompleted && conflict.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed,
                    "Weapon projection conflict fixture did not complete.");
            }
            catch { Dispose(); throw; }
        }
        internal void Commit()
        {
            var committed = Prepared.ResolveAndCommit(DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f));
            Check(committed.Status == DRPGPrimaryImpactCommitStatus.Committed, committed.Error); Ledger = committed.Receipt.Ledger;
        }
        public void Dispose()
        { Projection?.Dispose(); Prepared?.Dispose(); Surface?.Unbind(); if (Root != null) UnityEngine.Object.DestroyImmediate(Root); Rules?.Dispose(); }
    }
    private sealed class Counter : IDRPGImmediateReactionSource
    {
        private readonly DRPGResolvedActionSpec spec;
        internal Counter(DRPGResolvedActionSpec spec) { this.spec = spec; }
        public string SourceId => "weapon-clamp-counter";
        public ParticipantId Participant => new ParticipantId(9902);
        public int HomeSlot => 0;
        public bool IsPassive => false;
        public void Collect(DRPGImmediateReactionContext context, System.Collections.Generic.ICollection<DRPGImmediateReactionRequest> requests)
        {
            if (context.Depth == 0) requests.Add(new DRPGImmediateReactionRequest(context.Identity, context.EventId, SourceId,
                DRPGImmediateReactionCategory.Counterattack, 0, spec, DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f)));
        }
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
#endif
