using System;
using System.Collections.Generic;

public sealed class BattlePresentationDirector : IBattlePresentationDirector
{
    private readonly BattlePresentationRecipeResolver resolver;
    private readonly BattlePresentationRecipeSetSO recipeSet;
    private readonly Dictionary<BattlePresentationCueKind,
        IBattlePresentationCueExecutor> executors;
    private readonly BattlePresentationTraceBuffer trace;
    private readonly List<RunState> activeRuns = new List<RunState>();
    private readonly Dictionary<BattlePresentationLaneKey, LaneState> lanes =
        new Dictionary<BattlePresentationLaneKey, LaneState>();
    private float realtimeNow;
    private long battleGeneration;

    public BattlePresentationDirector(
        BattlePresentationRecipeResolver resolver,
        BattlePresentationRecipeSetSO recipeSet,
        IEnumerable<IBattlePresentationCueExecutor> executors,
        BattleInputGate inputGate,
        BattlePresentationTraceBuffer trace)
    {
        this.resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        this.recipeSet = recipeSet ?? throw new ArgumentNullException(nameof(recipeSet));
        InputGate = inputGate ?? throw new ArgumentNullException(nameof(inputGate));
        this.trace = trace ?? throw new ArgumentNullException(nameof(trace));
        this.executors = BuildExecutorMap(executors);
    }

    public BattleInputGate InputGate { get; }
    public bool HasDanglingRuns => activeRuns.Count > 0;

    public BattlePresentationRun Play(BattlePresentationRequest request)
    {
        return Play(request, realtimeNow);
    }

    public BattlePresentationRun Play(
        BattlePresentationRequest request,
        float realtimeNow)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        this.realtimeNow = realtimeNow;
        if (request.Scope.BattleGeneration < battleGeneration)
        {
            return CreateSettledRun(
                request,
                BattlePresentationOutcome.Cancelled);
        }

        if (request.Scope.BattleGeneration > battleGeneration)
        {
            CancelOlderBattleGenerations(request.Scope.BattleGeneration);
            battleGeneration = request.Scope.BattleGeneration;
        }

        ResolvedBattlePresentationRecipe recipe;
        try
        {
            recipe = resolver.Resolve(request, recipeSet);
        }
        catch (Exception exception)
        {
            trace.Record(
                $"Request {request.RequestId} Faulted: {exception.Message}");
            return CreateSettledRun(request, BattlePresentationOutcome.Faulted);
        }

        if (!recipe.HasContent)
        {
            trace.Record($"Request {request.RequestId} Skipped: no recipe");
            return CreateSettledRun(request, BattlePresentationOutcome.Skipped);
        }

        var run = new BattlePresentationRun(request, recipe, OnRunCancelRequested);
        if (recipe.GateKind == BattlePresentationGateKind.Input)
            run.SetInputLease(InputGate.Acquire("request:" + request.RequestId));
        if (recipe.GateKind == BattlePresentationGateKind.None)
            run.ReleaseGate(BattlePresentationOutcome.Completed);

