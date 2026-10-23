#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

internal static class DRPGBasicAttackSemanticOutcomeProbe
{
    internal static void RunAll()
    {
        PrimaryAndReactionProjectTheirOwnStatusAndPosture();
        StalePostureRejectsOutcomeBeforeAnyProjectionWrite();
    }

    // Break caught: HP advances but status/posture stay at the lock baseline,
    // or the primary leaks the reaction's already-committed final state.
    internal static void PrimaryAndReactionProjectTheirOwnStatusAndPosture()
    {
        foreach (bool enemy in new[] { false, true })
        using (var f = new Fixture(enemy))
        {
            f.Commit();
            Check(f.Target.CurrentPosture == 104 && f.Target.Statuses.Snapshot().Count == 1,
                "The real rules did not produce the ordered fixture outcomes.");
            using (var sequence = new DRPGBasicAttackImpactSequence(f.Ledger, f.Adapter, f.Clock, f.Source.Lease))
            {
                var target = f.Projection.GetPresented(f.Target.RuntimeId);
                var actor = f.Projection.GetPresented(f.Actor.RuntimeId);
                Check(target.Posture == 99 && target.Statuses.Count == 0 && actor.Posture == 90,
                    "Primary did not project its own posture/status and frozen cost; or leaked a future reaction.");
                Check(f.Visuals.ActiveNumbers().Length == 1, "Primary lost the single feedback owner.");
                f.Step(sequence, .46);
                Check(f.Projection.GetPresented(f.Target.RuntimeId).Posture == 99 &&
                    f.Projection.GetPresented(f.Target.RuntimeId).Statuses.Count == 0,
                    "Reaction preparation revealed its result early.");
                f.Step(sequence, .13);
                target = f.Projection.GetPresented(f.Target.RuntimeId);
                Check(target.Posture == 104 && target.Statuses.Count == 1 &&
                    target.Statuses[0].SourceParticipantId.Value == f.Target.RuntimeId && f.Visuals.ActiveNumbers().Length == 1,
                    "Non-hit reaction did not advance its own semantic cursor.");
                f.Step(sequence, .46);
                Check(sequence.ReadyForReturn && f.Source.Events == 1 && f.Source.Lease.IsActive &&
                    DRPGActionStatusChange.SameSnapshots(target.Statuses, f.Target.Statuses.Snapshot()),
                    "Stage7 did not leave the exact committed status baseline for Stage9.");
            }
        }
    }

    // Break caught: stale posture with correct HP is accepted; or cost/HP is
    // written before a later field in the same outcome is rejected.
    internal static void StalePostureRejectsOutcomeBeforeAnyProjectionWrite()
    {
        using (var f = new Fixture(false))
        {
            f.Commit();
            var before = f.Projection.GetPresented(f.Target.RuntimeId);
            Check(f.Projection.Apply(new BattleProjectionCursor(f.Projection.Owner, 0, before,
                before.WithSettlement(before.Hp, 99, before.Statuses))).Accepted, "Failed to arrange a stale semantic cursor.");
            long ordinal = f.Projection.NextCursorOrdinal;
            var receipt = f.Adapter.PresentImpact(new ImpactPresentationCommand(f.Ledger, f.Clock.Now));
            Check(receipt.Completion.IsCompleted && receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Faulted &&
                f.Projection.GetPresented(f.Target.RuntimeId).Hp == before.Hp &&
                f.Projection.GetPresented(f.Actor.RuntimeId).Posture == 100 &&
                f.Projection.NextCursorOrdinal == ordinal && f.Visuals.ActiveNumbers().Length == 0,
                "A stale semantic before-state was accepted or partially projected.");
        }
    }

