using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "DRPGTrait",
    menuName = "ANKALUTE/DRPG/Equipment/Trait")]
public sealed class DRPGTraitDefinitionSO : ScriptableObject
{
    [SerializeField] private string traitId = "trait";
    [SerializeField] private string displayName = "Trait";
    [TextArea]
    [SerializeField] private string description;
    [SerializeField] private Sprite icon;
    [SerializeField] private List<DRPGPassiveModifierSO> passiveModifiers =
        new List<DRPGPassiveModifierSO>();
    [SerializeField] private List<DRPGActionSpecModifierDefinition> actionModifiers =
        new List<DRPGActionSpecModifierDefinition>();

    public string TraitId => traitId;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public IReadOnlyList<DRPGPassiveModifierSO> PassiveModifiers =>
        passiveModifiers ??
        (IReadOnlyList<DRPGPassiveModifierSO>)
        Array.Empty<DRPGPassiveModifierSO>();
    public IReadOnlyList<DRPGActionSpecModifierDefinition> ActionModifiers =>
        actionModifiers ??
        (IReadOnlyList<DRPGActionSpecModifierDefinition>)
        Array.Empty<DRPGActionSpecModifierDefinition>();
}
