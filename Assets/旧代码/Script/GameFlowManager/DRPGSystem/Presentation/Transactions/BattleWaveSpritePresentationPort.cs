using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

internal readonly struct BattleWaveSpriteEntry
{
    internal BattleWaveSpriteEntry(DRPGBattleParticipantViewModel actor, Vector3 home, Vector3 scale, int sortingOrder = 0)
    { Actor = actor; Home = home; Scale = scale; SortingOrder = sortingOrder; }
    internal int SortingOrder { get; }
    internal DRPGBattleParticipantViewModel Actor { get; }
    internal Vector3 Home { get; }
    internal Vector3 Scale { get; }
}

internal sealed class BattleWaveSpritePresentationPort : IWaveTransitionPresentationPort, IDisposable
{
    private readonly DRPGBattleActionProjectionLease lease;
    private readonly BattleWorldActorPool pool;
    private readonly BattleActionAnnouncementPresenter announcement;
    private readonly IDRPGBattlePresentationClock clock;
    private readonly BattleWaveSpriteEntry[] entries;
    private readonly float entrySeconds, entryDistance;
    private readonly List<BattleWorldActorView> views = new List<BattleWorldActorView>();
    private BattleWaveActorStaging staging;
    private PresentationReceipt announced, staged, ready;
    private PresentationCleanupReceipt retraction;
    private PresentationReceipt[] members;
    private long[] homeFrames;
    private double startedAt, stagger, lastTime;
    private bool disposed, normalClosed, stagingCall;

