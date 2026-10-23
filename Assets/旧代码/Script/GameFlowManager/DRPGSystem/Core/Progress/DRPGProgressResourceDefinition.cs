using System;

public sealed class DRPGProgressResourceDefinition
{
    public DRPGProgressResourceDefinition(
        string resourceId,
        DRPGProgressScope scope,
        float capacity,
        float initialValue = 0f)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            throw new ArgumentException(
                "A stable progress resource ID is required.",
                nameof(resourceId));
        }
        if (!IsFiniteNonNegative(capacity))
            throw new ArgumentOutOfRangeException(nameof(capacity));
        if (!IsFiniteNonNegative(initialValue) || initialValue > capacity)
            throw new ArgumentOutOfRangeException(nameof(initialValue));

        ResourceId = resourceId.Trim();
        Scope = scope;
        Capacity = capacity;
        BaseCapacity = capacity;
        InitialValue = initialValue;
    }

    public string ResourceId { get; }
    public DRPGProgressScope Scope { get; }
    public float Capacity { get; }
    public float InitialValue { get; }
    internal float BaseCapacity { get; }
    internal float EquipmentCapacityModifier { get; }

    private DRPGProgressResourceDefinition(string resourceId, DRPGProgressScope scope,
        float baseCapacity, float initialValue, float equipmentModifier)
        : this(resourceId, scope, baseCapacity + equipmentModifier, initialValue)
    {
        BaseCapacity = baseCapacity;
        EquipmentCapacityModifier = equipmentModifier;
    }

    internal DRPGProgressResourceDefinition WithEquipmentCapacityModifier(float modifier)
    {
        if (float.IsNaN(modifier) || float.IsInfinity(modifier)) throw new ArgumentOutOfRangeException(nameof(modifier));
        return new DRPGProgressResourceDefinition(ResourceId, Scope, BaseCapacity, InitialValue, modifier);
    }

    internal bool HasSameValues(DRPGProgressResourceDefinition other) => other != null &&
        ResourceId == other.ResourceId && Scope == other.Scope && Capacity == other.Capacity &&
        InitialValue == other.InitialValue && BaseCapacity == other.BaseCapacity &&
        EquipmentCapacityModifier == other.EquipmentCapacityModifier;

    private static bool IsFiniteNonNegative(float value)
    {
        return value >= 0f && !float.IsNaN(value) &&
            !float.IsInfinity(value);
    }
}

public sealed class DRPGProgressSnapshot
{
    public DRPGProgressSnapshot(
        string ownerStableId,
        DRPGProgressResourceDefinition definition,
        float currentValue)
    {
        OwnerStableId = ownerStableId ?? string.Empty;
        Definition = definition ??
            throw new ArgumentNullException(nameof(definition));
        CurrentValue = currentValue;
    }

    public string OwnerStableId { get; }
    public DRPGProgressResourceDefinition Definition { get; }
    public string ResourceId => Definition.ResourceId;
    public DRPGProgressScope Scope => Definition.Scope;
    public float CurrentValue { get; }
    public float Capacity => Definition.Capacity;
}
