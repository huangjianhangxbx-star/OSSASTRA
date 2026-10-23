using System;
using System.Collections.Generic;

// Consumes only entries already sealed by the shared continuation Inbox.
// Rule HP and the live roster are deliberately not inputs to this barrier.
internal sealed class DRPGEnemyWaveDepartureLedger
{
    private readonly HashSet<ParticipantId> members = new HashSet<ParticipantId>();
    private readonly HashSet<ParticipantId> departed = new HashSet<ParticipantId>();
    private readonly Dictionary<BattleTransactionIdentity, DRPGBattleContinuationInbox.Entry> observations =
        new Dictionary<BattleTransactionIdentity, DRPGBattleContinuationInbox.Entry>();
    private readonly long generation, activationRevision;
    internal DRPGEnemyWaveDepartureLedger(in WaveTransitionCompletion activation,
        IReadOnlyDictionary<EnemyDeploymentId, ParticipantId> runtimeIdentities)
    {
        if (!activation.Identity.IsValid || activation.ActivatedMembers == null || activation.ActivatedMembers.Count == 0 || runtimeIdentities == null)
            throw new ArgumentException("The departure barrier requires a sealed activation and authored runtime mapping.");
        generation = activation.Owner.BattleGeneration;
        activationRevision = activation.FinalBattleRevision;
        foreach (var id in activation.ActivatedMembers)
            if (!runtimeIdentities.TryGetValue(id, out var runtime) || runtime.Value <= 0 || !members.Add(runtime))
                throw new ArgumentException("Every activated member requires one unique runtime identity.");
    }
    internal bool AllDeparted => departed.Count == members.Count;
    internal bool TryObserve(DRPGBattleContinuationInbox.Entry entry, out string error)
    {
        error = string.Empty;
        // Other precursor facets must supply their own immutable final revision
        // before registration here; never recover missing facts from live State.
        if (entry == null || (!entry.IsRoot && !entry.IsTurnEntry && !entry.IsLegacy))
        { error = "This departure source has no registered complete immutable facts."; return false; }
        var owner = entry.IsRoot ? entry.Root.Owner : entry.IsLegacy ? entry.Legacy.Owner : entry.TurnEntry.Owner;
        long revision = entry.IsRoot ? entry.Root.FinalBattleRevision : entry.IsLegacy ? entry.Legacy.FinalBattleRevision : entry.TurnEntry.FinalBattleRevision;
        var departures = entry.IsRoot ? entry.Root.Departures : entry.IsLegacy ? entry.Legacy.Departures : entry.TurnEntry.Departures;
        if (owner.BattleGeneration != generation || revision < activationRevision ||
            departures == null || departures.Owner != owner)
        { error = "Departure belongs to another battle or predates this wave activation."; return false; }
        if (observations.TryGetValue(owner, out var previous))
        {
            bool same = entry.IsRoot
                ? previous.IsRoot && previous.Root.HasSameValues(entry.Root)
                : entry.IsLegacy ? previous.IsLegacy && previous.Legacy.HasSameValues(entry.Legacy)
                : previous.IsTurnEntry && previous.TurnEntry.HasSameValues(entry.TurnEntry);
            if (!same) error = "A sealed departure source changed its facts.";
            return same;
        }
        // Validate the whole journal before changing any membership. A single
        // foreign enemy cannot partially retire otherwise valid peers.
        foreach (var member in departures.Members)
            if ((member.Side == DRPGBattleSide.Enemy) != members.Contains(member.Participant))
            { error = "Departure is not a member of this exact activated wave."; return false; }
        foreach (var member in departures.Members)
            if (member.Side == DRPGBattleSide.Enemy) departed.Add(member.Participant);
        observations.Add(owner, entry);
        return true;
    }
}
