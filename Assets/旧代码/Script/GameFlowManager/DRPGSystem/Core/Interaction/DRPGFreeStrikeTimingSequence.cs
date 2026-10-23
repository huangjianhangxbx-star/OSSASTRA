using System;

public sealed class DRPGFreeStrikeTimingSequence
{
    private const float BoundaryTolerance = 0.0001f;
    private readonly float durationSeconds;
    private readonly float cadenceSeconds;
    private float lastElapsed;
    private float nextAcceptedTime;

    public DRPGFreeStrikeTimingSequence(
        float durationSeconds,
        float cadenceSeconds)
    {
        if (durationSeconds <= 0f)
            throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        if (cadenceSeconds <= 0f)
            throw new ArgumentOutOfRangeException(nameof(cadenceSeconds));

        this.durationSeconds = durationSeconds;
        this.cadenceSeconds = cadenceSeconds;
    }

    public bool IsComplete { get; private set; }
    public int AcceptedCount { get; private set; }
    public float Duration => durationSeconds;
    public float Elapsed => lastElapsed;
    public float NormalizedRemaining => Math.Max(
        0f,
        1f - lastElapsed / durationSeconds);

    public bool TryAccept(float elapsedSeconds)
    {
        ValidateElapsed(elapsedSeconds);
        lastElapsed = elapsedSeconds;
        if (IsComplete ||
            elapsedSeconds + BoundaryTolerance >= durationSeconds ||
            elapsedSeconds + BoundaryTolerance < nextAcceptedTime)
        {
            return false;
        }

        AcceptedCount++;
        nextAcceptedTime = elapsedSeconds + cadenceSeconds;
        return true;
    }

    public bool Advance(float elapsedSeconds)
    {
        ValidateElapsed(elapsedSeconds);
        lastElapsed = elapsedSeconds;
        if (elapsedSeconds + BoundaryTolerance >= durationSeconds)
            IsComplete = true;
        return IsComplete;
    }

    private void ValidateElapsed(float elapsedSeconds)
    {
        if (elapsedSeconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (elapsedSeconds + BoundaryTolerance < lastElapsed)
        {
            throw new ArgumentOutOfRangeException(
                nameof(elapsedSeconds),
                "Free-strike timing cannot move backwards.");
        }
    }
}
