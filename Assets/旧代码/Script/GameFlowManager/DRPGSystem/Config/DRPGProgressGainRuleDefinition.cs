using System;
using UnityEngine;

public enum DRPGProgressOwnerSelection
{
    Self = 0,
    ExplicitCharacter = 1,
    AnyAlly = 2
}

[Serializable]
public sealed class DRPGProgressGainRuleDefinition
{
    [SerializeField] private string ruleId = "progress_gain";
    [SerializeField] private DRPGProgressResourceSO resource;
    [SerializeField] private DRPGBattleEventKind eventKind =
        DRPGBattleEventKind.ActionImpactResolved;
    [Min(0f)]
    [SerializeField] private float amount = 1f;
    [SerializeField] private DRPGProgressOwnerSelection ownerSelection;
    [SerializeField] private string explicitCharacterId;
    [SerializeField] private bool includeReactionDepth;
    [SerializeField] private bool oncePerRootAction = true;

    public DRPGProgressGainRuleDefinition(
        string ruleId,
        DRPGProgressResourceSO resource,
        DRPGBattleEventKind eventKind,
        float amount,
        DRPGProgressOwnerSelection ownerSelection,
        string explicitCharacterId = "",
        bool includeReactionDepth = false,
        bool oncePerRootAction = true)
    {
        this.ruleId = ruleId;
        this.resource = resource;
        this.eventKind = eventKind;
        this.amount = amount;
        this.ownerSelection = ownerSelection;
        this.explicitCharacterId = explicitCharacterId;
        this.includeReactionDepth = includeReactionDepth;
        this.oncePerRootAction = oncePerRootAction;
        ValidateOrThrow();
    }

    public string RuleId => ruleId != null ? ruleId.Trim() : string.Empty;
    public DRPGProgressResourceSO Resource => resource;
    public string ResourceId => resource != null ? resource.ResourceId : string.Empty;
    public DRPGBattleEventKind EventKind => eventKind;
    public float Amount => Mathf.Max(0f, amount);
    public DRPGProgressOwnerSelection OwnerSelection => ownerSelection;
    public string ExplicitCharacterId => explicitCharacterId != null
        ? explicitCharacterId.Trim()
        : string.Empty;
    public bool IncludeReactionDepth => includeReactionDepth;
    public bool OncePerRootAction => oncePerRootAction;

    public void ValidateOrThrow()
    {
        if (string.IsNullOrWhiteSpace(RuleId))
            throw new InvalidOperationException("Progress gain rule ID is required.");
        if (resource == null || string.IsNullOrWhiteSpace(ResourceId))
            throw new InvalidOperationException("Progress gain rule resource is required: " + RuleId);
        if (!(amount > 0f) || float.IsNaN(amount) || float.IsInfinity(amount))
            throw new InvalidOperationException("Progress gain amount is invalid: " + RuleId);
        if (ownerSelection == DRPGProgressOwnerSelection.ExplicitCharacter &&
            string.IsNullOrWhiteSpace(ExplicitCharacterId))
        {
            throw new InvalidOperationException(
                "Explicit progress owner is required: " + RuleId);
        }
    }
}

public sealed class DRPGConfiguredProgressRuleBinding
{
    public DRPGConfiguredProgressRuleBinding(
        string sourceSkillId,
        DRPGProgressGainRuleDefinition rule)
    {
        if (string.IsNullOrWhiteSpace(sourceSkillId))
            throw new ArgumentException("Source skill ID is required.", nameof(sourceSkillId));
        SourceSkillId = sourceSkillId.Trim();
        Rule = rule ?? throw new ArgumentNullException(nameof(rule));
        Rule.ValidateOrThrow();
    }

    public string SourceSkillId { get; }
    public DRPGProgressGainRuleDefinition Rule { get; }
}
