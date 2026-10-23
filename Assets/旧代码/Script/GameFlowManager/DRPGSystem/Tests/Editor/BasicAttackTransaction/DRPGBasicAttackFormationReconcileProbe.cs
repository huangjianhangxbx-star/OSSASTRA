#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

public static class DRPGBasicAttackFormationReconcileProbe
{
    public static void FormationWaitsForDepartureThenMovesTheWholeLayout()
    {
        foreach (double delta in new[] { 1d / 60d, .12d })
        using (var f = new DRPGBasicAttackDefeatTransferProbe.Fixture(false, fullBattlefield: true))
        {
            f.Hit(); f.RenderHome();
            // Deliberately call the rule primitive before transfer in this
            // adversarial test. The presentation gate must still reject it.
            Check(f.Domain.Prepared.TryReconcileFormationAfterReturn(out var rules, out var error), error);
            Check(Faulted(f.Adapter.ReconcileFormation(rules)), "Formation started before typed departure.");
            var departed = f.Adapter.CompleteDefeats(f.Departures);
            Check(Faulted(f.Adapter.ReconcileFormation(rules)) && !departed.Receipt.Completion.IsCompleted,
                "Formation started before the departure's new-state rendered frame.");
            f.Advance(.01d);
            var positions = new Dictionary<int, Vector3>();
            foreach (var view in f.Domain.Domain.Roster.ActiveViews) positions.Add(view.RuntimeId, view.transform.position);
            var receipt = f.Adapter.ReconcileFormation(rules);
            Check(!receipt.Completion.IsCompleted, "Nonempty formation completed instead of issuing a movement barrier.");
            double started = f.Domain.Clock.Now;
            while (f.Domain.Clock.Now - started < .30d)
            {
                f.Advance(delta);
                foreach (int id in new[] { 7121, 7122 })
                {
                    Check(f.Projection.TryGetView(id, out var view) && view.transform.position != positions[id],
                        "A surviving actor missed the shared formation movement start.");
                }
                CheckUniquePositions(f);
                Check(!receipt.Completion.IsCompleted, "Formation skipped minimum duration or its rendered-frame fence.");
            }
            f.Adapter.Tick(); Check(!receipt.Completion.IsCompleted, "Extra Tick replaced the formation rendered frame.");
            f.Advance(.01d);
            Check(receipt.Completion.IsCompleted && !Faulted(receipt) && f.Domain.Lease.IsActive && f.Domain.Events == 1,
                "Formation did not close its own barrier without advancing Root.");
            var checkpoint = f.Projection.Capture(DRPGBattleProjectionCheckpointKind.PostReturnImpactReconcileCheckpoint);
            var finalPositions = new Dictionary<int, Vector3>();
            foreach (var row in checkpoint.Actors)
            {
                f.Projection.TryGetView(row.RuntimeId, out var view);
                Check(row.Home.Position == view.transform.position, "Reconciled checkpoint retained the old active home.");
                finalPositions.Add(row.RuntimeId, view.transform.position);
            }
            Check(ReferenceEquals(receipt, f.Adapter.ReconcileFormation(rules)), "Repeated formation issued another batch.");
            f.Projection.Dispose();
            foreach (var view in f.Domain.Domain.Roster.ActiveViews)
                Check(view.transform.position == finalPositions[view.RuntimeId], "Final canonical bind snapped the newly reconciled formation.");
        }
    }

    public static void EmptyFormationHasAnExplicitCompletedReceipt()
    {
        using (var f = new DRPGBasicAttackDefeatTransferProbe.Fixture(false, lethal: false))
        {
            f.Hit(); f.RenderHome();
            var departed = f.Adapter.CompleteDefeats(f.Departures);
            Check(departed.Receipt.Completion.IsCompleted, "Empty departure was not explicit.");
            Check(f.Domain.Prepared.TryReconcileFormationAfterReturn(out var rules, out var error), error);
            var receipt = f.Adapter.ReconcileFormation(rules);
            Check(receipt != null && receipt.Completion.IsCompleted && !Faulted(receipt) && f.Domain.Lease.IsActive,
                "An empty formation did not return its explicit successful terminal: " +
                (receipt == null ? "null" : receipt.Completion.IsCompleted ? receipt.Completion.Result.Detail : "pending"));
        }
    }

    public static void FormationTimeoutStopsOwnedMotionWithoutAdvancingRoot()
    {
        using (var f = new DRPGBasicAttackDefeatTransferProbe.Fixture(false, fullBattlefield: true))
        {
            f.Hit(); f.RenderHome(); f.Adapter.CompleteDefeats(f.Departures); f.Advance(.01d);
            Check(f.Domain.Prepared.TryReconcileFormationAfterReturn(out var rules, out var error), error);
            var receipt = f.Adapter.ReconcileFormation(rules); f.Advance(.15d);
            var positions = new Dictionary<int, Vector3>();
            foreach (var view in f.Domain.Domain.Roster.ActiveViews) positions.Add(view.RuntimeId, view.transform.position);
            f.Advance(3d);
            Check(Faulted(receipt) && f.Domain.Lease.IsActive && f.Domain.Events == 1 && f.Adapter.RecoveryCount == 1,
                "Formation timeout falsely completed or advanced Root.");
            f.Advance(.2d);
            foreach (var view in f.Domain.Domain.Roster.ActiveViews)
                Check(view.transform.position == positions[view.RuntimeId], "Formation timeout snapped or left an owned tween running.");
            CheckUniquePositions(f);
        }
    }

    public static void FormationRejectsStaleRevisionWithoutMovement()
    {
        using (var f = new DRPGBasicAttackDefeatTransferProbe.Fixture(false, fullBattlefield: true))
        {
            f.Hit(); f.RenderHome(); f.Adapter.CompleteDefeats(f.Departures); f.Advance(.01d);
            Check(f.Domain.Prepared.TryReconcileFormationAfterReturn(out var rules, out var error), error);
            var stale = new DRPGBattlePostReturnResult(rules.Departures, rules.CommittedRevision - 1, rules.FormationChanges);
            var positions = new Dictionary<int, Vector3>();
            foreach (var view in f.Domain.Domain.Roster.ActiveViews) positions.Add(view.RuntimeId, view.transform.position);
            Check(Faulted(f.Adapter.ReconcileFormation(stale)), "Stale rule revision started a formation batch.");
            f.Advance(.1d);
            foreach (var view in f.Domain.Domain.Roster.ActiveViews)
                Check(view.transform.position == positions[view.RuntimeId], "Rejected formation mutated a view.");
        }
    }

    private static void CheckUniquePositions(DRPGBasicAttackDefeatTransferProbe.Fixture f)
    {
        var views = f.Domain.Domain.Roster.ActiveViews;
        for (int i = 0; i < views.Count; i++)
        for (int j = i + 1; j < views.Count; j++)
            Check(Vector3.Distance(views[i].transform.position, views[j].transform.position) > .001f,
                "Formation assigned two actors the same position.");
    }
    private static bool Faulted(PresentationReceipt receipt) => receipt.Completion.IsCompleted && receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Faulted;
    private static void Check(bool value, string message) => DRPGBasicAttackProjectionProbe.Check(value, message);
}
#endif
