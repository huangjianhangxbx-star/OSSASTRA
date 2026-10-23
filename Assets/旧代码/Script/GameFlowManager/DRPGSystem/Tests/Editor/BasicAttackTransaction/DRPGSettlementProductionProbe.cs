#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Existing real Rule/Adapter/Lease fixture prepares Stage3-8 deterministically;
// Stage9 itself uses Unity's unscaled clock and actual EndOfFrame observations.
// These supplemental cases do not enable F8 or claim production asset coverage.
public static class DRPGSettlementProductionProbe
{
    public static string Status { get; private set; } = "NotRun";
    public static string StartProduction()
    {
        if (!Application.isPlaying || Status == "Running") throw new InvalidOperationException("A fresh real Play Mode run is required.");
        Status = "Running";
        new GameObject("SettlementRealFrameProbeHost").AddComponent<Host>().Begin(); return Status;
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
                if (!next) { Status = "PASS: 14 real-frame cases: 8 Stage9 cases + 4 full Root Stage3-9 pipelines + 2 serial status-widget cases"; break; }
                yield return work.Current;
            }
            (work as IDisposable)?.Dispose(); work = null; Destroy(gameObject);
        }
        private void OnDestroy() { (work as IDisposable)?.Dispose(); work = null; }
    }
    private static IEnumerator Run()
    {
        // Columns: statuses, lethal party, lethal enemy, full field, last word.
        var cases = new[] {
            new[] { false, false, false, false, false },
            new[] { true, false, false, false, false },
            new[] { true, true, false, false, false },
            new[] { true, false, true, false, false },
            new[] { true, true, true, false, false },
            new[] { true, true, true, true, false },
            new[] { true, true, false, false, true },
            new[] { true, false, true, true, false }
        };
        for (int index = 0; index < cases.Length; index++)
        {
            var c = cases[index];
            using (var f = new DRPGSettlementPresentationProbe.Fixture(c[0], c[1], c[2], fullBattlefield: c[3], lastWord: c[4]))
            {
                var clock = f.Domain.Root.AddComponent<UnityRenderFrameFence>(); f.Clock.UseLiveClock(clock);
                var actor = f.Domain.Session.CurrentActor;
                var originalViews = new Dictionary<int, int>();
                foreach (var view in f.Domain.Roster.ActiveViews) originalViews.Add(view.RuntimeId, view.GetInstanceID());
                double deadline = clock.Now + 20d, began = clock.Now, labelBegan = began;
                long firstFrame = clock.RenderedFrame;
                using (var sequence = f.Start())
                {
                    string labelId = NumberKey(f); int visibleItems = labelId.Length == 0 ? 0 : 1;
                    int expectedItems = !c[0] ? 0 : c[1] ? 2 : 3;
                    while (!sequence.ReadyForDefeatFinalization && clock.Now < deadline)
                    {
                        yield return null; sequence.Tick(); f.Domain.Bridge.FlushQueuedSignals();
                        Check(sequence.Fault.Length == 0, index, "Settlement sequence: " + sequence.Fault);
                        Check(f.Visuals.ActiveNumbers().Length <= 1 && f.Domain.Pool.ReleaseCount == 0 && f.Defeated.Count == 0,
                            index, "Parallel labels or departure before the complete item queue.");
                        foreach (var view in f.Domain.Roster.ActiveViews)
                            Check(view.GetInstanceID() == originalViews[view.RuntimeId] && view.gameObject.activeInHierarchy,
                                index, "Bridge flush replaced or hid a pinned settlement actor.");
                        string next = NumberKey(f);
                        if (next != labelId)
                        {
                            if (labelId.Length != 0) Check(clock.Now - labelBegan >= .45d, index, "Real feedback disappeared before .45s.");
                            if (next.Length != 0) { visibleItems++; labelBegan = clock.Now; }
                            labelId = next;
                        }
                        Check(f.Completed == 0 && ReferenceEquals(actor, f.Domain.Session.CurrentActor) && f.RuleLease.IsActive,
                            index, "Step presentation advanced or unlocked the action.");
                    }
                    Check(sequence.ReadyForDefeatFinalization && visibleItems == expectedItems && clock.RenderedFrame > firstFrame &&
                        clock.Now - began >= (c[0] ? expectedItems * .45d : .30d), index, "Incomplete or shortened real settlement queue: ready=" +
                        sequence.ReadyForDefeatFinalization + "; items=" + visibleItems + "/" + expectedItems + "; elapsed=" + (clock.Now - began));
                    Check(f.Prepared.TryDescribeSettlementDepartures(out var facts, out var error), index, error);
                    int deathCount = (c[1] ? 1 : 0) + (c[2] || c[4] ? 1 : 0);
                    Check(facts.Members.Count == deathCount && f.Visuals.ActiveDefeatMarkers().Length == deathCount,
                        index, "Death cursor lost an X or typed causal member.");
                    long homeFrame = clock.RenderedFrame;
                    var departure = f.Adapter.CompleteSettlementDefeats(facts);
                    while (!departure.Receipt.Completion.IsCompleted && clock.Now < deadline)
                    {
                        yield return null; f.Adapter.Tick(); f.Domain.Bridge.FlushQueuedSignals();
                        if (f.Domain.Pool.ReleaseCount != 0)
                            Check(clock.RenderedFrame > homeFrame, index, "Real departure reused an old frame fence.");
                    }
                    Check(Completed(departure.Receipt) && f.Domain.Pool.ReleaseCount == deathCount && departure.Views.Count == deathCount &&
                        f.Defeated.Count == (c[1] ? 1 : 0), index, "Typed real-frame death transfer did not complete.");
                    Check(f.Prepared.TryReconcileFormationAfterSettlement(departure, out var rules, out error), index, error);
                    double formationAt = clock.Now;
                    var formation = f.Adapter.ReconcileSettlementFormation(rules);
                    while (!formation.Completion.IsCompleted && clock.Now < deadline)
                    {
                        yield return null; f.Adapter.Tick();
                        var views = f.Domain.Roster.ActiveViews;
                        for (int a = 0; a < views.Count; a++)
                        for (int b = a + 1; b < views.Count; b++) Check(Vector3.Distance(views[a].transform.position, views[b].transform.position) > .001f,
                            index, "Real-frame survivor formation overlapped.");
                    }
                    Check(Completed(formation) && (!c[3] || deathCount == 0 || clock.Now - formationAt >= .30d) &&
                        f.Projection.IsActive && f.RuleLease.IsActive && f.Completed == 0 && ReferenceEquals(actor, f.Domain.Session.CurrentActor),
                        index, "Final formation skipped its movement or advanced Root.");
                }
            }
            yield return null;
        }
        var roots = DRPGBasicAttackRootSettlementProbe.ProductionRootSettlement();
        try { while (roots.MoveNext()) yield return roots.Current; }
        finally { (roots as IDisposable)?.Dispose(); }
        var widgets = DRPGSettlementWidgetsProbe.ProductionWidgets();
        try { while (widgets.MoveNext()) yield return widgets.Current; }
        finally { (widgets as IDisposable)?.Dispose(); }
    }
    private static string NumberKey(DRPGSettlementPresentationProbe.Fixture f)
    {
        // The real number pool intentionally reuses one GameObject between
        // items; these fixtures use distinct visible labels, not distinct views.
        var numbers = f.Visuals.ActiveNumbers();
        return numbers.Length == 1 ? numbers[0].GetComponent<TMPro.TMP_Text>().text : string.Empty;
    }
    private static bool Completed(PresentationReceipt receipt) => receipt.Completion.IsCompleted &&
        receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed;
    private static void Check(bool value, int index, string detail)
    { if (!value) throw new InvalidOperationException("Stage9 case " + index + ": " + detail); }
}
#endif
