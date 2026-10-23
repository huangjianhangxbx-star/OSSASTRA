using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StatsEntry
{
    [SerializeField]
    private CharacterStats characterStats;

    [SerializeField]
    private float value;

    public CharacterStats CharacterStats => characterStats;

    public float Value
    {
        get => value;
        set => this.value = value;
    }

    public StatsEntry(CharacterStats characterStats, float value)
    {
        this.characterStats = characterStats;
        this.value = value;
    }
}

[Serializable]
public class StatSheet
{
    [SerializeField]
    private List<StatsEntry> values = new List<StatsEntry>();

    public IReadOnlyList<StatsEntry> Values => values;

    public float GetValue(CharacterStats type)
    {
        EnsureCompleteAndUnique();

        for (int i = 0; i < values.Count; i++)
        {
            if (values[i].CharacterStats == type)
                return values[i].Value;
        }

        return 0f;
    }

    public List<StatsModifier> ToAddModifiers(object source)
    {
        EnsureCompleteAndUnique();

        List<StatsModifier> result = new List<StatsModifier>();

        for (int i = 0; i < values.Count; i++)
        {
            StatsEntry entry = values[i];

            result.Add(new StatsModifier(
                entry.CharacterStats,
                StatModifierMode.Add,
                entry.Value,
                source
            ));
        }

        return result;
    }

    public void EnsureCompleteAndUnique()
    {
        Dictionary<CharacterStats, float> oldValueMap = new Dictionary<CharacterStats, float>();

        if (values != null)
        {
            for (int i = 0; i < values.Count; i++)
            {
                StatsEntry entry = values[i];

                if (entry == null)
                    continue;

                CharacterStats statType = entry.CharacterStats;

                if (ShouldSkipStat(statType))
                    continue;

                if (!oldValueMap.ContainsKey(statType))
                    oldValueMap.Add(statType, entry.Value);
            }
        }

        if (values == null)
            values = new List<StatsEntry>();

        values.Clear();

        Array statTypes = Enum.GetValues(typeof(CharacterStats));

        for (int i = 0; i < statTypes.Length; i++)
        {
            CharacterStats statType = (CharacterStats)statTypes.GetValue(i);

            if (ShouldSkipStat(statType))
                continue;

            float oldValue = 0f;

            if (oldValueMap.TryGetValue(statType, out float cachedValue))
                oldValue = cachedValue;

            values.Add(new StatsEntry(statType, oldValue));
        }
    }

    private bool ShouldSkipStat(CharacterStats statType)
    {
        string name = statType.ToString();

        if (name == "None")
            return true;

        if (name == "Invalid")
            return true;

        if (name == "Count")
            return true;

        return false;
    }
}