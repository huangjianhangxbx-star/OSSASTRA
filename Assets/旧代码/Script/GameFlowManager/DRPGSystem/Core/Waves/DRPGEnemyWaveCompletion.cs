using System;
using System.Collections.Generic;

internal readonly struct WaveTransitionCompletion : IBattleTransactionCompletionPayload
{
    internal WaveTransitionCompletion(WaveTransitionIdentity identity, IReadOnlyList<EnemyDeploymentId> activatedMembers,
        long finalBattleRevision, BattleRuleMutationLease lease)
    {
        if (!identity.IsValid || finalBattleRevision < 0 || lease == null || lease.Owner != BattleTransactionIdentity.ForWave(identity) ||
            activatedMembers == null || activatedMembers.Count == 0) throw new ArgumentException("Completed Wave requires its identity, members, revision and physical lease.");
        var copy = new EnemyDeploymentId[activatedMembers.Count];
        var seen = new HashSet<EnemyDeploymentId>();
        for (int i=0;i<copy.Length;i++)
        {
            var member=activatedMembers[i];
            if (string.IsNullOrEmpty(member.EncounterId) || !seen.Add(member)) throw new ArgumentException("Wave activation requires unique deployment identities.");
            copy[i]=member;
        }
        Identity=identity; ActivatedMembers=Array.AsReadOnly(copy); FinalBattleRevision=finalBattleRevision; Lease=lease;
        unchecked
        {
            ulong hash=14695981039346656037UL;
            hash=(hash^(ulong)identity.BattleGeneration)*1099511628211UL;
            hash=(hash^(ulong)identity.TransitionId)*1099511628211UL;
            hash=(hash^(ulong)identity.WaveId)*1099511628211UL;
            hash=(hash^(ulong)finalBattleRevision)*1099511628211UL;
            foreach(var member in copy)
            {
                foreach(char ch in member.EncounterId) hash=(hash^ch)*1099511628211UL;
                hash=(hash^0UL)*1099511628211UL;
                hash=(hash^(ulong)member.AuthoredOrdinal)*1099511628211UL;
            }
            Fingerprint=hash==0?1UL:hash;
        }
        Departures=new DRPGBattleDepartureJournal(BattleTransactionIdentity.ForWave(identity),Fingerprint,Array.Empty<DRPGBattleDepartureMember>());
    }
    public WaveTransitionIdentity Identity { get; }
    public BattleTransactionIdentity Owner => Identity.IsValid ? BattleTransactionIdentity.ForWave(Identity) : default;
    public BattleTransactionCompletionKind Kind => BattleTransactionCompletionKind.WaveTransition;
    public IReadOnlyList<EnemyDeploymentId> ActivatedMembers { get; }
    public long FinalBattleRevision { get; }
    public DRPGBattleDepartureJournal Departures { get; }
    public ulong Fingerprint { get; }
    internal BattleRuleMutationLease Lease { get; }
    internal bool HasSameValues(in WaveTransitionCompletion other)
    {
        if (Identity!=other.Identity || FinalBattleRevision!=other.FinalBattleRevision || Fingerprint!=other.Fingerprint ||
            !ReferenceEquals(Lease,other.Lease) || ActivatedMembers==null || other.ActivatedMembers==null ||
            ActivatedMembers.Count!=other.ActivatedMembers.Count || Departures==null || !Departures.HasSameValues(other.Departures)) return false;
        for(int i=0;i<ActivatedMembers.Count;i++) if(!ActivatedMembers[i].Equals(other.ActivatedMembers[i])) return false;
        return true;
    }
}
