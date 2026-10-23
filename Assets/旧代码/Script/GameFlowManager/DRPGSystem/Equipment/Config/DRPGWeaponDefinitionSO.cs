using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "DRPGWeapon",
    menuName = "ANKALUTE/DRPG/Equipment/Weapon")]
public sealed class DRPGWeaponDefinitionSO : ScriptableObject
{
    [SerializeField] private string weaponId = "weapon";
    [SerializeField] private string displayName = "Weapon";
    [TextArea]
    [SerializeField] private string description;
    [SerializeField] private Sprite icon;
    [SerializeField] private DRPGEquipmentRarity rarity =
        DRPGEquipmentRarity.Blue;
    [SerializeField] private DRPGWeaponOwnershipKind ownershipKind =
        DRPGWeaponOwnershipKind.General;
    [SerializeField] private string signatureCharacterId;
    [SerializeField] private List<DRPGProfessionSO> compatibleProfessions =
        new List<DRPGProfessionSO>();
    [Min(0f)]
    [SerializeField] private float weight = 1f;
    [Min(1)]
    [SerializeField] private int baseDurability = 60;
    [Range(0.01f, 1f)]
    [SerializeField] private float initialDurabilityFloorRatio = 0.75f;
    [SerializeField] private List<DRPGAttributeRequirement> requirements =
        new List<DRPGAttributeRequirement>();
    [SerializeField] private DRPGSkillSO basicAttackOverride;
    [SerializeField] private DRPGSkillSO intrinsicSkill;
    [SerializeField] private string attackPresentationKey;
    [SerializeField] private List<DRPGWeaponAffixDefinitionSO> fixedAffixes =
        new List<DRPGWeaponAffixDefinitionSO>();
    [SerializeField] private List<DRPGActionSpecModifierDefinition> actionModifiers =
        new List<DRPGActionSpecModifierDefinition>();
    [SerializeField] private List<DRPGProgressCapacityModifierDefinition>
        progressCapacityModifiers =
            new List<DRPGProgressCapacityModifierDefinition>();

    public string WeaponId => weaponId;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public DRPGEquipmentRarity Rarity => rarity;
    public DRPGWeaponOwnershipKind OwnershipKind => ownershipKind;
    public string SignatureCharacterId => signatureCharacterId;
    public IReadOnlyList<DRPGProfessionSO> CompatibleProfessions =>
        compatibleProfessions ??
        (IReadOnlyList<DRPGProfessionSO>)Array.Empty<DRPGProfessionSO>();
    public float Weight => Mathf.Max(0f, weight);
    public int BaseDurability => Mathf.Max(1, baseDurability);
    public float InitialDurabilityFloorRatio =>
        Mathf.Clamp(initialDurabilityFloorRatio, 0.01f, 1f);
    public IReadOnlyList<DRPGAttributeRequirement> Requirements =>
        requirements ??
        (IReadOnlyList<DRPGAttributeRequirement>)
            Array.Empty<DRPGAttributeRequirement>();
    public DRPGSkillSO BasicAttackOverride => basicAttackOverride;
    public DRPGSkillSO IntrinsicSkill => intrinsicSkill;
    public string AttackPresentationKey => attackPresentationKey;
    public IReadOnlyList<DRPGWeaponAffixDefinitionSO> FixedAffixes =>
        fixedAffixes ??
        (IReadOnlyList<DRPGWeaponAffixDefinitionSO>)
            Array.Empty<DRPGWeaponAffixDefinitionSO>();
    public IReadOnlyList<DRPGActionSpecModifierDefinition> ActionModifiers =>
        actionModifiers ??
        (IReadOnlyList<DRPGActionSpecModifierDefinition>)
        Array.Empty<DRPGActionSpecModifierDefinition>();
    public IReadOnlyList<DRPGProgressCapacityModifierDefinition>
        ProgressCapacityModifiers => progressCapacityModifiers ??
        (IReadOnlyList<DRPGProgressCapacityModifierDefinition>)
        Array.Empty<DRPGProgressCapacityModifierDefinition>();
    public DRPGWeaponRarityBudget RarityBudget =>
        DRPGWeaponRarityRules.GetBudget(rarity);
    public int AttachmentSlots => DRPGWeaponRarityRules.AttachmentSlots;
    public DRPGWeaponBreakDisposition BreakDisposition =>
        ownershipKind == DRPGWeaponOwnershipKind.Signature
            ? DRPGWeaponBreakDisposition.PreserveSignatureAtZero
            : DRPGWeaponBreakDisposition.DestroyIntoFragments;

