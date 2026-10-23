using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

// Frozen facts for later event/presentation adapters. Reading these facts never
// runs a reaction source, mutates rules, or completes a presentation receipt.
internal sealed class DRPGCommittedActionEvent
{
    private DRPGCommittedActionEvent(ActionIdentity identity, int ordinal, string eventId, string parentEventId,
        ulong ledgerFingerprint, DRPGLedgerDisposition disposition, ParticipantId source, string sourceId,
        DRPGBattleHitResult hit, IReadOnlyList<DRPGActionEffectResult> effects, ParticipantId target, string skillId)
    {
        Identity = identity; StepOrdinal = ordinal; EventId = eventId; ParentEventId = parentEventId;
        Disposition = disposition; Source = source; SourceId = sourceId; Hit = hit;
        Effects = effects;
        Target = target;
        SkillId = skillId;
        Fingerprint = DRPGActionResolutionFingerprint.ComputeEvent(ledgerFingerprint, identity, ordinal, eventId);
    }
    internal ActionIdentity Identity { get; }
    internal int StepOrdinal { get; }
    internal string EventId { get; }
    internal string ParentEventId { get; }
    internal ulong Fingerprint { get; }
    internal DRPGBattleHitResult Hit { get; }
    internal DRPGLedgerDisposition Disposition { get; }
    internal bool RequiresPresentation => Disposition == DRPGLedgerDisposition.Presentable;
    internal ParticipantId Source { get; }
    internal ParticipantId Target { get; }
    internal string SourceId { get; }
    internal string SkillId { get; }
    internal IReadOnlyList<DRPGActionEffectResult> Effects { get; }

    internal static IReadOnlyList<DRPGCommittedActionEvent> Build(DRPGBattleActionResolutionLedger ledger)
    {
        var primary = ledger.PrimaryImpact;
        var facts = new List<DRPGCommittedActionEvent>(ledger.Reactions.Count + 1)
        {
            new DRPGCommittedActionEvent(ledger.Identity, 0, primary.EventId, primary.ParentEventId,
                ledger.Fingerprint, primary.Disposition, new ParticipantId(primary.Hit.ActorRuntimeId),
                primary.Hit.SkillId, primary.Hit, primary.Effects, new ParticipantId(primary.Hit.TargetRuntimeId), primary.Hit.SkillId)
        };
        foreach (var reaction in ledger.Reactions)
            facts.Add(new DRPGCommittedActionEvent(ledger.Identity, facts.Count, reaction.EventId, reaction.ParentEventId,
                ledger.Fingerprint, reaction.Disposition, reaction.SourceParticipant, reaction.SourceId,
                reaction.State == DRPGImmediateReactionState.Resolved && reaction.Batch.Hits.Count == 1 ? reaction.Batch.Hits[0] : null,
                reaction.State == DRPGImmediateReactionState.Resolved ? reaction.Batch.Effects : Array.Empty<DRPGActionEffectResult>(), reaction.TargetParticipant, reaction.SkillId));
        return Array.AsReadOnly(facts.ToArray());
    }
}

internal sealed partial class DRPGBattleActionResolutionLedgerStore
{
    internal bool TryGetCommittedActionEvent(ActionIdentity identity, string eventId, ulong fingerprint,
        BattleRuleMutationLease lease, out DRPGCommittedActionEvent fact)
    {
        fact = null;
        if (!identity.IsValid || identity.BattleGeneration != state.BattleGeneration || string.IsNullOrWhiteSpace(eventId) ||
            fingerprint == 0UL || !state.OwnsRuleMutationLease(lease, BattleTransactionIdentity.ForRootAction(identity)))
            return false;
        lock (commitGate)
        {
            if (!committed.TryGetValue(new DRPGPrimaryImpactKey(identity, 0), out var receipt)) return false;
            foreach (var candidate in receipt.Ledger.Events)
            {
                if (candidate.EventId != eventId) continue;
                if (candidate.Fingerprint != fingerprint) return false;
                fact = candidate;
                return true;
            }
            return false;
        }
    }
}

internal static partial class DRPGActionResolutionFingerprint
{
    internal static ulong ComputeEvent(ulong ledgerFingerprint, ActionIdentity identity, int ordinal, string eventId)
    {
        var builder = new StringBuilder(128);
        Append(builder, "committed-action-event-v1");
        Append(builder, ledgerFingerprint.ToString("X16", CultureInfo.InvariantCulture));
        AppendKey(builder, new DRPGPrimaryImpactKey(identity, 0));
        Append(builder, ordinal);
        Append(builder, eventId);
        return Hash(builder);
    }
}
