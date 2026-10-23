#if UNITY_EDITOR
using System;
using System.Reflection;

internal static class DRPGBasicAttackRootSettlementProbe
{
    // Break caught: distinct real Roots emit the same Stage9 phase EventId.
    internal static void SettlementTracePhaseIdsAreUniqueAcrossRoots()
    {
        var phaseIds = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        foreach (bool enemy in new[] { false, true })
        using (var f = new DRPGBasicAttackRootPipelineProbe.Fixture(enemy))
        {
            f.Start(); f.ReachHit();
            for (int i = 0; i < 100 && SettlementLedger(f.Root) == null; i++) f.Step(.03);
            Check(SettlementLedger(f.Root) != null, "Root did not enter Settlement.");
            int count = 0;
            foreach (var entry in f.StartResult.Receipt.SnapshotTrace())
                if (entry.EventKind == ActionTraceEventKind.PhaseEntered && entry.Stage == DRPGBattleActionStage.Settlement)
                { count++; Check(phaseIds.Add(entry.EventId), "Distinct Root settlements reused a phase EventId."); }
            Check(count == 1, "Root did not emit exactly one Settlement phase entry.");
        }
    }

    // Break caught: the real Root still stops at Return, the final HP leaks
    // before each result, or an empty/normal settlement bypasses the Root lock.
    internal static void RootAutomaticallyRunsBothSettlementLanes()
    {
        foreach (bool enemy in new[] { false, true })
        using (var f = new DRPGBasicAttackRootPipelineProbe.Fixture(enemy, statuses: true))
        {
            var actor = f.Domain.Session.CurrentActor; long revision = f.Domain.Session.TransactionState.BattleRevision;
            f.Start(); f.ReachHit();
            for (int i = 0; i < 100 && SettlementLedger(f.Root) == null; i++) f.Step(.03);
            Check(SettlementLedger(f.Root) != null, "Root never entered its own Stage9 settlement after rendered return.");
            var projection = f.Domain.Surface.ProjectionArbiter.Active;
            // The primary strike is posture-first for either side. Stage9 therefore
            // starts from the unchanged HP cursors and reveals only settlement deltas.
            float playerFirst = 90, enemyBefore = 100;
            Check(projection.GetPresented(f.Domain.Player.RuntimeId).Hp == playerFirst &&
                projection.GetPresented(f.Domain.Enemy.RuntimeId).Hp == enemyBefore && f.Visuals.ActiveNumbers().Length == 1,
                "Root leaked future HP at the first party settlement item.");
            f.Step(.449);
            Check(projection.GetPresented(f.Domain.Player.RuntimeId).Hp == playerFirst && !Ready(f.Root), "Root shortened the first settlement item.");
            f.Step(.002);
            Check(projection.GetPresented(f.Domain.Player.RuntimeId).Hp == playerFirst + 3 &&
                projection.GetPresented(f.Domain.Enemy.RuntimeId).Hp == enemyBefore, "Root did not finish the party lane first.");
            f.Step(.451);
            Check(projection.GetPresented(f.Domain.Enemy.RuntimeId).Hp == enemyBefore - 7 && !Ready(f.Root), "Enemy settlement was omitted or completed early.");
            for (int i = 0; i < 80 && !Ready(f.Root); i++) f.Step(.03);
            Check(Ready(f.Root) && f.Source.Events == 1 && f.Domain.Session.TransactionState.BattleRevision == revision + 4 &&
                f.Source.Lease.IsActive && projection.IsActive && ReferenceEquals(actor, f.Domain.Session.CurrentActor) &&
                f.CompletedEvents == 0 && !f.StartResult.Receipt.Completion.IsCompleted,
                "Root did not stop after the full Stage9 barrier with one impact and its original actor locked.");
        }
    }

