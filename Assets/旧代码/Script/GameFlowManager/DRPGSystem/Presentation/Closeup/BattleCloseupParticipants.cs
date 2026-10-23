using System;
using System.Collections.Generic;

internal sealed class BattleCloseupParticipants
{
    private readonly ActionIdentity owner;
    private readonly List<ParticipantId> ordered = new List<ParticipantId>();
    private readonly HashSet<ParticipantId> members = new HashSet<ParticipantId>();

    internal BattleCloseupParticipants(ActionIdentity owner, ParticipantId actor, ParticipantId primaryTarget)
    {
        if (!owner.IsValid) throw new ArgumentException("A valid action owner is required.", nameof(owner));
        this.owner = owner;
        Add(actor);
        Add(primaryTarget);
    }

    internal int Count => ordered.Count;
    internal IReadOnlyList<ParticipantId> Ordered => ordered;
    internal bool Contains(ParticipantId participant) => members.Contains(participant);

    internal bool IncludeReaction(ActionIdentity action, ParticipantId participant)
    {
        if (action != owner || participant.Value <= 0 || members.Contains(participant)) return false;
        Add(participant);
        return true;
    }

    private void Add(ParticipantId participant)
    {
        if (participant.Value <= 0) throw new ArgumentException("Participant is required.", nameof(participant));
        if (!members.Add(participant)) return;
        ordered.Add(participant);
    }
}
