using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WeaponCharacterBonusStats
{
    [Header("生存加成")]
    public float maxHpAdd;
    public float defenseAdd;

    [Header("移动加成")]
    public float moveSpeedAdd;
    public float moveSpeedPercentAdd;

    [Header("暴击加成")]
    public float critChanceAdd;
    public float critDamageAdd;

    [Header("枪械能力加成")]
    public float firearmDamagePercentAdd;
    public float fireRatePercentAdd;
    public float reloadSpeedPercentAdd;
    public float recoilControlAdd;
    public float accuracyPercentAdd;

    [Header("近战能力加成")]
    public float meleeDamagePercentAdd;
    public float meleeAttackSpeedPercentAdd;

    public void AppendModifiers(List<StatsModifier> result, object source)
    {
        if (result == null)
            return;

        AddIfNotZero(result, CharacterStats.MaxHP, StatModifierMode.Add, maxHpAdd, source);
        AddIfNotZero(result, CharacterStats.Defense, StatModifierMode.Add, defenseAdd, source);

        AddIfNotZero(result, CharacterStats.MoveSpeed, StatModifierMode.Add, moveSpeedAdd, source);
        AddIfNotZero(result, CharacterStats.MoveSpeed, StatModifierMode.PercentAdd, moveSpeedPercentAdd, source);

        AddIfNotZero(result, CharacterStats.CritChanceBonus, StatModifierMode.Add, critChanceAdd, source);
        AddIfNotZero(result, CharacterStats.CritDamageBonus, StatModifierMode.Add, critDamageAdd, source);

        AddIfNotZero(result, CharacterStats.FirearmDamageBonus, StatModifierMode.PercentAdd, firearmDamagePercentAdd, source);
        AddIfNotZero(result, CharacterStats.FireRateBonus, StatModifierMode.PercentAdd, fireRatePercentAdd, source);
        AddIfNotZero(result, CharacterStats.ReloadSpeedBonus, StatModifierMode.PercentAdd, reloadSpeedPercentAdd, source);
        AddIfNotZero(result, CharacterStats.RecoilControl, StatModifierMode.Add, recoilControlAdd, source);
        AddIfNotZero(result, CharacterStats.AccuracyBonus, StatModifierMode.PercentAdd, accuracyPercentAdd, source);

        AddIfNotZero(result, CharacterStats.MeleeDamageBonus, StatModifierMode.PercentAdd, meleeDamagePercentAdd, source);
        AddIfNotZero(result, CharacterStats.MeleeAttackSpeedBonus, StatModifierMode.PercentAdd, meleeAttackSpeedPercentAdd, source);
    }

    private void AddIfNotZero(
        List<StatsModifier> result,
        CharacterStats stat,
        StatModifierMode mode,
        float value,
        object source
    )
    {
        if (Mathf.Approximately(value, 0f))
            return;

        result.Add(new StatsModifier(stat, mode, value, source));
    }
}