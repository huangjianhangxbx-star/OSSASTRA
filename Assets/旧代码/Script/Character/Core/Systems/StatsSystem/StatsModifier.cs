using System;
using UnityEngine;

[Serializable]
public class StatsModifier
{
    public CharacterStats characterStats;
    public StatModifierMode mode;
    public float value;

    [NonSerialized]
    public object source;
    public StatsModifier() { }

    public StatsModifier(CharacterStats statType, StatModifierMode mode, float value, object source = null)
    {
        this.characterStats = statType;
        this.mode = mode;
        this.value = value;
        this.source = source;
    }
}