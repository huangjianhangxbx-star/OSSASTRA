#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ManualBattleClock
{
    private const float DeltaEpsilon = 0.0001f;

    public double Time { get; private set; }
    public int FrameOrdinal { get; private set; }
    public int Microstep { get; private set; }

    public void Advance(float delta)
    {
        if (delta <= 0f || !IsSupportedDelta(delta))
            throw new ArgumentOutOfRangeException(nameof(delta));

        Time += delta;
    }

    public void BeginFrame()
    {
        FrameOrdinal++;
        Microstep = 0;
    }

    public int NextMicrostep()
    {
        Microstep++;
        return Microstep;
    }

    private static bool IsSupportedDelta(float delta)
    {
        return Mathf.Abs(delta - (1f / 60f)) <= DeltaEpsilon ||
            Mathf.Abs(delta - (1f / 30f)) <= DeltaEpsilon ||
            Mathf.Abs(delta - 0.12f) <= DeltaEpsilon;
    }
}

public readonly struct TransformObservation
{
    public TransformObservation(
        int frameOrdinal,
        double time,
        int microstep,
        string objectPath,
        string property,
        Vector3 before,
        Vector3 after)
    {
        FrameOrdinal = frameOrdinal;
        Time = time;
        Microstep = microstep;
        ObjectPath = objectPath ?? throw new ArgumentNullException(nameof(objectPath));
        Property = property ?? throw new ArgumentNullException(nameof(property));
        Before = before;
        After = after;
    }

    public int FrameOrdinal { get; }
    public double Time { get; }
    public int Microstep { get; }
    public string ObjectPath { get; }
    public string Property { get; }
    public Vector3 Before { get; }
    public Vector3 After { get; }
}

public sealed class CollectingBattleTrace<T>
{
    private readonly List<T> entries = new List<T>();

    public int Count => entries.Count;

    public void Append(T entry)
    {
        entries.Add(entry);
    }

    public IReadOnlyList<T> Snapshot()
    {
        return new List<T>(entries).AsReadOnly();
    }
}

public sealed class RecordingContinuation
{
    private readonly List<string> entries = new List<string>();

    public void Record(string entry)
    {
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));

        entries.Add(entry);
    }

    public IReadOnlyList<string> Snapshot()
    {
        return new List<string>(entries).AsReadOnly();
    }
}
#endif
