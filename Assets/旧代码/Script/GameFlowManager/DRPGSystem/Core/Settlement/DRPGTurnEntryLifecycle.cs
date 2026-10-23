// Ledger-facing values contain no live participant or enemy-plan reference.
internal readonly struct DRPGTurnEntryReservationFacts
{
    internal DRPGTurnEntryReservationFacts(DRPGTurnCandidateReservation reservation)
    {
        ReservationId = reservation.ReservationId; SourceKind = reservation.SourceKind;
        Actor = new ParticipantId(reservation.Candidate.Participant.RuntimeId);
        ScheduledTime = reservation.Candidate.ScheduledTime; Priority = reservation.Candidate.Priority;
        LogicalTime = (float)reservation.ReservedLogicalTime;
    }
    internal long ReservationId { get; }
    internal DRPGTurnCandidateSourceKind SourceKind { get; }
    internal ParticipantId Actor { get; }
    internal float ScheduledTime { get; }
    internal int Priority { get; }
    internal float LogicalTime { get; }
}

internal sealed class DRPGTurnEntryLifecycle
{
    internal DRPGTurnEntryLifecycle(ParticipantId actor, bool skipAction, bool alive, bool clearedDefend,
        DRPGPostureRuleSnapshot before, DRPGPostureRuleSnapshot after, float postureBefore, float postureAfter)
    {
        Actor = actor; SkipAction = skipAction; CandidateAlive = alive; ClearedDefend = clearedDefend;
        Before = before; After = after; PostureBefore = postureBefore; PostureAfter = postureAfter;
    }
    internal ParticipantId Actor { get; }
    internal bool SkipAction { get; }
    internal bool CandidateAlive { get; }
    internal bool ClearedDefend { get; }
    internal DRPGPostureRuleSnapshot Before { get; }
    internal DRPGPostureRuleSnapshot After { get; }
    internal float PostureBefore { get; }
    internal float PostureAfter { get; }
}
