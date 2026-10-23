#if UNITY_EDITOR
using System;
using System.Collections.Generic;

public static class DRPGBasicAttackImpactSequenceProbe
{
    public static void ReactionPresentationReceiptsAreSerial()
    {
        using (var f = new Fixture())
        using (var sequence = new DRPGBasicAttackImpactSequence(f.Ledger, f.Port, f.Clock, f.Lease))
        {
            Check(f.Port.History.Count == 1 && !sequence.ReadyForReturn,
                "Stage7 did not issue only the primary ledger receipt on entry.");
            f.Port.CompleteLast();
            f.Clock.Now = .44d; f.Clock.RenderedFrame = 1; sequence.Tick();
            Check(f.Port.History.Count == 1, "Primary feedback skipped its readable minimum.");
            f.Clock.Now = .46d; f.Clock.RenderedFrame++; sequence.Tick();
            Check(f.Port.History.Count == 2 && f.Port.History[1].StepOrdinal == 1,
                "First reaction did not follow the completed primary beat.");
            sequence.Tick();
            Check(f.Port.History.Count == 2, "A pending reaction was bypassed.");
            f.Port.CompleteLast();
            f.Clock.Now = .92d; f.Clock.RenderedFrame++; sequence.Tick();
            Check(f.Port.History.Count == 3 && f.Port.History[2].StepOrdinal == 2 && !sequence.ReadyForReturn,
                "Second reaction was omitted or return began too early.");
            f.Port.CompleteLast();
            f.Clock.Now = 1.38d; f.Clock.RenderedFrame++; sequence.Tick();
            Check(sequence.ReadyForReturn && f.Events == 1 &&
                f.Domain.Player.CurrentHp == f.PlayerHp && f.Domain.Enemy.CurrentHp == f.EnemyHp &&
                ReferenceEquals(f.Domain.Session.CurrentActor, f.Domain.Player),
                "Presentation re-ran mutation or advanced the actor before return.");
            for (int index = 0; index < f.Port.Ledgers.Count; index++)
                Check(ReferenceEquals(f.Port.Ledgers[index], f.Ledger), "Presentation received a rebuilt ledger.");
        }
    }

    public static void ReactionTimeoutContinuesWithoutRemutation()
    {
        using (var f = new Fixture())
        using (var sequence = new DRPGBasicAttackImpactSequence(f.Ledger, f.Port, f.Clock, f.Lease))
        {
            Check(f.Port.History.Count == 1, "Primary receipt missing.");
            f.Clock.Now = 2.5d; f.Clock.RenderedFrame = 1; sequence.Tick();
            Check(sequence.Degraded && f.Port.Cancelled.Count == 1 && f.Port.History.Count == 2 && f.Events == 1,
                "Timed-out beat did not clean only its receipt and continue to the next immutable reaction.");
            Check(f.Port.Cancelled[0] == f.Port.History[0] &&
                f.Domain.Player.CurrentHp == f.PlayerHp && f.Domain.Enemy.CurrentHp == f.EnemyHp && f.Lease.IsActive,
                "Feedback recovery cancelled another beat, mutated rules, or released the root lease.");
        }
    }

    public static void ImpactQueueRejectsOwnershipLostDuringPump()
    {
        using (var f = new Fixture())
        using (var sequence = new DRPGBasicAttackImpactSequence(f.Ledger, f.Port, f.Clock, f.Lease))
        {
            f.Port.CompleteLast();
            f.Clock.Now = .46d; f.Clock.RenderedFrame = 1;
            f.Port.OnTick = () => f.Lease.Dispose();
            sequence.Tick();
            Check(sequence.Fault.Length != 0 && f.Port.History.Count == 1 && !sequence.ReadyForReturn,
                "A presentation callback released ownership but still issued the next reaction.");
        }
    }

