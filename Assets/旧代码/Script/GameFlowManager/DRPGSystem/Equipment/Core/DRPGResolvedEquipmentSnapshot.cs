using System;
using System.Collections.Generic;

public sealed class DRPGResolvedEquipmentSnapshot
{
    public DRPGResolvedEquipmentSnapshot(
        string weaponInstanceId,
        string weaponDefinitionId,
        string activeProfessionId,
        DRPGSkillSO basicAttackOverride,
        IReadOnlyList<DRPGSkillSO> weaponIntrinsicSkills,
        IReadOnlyList<DRPGSkillSO> equipmentGrantedSkills,
        IReadOnlyList<DRPGPassiveModifierSO> passiveModifiers,
        int currentDurability,
        int maximumDurability,
        bool isBroken,
        string attackPresentationKey,
        IReadOnlyList<DRPGResolvedActionModifier> actionModifiers = null,
        IReadOnlyList<DRPGResolvedProgressCapacityModifier>
            progressCapacityModifiers = null)
    {
        if (currentDurability < 0)
            throw new ArgumentOutOfRangeException(nameof(currentDurability));
        if (maximumDurability < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumDurability));
        if (currentDurability > maximumDurability)
        {
            throw new ArgumentException(
                "Current durability cannot exceed maximum durability.");
        }

