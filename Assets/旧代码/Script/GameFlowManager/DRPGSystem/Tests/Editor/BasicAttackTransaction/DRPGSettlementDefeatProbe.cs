#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;

internal static class DRPGSettlementDefeatProbe
{
    // Break caught: canonical lethal settlement filters a pinned view early,
    // or a later lane erases the original X/dim/pose before finalization.
    internal static void LethalCursorRetainsOriginalAcrossBothLanes()
    {
        using (var f = new DRPGSettlementPresentationProbe.Fixture(true, true, true))
        {
            f.Projection.TryGetView(f.Domain.Player.RuntimeId, out var player);
            f.Projection.TryGetView(f.Domain.Enemy.RuntimeId, out var enemy);
            Check(f.PlayerHp == 100 && f.EnemyHp == 100 && f.Visuals.ActiveDefeatMarkers().Length == 0 &&
                player.gameObject.activeInHierarchy && enemy.gameObject.activeInHierarchy, "Committed future deaths leaked before their cursors.");
            using (var sequence = f.Start())
            {
                var renderer = player.GetComponentInChildren<SpriteRenderer>(); var sprite = renderer.sprite;
                Check(f.PlayerHp == 0 && f.EnemyHp == 100 && f.Visuals.ActiveDefeatMarkers().Length == 1 &&
                    renderer.color.r <= .4f && renderer.color.a > .9f, "Player lethal cursor lost its X/dim or exposed the enemy's future death.");
                f.Step(sequence, .451);
                Check(f.EnemyHp == 0 && f.Visuals.ActiveDefeatMarkers().Length == 2 && player.gameObject.activeInHierarchy &&
                    enemy.gameObject.activeInHierarchy && renderer.sprite == sprite && f.Domain.Pool.ReleaseCount == 0 && f.Defeated.Count == 0,
                    "Second lane prematurely removed or swapped a settlement death.");
                f.Step(sequence, .451);
                Check(sequence.ReadyForDefeatFinalization && f.Domain.Pool.ReleaseCount == 0 && f.Completed == 0 && f.RuleLease.IsActive,
                    "Completing the item queue bypassed the independent finalization barrier.");
            }
        }
    }