        var state = new RunState(run, realtimeNow);
        activeRuns.Add(state);
        run.WhenFinished(_ => activeRuns.Remove(state));
        trace.Record(
            $"Request {request.RequestId} started: " +
            string.Join(" + ", recipe.SourceRecipeIds));
        Advance(state, realtimeNow);
        return run;
    }

    public void Tick(float realtimeNow)
    {
        this.realtimeNow = realtimeNow;
        RunState[] snapshot = activeRuns.ToArray();
        for (int i = 0; i < snapshot.Length; i++)
        {
            RunState state = snapshot[i];
            if (state.Run.IsFinished)
                continue;

            if (!state.Run.IsGateReleased &&
                state.Run.Recipe.GateKind != BattlePresentationGateKind.None &&
                realtimeNow >= state.GateDeadline)
            {
                Timeout(state);
                continue;
            }

            Advance(state, realtimeNow);
        }
    }

    public void CancelScope(
        BattlePresentationScope scope,
        BattlePresentationCancelReason reason)
    {
        RunState[] snapshot = activeRuns.ToArray();
        for (int i = 0; i < snapshot.Length; i++)
        {
            if (snapshot[i].Run.Request.Scope == scope)
                snapshot[i].Run.CancelDirect(reason);
        }
    }

    public void CancelAll(BattlePresentationCancelReason reason)
    {
        RunState[] snapshot = activeRuns.ToArray();
        for (int i = 0; i < snapshot.Length; i++)
            snapshot[i].Run.CancelDirect(reason);
    }

    private void Advance(RunState state, float now)
    {
        int safety = state.Run.Recipe.Beats.Count + 1;
        while (!state.Run.IsFinished && safety-- > 0)
        {
            if (state.BeatIndex >= state.Run.Recipe.Beats.Count)
            {
                state.Run.MarkRecipeComplete(BattlePresentationOutcome.Completed);
                return;
            }

            ResolvedBattlePresentationBeat beat =
                state.Run.Recipe.Beats[state.BeatIndex];
            if (!state.BeatStarted)
            {
                if (now < state.BeatReadyAt + beat.DelayBeforeSeconds)
                    return;

                state.BeatStarted = true;
                state.BeatStartedAt = now;
                state.BlockingHandles.Clear();
                for (int i = 0; i < beat.Cues.Count; i++)
                {
                    ResolvedBattlePresentationCue cue = beat.Cues[i];
                    IBattlePresentationHandle handle = StartCue(state.Run, cue);
                    state.Run.RegisterCue(handle);
                    if (cue.BlocksBeat)
                        state.BlockingHandles.Add(handle);
                }
            }

            if (now < state.BeatStartedAt + beat.MinimumDurationSeconds ||
                HasUnsettled(state.BlockingHandles))
            {
                return;
            }

            if (beat.ReleasesGate)
            {
                state.Run.ReleaseGate(
                    MergeOutcomes(state.BlockingHandles));
            }

            state.BeatIndex++;
            state.BeatReadyAt = now;
            state.BeatStarted = false;
            state.BlockingHandles.Clear();
        }
    }

    private IBattlePresentationHandle StartCue(
        BattlePresentationRun run,
        ResolvedBattlePresentationCue cue)
    {
        var playback = new CuePlaybackHandle();
        if (run.Request.IsStandardBasicAttackLeaf &&
            (cue.Cue.Kind == BattlePresentationCueKind.ConflictRestore || cue.Cue.Kind == BattlePresentationCueKind.SpineMarker ||
             cue.Cue is BattleSemanticPresentationCueSO semantic &&
                (semantic.ModuleKind == BattleSemanticPresentationModuleKind.Exit || semantic.ModuleKind == BattleSemanticPresentationModuleKind.Spine)))
        {
            trace.Record("Standard leaf rejected legacy rule/restore cue: " + cue.Cue.Kind);
            playback.TrySettle(BattlePresentationOutcome.Skipped);
            return playback;
        }
        if (!executors.TryGetValue(cue.Cue.Kind, out
            IBattlePresentationCueExecutor executor))
        {
            trace.Record(
                $"Request {run.Request.RequestId} cue {cue.Cue.CueId} Skipped: " +
                "no executor");
            playback.TrySettle(BattlePresentationOutcome.Skipped);
            return playback;
        }

        if (cue.Lane == BattlePresentationLaneKind.None ||
            cue.ConflictPolicy == BattlePresentationConflictPolicy.Blend)
        {
            StartExecutor(playback, executor, cue.Cue, run.Request);
            return playback;
        }

        BattlePresentationLaneKey key =
            BattlePresentationLaneKey.From(cue.Lane, run.Request);
        if (!lanes.TryGetValue(key, out LaneState lane))
        {
            lane = new LaneState();
            lanes.Add(key, lane);
        }

        var queued = new QueuedCue(
            key,
            run,
            cue,
            executor,
            playback);
        if (lane.Active == null || lane.Active.IsSettled)
        {
            StartExclusive(lane, queued);
            return playback;
        }

        switch (cue.ConflictPolicy)
        {
            case BattlePresentationConflictPolicy.IgnoreIfBusy:
                playback.TrySettle(BattlePresentationOutcome.Skipped);
                break;
            case BattlePresentationConflictPolicy.Queue:
                lane.Queue.Enqueue(queued);
                break;
            case BattlePresentationConflictPolicy.Replace:
                if (MustProtectActionFlow(lane.ActiveCue, queued))
                {
                    lane.Queue.Enqueue(queued);
                }
                else
                {
                    lane.SuppressDrain = true;
                    lane.Active.Cancel(BattlePresentationCancelReason.Replaced);
                    lane.SuppressDrain = false;
                    StartExclusive(lane, queued);
                }
                break;
            default:
                StartExecutor(playback, executor, cue.Cue, run.Request);
                break;
        }
        return playback;
    }

    private void StartExclusive(LaneState lane, QueuedCue queued)
    {
        if (queued.Playback.IsSettled)
        {
            DrainLane(lane);
            return;
        }

        lane.Active = queued.Playback;
        lane.ActiveCue = queued;
        queued.Playback.WhenSettled(_ => OnLaneSettled(lane, queued));
        StartExecutor(
            queued.Playback,
            queued.Executor,
            queued.Cue.Cue,
            queued.Run.Request);
    }

    private void OnLaneSettled(LaneState lane, QueuedCue queued)
    {
        if (!ReferenceEquals(lane.ActiveCue, queued))
            return;

        lane.Active = null;
        lane.ActiveCue = null;
        if (!lane.SuppressDrain)
            DrainLane(lane);
    }

    private void DrainLane(LaneState lane)
    {
        while (lane.Active == null && lane.Queue.Count > 0)
        {
            QueuedCue next = lane.Queue.Dequeue();
            if (next.Playback.IsSettled || next.Run.IsFinished)
                continue;
            StartExclusive(lane, next);
        }
    }

    private void StartExecutor(
        CuePlaybackHandle playback,
        IBattlePresentationCueExecutor executor,
        BattlePresentationCueSO cue,
        BattlePresentationRequest request)
    {
        if (playback.IsSettled)
            return;

        try
        {
            IBattlePresentationHandle inner = executor.Play(cue, request);
            if (inner == null)
            {
                playback.TrySettle(BattlePresentationOutcome.Skipped);
                trace.Record(
                    $"Request {request.RequestId} cue {cue.CueId} Skipped: " +
                    "executor returned null");
                return;
            }

            playback.Attach(inner);
            trace.Record($"Request {request.RequestId} cue {cue.CueId} started");
        }
        catch (Exception exception)
        {
            playback.TrySettle(BattlePresentationOutcome.Faulted);
            trace.Record(
                $"Request {request.RequestId} cue {cue.CueId} Faulted: " +
                exception.Message);
        }
    }

    private void Timeout(RunState state)
    {
        BattlePresentationRun run = state.Run;
        trace.Record(
            $"Request {run.Request.RequestId} / Action " +
            $"{run.Request.Scope.ActionId} / Recipe " +
            $"{string.Join("+", run.Recipe.SourceRecipeIds)} TimedOut");
        run.CancelUnsettledCues(BattlePresentationCancelReason.TimedOut);
        run.ReleaseGate(BattlePresentationOutcome.TimedOut);
        run.MarkRecipeComplete(BattlePresentationOutcome.TimedOut);
    }

    private void OnRunCancelRequested(
        BattlePresentationRun run,
        BattlePresentationCancelReason reason)
    {
        run.CancelDirect(reason);
    }

    private void CancelOlderBattleGenerations(long newGeneration)
    {
        RunState[] snapshot = activeRuns.ToArray();
        for (int i = 0; i < snapshot.Length; i++)
        {
            if (snapshot[i].Run.Request.Scope.BattleGeneration < newGeneration)
            {
                snapshot[i].Run.CancelDirect(
                    BattlePresentationCancelReason.ScopeEnded);
            }
        }
    }

    private BattlePresentationRun CreateSettledRun(
        BattlePresentationRequest request,
        BattlePresentationOutcome outcome)
    {
        var empty = new ResolvedBattlePresentationRecipe(
            Array.Empty<string>(),
            BattlePresentationGateKind.None,
            0f,
            Array.Empty<ResolvedBattlePresentationBeat>());
        var run = new BattlePresentationRun(request, empty, null);
        run.ReleaseGate(outcome);
        run.MarkRecipeComplete(outcome);
        return run;
    }

    private static Dictionary<BattlePresentationCueKind,
        IBattlePresentationCueExecutor> BuildExecutorMap(
        IEnumerable<IBattlePresentationCueExecutor> source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        var map = new Dictionary<BattlePresentationCueKind,
            IBattlePresentationCueExecutor>();
        foreach (IBattlePresentationCueExecutor executor in source)
        {
            if (executor == null)
                continue;
            if (map.ContainsKey(executor.Kind))
            {
                throw new InvalidOperationException(
                    "Duplicate presentation cue executor: " + executor.Kind);
            }
            map.Add(executor.Kind, executor);
        }
        return map;
    }

    private static bool MustProtectActionFlow(
        QueuedCue active,
        QueuedCue incoming)
    {
        if (active == null || incoming == null ||
            !active.Cue.BlocksBeat || !incoming.Cue.BlocksBeat ||
            active.Run.Recipe.GateKind != BattlePresentationGateKind.ActionFlow)
        {
            return false;
        }

        long activeAction = active.Run.Request.Scope.ActionId;
        long incomingAction = incoming.Run.Request.Scope.ActionId;
        return activeAction != incomingAction;
    }

    private static bool HasUnsettled(
        IReadOnlyList<IBattlePresentationHandle> handles)
    {
        for (int i = 0; i < handles.Count; i++)
        {
            if (!handles[i].IsSettled)
                return true;
        }
        return false;
    }

    private static BattlePresentationOutcome MergeOutcomes(
        IReadOnlyList<IBattlePresentationHandle> handles)
    {
        bool anyCompleted = handles.Count == 0;
        bool anyCancelled = false;
        bool anyTimedOut = false;
        bool anyFaulted = false;
        for (int i = 0; i < handles.Count; i++)
        {
            switch (handles[i].Outcome)
            {
                case BattlePresentationOutcome.Completed:
                    anyCompleted = true;
                    break;
                case BattlePresentationOutcome.Cancelled:
                    anyCancelled = true;
                    break;
                case BattlePresentationOutcome.TimedOut:
                    anyTimedOut = true;
                    break;
                case BattlePresentationOutcome.Faulted:
                    anyFaulted = true;
                    break;
            }
        }

        if (anyFaulted)
            return BattlePresentationOutcome.Faulted;
        if (anyTimedOut)
            return BattlePresentationOutcome.TimedOut;
        if (anyCancelled)
            return BattlePresentationOutcome.Cancelled;
        if (anyCompleted)
            return BattlePresentationOutcome.Completed;
        return BattlePresentationOutcome.Skipped;
    }

    private sealed class RunState
    {
        public RunState(BattlePresentationRun run, float startedAt)
        {
            Run = run;
            BeatReadyAt = startedAt;
            GateDeadline = startedAt + Math.Max(0f, run.Recipe.MaximumGateSeconds);
        }

        public BattlePresentationRun Run { get; }
        public int BeatIndex { get; set; }
        public bool BeatStarted { get; set; }
        public float BeatReadyAt { get; set; }
        public float BeatStartedAt { get; set; }
        public float GateDeadline { get; }
        public List<IBattlePresentationHandle> BlockingHandles { get; } =
            new List<IBattlePresentationHandle>();
    }

    private sealed class LaneState
    {
        public CuePlaybackHandle Active;
        public QueuedCue ActiveCue;
        public bool SuppressDrain;
        public Queue<QueuedCue> Queue { get; } = new Queue<QueuedCue>();
    }

    private sealed class QueuedCue
    {
        public QueuedCue(
            BattlePresentationLaneKey key,
            BattlePresentationRun run,
            ResolvedBattlePresentationCue cue,
            IBattlePresentationCueExecutor executor,
            CuePlaybackHandle playback)
        {
            Key = key;
            Run = run;
            Cue = cue;
            Executor = executor;
            Playback = playback;
        }

        public BattlePresentationLaneKey Key { get; }
        public BattlePresentationRun Run { get; }
        public ResolvedBattlePresentationCue Cue { get; }
        public IBattlePresentationCueExecutor Executor { get; }
        public CuePlaybackHandle Playback { get; }
    }

    private sealed class CuePlaybackHandle : IBattlePresentationHandle
    {
        private readonly BattlePresentationHandle completion =
            new BattlePresentationHandle();
        private IBattlePresentationHandle inner;

        public bool IsSettled => completion.IsSettled;
        public BattlePresentationOutcome Outcome => completion.Outcome;

        public void Attach(IBattlePresentationHandle handle)
        {
            if (handle == null)
                throw new ArgumentNullException(nameof(handle));
            if (inner != null)
                throw new InvalidOperationException("Cue playback is already attached.");

            inner = handle;
            if (completion.IsSettled)
            {
                if (!handle.IsSettled)
                    handle.Cancel(BattlePresentationCancelReason.ScopeEnded);
                return;
            }
            handle.WhenSettled(outcome => completion.TrySettle(outcome));
        }

        public bool TrySettle(BattlePresentationOutcome outcome)
        {
            return completion.TrySettle(outcome);
        }

        public void WhenSettled(Action<BattlePresentationOutcome> callback)
        {
            completion.WhenSettled(callback);
        }

        public void Cancel(BattlePresentationCancelReason reason)
        {
            if (completion.IsSettled)
                return;
            if (inner != null && !inner.IsSettled)
                inner.Cancel(reason);
            if (!completion.IsSettled)
                completion.Cancel(reason);
        }
    }
}
