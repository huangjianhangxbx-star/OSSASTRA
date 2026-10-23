#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

internal static class DRPGSettlementSequenceProbe
{
    [UnityEditor.MenuItem("Tools/ANKALUTE/DRPG/Run Settlement Presentation Foundation Probes")]
    public static void RunFromMenu()
    {
        RunAll(); DRPGSettlementProjectionProbe.RunAll();
        Debug.Log("Settlement presentation foundation PASS (16 checks); not Stage9 production visual acceptance.");
    }

    internal static void RunAll()
    {
        ReceiptsStaySerialAfterEarlyCompletion(); EmptySettlementHasOneRenderedHandoff();
        CommandsCarryImmutableCausalOrder(); TimeoutWinsAndStillWaitsForRenderedFrame();
        ForeignReceiptIsRejectedWithoutCancellingItsOwner(); PumpCannotReenterSequence();
        DisposalDuringIssuanceCancelsReturnedLeaf(); OwnershipLossDuringIssuanceStopsTheQueue();
        InvalidClockCannotIssueOrFinishAStatus(); CleanupFailureFaultsWithoutEscapingTick();
    }

    // Break caught: a completed leaf collapses all status outcomes into one frame.
    internal static void ReceiptsStaySerialAfterEarlyCompletion()
    {
        using (var test = new Fixture(true))
        using (var sequence = Start(test))
        {
            Check(test.Port.Receipts.Count == 1, "Settlement did not issue only its first status.");
            test.Port.Complete(0);
            test.Clock.Now = .44; test.Clock.RenderedFrame = 1; Tick(sequence);
            Check(test.Port.Receipts.Count == 1 && !Ready(sequence), "Early receipt skipped the readable window.");
            test.Clock.Now = .45; Tick(sequence);
            Check(test.Port.Receipts.Count == 2 && test.Port.Issued[1] == .45 && !Ready(sequence), "Second status was not issued after the first readable receipt.");
            test.Port.Complete(1); test.Clock.Now = .90; Tick(sequence);
            Check(!Ready(sequence), "The second status completed without a new rendered frame.");
            test.Clock.RenderedFrame = 2; Tick(sequence);
            Check(Ready(sequence) && test.State.BattleRevision == test.Revision && test.Player.CurrentHp == 95 && test.Enemy.CurrentHp == 113 &&
                test.Lease.Arbiter.Owns(test.Lease, test.Lease.Owner), "Presentation resolved rules again, released Root, or did not reach its defeat barrier.");
            Tick(sequence); Check(test.Port.Receipts.Count == 2, "Completed sequence replayed its status queue.");
        }
    }

    // Break caught: empty lanes skip Stage9, wait twice, or bypass the render fence.
    internal static void EmptySettlementHasOneRenderedHandoff()
    {
        using (var test = new Fixture(false))
        using (var sequence = Start(test))
        {
            Check(test.Port.Receipts.Count == 1, "Empty settlement needs one stage-level receipt.");
            test.Port.Complete(0); test.Clock.Now = 0; test.Clock.RenderedFrame = 0; Tick(sequence);
            Check(!Ready(sequence), "Empty settlement skipped the rendered handoff.");
            test.Clock.RenderedFrame = 1; Tick(sequence);
            Check(Ready(sequence) && test.Port.Receipts.Count == 1 && test.State.BattleRevision == test.Revision,
                "Empty settlement waited separately per lane or changed rule state.");
        }
    }

    // Break caught: flattening by side or ordinary status only loses a root's child subtree.
    internal static void CommandsCarryImmutableCausalOrder()
    {
        using (var test = new Fixture(true, true))
        using (var sequence = Start(test))
        {
            Check(test.Ledger.Groups.Count == 2 && test.Ledger.Groups[0].Children.Count == 3 &&
                test.Ledger.Groups[0].Children[0].ReactionId == "child" && test.Ledger.Groups[0].Children[1].ReactionId == "nested" &&
                test.Ledger.Groups[0].Children[2].State == DRPGImmediateReactionState.SkippedDuplicate,
                "Causal fixture needs a child, nested child and duplicate diagnostic.");
            var expected = new object[] { test.Ledger.Steps[0], test.Ledger.Groups[0].Children[0],
                test.Ledger.Groups[0].Children[1], test.Ledger.Steps[1] };
            DRPGSettlementPresentationPlan plan = null;
            for (int i = 0; i < expected.Length; i++)
            {
                Check(test.Port.Commands.Count == i + 1, "Causal queue issued more than one visible item.");
                var command = test.Port.Commands[i]; var currentPlan = command.Plan;
                if (plan == null) plan = currentPlan;
                Check(ReferenceEquals(plan, currentPlan) && ReferenceEquals(plan.Ledger, test.Ledger), "Commands do not share their sealed settlement ledger.");
                var step = command.Step;
                Check(ReferenceEquals(i == 0 || i == 3 ? (object)step.Status : step.Reaction, expected[i]) &&
                    step.Lane == (i == 3 ? DRPGBattleSide.Enemy : DRPGBattleSide.Player),
                    "Visible steps lost root/child order or moved a cross-side child into another lane.");
                Check(command.IssuedAt == test.Clock.Now && command.StepOrdinal == i,
                    "Command did not freeze its issuance boundary.");
                test.Port.Complete(i); test.Clock.Now += .50; test.Clock.RenderedFrame++; Tick(sequence);
            }
            Check(Ready(sequence) && test.Port.Commands.Count == 4 && test.State.BattleRevision == test.Revision,
                "Diagnostic child became visible or causal presentation resolved rules again.");
        }
    }