    // Break caught: an already-completed Stage8 return authorizes a NEW Stage9 death.
    internal static void OldReturnCannotAuthorizeSettlementDeath()
    {
        using (var f = new DRPGSettlementPresentationProbe.Fixture(true, true, false))
        using (var sequence = f.Start())
        {
            var old = (BattleReturnBarrier)typeof(DRPGBattleTransactionPresentationAdapter)
                .GetField("returnBarrier", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(f.Adapter);
            Check(old.Receipt.Completion.IsCompleted && !f.Projection.CanDepart(old, f.Domain.Player.RuntimeId),
                "Stage9 death reused the earlier Stage8 rendered-home permit.");
        }
    }

    // Break caught: transfer starts before all lanes, reuses the previous render
    // fence, completes before the new models render, or releases twice on replay.
    internal static void FinalizationWaitsForAllLanesAndOwnRenderedHome()
    {
        using (var f = new DRPGSettlementPresentationProbe.Fixture(true, true, true))
        using (var sequence = f.Start())
        {
            var facts = Describe(f);
            Check(Faulted(Complete(f, facts).Receipt) && f.Domain.Pool.ReleaseCount == 0, "Incomplete player lane allowed finalization.");
            f.Step(sequence, .451);
            Check(Faulted(Complete(f, facts).Receipt) && f.Domain.Pool.ReleaseCount == 0, "Incomplete enemy result allowed finalization.");
            f.Step(sequence, .451);
            var result = Complete(f, facts);
            Check(!result.Receipt.Completion.IsCompleted && result.Receipt.Stage == DRPGBattleActionStage.Settlement &&
                f.Domain.Pool.ReleaseCount == 0, "Finalization skipped its independent home fence.");
            Advance(f); Check(f.Domain.Pool.ReleaseCount == 0, "First home observation substituted for a rendered frame.");
            Advance(f); Check(f.Domain.Pool.ReleaseCount == 2 && f.Defeated.Count == 1 && !result.Receipt.Completion.IsCompleted,
                "Both deaths were not transferred together after their new home fence.");
            Advance(f);
            Check(result.Receipt.Completion.IsCompleted && !Faulted(result.Receipt) && result.Views.Count == 2 &&
                f.Defeated.TryGet(new ParticipantId(f.Domain.Player.RuntimeId), out var model) && model.activeInHierarchy &&
                model.transform.position == f.IncapAnchor.position && f.Visuals.ActiveDefeatMarkers().Length == 1 &&
                ReferenceEquals(result, Complete(f, facts)) && f.Domain.Pool.ReleaseCount == 2 && f.Completed == 0 &&
                f.RuleLease.IsActive && f.Projection.IsActive && ReferenceEquals(f.Domain.Session.CurrentActor, f.Domain.Player),
                "Typed settlement departure lost its rendered model, identity, replay protection or Root lock.");
        }
    }

    internal static void HiddenHomeOrMissingIncapCannotReleaseAnyMember()
    {
        foreach (bool hidden in new[] { false, true })
        using (var f = new DRPGSettlementPresentationProbe.Fixture(true, true, true, bindIncap: hidden))
        using (var sequence = f.Start())
        {
            f.Step(sequence, .451); f.Step(sequence, .451);
            if (hidden) f.Visuals.ActiveDefeatMarkers()[0].gameObject.SetActive(false);
            var result = Complete(f, Describe(f)); Advance(f); Advance(f); Advance(f);
            Check(Faulted(result.Receipt) && f.Domain.Pool.ReleaseCount == 0 && f.Defeated.Count == 0 && f.RuleLease.IsActive,
                "Invisible home or missing authored incap binding caused partial settlement departure.");
        }
    }

    // Break caught: the rule commit or whole-roster movement starts before the
    // rendered departure receipt, compacts one death at a time, or advances Root.
    internal static void SettlementReconcilesAllSurvivorsOnceAfterTypedDeparture()
    {
        using (var f = new DRPGSettlementPresentationProbe.Fixture(true, true, true, fullBattlefield: true))
        using (var sequence = f.Start())
        {
            long revision = f.Domain.Session.TransactionState.BattleRevision;
            var origin = new System.Collections.Generic.Dictionary<int, Vector3>();
            foreach (var view in f.Domain.Roster.ActiveViews) origin.Add(view.RuntimeId, view.transform.position);
            var missing = new DRPGBattleDeparturePresentation(Describe(f));
            Check(!TryReconcile(f, missing, out _) && f.Domain.Session.TransactionState.BattleRevision == revision,
                "Unrendered departure changed timeline or formation.");
            f.Step(sequence, .451); f.Step(sequence, .451);
            var departure = Complete(f, Describe(f)); Advance(f); Advance(f);
            Check(!TryReconcile(f, departure, out _) && f.Domain.Session.TransactionState.BattleRevision == revision,
                "Model activation substituted for rendered departure.");
            Advance(f);
            Check(TryReconcile(f, departure, out var rules) && rules.FormationChanges.Count == 4 &&
                rules.CommittedRevision == revision + 1 && f.RuleLease.IsActive && f.Completed == 0,
                "Final settlement did not compact both sides in one rule commit.");
            foreach (var entry in f.Domain.Session.TransactionState.CaptureRuleSnapshot().Timeline)
                Check(entry.Participant.Value != 7101 && entry.Participant.Value != 7102, "Settlement death remained a future candidate.");
            foreach (var change in rules.FormationChanges)
                Check(change.Participant.Value == 7111 || change.Participant.Value == 7121
                    ? change.Before == DRPGFormationRow.Middle && change.After == DRPGFormationRow.Front
                    : change.Before == DRPGFormationRow.Rear && change.After == DRPGFormationRow.Middle,
                    "Wrong frozen formation row transition.");
            var receipt = ReconcileViews(f, rules);
            Check(!receipt.Completion.IsCompleted && receipt.Stage == DRPGBattleActionStage.Settlement, "Stage9 formation was skipped or disguised as Return.");
            foreach (var view in f.Domain.Roster.ActiveViews) Check(view.transform.position == origin[view.RuntimeId], "Formation jumped before its first motion frame.");
            Advance(f, .15);
            foreach (var view in f.Domain.Roster.ActiveViews) Check(view.transform.position != origin[view.RuntimeId], "A survivor missed the shared formation start.");
            Check(!receipt.Completion.IsCompleted, "Formation lost its .30s minimum.");
            Advance(f, .16); Check(!receipt.Completion.IsCompleted, "Formation completion skipped the new home render fence.");
            Advance(f);
            Check(receipt.Completion.IsCompleted && !Faulted(receipt) && f.Projection.IsActive && f.RuleLease.IsActive && f.Completed == 0,
                "Settlement formation failed or advanced Root.");
            var homes = f.Projection.Capture(DRPGBattleProjectionCheckpointKind.PostSettlementReconcileCheckpoint);
            Check(homes.Actors.Count == 4, "Final home checkpoint retained a departed actor.");
            for (int i = 0; i < homes.Actors.Count; i++)
            for (int j = i + 1; j < homes.Actors.Count; j++) Check(homes.Actors[i].Home.Position != homes.Actors[j].Home.Position, "Two survivors share a home.");
            Check(TryReconcile(f, departure, out var replay) && ReferenceEquals(rules, replay) &&
                ReferenceEquals(receipt, ReconcileViews(f, replay)) && f.Domain.Session.TransactionState.BattleRevision == revision + 1,
                "Settlement reconciliation replayed rule writes or animation.");
        }
    }

    // Break caught: Stage9 checks obsolete LockBaseline after a prior formation
    // has legitimately published a new Home checkpoint.
    internal static void SettlementDefeatUsesLatestPublishedHome()
    {
        using (var f = new DRPGSettlementPresentationProbe.Fixture(true, true, true))
        {
            var motion = (BattleActorMotionController)typeof(DRPGBattleTransactionPresentationAdapter)
                .GetField("motion", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(f.Adapter);
            f.Projection.TryGetView(f.Domain.Player.RuntimeId, out var player);
            player.transform.position += Vector3.left; f.IncapAnchor.position = player.transform.position;
            f.Projection.UpdateFormationHome(motion, f.Domain.Player.RuntimeId);
            using (var sequence = f.Start())
            {
                f.Step(sequence, .451); f.Step(sequence, .451);
                var result = Complete(f, Describe(f)); Advance(f); Advance(f); Advance(f);
                Check(result.Receipt.Completion.IsCompleted && !Faulted(result.Receipt) && f.Domain.Pool.ReleaseCount == 2,
                    "Settlement death checked its old lock position instead of the current published home.");
            }
        }
    }

    internal static void ChildReactionDeathStaysInItsParentLaneUntilFinalization()
    {
        using (var f = new DRPGSettlementPresentationProbe.Fixture(true, true, false, lastWord: true))
        using (var sequence = f.Start())
        {
            Check(f.Ledger.Groups.Count == 1 && f.Ledger.Groups[0].Children.Count == 1 && f.Ledger.Groups[0].Children[0].RequiresPresentation &&
                f.PlayerHp == 0 && f.EnemyHp == 100 && Describe(f).Members.Count == 2 && f.Visuals.ActiveDefeatMarkers().Length == 1,
                "OnDefeat child leaked early, became another lane or escaped typed death membership.");
            f.Step(sequence, .451);
            Check(f.EnemyHp == 0 && f.Visuals.ActiveDefeatMarkers().Length == 2 && f.Domain.Pool.ReleaseCount == 0 &&
                !sequence.ReadyForDefeatFinalization, "Child death was not a separately held parent-lane result.");
            f.Step(sequence, .451);
            var result = Complete(f, Describe(f)); Advance(f); Advance(f); Advance(f);
            Check(!Faulted(result.Receipt) && result.Views.Count == 2 && f.Completed == 0 && f.RuleLease.IsActive,
                "Child-caused death bypassed the shared finalization transaction.");
        }
    }

    internal static void ForeignFactsAndUnrenderedReplayCannotMutateSettlement()
    {
        using (var f = new DRPGSettlementPresentationProbe.Fixture(true, true, true))
        using (var sequence = f.Start())
        {
            f.Step(sequence, .451); f.Step(sequence, .451);
            var facts = Describe(f); long revision = f.Domain.Session.TransactionState.BattleRevision;
            foreach (var invalid in new[] {
                new DRPGBattleDepartureJournal(facts.Owner, facts.SourceFingerprint + 1, facts.Members, DRPGBattleActionStage.Settlement),
                new DRPGBattleDepartureJournal(facts.Owner, facts.SourceFingerprint, facts.Members),
                new DRPGBattleDepartureJournal(facts.Owner, facts.SourceFingerprint, Array.Empty<DRPGBattleDepartureMember>(), DRPGBattleActionStage.Settlement) })
                Check(Faulted(Complete(f, invalid).Receipt) && f.Domain.Pool.ReleaseCount == 0, "Foreign death provenance caused departure.");
            var fake = new DRPGBattleDeparturePresentation(facts);
            fake.Receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "No actual views transferred"));
            Check(!TryReconcile(f, fake, out _) && f.Domain.Session.TransactionState.BattleRevision == revision,
                "A completed receipt with missing view facts changed formation.");
            var real = Complete(f, facts); Advance(f); Advance(f); Advance(f);
            Check(!Faulted(real.Receipt), "Invalid request poisoned later valid finalization.");
            Check(f.Domain.Session.TransactionState.TryCommitJournal(f.RuleLease, f.Domain.Session.TransactionState.CreateWorkspace()).Succeeded,
                "Could not create the intervening-revision boundary.");
            revision++;
            Check(!TryReconcile(f, real, out _) && f.Domain.Session.TransactionState.BattleRevision == revision,
                "Settlement reconciliation consumed an intervening revision.");
        }
    }

