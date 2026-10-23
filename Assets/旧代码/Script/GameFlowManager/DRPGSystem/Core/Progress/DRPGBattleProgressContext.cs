using System;
using System.Collections.Generic;

public sealed class DRPGBattleProgressContext
{
    private readonly Dictionary<int, string> runtimeToStableId;
    private readonly Dictionary<int, OwnerSnapshot> runtimeParticipants;
    private readonly HashSet<string> knownOwners;

    public DRPGBattleProgressContext(
        DRPGBattleEventBatch batch,
        IReadOnlyList<BattleParticipantRuntime> participants)
    {
        Batch = batch ?? throw new ArgumentNullException(nameof(batch));
        runtimeToStableId = new Dictionary<int, string>();
        runtimeParticipants = new Dictionary<int, OwnerSnapshot>();
        knownOwners = new HashSet<string>(StringComparer.Ordinal);

        int count = participants != null ? participants.Count : 0;
        for (int i = 0; i < count; i++)
        {
            BattleParticipantRuntime participant = participants[i];
            if (participant == null)
            {
                throw new ArgumentException(
                    "Progress participants cannot contain null.",
                    nameof(participants));
            }

            string stableId = participant.Profile != null
                ? participant.Profile.CombatantId
                : string.Empty;
            if (string.IsNullOrWhiteSpace(stableId))
                continue;
            stableId = stableId.Trim();
            runtimeToStableId.Add(participant.RuntimeId, stableId);
            runtimeParticipants.Add(participant.RuntimeId, new OwnerSnapshot(participant.RuntimeId, participant.Side,
                participant.PartyOrderIndex, participant.IsAlive, participant.IsDeployed));
            knownOwners.Add(stableId);
        }
    }

    public DRPGBattleEventBatch Batch { get; }

    internal DRPGBattleProgressContext(DRPGBattleEventBatch batch, DRPGBattleMutationWorkspace workspace)
    {
        Batch = batch ?? throw new ArgumentNullException(nameof(batch));
        if (workspace == null) throw new ArgumentNullException(nameof(workspace));
        runtimeToStableId = new Dictionary<int, string>();
        runtimeParticipants = new Dictionary<int, OwnerSnapshot>();
        knownOwners = new HashSet<string>(StringComparer.Ordinal);
        foreach (var participant in workspace.Snapshot.Participants)
        {
            if (string.IsNullOrWhiteSpace(participant.OwnerStableId)) continue;
            var staged = workspace.For(participant.Participant);
            runtimeToStableId.Add(participant.Participant.Value, participant.OwnerStableId);
            runtimeParticipants.Add(participant.Participant.Value, new OwnerSnapshot(participant.Participant.Value,
                participant.Side, participant.PartyOrder, staged.HitPoints > 0f, staged.IsDeployed));
            knownOwners.Add(participant.OwnerStableId);
        }
    }

    private sealed class OwnerSnapshot
    {
        internal OwnerSnapshot(int id, DRPGBattleSide side, int order, bool alive, bool deployed)
        { RuntimeId = id; Side = side; PartyOrderIndex = order; IsAlive = alive; IsDeployed = deployed; }
        internal int RuntimeId { get; }
        internal DRPGBattleSide Side { get; }
        internal int PartyOrderIndex { get; }
        internal bool IsAlive { get; }
        internal bool IsDeployed { get; }
    }

    public string ResolveOwnerStableId(int runtimeId)
    {
        if (runtimeToStableId.TryGetValue(runtimeId, out string stableId))
            return stableId;
        throw new InvalidOperationException(
            "No stable progress owner exists for runtime ID " + runtimeId + ".");
    }

    public bool IsKnownOwner(string ownerStableId)
    {
        return !string.IsNullOrWhiteSpace(ownerStableId) &&
            knownOwners.Contains(ownerStableId.Trim());
    }

    public string ResolveFirstAllyOwnerStableId(int sourceRuntimeId)
    {
        if (!runtimeParticipants.TryGetValue(
                sourceRuntimeId,
                out OwnerSnapshot source))
        {
            return string.Empty;
        }

        OwnerSnapshot selected = null;
        foreach (OwnerSnapshot candidate in runtimeParticipants.Values)
        {
            if (candidate.RuntimeId == sourceRuntimeId ||
                candidate.Side != source.Side || !candidate.IsAlive ||
                !candidate.IsDeployed)
            {
                continue;
            }
            if (selected == null ||
                candidate.PartyOrderIndex < selected.PartyOrderIndex ||
                candidate.PartyOrderIndex == selected.PartyOrderIndex &&
                candidate.RuntimeId < selected.RuntimeId)
            {
                selected = candidate;
            }
        }
        return selected != null
            ? ResolveOwnerStableId(selected.RuntimeId)
            : string.Empty;
    }
}