    internal BattleWaveSpritePresentationPort(DRPGBattleActionProjectionLease lease, BattleWorldActorPool pool,
        BattleActionAnnouncementPresenter announcement, IDRPGBattlePresentationClock clock,
        IReadOnlyList<BattleWaveSpriteEntry> entries, float entrySeconds, float entryDistance)
    {
        this.lease = lease ?? throw new ArgumentNullException(nameof(lease));
        this.pool = pool != null ? pool : throw new ArgumentNullException(nameof(pool));
        this.announcement = announcement ?? throw new ArgumentNullException(nameof(announcement));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        if (lease.Owner.Kind != BattleTransactionKind.WaveTransition || entries == null || entries.Count == 0 ||
            !Finite(entrySeconds) || entrySeconds <= 0 || !Finite(entryDistance) || entryDistance <= 0 || !Finite(clock.Now))
            throw new ArgumentException("Wave presentation requires a frozen batch and finite entry timing.");
        this.entries = new BattleWaveSpriteEntry[entries.Count];
        var ids = new HashSet<int>();
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.Actor == null || !e.Actor.IsEnemy || !ids.Add(e.Actor.RuntimeId) || !Finite(e.Home) ||
                !Finite(e.Scale) || e.Scale.x <= 0 || e.Scale.y <= 0 || e.Scale.z <= 0)
                throw new ArgumentException("Wave member identity or frozen slot is invalid.");
            for (int j = 0; j < i; j++) if (this.entries[j].Home == e.Home)
                throw new ArgumentException("Wave members cannot share a home slot.");
            this.entries[i] = e;
        }
        this.entrySeconds = entrySeconds; this.entryDistance = entryDistance; lastTime = clock.Now;
        if (!lease.TryClaimMotion(this)) throw new InvalidOperationException("Wave motion already owned or projection lost.");
    }

    public PresentationReceipt AnnounceWave(in WaveAnnouncementCommand command)
    {
        RequireOwner(command.Identity);
        if (normalClosed) throw new InvalidOperationException("Wave is already closed.");
        if (announced != null) return announced;
        return announced = announcement.Present(command);
    }

    public WaveStagingReceipt StageBatch(in WaveBatchStagingCommand command)
    {
        RequireOwner(command.Identity);
        if (normalClosed || stagingCall || !Good(announced)) throw new InvalidOperationException("Wave announcement has not completed.");
        if (command.Members == null || command.MemberReady == null || command.Members.Count != entries.Length ||
            command.MemberReady.Count != entries.Length || !Finite(command.StaggerMaximum) || command.StaggerMaximum < 0 || command.StaggerMaximum > .5)
            throw new ArgumentException("Wave staging must match the entire frozen batch.");
        for (int i = 0; i < entries.Length; i++)
        {
            var receipt = command.MemberReady[i];
            if (command.Members[i].Value != entries[i].Actor.RuntimeId || receipt == null || receipt.Owner != lease.Owner ||
                receipt.Stage != DRPGBattleActionStage.TurnBegin || receipt.StepOrdinal != i + 1 ||
                (members != null ? !ReferenceEquals(members[i], receipt) : receipt.Completion.IsCompleted))
                throw new ArgumentException("Wave member readiness identity changed.");
        }
        if (staged != null) return new WaveStagingReceipt(staged);
        stagingCall = true;
        try
        {
            members = new PresentationReceipt[entries.Length]; homeFrames = new long[entries.Length];
            for (int i = 0; i < entries.Length; i++) { members[i] = command.MemberReady[i]; homeFrames[i] = -1; }
            staged = Receipt(DRPGBattleActionStage.ConflictComposition);
            startedAt = clock.Now; stagger = command.StaggerMaximum;
            staging = new BattleWaveActorStaging(pool, lease);
            for (int i = 0; i < entries.Length; i++)
            {
                var view = staging.Stage(entries[i].Actor); views.Add(view);
                if (!HasSprite(view)) throw new InvalidOperationException("Wave actor has no ready Sprite visual.");
                view.transform.localScale = entries[i].Scale;
                var renderers = view.GetComponentsInChildren<SpriteRenderer>(true);
                for (int r = 0; r < renderers.Length; r++) renderers[r].sortingOrder = entries[i].SortingOrder + r;
                // In-place waves are born at their final Home Slot. Readiness
                // still waits for rendered frames before scheduler adoption.
                view.transform.position = entries[i].Home;
                homeFrames[i] = clock.RenderedFrame;
                if (disposed || !lease.OwnsMotion(this)) throw new InvalidOperationException("Wave cancelled while staging.");
            }
            Complete(staged, PresentationReceiptTerminalKind.Completed, "Whole wave staged.");
            return new WaveStagingReceipt(staged);
        }
        catch { Dispose(); throw; }
        finally { stagingCall = false; if (disposed) Dispose(); }
    }

    public PresentationReceipt AwaitBatchReady(in WaveBatchReadyCommand command)
    {
        RequireOwner(command.Identity);
        if (!Good(staged) || normalClosed) throw new InvalidOperationException("Wave batch was not staged.");
        if (ready == null) ready = Receipt(DRPGBattleActionStage.TurnBegin);
        return ready;
    }

    public void Tick()
    {
        if (disposed) return;
        if (!Finite(clock.Now) || clock.Now < lastTime || !lease.OwnsMotion(this))
        { Dispose(); throw new InvalidOperationException("Wave clock or projection ownership lost."); }
        lastTime = clock.Now; announcement.Tick();
        if (normalClosed)
        {
            if (!HomesIntact())
            { Dispose(); throw new InvalidOperationException("Ready wave actor or home lost before deployment handoff."); }
            return;
        }
        if (!Good(staged) || stagingCall) return;
        bool allReady = true;
        for (int i = 0; i < views.Count; i++)
        {
            var view = views[i];
            if (view == null || view.RuntimeId != entries[i].Actor.RuntimeId || !view.gameObject.activeInHierarchy ||
                !HasSprite(view))
            { Dispose(); throw new InvalidOperationException("Wave entry actor or owned tween lost."); }
            if (view.transform.position == entries[i].Home && view.transform.localScale == entries[i].Scale)
            {
                if (clock.RenderedFrame - homeFrames[i] >= 2)
                    Complete(members[i], PresentationReceiptTerminalKind.Completed, "Wave actor rendered at home.");
            }
            if (!Good(members[i])) allReady = false;
        }
        if (allReady && ready != null) Complete(ready, PresentationReceiptTerminalKind.Completed, "Whole wave ready.");
    }

    public PresentationCleanupReceipt CloseWave(WaveTransitionIdentity identity, bool retractActors)
    {
        if (identity != lease.Owner.WaveTransition) throw new InvalidOperationException("Foreign wave cleanup.");
        if (retractActors)
        {
            if (retraction == null) retraction = new PresentationCleanupReceipt();
            Dispose(); return retraction;
        }
        var result = new PresentationCleanupReceipt();
        RequireOwner(identity);
        if (!Good(ready) || !HomesIntact()) { result.TryComplete(false); return result; }
        normalClosed = true; KillMotion(); announcement.Cancel(announced);
        result.TryComplete(true); return result;
    }

    internal bool TryAdoptDeployed() => !disposed && normalClosed && lease.OwnsMotion(this) && HomesIntact() && staging != null && staging.TryAdoptDeployed();

    public void Dispose()
    {
        disposed = true;
        if (stagingCall) return;
        KillMotion(); announcement.Cancel(announced);
        Complete(announced, PresentationReceiptTerminalKind.Faulted, "Wave presentation closed.");
        Complete(staged, PresentationReceiptTerminalKind.Faulted, "Wave presentation closed.");
        Complete(ready, PresentationReceiptTerminalKind.Faulted, "Wave presentation closed.");
        if (members != null) foreach (var member in members) Complete(member, PresentationReceiptTerminalKind.Faulted, "Wave entry cancelled.");
        staging?.Dispose();
        retraction?.TryComplete(true);
    }
    private bool HomesIntact()
    {
        if (views.Count != entries.Length) return false;
        for (int i = 0; i < views.Count; i++)
        {
            var view = views[i];
            if (view == null || !view.gameObject.activeInHierarchy || view.RuntimeId != entries[i].Actor.RuntimeId ||
                view.BoundActor == null || !ReferenceEquals(view.BoundActor.Participant, entries[i].Actor.Participant) ||
                view.transform.position != entries[i].Home || view.transform.localScale != entries[i].Scale || !HasSprite(view)) return false;
        }
        return true;
    }
    private void RequireOwner(WaveTransitionIdentity identity)
    {
        if (disposed || identity != lease.Owner.WaveTransition || !lease.OwnsMotion(this))
            throw new InvalidOperationException("Wave presentation identity is not active.");
    }
    private void KillMotion() { }
    private PresentationReceipt Receipt(DRPGBattleActionStage stage) => new PresentationReceipt(lease.Owner, stage);
    private static void Complete(PresentationReceipt receipt, PresentationReceiptTerminalKind kind, string detail) =>
        receipt?.TryComplete(new PresentationReceiptTerminal(kind, detail));
    private static bool Good(PresentationReceipt receipt) => receipt != null && receipt.Completion.IsCompleted &&
        receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed;
    private static bool HasSprite(BattleWorldActorView view)
    {
        foreach (var renderer in view.GetComponentsInChildren<SpriteRenderer>(true))
            if (renderer.enabled && renderer.gameObject.activeInHierarchy && renderer.sprite != null) return true;
        return false;
    }
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
}