    public static void ReactionDiagnosticsDoNotIssueReceipts()
    {
        using (var f = new Fixture(diagnosticDuplicate: true))
        using (var sequence = new DRPGBasicAttackImpactSequence(f.Ledger, f.Port, f.Clock, f.Lease))
        {
            Check(f.Ledger.Reactions.Count > 2, "Fixture did not produce a real skipped diagnostic.");
            var expected = new List<int> { 0 };
            for (int index = 0; index < f.Ledger.Reactions.Count; index++)
                if (f.Ledger.Reactions[index].RequiresPresentation) expected.Add(index + 1);
            for (int index = 0; index < expected.Count; index++)
            {
                Check(f.Port.History.Count == index + 1 && f.Port.History[index].StepOrdinal == expected[index],
                    "A diagnostic created a receipt or renumbered immutable ledger steps.");
                f.Port.CompleteLast(); f.Clock.Now += .46d; f.Clock.RenderedFrame++; sequence.Tick();
            }
            Check(sequence.ReadyForReturn && f.Port.History.Count == 3 && f.Events == 1,
                "Skipped diagnostics added visible beats or blocked return.");
        }
    }

    public static void ImpactQueueRejectsForeignReceiptsWithoutCancellingThem()
    {
        using (var f = new Fixture())
        {
            f.Port.StepOffset = 1;
            using (var sequence = new DRPGBasicAttackImpactSequence(f.Ledger, f.Port, f.Clock, f.Lease))
                Check(sequence.Fault.Length != 0 && f.Port.Cancelled.Count == 0 &&
                    !f.Port.History[0].Completion.IsCompleted && f.Lease.IsActive,
                    "A mismatched receipt was accepted, cancelled or released the root lease.");
        }
    }

