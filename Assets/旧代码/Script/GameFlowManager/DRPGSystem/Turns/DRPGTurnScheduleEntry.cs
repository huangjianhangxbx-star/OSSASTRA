public readonly struct DRPGTurnScheduleEntry
{
    public DRPGTurnScheduleEntry(
        BattleParticipantRuntime participant,
        float scheduledTime,
        int priority)
    {
        Participant = participant;
        ScheduledTime = scheduledTime;
        Priority = priority;
    }

    public BattleParticipantRuntime Participant { get; }
    public float ScheduledTime { get; }
    public int Priority { get; }
}

internal enum DRPGTurnCandidateSourceKind
{
    None = 0,
    ExecutableEnemyPlan = 1,
    SchedulerTimeline = 2
}

internal readonly struct DRPGTurnCandidateReservation
{
    internal DRPGTurnCandidateReservation(
        long reservationId,
        DRPGTurnCandidateSourceKind sourceKind,
        DRPGTurnScheduleEntry candidate,
        double reservedLogicalTime)
    {
        if (reservationId <= 0L)
            throw new System.ArgumentOutOfRangeException(nameof(reservationId));
        if (sourceKind == DRPGTurnCandidateSourceKind.None)
            throw new System.ArgumentOutOfRangeException(nameof(sourceKind));
        if (candidate.Participant == null)
            throw new System.ArgumentException(
                "A reserved turn candidate requires a participant.",
                nameof(candidate));
        if (reservedLogicalTime < 0d)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(reservedLogicalTime));
        }

        ReservationId = reservationId;
        SourceKind = sourceKind;
        Candidate = candidate;
        ReservedLogicalTime = reservedLogicalTime;
    }

    internal long ReservationId { get; }
    internal DRPGTurnCandidateSourceKind SourceKind { get; }
    internal DRPGTurnScheduleEntry Candidate { get; }
    internal double ReservedLogicalTime { get; }
    internal bool IsValid => ReservationId > 0L &&
        SourceKind != DRPGTurnCandidateSourceKind.None &&
        Candidate.Participant != null;
}