    public bool IsCompatibleWith(DRPGProfessionSO profession)
    {
        if (profession == null)
            return false;
        IReadOnlyList<DRPGProfessionSO> source = CompatibleProfessions;
        for (int i = 0; i < source.Count; i++)
        {
            if (ReferenceEquals(source[i], profession) ||
                source[i] != null &&
                string.Equals(
                    source[i].ProfessionId,
                    profession.ProfessionId,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    public void ValidateOrThrow()
    {
        if (string.IsNullOrWhiteSpace(weaponId))
            throw new InvalidOperationException("Weapon ID is required.");
        if (baseDurability < 1 || initialDurabilityFloorRatio <= 0f ||
            initialDurabilityFloorRatio > 1f)
        {
            throw new InvalidOperationException(
                "Weapon durability configuration is invalid: " + weaponId);
        }
        if (ownershipKind == DRPGWeaponOwnershipKind.Signature &&
            string.IsNullOrWhiteSpace(signatureCharacterId))
        {
            throw new InvalidOperationException(
                "Signature weapon requires an owner: " + weaponId);
        }
        if (ownershipKind == DRPGWeaponOwnershipKind.General &&
            !string.IsNullOrWhiteSpace(signatureCharacterId))
        {
            throw new InvalidOperationException(
                "General weapon cannot declare a signature owner: " + weaponId);
        }
        if (intrinsicSkill != null && RarityBudget.IntrinsicSkillSlots == 0)
        {
            throw new InvalidOperationException(
                "Only RedGold weapons may define an intrinsic skill: " + weaponId);
        }
        if (FixedAffixes.Count > RarityBudget.AffixSlots)
        {
            throw new InvalidOperationException(
                "Fixed affixes exceed rarity budget: " + weaponId);
        }

        ValidateProfessions();
        ValidateRequirements();
        ValidateAffixes();
        ValidateProgressCapacityModifiers();
    }

    private void ValidateProfessions()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        IReadOnlyList<DRPGProfessionSO> source = CompatibleProfessions;
        for (int i = 0; i < source.Count; i++)
        {
            DRPGProfessionSO profession = source[i];
            if (profession == null ||
                string.IsNullOrWhiteSpace(profession.ProfessionId) ||
                !ids.Add(profession.ProfessionId))
            {
                throw new InvalidOperationException(
                    "Weapon has a null, unnamed, or duplicate profession: " +
                    weaponId);
            }
        }
    }

    private void ValidateRequirements()
    {
        var attributes = new HashSet<DRPGCoreAttributeId>();
        IReadOnlyList<DRPGAttributeRequirement> source = Requirements;
        for (int i = 0; i < source.Count; i++)
        {
            if (!attributes.Add(source[i].Attribute))
            {
                throw new InvalidOperationException(
                    "Weapon has duplicate attribute requirements: " + weaponId);
            }
        }
    }

    private void ValidateAffixes()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        IReadOnlyList<DRPGWeaponAffixDefinitionSO> source = FixedAffixes;
        for (int i = 0; i < source.Count; i++)
        {
            DRPGWeaponAffixDefinitionSO affix = source[i];
            if (affix == null || string.IsNullOrWhiteSpace(affix.AffixId) ||
                !ids.Add(affix.AffixId))
            {
                throw new InvalidOperationException(
                    "Weapon has a null, unnamed, or duplicate affix: " + weaponId);
            }
        }
    }

    private void ValidateProgressCapacityModifiers()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        IReadOnlyList<DRPGProgressCapacityModifierDefinition> source =
            ProgressCapacityModifiers;
        for (int i = 0; i < source.Count; i++)
        {
            DRPGProgressCapacityModifierDefinition modifier = source[i];
            if (modifier == null || string.IsNullOrWhiteSpace(modifier.ResourceId) ||
                !ids.Add(modifier.ResourceId))
            {
                throw new InvalidOperationException(
                    "Weapon has a null, unnamed, or duplicate progress capacity modifier: " +
                    weaponId);
            }
            modifier.Freeze();
        }
    }
}
