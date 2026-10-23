using System;
using System.Collections.Generic;
using UnityEngine;

public enum DRPGActionModifierSourceKind
{
    Weapon = 100,
    WeaponAffix = 200,
    WeaponCard = 300,
    WeaponAttachment = 400,
    Accessory = 450,
    ProgressionPassive = 500,
    Trait = 550,
    Status = 600
}

public enum DRPGActionFormationModifierMode
{
    Keep = 0,
    Intersect = 1,
    Override = 2
}

[Serializable]
public sealed class DRPGActionModifierCondition
{
    [SerializeField] private bool limitCommandKind;
    [SerializeField] private DRPGBattleCommandKind commandKind;
    [SerializeField] private string requiredSkillId;
    [SerializeField] private bool limitExecutionMode;
    [SerializeField] private DRPGSkillExecutionMode executionMode;
    [SerializeField] private string requiredProfessionId;
    [SerializeField] private string requiredWeaponId;
    [SerializeField] private DRPGFormationRowMask actorFormationRows =
        DRPGFormationRowMask.All;
    [Range(0f, 1f)] [SerializeField] private float minimumHpRatio;
    [Range(0f, 1f)] [SerializeField] private float maximumHpRatio = 1f;
    [Range(0f, 1f)] [SerializeField] private float minimumMpRatio;
    [Range(0f, 1f)] [SerializeField] private float maximumMpRatio = 1f;
    [Range(0f, 1f)] [SerializeField] private float minimumPostureRatio;
    [Range(0f, 1f)] [SerializeField] private float maximumPostureRatio = 1f;
    [SerializeField] private List<string> requiredStatusIds = new List<string>();
    [SerializeField] private List<string> forbiddenStatusIds = new List<string>();

    internal DRPGResolvedActionModifierCondition Resolve()
    {
        return new DRPGResolvedActionModifierCondition(
            limitCommandKind,
            commandKind,
            requiredSkillId,
            limitExecutionMode,
            executionMode,
            requiredProfessionId,
            requiredWeaponId,
            actorFormationRows,
            minimumHpRatio,
            maximumHpRatio,
            minimumMpRatio,
            maximumMpRatio,
            minimumPostureRatio,
            maximumPostureRatio,
            requiredStatusIds,
            forbiddenStatusIds);
    }
}

[Serializable]
public sealed class DRPGActionSpecModifierDefinition
{
    [SerializeField] private string modifierId = "action_modifier";
    [SerializeField] private DRPGActionModifierCondition condition =
        new DRPGActionModifierCondition();

    [Header("Core action values")]
    [SerializeField] private float skillPowerAdditive;
    [Min(0f)] [SerializeField] private float skillPowerMultiplier = 1f;
    [SerializeField] private float outgoingDamageMultiplierAdditive;
    [Min(0f)] [SerializeField] private float outgoingDamageMultiplier = 1f;
    [SerializeField] private float outgoingHealingMultiplierAdditive;
    [Min(0f)] [SerializeField] private float outgoingHealingMultiplier = 1f;
    [SerializeField] private float manaCostAdditive;
    [Min(0f)] [SerializeField] private float manaCostMultiplier = 1f;
    [SerializeField] private float postureCostAdditive;
    [Min(0f)] [SerializeField] private float postureCostMultiplier = 1f;
    [SerializeField] private float prepareDelayAdditive;
    [Min(0f)] [SerializeField] private float prepareDelayMultiplier = 1f;
    [SerializeField] private float recoveryDelayAdditive;
    [Min(0f)] [SerializeField] private float recoveryDelayMultiplier = 1f;

    [Header("Effects and range")]
    [SerializeField] private DRPGDamageTag addDamageTags;
    [SerializeField] private DRPGDamageTag removeDamageTags;
    [SerializeField] private List<DRPGActionEffectDefinition> additionalEffects =
        new List<DRPGActionEffectDefinition>();
    [SerializeField] private DRPGActionFormationModifierMode sourceFormationMode;
    [SerializeField] private DRPGFormationRowMask sourceFormationRows =
        DRPGFormationRowMask.All;
    [SerializeField] private DRPGActionFormationModifierMode targetFormationMode;
    [SerializeField] private DRPGFormationRowMask targetFormationRows =
        DRPGFormationRowMask.All;

