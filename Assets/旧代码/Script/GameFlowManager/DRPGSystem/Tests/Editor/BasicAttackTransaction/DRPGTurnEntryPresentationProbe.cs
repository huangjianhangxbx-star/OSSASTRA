#if UNITY_EDITOR
using System;
using UnityEngine;

internal static class DRPGTurnEntryPresentationProbe
{
    // Break caught: the shared production presenter requires an ActionId or leaks final canonical HP before each cursor.
    internal static void CandidatePresenterRevealsActualNumbersAndHpOneStepAtATime()
    {
        using (var f = new Fixture())
        using (var sequence = f.Start())
        {
            Check(sequence.Fault.Length == 0, sequence.Fault);
            Check(f.Domain.Player.CurrentHp == 93 && f.PresentedHp == 97 && f.Visuals.ActiveNumbers().Length == 1,
                "Candidate did not reveal exactly the first real status feedback.");
            f.Domain.Surface.RenderCombatants(DRPGBattlePresentationSnapshot.Capture(f.Domain.Session));
            Check(f.PresentedHp == 97 && f.Domain.Pool.ActiveCount == 2, "Canonical refresh exposed future status HP.");
            f.Step(sequence, .449);
            Check(f.PresentedHp == 97 && !sequence.ReadyForDefeatFinalization, "Real candidate presenter skipped its hold.");
            f.Step(sequence, .002);
            Check(f.PresentedHp == 93 && f.Visuals.ActiveNumbers().Length == 1, "Second feedback overlapped or failed to advance HP.");
            f.Step(sequence, .451);
            Check(sequence.ReadyForDefeatFinalization && f.Completed == 0 && f.RuleLease.IsActive,
                "Leaf candidate presenter advanced the battle or released ownership.");
        }
    }

    internal static void LethalPlayerRetainsXAndDimUntilFreshHomeThenIncapacity() => Lethal(false);
    internal static void LethalEnemyRetainsXAndDimUntilFreshHomeThenDeparture() => Lethal(true);
    private static void Lethal(bool enemy)
    {
        using (var f = new Fixture(enemy, true))
        using (var sequence = f.Start())
        {
            Check(sequence.Fault.Length == 0, sequence.Fault);
            Check(f.Candidate.CurrentHp == 0 && f.Candidate.IsDeployed && f.PresentedHp == 0 &&
                f.Visuals.ActiveDefeatMarkers().Length == 1 && f.CandidateView.gameObject.activeInHierarchy && f.Body.color.r <= .401f &&
                f.CandidateView.transform.position == f.Home, "Candidate death did not retain its same home/view with X and dim.");
            f.Step(sequence, .451);
            Check(sequence.ReadyForDefeatFinalization && f.Domain.Pool.ActiveCount == 2, "Candidate vanished before its death barrier.");
            var facts = DRPGSettlementDepartureFacts.Describe(f.Ledger);
            Check(facts.Owner == f.RuleLease.Owner && facts.Stage == DRPGBattleActionStage.TurnBegin && facts.Members.Count == 1,
                "TurnEntry death facts borrowed RootAction identity or stage9 provenance.");
            var departure = f.Presenter.CompleteDefeats(facts, f.Defeated);
            f.Presenter.Tick();
            Check(!departure.Receipt.Completion.IsCompleted && f.Domain.Pool.ActiveCount == 2 && f.Visuals.ActiveDefeatMarkers().Length == 1,
                "Old status frames were reused as the fresh home-rendered permit.");
            f.Clock.Now += .01; f.Clock.RenderedFrame++; f.Presenter.Tick();
            Check(f.Domain.Pool.ActiveCount == 1 && !departure.Receipt.Completion.IsCompleted,
                "Home-rendered barrier failed to transfer once or skipped post-transfer rendering.");
            f.Clock.Now += .01; f.Clock.RenderedFrame++; f.Presenter.Tick();
            Check(departure.Receipt.Completion.IsCompleted && departure.Receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed &&
                departure.Views.Count == 1 && departure.Views[0].OriginalViewInstanceId == f.ViewId &&
                (enemy ? departure.Views[0].IncapacitatedModelInstanceId == 0 :
                    f.Defeated.IsVisible(new ParticipantId(f.Candidate.RuntimeId), departure.Views[0].IncapacitatedModelInstanceId)) &&
                f.Completed == 0 && f.RuleLease.IsActive && f.Candidate.IsDeployed,
                "Typed candidate transfer lost view provenance, visible incap or the still-locked rule owner.");
        }
    }

