#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEngine;

internal static class DRPGWaveSpritePresentationProbe
{
    internal static void ReentrantCleanupAcknowledgesOnlyAfterBindingUnwinds()
    {
        using (var f = new Fixture())
        {
            var template = new GameObject("WaveCancelDuringBind", typeof(BattleWorldActorView));
            try
            {
                var view = template.GetComponent<BattleWorldActorView>();
                view.ConfigureVisualDriver(template.AddComponent<WaveStagingCancellationDriver>());
                f.Pool.Configure(view, f.Pool.transform);
                PresentationCleanupReceipt cleanup = null; bool early = false;
                WaveStagingCancellationDriver.OnBind = () =>
                {
                    cleanup = f.Port.CloseWave(f.Identity, true);
                    early = cleanup.Completion.IsCompleted;
                };
                f.Port.AnnounceWave(new WaveAnnouncementCommand(f.Identity, .75)); f.Step(.8, 2);
                try { f.Port.StageBatch(f.Command); } catch (InvalidOperationException) { }
                Check(!early && cleanup != null && cleanup.Completion.IsCompleted && cleanup.Completion.Result && f.Views.Length == 0,
                    "Reentrant cleanup acknowledged before actor binding and recycling had finished.");
            }
            finally { WaveStagingCancellationDriver.OnBind = null; UnityEngine.Object.DestroyImmediate(template); }
        }
    }

    internal static void ReadyPoseDriftIsNotAcceptedAsADeploymentHome()
    {
        using (var f = new Fixture())
        {
            f.MakeReady(); f.Port.CloseWave(f.Identity, false);
            f.Views[0].transform.position += Vector3.right;
            bool rejected = false;
            try { f.Step(0, 1); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected && !f.Port.TryAdoptDeployed(), "Post-ready external displacement bypassed the home barrier.");
        }
    }

    internal static void RuleRunWithActualSpritePortCommitsOnlyAfterCleanup()
    {
        using (var f = new Fixture())
        {
            var run = f.MakeRun(); run.Tick();
            f.RunStep(run, .8, 2); f.RunStep(run, .2, 1); f.RunStep(run, 1, 1); f.RunStep(run, 0, 2);
            Check(!run.CompletionSubmitted && f.Inbox.PendingCount == 0 && f.Actors.All(x => !x.IsDeployed) && f.Pool.ActiveCount == 2,
                "Actual Sprite readiness bypassed the normal cleanup commit boundary.");
            var before = f.Views;
            f.RunStep(run, 0, 1);
            Check(run.CompletionSubmitted && f.Inbox.PendingCount == 1 && f.Actors.All(x => x.IsDeployed) &&
                f.Port.TryAdoptDeployed() && f.Pool.ActiveCount == 4 && f.Views.SequenceEqual(before),
                "Actual Sprite port did not hand the atomically deployed batch to the original pool actors.");
            f.RunStep(run, 1, 3); Check(f.Inbox.PendingCount == 1, "Sprite run resubmitted its completion.");
        }
    }

    internal static void ActorLossAtCleanupPreventsRealRuleRunDeployment()
    {
        using (var f = new Fixture())
        {
            var run = f.MakeRun(); run.Tick();
            f.RunStep(run, .8, 2); f.RunStep(run, .2, 1); f.RunStep(run, 1, 1); f.RunStep(run, 0, 2);
            UnityEngine.Object.DestroyImmediate(f.Views[0].gameObject);
            f.RunStep(run, 0, 1); f.RunStep(run, 0, 1);
            Check(run.Fault.Length > 0 && run.FaultCleanupCompleted && !run.CompletionSubmitted &&
                f.Inbox.PendingCount == 0 && f.Actors.All(x => !x.IsDeployed) && f.Views.Length == 0,
                "Actor loss after ready committed or left a partial wave.");
        }
    }

    internal static void ReadyActorLostAfterNormalCloseCannotBeCommitted()
    {
        using (var f = new Fixture())
        {
            f.MakeReady(); f.Port.CloseWave(f.Identity, false);
            UnityEngine.Object.DestroyImmediate(f.Views[0].gameObject);
            bool rejected = false;
            try { f.Step(0, 1); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected && !f.Port.TryAdoptDeployed() && f.Pool.ActiveCount == 2,
                "Normal cleanup acknowledgement hid subsequent actor loss from the commit pump.");
        }
    }

    internal static void ForgedMembersCannotCompleteOrCancelTheRealBatch()
    {
        using (var f = new Fixture())
        {
            f.Port.AnnounceWave(new WaveAnnouncementCommand(f.Identity, .75)); f.Step(.8, 2);
            var foreign = new WaveTransitionIdentity(f.Identity.BattleGeneration, 2, 2);
            var forged = new WaveBatchStagingCommand(f.Identity, f.Command.Members,
                new[] { new PresentationReceipt(BattleTransactionIdentity.ForWave(foreign), DRPGBattleActionStage.TurnBegin, 1), f.Ready[1] }, .5);
            bool rejected = false;
            try { f.Port.StageBatch(forged); } catch (ArgumentException) { rejected = true; }
            Check(rejected && f.Views.Length == 0 && f.Ready.All(x => !x.Completion.IsCompleted), "Forged readiness mutated the real batch.");
            f.Port.StageBatch(f.Command);
            try { f.Port.CloseWave(foreign, true); rejected = false; } catch (InvalidOperationException) { }
            Check(rejected && f.Views.Length == 2, "Foreign cleanup retracted the owned wave.");
        }
    }

