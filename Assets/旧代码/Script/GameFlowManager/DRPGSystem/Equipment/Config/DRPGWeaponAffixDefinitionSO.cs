using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "DRPGWeaponAffix",
    menuName = "ANKALUTE/DRPG/Equipment/Weapon Affix")]
public sealed class DRPGWeaponAffixDefinitionSO : ScriptableObject
{
    [SerializeField] private string affixId = "weapon_affix";
    [SerializeField] private string displayName = "Weapon Affix";
    [TextArea]
    [SerializeField] private string description;
    [SerializeField] private List<DRPGPassiveModifierSO> passiveModifiers =
        new List<DRPGPassiveModifierSO>();
    [SerializeField] private string effectKey;
    [SerializeField] private List<DRPGActionSpecModifierDefinition> actionModifiers =
        new List<DRPGActionSpecModifierDefinition>();

    public string AffixId => affixId;
    public string DisplayName => displayName;
    public string Description => description;
    public IReadOnlyList<DRPGPassiveModifierSO> PassiveModifiers =>
        passiveModifiers;
    public string EffectKey => effectKey;
    public IReadOnlyList<DRPGActionSpecModifierDefinition> ActionModifiers =>
        actionModifiers ??
        (IReadOnlyList<DRPGActionSpecModifierDefinition>)
        Array.Empty<DRPGActionSpecModifierDefinition>();
}