    internal sealed class Fixture : IDisposable
    {
        internal readonly DRPGBasicAttackImpactSequenceProbe.Fixture Source;
        internal DRPGBasicAttackPreludeProbe.ProbeClock Clock => Source.Clock;
        internal readonly BattleParticipantRuntime Actor, Target;
        internal readonly DRPGBasicAttackHitOutcomeProbe.Visuals Visuals;
        internal readonly DRPGBattleTransactionPresentationAdapter Adapter;
        internal readonly DRPGBattleActionProjectionLease Projection;
        internal readonly DRPGPreparedBasicAttackRuleAction Prepared;
        internal DRPGBattleActionResolutionLedger Ledger;
        private readonly DRPGStatusDefinitionSO status;
        private readonly DRPGSkillSO reactionSkill;
        internal Fixture(bool enemy)
        {
            Source = new DRPGBasicAttackImpactSequenceProbe.Fixture(false, enemy, false, prepareRules: false);
            try
            {
            Actor = enemy ? Source.Domain.Enemy : Source.Domain.Player;
            Target = enemy ? Source.Domain.Player : Source.Domain.Enemy;
            Check(Actor.CurrentPosture == 100 && Target.CurrentPosture == 100, "Unexpected authored posture baseline.");
            status = ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>();
            DRPGBasicAttackPreludeProbe.Set(status, "statusId", "semantic-outcome");
            DRPGBasicAttackPreludeProbe.Set(status, "durationTurns", 2);
            var skill = Source.Domain.Player.Build.BasicAttack;
            DRPGBasicAttackPreludeProbe.Set(skill, "postureCost", 10f);
            DRPGBasicAttackPreludeProbe.Set(skill, "effects", new List<DRPGActionEffectDefinition> {
                DRPGActionEffectDefinition.Damage(DRPGActionEffectTarget.PrimaryTarget, 1f) });
            reactionSkill = ScriptableObject.CreateInstance<DRPGSkillSO>();
            DRPGBasicAttackPreludeProbe.Set(reactionSkill, "effects", new List<DRPGActionEffectDefinition> {
                DRPGActionEffectDefinition.PostureRestore(DRPGActionEffectTarget.PrimaryTarget, 5f),
                DRPGActionEffectDefinition.ApplyStatus(DRPGActionEffectTarget.PrimaryTarget, status) });
            var turn = Source.Domain.Session.CurrentActorTurnToken;
            var frozen = new DRPGBasicAttackFrozenSnapshot(turn,
                new TargetSelectionToken(turn.BattleGeneration, turn.TurnSerial, 1, turn.Actor),
                new ParticipantId(Target.RuntimeId), skill, Actor.Side,
                new ActionTracePosition(0, 0, 0), new ActionTracePosition(enemy ? .5f : -.5f, 0, 0)).WithIdentity(Source.Frozen.Identity);
            var primary = DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(Actor.RuntimeId, Target.RuntimeId, skill), Actor);
            var reaction = DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(Target.RuntimeId, Target.RuntimeId, reactionSkill), Target);
            Check(Source.Domain.Session.StandardBasicAttackRules.TryPrepare(frozen, primary, Source.Lease,
                out Prepared, out var error, new[] { new ReactionSource(Target.RuntimeId, reaction) }), error);
            Visuals = new DRPGBasicAttackHitOutcomeProbe.Visuals(Source.Domain.Root, Clock);
            Adapter = Visuals.CreateAdapter(Source);
            Projection = (DRPGBattleActionProjectionLease)Adapter.OpenProjection(frozen);
            Adapter.EnterConflict(new ConflictCompositionCommand(frozen, Clock.Now));
            Clock.Now = .13; Clock.RenderedFrame++; Adapter.Tick();
            }
            catch { Dispose(); throw; }
        }
        internal void Commit()
        {
            var committed = Prepared.ResolveAndCommit(DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f, 7f), DRPGHitOutcome.Hit);
            Check(committed.Status == DRPGPrimaryImpactCommitStatus.Committed, committed.Error);
            Ledger = committed.Receipt.Ledger;
            Source.Domain.Bridge.FlushQueuedSignals();
        }
        internal void Step(DRPGBasicAttackImpactSequence sequence, double seconds)
        { Clock.Now += seconds; Clock.RenderedFrame++; sequence.Tick(); Check(sequence.Fault.Length == 0, sequence.Fault); }
        public void Dispose()
        { Projection?.Dispose(); Prepared?.Dispose(); Source.Dispose(); if (reactionSkill != null) UnityEngine.Object.DestroyImmediate(reactionSkill); if (status != null) UnityEngine.Object.DestroyImmediate(status); }
    }
    private sealed class ReactionSource : IDRPGImmediateReactionSource
    {
        private readonly DRPGResolvedActionSpec spec;
        internal ReactionSource(int id, DRPGResolvedActionSpec spec) { Participant = new ParticipantId(id); this.spec = spec; }
        public ParticipantId Participant { get; }
        public string SourceId => "semantic-self-reaction";
        public bool IsPassive => false;
        public int HomeSlot => 0;
        public void Collect(DRPGImmediateReactionContext context, ICollection<DRPGImmediateReactionRequest> requests)
        {
            if (context.Depth != 0) return;
            requests.Add(new DRPGImmediateReactionRequest(context.Identity, context.EventId, SourceId,
                DRPGImmediateReactionCategory.Counterattack, 0, spec, DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f)));
        }
    }
    private static void Check(bool value, string detail) { if (!value) throw new InvalidOperationException(detail); }
}
#endif
