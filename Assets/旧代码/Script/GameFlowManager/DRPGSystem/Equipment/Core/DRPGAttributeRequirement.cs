using System;
using UnityEngine;

[Serializable]
public struct DRPGAttributeRequirement
{
    [SerializeField] private DRPGCoreAttributeId attribute;
    [Min(0)]
    [SerializeField] private int minimumValue;

    public DRPGAttributeRequirement(
        DRPGCoreAttributeId attribute,
        int minimumValue)
    {
        this.attribute = attribute;
        this.minimumValue = Mathf.Max(0, minimumValue);
    }

    public DRPGCoreAttributeId Attribute => attribute;
    public int MinimumValue => Mathf.Max(0, minimumValue);

    public bool IsMetBy(DRPGCoreAttributes permanentAttributes)
    {
        return permanentAttributes.Get(attribute) >= MinimumValue;
    }
}
