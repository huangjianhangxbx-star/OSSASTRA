using System;
using System.Threading;

internal interface IDRPGBattleGenerationSource
{
    long NextGeneration();
}

internal sealed class DRPGBattleActionIdentityReservation : IDisposable
{
    private readonly DRPGBattleIdentityAuthority authority;
    private bool isClosed;

    internal DRPGBattleActionIdentityReservation(
        DRPGBattleIdentityAuthority authority,
        ActorTurnToken turn,
        long candidateActionId)
    {
        this.authority = authority ??
            throw new ArgumentNullException(nameof(authority));
        Turn = turn;
        CandidateActionId = candidateActionId;
    }

    internal ActorTurnToken Turn { get; }
    internal long CandidateActionId { get; }

    internal ActionIdentity Commit()
    {
        if (isClosed)
            throw new InvalidOperationException("Action identity reservation is closed.");
        ActionIdentity identity = authority.Commit(this);
        isClosed = true;
        return identity;
    }

    public void Dispose()
    {
        if (isClosed)
            return;
        authority.Cancel(this);
        isClosed = true;
    }
}

internal sealed class DRPGProcessBattleGenerationSource : IDRPGBattleGenerationSource
{
    private static readonly DRPGProcessBattleGenerationSource instance =
        new DRPGProcessBattleGenerationSource();
    private static long nextGeneration;

    private DRPGProcessBattleGenerationSource()
    {
    }

    internal static DRPGProcessBattleGenerationSource Instance => instance;

    public long NextGeneration() => Interlocked.Increment(ref nextGeneration);
}

internal sealed class DRPGBattleIdentityAuthority
{
    private readonly object actionReservationGate = new object();
    private long nextBattleStartSerial;
    private long nextTurnSerial;
    private long nextSelectionSerial;
    private long nextActionSerial;
    private long nextWaveTransitionSerial;
    private ActorTurnToken currentTurnToken;
    private TurnEntryIdentity pendingTurnEntry;
    private BattleRuleMutationLease activeTurnEntryLease;
    private DRPGBattleActionIdentityReservation activeActionReservation;

    internal DRPGBattleIdentityAuthority(long battleGeneration)
    {
        if (battleGeneration <= 0L)
            throw new ArgumentOutOfRangeException(nameof(battleGeneration));
        BattleGeneration = battleGeneration;
    }

    internal long BattleGeneration { get; }

    internal BattleStartIdentity AllocateBattleStart()
    {
        return new BattleStartIdentity(
            BattleGeneration,
            Interlocked.Increment(ref nextBattleStartSerial));
    }

    internal ActorTurnToken AllocateTurn(ParticipantId actor)
    {
        if (actor.Value <= 0)
            throw new ArgumentException("Actor identity is required.", nameof(actor));
        currentTurnToken = new ActorTurnToken(
            BattleGeneration,
            Interlocked.Increment(ref nextTurnSerial),
            actor);
        pendingTurnEntry = default;
        activeTurnEntryLease = null;
        return currentTurnToken;
    }

    internal TargetSelectionToken AllocateSelection(ActorTurnToken turn)
    {
        ValidateTurn(turn);
        return new TargetSelectionToken(
            BattleGeneration,
            turn.TurnSerial,
            Interlocked.Increment(ref nextSelectionSerial),
            turn.Actor);
    }

    internal ActionIdentity AllocateAction(ActorTurnToken turn)
    {
        using (DRPGBattleActionIdentityReservation reservation =
            ReserveAction(turn))
        {
            return reservation.Commit();
        }
    }

    internal DRPGBattleActionIdentityReservation ReserveAction(
        ActorTurnToken turn)
    {
        ValidateTurn(turn);
        lock (actionReservationGate)
        {
            if (activeActionReservation != null)
            {
                throw new InvalidOperationException(
                    "Another action identity reservation is active.");
            }

            activeActionReservation =
                new DRPGBattleActionIdentityReservation(
                    this,
                    turn,
                    nextActionSerial + 1L);
            return activeActionReservation;
        }
    }

