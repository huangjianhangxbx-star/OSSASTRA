using System;
using UnityEngine;

[CreateAssetMenu(
    fileName = "DRPGProgressResource",
    menuName = "ANKALUTE/DRPG/Progress Resource")]
public sealed class DRPGProgressResourceSO : ScriptableObject
{
    [SerializeField] private string resourceId = "battle_resource";
    [SerializeField] private string displayName = "Battle Resource";
    [SerializeField] private DRPGProgressScope scope = DRPGProgressScope.Battle;
    [Min(0f)]
    [SerializeField] private float baseCapacity;
    [Min(0f)]
    [SerializeField] private float initialValue;

    public string ResourceId => resourceId != null ? resourceId.Trim() : string.Empty;
    public string DisplayName => displayName ?? string.Empty;
    public DRPGProgressScope Scope => scope;
    public float BaseCapacity => Mathf.Max(0f, baseCapacity);
    public float InitialValue => Mathf.Max(0f, initialValue);

    public DRPGProgressResourceDefinition CreateDefinition(
        float capacityModifier = 0f)
    {
        ValidateOrThrow();
        float capacity = BaseCapacity + capacityModifier;
        if (capacity < 0f || float.IsNaN(capacity) || float.IsInfinity(capacity))
            throw new InvalidOperationException("Resolved progress capacity is invalid: " + ResourceId);
        if (InitialValue > capacity)
        {
            throw new InvalidOperationException(
                "Progress initial value exceeds resolved capacity: " + ResourceId);
        }
        return new DRPGProgressResourceDefinition(
            ResourceId,
            scope,
            BaseCapacity,
            InitialValue).WithEquipmentCapacityModifier(capacityModifier);
    }

    public void ValidateOrThrow()
    {
        if (string.IsNullOrWhiteSpace(resourceId))
            throw new InvalidOperationException("Progress resource ID is required.");
        if (scope != DRPGProgressScope.Battle)
        {
            throw new InvalidOperationException(
                "P4 progress resources must use Battle scope: " + ResourceId);
        }
        if (baseCapacity < 0f || float.IsNaN(baseCapacity) ||
            float.IsInfinity(baseCapacity) || initialValue < 0f ||
            float.IsNaN(initialValue) || float.IsInfinity(initialValue) ||
            initialValue > baseCapacity)
        {
            throw new InvalidOperationException(
                "Progress resource values are invalid: " + ResourceId);
        }
    }
}

[Serializable]
public sealed class DRPGProgressCapacityModifierDefinition
{
    [SerializeField] private DRPGProgressResourceSO resource;
    [SerializeField] private string resourceId;
    [SerializeField] private float capacityModifier;

    public DRPGProgressCapacityModifierDefinition(
        DRPGProgressResourceSO resource,
        float capacityModifier)
    {
        this.resource = resource;
        resourceId = resource != null ? resource.ResourceId : string.Empty;
        this.capacityModifier = capacityModifier;
    }

    public DRPGProgressResourceSO Resource => resource;
    public string ResourceId => resource != null
        ? resource.ResourceId
        : resourceId != null ? resourceId.Trim() : string.Empty;
    public float CapacityModifier => capacityModifier;

    public DRPGResolvedProgressCapacityModifier Freeze()
    {
        if (string.IsNullOrWhiteSpace(ResourceId) ||
            float.IsNaN(capacityModifier) || float.IsInfinity(capacityModifier))
        {
            throw new InvalidOperationException(
                "Progress capacity modifier requires a stable resource and finite value.");
        }
        return new DRPGResolvedProgressCapacityModifier(
            ResourceId,
            capacityModifier);
    }
}

public sealed class DRPGResolvedProgressCapacityModifier
{
    public DRPGResolvedProgressCapacityModifier(
        string resourceId,
        float capacityModifier)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
            throw new ArgumentException("Resource ID is required.", nameof(resourceId));
        if (float.IsNaN(capacityModifier) || float.IsInfinity(capacityModifier))
            throw new ArgumentOutOfRangeException(nameof(capacityModifier));
        ResourceId = resourceId.Trim();
        CapacityModifier = capacityModifier;
    }

    public string ResourceId { get; }
    public float CapacityModifier { get; }
}
