using System;
using System.Collections.Generic;
using UnityEngine;

internal static class DRPGActionSpecContributionResolver
{
    public static DRPGResolvedActionSpec Resolve(
        DRPGBattleCommand command,
        BattleParticipantRuntime actor)
    {
        var context = new DRPGActionSpecResolveContext(command, actor);
        var draft = new DRPGActionSpecDraft(context);
        List<DRPGResolvedActionModifier> modifiers = CollectModifiers(context);
        modifiers.Sort(CompareModifiers);

        var seenSourceIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < modifiers.Count; i++)
        {
            DRPGResolvedActionModifier modifier = modifiers[i];
            string before = draft.CreateSummary();
            if (!seenSourceIds.Add(modifier.SourceId))
            {
                draft.AddTrace(new DRPGActionSpecContributionTrace(
                    modifier.SourceId,
                    modifier.SourceKind,
                    modifier.SourceSequence,
                    false,
                    "duplicate stable source ID",
                    before,
                    before));
                continue;
            }

            if (!modifier.Condition.Matches(context, out string reason))
            {
                draft.AddTrace(new DRPGActionSpecContributionTrace(
                    modifier.SourceId,
                    modifier.SourceKind,
                    modifier.SourceSequence,
                    false,
                    reason,
                    before,
                    before));
                continue;
            }

            draft.Apply(modifier);
            draft.AddTrace(new DRPGActionSpecContributionTrace(
                modifier.SourceId,
                modifier.SourceKind,
                modifier.SourceSequence,
                true,
                string.Empty,
                before,
                draft.CreateSummary()));
        }

        return draft.Freeze();
    }

    private static List<DRPGResolvedActionModifier> CollectModifiers(
        DRPGActionSpecResolveContext context)
    {
        var result = new List<DRPGResolvedActionModifier>();
        IReadOnlyList<DRPGResolvedActionModifier> buildModifiers =
            context.Actor.Build.ActionModifiers;
        if (buildModifiers != null)
        {
            for (int i = 0; i < buildModifiers.Count; i++)
            {
                if (buildModifiers[i] != null)
                    result.Add(buildModifiers[i]);
            }
        }

        int sequence = result.Count;
        IReadOnlyList<DRPGStatusRuntimeSnapshot> statuses =
            context.StatusSnapshots;
        for (int i = 0; i < statuses.Count; i++)
            result.Add(DRPGResolvedActionModifier.FromStatus(statuses[i], sequence++));
        return result;
    }

    private static int CompareModifiers(
        DRPGResolvedActionModifier left,
        DRPGResolvedActionModifier right)
    {
        int kind = left.SourceKind.CompareTo(right.SourceKind);
        if (kind != 0)
            return kind;
        int id = string.Compare(
            left.SourceId,
            right.SourceId,
            StringComparison.Ordinal);
        return id != 0
            ? id
            : left.SourceSequence.CompareTo(right.SourceSequence);
    }
}

internal sealed class DRPGActionSpecDraft
{
    private readonly DRPGActionSpecResolveContext context;
    private readonly DRPGActionScalarAccumulator skillPower;
    private readonly DRPGActionScalarAccumulator damageMultiplier;
    private readonly DRPGActionScalarAccumulator healingMultiplier;
    private readonly DRPGActionScalarAccumulator manaCost;
    private readonly DRPGActionScalarAccumulator postureCost;
    private readonly DRPGActionScalarAccumulator prepareDelay;
    private readonly DRPGActionScalarAccumulator recoveryDelay;
    private readonly DRPGActionScalarAccumulator freeDuration;
    private readonly DRPGActionScalarAccumulator freeCadence;
    private readonly DRPGActionScalarAccumulator freePostureCost;
    private readonly DRPGActionScalarAccumulator freeLineLength;
    private readonly DRPGActionScalarAccumulator freeLineWidth;
    private readonly DRPGActionScalarAccumulator secondaryRadius;
    private readonly DRPGActionScalarAccumulator secondaryDamageMultiplier;
    private readonly DRPGActionScalarAccumulator guardLeadSeconds;
    private readonly DRPGActionScalarAccumulator perfectLeadSeconds;
    private readonly DRPGActionScalarAccumulator guardedDamageMultiplier;
    private readonly DRPGActionScalarAccumulator missedGuardPostureDamage;
    private readonly DRPGActionScalarAccumulator guardedPostureDamage;
    private readonly List<DRPGResolvedAdditionalActionEffect> additionalEffects =
        new List<DRPGResolvedAdditionalActionEffect>();
    private readonly List<DRPGActionSpecContributionTrace> trace =
        new List<DRPGActionSpecContributionTrace>();
    private readonly bool canChargeResources;
    private DRPGFormationRowMask sourceRows;
    private DRPGFormationRowMask targetRows;
    private DRPGDamageTag addedDamageTags;
    private DRPGDamageTag removedDamageTags;
    private DRPGFreeStrikeShape freeShape;
    private DRPGFreeStrikeContactPolicy contactPolicy;
    private int maxPrimaryContacts;
    private DRPGSecondaryImpactKind secondaryImpactKind;