    // Break caught: a backward frame/time between home observations proves a
    // new frame by accident, or a timed-out finalizer later releases its views.
    internal static void FinalizationRejectsBackwardClockAndLateRelease()
    {
        foreach (bool backward in new[] { true, false })
        using (var f = new DRPGSettlementPresentationProbe.Fixture(true, true, true))
        using (var sequence = f.Start())
        {
            f.Step(sequence, .451); f.Step(sequence, .451);
            var result = Complete(f, Describe(f)); Advance(f, .1);
            if (backward) { f.Clock.Now -= .05; f.Clock.RenderedFrame++; f.Adapter.Tick(); }
            else Advance(f, 2.1);
            Check(Faulted(result.Receipt) && f.Domain.Pool.ReleaseCount == 0 && f.RuleLease.IsActive,
                "Backward clock or deadline expiry authorized settlement departure: backward=" + backward +
                "; terminal=" + (result.Receipt.Completion.IsCompleted ? result.Receipt.Completion.Result.Kind.ToString() : "Pending") +
                "; releases=" + f.Domain.Pool.ReleaseCount);
            Advance(f);
            Check(f.Domain.Pool.ReleaseCount == 0 && f.Completed == 0, "Late frame revived a faulted departure barrier.");
        }
    }

    internal static void FormationTimeoutCannotBecomeDegradedCompletion()
    {
        using (var f = new DRPGSettlementPresentationProbe.Fixture(true, true, true, fullBattlefield: true))
        using (var sequence = f.Start())
        {
            f.Step(sequence, .451); f.Step(sequence, .451);
            var departure = Complete(f, Describe(f)); Advance(f); Advance(f); Advance(f);
            Check(TryReconcile(f, departure, out var rules), "Could not prepare the real formation timeout boundary.");
            var receipt = ReconcileViews(f, rules); Advance(f, 2.4);
            Check(Faulted(receipt) && f.Projection.IsActive && f.RuleLease.IsActive && f.Completed == 0,
                "Timed-out formation granted Degraded without rendered home proof.");
            Advance(f);
            Check(ReferenceEquals(receipt, ReconcileViews(f, rules)) && Faulted(receipt) && f.Completed == 0,
                "Late frame restarted a failed settlement formation.");
        }
    }

