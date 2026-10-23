/// <summary>
/// Node-local lifecycle for one authored encounter point.
/// </summary>
public sealed class DRPGEncounterPointState
{
    private bool occupied;
    private bool mustLeaveBeforeRetry;

    public bool Completed { get; private set; }
    public bool Occupied => occupied;
    public bool MustLeaveBeforeRetry => mustLeaveBeforeRetry;

    public bool TryEnter(bool valid)
    {
        if (!valid || Completed || occupied || mustLeaveBeforeRetry)
            return false;
        occupied = true;
        return true;
    }

    public void Finish(bool victory)
    {
        if (!occupied)
            return;
        occupied = false;
        if (victory)
        {
            Completed = true;
            mustLeaveBeforeRetry = false;
        }
        else
        {
            mustLeaveBeforeRetry = true;
        }
    }

    public void Leave()
    {
        occupied = false;
        if (!Completed)
            mustLeaveBeforeRetry = false;
    }
}

/// <summary>
/// One deterministic sample of a party root against an authored encounter band.
/// </summary>
public readonly struct DRPGEncounterPointSample
{
    public DRPGEncounterPointSample(bool shouldEnter, bool left, bool isInside)
    {
        ShouldEnter = shouldEnter;
        Left = left;
        IsInside = isInside;
    }

    public bool ShouldEnter { get; }
    public bool Left { get; }
    public bool IsInside { get; }
}

/// <summary>
/// Samples queue-root movement so a thin authored encounter point cannot be
/// skipped between rendered frames. It owns no battle or completion state.
/// </summary>
public sealed class DRPGEncounterPointTraversal
{
    private bool hasPrevious;
    private bool contactedLastSample;
    private float previousX;

    public DRPGEncounterPointSample Sample(
        float currentX,
        float centerX,
        float halfWidth)
    {
        if (!float.IsFinite(currentX) || !float.IsFinite(centerX) ||
            !float.IsFinite(halfWidth) || halfWidth < 0f)
        {
            return new DRPGEncounterPointSample(false, false, false);
        }

        float minimum = centerX - halfWidth;
        float maximum = centerX + halfWidth;
        bool inside = currentX >= minimum && currentX <= maximum;
        bool crossed = hasPrevious &&
            ((previousX < minimum && currentX > maximum) ||
             (previousX > maximum && currentX < minimum));
        bool contacted = inside || crossed;
        bool shouldEnter = contacted && !contactedLastSample;
        bool left = !contacted && contactedLastSample;

        previousX = currentX;
        hasPrevious = true;
        contactedLastSample = contacted;
        return new DRPGEncounterPointSample(shouldEnter, left, inside);
    }
}