    [Header("Free strike")]
    [SerializeField] private float freeStrikeDurationAdditive;
    [Min(0f)] [SerializeField] private float freeStrikeDurationMultiplier = 1f;
    [SerializeField] private float freeStrikeCadenceAdditive;
    [Min(0f)] [SerializeField] private float freeStrikeCadenceMultiplier = 1f;
    [SerializeField] private float freeStrikePostureCostAdditive;
    [Min(0f)] [SerializeField] private float freeStrikePostureCostMultiplier = 1f;
    [SerializeField] private float freeStrikeLineLengthAdditive;
    [Min(0f)] [SerializeField] private float freeStrikeLineLengthMultiplier = 1f;
    [SerializeField] private float freeStrikeLineWidthAdditive;
    [Min(0f)] [SerializeField] private float freeStrikeLineWidthMultiplier = 1f;
    [SerializeField] private bool overrideFreeStrikeShape;
    [SerializeField] private DRPGFreeStrikeShape freeStrikeShape;
    [SerializeField] private bool overrideFreeStrikeContactPolicy;
    [SerializeField] private DRPGFreeStrikeContactPolicy freeStrikeContactPolicy;
    [Min(1)] [SerializeField] private int freeStrikeMaxPrimaryContacts = 1;
    [SerializeField] private bool overrideSecondaryImpactKind;
    [SerializeField] private DRPGSecondaryImpactKind secondaryImpactKind;
    [SerializeField] private float secondaryRadiusAdditive;
    [Min(0f)] [SerializeField] private float secondaryRadiusMultiplier = 1f;
    [Min(0f)] [SerializeField] private float secondaryDamageMultiplier = 1f;

    [Header("Special defense")]
    [SerializeField] private float guardLeadSecondsAdditive;
    [Min(0f)] [SerializeField] private float guardLeadSecondsMultiplier = 1f;
    [SerializeField] private float perfectLeadSecondsAdditive;
    [Min(0f)] [SerializeField] private float perfectLeadSecondsMultiplier = 1f;
    [Min(0f)] [SerializeField] private float guardedDamageMultiplier = 1f;
    [SerializeField] private float missedGuardPostureDamageAdditive;
    [Min(0f)] [SerializeField] private float missedGuardPostureDamageMultiplier = 1f;
    [SerializeField] private float guardedPostureDamageAdditive;
    [Min(0f)] [SerializeField] private float guardedPostureDamageMultiplier = 1f;

    public string ModifierId => modifierId;

