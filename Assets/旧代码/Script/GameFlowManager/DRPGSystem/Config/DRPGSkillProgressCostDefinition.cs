using System;
using UnityEngine;

[Serializable]
public sealed class DRPGSkillProgressCostDefinition
{
    [SerializeField] private DRPGProgressResourceSO resource;
    [SerializeField] private string resourceId;
    [Min(0f)]
    [SerializeField] private float amount;

    public DRPGSkillProgressCostDefinition(
        DRPGProgressResourceSO resource,
        float amount)
    {
        this.resource = resource;
        resourceId = resource != null ? resource.ResourceId : string.Empty;
        this.amount = amount;
    }

    public DRPGProgressResourceSO Resource => resource;
    public string ResourceId => resource != null
        ? resource.ResourceId
        : resourceId != null ? resourceId.Trim() : string.Empty;
    public float Amount => Mathf.Max(0f, amount);
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ResourceId) && Amount > 0f;

    public DRPGResolvedProgressCost Freeze()
    {
        return IsConfigured
            ? new DRPGResolvedProgressCost(ResourceId, Amount)
            : DRPGResolvedProgressCost.None;
    }
}

public sealed class DRPGResolvedProgressCost
{
    public static readonly DRPGResolvedProgressCost None =
        new DRPGResolvedProgressCost(string.Empty, 0f, false);

    public DRPGResolvedProgressCost(string resourceId, float amount)
        : this(resourceId, amount, true)
    {
    }

    private DRPGResolvedProgressCost(
        string resourceId,
        float amount,
        bool validate)
    {
        if (validate && string.IsNullOrWhiteSpace(resourceId))
            throw new ArgumentException("Progress cost resource ID is required.", nameof(resourceId));
        if (amount < 0f || float.IsNaN(amount) || float.IsInfinity(amount))
            throw new ArgumentOutOfRangeException(nameof(amount));
        ResourceId = resourceId != null ? resourceId.Trim() : string.Empty;
        Amount = amount;
    }

    public string ResourceId { get; }
    public float Amount { get; }
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ResourceId) && Amount > 0f;
}
