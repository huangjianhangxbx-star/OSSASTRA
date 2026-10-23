#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class DRPGBattleBasicAttackTransactionFixture : IDisposable
{
    public const float Epsilon = 0.0001f;
    public static readonly Vector3 HomePosition = new Vector3(-4f, 0f, 0f);
    public static readonly Vector3 ConflictPosition = new Vector3(0f, 0f, 0f);
    public static readonly Vector3 RecoilPosition = new Vector3(-1f, 0f, 0f);

    private readonly List<GameObject> trackedObjects = new List<GameObject>();
    private readonly Dictionary<string, Vector3> lastObservedValues =
        new Dictionary<string, Vector3>();
    private int productionObservationFrame = -1;
    private int productionObservationMicrostep;
    private bool disposed;

    public GameObject CreateTracked(string name)
    {
        ThrowIfDisposed();
        var gameObject = new GameObject(name ?? throw new ArgumentNullException(nameof(name)));
        trackedObjects.Add(gameObject);
        return gameObject;
    }

    public void SampleTransform(
        Transform transform,
        ManualBattleClock clock,
        string path,
        List<TransformObservation> observations)
    {
        if (transform == null)
            throw new ArgumentNullException(nameof(transform));
        if (clock == null)
            throw new ArgumentNullException(nameof(clock));
        if (path == null)
            throw new ArgumentNullException(nameof(path));
        if (observations == null)
            throw new ArgumentNullException(nameof(observations));

        RecordObservation(
            path,
            "position",
            transform.position,
            clock.FrameOrdinal,
            clock.Time,
            clock.NextMicrostep(),
            observations);
        RecordObservation(
            path,
            "localScale",
            transform.localScale,
            clock.FrameOrdinal,
            clock.Time,
            clock.NextMicrostep(),
            observations);
    }

    public void SampleProductionTransform(
        Transform transform,
        string path,
        List<TransformObservation> observations)
    {
        if (transform == null)
            throw new ArgumentNullException(nameof(transform));
        if (path == null)
            throw new ArgumentNullException(nameof(path));
        if (observations == null)
            throw new ArgumentNullException(nameof(observations));

        int frame = Time.frameCount;
        if (productionObservationFrame != frame)
        {
            productionObservationFrame = frame;
            productionObservationMicrostep = 0;
        }

        RecordObservation(
            path,
            "position",
            transform.position,
            frame,
            Time.realtimeSinceStartupAsDouble,
            ++productionObservationMicrostep,
            observations);
        RecordObservation(
            path,
            "localScale",
            transform.localScale,
            frame,
            Time.realtimeSinceStartupAsDouble,
            ++productionObservationMicrostep,
            observations);
    }

    private void RecordObservation(
        string path,
        string property,
        Vector3 current,
        int frame,
        double time,
        int microstep,
        ICollection<TransformObservation> observations)
    {
        string key = path + "|" + property;
        Vector3 before;
        if (!lastObservedValues.TryGetValue(key, out before))
            before = current;
        observations.Add(new TransformObservation(
            frame,
            time,
            microstep,
            path,
            property,
            before,
            current));
        lastObservedValues[key] = current;
    }

    public ViewIdentitySnapshot CaptureViewIdentity(
        BattleWorldActorPool pool,
        int runtimeId)
    {
        if (pool == null)
            throw new ArgumentNullException(nameof(pool));

        if (!pool.TryGet(runtimeId, out BattleWorldActorView view) || view == null)
            return new ViewIdentitySnapshot(runtimeId, 0, false);

        return new ViewIdentitySnapshot(
            view.RuntimeId,
            view.GetInstanceID(),
            view.gameObject.activeInHierarchy);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        for (int i = trackedObjects.Count - 1; i >= 0; i--)
        {
            if (trackedObjects[i] != null)
                UnityEngine.Object.DestroyImmediate(trackedObjects[i]);
        }

        trackedObjects.Clear();
        lastObservedValues.Clear();
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(DRPGBattleBasicAttackTransactionFixture));
    }
}

public readonly struct ViewIdentitySnapshot
{
    public ViewIdentitySnapshot(int runtimeId, int instanceId, bool active)
    {
        RuntimeId = runtimeId;
        InstanceId = instanceId;
        Active = active;
    }

    public int RuntimeId { get; }
    public int InstanceId { get; }
    public bool Active { get; }
}

internal sealed class ManualBattleGenerationSource : IDRPGBattleGenerationSource
{
    private long next;

    internal ManualBattleGenerationSource(long firstGeneration = 1L)
    {
        if (firstGeneration <= 0L)
            throw new ArgumentOutOfRangeException(nameof(firstGeneration));
        next = firstGeneration - 1L;
    }

    public long NextGeneration()
    {
        next++;
        return next;
    }
}

internal sealed class LosslessActionTraceSink : IActionTraceSink
{
    private readonly List<ActionTraceEntry> entries =
        new List<ActionTraceEntry>();

    public void Append(ActionTraceEntry entry)
    {
        entries.Add(entry);
    }

    public IReadOnlyList<ActionTraceEntry> Snapshot()
    {
        return new List<ActionTraceEntry>(entries).AsReadOnly();
    }
}
#endif