    internal DRPGResolvedActionModifier Resolve(
        string sourceId,
        DRPGActionModifierSourceKind sourceKind,
        int sourceSequence)
    {
        return new DRPGResolvedActionModifier(
            sourceId,
            sourceKind,
            sourceSequence,
            condition != null
                ? condition.Resolve()
                : DRPGResolvedActionModifierCondition.Unconditional,
            skillPowerAdditive,
            Mathf.Max(0f, skillPowerMultiplier),
            outgoingDamageMultiplierAdditive,
            Mathf.Max(0f, outgoingDamageMultiplier),
            outgoingHealingMultiplierAdditive,
            Mathf.Max(0f, outgoingHealingMultiplier),
            manaCostAdditive,
            Mathf.Max(0f, manaCostMultiplier),
            postureCostAdditive,
            Mathf.Max(0f, postureCostMultiplier),
            prepareDelayAdditive,
            Mathf.Max(0f, prepareDelayMultiplier),
            recoveryDelayAdditive,
            Mathf.Max(0f, recoveryDelayMultiplier),
            addDamageTags,
            removeDamageTags,
            DRPGResolvedAdditionalActionEffect.Snapshot(additionalEffects),
            sourceFormationMode,
            sourceFormationRows,
            targetFormationMode,
            targetFormationRows,
            freeStrikeDurationAdditive,
            Mathf.Max(0f, freeStrikeDurationMultiplier),
            freeStrikeCadenceAdditive,
            Mathf.Max(0f, freeStrikeCadenceMultiplier),
            freeStrikePostureCostAdditive,
            Mathf.Max(0f, freeStrikePostureCostMultiplier),
            freeStrikeLineLengthAdditive,
            Mathf.Max(0f, freeStrikeLineLengthMultiplier),
            freeStrikeLineWidthAdditive,
            Mathf.Max(0f, freeStrikeLineWidthMultiplier),
            overrideFreeStrikeShape,
            freeStrikeShape,
            overrideFreeStrikeContactPolicy,
            freeStrikeContactPolicy,
            Mathf.Max(1, freeStrikeMaxPrimaryContacts),
            overrideSecondaryImpactKind,
            secondaryImpactKind,
            secondaryRadiusAdditive,
            Mathf.Max(0f, secondaryRadiusMultiplier),
            Mathf.Max(0f, secondaryDamageMultiplier),
            guardLeadSecondsAdditive,
            Mathf.Max(0f, guardLeadSecondsMultiplier),
            perfectLeadSecondsAdditive,
            Mathf.Max(0f, perfectLeadSecondsMultiplier),
            Mathf.Max(0f, guardedDamageMultiplier),
            missedGuardPostureDamageAdditive,
            Mathf.Max(0f, missedGuardPostureDamageMultiplier),
            guardedPostureDamageAdditive,
            Mathf.Max(0f, guardedPostureDamageMultiplier));
    }
}

public sealed class DRPGActionSpecContributionTrace
{
    public DRPGActionSpecContributionTrace(
        string sourceId,
        DRPGActionModifierSourceKind sourceKind,
        int sourceSequence,
        bool wasApplied,
        string skipReason,
        string beforeSummary,
        string afterSummary)
    {
        SourceId = sourceId ?? string.Empty;
        SourceKind = sourceKind;
        SourceSequence = sourceSequence;
        WasApplied = wasApplied;
        SkipReason = skipReason ?? string.Empty;
        BeforeSummary = beforeSummary ?? string.Empty;
        AfterSummary = afterSummary ?? string.Empty;
    }

    public string SourceId { get; }
    public DRPGActionModifierSourceKind SourceKind { get; }
    public int SourceSequence { get; }
    public bool WasApplied { get; }
    public string SkipReason { get; }
    public string BeforeSummary { get; }
    public string AfterSummary { get; }
}

internal sealed class DRPGResolvedActionModifierCondition
{
    private readonly HashSet<string> requiredStatuses;
    private readonly HashSet<string> forbiddenStatuses;

    public DRPGResolvedActionModifierCondition(
        bool limitCommandKind,
        DRPGBattleCommandKind commandKind,
        string requiredSkillId,
        bool limitExecutionMode,
        DRPGSkillExecutionMode executionMode,
        string requiredProfessionId,
        string requiredWeaponId,
        DRPGFormationRowMask actorRows,
        float minimumHpRatio,
        float maximumHpRatio,
        float minimumMpRatio,
        float maximumMpRatio,
        float minimumPostureRatio,
        float maximumPostureRatio,
        IReadOnlyList<string> requiredStatusIds,
        IReadOnlyList<string> forbiddenStatusIds)
    {
        LimitCommandKind = limitCommandKind;
        CommandKind = commandKind;
        RequiredSkillId = requiredSkillId ?? string.Empty;
        LimitExecutionMode = limitExecutionMode;
        ExecutionMode = executionMode;
        RequiredProfessionId = requiredProfessionId ?? string.Empty;
        RequiredWeaponId = requiredWeaponId ?? string.Empty;
        ActorRows = actorRows;
        MinimumHpRatio = Mathf.Clamp01(minimumHpRatio);
        MaximumHpRatio = Mathf.Clamp(maximumHpRatio, MinimumHpRatio, 1f);
        MinimumMpRatio = Mathf.Clamp01(minimumMpRatio);
        MaximumMpRatio = Mathf.Clamp(maximumMpRatio, MinimumMpRatio, 1f);
        MinimumPostureRatio = Mathf.Clamp01(minimumPostureRatio);
        MaximumPostureRatio = Mathf.Clamp(
            maximumPostureRatio,
            MinimumPostureRatio,
            1f);
        requiredStatuses = CopyIds(requiredStatusIds);
        forbiddenStatuses = CopyIds(forbiddenStatusIds);
    }

