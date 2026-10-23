using System;

internal readonly struct LegacyActionCompletion : IBattleTransactionCompletionPayload
{
    internal LegacyActionCompletion(ActionIdentity identity, DRPGBattleOutcome outcome, long revision,
        DRPGBattleDepartureJournal departures, BattleRuleMutationLease lease, bool wavePending = false)
    {
        Identity = identity; Outcome = outcome; FinalBattleRevision = revision; Departures = departures; Lease = lease; Fingerprint = 0; WavePending = wavePending;
        if (!identity.IsValid || revision < 0 || departures == null || departures.Owner != Owner ||
            lease == null || lease.Owner != Owner || !Enum.IsDefined(typeof(DRPGBattleOutcome), outcome) || outcome == DRPGBattleOutcome.Aborted ||
            wavePending && outcome != DRPGBattleOutcome.None)
            throw new ArgumentException("Legacy completion requires sealed committed facts and its original lease.");
        unchecked
        {
            ulong hash = ((departures.SourceFingerprint ^ (ulong)revision) * 1099511628211UL) ^ (ulong)outcome;
            hash = (hash ^ (wavePending ? 1UL : 0UL)) * 1099511628211UL;
            foreach (var member in departures.Members)
            { hash = (hash ^ (ulong)member.Participant.Value) * 1099511628211UL; hash = (hash ^ (ulong)member.Side) * 1099511628211UL; }
            Fingerprint = hash;
        }
        if (Fingerprint == 0) Fingerprint = 1;
    }
    internal ActionIdentity Identity { get; }
    internal DRPGBattleOutcome Outcome { get; }
    internal bool WavePending { get; }
    internal long FinalBattleRevision { get; }
    internal BattleRuleMutationLease Lease { get; }
    public BattleTransactionCompletionKind Kind => BattleTransactionCompletionKind.LegacyAction;
    public BattleTransactionIdentity Owner => Identity.IsValid ? BattleTransactionIdentity.ForLegacyAction(Identity) : default;
    public DRPGBattleDepartureJournal Departures { get; }
    public ulong Fingerprint { get; }
    internal bool HasSameValues(in LegacyActionCompletion other) => Identity == other.Identity && Outcome == other.Outcome &&
        WavePending == other.WavePending && FinalBattleRevision == other.FinalBattleRevision && Fingerprint == other.Fingerprint && ReferenceEquals(Lease, other.Lease) &&
        Departures != null && Departures.HasSameValues(other.Departures);
}

internal interface IBattleLegacyContinuationRunner
{
    bool TryPrepare(in LegacyActionCompletion completion, out BattleTransactionIdentity next, out string error);
    void Start(in LegacyActionCompletion completion, BattleRuleMutationLease lease);
}
