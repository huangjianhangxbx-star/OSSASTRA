using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "DRPGAccessory",
    menuName = "ANKALUTE/DRPG/Equipment/Accessory")]
public sealed class DRPGAccessoryDefinitionSO : ScriptableObject
{
    [SerializeField] private string accessoryId = "accessory";
    [SerializeField] private string displayName = "Accessory";
    [SerializeField] private Sprite icon;
    [Min(0f)]
    [SerializeField] private float weight;
    [SerializeField] private List<DRPGPassiveModifierSO> passiveModifiers =
        new List<DRPGPassiveModifierSO>();
    [SerializeField] private List<DRPGActionSpecModifierDefinition> actionModifiers =
        new List<DRPGActionSpecModifierDefinition>();

    public string AccessoryId => accessoryId;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public float Weight => Mathf.Max(0f, weight);
    public IReadOnlyList<DRPGPassiveModifierSO> PassiveModifiers =>
        passiveModifiers ??
        (IReadOnlyList<DRPGPassiveModifierSO>)
        Array.Empty<DRPGPassiveModifierSO>();
    public IReadOnlyList<DRPGActionSpecModifierDefinition> ActionModifiers =>
        actionModifiers ??
        (IReadOnlyList<DRPGActionSpecModifierDefinition>)
        Array.Empty<DRPGActionSpecModifierDefinition>();
}
