using PixelCrushers.DialogueSystem.Articy.Articy_4_0;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class CharacterStatsController : MonoBehaviour, ICharacterSubsystem
{
    private CharacterRoot characterRoot;
    private CharacterDataBus dataBus;
    private Dictionary<CharacterStats, float> finalStats = new Dictionary<CharacterStats, float>();

    private List<StatsModifier> externalModifiers = new List<StatsModifier>();

    private bool _isInitialized;

    public void Initialize(CharacterRoot root)
    {
        if (_isInitialized)
            return;

        characterRoot = root;
        dataBus = root.characterDataBus;

        //if (dataBus.currentWeapon == null && dataBus.initialWeaponConfig != null)
        //{
        //    dataBus.currentWeapon = new WeaponRuntimeData(dataBus.initialWeaponConfig);
        //}

        RecalculateStats();

        DebugPrintStatBreakdownFromContextMenu();

        _isInitialized = true;
    }

    public float GetStat(CharacterStats statType)
    {
        if (finalStats.TryGetValue(statType, out float value))
            return value;

        return 0f;
    }

    public void AddModifier(StatsModifier modifier)
    {
        if (modifier == null)
            return;

        externalModifiers.Add(modifier);
        RecalculateStats();
    }

    public void RemoveModifiersFromSource(object source)
    {
        externalModifiers.RemoveAll(m => m.source == source);
        RecalculateStats();
    }

    public void RecalculateStats()
    {
        List<StatsModifier> allModifiers = new List<StatsModifier>();

        CollectCharacterBaseStats(allModifiers);
        CollectLevelGrowthStats(allModifiers);
        CollectWeaponStats(allModifiers);
        CollectWeaponRuntimeModifiers(allModifiers);
        CollectExternalModifiers(allModifiers);

        BuildFinalStats(allModifiers);
    }

    private void CollectCharacterBaseStats(List<StatsModifier> result)
    {
        if (dataBus == null)
            return;

        if (dataBus.statProfile == null)
            return;

        if (dataBus.statProfile.baseStats == null)
            return;

        result.AddRange(dataBus.statProfile.baseStats.baseStats.ToAddModifiers(dataBus.statProfile.baseStats));
    }

    private void CollectLevelGrowthStats(List<StatsModifier> result)
    {
        if (dataBus == null)
            return;

        if (dataBus.statProfile == null)
            return;

        if (dataBus.statProfile.levelGrowth == null)
            return;

        if (dataBus.statProfile.levelGrowth.growthPerLevel == null)
            return;

        int level = Mathf.Max(1, dataBus.level);
        int growthCount = level - 1;

        StatSheet growth = dataBus.statProfile.levelGrowth.growthPerLevel;

        IReadOnlyList<StatsEntry> entries = growth.Values;

        for (int i = 0; i < entries.Count; i++)
        {
            StatsEntry entry = entries[i];

            result.Add(new StatsModifier(
                entry.CharacterStats,
                StatModifierMode.Add,
                entry.Value * growthCount,
                dataBus.statProfile.levelGrowth
            ));
        }
    }

    private void CollectWeaponStats(List<StatsModifier> result)
    {
        if (dataBus == null)
            return;

        if (dataBus.currentWeaponRuntimeData == null)
            return;

        if (dataBus.currentWeaponRuntimeData.Stats == null)
            return;

        result.AddRange(dataBus.currentWeaponRuntimeData.Stats.ToCharacterStatModifiers(dataBus.currentWeaponRuntimeData.Stats));
    }
    private void CollectWeaponRuntimeModifiers(List<StatsModifier> result)
    {
        if (dataBus == null)
            return;

        if (dataBus.currentWeaponRuntimeData == null)
            return;

        result.AddRange(dataBus.currentWeaponRuntimeData.runtimeModifiers);
    }

    private void CollectExternalModifiers(List<StatsModifier> result)
    {
        result.AddRange(externalModifiers);
    }

    private void BuildFinalStats(List<StatsModifier> modifiers)
    {
        finalStats.Clear();

        foreach (CharacterStats statType in System.Enum.GetValues(typeof(CharacterStats)))
        {
            if (ShouldSkipStat(statType))
                continue;

            float add = 0f;
            float percentAdd = 0f;
            float multiply = 1f;

            bool hasOverride = false;
            float overrideValue = 0f;

            for (int i = 0; i < modifiers.Count; i++)
            {
                StatsModifier modifier = modifiers[i];

                if (modifier == null)
                    continue;

                if (modifier.characterStats != statType)
                    continue;

                switch (modifier.mode)
                {
                    case StatModifierMode.Add:
                        add += modifier.value;
                        break;

                    case StatModifierMode.PercentAdd:
                        percentAdd += modifier.value;
                        break;

                    case StatModifierMode.Multiply:
                        multiply *= modifier.value;
                        break;

                    case StatModifierMode.Override:
                        hasOverride = true;
                        overrideValue = modifier.value;
                        break;
                }
            }

            float finalValue = add * (1f + percentAdd) * multiply;

            if (hasOverride)
                finalValue = overrideValue;

            finalStats[statType] = finalValue;
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


    [ContextMenu("Debug/Print Stat Breakdown")]
    public void DebugPrintStatBreakdownFromContextMenu()
    {
        DebugPrintStatBreakdown();
    }

    public void DebugPrintStatBreakdown()
    {
        List<StatsModifier> allModifiers = new List<StatsModifier>();

        CollectCharacterBaseStats(allModifiers);
        CollectLevelGrowthStats(allModifiers);
        CollectWeaponStats(allModifiers);
        CollectWeaponRuntimeModifiers(allModifiers);
        CollectExternalModifiers(allModifiers);

        BuildFinalStats(allModifiers);

        StringBuilder sb = new StringBuilder();

        string characterName = "Unknown";

        if (dataBus != null && !string.IsNullOrEmpty(dataBus.characterName))
            characterName = dataBus.characterName;
        else if (characterRoot != null)
            characterName = characterRoot.name;

        sb.AppendLine("========== Character Stat Breakdown ==========");
        sb.AppendLine($"Character: {characterName}");

        if (dataBus != null)
        {
            sb.AppendLine($"Level: {dataBus.level}");

            if (dataBus.currentWeaponRuntimeData != null &&
                dataBus.currentWeaponRuntimeData.config != null)
                sb.AppendLine($"Weapon: {dataBus.currentWeaponRuntimeData.config.weaponName}");
            else
                sb.AppendLine("Weapon: null");
        }

        sb.AppendLine("----------------------------------------------");

        foreach (CharacterStats statType in System.Enum.GetValues(typeof(CharacterStats)))
        {
            float add = 0f;
            float percentAdd = 0f;
            float multiply = 1f;

            bool hasAny = false;
            bool hasOverride = false;
            float overrideValue = 0f;

            sb.AppendLine($"[{statType}]");

            for (int i = 0; i < allModifiers.Count; i++)
            {
                StatsModifier modifier = allModifiers[i];

                if (modifier == null)
                    continue;

                if (modifier.characterStats != statType)
                    continue;

                hasAny = true;

                sb.AppendLine(
                    $"  - {modifier.mode} {modifier.value} | Source: {GetModifierSourceName(modifier.source)}"
                );

                switch (modifier.mode)
                {
                    case StatModifierMode.Add:
                        add += modifier.value;
                        break;

                    case StatModifierMode.PercentAdd:
                        percentAdd += modifier.value;
                        break;

                    case StatModifierMode.Multiply:
                        multiply *= modifier.value;
                        break;

                    case StatModifierMode.Override:
                        hasOverride = true;
                        overrideValue = modifier.value;
                        break;
                }
            }

            if (!hasAny)
            {
                sb.AppendLine("  no modifiers");
                sb.AppendLine($"  Final: {GetStat(statType)}");
                continue;
            }

            float calculated = add * (1f + percentAdd) * multiply;

            if (hasOverride)
                calculated = overrideValue;

            sb.AppendLine($"  AddSum: {add}");
            sb.AppendLine($"  PercentAddSum: {percentAdd}");
            sb.AppendLine($"  MultiplyProduct: {multiply}");

            if (hasOverride)
                sb.AppendLine($"  Override: {overrideValue}");

            sb.AppendLine($"  Final: {calculated}");
            sb.AppendLine();
        }

        sb.AppendLine("==============================================");

        Debug.Log(sb.ToString(), this);
    }

    private string GetModifierSourceName(object source)
    {
        if (source == null)
            return "null";

        if (source is Object unityObject)
            return unityObject.name + $" ({unityObject.GetType().Name})";

        return source.GetType().Name;
    }


















}