    internal static void EmptyFinalizationHasExplicitTypedNoOpWithoutAdvancingRoot()
    {
        using (var f = new DRPGSettlementPresentationProbe.Fixture(false))
        using (var sequence = f.Start())
        {
            f.Step(sequence, .31);
            var result = Complete(f, Describe(f)); Advance(f); Advance(f);
            Check(result.Receipt.Completion.IsCompleted && !Faulted(result.Receipt) && result.Views.Count == 0 &&
                f.Domain.Pool.ReleaseCount == 0,
                "Empty finalization skipped its explicit typed rule handoff.");
            Check(TryReconcile(f, result, out var rules) && rules.FormationChanges.Count == 0, "Empty rule reconciliation failed.");
            // The explicit empty layout is immediate: the only readable empty
            // settlement delay was already paid by the step sequence above.
            var formation = ReconcileViews(f, rules);
            Check(formation.Completion.IsCompleted && !Faulted(formation) && f.RuleLease.IsActive && f.Completed == 0,
                "Empty layout created another wait or completed the Root.");
        }
    }

    internal static void IncapActivationLosingProjectionCannotLeakAModel()
    {
        using (var f = new DRPGSettlementPresentationProbe.Fixture(true, true, true))
        using (var sequence = f.Start())
        {
            f.Domain.Root.transform.Find("SettlementIncapTemplateProbe").gameObject.AddComponent<DRPGBasicAttackDefeatTransferProbe.IncapEnableHook>();
            f.Step(sequence, .451); f.Step(sequence, .451);
            var result = Complete(f, Describe(f)); int callbacks = 0;
            DRPGBasicAttackDefeatTransferProbe.IncapEnableHook.Callback = () => { callbacks++; f.Projection.Dispose(); };
            try
            {
                Advance(f); Advance(f);
                Check(callbacks == 1 && Faulted(result.Receipt) && f.Defeated.Count == 0 && f.Completed == 0,
                    "Reentrant incap activation retained a model or completed settlement after owner loss.");
            }
            finally { DRPGBasicAttackDefeatTransferProbe.IncapEnableHook.Callback = null; }
        }
    }