    public static void ImpactQueueRequiresRenderedFrameAndStopsOnFault()
    {
        using (var f = new Fixture())
        using (var sequence = new DRPGBasicAttackImpactSequence(f.Ledger, f.Port, f.Clock, f.Lease))
        {
            f.Port.CompleteLast(); f.Clock.Now = .46d; sequence.Tick();
            Check(f.Port.History.Count == 1, "Elapsed wall time substituted for a rendered frame.");
            f.Clock.RenderedFrame = 1; sequence.Tick();
            f.Port.History[1].TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, "Cue failed"));
            sequence.Tick();
            Check(sequence.Fault == "Cue failed" && f.Port.History.Count == 2 && !sequence.ReadyForReturn && f.Lease.IsActive,
                "Faulted presentation advanced or released root ownership.");
        }
    }

    internal sealed class Fixture : IDisposable
    {
        internal readonly DRPGBasicAttackProjectionProbe.Fixture Domain;
        internal readonly DRPGBasicAttackPreludeProbe.ProbeClock Clock = new DRPGBasicAttackPreludeProbe.ProbeClock();
        internal readonly RecordingPort Port;
        internal readonly BattleRuleMutationLease Lease;
        internal readonly DRPGPreparedBasicAttackRuleAction Prepared;
        internal DRPGBattleActionResolutionLedger Ledger { get; private set; }
        internal float PlayerHp { get; private set; }
        internal float EnemyHp { get; private set; }
        internal readonly DRPGBasicAttackFrozenSnapshot Frozen;
        private readonly bool withReactions;
        private readonly UnityEngine.ScriptableObject healingSkill;
        internal int Events;
        internal Fixture(bool commitImmediately = true, bool enemyFirst = false, bool withReactions = true,
            bool healingReaction = false, bool diagnosticDuplicate = false, bool lethal = false,
            bool fullBattlefield = false,
            Action<DRPGBasicAttackProjectionProbe.Fixture, BattleRuleMutationLease> configureRules = null,
            bool thirdPartyReactions = false, bool prepareRules = true,
            Action<DRPGBattleSession, BattleParticipantRuntime, BattleParticipantRuntime> beforeStart = null)
        {
            Domain = new DRPGBasicAttackProjectionProbe.Fixture(lethal, fullBattlefield, enemyFirst, beforeStart);
            this.withReactions = withReactions;
            Port = new RecordingPort(Clock);
            Check(Domain.Session.TransactionArbiter.TryAcquire(Domain.Owner, out Lease, out string error), error);
            configureRules?.Invoke(Domain, Lease);
            Domain.Session.ActionLedgerCommitted += _ => Events++;
            var turn = Domain.Session.CurrentActorTurnToken;
            var skill = Domain.Player.Build.BasicAttack;
            typeof(DRPGSkillSO).GetField("effects", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(skill, new List<DRPGActionEffectDefinition> {
                    DRPGActionEffectDefinition.Damage(DRPGActionEffectTarget.PrimaryTarget, 1f) });
            var actor = enemyFirst ? Domain.Enemy : Domain.Player;
            var target = enemyFirst ? Domain.Player : Domain.Enemy;
            Frozen = new DRPGBasicAttackFrozenSnapshot(turn,
                new TargetSelectionToken(turn.BattleGeneration, turn.TurnSerial, 1, turn.Actor),
                new ParticipantId(target.RuntimeId), skill, enemyFirst ? DRPGBattleSide.Enemy : DRPGBattleSide.Player,
                new ActionTracePosition(0, 0, 0), new ActionTracePosition(enemyFirst ? .5f : -.5f, 0, 0)).WithIdentity(Domain.Owner.Action);
            var primary = DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(actor.RuntimeId, target.RuntimeId, skill), actor);
            var counter = DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(target.RuntimeId, actor.RuntimeId, skill), target);
            int counterSource = target.RuntimeId, pursuitSource = actor.RuntimeId;
            var pursuit = primary;
            if (thirdPartyReactions)
            {
                counterSource = enemyFirst ? 7111 : 7121;
                pursuitSource = enemyFirst ? 7121 : 7111;
                BattleParticipantRuntime counterActor = null, pursuitActor = null;
                foreach (var participant in Domain.Session.Participants)
                {
                    if (participant.RuntimeId == counterSource) counterActor = participant;
                    if (participant.RuntimeId == pursuitSource) pursuitActor = participant;
                }
                Check(counterActor != null && pursuitActor != null, "Third-party fixture requires a full field.");
                counter = DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(counterSource, actor.RuntimeId, skill), counterActor);
                pursuit = DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(pursuitSource, target.RuntimeId, skill), pursuitActor);
            }
            if (healingReaction)
            {
                var healing = UnityEngine.ScriptableObject.CreateInstance<DRPGSkillSO>(); healingSkill = healing;
                typeof(DRPGSkillSO).GetField("effects", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(healing, new List<DRPGActionEffectDefinition> { DRPGActionEffectDefinition.Heal(DRPGActionEffectTarget.PrimaryTarget, 1f) });
                counter = DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(target.RuntimeId, target.RuntimeId, healing), target);
            }
            if (!prepareRules) return;
            Check(Domain.Session.StandardBasicAttackRules.TryPrepare(Frozen, primary, Lease,
                out Prepared, out error, withReactions ? new IDRPGImmediateReactionSource[] {
                    new Source(counterSource, "counter", DRPGImmediateReactionCategory.Counterattack, counter, diagnosticDuplicate),
                    new Source(pursuitSource, "pursuit", DRPGImmediateReactionCategory.Pursuit, pursuit) } : null), error);
            if (commitImmediately) Commit();
        }
        internal void Commit(DRPGHitOutcome outcome = DRPGHitOutcome.Hit)
        {
            var result = Prepared.ResolveAndCommit(DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f), outcome);
            Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed,
                "Fixture did not commit a real action ledger: " + result.Error);
            Ledger = result.Receipt.Ledger;
            int presentable = 0;
            foreach (var reaction in Ledger.Reactions) if (reaction.RequiresPresentation) presentable++;
            Check(!withReactions || presentable == 2,
                "Fixture requires two real presentable reactions. presentable=" + presentable +
                "; entries=" + Ledger.Reactions.Count +
                (Ledger.Reactions.Count > 0 ? "; first=" + Ledger.Reactions[0].State + "/" +
                    Ledger.Reactions[0].Disposition + "/present=" + Ledger.Reactions[0].RequiresPresentation +
                    "/batch=" + (Ledger.Reactions[0].Batch != null) : string.Empty) +
                (Ledger.Reactions.Count > 1 ? "; second=" + Ledger.Reactions[1].State + "/" +
                    Ledger.Reactions[1].Disposition + "/present=" + Ledger.Reactions[1].RequiresPresentation +
                    "/batch=" + (Ledger.Reactions[1].Batch != null) : string.Empty));
            PlayerHp = Domain.Player.CurrentHp; EnemyHp = Domain.Enemy.CurrentHp;
        }
        public void Dispose() { Prepared?.Dispose(); Lease?.Dispose(); Domain.Dispose(); if (healingSkill != null) UnityEngine.Object.DestroyImmediate(healingSkill); }
    }

    private sealed class Source : IDRPGImmediateReactionSource
    {
        private readonly DRPGImmediateReactionCategory category;
        private readonly DRPGResolvedActionSpec spec;
        private readonly bool duplicate;
        internal Source(int runtimeId, string id, DRPGImmediateReactionCategory category, DRPGResolvedActionSpec spec, bool duplicate = false)
        { Participant = new ParticipantId(runtimeId); SourceId = id; this.category = category; this.spec = spec; this.duplicate = duplicate; }
        public string SourceId { get; }
        public ParticipantId Participant { get; }
        public int HomeSlot => 0;
        public bool IsPassive => false;
        public void Collect(DRPGImmediateReactionContext context, ICollection<DRPGImmediateReactionRequest> requests)
        {
            if (context.Depth != 0) return;
            requests.Add(new DRPGImmediateReactionRequest(context.Identity, context.EventId, SourceId,
                category, 0, spec, DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f)));
            if (duplicate) requests.Add(new DRPGImmediateReactionRequest(context.Identity, context.EventId, SourceId,
                category, 0, spec, DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f)));
        }
    }

    internal sealed class RecordingPort : IStandardBasicAttackPresentationPort, IDRPGBattlePresentationPump
    {
        internal RecordingPort(IDRPGBattlePresentationClock clock) { Clock = clock; }
        internal readonly List<PresentationReceipt> History = new List<PresentationReceipt>();
        internal readonly List<DRPGBattleActionResolutionLedger> Ledgers = new List<DRPGBattleActionResolutionLedger>();
        internal readonly List<PresentationReceipt> Cancelled = new List<PresentationReceipt>();
        internal Action OnTick;
        internal int StepOffset;
        public IDRPGBattlePresentationClock Clock { get; }
        private PresentationReceipt Issue(ActionIdentity identity, int step, DRPGBattleActionResolutionLedger ledger)
        { var receipt = new PresentationReceipt(identity, DRPGBattleActionStage.ImpactAndReactions, step + StepOffset); History.Add(receipt); Ledgers.Add(ledger); return receipt; }
        public PresentationReceipt PresentImpact(in ImpactPresentationCommand command) => Issue(command.Identity, 0, command.Ledger);
        public PresentationReceipt PresentReaction(in ReactionPresentationCommand command) => Issue(command.Identity, command.StepOrdinal, command.Ledger);
        internal void CompleteLast() => History[History.Count - 1].TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "Rendered"));
        public void Tick() { OnTick?.Invoke(); }
        public void CancelReceipt(PresentationReceipt receipt, string reason) { Cancelled.Add(receipt); }
        public bool TryCaptureFrozenSnapshot(in DRPGBasicAttackPresentationFreezeRequest request, out DRPGBasicAttackPresentationFreezeInput snapshot) { snapshot = default; return false; }
        public PresentationReceipt Announce(in AnnouncementCommand command) => throw new InvalidOperationException("Unexpected announcement.");
        public PresentationReceipt EnterConflict(in ConflictCompositionCommand command) => throw new InvalidOperationException("Unexpected composition.");
        public PresentationReceipt PlayWindup(in BasicAttackWindupCommand command) => throw new InvalidOperationException("Unexpected windup.");
        public PresentationReceipt Return(in ReturnPresentationCommand command) => throw new InvalidOperationException("Stage7 cannot start return itself.");
        public PresentationReceipt PresentSettlement(in SettlementPresentationCommand command) => throw new InvalidOperationException("Stage7 cannot settle a turn.");
        public PresentationCleanupReceipt EmergencyCleanup(in BattlePresentationCleanupCommand command) => throw new InvalidOperationException("Unexpected whole-battle cleanup.");
    }
    private static void Check(bool value, string message) => DRPGBasicAttackProjectionProbe.Check(value, message);
}
#endif
