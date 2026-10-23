using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class BattleAttributeResistance
{
    [SerializeField] private DRPGDamageTag attribute;
    [Min(0)] [SerializeField] private int additive;
    [Min(-1f)] [SerializeField] private float percent;

    public BattleAttributeResistance(
        DRPGDamageTag attribute,
        int additive,
        float percent)
    {
        if (additive < 0)
            throw new ArgumentOutOfRangeException(nameof(additive));
        if (float.IsNaN(percent) || float.IsInfinity(percent) || percent < -1f)
            throw new ArgumentOutOfRangeException(nameof(percent));
        this.attribute = attribute;
        this.additive = additive;
        this.percent = percent;
    }

    public DRPGDamageTag Attribute => attribute;
    public int Additive => Math.Max(0, additive);
    public float Percent => Math.Max(-1f, percent);
}

public static class BattleResistanceMath
{
    public static int Resolve(
        DRPGDamageTag incomingAttribute,
        IReadOnlyList<BattleAttributeResistance> configured)
    {
        if (incomingAttribute == DRPGDamageTag.None || configured == null)
            return 0;

        int total = 0;
        for (int i = 0; i < configured.Count; i++)
        {
            BattleAttributeResistance entry = configured[i];
            if (entry == null || entry.Attribute != incomingAttribute)
                continue;
            total = checked(total + BattleEffectPanel.Calculate(
                entry.Additive,
                0d,
                entry.Percent));
        }
        return total;
    }
}