    internal static void StageCannotBypassAnnouncementAndDuplicateDoesNotRespawn()
    {
        using (var f = new Fixture())
        {
            bool rejected = false;
            try { f.Port.StageBatch(f.Command); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected && f.Views.Length == 0, "Wave bypassed its announcement.");
            f.Port.AnnounceWave(new WaveAnnouncementCommand(f.Identity, .75)); f.Step(.8, 2);
            var a = f.Port.StageBatch(f.Command); var views = f.Views;
            var b = f.Port.StageBatch(f.Command);
            Check(ReferenceEquals(a.Receipt, b.Receipt) && f.Views.SequenceEqual(views), "Duplicate staging regenerated actors.");
        }
    }

    internal static void AnnouncementThenSmoothEntryThenRenderedWholeBatch()
    {
        using (var f = new Fixture())
        {
            var label = f.Port.AnnounceWave(new WaveAnnouncementCommand(f.Identity, .75));
            Check(label != null && f.Group.alpha == 1 && f.Text.text.Contains("1"), "Wave announcement was not shown.");
            f.Step(.74, 2); Check(!label.Completion.IsCompleted && f.Views.Length == 0, "Announcement finished or staged early.");
            f.Step(.01, 1); Check(Good(label), "Readable announcement did not finish.");
            var staged = f.Port.StageBatch(f.Command);
            Check(staged != null && Good(staged.Receipt) && f.Views.Length == 2 && f.Pool.ActiveCount == 2,
                "Whole batch was not staged outside the active pool.");
            var views = f.Views; var first = views.Single(x => x.RuntimeId == 9901);
            var start = first.transform.position;
            var scale = first.transform.localScale;
            var ready = f.Port.AwaitBatchReady(new WaveBatchReadyCommand(f.Identity));
            f.Step(.175, 1);
            Check(first.transform.position.x < start.x && first.transform.position.x > 3 &&
                first.transform.position.y == 2 && first.transform.localScale == scale && !ready.Completion.IsCompleted,
                "Entry snapped, moved vertically, changed scale, or completed early.");
            f.Step(.7, 1); Check(!ready.Completion.IsCompleted, "Ready ignored rendered home frames.");
            f.Step(0, 2); Check(Good(ready) && f.Ready.All(Good), "Batch never reached rendered readiness.");
            var close = f.Port.CloseWave(f.Identity, false);
            Check(close != null && close.Completion.IsCompleted && close.Completion.Result &&
                !f.Port.TryAdoptDeployed() && f.Pool.ActiveCount == 2, "Normal close deployed rules or removed staged views.");
            f.Commit(); Check(f.Port.TryAdoptDeployed() && f.Pool.ActiveCount == 4 &&
                f.Pool.TryGet(9901, out var adopted) && ReferenceEquals(first, adopted), "Rule commit did not adopt original actors.");
        }
    }

    internal static void CancellationKillsMotionAndNeverCompletesReady()
    {
        using (var f = new Fixture())
        {
            f.Port.AnnounceWave(new WaveAnnouncementCommand(f.Identity, .75)); f.Step(.8, 2);
            f.Port.StageBatch(f.Command); var ready = f.Port.AwaitBatchReady(new WaveBatchReadyCommand(f.Identity));
            var views = f.Views; Check(views.Length == 2, "No actors entered before cancellation.");
            var closed = f.Port.CloseWave(f.Identity, true); f.Step(10, 10);
            Check(closed != null && closed.Completion.IsCompleted && closed.Completion.Result &&
                views.All(x => !x.gameObject.activeSelf && x.RuntimeId == 0) && !Good(ready) &&
                f.Ready.All(x => !Good(x)) && !f.Port.TryAdoptDeployed() && f.Group.alpha == 0,
                "Cancelled entry left actors, label, motion, or successful ready receipts.");
        }
    }