    internal ActionIdentity Commit(
        DRPGBattleActionIdentityReservation reservation)
    {
        lock (actionReservationGate)
        {
            if (reservation == null ||
                !ReferenceEquals(activeActionReservation, reservation))
            {
                throw new InvalidOperationException(
                    "Only the active action identity reservation can commit.");
            }
            ValidateTurn(reservation.Turn);
            if (reservation.CandidateActionId != nextActionSerial + 1L)
            {
                throw new InvalidOperationException(
                    "Action identity reservation is no longer consecutive.");
            }

            nextActionSerial = reservation.CandidateActionId;
            activeActionReservation = null;
            return new ActionIdentity(
                BattleGeneration,
                reservation.Turn.TurnSerial,
                nextActionSerial);
        }
    }

    internal void Cancel(DRPGBattleActionIdentityReservation reservation)
    {
        lock (actionReservationGate)
        {
            if (ReferenceEquals(activeActionReservation, reservation))
                activeActionReservation = null;
        }
    }

    internal TurnEntryIdentity AllocateTurnEntry(ParticipantId candidate)
    {
        if (candidate.Value <= 0)
            throw new ArgumentException(
                "Candidate actor identity is required.",
                nameof(candidate));
        // Candidates reserve the same lifecycle boundary later published by
        // ActorTurnToken. Discarded candidates leave gaps, never a clock rollback.
        pendingTurnEntry = new TurnEntryIdentity(
            BattleGeneration,
            Interlocked.Increment(ref nextTurnSerial),
            candidate);
        return pendingTurnEntry;
    }

    internal bool TryPublishTurnEntry(TurnEntryIdentity entry, out ActorTurnToken turn)
    {
        turn = default;
        if (!entry.IsValid || entry != pendingTurnEntry || !OwnsEntry(entry, activeTurnEntryLease)) return false;
        turn = new ActorTurnToken(BattleGeneration, entry.CandidateSerial, entry.Actor);
        currentTurnToken = turn;
        pendingTurnEntry = default;
        activeTurnEntryLease = null;
        return true;
    }

    internal bool CanPublishTurnEntry(TurnEntryIdentity entry, BattleRuleMutationLease lease) =>
        entry.IsValid && entry == pendingTurnEntry &&
        ReferenceEquals(lease, activeTurnEntryLease) && OwnsEntry(entry, lease);

    internal bool TryDiscardTurnEntry(TurnEntryIdentity entry, BattleRuleMutationLease lease)
    {
        if (!CanPublishTurnEntry(entry, lease)) return false;
        pendingTurnEntry = default;
        activeTurnEntryLease = null;
        currentTurnToken = default;
        return true;
    }

    internal bool TryBeginTurnEntry(TurnEntryIdentity entry, BattleRuleMutationLease lease)
    {
        if (!entry.IsValid || entry != pendingTurnEntry || !OwnsEntry(entry, lease)) return false;
        activeTurnEntryLease = lease;
        currentTurnToken = default;
        return true;
    }

    private static bool OwnsEntry(TurnEntryIdentity entry, BattleRuleMutationLease lease) =>
        lease != null && lease.Owner == BattleTransactionIdentity.ForTurnEntry(entry) &&
        lease.Arbiter.Owns(lease, lease.Owner);

    internal WaveTransitionIdentity AllocateWave(long waveId)
    {
        if (waveId <= 0L)
            throw new ArgumentOutOfRangeException(nameof(waveId));
        return new WaveTransitionIdentity(
            BattleGeneration,
            Interlocked.Increment(ref nextWaveTransitionSerial),
            waveId);
    }

    private void ValidateTurn(ActorTurnToken turn)
    {
        if (!turn.IsValid || turn != currentTurnToken)
        {
            throw new ArgumentException(
                "The turn token is stale or does not belong to this battle " +
                "identity authority.",
                nameof(turn));
        }
    }
}
