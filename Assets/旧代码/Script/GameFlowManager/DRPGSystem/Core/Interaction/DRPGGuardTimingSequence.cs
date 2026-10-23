using System;
using System.Collections.Generic;

public readonly struct DRPGGuardImpactDecision
{
    public DRPGGuardImpactDecision(
        int impactIndex,
        float impactTime,
        DRPGGuardGrade grade)
    {
        ImpactIndex = impactIndex;
        ImpactTime = impactTime;
        Grade = grade;
    }

    public int ImpactIndex { get; }
    public float ImpactTime { get; }
    public DRPGGuardGrade Grade { get; }
}

public sealed class DRPGGuardTimingSequence
{
    private const float BoundaryTolerance = 0.0001f;
    private readonly float[] impactTimes;
    private readonly float guardLeadSeconds;
    private readonly float perfectLeadSeconds;
    private int nextImpactIndex;
    private bool hasGuardPress;
    private float guardPressTime;
    private float lastElapsed;

    public DRPGGuardTimingSequence(
        IReadOnlyList<float> configuredImpactTimes,
        float guardLeadSeconds,
        float perfectLeadSeconds)
    {
        if (configuredImpactTimes == null || configuredImpactTimes.Count == 0)
        {
            throw new ArgumentException(
                "At least one impact time is required.",
                nameof(configuredImpactTimes));
        }
        if (guardLeadSeconds <= 0f)
            throw new ArgumentOutOfRangeException(nameof(guardLeadSeconds));
        if (perfectLeadSeconds <= 0f || perfectLeadSeconds > guardLeadSeconds)
            throw new ArgumentOutOfRangeException(nameof(perfectLeadSeconds));

        impactTimes = new float[configuredImpactTimes.Count];
        float previous = 0f;
        for (int i = 0; i < configuredImpactTimes.Count; i++)
        {
            float value = configuredImpactTimes[i];
            if (value <= 0f || i > 0 && value <= previous)
            {
                throw new ArgumentException(
                    "Impact times must be positive and strictly increasing.",
                    nameof(configuredImpactTimes));
            }
            impactTimes[i] = value;
            previous = value;
        }

        this.guardLeadSeconds = guardLeadSeconds;
        this.perfectLeadSeconds = perfectLeadSeconds;
    }

    public bool IsComplete => nextImpactIndex >= impactTimes.Length;
    public int NextImpactIndex => nextImpactIndex;
    public float NextImpactTime => IsComplete
        ? impactTimes[impactTimes.Length - 1]
        : impactTimes[nextImpactIndex];
    public float Duration => impactTimes[impactTimes.Length - 1];

    public bool SubmitGuard(float elapsedSeconds)
    {
        ValidateElapsed(elapsedSeconds);
        if (IsComplete || hasGuardPress)
            return false;

        float lead = impactTimes[nextImpactIndex] - elapsedSeconds;
        if (lead < -BoundaryTolerance ||
            lead - guardLeadSeconds > BoundaryTolerance)
        {
            return false;
        }

        hasGuardPress = true;
        guardPressTime = elapsedSeconds;
        lastElapsed = Math.Max(lastElapsed, elapsedSeconds);
        return true;
    }

    public IReadOnlyList<DRPGGuardImpactDecision> Advance(float elapsedSeconds)
    {
        ValidateElapsed(elapsedSeconds);
        lastElapsed = elapsedSeconds;
        if (IsComplete || impactTimes[nextImpactIndex] - elapsedSeconds > BoundaryTolerance)
            return Array.Empty<DRPGGuardImpactDecision>();

        var decisions = new List<DRPGGuardImpactDecision>();
        while (!IsComplete &&
            impactTimes[nextImpactIndex] - elapsedSeconds <= BoundaryTolerance)
        {
            float impactTime = impactTimes[nextImpactIndex];
            DRPGGuardGrade grade = DRPGGuardGrade.Miss;
            if (hasGuardPress)
            {
                float lead = Math.Max(0f, impactTime - guardPressTime);
                grade = lead - perfectLeadSeconds <= BoundaryTolerance
                    ? DRPGGuardGrade.Perfect
                    : DRPGGuardGrade.Guard;
            }

            decisions.Add(new DRPGGuardImpactDecision(
                nextImpactIndex,
                impactTime,
                grade));
            nextImpactIndex++;
            hasGuardPress = false;
            guardPressTime = 0f;
        }

        return decisions;
    }

    private void ValidateElapsed(float elapsedSeconds)
    {
        if (elapsedSeconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (elapsedSeconds + BoundaryTolerance < lastElapsed)
        {
            throw new ArgumentOutOfRangeException(
                nameof(elapsedSeconds),
                "Guard timing cannot move backwards.");
        }
    }
}
