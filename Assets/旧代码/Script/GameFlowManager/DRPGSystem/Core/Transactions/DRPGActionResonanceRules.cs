using System;
using System.Collections.Generic;
using System.Text;

internal sealed class DRPGActionResonanceRules
{
    private readonly DRPGBattleResonanceAccess access;
    internal DRPGActionResonanceRules(DRPGBattleResonanceAccess access, long revision, string partner, string skill,
        int capacity, int protagonistId, int partnerId, IReadOnlyList<DRPGResonanceProgressSource> sources)
    {
        this.access = access; Revision = revision; Partner = partner; Skill = skill;
        Capacity = capacity; ProtagonistId = protagonistId; PartnerId = partnerId;
        var rules = new List<Rule>();
        if (sources != null)
            foreach (var source in sources) if (source != null) rules.Add(new Rule(source));
        Rules = Array.AsReadOnly(rules.ToArray());
    }
    internal long Revision { get; }
    internal string Partner { get; }
    internal string Skill { get; }
    internal int Capacity { get; }
    internal int ProtagonistId { get; }
    internal int PartnerId { get; }
    internal IReadOnlyList<Rule> Rules { get; }
    internal bool Matches(DRPGBattleResonanceAccess current) => ReferenceEquals(access, current) && current != null &&
        current.Service.State.Revision == Revision;
    internal void Stage(ActionIdentity identity, IReadOnlyList<DRPGBattleEventBatch> batches, DRPGBattleActionResourceWorkspace workspace)
    {
        if (string.IsNullOrEmpty(Skill) || string.IsNullOrEmpty(Partner) || PartnerId <= 0 || Capacity <= 0) return;
        foreach (var batch in batches)
        foreach (var fact in batch.Events)
        for (int i = 0; i < Rules.Count; i++)
        {
            var rule = Rules[i];
            if (rule.Event != fact.Kind || (rule.Actor == DRPGResonanceProgressActorRule.Protagonist && fact.SourceActorRuntimeId != ProtagonistId) ||
                (rule.Actor == DRPGResonanceProgressActorRule.Partner && fact.SourceActorRuntimeId != PartnerId)) continue;
            if (!workspace.TryStageResonance(identity, "resonance:" + identity + ":" + fact.EventId + ":" + i,
                Partner, Skill, rule.Amount, Capacity, false, out var error)) throw new InvalidOperationException(error);
        }
    }
    internal sealed class Rule
    {
        internal Rule(DRPGResonanceProgressSource source)
        {
            Event = source.EventKind; Actor = source.ActorRule; Amount = source.ProgressAmount;
            if (!Enum.IsDefined(typeof(DRPGBattleEventKind), Event) || !Enum.IsDefined(typeof(DRPGResonanceProgressActorRule), Actor))
                throw new InvalidOperationException("Resonance source contains an invalid event or actor selector.");
        }
        internal DRPGBattleEventKind Event { get; }
        internal DRPGResonanceProgressActorRule Actor { get; }
        internal int Amount { get; }
    }
}

internal static partial class DRPGActionResolutionFingerprint
{
    private static void AppendResonanceRules(StringBuilder builder, DRPGActionResonanceRules rules)
    {
        Append(builder, rules != null ? 1 : 0); if (rules == null) return;
        Append(builder, rules.Revision); Append(builder, rules.Partner); Append(builder, rules.Skill); Append(builder, rules.Capacity);
        Append(builder, rules.ProtagonistId); Append(builder, rules.PartnerId); Append(builder, rules.Rules.Count);
        foreach (var rule in rules.Rules) { Append(builder, (int)rule.Event); Append(builder, (int)rule.Actor); Append(builder, rule.Amount); }
    }
}
