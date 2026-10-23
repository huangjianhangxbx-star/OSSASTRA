using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class DRPGResolvedCombatantBuild
{
    private readonly DRPGSkillSO professionBasicSource;
    private readonly IReadOnlyList<DRPGPassiveModifierSO> progressionPassiveSource;
    private readonly IReadOnlyList<DRPGSkillSO> stateGrantedSource;
    public DRPGResolvedCombatantBuild(
        DRPGCombatantProfileSO profile,
        DRPGProfessionSO profession,
        DRPGSkillSO basicAttack,
        IReadOnlyList<DRPGSkillSO> equippedSkills,
        IReadOnlyList<DRPGSkillSO> availableSkills,
        IReadOnlyList<DRPGPassiveModifierSO> passiveModifiers,
        DRPGDerivedStats growthStats = null)
        : this(
            profile,
            profession,
            basicAttack,
            equippedSkills,
            availableSkills,
            passiveModifiers,
            growthStats,
            DRPGResolvedEquipmentSnapshot.Empty(
                profession != null ? profession.ProfessionId : string.Empty),
            Array.Empty<DRPGSkillSO>())
    {
    }

    public DRPGResolvedCombatantBuild(
        DRPGCombatantProfileSO profile,
        DRPGProfessionSO profession,
        DRPGSkillSO professionBasicAttack,
        IReadOnlyList<DRPGSkillSO> configuredSkills,
        IReadOnlyList<DRPGSkillSO> availableSkills,
        IReadOnlyList<DRPGPassiveModifierSO> progressionPassiveModifiers,
        DRPGDerivedStats growthStats,
        DRPGResolvedEquipmentSnapshot equipment,
        IReadOnlyList<DRPGSkillSO> stateGrantedSkills,
        DRPGResolvedIdentityContribution identityContribution = null)
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        professionBasicSource = professionBasicAttack;
        progressionPassiveSource = CopyNonNull(progressionPassiveModifiers);
        stateGrantedSource = CopyNonNull(stateGrantedSkills);
        Profession = profession;
        Equipment = equipment ?? DRPGResolvedEquipmentSnapshot.Empty(
            profession != null ? profession.ProfessionId : string.Empty);
        BasicAttack = Equipment.BasicAttackOverride != null
            ? Equipment.BasicAttackOverride
            : professionBasicAttack;
        ConfiguredSkills = CopyLoadout(configuredSkills);
        EquippedSkills = ConfiguredSkills;
        GrantedSkills = MergeGrantedSkills(
            ConfiguredSkills,
            Equipment.WeaponIntrinsicSkills,
            Equipment.EquipmentGrantedSkills,
            stateGrantedSkills);
        BattleSkills = MergeBattleSkills(
            ConfiguredSkills,
            GrantedSkills,
            Equipment);
        AvailableSkills = CopyNonNull(availableSkills);
        IdentityContribution = identityContribution ??
            DRPGResolvedIdentityContribution.Empty;
        IReadOnlyList<DRPGPassiveModifierSO> identityAwarePassives =
            MergePassiveModifiers(
                progressionPassiveModifiers,
                IdentityContribution.PassiveModifiers,
                Array.Empty<DRPGPassiveModifierSO>());
        PassiveModifiers = MergePassiveModifiers(
            identityAwarePassives,
            Array.Empty<DRPGPassiveModifierSO>(),
            Equipment.PassiveModifiers);
        ActionModifiers = MergeActionModifiers(
            identityAwarePassives,
            Equipment.ActionModifiers);
        AttackPresentationKey = Equipment.AttackPresentationKey;

        float damage = 1f;
        float hp = 1f;
        float agility = 1f;
        float recovery = 1f;
        float posture = 1f;
        float postureRecovery = 1f;
        float freeDuration = 0f;
        float freeCadence = 1f;
        float freePosture = 1f;
        float freeReach = 1f;
        float guardWindow = 1f;
        float escape = 0f;
        for (int i = 0; i < PassiveModifiers.Count; i++)
        {
            DRPGPassiveModifierSO modifier = PassiveModifiers[i];
            damage *= modifier.DamageMultiplier;
            hp *= modifier.MaxHpMultiplier;
            agility *= modifier.AgilityMultiplier;
            recovery *= modifier.RecoveryDelayMultiplier;
            posture *= modifier.MaxPostureMultiplier;
            postureRecovery *= modifier.PostureRecoveryMultiplier;
            freeDuration += modifier.FreeStrikeDurationBonus;
            freeCadence *= modifier.FreeStrikeCadenceMultiplier;
            freePosture *= modifier.FreeStrikePostureCostMultiplier;
            freeReach *= modifier.FreeStrikeReachMultiplier;
            guardWindow *= modifier.GuardWindowMultiplier;
            escape += modifier.EscapeChanceBonus;
        }

        DRPGDerivedStats resolvedGrowth = growthStats ??
            DRPGDerivedStatResolver.Resolve(
                profile,
                DRPGCoreAttributes.Default,
                DRPGCoreAttributes.Default,
                1);
        GrowthStats = resolvedGrowth;
        DamageMultiplier = Mathf.Max(
            0f,
            damage * resolvedGrowth.FinalPotencyMultiplier);
        EffectiveMaxHp = Mathf.Max(1f, resolvedGrowth.MaxHp * hp);
        EffectiveMaxMp = Mathf.Max(0f, resolvedGrowth.MaxMp);
        EffectiveStaminaCapacity = profile.StaminaCapacity;
        EffectiveStaminaRecoveryPerCompletedAction =
            profile.StaminaRecoveryPerCompletedAction;
        EffectiveExpeditionEnergy = Mathf.Max(1f, resolvedGrowth.ExpeditionEnergy);
        EffectiveAgility = Mathf.Max(0.01f, resolvedGrowth.SpeedScore * agility);
        EffectiveBaseRecoveryDelay =
            Mathf.Max(0f, profile.BaseRecoveryDelay * recovery);
        // Battle V2 posture starts from the authored small integer default.
        // It is deliberately not an HP-like derived maximum; explicit recovery
        // may exceed it and own-turn entry never refills it.
        EffectiveMaxPosture = Mathf.Max(1f, profile.MaxPosture);
        EffectivePostureRecoveryPerTurn = 0f;
        FreeStrikeDurationBonus = freeDuration;
        FreeStrikeCadenceMultiplier = Mathf.Max(0.01f, freeCadence);
        FreeStrikePostureCostMultiplier = Mathf.Max(0f, freePosture);
        FreeStrikeReachMultiplier = Mathf.Max(0.01f, freeReach);
        GuardWindowMultiplier = Mathf.Max(
            0.01f,
            guardWindow * resolvedGrowth.GuardWindowMultiplier);
        EscapeChanceBonus = escape;
    }

    public DRPGCombatantProfileSO Profile { get; }
    public DRPGProfessionSO Profession { get; }
    public DRPGSkillSO BasicAttack { get; }
    public IReadOnlyList<DRPGSkillSO> ConfiguredSkills { get; }
    public IReadOnlyList<DRPGSkillSO> GrantedSkills { get; }
    public IReadOnlyList<DRPGSkillSO> BattleSkills { get; }
    public DRPGResolvedEquipmentSnapshot Equipment { get; private set; }
    public string AttackPresentationKey { get; }
    // Compatibility alias: this remains the profession's four configured slots.
    public IReadOnlyList<DRPGSkillSO> EquippedSkills { get; }
    public IReadOnlyList<DRPGSkillSO> AvailableSkills { get; }
    public IReadOnlyList<DRPGPassiveModifierSO> PassiveModifiers { get; }
    public IReadOnlyList<DRPGResolvedActionModifier> ActionModifiers { get; }
    public DRPGResolvedIdentityContribution IdentityContribution { get; }
    public float DamageMultiplier { get; }
    public float EffectiveMaxHp { get; }
    public float EffectiveMaxMp { get; }
    public int EffectiveStaminaCapacity { get; }
    public double EffectiveStaminaRecoveryPerCompletedAction { get; }
    public float EffectiveExpeditionEnergy { get; }
    public float EffectiveAgility { get; }
    public float EffectiveBaseRecoveryDelay { get; }
    public float EffectiveMaxPosture { get; }
    public float EffectivePostureRecoveryPerTurn { get; }
    public float FreeStrikeDurationBonus { get; }
    public float FreeStrikeCadenceMultiplier { get; }
    public float FreeStrikePostureCostMultiplier { get; }
    public float FreeStrikeReachMultiplier { get; }
    public float GuardWindowMultiplier { get; }
    public float EscapeChanceBonus { get; }
    public DRPGDerivedStats GrowthStats { get; }

    internal DRPGResolvedCombatantBuild WithWeaponCompletion(DRPGResolvedEquipmentSnapshot equipment)
    {
        if (equipment == null) throw new ArgumentNullException(nameof(equipment));
        if (!equipment.IsBroken && !Equipment.IsBroken && equipment.WeaponInstanceId == Equipment.WeaponInstanceId)
        {
            // Healthy use changes the frozen durability display, not any stats or
            // skills. Do not re-read mutable authored modifiers during completion.
            var copy = (DRPGResolvedCombatantBuild)MemberwiseClone();
            copy.Equipment = equipment;
            return copy;
        }
        return new DRPGResolvedCombatantBuild(Profile, Profession, professionBasicSource,
            ConfiguredSkills, AvailableSkills, progressionPassiveSource, GrowthStats, equipment,
            stateGrantedSource, IdentityContribution);
    }

    public bool ContainsEquippedSkill(DRPGSkillSO skill)
    {
        if (skill == null)
            return false;
        for (int i = 0; i < EquippedSkills.Count; i++)
        {
            if (ReferenceEquals(EquippedSkills[i], skill))
                return true;
        }
        return false;
    }

    public bool ContainsBattleSkill(string skillId)
    {
        if (string.IsNullOrWhiteSpace(skillId))
            return false;
        for (int i = 0; i < BattleSkills.Count; i++)
        {
            if (string.Equals(
                    BattleSkills[i].SkillId,
                    skillId,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    public static DRPGResolvedCombatantBuild CreateLegacy(
        DRPGCombatantProfileSO profile)
    {
        return CreateLegacy(profile, null);
    }

    public static DRPGResolvedCombatantBuild CreateLegacy(
        DRPGCombatantProfileSO profile,
        DRPGResolvedEquipmentSnapshot equipment)
    {
        if (profile == null)
            throw new ArgumentNullException(nameof(profile));

        IReadOnlyList<DRPGSkillSO> source = profile.Skills;
        DRPGSkillSO basicAttack = source.Count > 0 ? source[0] : null;
        var loadout = new DRPGSkillSO[DRPGSkillLoadout.SlotCount];
        for (int i = 1; i < source.Count && i <= loadout.Length; i++)
            loadout[i - 1] = source[i];
        return new DRPGResolvedCombatantBuild(
            profile,
            null,
            basicAttack,
            loadout,
            source,
            Array.Empty<DRPGPassiveModifierSO>(),
            null,
            equipment ?? DRPGResolvedEquipmentSnapshot.Empty(string.Empty),
            Array.Empty<DRPGSkillSO>());
    }

    private static IReadOnlyList<DRPGSkillSO> CopyLoadout(
        IReadOnlyList<DRPGSkillSO> source)
    {
        var result = new DRPGSkillSO[DRPGSkillLoadout.SlotCount];
        if (source != null)
        {
            int count = Mathf.Min(result.Length, source.Count);
            for (int i = 0; i < count; i++)
                result[i] = source[i];
        }
        return Array.AsReadOnly(result);
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

    private static IReadOnlyList<DRPGSkillSO> MergeGrantedSkills(
        IReadOnlyList<DRPGSkillSO> configured,
        IReadOnlyList<DRPGSkillSO> intrinsic,
        IReadOnlyList<DRPGSkillSO> equipmentGranted,
        IReadOnlyList<DRPGSkillSO> stateGranted)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        AddSkillIds(configured, ids);
        var result = new List<DRPGSkillSO>();
        AddUniqueSkills(intrinsic, ids, result);
        AddUniqueSkills(equipmentGranted, ids, result);
        AddUniqueSkills(stateGranted, ids, result);
        return result.AsReadOnly();
    }

    private static IReadOnlyList<DRPGSkillSO> MergeBattleSkills(
        IReadOnlyList<DRPGSkillSO> configured,
        IReadOnlyList<DRPGSkillSO> granted,
        DRPGResolvedEquipmentSnapshot equipment)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<DRPGSkillSO>();
        AddBattleUsableSkills(configured, equipment, ids, result);
        AddBattleUsableSkills(granted, equipment, ids, result);
        return result.AsReadOnly();
    }

    private static void AddBattleUsableSkills(
        IReadOnlyList<DRPGSkillSO> source,
        DRPGResolvedEquipmentSnapshot equipment,
        ISet<string> ids,
        ICollection<DRPGSkillSO> destination)
    {
        if (source == null)
            return;
        bool hasFunctionalWeapon = equipment != null &&
            equipment.HasWeapon && !equipment.IsBroken;
        for (int i = 0; i < source.Count; i++)
        {
            DRPGSkillSO skill = source[i];
            if (skill == null || string.IsNullOrWhiteSpace(skill.SkillId) ||
                skill.RequiresFunctionalWeapon && !hasFunctionalWeapon ||
                !ids.Add(skill.SkillId))
            {
                continue;
            }
            destination.Add(skill);
        }
    }

    private static void AddSkillIds(
        IReadOnlyList<DRPGSkillSO> source,
        ISet<string> ids)
    {
        if (source == null)
            return;
        for (int i = 0; i < source.Count; i++)
        {
            DRPGSkillSO skill = source[i];
            if (skill != null && !string.IsNullOrWhiteSpace(skill.SkillId))
                ids.Add(skill.SkillId);
        }
    }

    private static void AddUniqueSkills(
        IReadOnlyList<DRPGSkillSO> source,
        ISet<string> ids,
        ICollection<DRPGSkillSO> destination)
    {
        if (source == null)
            return;
        for (int i = 0; i < source.Count; i++)
        {
            DRPGSkillSO skill = source[i];
            if (skill == null || string.IsNullOrWhiteSpace(skill.SkillId) ||
                !ids.Add(skill.SkillId))
            {
                continue;
            }
            destination.Add(skill);
        }
    }

    private static IReadOnlyList<DRPGPassiveModifierSO> MergePassiveModifiers(
        IReadOnlyList<DRPGPassiveModifierSO> progression,
        IReadOnlyList<DRPGPassiveModifierSO> identity,
        IReadOnlyList<DRPGPassiveModifierSO> equipment)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<DRPGPassiveModifierSO>();
        AddUniqueModifiers(progression, ids, result);
        AddUniqueModifiers(identity, ids, result);
        AddUniqueModifiers(equipment, ids, result);
        return result.AsReadOnly();
    }

    private static IReadOnlyList<DRPGResolvedActionModifier> MergeActionModifiers(
        IReadOnlyList<DRPGPassiveModifierSO> progression,
        IReadOnlyList<DRPGResolvedActionModifier> equipment)
    {
        var result = new List<DRPGResolvedActionModifier>();
        if (equipment != null)
        {
            for (int i = 0; i < equipment.Count; i++)
            {
                if (equipment[i] != null)
                    result.Add(equipment[i]);
            }
        }

        int sourceSequence = result.Count;
        if (progression != null)
        {
            for (int i = 0; i < progression.Count; i++)
            {
                DRPGPassiveModifierSO passive = progression[i];
                if (passive == null)
                    continue;
                DRPGActionModifierSnapshotFactory.AppendPassive(
                    "progression",
                    DRPGActionModifierSourceKind.ProgressionPassive,
                    passive,
                    result,
                    ref sourceSequence);
            }
        }
        return result.AsReadOnly();
    }

    private static void AddUniqueModifiers(
        IReadOnlyList<DRPGPassiveModifierSO> source,
        ISet<string> ids,
        ICollection<DRPGPassiveModifierSO> destination)
    {
        if (source == null)
            return;
        for (int i = 0; i < source.Count; i++)
        {
            DRPGPassiveModifierSO modifier = source[i];
            if (modifier == null || string.IsNullOrWhiteSpace(modifier.ModifierId) ||
                !ids.Add(modifier.ModifierId))
            {
                continue;
            }
            destination.Add(modifier);
        }
    }
}
