using System;
using System.Collections.Generic;

// A read-only view of an already committed settlement. Children stay next to
// their causal root; their actual participant sides remain in the sealed ledger.
internal sealed class DRPGSettlementPresentationPlan
{
    internal DRPGSettlementPresentationPlan(DRPGBattleSettlementLedger ledger)
    {
        Ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        var steps = new List<DRPGSettlementPresentationStep>();
        foreach (var group in ledger.Groups)
        {
            steps.Add(new DRPGSettlementPresentationStep(group.Root, null));
            foreach (var child in group.Children)
                if (child.RequiresPresentation)
                    steps.Add(new DRPGSettlementPresentationStep(group.Root, child));
        }
        Steps = Array.AsReadOnly(steps.ToArray());
    }

    internal DRPGBattleSettlementLedger Ledger { get; }
    internal IReadOnlyList<DRPGSettlementPresentationStep> Steps { get; }
    internal bool IsEmpty => Steps.Count == 0;
}

internal sealed class DRPGSettlementPresentationStep
{
    internal DRPGSettlementPresentationStep(DRPGSettlementStatusStep root, DRPGReactionLedgerEntry reaction)
    { Root = root; Reaction = reaction; }

    internal DRPGSettlementStatusStep Root { get; }
    internal DRPGSettlementStatusStep Status => Reaction == null ? Root : null;
    internal DRPGReactionLedgerEntry Reaction { get; }
    internal DRPGBattleSide Lane => Root.Lane;
    internal string EventId => Reaction != null ? Reaction.EventId : Root.EventId;
}
