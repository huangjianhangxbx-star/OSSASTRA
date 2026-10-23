using System;

internal enum DRPGBattleOutcomeCandidate { Continue, Victory, Defeat, WavePending }

internal readonly struct RootActionCompletion : IBattleTransactionCompletionPayload
{
    internal RootActionCompletion(ActionIdentity identity, RootActionTerminal terminal,
        DRPGBattleOutcomeCandidate outcome, long revision, DRPGBattleDepartureJournal departures,
        BattleRuleMutationLease lease)
    {
        var owner = BattleTransactionIdentity.ForRootAction(identity);
        if (!identity.IsValid || revision < 0 || departures == null || departures.Owner != owner ||
            lease == null || lease.Owner != owner || !Enum.IsDefined(typeof(DRPGBattleOutcomeCandidate), outcome) ||
            terminal.Kind == RootActionTerminalKind.Faulted)
            throw new ArgumentException("Root completion requires sealed successful facts and its physical lease.");
        Identity = identity; Terminal = terminal; Outcome = outcome; FinalBattleRevision = revision;
        Departures = departures; Lease = lease;
        unchecked
        {
            ulong hash = 14695981039346656037UL;
            hash = (hash ^ (ulong)identity.BattleGeneration) * 1099511628211UL;
            hash = (hash ^ (ulong)identity.TurnSerial) * 1099511628211UL;
            hash = (hash ^ (ulong)identity.ActionId) * 1099511628211UL;
            hash = (hash ^ (ulong)revision) * 1099511628211UL;
            hash = (hash ^ (ulong)outcome) * 1099511628211UL;
            hash = (hash ^ (ulong)terminal.Kind) * 1099511628211UL;
            foreach (char ch in terminal.Detail ?? string.Empty) hash = (hash ^ ch) * 1099511628211UL;
            hash = (hash ^ departures.SourceFingerprint) * 1099511628211UL;
            foreach (var member in departures.Members)
            { hash = (hash ^ (ulong)member.Participant.Value) * 1099511628211UL; hash = (hash ^ (ulong)member.Side) * 1099511628211UL; }
            Fingerprint = hash == 0 ? 1UL : hash;
        }
    }
    public BattleTransactionCompletionKind Kind => BattleTransactionCompletionKind.RootAction;
    public BattleTransactionIdentity Owner => Identity.IsValid ? BattleTransactionIdentity.ForRootAction(Identity) : default;
    public ActionIdentity Identity { get; }
    public RootActionTerminal Terminal { get; }
    public DRPGBattleOutcomeCandidate Outcome { get; }
    public long FinalBattleRevision { get; }
    public DRPGBattleDepartureJournal Departures { get; }
    public ulong Fingerprint { get; }
    internal BattleRuleMutationLease Lease { get; }
    internal bool HasSameValues(in RootActionCompletion other) => Identity == other.Identity &&
        Terminal.Kind == other.Terminal.Kind && Terminal.Detail == other.Terminal.Detail && Outcome == other.Outcome &&
        FinalBattleRevision == other.FinalBattleRevision && Fingerprint == other.Fingerprint &&
        ReferenceEquals(Lease, other.Lease) && Departures != null && Departures.HasSameValues(other.Departures);

    internal static DRPGBattleOutcomeCandidate DetermineOutcome(BattleRuleSnapshot snapshot)
    {
        bool player = false, enemy = false, reserve = false;
        foreach (var participant in snapshot.Participants)
        {
            if (participant.CurrentHp <= 0) continue;
            if (participant.Side == DRPGBattleSide.Player) player |= participant.IsDeployed;
            else if (participant.IsDeployed) enemy = true;
            else reserve = true;
        }
        return !player ? DRPGBattleOutcomeCandidate.Defeat : enemy ? DRPGBattleOutcomeCandidate.Continue :
            reserve ? DRPGBattleOutcomeCandidate.WavePending : DRPGBattleOutcomeCandidate.Victory;
    }
}