    // Break caught: late callbacks beat timeout cleanup, or degraded feedback skips the render fence.
    internal static void TimeoutWinsAndStillWaitsForRenderedFrame()
    {
        using (var test = new Fixture(true))
        using (var sequence = Start(test))
        {
            bool accepted = true;
            test.Port.OnTick = () => accepted = test.Port.Receipts[0].TryComplete(
                new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "Late leaf"));
            test.Clock.Now = 2.45; Tick(sequence);
            Check(!accepted && test.Port.Cancelled.Count == 1 && test.Port.Receipts[0].Completion.Result.Kind == PresentationReceiptTerminalKind.Degraded,
                "Late callback beat owned timeout recovery.");
            Check(test.Port.Receipts.Count == 1 && !Ready(sequence), "Recovery skipped the rendered frame.");
            test.Clock.RenderedFrame = 1; Tick(sequence);
            Check(test.Port.Receipts.Count == 2 && sequence.Degraded, "Recovered status did not advance once.");
            sequence.Dispose();
            Check(test.Port.Cancelled.Count == 2 && ReferenceEquals(test.Port.Cancelled[1], test.Port.Receipts[1]) &&
                test.State.BattleRevision == test.Revision && test.Lease.Arbiter.Owns(test.Lease, test.Lease.Owner),
                "Sequence disposal cancelled a completed/foreign leaf, mutated rules, or released Root.");
        }
    }

    internal static void ForeignReceiptIsRejectedWithoutCancellingItsOwner()
    {
        using (var test = new Fixture(false))
        {
            test.Port.Foreign = true;
            using (var sequence = Start(test))
                Check(sequence.Fault.Length != 0 && !Ready(sequence) && test.Port.Cancelled.Count == 0 &&
                    !test.Port.Receipts[0].Completion.IsCompleted, "Foreign receipt was accepted or another owner was cancelled.");
        }
    }

    internal static void PumpCannotReenterSequence()
    {
        using (var test = new Fixture(true))
        using (var sequence = Start(test))
        {
            int entries = 0;
            test.Port.OnTick = () => { entries++; if (entries == 1) Tick(sequence); else throw new InvalidOperationException("Reentrant pump reached a leaf twice."); };
            test.Port.Complete(0); test.Clock.Now = .45; test.Clock.RenderedFrame = 1; Tick(sequence);
            Check(entries == 1 && test.Port.Receipts.Count == 2 && sequence.Fault.Length == 0,
                "A leaf callback reentered the sequence pump.");
        }
    }

    // Break caught: Dispose during PresentSettlement leaks the receipt returned afterwards.
    internal static void DisposalDuringIssuanceCancelsReturnedLeaf()
    {
        using (var test = new Fixture(true))
        using (var sequence = Start(test))
        {
            test.Port.OnPresent = () => sequence.Dispose();
            test.Port.Complete(0); test.Clock.Now = .45; test.Clock.RenderedFrame = 1; sequence.Tick();
            Check(test.Port.Receipts.Count == 2 && test.Port.Cancelled.Count == 1 &&
                test.Port.Receipts[1].Completion.IsCompleted && test.Port.Receipts[1].Completion.Result.Kind == PresentationReceiptTerminalKind.Faulted,
                "A receipt returned after disposal escaped cleanup.");
            test.Clock.Now = 10; test.Clock.RenderedFrame++; sequence.Tick();
            Check(!sequence.ReadyForDefeatFinalization && test.Port.Receipts.Count == 2 && test.Lease.Arbiter.Owns(test.Lease, test.Lease.Owner),
                "Disposed sequence advanced or released Root.");
        }
    }

    internal static void OwnershipLossDuringIssuanceStopsTheQueue()
    {
        using (var test = new Fixture(true))
        using (var sequence = Start(test))
        {
            test.Port.OnPresent = () => test.Lease.Dispose();
            test.Port.Complete(0); test.Clock.Now = .45; test.Clock.RenderedFrame = 1; sequence.Tick();
            Check(sequence.Fault.Length != 0 && test.Port.Cancelled.Count == 1 && test.Port.Receipts[1].Completion.IsCompleted &&
                !sequence.ReadyForDefeatFinalization, "Ownership lost during issuance left a live settlement leaf.");
        }
    }

    internal static void InvalidClockCannotIssueOrFinishAStatus()
    {
        using (var test = new Fixture(false))
        {
            test.Clock.Now = double.NaN;
            using (var sequence = Start(test))
                Check(sequence.Fault.Length != 0 && test.Port.Commands.Count == 0, "Invalid clock was handed to a presentation leaf.");
        }
        foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, -.1d })
        using (var test = new Fixture(false))
        using (var sequence = Start(test))
        {
            test.Port.Complete(0); test.Clock.Now = invalid; test.Clock.RenderedFrame = 1; sequence.Tick();
            Check(sequence.Fault.Length != 0 && !sequence.ReadyForDefeatFinalization, "Invalid time bypassed the readable barrier.");
        }
    }

    internal static void CleanupFailureFaultsWithoutEscapingTick()
    {
        using (var test = new Fixture(true))
        using (var sequence = Start(test))
        {
            test.Port.OnCancel = () => throw new InvalidOperationException("Injected cleanup failure");
            test.Port.OnTick = () => throw new InvalidOperationException("Injected leaf failure");
            sequence.Tick();
            Check(sequence.Fault.Length != 0 && test.Port.Receipts[0].Completion.IsCompleted && !sequence.ReadyForDefeatFinalization &&
                test.Lease.Arbiter.Owns(test.Lease, test.Lease.Owner), "Failed cleanup escaped or released the rule owner.");
        }
    }

    private static DRPGBattleSettlementSequence Start(Fixture test) => new DRPGBattleSettlementSequence(test.Ledger, test.Port, test.Clock, test.Lease);
    private static void Tick(DRPGBattleSettlementSequence sequence) => sequence.Tick();
    private static bool Ready(DRPGBattleSettlementSequence sequence) => sequence.ReadyForDefeatFinalization;
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private sealed class Clock : IDRPGBattlePresentationClock
    {
        public double Now { get; set; }
        public long RenderedFrame { get; set; }
    }
    // Only leaf receipt completion is controlled; the rule ledger and sequence are real.
    private sealed class Port : IStandardBasicAttackPresentationPort, IDRPGBattlePresentationPump
    {
        internal Port(Clock clock) { Clock = clock; }
        public IDRPGBattlePresentationClock Clock { get; }
        internal readonly List<PresentationReceipt> Receipts = new List<PresentationReceipt>();
        internal readonly List<double> Issued = new List<double>();
        internal readonly List<SettlementPresentationCommand> Commands = new List<SettlementPresentationCommand>();
        internal readonly List<PresentationReceipt> Cancelled = new List<PresentationReceipt>();
        internal Action OnTick;
        internal Action OnPresent;
        internal Action OnCancel;
        internal bool Foreign;
        public PresentationReceipt PresentSettlement(in SettlementPresentationCommand command)
        {
            Commands.Add(command); Issued.Add(Clock.Now);
            var id = command.Identity;
            if (Foreign) id = new ActionIdentity(id.BattleGeneration, id.TurnSerial, id.ActionId + 1);
            var receipt = new PresentationReceipt(id, DRPGBattleActionStage.Settlement, command.StepOrdinal);
            Receipts.Add(receipt); OnPresent?.Invoke(); return receipt;
        }
        internal void Complete(int index) => Receipts[index].TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "Controlled leaf completed."));
        public void Tick() { OnTick?.Invoke(); }
        public void CancelReceipt(PresentationReceipt receipt, string reason) { Cancelled.Add(receipt); OnCancel?.Invoke(); }
        public bool TryCaptureFrozenSnapshot(in DRPGBasicAttackPresentationFreezeRequest request,
            out DRPGBasicAttackPresentationFreezeInput snapshot) { snapshot = default; return false; }
        public PresentationReceipt Announce(in AnnouncementCommand c) => throw new InvalidOperationException("Unexpected announcement.");
        public PresentationReceipt EnterConflict(in ConflictCompositionCommand c) => throw new InvalidOperationException("Unexpected conflict.");
        public PresentationReceipt PlayWindup(in BasicAttackWindupCommand c) => throw new InvalidOperationException("Unexpected windup.");
        public PresentationReceipt PresentImpact(in ImpactPresentationCommand c) => throw new InvalidOperationException("Unexpected primary impact.");
        public PresentationReceipt PresentReaction(in ReactionPresentationCommand c) => throw new InvalidOperationException("Unexpected Stage7 reaction.");
        public PresentationReceipt Return(in ReturnPresentationCommand c) => throw new InvalidOperationException("Unexpected return.");
        public PresentationCleanupReceipt EmergencyCleanup(in BattlePresentationCleanupCommand c) => throw new InvalidOperationException("Unexpected broad cleanup.");
    }
    private sealed class Fixture : IDisposable
    {
        private readonly DRPGBattleStateMutationFixture fixture = DRPGBattleStateMutationFixture.Create();
        internal Fixture(bool statuses, bool reactions = false)
        {
            if (statuses) { Apply(Player, "party-poison", -5); Apply(Enemy, "enemy-poison", -7); }
            fixture.Lease.Dispose();
            var id = new ActionIdentity(fixture.BattleGeneration, 1, 21);
            Check(fixture.Arbiter.TryAcquire(BattleTransactionIdentity.ForRootAction(id), out var lease, out var error), error); Lease = lease;
            DRPGImmediateReactionLedgerBuilder.Continuation continuation = null;
            if (reactions)
            {
                var skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
                try
                {
                    Set(skill, "skillId", "sequence-child"); Set(skill, "power", 1f);
                    Set(skill, "effects", new List<DRPGActionEffectDefinition> { DRPGActionEffectDefinition.Damage(DRPGActionEffectTarget.PrimaryTarget, 1f, DRPGDamageTag.Slash) });
                    var spec = DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(Player.RuntimeId, Enemy.RuntimeId, skill), Player);
                    var draft = State.CreateWorkspace(); var resolver = new DRPGActionResolver(); var participants = new[] { Player, Enemy };
                    var primary = resolver.ResolveIntoWorkspace(spec, Player, Enemy, participants, draft, DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f));
                    var closure = new DRPGImmediateReactionLedgerBuilder().Build(id, "sequence-primary", primary, draft, participants,
                        new[] { new Source(new ParticipantId(Player.RuntimeId), spec) }, resolver);
                    Check(closure.Succeeded && State.TryCommitJournal(lease, closure.Workspace).Succeeded, "Causal sequence fixture failed.");
                    continuation = closure.Continuation;
                }
                finally { UnityEngine.Object.DestroyImmediate(skill); }
            }
            var homes = new Dictionary<ParticipantId, int> { { new ParticipantId(Player.RuntimeId), 0 }, { new ParticipantId(Enemy.RuntimeId), 0 } };
            Check(State.TryCommitSettlement(lease, new ParticipantId(Player.RuntimeId), homes, out var ledger, out error, continuation), error);
            Ledger = ledger; Revision = State.BattleRevision; Port = new Port(Clock);
        }
        internal BattleParticipantRuntime Player => fixture.Player;
        internal BattleParticipantRuntime Enemy => fixture.Enemy;
        internal DRPGBattleSessionState State => fixture.State;
        internal BattleRuleMutationLease Lease { get; }
        internal DRPGBattleSettlementLedger Ledger { get; }
        internal long Revision { get; }
        internal Clock Clock { get; } = new Clock();
        internal Port Port { get; }
        private void Apply(BattleParticipantRuntime owner, string id, float hp)
        {
            var definition = ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>();
            try
            {
                Set(definition, "statusId", id); Set(definition, "triggerTiming", DRPGStatusTriggerTiming.AfterAction);
                Set(definition, "durationTurns", 2); Set(definition, "turnStartHpDelta", hp);
                var draft = State.CreateWorkspace();
                draft.For(new ParticipantId(owner.RuntimeId)).ApplyStatus(DRPGResolvedStatusSpec.From(definition), 1, 0, new ParticipantId(owner.RuntimeId));
                Check(State.TryCommitJournal(fixture.Lease, draft).Succeeded, "Status fixture failed.");
            }
            finally { UnityEngine.Object.DestroyImmediate(definition); }
        }
        public void Dispose() { Lease.Dispose(); fixture.Dispose(); }
    }
    private sealed class Source : IDRPGImmediateReactionSource
    {
        private readonly DRPGResolvedActionSpec spec;
        internal Source(ParticipantId participant, DRPGResolvedActionSpec spec) { Participant = participant; this.spec = spec; }
        public string SourceId => "sequence-source";
        public ParticipantId Participant { get; }
        public int HomeSlot => 0;
        public bool IsPassive => false;
        public void Collect(DRPGImmediateReactionContext context, ICollection<DRPGImmediateReactionRequest> requests)
        {
            bool root = context.Settlement != null && context.Settlement.Owner == Participant;
            if (!root && context.Depth != 1) return;
            var request = new DRPGImmediateReactionRequest(context.Identity, context.EventId, root ? "child" : "nested",
                DRPGImmediateReactionCategory.OtherImmediate, 0, spec, DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f));
            requests.Add(request); if (root) requests.Add(request);
        }
    }
}
#endif