    public DRPGActionSpecDraft(DRPGActionSpecResolveContext context)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
        DRPGSkillSO skill = context.Skill;
        DRPGResolvedCombatantBuild build = context.Actor.Build;
        bool isRelease = context.Command.Stage == DRPGBattleCommandStage.Release;
        canChargeResources = !isRelease;

        skillPower = new DRPGActionScalarAccumulator(
            skill != null ? skill.Power : 0f);
        damageMultiplier = new DRPGActionScalarAccumulator(
            build != null ? build.DamageMultiplier : 1f);
        healingMultiplier = new DRPGActionScalarAccumulator(1f);
        manaCost = new DRPGActionScalarAccumulator(
            !isRelease && skill != null ? skill.ManaCost : 0f);
        postureCost = new DRPGActionScalarAccumulator(
            !isRelease && skill != null ? skill.PostureCost : 0f);
        prepareDelay = new DRPGActionScalarAccumulator(
            skill != null ? skill.PrepareDelay : 0f);
        recoveryDelay = new DRPGActionScalarAccumulator(
            skill != null ? skill.RecoveryDelay : 0f);
        sourceRows = skill != null
            ? skill.SourceFormationRows
            : DRPGFormationRowMask.All;
        targetRows = skill != null
            ? skill.TargetFormationRows
            : DRPGFormationRowMask.All;

        float buildReach = build != null ? build.FreeStrikeReachMultiplier : 1f;
        freeDuration = new DRPGActionScalarAccumulator(
            skill != null
                ? skill.FreeStrikeDuration +
                  (build != null ? build.FreeStrikeDurationBonus : 0f)
                : 0f);
        freeCadence = new DRPGActionScalarAccumulator(
            skill != null
                ? skill.FreeStrikeCadence *
                  (build != null ? build.FreeStrikeCadenceMultiplier : 1f)
                : 0f);
        freePostureCost = new DRPGActionScalarAccumulator(
            skill != null
                ? skill.FreeStrikePostureCost *
                  (build != null ? build.FreeStrikePostureCostMultiplier : 1f)
                : 0f);
        freeLineLength = new DRPGActionScalarAccumulator(
            skill != null ? skill.FreeStrikeLineLength * buildReach : 0f);
        freeLineWidth = new DRPGActionScalarAccumulator(
            skill != null ? skill.FreeStrikeLineWidth * buildReach : 0f);
        secondaryRadius = new DRPGActionScalarAccumulator(
            skill != null ? skill.FreeStrikeSecondaryRadius * buildReach : 0f);
        secondaryDamageMultiplier = new DRPGActionScalarAccumulator(
            skill != null ? skill.FreeStrikeSecondaryDamageMultiplier : 0f);
        freeShape = skill != null ? skill.FreeStrikeShape : default;
        contactPolicy = skill != null
            ? skill.FreeStrikeContactPolicy
            : DRPGFreeStrikeContactPolicy.AllContacts;
        maxPrimaryContacts = skill != null
            ? skill.FreeStrikeMaxPrimaryContacts
            : 1;
        secondaryImpactKind = skill != null
            ? skill.FreeStrikeSecondaryImpactKind
            : DRPGSecondaryImpactKind.None;