    public bool LimitCommandKind { get; }
    public DRPGBattleCommandKind CommandKind { get; }
    public string RequiredSkillId { get; }
    public bool LimitExecutionMode { get; }
    public DRPGSkillExecutionMode ExecutionMode { get; }
    public string RequiredProfessionId { get; }
    public string RequiredWeaponId { get; }
    public DRPGFormationRowMask ActorRows { get; }
    public float MinimumHpRatio { get; }
    public float MaximumHpRatio { get; }
    public float MinimumMpRatio { get; }
    public float MaximumMpRatio { get; }
    public float MinimumPostureRatio { get; }
    public float MaximumPostureRatio { get; }

    public bool Matches(DRPGActionSpecResolveContext context, out string reason)
    {
        if (LimitCommandKind && context.Command.Kind != CommandKind)
            return Fail("command kind mismatch", out reason);
        if (!string.IsNullOrEmpty(RequiredSkillId) &&
            !string.Equals(
                context.SkillId,
                RequiredSkillId,
                StringComparison.Ordinal))
        {
            return Fail("skill ID mismatch", out reason);
        }
        if (LimitExecutionMode && context.ExecutionMode != ExecutionMode)
            return Fail("execution mode mismatch", out reason);
        if (!string.IsNullOrEmpty(RequiredProfessionId) &&
            !string.Equals(
                context.ProfessionId,
                RequiredProfessionId,
                StringComparison.Ordinal))
        {
            return Fail("profession ID mismatch", out reason);
        }
        if (!string.IsNullOrEmpty(RequiredWeaponId) &&
            !string.Equals(
                context.WeaponId,
                RequiredWeaponId,
                StringComparison.Ordinal))
        {
            return Fail("weapon ID mismatch", out reason);
        }
        if (!ActorRows.Contains(context.ActorFormationRow))
            return Fail("actor formation mismatch", out reason);
        if (!InRange(context.HpRatio, MinimumHpRatio, MaximumHpRatio))
            return Fail("actor HP ratio mismatch", out reason);
        if (!InRange(context.MpRatio, MinimumMpRatio, MaximumMpRatio))
            return Fail("actor MP ratio mismatch", out reason);
        if (!InRange(
                context.PostureRatio,
                MinimumPostureRatio,
                MaximumPostureRatio))
        {
            return Fail("actor posture ratio mismatch", out reason);
        }
        foreach (string statusId in requiredStatuses)
        {
            if (!context.HasStatus(statusId))
                return Fail("required status missing: " + statusId, out reason);
        }
        foreach (string statusId in forbiddenStatuses)
        {
            if (context.HasStatus(statusId))
                return Fail("forbidden status present: " + statusId, out reason);
        }

        reason = string.Empty;
        return true;
    }

    public static DRPGResolvedActionModifierCondition Unconditional =>
        new DRPGResolvedActionModifierCondition(
            false,
            default,
            string.Empty,
            false,
            default,
            string.Empty,
            string.Empty,
            DRPGFormationRowMask.All,
            0f,
            1f,
            0f,
            1f,
            0f,
            1f,
            Array.Empty<string>(),
            Array.Empty<string>());

    private static bool InRange(float value, float minimum, float maximum)
    {
        return value >= minimum - 0.0001f && value <= maximum + 0.0001f;
    }

    private static bool Fail(string message, out string reason)
    {
        reason = message;
        return false;
    }

    private static HashSet<string> CopyIds(IReadOnlyList<string> source)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (source == null)
            return result;
        for (int i = 0; i < source.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(source[i]))
                result.Add(source[i]);
        }
        return result;
    }
}

