using System;

// Serial is the scheduler's stable actor-turn serial, not the number of attacks
// or a guessed round. Timing disambiguates boundaries within the same actor turn.
public readonly struct DRPGStatusBoundary
{
    public DRPGStatusBoundary(DRPGStatusTriggerTiming timing, long serial, ParticipantId actor)
    {
        if (!Enum.IsDefined(typeof(DRPGStatusTriggerTiming), timing))
            throw new ArgumentOutOfRangeException(nameof(timing));
        if (serial <= 0 || actor.Value <= 0) throw new ArgumentException("A status boundary requires a serial and actor.");
        if (timing == DRPGStatusTriggerTiming.RoundEnd)
            throw new InvalidOperationException("RoundEnd requires a deterministic RoundBoundaryProvider; none is installed.");
        Timing = timing; Serial = serial; Actor = actor;
    }
    public DRPGStatusTriggerTiming Timing { get; }
    public long Serial { get; }
    public ParticipantId Actor { get; }
    internal bool Affects(ParticipantId owner) => Serial > 0 &&
        (Timing == DRPGStatusTriggerTiming.AfterAction || Actor == owner);
}

public readonly struct DRPGStatusBoundaryStep
{
    internal DRPGStatusBoundaryStep(DRPGStatusRuntimeSnapshot before, DRPGStatusRuntimeSnapshot after,
        float hpDelta, float postureDelta)
    { Before = before; After = after; HpDelta = hpDelta; PostureDelta = postureDelta; }
    public DRPGStatusRuntimeSnapshot Before { get; }
    public DRPGStatusRuntimeSnapshot After { get; }
    public float HpDelta { get; }
    public float PostureDelta { get; }
    public bool IsExpired => After.RemainingTurns == 0;
}