        float buildGuardWindow = build != null
            ? build.GuardWindowMultiplier
            : 1f;
        guardLeadSeconds = new DRPGActionScalarAccumulator(
            skill != null ? skill.GuardLeadSeconds * buildGuardWindow : 0f);
        perfectLeadSeconds = new DRPGActionScalarAccumulator(
            skill != null ? skill.PerfectLeadSeconds * buildGuardWindow : 0f);
        guardedDamageMultiplier = new DRPGActionScalarAccumulator(
            skill != null ? skill.GuardedDamageMultiplier : 1f);
        missedGuardPostureDamage = new DRPGActionScalarAccumulator(
            skill != null ? skill.MissedGuardPostureDamage : 0f);
        guardedPostureDamage = new DRPGActionScalarAccumulator(
            skill != null ? skill.GuardedPostureDamage : 0f);
    }

    public void Apply(DRPGResolvedActionModifier modifier)
    {
        skillPower.Apply(
            modifier.SkillPowerAdditive,
            modifier.SkillPowerMultiplier);
        damageMultiplier.Apply(
            modifier.DamageMultiplierAdditive,
            modifier.DamageMultiplier);
        healingMultiplier.Apply(
            modifier.HealingMultiplierAdditive,
            modifier.HealingMultiplier);
        if (canChargeResources)
        {
            manaCost.Apply(
                modifier.ManaCostAdditive,
                modifier.ManaCostMultiplier);
            postureCost.Apply(
                modifier.PostureCostAdditive,
                modifier.PostureCostMultiplier);
        }
        prepareDelay.Apply(
            modifier.PrepareDelayAdditive,
            modifier.PrepareDelayMultiplier);
        recoveryDelay.Apply(
            modifier.RecoveryDelayAdditive,
            modifier.RecoveryDelayMultiplier);

        addedDamageTags |= modifier.AddDamageTags;
        removedDamageTags |= modifier.RemoveDamageTags;
        AddEffects(modifier.AdditionalEffects);
        sourceRows = ApplyFormation(
            sourceRows,
            modifier.SourceFormationMode,
            modifier.SourceFormationRows);
        targetRows = ApplyFormation(
            targetRows,
            modifier.TargetFormationMode,
            modifier.TargetFormationRows);

        freeDuration.Apply(
            modifier.FreeStrikeDurationAdditive,
            modifier.FreeStrikeDurationMultiplier);
        freeCadence.Apply(
            modifier.FreeStrikeCadenceAdditive,
            modifier.FreeStrikeCadenceMultiplier);
        freePostureCost.Apply(
            modifier.FreeStrikePostureCostAdditive,
            modifier.FreeStrikePostureCostMultiplier);
        freeLineLength.Apply(
            modifier.FreeStrikeLineLengthAdditive,
            modifier.FreeStrikeLineLengthMultiplier);
        freeLineWidth.Apply(
            modifier.FreeStrikeLineWidthAdditive,
            modifier.FreeStrikeLineWidthMultiplier);
        secondaryRadius.Apply(
            modifier.SecondaryRadiusAdditive,
            modifier.SecondaryRadiusMultiplier);
        secondaryDamageMultiplier.Apply(
            0f,
            modifier.SecondaryDamageMultiplier);
        if (modifier.OverrideFreeStrikeShape)
            freeShape = modifier.FreeStrikeShape;
        if (modifier.OverrideContactPolicy)
        {
            contactPolicy = modifier.ContactPolicy;
            maxPrimaryContacts = modifier.MaxPrimaryContacts;
        }
        if (modifier.OverrideSecondaryImpactKind)
            secondaryImpactKind = modifier.SecondaryImpactKind;

        guardLeadSeconds.Apply(
            modifier.GuardLeadSecondsAdditive,
            modifier.GuardLeadSecondsMultiplier);
        perfectLeadSeconds.Apply(
            modifier.PerfectLeadSecondsAdditive,
            modifier.PerfectLeadSecondsMultiplier);
        guardedDamageMultiplier.Apply(
            0f,
            modifier.GuardedDamageMultiplier);
        missedGuardPostureDamage.Apply(
            modifier.MissedGuardPostureDamageAdditive,
            modifier.MissedGuardPostureDamageMultiplier);
        guardedPostureDamage.Apply(
            modifier.GuardedPostureDamageAdditive,
            modifier.GuardedPostureDamageMultiplier);
    }

    public void AddTrace(DRPGActionSpecContributionTrace entry)
    {
        if (entry != null)
            trace.Add(entry);
    }

    public string CreateSummary()
    {
        return string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "power={0:0.###};damage={1:0.###};mana={2:0.###};posture={3:0.###};source={4};target={5};tags+={6};tags-={7}",
            skillPower.Value(0f),
            damageMultiplier.Value(0f),
            manaCost.Value(0f),
            postureCost.Value(0f),
            sourceRows,
            targetRows,
            addedDamageTags,
            removedDamageTags);
    }

    public DRPGResolvedActionSpec Freeze()
    {
        DRPGSkillSO skill = context.Skill;
        float resolvedPower = skillPower.Value(0f);
        IReadOnlyList<DRPGResolvedActionEffect> effects = ResolveEffects(
            skill,
            resolvedPower);
        bool hasFreeStrike = skill != null &&
            skill.ExecutionMode == DRPGSkillExecutionMode.FreeStrike;
        bool hasGuard = skill != null &&
            skill.ExecutionMode == DRPGSkillExecutionMode.TelegraphedSpecial;

        return new DRPGResolvedActionSpec(
            context.Command,
            skill != null ? skill.SkillId : string.Empty,
            skill != null ? skill.DisplayName : context.Command.Kind.ToString(),
            skill != null ? skill.PresentationKey : string.Empty,
            skill != null ? skill.AnimationIntent : EAnimationLogicState.None,
            skill != null
                ? skill.ExecutionMode
                : DRPGSkillExecutionMode.Standard,
            skill != null ? skill.TargetRule : DRPGSkillTargetRule.None,
            sourceRows,
            targetRows,
            prepareDelay.Value(0f),
            recoveryDelay.Value(0f),
            manaCost.Value(0f),
            postureCost.Value(0f),
            resolvedPower,
            damageMultiplier.Value(0f),
            healingMultiplier.Value(0f),
            effects,
            hasGuard ? ResolveGuard(skill) : null,
            hasFreeStrike ? ResolveFreeStrike(skill, resolvedPower) : default,
            hasFreeStrike,
            trace.AsReadOnly(),
            skill != null
                ? skill.PostureBreakInterruptionPolicy
                : DRPGPostureBreakInterruptionPolicy.InterruptBeforeExecution,
            skill != null && context.Command.Stage !=
                DRPGBattleCommandStage.Release &&
                skill.ActionProgressCost != null
                    ? skill.ActionProgressCost.Freeze()
                    : DRPGResolvedProgressCost.None,
            skill != null && context.Command.Stage !=
                DRPGBattleCommandStage.Release &&
                skill.InputProgressCost != null
                    ? skill.InputProgressCost.Freeze()
                    : DRPGResolvedProgressCost.None,
            skill != null
                ? skill.SelfPostureDepletionPolicy
                : DRPGSelfPostureDepletionPolicy.ResolveImpactThenEnterPostureBreak,
            skill != null && context.Command.Stage != DRPGBattleCommandStage.Release
                ? skill.StaminaCost
                : 0);
    }

    private IReadOnlyList<DRPGResolvedActionEffect> ResolveEffects(
        DRPGSkillSO skill,
        float resolvedPower)
    {
        IReadOnlyList<DRPGResolvedActionEffect> baseEffects =
            DRPGActionSpecResolver.ResolveEffects(skill, resolvedPower);
        var result = new List<DRPGResolvedActionEffect>(
            baseEffects.Count + additionalEffects.Count);
        for (int i = 0; i < baseEffects.Count; i++)
            result.Add(ApplyTags(baseEffects[i]));
        for (int i = 0; i < additionalEffects.Count; i++)
            result.Add(ApplyTags(additionalEffects[i].Resolve(resolvedPower)));
        return result.AsReadOnly();
    }

    private DRPGResolvedActionEffect ApplyTags(DRPGResolvedActionEffect effect)
    {
        return effect.Kind == DRPGActionEffectKind.Damage
            ? effect.WithDamageTags(addedDamageTags, removedDamageTags)
            : effect;
    }

    private DRPGResolvedGuardSpec ResolveGuard(DRPGSkillSO skill)
    {
        return new DRPGResolvedGuardSpec(
            skill.SpecialImpactTimes,
            guardLeadSeconds.Value(0.01f),
            perfectLeadSeconds.Value(0.01f),
            guardedDamageMultiplier.Value(0f, 1f),
            missedGuardPostureDamage.Value(0f),
            guardedPostureDamage.Value(0f));
    }

    private DRPGResolvedFreeStrikeSpec ResolveFreeStrike(
        DRPGSkillSO skill,
        float resolvedPower)
    {
        return new DRPGResolvedFreeStrikeSpec(
            freeShape,
            resolvedPower,
            freeDuration.Value(0.1f),
            freeCadence.Value(0.01f),
            freePostureCost.Value(0f),
            freeLineLength.Value(0f),
            freeLineWidth.Value(0f),
            contactPolicy,
            maxPrimaryContacts,
            secondaryImpactKind,
            secondaryRadius.Value(0f),
            secondaryDamageMultiplier.Value(0f),
            skill.PresentationKey);
    }

    private void AddEffects(
        IReadOnlyList<DRPGResolvedAdditionalActionEffect> source)
    {
        if (source == null)
            return;
        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] != null)
                additionalEffects.Add(source[i]);
        }
    }

    private static DRPGFormationRowMask ApplyFormation(
        DRPGFormationRowMask current,
        DRPGActionFormationModifierMode mode,
        DRPGFormationRowMask value)
    {
        switch (mode)
        {
            case DRPGActionFormationModifierMode.Intersect:
                return current & value;
            case DRPGActionFormationModifierMode.Override:
                return value;
            default:
                return current;
        }
    }
}

internal sealed class DRPGActionScalarAccumulator
{
    private readonly float baseValue;
    private float additive;
    private float multiplier = 1f;

    public DRPGActionScalarAccumulator(float baseValue)
    {
        this.baseValue = baseValue;
    }

    public void Apply(float additiveValue, float multiplierValue)
    {
        additive += additiveValue;
        multiplier *= multiplierValue;
    }

    public float Value(float minimum, float maximum = float.PositiveInfinity)
    {
        return Mathf.Clamp((baseValue + additive) * multiplier, minimum, maximum);
    }
}
