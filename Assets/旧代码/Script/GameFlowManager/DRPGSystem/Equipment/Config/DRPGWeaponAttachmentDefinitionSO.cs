using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "DRPGWeaponAttachment",
    menuName = "ANKALUTE/DRPG/Equipment/Weapon Attachment")]
public sealed class DRPGWeaponAttachmentDefinitionSO : ScriptableObject
{
    [SerializeField] private string attachmentId = "weapon_attachment";
    [SerializeField] private string displayName = "Weapon Attachment";
    [TextArea]
    [SerializeField] private string description;
    [SerializeField] private Sprite icon;
    [SerializeField] private List<DRPGPassiveModifierSO> passiveModifiers =
        new List<DRPGPassiveModifierSO>();
    [SerializeField] private List<DRPGActionSpecModifierDefinition> actionModifiers =
        new List<DRPGActionSpecModifierDefinition>();

    public string AttachmentId => attachmentId;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public IReadOnlyList<DRPGPassiveModifierSO> PassiveModifiers =>
        passiveModifiers;
    public IReadOnlyList<DRPGActionSpecModifierDefinition> ActionModifiers =>
        actionModifiers ??
        (IReadOnlyList<DRPGActionSpecModifierDefinition>)
        Array.Empty<DRPGActionSpecModifierDefinition>();
}