public sealed class DRPGResolvedActionModifier
{
    internal DRPGResolvedActionModifier(
        string sourceId,
        DRPGActionModifierSourceKind sourceKind,
        int sourceSequence,
        DRPGResolvedActionModifierCondition condition,
        float skillPowerAdditive,
        float skillPowerMultiplier,
        float damageAdditive,
        float damageMultiplier,
        float healingAdditive,
        float healingMultiplier,
        float manaAdditive,
        float manaMultiplier,
        float postureAdditive,
        float postureMultiplier,
        float prepareAdditive,
        float prepareMultiplier,
        float recoveryAdditive,
        float recoveryMultiplier,
        DRPGDamageTag addDamageTags,
        DRPGDamageTag removeDamageTags,
        IReadOnlyList<DRPGResolvedAdditionalActionEffect> additionalEffects,
        DRPGActionFormationModifierMode sourceFormationMode,
        DRPGFormationRowMask sourceFormationRows,
        DRPGActionFormationModifierMode targetFormationMode,
        DRPGFormationRowMask targetFormationRows,
        float freeDurationAdditive,
        float freeDurationMultiplier,
        float freeCadenceAdditive,
        float freeCadenceMultiplier,
        float freePostureAdditive,
        float freePostureMultiplier,
        float freeLengthAdditive,
        float freeLengthMultiplier,
        float freeWidthAdditive,
        float freeWidthMultiplier,
        bool overrideFreeShape,
        DRPGFreeStrikeShape freeShape,
        bool overrideContactPolicy,
        DRPGFreeStrikeContactPolicy contactPolicy,
        int maxPrimaryContacts,
        bool overrideSecondaryKind,
        DRPGSecondaryImpactKind secondaryKind,
        float secondaryRadiusAdditive,
        float secondaryRadiusMultiplier,
        float secondaryDamageMultiplier,
        float guardLeadAdditive,
        float guardLeadMultiplier,
        float perfectLeadAdditive,
        float perfectLeadMultiplier,
        float guardedDamageMultiplier,
        float missedPostureAdditive,
        float missedPostureMultiplier,
        float guardedPostureAdditive,
        float guardedPostureMultiplier)
    {
        SourceId = sourceId ?? string.Empty;
        SourceKind = sourceKind;
        SourceSequence = sourceSequence;
        Condition = condition ?? DRPGResolvedActionModifierCondition.Unconditional;
        SkillPowerAdditive = skillPowerAdditive;
        SkillPowerMultiplier = skillPowerMultiplier;
        DamageMultiplierAdditive = damageAdditive;
        DamageMultiplier = damageMultiplier;
        HealingMultiplierAdditive = healingAdditive;
        HealingMultiplier = healingMultiplier;
        ManaCostAdditive = manaAdditive;
        ManaCostMultiplier = manaMultiplier;
        PostureCostAdditive = postureAdditive;
        PostureCostMultiplier = postureMultiplier;
        PrepareDelayAdditive = prepareAdditive;
        PrepareDelayMultiplier = prepareMultiplier;
        RecoveryDelayAdditive = recoveryAdditive;
        RecoveryDelayMultiplier = recoveryMultiplier;
        AddDamageTags = addDamageTags;
        RemoveDamageTags = removeDamageTags;
        AdditionalEffects = additionalEffects ??
            Array.Empty<DRPGResolvedAdditionalActionEffect>();
        SourceFormationMode = sourceFormationMode;
        SourceFormationRows = sourceFormationRows;
        TargetFormationMode = targetFormationMode;
        TargetFormationRows = targetFormationRows;
        FreeStrikeDurationAdditive = freeDurationAdditive;
        FreeStrikeDurationMultiplier = freeDurationMultiplier;
        FreeStrikeCadenceAdditive = freeCadenceAdditive;
        FreeStrikeCadenceMultiplier = freeCadenceMultiplier;
        FreeStrikePostureCostAdditive = freePostureAdditive;
        FreeStrikePostureCostMultiplier = freePostureMultiplier;
        FreeStrikeLineLengthAdditive = freeLengthAdditive;
        FreeStrikeLineLengthMultiplier = freeLengthMultiplier;
        FreeStrikeLineWidthAdditive = freeWidthAdditive;
        FreeStrikeLineWidthMultiplier = freeWidthMultiplier;
        OverrideFreeStrikeShape = overrideFreeShape;
        FreeStrikeShape = freeShape;
        OverrideContactPolicy = overrideContactPolicy;
        ContactPolicy = contactPolicy;
        MaxPrimaryContacts = maxPrimaryContacts;
        OverrideSecondaryImpactKind = overrideSecondaryKind;
        SecondaryImpactKind = secondaryKind;
        SecondaryRadiusAdditive = secondaryRadiusAdditive;
        SecondaryRadiusMultiplier = secondaryRadiusMultiplier;
        SecondaryDamageMultiplier = secondaryDamageMultiplier;
        GuardLeadSecondsAdditive = guardLeadAdditive;
        GuardLeadSecondsMultiplier = guardLeadMultiplier;
        PerfectLeadSecondsAdditive = perfectLeadAdditive;
        PerfectLeadSecondsMultiplier = perfectLeadMultiplier;
        GuardedDamageMultiplier = guardedDamageMultiplier;
        MissedGuardPostureDamageAdditive = missedPostureAdditive;
        MissedGuardPostureDamageMultiplier = missedPostureMultiplier;
        GuardedPostureDamageAdditive = guardedPostureAdditive;
        GuardedPostureDamageMultiplier = guardedPostureMultiplier;
    }

