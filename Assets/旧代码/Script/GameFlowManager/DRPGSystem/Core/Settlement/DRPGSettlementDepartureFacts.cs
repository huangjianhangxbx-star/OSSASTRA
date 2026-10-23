using System;
using System.Collections.Generic;

// Death membership is read from sealed facts, never a filtered live roster.
internal static class DRPGSettlementDepartureFacts
{
    internal static DRPGBattleDepartureJournal Describe(DRPGBattleSettlementLedger ledger)
    {
        if (ledger == null) throw new ArgumentNullException(nameof(ledger));
        var ids = new HashSet<ParticipantId>();
        foreach (var group in ledger.Groups)
        {
            if (group.Root.RuleDefeated) ids.Add(group.Root.Owner);
            foreach (var child in group.Children)
                if (child.Batch != null)
                    foreach (var hit in child.Batch.Hits)
                        if (hit.WasDefeated) ids.Add(new ParticipantId(hit.TargetRuntimeId));
        }
        var members = new List<DRPGBattleDepartureMember>();
        foreach (var id in ids)
        {
            if (!ledger.ParticipantSides.TryGetValue(id, out var side))
                throw new InvalidOperationException("Settlement death lacks a frozen participant side.");
            members.Add(new DRPGBattleDepartureMember(id, side));
        }
        return new DRPGBattleDepartureJournal(ledger.Owner, ledger.Fingerprint, members,
            ledger.Owner.Kind == BattleTransactionKind.TurnEntry ? DRPGBattleActionStage.TurnBegin : DRPGBattleActionStage.Settlement);
    }
}