    // Break caught: comparing default ActionIdentity accepts a different candidate's ledger under the pinned views.
    internal static void SameGenerationForeignCandidateCannotOwnThePresenter()
    {
        using (var f = new Fixture(createPresenter: false))
        {
            f.Projection.Dispose();
            var other = BattleTransactionIdentity.ForTurnEntry(new TurnEntryIdentity(f.Entry.BattleGeneration, f.Entry.CandidateSerial + 1, f.Entry.Actor));
            using (var projection = f.Domain.Surface.AcquireProjectionLease(other))
            {
                bool rejected = false;
                try { using (var presenter = new BattleSettlementStepPresenter(new DRPGSettlementPresentationPlan(f.Ledger),
                    projection, f.Visuals.Feedback, f.Visuals.Camera, f.Clock)) { } }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "Different candidate with the same empty ActionIdentity acquired the presenter.");
            }
        }
    }

    internal static void ClosingCandidateProjectionCancelsItsRealFeedback()
    {
        using (var f = new Fixture())
        using (var sequence = f.Start())
        {
            Check(sequence.Fault.Length == 0 && f.Visuals.ActiveNumbers().Length == 1, sequence.Fault);
            f.Projection.Dispose();
            Check(f.Visuals.ActiveNumbers().Length == 0 && f.Completed == 0 && f.RuleLease.IsActive,
                "Candidate label survived its scope or presentation disposal released domain input.");
        }
    }

    // Tests the actual shared presenter, projection guard, pool and defeat layer.
    // The existing Session fixture supplies a battlefield, NOT the not-yet-wired TurnEntry orchestration.
    internal sealed class Fixture : IDisposable
    {
        internal readonly DRPGBasicAttackProjectionProbe.Fixture Domain;
        internal readonly DRPGSettlementPresentationProbe.FixtureClock Clock = new DRPGSettlementPresentationProbe.FixtureClock();
        internal readonly BattleParticipantRuntime Candidate;
        internal readonly TurnEntryIdentity Entry;
        internal readonly BattleRuleMutationLease RuleLease;
        internal readonly DRPGBattleActionProjectionLease Projection;
        internal readonly DRPGBattleSettlementLedger Ledger;
        internal readonly DRPGSettlementPresentationPlan Plan;
        internal readonly System.Collections.Generic.IReadOnlyDictionary<ParticipantId, int> HomeSlots;
        internal readonly DRPGTurnCandidateReservation Reservation;
        internal readonly DRPGBasicAttackHitOutcomeProbe.Visuals Visuals;
        internal readonly DRPGSettlementWidgetsProbe.Surfaces Widgets;
        internal readonly BattleSettlementStepPresenter Presenter;
        internal readonly BattleDefeatedActorRoster Defeated;
        internal readonly BattleWorldActorView CandidateView;
        internal readonly SpriteRenderer Body;
        internal readonly Vector3 Home;
        internal readonly int ViewId;
        internal int Completed;
        private readonly Texture2D texture;
        private readonly Sprite sprite;
        internal float PresentedHp => Projection.GetPresented(Candidate.RuntimeId).Hp;
        internal Fixture(bool enemy = false, bool lethal = false, bool createPresenter = true, bool statuses = true,
            bool fullBattlefield = false, bool deferToModule = false, int lifecycle = 0, bool widgets = false)
        {
            try
            {
                Domain = new DRPGBasicAttackProjectionProbe.Fixture(false, fullBattlefield);
                Candidate = enemy ? Domain.Enemy : Domain.Player;
                var state = Domain.Session.TransactionState; var arbiter = Domain.Session.TransactionArbiter;
                var setupOwner = BattleTransactionIdentity.ForTestHarness(new BattleStartIdentity(Domain.Session.BattleGeneration, 501));
                Check(arbiter.TryAcquire(setupOwner, out var setup, out var error), error);
                using (setup)
                {
                    var draft = state.CreateWorkspace();
                    if (deferToModule)
                    {
                        float time = draft.CurrentLogicalTime + 10f;
                        foreach (var participant in Domain.Session.Participants)
                            if (participant.IsAlive && participant.IsDeployed)
                                draft.ScheduleAt(new ParticipantId(participant.RuntimeId), time + (participant == Candidate ? 0f : 1f),
                                    participant == Candidate ? -1 : 0);
                        Reservation = new DRPGTurnCandidateReservation(1, DRPGTurnCandidateSourceKind.SchedulerTimeline,
                            new DRPGTurnScheduleEntry(Candidate, time, -1), time);
                    }
                    if (fullBattlefield)
                        foreach (var participant in Domain.Session.Participants)
                            draft.For(new ParticipantId(participant.RuntimeId)).SetFormationRow(
                                participant.RuntimeId == 7111 || participant.RuntimeId == 7121 ? DRPGFormationRow.Middle :
                                participant.RuntimeId == 7112 || participant.RuntimeId == 7122 ? DRPGFormationRow.Rear : DRPGFormationRow.Front);
                    if (statuses) AddStatus(draft, Candidate.RuntimeId, "a-turn-entry", lethal ? -100 : -3);
                    if (statuses && !lethal) AddStatus(draft, Candidate.RuntimeId, "z-turn-entry", -4);
                    if (lifecycle != 0)
                    {
                        var candidate = draft.For(new ParticipantId(Candidate.RuntimeId));
                        candidate.SetPosture(lifecycle == 1 ? candidate.Snapshot.MaximumPosture * .4f : 0f);
                        if (lifecycle == 3) candidate.ResolveTurnEntryLifecycle();
                    }
                    Check(state.TryCommitJournal(setup, draft).Succeeded, "Could not seed visible candidate statuses.");
                }
                Entry = new TurnEntryIdentity(Domain.Session.BattleGeneration, Domain.Session.CurrentActorTurnToken.TurnSerial + 1,
                    new ParticipantId(Candidate.RuntimeId));
                Check(arbiter.TryAcquire(BattleTransactionIdentity.ForTurnEntry(Entry), out RuleLease, out error), error);
                Domain.Bridge.FlushQueuedSignals();
                DRPGBasicAttackDefeatTransferProbe.Fixture.ConfigureWorldOrigins(Domain, RuleLease);
                texture = new Texture2D(4, 4); var pixels = new Color[16]; Array.Fill(pixels, Color.white); texture.SetPixels(pixels); texture.Apply();
                sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(.5f, 0));
                foreach (var view in Domain.Roster.ActiveViews) view.VisualRoot.gameObject.AddComponent<SpriteRenderer>().sprite = sprite;
                Check(Domain.Pool.TryGet(Candidate.RuntimeId, out CandidateView), "Candidate view missing.");
                Body = CandidateView.GetComponentInChildren<SpriteRenderer>(); Home = CandidateView.transform.position; ViewId = CandidateView.GetInstanceID();
                Visuals = new DRPGBasicAttackHitOutcomeProbe.Visuals(Domain.Root, Clock);
                var template = new GameObject("TurnEntryIncapTemplate", typeof(SpriteRenderer));
                template.transform.SetParent(Domain.Root.transform, false); template.GetComponent<SpriteRenderer>().sprite = sprite; template.SetActive(false);
                var anchor = new GameObject("TurnEntryIncapAnchor").transform;
                anchor.SetParent(Domain.Root.transform, false); anchor.position = Home;
                Defeated = Domain.Root.AddComponent<BattleDefeatedActorRoster>();
                Defeated.Configure(new[] { new BattleIncapacitationBinding(new ParticipantId(Candidate.RuntimeId), template, anchor) });
                if (widgets) Widgets = new DRPGSettlementWidgetsProbe.Surfaces(Domain);
                Projection = Domain.Surface.AcquireProjectionLease(RuleLease.Owner);
                var homes = new System.Collections.Generic.Dictionary<ParticipantId, int>();
                foreach (var view in Domain.Roster.ActiveViews)
                    homes.Add(new ParticipantId(view.RuntimeId), view.RuntimeId == 7111 || view.RuntimeId == 7121 ? 1 :
                        view.RuntimeId == 7112 || view.RuntimeId == 7122 ? 2 : 0);
                HomeSlots = homes;
                if (!deferToModule)
                {
                    Check(state.TryCommitSettlement(RuleLease, Entry.Actor, homes, out Ledger, out error), error);
                    Plan = new DRPGSettlementPresentationPlan(Ledger);
                    if (createPresenter) Presenter = new BattleSettlementStepPresenter(Plan,
                        Projection, Visuals.Feedback, Visuals.Camera, Clock);
                }
                Domain.Session.ActionCompleted += _ => Completed++;
            }
            catch { Dispose(); throw; }
        }
        internal DRPGBattleSettlementSequence Start() => new DRPGBattleSettlementSequence(Plan, Presenter, Clock, RuleLease);
        internal void Step(DRPGBattleSettlementSequence sequence, double delta)
        { Clock.Now += delta; Clock.RenderedFrame++; sequence.Tick(); Check(sequence.Fault.Length == 0, sequence.Fault); }
        private static void AddStatus(DRPGBattleMutationWorkspace draft, int actor, string id, float hp)
        {
            var asset = ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>();
            try
            {
                DRPGBasicAttackPreludeProbe.Set(asset, "statusId", id); DRPGBasicAttackPreludeProbe.Set(asset, "displayName", id);
                DRPGBasicAttackPreludeProbe.Set(asset, "triggerTiming", DRPGStatusTriggerTiming.TurnStart);
                DRPGBasicAttackPreludeProbe.Set(asset, "turnStartHpDelta", hp); DRPGBasicAttackPreludeProbe.Set(asset, "durationTurns", 2);
                draft.For(new ParticipantId(actor)).ApplyStatus(DRPGResolvedStatusSpec.From(asset), 1, 0, new ParticipantId(actor));
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
        }
        public void Dispose()
        {
            Presenter?.Dispose(); Projection?.Dispose(); RuleLease?.Dispose(); Domain?.Dispose();
            if (sprite != null) UnityEngine.Object.DestroyImmediate(sprite);
            if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
        }
    }
    private static void Check(bool value, string detail) { if (!value) throw new InvalidOperationException(detail); }
}
#endif