    public string SourceId { get; }
    public DRPGActionModifierSourceKind SourceKind { get; }
    public int SourceSequence { get; }
    internal DRPGResolvedActionModifierCondition Condition { get; }
    public float SkillPowerAdditive { get; }
    public float SkillPowerMultiplier { get; }
    public float DamageMultiplierAdditive { get; }
    public float DamageMultiplier { get; }
    public float HealingMultiplierAdditive { get; }
    public float HealingMultiplier { get; }
    public float ManaCostAdditive { get; }
    public float ManaCostMultiplier { get; }
    public float PostureCostAdditive { get; }
    public float PostureCostMultiplier { get; }
    public float PrepareDelayAdditive { get; }
    public float PrepareDelayMultiplier { get; }
    public float RecoveryDelayAdditive { get; }
    public float RecoveryDelayMultiplier { get; }
    public DRPGDamageTag AddDamageTags { get; }
    public DRPGDamageTag RemoveDamageTags { get; }
    internal IReadOnlyList<DRPGResolvedAdditionalActionEffect> AdditionalEffects { get; }
    public DRPGActionFormationModifierMode SourceFormationMode { get; }
    public DRPGFormationRowMask SourceFormationRows { get; }
    public DRPGActionFormationModifierMode TargetFormationMode { get; }
    public DRPGFormationRowMask TargetFormationRows { get; }
    public float FreeStrikeDurationAdditive { get; }
    public float FreeStrikeDurationMultiplier { get; }
    public float FreeStrikeCadenceAdditive { get; }
    public float FreeStrikeCadenceMultiplier { get; }
    public float FreeStrikePostureCostAdditive { get; }
    public float FreeStrikePostureCostMultiplier { get; }
    public float FreeStrikeLineLengthAdditive { get; }
    public float FreeStrikeLineLengthMultiplier { get; }
    public float FreeStrikeLineWidthAdditive { get; }
    public float FreeStrikeLineWidthMultiplier { get; }
    public bool OverrideFreeStrikeShape { get; }
    public DRPGFreeStrikeShape FreeStrikeShape { get; }
    public bool OverrideContactPolicy { get; }
    public DRPGFreeStrikeContactPolicy ContactPolicy { get; }
    public int MaxPrimaryContacts { get; }
    public bool OverrideSecondaryImpactKind { get; }
    public DRPGSecondaryImpactKind SecondaryImpactKind { get; }
    public float SecondaryRadiusAdditive { get; }
    public float SecondaryRadiusMultiplier { get; }
    public float SecondaryDamageMultiplier { get; }
    public float GuardLeadSecondsAdditive { get; }
    public float GuardLeadSecondsMultiplier { get; }
    public float PerfectLeadSecondsAdditive { get; }
    public float PerfectLeadSecondsMultiplier { get; }
    public float GuardedDamageMultiplier { get; }
    public float MissedGuardPostureDamageAdditive { get; }
    public float MissedGuardPostureDamageMultiplier { get; }
    public float GuardedPostureDamageAdditive { get; }
    public float GuardedPostureDamageMultiplier { get; }