    private static bool Good(PresentationReceipt r) => r != null && r.Completion.IsCompleted &&
        r.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed;
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private sealed class Clock : IDRPGBattlePresentationClock
    { public double Now { get; set; } public long RenderedFrame { get; set; } }
    private sealed class Fixture : IDisposable
    {
        private readonly DRPGBasicAttackProjectionProbe.Fixture basis = new DRPGBasicAttackProjectionProbe.Fixture(false);
        private readonly Clock clock = new Clock();
        private readonly GameObject labelRoot = new GameObject("WaveLabelProbe", typeof(RectTransform));
        internal readonly CanvasGroup Group;
        internal readonly TMP_Text Text;
        internal readonly WaveTransitionIdentity Identity;
        internal readonly BattleWaveSpritePresentationPort Port;
        internal readonly PresentationReceipt[] Ready;
        internal readonly WaveBatchStagingCommand Command;
        private readonly DRPGBattleActionProjectionLease projection;
        private readonly DRPGBattleSessionState state;
        private readonly BattleRuleMutationLease rule;
        internal readonly BattleParticipantRuntime[] Actors;
        internal readonly DRPGBattleContinuationInbox Inbox;
        internal BattleWorldActorPool Pool => basis.Pool;
        internal BattleWorldActorView[] Views => Pool.GetComponentsInChildren<BattleWorldActorView>(true)
            .Where(x => x.RuntimeId >= 9901).OrderBy(x => x.RuntimeId).ToArray();
        internal Fixture()
        {
            var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<DRPGCombatantProfileSO>(
                "Assets/GameResources/DRPG/Config/Test/DRPGCombatant_ankalute_enemy_sentinel.asset");
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<BattleWorldActorView>(
                "Assets/GameResources/DRPG/Prefabs/PF_DRPGBattleWorldActor.prefab");
            Check(profile != null && prefab != null, "Formal Sprite resources missing.");
            Pool.Configure(prefab, Pool.transform);
            var actors = Enumerable.Range(0, 2).Select(i => new BattleParticipantRuntime(9901 + i,
                new BattleParticipantSpec(profile, DRPGBattleSide.Enemy, DRPGBattleRosterRole.Enemy, i, initiallyDeployed: false))).ToArray();
            Actors = actors;
            Identity = new WaveTransitionIdentity(basis.Session.BattleGeneration, 1, 1);
            var owner = BattleTransactionIdentity.ForWave(Identity);
            projection = basis.Surface.ProjectionArbiter.TryAcquire(owner).Lease;
            Check(projection != null, "Wave projection missing.");
            var arbiter = new DRPGBattleTransactionArbiter(owner.BattleGeneration,
                new DRPGBattleActionTraceWriter(new DRPGBoundedActionTraceSink(64)));
            var player = new BattleParticipantRuntime(9800, new BattleParticipantSpec(profile,
                DRPGBattleSide.Player, DRPGBattleRosterRole.Protagonist, 0));
            state = new DRPGBattleSessionState(owner.BattleGeneration, actors.Concat(new[] { player }).ToArray(), new DRPGTurnScheduler(), arbiter);
            Inbox = new DRPGBattleContinuationInbox(arbiter);
            Check(arbiter.TryAcquire(owner, out rule, out _), "Wave rule lease missing.");
            Group = labelRoot.AddComponent<CanvasGroup>(); Group.alpha = 0;
            Text = labelRoot.AddComponent<TextMeshProUGUI>();
            Ready = Enumerable.Range(1, 2).Select(i => new PresentationReceipt(owner, DRPGBattleActionStage.TurnBegin, i)).ToArray();
            Command = new WaveBatchStagingCommand(Identity, actors.Select(x => new ParticipantId(x.RuntimeId)).ToArray(), Ready, .5);
            Port = new BattleWaveSpritePresentationPort(projection, Pool,
                new BattleActionAnnouncementPresenter(Text, Group, clock), clock,
                actors.Select((x, i) => new BattleWaveSpriteEntry(new DRPGBattleParticipantViewModel(x, 0, false),
                    new Vector3(3 + i * 2, 2, 0), Vector3.one)).ToArray(), .35f, .8f);
        }
        internal void Step(double delta, long frames) { clock.Now += delta; clock.RenderedFrame += frames; Port.Tick(); }
        internal DRPGEnemyWavePresentationRun MakeRun()
        {
            var plan = DRPGEnemyWavePlan.Freeze(42, "sprite-wave-probe", Actors.Select(x => x.Spec).ToArray(), 2, 2, 3);
            return new DRPGEnemyWavePresentationRun(new DRPGEnemyWaveLifecycle(state, Inbox, plan, Actors), rule, Port,
                clock, new DRPGEnemyWavePolicySnapshot(2, 2, .75, .5, 3), 2, 0, 5);
        }
        internal void RunStep(DRPGEnemyWavePresentationRun run, double delta, long frames)
        { clock.Now += delta; clock.RenderedFrame += frames; run.Tick(); }
        internal void MakeReady()
        {
            Port.AnnounceWave(new WaveAnnouncementCommand(Identity, .75)); Step(.8, 2);
            Port.StageBatch(Command); Port.AwaitBatchReady(new WaveBatchReadyCommand(Identity));
            Step(1, 1); Step(0, 2);
        }
        internal void Commit()
        {
            var draft = state.CreateWorkspace();
            draft.For(new ParticipantId(9901)).SetDeployed(true); draft.For(new ParticipantId(9902)).SetDeployed(true);
            Check(state.TryCommitJournal(rule, draft).Succeeded, "Deployment commit failed.");
        }
        public void Dispose() { Port?.Dispose(); projection?.Dispose(); rule?.Dispose(); basis.Dispose(); UnityEngine.Object.DestroyImmediate(labelRoot); }
    }
}
#endif