        WeaponInstanceId = weaponInstanceId ?? string.Empty;
        WeaponDefinitionId = weaponDefinitionId ?? string.Empty;
        ActiveProfessionId = activeProfessionId ?? string.Empty;
        BasicAttackOverride = basicAttackOverride;
        WeaponIntrinsicSkills = CopyNonNull(weaponIntrinsicSkills);
        EquipmentGrantedSkills = CopyNonNull(equipmentGrantedSkills);
        PassiveModifiers = CopyNonNull(passiveModifiers);
        CurrentDurability = currentDurability;
        MaximumDurability = maximumDurability;
        IsBroken = isBroken;
        AttackPresentationKey = attackPresentationKey ?? string.Empty;
        ActionModifiers = CopyNonNull(actionModifiers);
        ProgressCapacityModifiers = CopyNonNull(progressCapacityModifiers);
    }

    public string WeaponInstanceId { get; }
    public string WeaponDefinitionId { get; }
    public string ActiveProfessionId { get; }
    public DRPGSkillSO BasicAttackOverride { get; }
    public IReadOnlyList<DRPGSkillSO> WeaponIntrinsicSkills { get; }
    public IReadOnlyList<DRPGSkillSO> EquipmentGrantedSkills { get; }
    public IReadOnlyList<DRPGPassiveModifierSO> PassiveModifiers { get; }
    public int CurrentDurability { get; }
    public int MaximumDurability { get; }
    public bool IsBroken { get; }
    public string AttackPresentationKey { get; }
    public IReadOnlyList<DRPGResolvedActionModifier> ActionModifiers { get; }
    public IReadOnlyList<DRPGResolvedProgressCapacityModifier>
        ProgressCapacityModifiers { get; }
    public bool HasWeapon => !string.IsNullOrWhiteSpace(WeaponInstanceId);

    public static DRPGResolvedEquipmentSnapshot Empty(string professionId)
    {
        return new DRPGResolvedEquipmentSnapshot(
            string.Empty,
            string.Empty,
            professionId,
            null,
            Array.Empty<DRPGSkillSO>(),
            Array.Empty<DRPGSkillSO>(),
            Array.Empty<DRPGPassiveModifierSO>(),
            0,
            0,
            false,
            string.Empty);
    }

    public static DRPGResolvedEquipmentSnapshot FromWeapon(
        DRPGWeaponInstance weapon,
        string professionId)
    {
        return FromWeapon(weapon, professionId, null);
    }

    public static DRPGResolvedEquipmentSnapshot FromWeapon(
        DRPGWeaponInstance weapon,
        string professionId,
        DRPGEquipmentDefinitionRegistry registry)
    {
        if (weapon == null)
            return Empty(professionId);

        bool functional = !weapon.IsBroken;
        DRPGSkillSO intrinsic = functional
            ? weapon.Definition.IntrinsicSkill
            : null;
        IReadOnlyList<DRPGSkillSO> intrinsicSkills = intrinsic != null
            ? (IReadOnlyList<DRPGSkillSO>)Array.AsReadOnly(new[] { intrinsic })
            : Array.Empty<DRPGSkillSO>();
        var grantedSkills = new List<DRPGSkillSO>();
        var passiveModifiers = new List<DRPGPassiveModifierSO>();
        var actionModifiers = new List<DRPGResolvedActionModifier>();
        var progressCapacityModifiers =
            new List<DRPGResolvedProgressCapacityModifier>();
        int sourceSequence = 0;
        if (functional)
        {
            DRPGActionModifierSnapshotFactory.AppendDefinitions(
                "weapon:" + weapon.Definition.WeaponId,
                DRPGActionModifierSourceKind.Weapon,
                weapon.Definition.ActionModifiers,
                actionModifiers,
                ref sourceSequence);
            ResolveAffixes(
                weapon,
                registry,
                passiveModifiers,
                actionModifiers,
                ref sourceSequence);
            ResolveCards(
                weapon,
                registry,
                grantedSkills,
                passiveModifiers,
                actionModifiers,
                ref sourceSequence);
            ResolveAttachments(
                weapon,
                registry,
                passiveModifiers,
                actionModifiers,
                ref sourceSequence);
            IReadOnlyList<DRPGProgressCapacityModifierDefinition>
                capacitySource = weapon.Definition.ProgressCapacityModifiers;
            for (int i = 0; i < capacitySource.Count; i++)
            {
                if (capacitySource[i] != null)
                    progressCapacityModifiers.Add(capacitySource[i].Freeze());
            }
        }
        return new DRPGResolvedEquipmentSnapshot(
            weapon.InstanceId,
            weapon.Definition.WeaponId,
            professionId,
            functional ? weapon.Definition.BasicAttackOverride : null,
            intrinsicSkills,
            grantedSkills.AsReadOnly(),
            passiveModifiers.AsReadOnly(),
            weapon.CurrentDurability,
            weapon.CurrentMaxDurability,
            weapon.IsBroken,
            functional ? weapon.Definition.AttackPresentationKey : string.Empty,
            actionModifiers.AsReadOnly(),
            progressCapacityModifiers.AsReadOnly());
    }

    public static DRPGResolvedEquipmentSnapshot FromCharacterEquipment(
        DRPGCharacterEquipmentState equipment,
        string professionId,
        DRPGEquipmentDefinitionRegistry registry)
    {
        if (equipment == null)
            return Empty(professionId);

        DRPGResolvedEquipmentSnapshot weapon = FromWeapon(
            equipment.EquippedWeapon,
            professionId,
            registry);
        var passives = new List<DRPGPassiveModifierSO>(weapon.PassiveModifiers);
        var actionModifiers =
            new List<DRPGResolvedActionModifier>(weapon.ActionModifiers);
        int sourceSequence = actionModifiers.Count;

        ResolveAccessories(
            equipment,
            registry,
            passives,
            actionModifiers,
            ref sourceSequence);
        ResolveTraits(
            equipment,
            registry,
            passives,
            actionModifiers,
            ref sourceSequence);

        return new DRPGResolvedEquipmentSnapshot(
            weapon.WeaponInstanceId,
            weapon.WeaponDefinitionId,
            weapon.ActiveProfessionId,
            weapon.BasicAttackOverride,
            weapon.WeaponIntrinsicSkills,
            weapon.EquipmentGrantedSkills,
            passives.AsReadOnly(),
            weapon.CurrentDurability,
            weapon.MaximumDurability,
            weapon.IsBroken,
            weapon.AttackPresentationKey,
            actionModifiers.AsReadOnly(),
            weapon.ProgressCapacityModifiers);
    }

    private static void ResolveAccessories(
        DRPGCharacterEquipmentState equipment,
        DRPGEquipmentDefinitionRegistry registry,
        ICollection<DRPGPassiveModifierSO> passives,
        ICollection<DRPGResolvedActionModifier> actionModifiers,
        ref int sourceSequence)
    {
        for (int i = 0; i < equipment.Accessories.Count; i++)
        {
            DRPGAccessoryDefinitionSO equipped = equipment.Accessories[i];
            if (equipped == null)
                continue;
            DRPGAccessoryDefinitionSO definition = equipped;
            if (registry != null && !registry.TryGetAccessory(
                    equipped.AccessoryId,
                    out definition))
            {
                throw MissingDefinition("accessory", equipped.AccessoryId);
            }
            AppendSource(
                "accessory:" + definition.AccessoryId,
                DRPGActionModifierSourceKind.Accessory,
                definition.PassiveModifiers,
                definition.ActionModifiers,
                passives,
                actionModifiers,
                ref sourceSequence);
        }
    }

    private static void ResolveTraits(
        DRPGCharacterEquipmentState equipment,
        DRPGEquipmentDefinitionRegistry registry,
        ICollection<DRPGPassiveModifierSO> passives,
        ICollection<DRPGResolvedActionModifier> actionModifiers,
        ref int sourceSequence)
    {
        IReadOnlyList<string> traitIds = equipment.ResolvedTraitIds;
        for (int i = 0; i < traitIds.Count; i++)
        {
            string id = traitIds[i];
            DRPGTraitDefinitionSO definition = null;
            if (registry != null)
            {
                if (!registry.TryGetTrait(id, out definition))
                    throw MissingDefinition("trait", id);
            }
            else if (equipment.CurrentOutfit != null &&
                     equipment.CurrentOutfit.GrantedTrait != null &&
                     string.Equals(
                         equipment.CurrentOutfit.GrantedTrait.TraitId,
                         id,
                         StringComparison.Ordinal))
            {
                definition = equipment.CurrentOutfit.GrantedTrait;
            }
            if (definition == null)
                continue;
            AppendSource(
                "trait:" + definition.TraitId,
                DRPGActionModifierSourceKind.Trait,
                definition.PassiveModifiers,
                definition.ActionModifiers,
                passives,
                actionModifiers,
                ref sourceSequence);
        }
    }

    private static void ResolveAffixes(
        DRPGWeaponInstance weapon,
        DRPGEquipmentDefinitionRegistry registry,
        ICollection<DRPGPassiveModifierSO> passives,
        ICollection<DRPGResolvedActionModifier> actionModifiers,
        ref int sourceSequence)
    {
        for (int i = 0; i < weapon.AffixIds.Count; i++)
        {
            string id = weapon.AffixIds[i];
            DRPGWeaponAffixDefinitionSO definition = registry != null
                ? RequireAffix(registry, id)
                : FindFixedAffix(weapon.Definition.FixedAffixes, id);
            if (definition == null)
                continue;
            string prefix = "affix:" + definition.AffixId;
            AppendSource(
                prefix,
                DRPGActionModifierSourceKind.WeaponAffix,
                definition.PassiveModifiers,
                definition.ActionModifiers,
                passives,
                actionModifiers,
                ref sourceSequence);
        }
    }

    private static void ResolveCards(
        DRPGWeaponInstance weapon,
        DRPGEquipmentDefinitionRegistry registry,
        ICollection<DRPGSkillSO> grantedSkills,
        ICollection<DRPGPassiveModifierSO> passives,
        ICollection<DRPGResolvedActionModifier> actionModifiers,
        ref int sourceSequence)
    {
        if (registry == null)
            return;
        for (int i = 0; i < weapon.CardIds.Count; i++)
        {
            string id = weapon.CardIds[i];
            if (!registry.TryGetCard(id, out DRPGWeaponCardDefinitionSO definition))
                throw MissingDefinition("card", id);
            if (definition.GrantedSkill != null)
                AddUniqueSkill(definition.GrantedSkill, grantedSkills);
            AppendSource(
                "card:" + definition.CardId,
                DRPGActionModifierSourceKind.WeaponCard,
                definition.PassiveModifiers,
                definition.ActionModifiers,
                passives,
                actionModifiers,
                ref sourceSequence);
        }
    }

    private static void ResolveAttachments(
        DRPGWeaponInstance weapon,
        DRPGEquipmentDefinitionRegistry registry,
        ICollection<DRPGPassiveModifierSO> passives,
        ICollection<DRPGResolvedActionModifier> actionModifiers,
        ref int sourceSequence)
    {
        if (registry == null)
            return;
        for (int i = 0; i < weapon.AttachmentIds.Count; i++)
        {
            string id = weapon.AttachmentIds[i];
            if (!registry.TryGetAttachment(
                    id,
                    out DRPGWeaponAttachmentDefinitionSO definition))
            {
                throw MissingDefinition("attachment", id);
            }
            AppendSource(
                "attachment:" + definition.AttachmentId,
                DRPGActionModifierSourceKind.WeaponAttachment,
                definition.PassiveModifiers,
                definition.ActionModifiers,
                passives,
                actionModifiers,
                ref sourceSequence);
        }
    }

    private static void AppendSource(
        string sourcePrefix,
        DRPGActionModifierSourceKind sourceKind,
        IReadOnlyList<DRPGPassiveModifierSO> sourcePassives,
        IReadOnlyList<DRPGActionSpecModifierDefinition> directModifiers,
        ICollection<DRPGPassiveModifierSO> passives,
        ICollection<DRPGResolvedActionModifier> actionModifiers,
        ref int sourceSequence)
    {
        DRPGActionModifierSnapshotFactory.AppendDefinitions(
            sourcePrefix,
            sourceKind,
            directModifiers,
            actionModifiers,
            ref sourceSequence);
        if (sourcePassives == null)
            return;
        for (int i = 0; i < sourcePassives.Count; i++)
        {
            DRPGPassiveModifierSO passive = sourcePassives[i];
            if (passive == null)
                continue;
            AddUniquePassive(passive, passives);
            DRPGActionModifierSnapshotFactory.AppendPassive(
                sourcePrefix,
                sourceKind,
                passive,
                actionModifiers,
                ref sourceSequence);
        }
    }

    private static DRPGWeaponAffixDefinitionSO RequireAffix(
        DRPGEquipmentDefinitionRegistry registry,
        string id)
    {
        if (registry.TryGetAffix(id, out DRPGWeaponAffixDefinitionSO definition))
            return definition;
        throw MissingDefinition("affix", id);
    }

    private static DRPGWeaponAffixDefinitionSO FindFixedAffix(
        IReadOnlyList<DRPGWeaponAffixDefinitionSO> source,
        string id)
    {
        if (source == null)
            return null;
        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] != null && string.Equals(
                    source[i].AffixId,
                    id,
                    StringComparison.Ordinal))
            {
                return source[i];
            }
        }
        return null;
    }

    private static void AddUniquePassive(
        DRPGPassiveModifierSO passive,
        ICollection<DRPGPassiveModifierSO> destination)
    {
        foreach (DRPGPassiveModifierSO existing in destination)
        {
            if (ReferenceEquals(existing, passive) || string.Equals(
                    existing.ModifierId,
                    passive.ModifierId,
                    StringComparison.Ordinal))
            {
                return;
            }
        }
        destination.Add(passive);
    }

    private static void AddUniqueSkill(
        DRPGSkillSO skill,
        ICollection<DRPGSkillSO> destination)
    {
        foreach (DRPGSkillSO existing in destination)
        {
            if (ReferenceEquals(existing, skill) || string.Equals(
                    existing.SkillId,
                    skill.SkillId,
                    StringComparison.Ordinal))
            {
                return;
            }
        }
        destination.Add(skill);
    }

    private static InvalidOperationException MissingDefinition(
        string kind,
        string id)
    {
        return new InvalidOperationException(
            "Equipped weapon references an unregistered " + kind + ": " + id);
    }

    private static IReadOnlyList<T> CopyNonNull<T>(IReadOnlyList<T> source)
        where T : class
    {
        var result = new List<T>();
        if (source != null)
        {
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] != null)
                    result.Add(source[i]);
            }
        }
        return result.AsReadOnly();
    }
}
