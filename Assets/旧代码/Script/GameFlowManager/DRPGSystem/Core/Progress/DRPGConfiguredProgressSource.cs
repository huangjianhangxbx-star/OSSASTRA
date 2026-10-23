using System;
using System.Collections.Generic;

public sealed class DRPGConfiguredProgressSource : IDRPGBattleProgressSource
{
    private readonly DRPGConfiguredProgressRuleBinding[] bindings;

    public DRPGConfiguredProgressSource(
        string sourceId,
        IEnumerable<DRPGConfiguredProgressRuleBinding> bindings)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
            throw new ArgumentException("Progress source ID is required.", nameof(sourceId));
        SourceId = sourceId.Trim();
        var collected = new List<DRPGConfiguredProgressRuleBinding>();
        if (bindings != null)
        {
            foreach (DRPGConfiguredProgressRuleBinding binding in bindings)
            {
                if (binding == null)
                    throw new ArgumentException("Progress bindings cannot contain null.", nameof(bindings));
                collected.Add(binding);
            }
        }
        collected.Sort((left, right) =>
        {
            int skill = string.CompareOrdinal(left.SourceSkillId, right.SourceSkillId);
            return skill != 0
                ? skill
                : string.CompareOrdinal(left.Rule.RuleId, right.Rule.RuleId);
        });
        this.bindings = collected.ToArray();
    }

    public string SourceId { get; }

    internal IDRPGBattleProgressSource FreezeForAction() => new FrozenSource(SourceId, bindings);

    private sealed class FrozenSource : IDRPGBattleProgressSource
    {
        private readonly RuleSnapshot[] rules;
        internal FrozenSource(string id, DRPGConfiguredProgressRuleBinding[] bindings)
        {
            SourceId = id; rules = new RuleSnapshot[bindings.Length];
            for (int i = 0; i < rules.Length; i++) rules[i] = new RuleSnapshot(bindings[i]);
        }
        public string SourceId { get; }
        public void Collect(DRPGBattleProgressContext context, ICollection<DRPGProgressMutationRequest> requests)
        {
            foreach (var fact in context.Batch.Events)
            foreach (var rule in rules)
            {
                if (fact.Kind != rule.EventKind || fact.SkillId != rule.Skill) continue;
                string owner = rule.Owner == DRPGProgressOwnerSelection.Self ? context.ResolveOwnerStableId(fact.SourceActorRuntimeId) :
                    rule.Owner == DRPGProgressOwnerSelection.AnyAlly ? context.ResolveFirstAllyOwnerStableId(fact.SourceActorRuntimeId) :
                    context.IsKnownOwner(rule.ExplicitOwner) ? rule.ExplicitOwner : string.Empty;
                if (string.IsNullOrEmpty(owner)) continue;
                string occurrence = rule.Once ? "root:" + fact.RootActionId : "event:" + fact.EventId;
                requests.Add(DRPGProgressMutationRequest.Gain(SourceId, rule.Id + ":" + occurrence + ":" + owner,
                    0, owner, rule.Resource, rule.Scope, rule.Amount, rule.IncludeReaction));
            }
        }
    }

    private sealed class RuleSnapshot
    {
        internal RuleSnapshot(DRPGConfiguredProgressRuleBinding binding)
        {
            var rule = binding.Rule; rule.ValidateOrThrow();
            if (!Enum.IsDefined(typeof(DRPGProgressOwnerSelection), rule.OwnerSelection) ||
                !Enum.IsDefined(typeof(DRPGBattleEventKind), rule.EventKind))
                throw new InvalidOperationException("Configured progress has an invalid event or owner selector.");
            Skill = binding.SourceSkillId; Id = rule.RuleId; Resource = rule.ResourceId; Scope = rule.Resource.Scope;
            EventKind = rule.EventKind; Amount = rule.Amount; Owner = rule.OwnerSelection; ExplicitOwner = rule.ExplicitCharacterId;
            IncludeReaction = rule.IncludeReactionDepth; Once = rule.OncePerRootAction;
        }
        internal readonly string Skill, Id, Resource, ExplicitOwner;
        internal readonly DRPGProgressScope Scope;
        internal readonly DRPGBattleEventKind EventKind;
        internal readonly float Amount;
        internal readonly DRPGProgressOwnerSelection Owner;
        internal readonly bool IncludeReaction, Once;
    }

    public void Collect(
        DRPGBattleProgressContext context,
        ICollection<DRPGProgressMutationRequest> requests)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        if (requests == null)
            throw new ArgumentNullException(nameof(requests));

        for (int eventIndex = 0; eventIndex < context.Batch.Events.Count; eventIndex++)
        {
            DRPGBattleEvent battleEvent = context.Batch.Events[eventIndex];
            for (int bindingIndex = 0; bindingIndex < bindings.Length; bindingIndex++)
            {
                DRPGConfiguredProgressRuleBinding binding = bindings[bindingIndex];
                DRPGProgressGainRuleDefinition rule = binding.Rule;
                if (battleEvent.Kind != rule.EventKind ||
                    !string.Equals(
                        battleEvent.SkillId,
                        binding.SourceSkillId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                string owner = ResolveOwner(context, battleEvent, rule);
                if (string.IsNullOrWhiteSpace(owner))
                    continue;
                string occurrence = rule.OncePerRootAction
                    ? "root:" + battleEvent.RootActionId
                    : "event:" + battleEvent.EventId;
                requests.Add(DRPGProgressMutationRequest.Gain(
                    SourceId,
                    rule.RuleId + ":" + occurrence + ":" + owner,
                    0,
                    owner,
                    rule.ResourceId,
                    rule.Resource.Scope,
                    rule.Amount,
                    rule.IncludeReactionDepth));
            }
        }
    }

    private static string ResolveOwner(
        DRPGBattleProgressContext context,
        DRPGBattleEvent battleEvent,
        DRPGProgressGainRuleDefinition rule)
    {
        switch (rule.OwnerSelection)
        {
            case DRPGProgressOwnerSelection.Self:
                return context.ResolveOwnerStableId(
                    battleEvent.SourceActorRuntimeId);
            case DRPGProgressOwnerSelection.ExplicitCharacter:
                return context.IsKnownOwner(rule.ExplicitCharacterId)
                    ? rule.ExplicitCharacterId
                    : string.Empty;
            case DRPGProgressOwnerSelection.AnyAlly:
                return context.ResolveFirstAllyOwnerStableId(
                    battleEvent.SourceActorRuntimeId);
            default:
                return string.Empty;
        }
    }
}