    internal static DRPGResolvedActionModifier FromStatus(
        DRPGStatusRuntimeSnapshot status,
        int sourceSequence)
    {
        float Stack(float multiplier)
        {
            return Mathf.Max(
                0f,
                1f + (multiplier - 1f) * Mathf.Max(1, status.Stacks));
        }

        return new DRPGResolvedActionModifier(
            "status:" + status.StatusId,
            DRPGActionModifierSourceKind.Status,
            sourceSequence,
            DRPGResolvedActionModifierCondition.Unconditional,
            0f, 1f,
            0f, Stack(status.Spec.OutgoingDamageMultiplier),
            0f, Stack(status.Spec.OutgoingHealingMultiplier),
            0f, Stack(status.Spec.ManaCostMultiplier),
            0f, Stack(status.Spec.PostureCostMultiplier),
            0f, 1f,
            0f, 1f,
            DRPGDamageTag.None,
            DRPGDamageTag.None,
            Array.Empty<DRPGResolvedAdditionalActionEffect>(),
            DRPGActionFormationModifierMode.Keep,
            DRPGFormationRowMask.All,
            DRPGActionFormationModifierMode.Keep,
            DRPGFormationRowMask.All,
            0f, 1f, 0f, 1f, 0f, 1f, 0f, 1f, 0f, 1f,
            false, default,
            false, default, 1,
            false, default,
            0f, 1f, 1f,
            0f, 1f, 0f, 1f, 1f,
            0f, 1f, 0f, 1f);
    }
}

internal sealed class DRPGResolvedAdditionalActionEffect
{
    private DRPGResolvedAdditionalActionEffect(DRPGActionEffectDefinition source)
    {
        Kind = source.Kind;
        Target = source.Target;
        MagnitudeMode = source.MagnitudeMode;
        Magnitude = source.Magnitude;
        DamageTags = source.DamageTags;
        Status = DRPGResolvedStatusSpec.From(source.Status);
        StatusStacks = source.StatusStacks;
        StatusDurationOverride = source.StatusDurationOverride;
        FormationRowDelta = source.FormationRowDelta;
        TimelineDelta = source.TimelineDelta;
        TimelinePriorityDelta = source.TimelinePriorityDelta;
    }

    public DRPGActionEffectKind Kind { get; }
    public DRPGActionEffectTarget Target { get; }
    public DRPGEffectMagnitudeMode MagnitudeMode { get; }
    public float Magnitude { get; }
    public DRPGDamageTag DamageTags { get; }
    public DRPGResolvedStatusSpec Status { get; }
    public int StatusStacks { get; }
    public int StatusDurationOverride { get; }
    public int FormationRowDelta { get; }
    public float TimelineDelta { get; }
    public int TimelinePriorityDelta { get; }

    public DRPGResolvedActionEffect Resolve(float skillPower)
    {
        return new DRPGResolvedActionEffect(
            Kind,
            Target,
            MagnitudeMode,
            Magnitude,
            skillPower,
            DamageTags,
            Status,
            StatusStacks,
            StatusDurationOverride,
            FormationRowDelta,
            TimelineDelta,
            TimelinePriorityDelta);
    }

