#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGBattlePresentationDirectorProbe
{
    public static string Run()
    {
        var created = new List<ScriptableObject>();
        try
        {
            VerifyBeatProgression(created);
            VerifyLanePolicies(created);
            VerifyWatchdog(created);
            VerifyBattleGenerationCancellation(created);
        }
        finally
        {
            for (int i = created.Count - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(created[i]);
        }

        return "PASS DRPG presentation director";
    }

    private static void VerifyBeatProgression(
        ICollection<ScriptableObject> created)
    {
        var fake = new FakeCueExecutor(BattlePresentationCueKind.ScreenFlash);
        BattlePresentationRecipeSetSO set = CreateRecipeSet(
            created,
            "progression",
            BattlePresentationGateKind.ActionFlow,
            5f,
            new[]
            {
                CreateBeat(
                    "blocking",
                    0f,
                    0.5f,
                    false,
                    CreateEntry(created, BattlePresentationConflictPolicy.Blend, true),
                    CreateEntry(created, BattlePresentationConflictPolicy.Blend, true)),
                CreateBeat(
                    "tail",
                    0f,
                    0f,
                    true,
                    CreateEntry(created, BattlePresentationConflictPolicy.Blend, false))
            });
        BattleInputGate inputGate = new BattleInputGate();
        var director = new BattlePresentationDirector(
            new BattlePresentationRecipeResolver(),
            set,
            new[] { fake },
            inputGate,
            new BattlePresentationTraceBuffer(64));

        BattlePresentationRun run = director.Play(
            CreateRequest(1, 1, 7),
            10f);
        AssertEqual(2, fake.Started.Count,
            "The first beat must start its parallel cues immediately");
        AssertFalse(run.IsGateReleased,
            "The action gate must wait for blocking cues and duration");

        fake.Started[0].Handle.TrySettle(BattlePresentationOutcome.Completed);
        director.Tick(10.25f);
        AssertFalse(run.IsGateReleased,
            "One remaining cue and minimum duration must retain the gate");

        fake.Started[1].Handle.TrySettle(BattlePresentationOutcome.Completed);
        director.Tick(10.50f);
        AssertTrue(run.IsGateReleased,
            "The release beat must release the action gate");
        AssertFalse(run.IsFinished,
            "A non-blocking tail cue must outlive the action gate");
        AssertEqual(3, fake.Started.Count,
            "The tail beat must start after the blocking beat");

        fake.Started[2].Handle.TrySettle(BattlePresentationOutcome.Completed);
        AssertTrue(run.IsFinished,
            "The run must finish when its tail settles");
        AssertEqual(1, run.GateReleaseCount,
            "The action gate must release exactly once");

        int lateCallbacks = 0;
        run.WhenGateReleased(_ => lateCallbacks++);
        AssertEqual(1, lateCallbacks,
            "Late gate subscribers must observe the retained outcome once");
    }

    private static void VerifyLanePolicies(
        ICollection<ScriptableObject> created)
    {
        VerifyReplace(created);
        VerifyQueue(created);
        VerifyBlend(created);
        VerifyIgnoreIfBusy(created);
        VerifyActionFlowCannotBeStolen(created);
    }

    private static void VerifyReplace(ICollection<ScriptableObject> created)
    {
        DirectorFixture fixture = CreateSingleCueFixture(
            created,
            "replace",
            BattlePresentationConflictPolicy.Replace,
            BattlePresentationGateKind.None);
        fixture.Director.Play(CreateRequest(10, 1, 0), 0f);
        fixture.Director.Play(CreateRequest(11, 1, 0), 0f);

        AssertEqual(2, fixture.Executor.Started.Count,
            "Replace must start the newer cue immediately");
        AssertEqual(
            BattlePresentationOutcome.Cancelled,
            fixture.Executor.Started[0].Handle.Outcome,
            "Replace must cancel the previous lane owner");
    }

    private static void VerifyQueue(ICollection<ScriptableObject> created)
    {
        DirectorFixture fixture = CreateSingleCueFixture(
            created,
            "queue",
            BattlePresentationConflictPolicy.Queue,
            BattlePresentationGateKind.None);
        fixture.Director.Play(CreateRequest(20, 1, 0), 0f);
        fixture.Director.Play(CreateRequest(21, 1, 0), 0f);
        AssertEqual(1, fixture.Executor.Started.Count,
            "Queue must wait while the lane is occupied");

        fixture.Executor.Started[0].Handle.TrySettle(
            BattlePresentationOutcome.Completed);
        AssertEqual(2, fixture.Executor.Started.Count,
            "Queue must start the next cue when the lane settles");
    }

    private static void VerifyBlend(ICollection<ScriptableObject> created)
    {
        DirectorFixture fixture = CreateSingleCueFixture(
            created,
            "blend",
            BattlePresentationConflictPolicy.Blend,
            BattlePresentationGateKind.None);
        fixture.Director.Play(CreateRequest(30, 1, 0), 0f);
        fixture.Director.Play(CreateRequest(31, 1, 0), 0f);
        AssertEqual(2, fixture.Executor.Started.Count,
            "Blend must allow concurrent cues");
    }

    private static void VerifyIgnoreIfBusy(
        ICollection<ScriptableObject> created)
    {
        DirectorFixture fixture = CreateSingleCueFixture(
            created,
            "ignore",
            BattlePresentationConflictPolicy.IgnoreIfBusy,
            BattlePresentationGateKind.None);
        fixture.Director.Play(CreateRequest(40, 1, 0), 0f);
        BattlePresentationRun ignored = fixture.Director.Play(
            CreateRequest(41, 1, 0),
            0f);
        AssertEqual(1, fixture.Executor.Started.Count,
            "IgnoreIfBusy must not start a second cue");
        AssertTrue(ignored.IsFinished,
            "A skipped non-gated cue must not leave a dangling run");
    }

    private static void VerifyActionFlowCannotBeStolen(
        ICollection<ScriptableObject> created)
    {
        DirectorFixture fixture = CreateSingleCueFixture(
            created,
            "action_scope",
            BattlePresentationConflictPolicy.Replace,
            BattlePresentationGateKind.ActionFlow);
        BattlePresentationRun first = fixture.Director.Play(
            CreateRequest(50, 1, 100),
            0f);
        BattlePresentationRun second = fixture.Director.Play(
            CreateRequest(51, 1, 101),
            0f);

        AssertEqual(1, fixture.Executor.Started.Count,
            "A different action scope must queue instead of stealing the lane");
        AssertFalse(first.IsGateReleased,
            "The first action gate must remain owned by the first action");
        AssertFalse(second.IsGateReleased,
            "The queued action gate must wait for its own cue");

        fixture.Executor.Started[0].Handle.TrySettle(
            BattlePresentationOutcome.Completed);
        fixture.Director.Tick(0f);
        AssertTrue(first.IsGateReleased,
            "The first action must release from its own cue");
        AssertEqual(2, fixture.Executor.Started.Count,
            "The queued action may start after the first lane owner settles");
    }

    private static void VerifyWatchdog(
        ICollection<ScriptableObject> created)
    {
        DirectorFixture fixture = CreateSingleCueFixture(
            created,
            "watchdog",
            BattlePresentationConflictPolicy.Blend,
            BattlePresentationGateKind.ActionFlow,
            maximumGateSeconds: 5f);
        BattlePresentationRun run = fixture.Director.Play(
            CreateRequest(60, 1, 200),
            10f);

        fixture.Director.Tick(14.99f);
        AssertFalse(run.IsGateReleased,
            "Watchdog must not fire before its realtime deadline");
        fixture.Director.Tick(15f);
        AssertTrue(run.IsGateReleased,
            "Watchdog must fail open at its realtime deadline");
        AssertEqual(
            BattlePresentationOutcome.TimedOut,
            run.GateOutcome,
            "Watchdog must expose a TimedOut gate outcome");
        AssertTrue(run.IsFinished,
            "Watchdog cleanup must not retain a dangling run");
        AssertTrue(
            fixture.Trace.Entries[fixture.Trace.Entries.Count - 1]
                .IndexOf("TimedOut", StringComparison.Ordinal) >= 0,
            "Watchdog timeout must be traced");
    }

    private static void VerifyBattleGenerationCancellation(
        ICollection<ScriptableObject> created)
    {
        DirectorFixture fixture = CreateSingleCueFixture(
            created,
            "generation",
            BattlePresentationConflictPolicy.Blend,
            BattlePresentationGateKind.ActionFlow);
        BattlePresentationRun oldRun = fixture.Director.Play(
            CreateRequest(70, 1, 300, battleGeneration: 1),
            0f);
        fixture.Director.Play(
            CreateRequest(71, 1, 301, battleGeneration: 2),
            0f);

        AssertTrue(oldRun.IsFinished,
            "A newer battle generation must cancel old runs");
        AssertEqual(
            BattlePresentationOutcome.Cancelled,
            oldRun.FinishedOutcome,
            "An old generation must finish as cancelled");
    }

    private static DirectorFixture CreateSingleCueFixture(
        ICollection<ScriptableObject> created,
        string id,
        BattlePresentationConflictPolicy policy,
        BattlePresentationGateKind gateKind,
        float maximumGateSeconds = 5f)
    {
        var fake = new FakeCueExecutor(BattlePresentationCueKind.ScreenFlash);
        BattlePresentationRecipeSetSO set = CreateRecipeSet(
            created,
            id,
            gateKind,
            maximumGateSeconds,
            new[]
            {
                CreateBeat(
                    id + "_beat",
                    0f,
                    0f,
                    gateKind != BattlePresentationGateKind.None,
                    CreateEntry(created, policy, true,
                        BattlePresentationLaneKind.Timeline))
            });
        var trace = new BattlePresentationTraceBuffer(64);
        var director = new BattlePresentationDirector(
            new BattlePresentationRecipeResolver(),
            set,
            new[] { fake },
            new BattleInputGate(),
            trace);
        return new DirectorFixture(director, fake, trace);
    }

    private static BattlePresentationRequest CreateRequest(
        long requestId,
        int turnSerial,
        long actionId,
        int battleGeneration = 1)
    {
        return new BattlePresentationRequest(
            requestId,
            BattlePresentationMoment.ImpactResolved,
            new BattlePresentationScope(
                battleGeneration,
                turnSerial,
                actionId),
            null,
            Array.Empty<BattleParticipantPresentationSnapshot>(),
            Array.Empty<int>(),
            string.Empty,
            EAnimationLogicState.None,
            Array.Empty<BattleHitPresentationSnapshot>(),
            BattlePresentationFacts.Empty);
    }

    private static BattlePresentationRecipeSetSO CreateRecipeSet(
        ICollection<ScriptableObject> created,
        string recipeId,
        BattlePresentationGateKind gateKind,
        float maximumGateSeconds,
        IReadOnlyList<BattlePresentationBeat> beats)
    {
        BattlePresentationRecipeSO recipe =
            ScriptableObject.CreateInstance<BattlePresentationRecipeSO>();
        created.Add(recipe);
        SetField(recipe, "recipeId", recipeId);
        SetField(recipe, "moment", BattlePresentationMoment.ImpactResolved);
        SetField(recipe, "gateKind", gateKind);
        SetField(recipe, "maximumGateSeconds", maximumGateSeconds);
        SetField(recipe, "beats", new List<BattlePresentationBeat>(beats));

        BattlePresentationRecipeSetSO set =
            ScriptableObject.CreateInstance<BattlePresentationRecipeSetSO>();
        created.Add(set);
        SetField(set, "baseRecipes", new List<BattlePresentationRecipeSO>
        {
            recipe
        });
        return set;
    }

    private static BattlePresentationBeat CreateBeat(
        string beatId,
        float delayBefore,
        float minimumDuration,
        bool releasesGate,
        params BattlePresentationCueEntry[] entries)
    {
        var beat = new BattlePresentationBeat();
        SetField(beat, "beatId", beatId);
        SetField(beat, "delayBeforeSeconds", delayBefore);
        SetField(beat, "minimumDurationSeconds", minimumDuration);
        SetField(beat, "releasesGate", releasesGate);
        SetField(beat, "cues", new List<BattlePresentationCueEntry>(entries));
        return beat;
    }

    private static BattlePresentationCueEntry CreateEntry(
        ICollection<ScriptableObject> created,
        BattlePresentationConflictPolicy policy,
        bool blocksBeat,
        BattlePresentationLaneKind lane = BattlePresentationLaneKind.ScreenFX)
    {
        TestScreenFlashCueSO cue =
            ScriptableObject.CreateInstance<TestScreenFlashCueSO>();
        created.Add(cue);
        SetField(cue, "cueId", "screen_flash");
        SetField(cue, "kind", BattlePresentationCueKind.ScreenFlash);

        var entry = new BattlePresentationCueEntry();
        SetField(entry, "cue", cue);
        SetField(entry, "condition", BattlePresentationCondition.Always);
        SetField(entry, "lane", lane);
        SetField(entry, "conflictPolicy", policy);
        SetField(entry, "blocksBeat", blocksBeat);
        return entry;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        Type type = target.GetType();
        while (type != null)
        {
            FieldInfo field = type.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }
            type = type.BaseType;
        }
        throw new MissingFieldException(target.GetType().Name, fieldName);
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    private static void AssertFalse(bool value, string message)
    {
        AssertTrue(!value, message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception(
                $"{message}. Expected={expected}, Actual={actual}");
        }
    }

    private sealed class TestScreenFlashCueSO : BattlePresentationCueSO
    {
    }

    private sealed class FakeCueExecutor : IBattlePresentationCueExecutor
    {
        public FakeCueExecutor(BattlePresentationCueKind kind)
        {
            Kind = kind;
        }

        public BattlePresentationCueKind Kind { get; }
        public List<StartedCue> Started { get; } = new List<StartedCue>();

        public IBattlePresentationHandle Play(
            BattlePresentationCueSO cue,
            BattlePresentationRequest request)
        {
            var handle = new BattlePresentationHandle();
            Started.Add(new StartedCue(cue, request, handle));
            return handle;
        }
    }

    private sealed class StartedCue
    {
        public StartedCue(
            BattlePresentationCueSO cue,
            BattlePresentationRequest request,
            BattlePresentationHandle handle)
        {
            Cue = cue;
            Request = request;
            Handle = handle;
        }

        public BattlePresentationCueSO Cue { get; }
        public BattlePresentationRequest Request { get; }
        public BattlePresentationHandle Handle { get; }
    }

    private readonly struct DirectorFixture
    {
        public DirectorFixture(
            BattlePresentationDirector director,
            FakeCueExecutor executor,
            BattlePresentationTraceBuffer trace)
        {
            Director = director;
            Executor = executor;
            Trace = trace;
        }

        public BattlePresentationDirector Director { get; }
        public FakeCueExecutor Executor { get; }
        public BattlePresentationTraceBuffer Trace { get; }
    }
}
#endif