    internal static bool TryReconcile(DRPGSettlementPresentationProbe.Fixture f, DRPGBattleDeparturePresentation departure, out DRPGBattlePostReturnResult rules)
    {
        var method = typeof(DRPGPreparedBasicAttackRuleAction).GetMethod("TryReconcileFormationAfterSettlement", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(method != null, "Settlement has no typed post-departure rule reconciliation.");
        var args = new object[] { departure, null, null }; bool result = (bool)method.Invoke(f.Prepared, args);
        rules = args[1] as DRPGBattlePostReturnResult; return result;
    }
    internal static PresentationReceipt ReconcileViews(DRPGSettlementPresentationProbe.Fixture f, DRPGBattlePostReturnResult rules)
    {
        var method = typeof(DRPGBattleTransactionPresentationAdapter).GetMethod("ReconcileSettlementFormation", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Check(method != null, "Adapter has no typed settlement formation receipt.");
        return (PresentationReceipt)method.Invoke(f.Adapter, new object[] { rules });
    }

    internal static DRPGBattleDepartureJournal Describe(DRPGSettlementPresentationProbe.Fixture f)
    {
        var method = typeof(DRPGPreparedBasicAttackRuleAction).GetMethod("TryDescribeSettlementDepartures", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(method != null, "Committed settlement has no typed departure description.");
        var args = new object[] { null, null };
        Check((bool)method.Invoke(f.Prepared, args), "Settlement departure rejected: " + args[1]);
        return (DRPGBattleDepartureJournal)args[0];
    }
    internal static DRPGBattleDeparturePresentation Complete(DRPGSettlementPresentationProbe.Fixture f, DRPGBattleDepartureJournal facts)
    {
        var method = typeof(DRPGBattleTransactionPresentationAdapter).GetMethod("CompleteSettlementDefeats", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Check(method != null, "Adapter has no whole-stage settlement death barrier.");
        return (DRPGBattleDeparturePresentation)method.Invoke(f.Adapter, new object[] { facts });
    }
    internal static void Advance(DRPGSettlementPresentationProbe.Fixture f, double delta = .01)
    { f.Clock.Now += delta; f.Clock.RenderedFrame++; f.Adapter.Tick(); }
    internal static bool Faulted(PresentationReceipt r) => r.Completion.IsCompleted && r.Completion.Result.Kind == PresentationReceiptTerminalKind.Faulted;
    internal static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
#endif
