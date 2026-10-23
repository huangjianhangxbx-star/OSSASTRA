using System;
using System.Collections.Generic;

internal sealed class DRPGBattleDeparturePresentation
{
    private readonly List<DRPGDepartureViewFact> views = new List<DRPGDepartureViewFact>();
    internal DRPGBattleDeparturePresentation(DRPGBattleDepartureJournal departures, int stepOrdinal = 1)
    {
        Departures = departures;
        Receipt = new PresentationReceipt(departures.Owner, departures.Stage, stepOrdinal);
    }
    internal DRPGBattleDepartureJournal Departures { get; }
    internal PresentationReceipt Receipt { get; }
    internal IReadOnlyList<DRPGDepartureViewFact> Views => views.AsReadOnly();
    internal void RecordView(DRPGDepartureViewFact fact)
    {
        if (Receipt.Completion.IsCompleted) throw new InvalidOperationException("A terminal departure receipt is sealed.");
        views.Add(fact);
    }
}

internal readonly struct DRPGDepartureViewFact
{
    internal DRPGDepartureViewFact(ParticipantId participant, int originalViewInstanceId, int incapacitatedModelInstanceId)
    { Participant = participant; OriginalViewInstanceId = originalViewInstanceId; IncapacitatedModelInstanceId = incapacitatedModelInstanceId; }
    internal ParticipantId Participant { get; }
    internal int OriginalViewInstanceId { get; }
    internal int IncapacitatedModelInstanceId { get; }
}

internal readonly struct DRPGBattleDepartureMember
{
    internal DRPGBattleDepartureMember(ParticipantId participant, DRPGBattleSide side)
    { Participant = participant; Side = side; }
    internal ParticipantId Participant { get; }
    internal DRPGBattleSide Side { get; }
}

internal sealed class DRPGBattleDepartureJournal
{
    internal DRPGBattleDepartureJournal(BattleTransactionIdentity owner, ulong sourceFingerprint,
        IReadOnlyList<DRPGBattleDepartureMember> members, DRPGBattleActionStage stage = DRPGBattleActionStage.Return)
    {
        if (!owner.IsValid) throw new ArgumentException("Departure owner is required.", nameof(owner));
        if (sourceFingerprint == 0) throw new ArgumentOutOfRangeException(nameof(sourceFingerprint));
        if (members == null) throw new ArgumentNullException(nameof(members));
        if (stage != DRPGBattleActionStage.Return && stage != DRPGBattleActionStage.Settlement &&
            !(stage == DRPGBattleActionStage.TurnBegin && owner.Kind == BattleTransactionKind.TurnEntry))
            throw new ArgumentOutOfRangeException(nameof(stage));
        Stage = stage;
        Owner = owner; SourceFingerprint = sourceFingerprint;
        var copy = new DRPGBattleDepartureMember[members.Count];
        var ids = new HashSet<ParticipantId>();
        for (int i = 0; i < copy.Length; i++)
        {
            var member = members[i];
            if (member.Participant.Value <= 0 || !Enum.IsDefined(typeof(DRPGBattleSide), member.Side) || !ids.Add(member.Participant))
                throw new ArgumentException("Departure members require unique identities and valid sides.", nameof(members));
            copy[i] = member;
        }
        Array.Sort(copy, (a, b) => a.Participant.Value.CompareTo(b.Participant.Value));
        Members = Array.AsReadOnly(copy);
    }
    internal BattleTransactionIdentity Owner { get; }
    internal DRPGBattleActionStage Stage { get; }
    internal ulong SourceFingerprint { get; }
    internal IReadOnlyList<DRPGBattleDepartureMember> Members { get; }
    internal bool HasSameValues(DRPGBattleDepartureJournal other)
    {
        if (other == null || !Owner.Equals(other.Owner) || Stage != other.Stage || SourceFingerprint != other.SourceFingerprint || Members.Count != other.Members.Count)
            return false;
        for (int i = 0; i < Members.Count; i++)
            if (Members[i].Participant != other.Members[i].Participant || Members[i].Side != other.Members[i].Side) return false;
        return true;
    }
}

// Rule rows only. World slots and their >=0.30s presentation receipt are a
// separate later barrier; this primitive cannot move a view or choose a turn.
internal static class DRPGPostReturnFormationDraft
{
    internal static IReadOnlyList<DRPGBattleFormationChange> Stage(DRPGBattleMutationWorkspace draft)
    {
        var changes = new List<DRPGBattleFormationChange>();
        foreach (var side in new[] { DRPGBattleSide.Player, DRPGBattleSide.Enemy })
        {
            var occupied = new bool[3];
            foreach (var participant in draft.Snapshot.Participants)
                if (Included(participant, side)) occupied[(int)participant.FormationRow] = true;
            var compacted = new DRPGFormationRow[3]; int next = 0;
            for (int row = 0; row < occupied.Length; row++)
                if (occupied[row]) compacted[row] = (DRPGFormationRow)next++;
            foreach (var participant in draft.Snapshot.Participants)
            {
                if (!Included(participant, side)) continue;
                var target = compacted[(int)participant.FormationRow];
                if (target == participant.FormationRow) continue;
                draft.For(participant.Participant).SetFormationRow(target);
                changes.Add(new DRPGBattleFormationChange(participant.Participant, participant.FormationRow, target));
            }
        }
        changes.Sort((a, b) => a.Participant.Value.CompareTo(b.Participant.Value));
        return changes.AsReadOnly();
    }

    private static bool Included(BattleParticipantRuleSnapshot participant, DRPGBattleSide side) =>
        participant.Side == side && participant.CurrentHp > 0f && participant.IsDeployed &&
        (side == DRPGBattleSide.Enemy || participant.RosterRole == DRPGBattleRosterRole.PartyMember ||
            participant.RosterRole == DRPGBattleRosterRole.Protagonist);
}

internal readonly struct DRPGBattleFormationChange
{
    internal DRPGBattleFormationChange(ParticipantId participant, DRPGFormationRow before, DRPGFormationRow after)
    { Participant = participant; Before = before; After = after; }
    internal ParticipantId Participant { get; }
    internal DRPGFormationRow Before { get; }
    internal DRPGFormationRow After { get; }
}

internal sealed class DRPGBattlePostReturnResult
{
    internal DRPGBattlePostReturnResult(DRPGBattleDepartureJournal departures, long revision,
        IReadOnlyList<DRPGBattleFormationChange> formationChanges)
    {
        Departures = departures; CommittedRevision = revision;
        var copy = new DRPGBattleFormationChange[formationChanges.Count];
        for (int i = 0; i < copy.Length; i++) copy[i] = formationChanges[i];
        FormationChanges = Array.AsReadOnly(copy);
    }
    internal DRPGBattleDepartureJournal Departures { get; }
    internal long CommittedRevision { get; }
    internal IReadOnlyList<DRPGBattleFormationChange> FormationChanges { get; }
}