    internal static void RootSettlementDeathsFinalizeOnlyAfterBothResults()
    {
        using (var f = new DRPGBasicAttackRootPipelineProbe.Fixture(statuses: true, settlementDeaths: true))
        {
            f.Start(); f.ReachHit();
            for (int i = 0; i < 100 && SettlementLedger(f.Root) == null; i++) f.Step(.03);
            Check(SettlementLedger(f.Root) != null, "Root did not commit lethal settlement.");
            Check(f.Domain.Player.CurrentHp == 0 && f.Domain.Enemy.CurrentHp == 0 && f.Domain.Pool.ReleaseCount == 0 &&
                f.Visuals.ActiveDefeatMarkers().Length == 1, "Lethal Root exposed the future enemy cursor or departed immediately.");
            f.Step(.451);
            Check(f.Domain.Pool.ReleaseCount == 0 && f.Visuals.ActiveDefeatMarkers().Length == 2 && !Ready(f.Root), "Root skipped the enemy result hold.");
            for (int i = 0; i < 80 && !Ready(f.Root); i++) f.Step(.03);
            Check(Ready(f.Root) && f.Domain.Pool.ReleaseCount == 2 && f.Defeated.Count == 1 && f.CompletedEvents == 0 &&
                f.Source.Lease.IsActive && ReferenceEquals(f.Domain.Session.CurrentActor, f.Domain.Player),
                "Root death finalization advanced the action or did not await both typed transfers.");
        }
    }
    internal static void EmptyRootSettlementStillWaitsBeforeFinalization()
    {
        using (var f = new DRPGBasicAttackRootPipelineProbe.Fixture())
        {
            f.Start(); f.ReachHit();
            for (int i = 0; i < 100 && SettlementLedger(f.Root) == null; i++) f.Step(.03);
            Check(SettlementLedger(f.Root) != null, "Empty Root settlement was skipped.");
            f.Step(.299); Check(!Ready(f.Root), "Empty Root skipped its .30s handoff.");
            for (int i = 0; i < 30 && !Ready(f.Root); i++) f.Step(.03);
            Check(Ready(f.Root) && f.CompletedEvents == 0 && f.Source.Lease.IsActive, "Empty Root failed or prematurely completed.");
        }
    }
    internal static void MissingSettlementDepartureCannotCommitFinalFormation()
    {
        using (var f = new DRPGBasicAttackRootPipelineProbe.Fixture(statuses: true, settlementDeaths: true))
        {
            f.Port.HoldSettlementDeparture = true; long revision = f.Domain.Session.TransactionState.BattleRevision;
            f.Start(); f.ReachHit();
            for (int i = 0; i < 160 && f.Port.HeldSettlementDeparture == null; i++) f.Step(.03);
            Check(f.Port.HeldSettlementDeparture != null && f.Domain.Session.TransactionState.BattleRevision == revision + 3 &&
                f.Domain.Pool.ReleaseCount == 0 && f.CompletedEvents == 0, "Missing death receipt already changed final formation.");
            f.Step(2.1);
            Check(f.StartResult.Receipt.Completion.IsCompleted && !f.Source.Lease.IsActive && f.CompletedEvents == 0 &&
                f.Domain.Session.TransactionState.BattleRevision == revision + 3, "Missing Stage9 death gate was accepted or left a stuck Root.");
            Check(!f.Port.HeldSettlementDeparture.Receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "Late receipt")),
                "Late death completion replaced its cancelled terminal.");
        }
    }
    internal static void HomeCaptureReentryCannotCommitSettlementAfterScopeLoss()
    {
        using (var f = new DRPGBasicAttackRootPipelineProbe.Fixture(statuses: true))
        {
            int callbacks = 0; long revision = f.Domain.Session.TransactionState.BattleRevision;
            f.Port.OnSettlementHomes = () => { callbacks++; f.Root.Dispose(); };
            f.Start(); f.ReachHit();
            for (int i = 0; i < 100 && !f.StartResult.Receipt.Completion.IsCompleted; i++) f.Step(.03);
            Check(callbacks == 1 && f.StartResult.Receipt.Completion.IsCompleted && !f.Source.Lease.IsActive &&
                f.Domain.Session.TransactionState.BattleRevision == revision + 2 && f.Domain.Player.CurrentHp == 100 &&
                f.CompletedEvents == 0, "A callback-disposed Root still committed its settlement ledger.");
        }
    }
    internal static void ForeignSettlementReceiptCannotAdvanceOrCancelAnotherOwner()
    {
        using (var f = new DRPGBasicAttackRootPipelineProbe.Fixture(statuses: true))
        {
            var action = f.Frozen.Identity;
            var foreign = new DRPGBattleDeparturePresentation(new DRPGBattleDepartureJournal(
                BattleTransactionIdentity.ForRootAction(new ActionIdentity(action.BattleGeneration, action.TurnSerial, action.ActionId + 1)),
                1, Array.Empty<DRPGBattleDepartureMember>(), DRPGBattleActionStage.Settlement), 3);
            f.Port.ForeignSettlementDeparture = foreign; long revision = f.Domain.Session.TransactionState.BattleRevision;
            f.Start(); f.ReachHit();
            for (int i = 0; i < 160 && !f.StartResult.Receipt.Completion.IsCompleted; i++) f.Step(.03);
            Check(f.StartResult.Receipt.Completion.IsCompleted && !foreign.Receipt.Completion.IsCompleted &&
                f.Domain.Session.TransactionState.BattleRevision == revision + 3 && f.CompletedEvents == 0 && !f.Source.Lease.IsActive,
                "Foreign settlement receipt advanced this Root or was cancelled by it.");
        }
    }

    internal static void SettlementHomeSnapshotUsesSurvivorsPublishedSlotOrder()
    {
        using (var f = new DRPGBasicAttackDefeatTransferProbe.Fixture(false, fullBattlefield: true))
        {
            Check(!f.Adapter.TryCaptureSettlementHomes(f.Domain.Frozen.Identity, out _), "Settlement homes were available before Return.");
            f.Hit(); f.RenderHome();
            Check(!f.Adapter.TryCaptureSettlementHomes(f.Domain.Frozen.Identity, out _), "Rendered Return alone omitted the departure/formation gate.");
            var departure = f.Adapter.CompleteDefeats(f.Departures); f.Advance(.01);
            Check(f.Domain.Prepared.TryReconcileFormationAfterReturn(out var rules, out var error), error);
            var formation = f.Adapter.ReconcileFormation(rules);
            Check(!f.Adapter.TryCaptureSettlementHomes(f.Domain.Frozen.Identity, out _), "Unrendered layout was frozen as settlement homes.");
            f.Advance(.31); f.Advance(.01);
            Check(formation.Completion.IsCompleted, "Formation fixture did not complete.");
            Check(f.Adapter.TryCaptureSettlementHomes(f.Domain.Frozen.Identity, out var homes),
                "Completed formation did not provide a home snapshot.");
            Check(homes.Count == 5 && homes[new ParticipantId(7101)] == 0 && homes[new ParticipantId(7111)] == 1 &&
                homes[new ParticipantId(7112)] == 2 && homes[new ParticipantId(7121)] == 0 && homes[new ParticipantId(7122)] == 1 &&
                !homes.ContainsKey(new ParticipantId(7102)), "Settlement froze lock-time slots or retained the departed enemy.");
            bool rejected = false;
            try { ((System.Collections.Generic.IDictionary<ParticipantId, int>)homes)[new ParticipantId(7101)] = 4; }
            catch (NotSupportedException) { rejected = true; }
            Check(rejected && homes[new ParticipantId(7101)] == 0, "A caller rewrote the frozen settlement slot order.");
        }
    }

    internal static System.Collections.IEnumerator ProductionRootSettlement()
    {
        foreach (bool enemy in new[] { false, true })
        foreach (bool lethal in new[] { false, true })
        using (var f = new DRPGBasicAttackRootPipelineProbe.Fixture(enemy, production: true, statuses: true, settlementDeaths: lethal))
        {
            var actor = f.Domain.Session.CurrentActor; long revision = f.Domain.Session.TransactionState.BattleRevision;
            f.Start(); double deadline = f.Clock.Now + 12d;
            bool observedSettlement = false; double settlementAt = 0;
            while (!Ready(f.Root) && !f.StartResult.Receipt.Completion.IsCompleted && f.Clock.Now < deadline)
            {
                yield return null; f.Root.Tick(); f.Domain.Bridge.FlushQueuedSignals();
                if (!observedSettlement && SettlementLedger(f.Root) != null) { observedSettlement = true; settlementAt = f.Clock.Now; }
                Check(f.CompletedEvents == 0 && ReferenceEquals(actor, f.Domain.Session.CurrentActor) && f.Source.Lease.IsActive,
                    "Real full Root advanced or failed during Stage9: " + f.Status);
                Check(f.Visuals.ActiveNumbers().Length <= 1, "Real full Root overlapped settlement results.");
                if (lethal && observedSettlement && f.Clock.Now - settlementAt < .9d)
                    Check(f.Domain.Pool.ReleaseCount == 0, "Real full Root departed a settlement death before both results were readable.");
            }
            Check(Ready(f.Root) && observedSettlement && f.Clock.Now - settlementAt >= (lethal ? .9d : 1.35d) &&
                f.Domain.Session.TransactionState.BattleRevision == revision + 4 && f.Source.Events == 1 && f.CompletedEvents == 0 &&
                f.Source.Lease.IsActive && !f.StartResult.Receipt.Completion.IsCompleted &&
                f.Domain.Pool.ReleaseCount == (lethal ? 2 : 0) && f.Defeated.Count == (lethal ? 1 : 0),
                "Real Stage3–9 Root did not finish its complete settlement barrier: " + f.Status);
        }
    }
    private static object SettlementLedger(DRPGStandardBasicAttackTransactionModule root) => typeof(DRPGStandardBasicAttackTransactionModule)
        .GetProperty("SettlementLedger", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(root);
    private static bool Ready(DRPGStandardBasicAttackTransactionModule root) => (bool?)typeof(DRPGStandardBasicAttackTransactionModule)
        .GetProperty("ReadyForFinalization", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(root) == true;
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
#endif