    public static IReadOnlyList<DRPGResolvedAdditionalActionEffect> Snapshot(
        IReadOnlyList<DRPGActionEffectDefinition> source)
    {
        if (source == null || source.Count == 0)
            return Array.Empty<DRPGResolvedAdditionalActionEffect>();
        var result = new List<DRPGResolvedAdditionalActionEffect>(source.Count);
        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] != null)
                result.Add(new DRPGResolvedAdditionalActionEffect(source[i]));
        }
        return result.AsReadOnly();
    }
}

internal sealed class DRPGActionSpecResolveContext
{
    private readonly HashSet<string> statuses;

    public DRPGActionSpecResolveContext(
        DRPGBattleCommand command,
        BattleParticipantRuntime actor)
    {
        Command = command ?? throw new ArgumentNullException(nameof(command));
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        Skill = command.Skill;
        SkillId = Skill != null ? Skill.SkillId : string.Empty;
        ExecutionMode = Skill != null
            ? Skill.ExecutionMode
            : DRPGSkillExecutionMode.Standard;
        ProfessionId = actor.Build.Profession != null
            ? actor.Build.Profession.ProfessionId
            : actor.Build.Equipment.ActiveProfessionId;
        WeaponId = actor.Build.Equipment.WeaponDefinitionId;
        ActorFormationRow = actor.FormationRow;
        HpRatio = SafeRatio(actor.CurrentHp, actor.EffectiveMaxHp);
        MpRatio = SafeRatio(actor.CurrentMp, actor.EffectiveMaxMp);
        PostureRatio = SafeRatio(
            actor.CurrentPosture,
            actor.EffectiveMaxPosture);
        statuses = new HashSet<string>(StringComparer.Ordinal);
        IReadOnlyList<DRPGStatusRuntimeSnapshot> snapshots =
            actor.Statuses.Snapshot();
        StatusSnapshots = snapshots;
        for (int i = 0; i < snapshots.Count; i++)
            statuses.Add(snapshots[i].StatusId);
    }

    public DRPGBattleCommand Command { get; }
    public BattleParticipantRuntime Actor { get; }
    public DRPGSkillSO Skill { get; }
    public string SkillId { get; }
    public DRPGSkillExecutionMode ExecutionMode { get; }
    public string ProfessionId { get; }
    public string WeaponId { get; }
    public DRPGFormationRow ActorFormationRow { get; }
    public float HpRatio { get; }
    public float MpRatio { get; }
    public float PostureRatio { get; }
    public IReadOnlyList<DRPGStatusRuntimeSnapshot> StatusSnapshots { get; }

    public bool HasStatus(string statusId)
    {
        return statuses.Contains(statusId ?? string.Empty);
    }

    private static float SafeRatio(float value, float maximum)
    {
        return maximum > 0f ? Mathf.Clamp01(value / maximum) : 0f;
    }
}

internal static class DRPGActionModifierSnapshotFactory
{
    public static void AppendPassive(
        string sourcePrefix,
        DRPGActionModifierSourceKind sourceKind,
        DRPGPassiveModifierSO passive,
        ICollection<DRPGResolvedActionModifier> destination,
        ref int sourceSequence)
    {
        if (passive == null)
            return;
        AppendDefinitions(
            sourcePrefix + ":passive:" + passive.ModifierId,
            sourceKind,
            passive.ActionModifiers,
            destination,
            ref sourceSequence);
    }

    public static void AppendDefinitions(
        string sourcePrefix,
        DRPGActionModifierSourceKind sourceKind,
        IReadOnlyList<DRPGActionSpecModifierDefinition> definitions,
        ICollection<DRPGResolvedActionModifier> destination,
        ref int sourceSequence)
    {
        if (definitions == null || destination == null)
            return;
        for (int i = 0; i < definitions.Count; i++)
        {
            DRPGActionSpecModifierDefinition definition = definitions[i];
            if (definition == null)
                continue;
            string localId = string.IsNullOrWhiteSpace(definition.ModifierId)
                ? "modifier_" + i
                : definition.ModifierId;
            destination.Add(definition.Resolve(
                sourcePrefix + ":" + localId,
                sourceKind,
                sourceSequence++));
        }
    }
}
