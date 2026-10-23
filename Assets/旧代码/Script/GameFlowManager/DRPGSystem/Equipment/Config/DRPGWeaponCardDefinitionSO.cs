using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "DRPGWeaponCard",
    menuName = "ANKALUTE/DRPG/Equipment/Weapon Card")]
public sealed class DRPGWeaponCardDefinitionSO : ScriptableObject
{
    [SerializeField] private string cardId = "weapon_card";
    [SerializeField] private string displayName = "Weapon Card";
    [TextArea]
    [SerializeField] private string description;
    [SerializeField] private Sprite icon;
    [Min(1)]
    [SerializeField] private int cost = 1;
    [SerializeField] private List<DRPGPassiveModifierSO> passiveModifiers =
        new List<DRPGPassiveModifierSO>();
    [SerializeField] private DRPGSkillSO grantedSkill;
    [SerializeField] private string effectKey;
    [SerializeField] private List<DRPGActionSpecModifierDefinition> actionModifiers =
        new List<DRPGActionSpecModifierDefinition>();

    public string CardId => cardId;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public int Cost => Mathf.Max(1, cost);
    public IReadOnlyList<DRPGPassiveModifierSO> PassiveModifiers =>
        passiveModifiers;
    public DRPGSkillSO GrantedSkill => grantedSkill;
    public string EffectKey => effectKey;
    public IReadOnlyList<DRPGActionSpecModifierDefinition> ActionModifiers =>
        actionModifiers ??
        (IReadOnlyList<DRPGActionSpecModifierDefinition>)
        Array.Empty<DRPGActionSpecModifierDefinition>();
}
