#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;

// Supplemental real-frame tests for shared TurnEntry settlement/death/reconciliation.
// They do not claim Session startup, candidate publication or F8 integration.
public static class DRPGTurnEntryProductionProbe
{
    public static string Status { get; private set; } = "NotRun";
    public static string StartProduction()
    {
        if (!Application.isPlaying || Status == "Running") throw new InvalidOperationException("Fresh Play Mode required.");
        Status = "Running"; new GameObject("TurnEntryRealFrameProbeHost").AddComponent<Host>().Begin(); return Status;
    }
    private sealed class Host : MonoBehaviour
    {
        private IEnumerator work;
        internal void Begin() { work = Run(); StartCoroutine(Drive()); }
        private IEnumerator Drive()
        {
            while (true)
            {
                bool next;
                try { next = work.MoveNext(); }
                catch (Exception error) { Status = "FAIL: " + error; break; }
                if (!next) { Status = "PASS: 7 TurnEntry real-frame cases (empty, serial status, death, whole-roster reconciliation)"; break; }
                yield return work.Current;
            }
            (work as IDisposable)?.Dispose(); work = null; Destroy(gameObject);
        }
        private void OnDestroy()
        {
            (work as IDisposable)?.Dispose(); work = null;
            if (Status == "Running") Status = "Interrupted: host destroyed before completion";
        }
    }
    private static IEnumerator Run()
    {
        // enemy candidate, lethal, statuses, full battlefield
        var cases = new[] { new[] { false, false, false, false }, new[] { false, false, true, false }, new[] { true, false, true, false },
            new[] { false, true, true, false }, new[] { true, true, true, false },
            new[] { false, true, true, true }, new[] { true, true, true, true } };
        for (int index = 0; index < cases.Length; index++)
        {
            var c = cases[index];
            using (var f = new DRPGTurnEntryPresentationProbe.Fixture(c[0], c[1], statuses: c[2], fullBattlefield: c[3]))
            {
                var clock = f.Domain.Root.AddComponent<UnityRenderFrameFence>(); f.Clock.UseLiveClock(clock);
                var previousActor = f.Domain.Session.CurrentActor;
                long revision = f.Domain.Session.TransactionState.BattleRevision;
                double began = clock.Now, deadline = began + 8d, visibleSince = began;
                long startedFrame = clock.RenderedFrame;
                int visibleItems = c[2] ? 1 : 0;
                int initialCount = c[3] ? 6 : 2;
                string label = string.Empty;
                using (var motion = new BattleActorMotionController(f.Projection, clock))
                using (var sequence = f.Start())
                {
                    Check(sequence.Fault.Length == 0, index, sequence.Fault);
                    label = Caption(f);
                    while (!sequence.ReadyForDefeatFinalization && clock.Now < deadline)
                    {
                        yield return null; sequence.Tick();
                        f.Domain.Surface.RenderCombatants(DRPGBattlePresentationSnapshot.Capture(f.Domain.Session));
                        f.Domain.Bridge.FlushQueuedSignals();
                        Check(sequence.Fault.Length == 0 && f.Visuals.ActiveNumbers().Length <= 1, index,
                            "Candidate sequence fault or overlapping labels: " + sequence.Fault);
                        var next = Caption(f);
                        if (next != label)
                        {
                            if (label.Length != 0) Check(clock.Now - visibleSince >= .45d, index, "Visible status vanished before minimum dwell.");
                            if (next.Length != 0) { visibleItems++; visibleSince = clock.Now; }
                            label = next;
                        }
                        if (label.StartsWith("a-turn-entry", StringComparison.Ordinal))
                            Check(f.PresentedHp == (c[1] ? 0 : 97), index, "Canonical update exposed a future candidate HP step.");
                        Check(f.CandidateView.GetInstanceID() == f.ViewId && f.CandidateView.gameObject.activeInHierarchy &&
                            f.CandidateView.transform.position == f.Home && f.Domain.Pool.ReleaseCount == 0 &&
                            f.Completed == 0 && ReferenceEquals(f.Domain.Session.CurrentActor, previousActor) &&
                            f.RuleLease.IsActive && f.Domain.Session.TransactionState.BattleRevision == revision,
                            index, "Candidate presenter moved/replaced an actor or advanced rules/actor/completion.");
                        if (c[1]) Check(f.Visuals.ActiveDefeatMarkers().Length == 1 && f.Body.color.r <= .401f && f.Defeated.Count == 0,
                            index, "Lethal candidate lost X/dim or changed to incap before its barrier.");
                    }
                    int expectedItems = c[2] ? c[1] ? 1 : 2 : 0;
                    Check(sequence.ReadyForDefeatFinalization && visibleItems == expectedItems && clock.RenderedFrame > startedFrame &&
                        clock.Now - began >= (c[2] ? expectedItems * .45d : .30d), index, "Candidate sequence shortened/omitted readable steps.");
                    var facts = DRPGSettlementDepartureFacts.Describe(f.Ledger);
                    var departure = f.Presenter.CompleteDefeats(facts, f.Defeated);
                    f.Presenter.Tick(); long homeFrame = clock.RenderedFrame;
                    Check(!departure.Receipt.Completion.IsCompleted && f.Domain.Pool.ActiveCount == initialCount, index, "Candidate reused an old rendered-home permit.");
                    Check(!f.Domain.Session.TransactionState.TryReconcileSettlement(f.RuleLease, f.Ledger, departure, out _, out _) &&
                        f.Domain.Session.TransactionState.BattleRevision == revision, index, "Unrendered departure changed timeline/rows.");
                    while (!departure.Receipt.Completion.IsCompleted && clock.Now < deadline)
                    { yield return null; f.Presenter.Tick(); }
                    Check(departure.Receipt.Completion.IsCompleted && departure.Receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed &&
                        departure.Receipt.Owner == f.RuleLease.Owner && departure.Receipt.Stage == DRPGBattleActionStage.TurnBegin &&
                        clock.RenderedFrame > homeFrame && f.Domain.Pool.ActiveCount == initialCount - (c[1] ? 1 : 0), index, "Candidate typed death/empty barrier failed.");
                    if (c[1])
                    {
                        Check(departure.Views.Count == 1 && departure.Views[0].OriginalViewInstanceId == f.ViewId, index, "Departure lost original view identity.");
                        var view = departure.Views[0];
                        Check(c[0] ? view.IncapacitatedModelInstanceId == 0 : f.Defeated.IsVisible(view.Participant, view.IncapacitatedModelInstanceId),
                            index, "Wrong visible enemy departure or allied incap state.");
                    }
                    Check(f.Completed == 0 && f.RuleLease.IsActive && ReferenceEquals(f.Domain.Session.CurrentActor, previousActor),
                        index, "Death presentation published an action or candidate by itself.");
                    Check(f.Domain.Session.TransactionState.TryReconcileSettlement(f.RuleLease, f.Ledger, departure, out var rules, out var error), index, error);
                    Check(rules.CommittedRevision == revision + 1, index, "Candidate reconciliation was not one atomic commit.");
                    var origins = new System.Collections.Generic.Dictionary<int, Vector3>();
                    foreach (var view in f.Domain.Roster.ActiveViews) origins.Add(view.RuntimeId, view.transform.position);
                    using (var formation = new BattleFormationReconcileBarrier(f.Projection, motion, clock, f.Ledger, rules))
                    {
                        double movementStart = clock.Now; bool sawMotion = false;
                        foreach (var view in f.Domain.Roster.ActiveViews) Check(view.transform.position == origins[view.RuntimeId], index, "Formation teleported on issuance.");
                        while (!formation.Receipt.Completion.IsCompleted && clock.Now < deadline)
                        {
                            yield return null; motion.Tick(); formation.Tick();
                            foreach (var change in rules.FormationChanges)
                            {
                                Check(f.Projection.TryGetView(change.Participant.Value, out var view), index, "Formation survivor disappeared.");
                                sawMotion |= view.transform.position != origins[view.RuntimeId];
                            }
                            Check(f.RuleLease.IsActive && f.Completed == 0 && ReferenceEquals(f.Domain.Session.CurrentActor, previousActor) &&
                                f.Domain.Session.TransactionState.BattleRevision == revision + 1, index, "Formation advanced a candidate or replayed rules.");
                        }
                        Check(formation.Receipt.Completion.IsCompleted && formation.Receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed &&
                            formation.Receipt.Owner == f.RuleLease.Owner && formation.Receipt.Stage == DRPGBattleActionStage.TurnBegin,
                            index, "Candidate formation failed its owned rendered barrier.");
                        if (c[3]) Check(sawMotion && clock.Now - movementStart >= .30, index, "Full battlefield skipped smooth reconciliation.");
                        foreach (var write in motion.Journal) Check(write.Transaction == f.RuleLease.Owner && !write.Owner.IsValid,
                            index, "Candidate movement borrowed RootAction identity.");
                        var checkpoint = f.Projection.Capture(DRPGBattleProjectionCheckpointKind.PostSettlementReconcileCheckpoint);
                        Check(checkpoint.Actors.Count == initialCount - (c[1] ? 1 : 0), index, "Reconciled roster retained a departed view.");
                        for (int a = 0; a < checkpoint.Actors.Count; a++)
                        for (int b = a + 1; b < checkpoint.Actors.Count; b++)
                            Check(checkpoint.Actors[a].Home.Position != checkpoint.Actors[b].Home.Position, index, "Reconciled homes overlap.");
                    }
                }
            }
        }
    }
    private static string Caption(DRPGTurnEntryPresentationProbe.Fixture f)
    {
        var numbers = f.Visuals.ActiveNumbers();
        return numbers.Length == 0 ? string.Empty : numbers[0].GetComponent<TMPro.TMP_Text>().text;
    }
    private static void Check(bool value, int index, string detail)
    { if (!value) throw new InvalidOperationException("TurnEntry real-frame case " + index + ": " + detail); }
}
#endif
