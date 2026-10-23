using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WeaponStats", menuName = "ScriptableObject/武器/武器数值")]
public class WeaponStatsSO : ScriptableObject
{
    [Header("武器类型")]
    public WeaponType weaponType = WeaponType.Firearm;

    [Header("武器核心硬数值")]
    public WeaponCommonCoreStats commonCoreStats = new WeaponCommonCoreStats();

    [Header("装备后对角色的加成")]
    public WeaponCharacterBonusStats characterBonusStats = new WeaponCharacterBonusStats();

    [Header("枪械硬数值")]
    public FirearmWeaponStats firearmStats = new FirearmWeaponStats();

    [Header("近战硬数值")]
    public MeleeWeaponStats meleeStats = new MeleeWeaponStats();

    public bool IsFirearm => weaponType == WeaponType.Firearm;
    public bool IsMelee => weaponType == WeaponType.Melee;

    public List<StatsModifier> ToCharacterStatModifiers(object source)
    {
        List<StatsModifier> result = new List<StatsModifier>();

        if (characterBonusStats != null)
            characterBonusStats.AppendModifiers(result, source);

        return result;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (commonCoreStats == null)
            commonCoreStats = new WeaponCommonCoreStats();

        if (characterBonusStats == null)
            characterBonusStats = new WeaponCharacterBonusStats();

        if (firearmStats == null)
            firearmStats = new FirearmWeaponStats();

        if (meleeStats == null)
            meleeStats = new MeleeWeaponStats();

        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}