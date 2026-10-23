using System;

public sealed class DRPGRecoveredAccessoryItem
{
    public DRPGRecoveredAccessoryItem(
        string instanceId,
        DRPGAccessoryDefinitionSO definition)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            throw new ArgumentException("Instance ID is required.", nameof(instanceId));
        InstanceId = instanceId;
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public string InstanceId { get; }
    public DRPGAccessoryDefinitionSO Definition { get; }
    public float Weight => Definition.Weight;
}
