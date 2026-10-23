using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class DRPGResolvedActionEffect
{
    public DRPGResolvedActionEffect(
        DRPGActionEffectDefinition definition,
        float skillPower)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        Kind = definition.Kind;
        Target = definition.Target;
        MagnitudeMode = definition.MagnitudeMode;
        SkillPowerMultiplier = definition.MagnitudeMode ==
            DRPGEffectMagnitudeMode.SkillPower
            ? definition.Magnitude
            : 0f;
        Magnitude = definition.MagnitudeMode ==
            DRPGEffectMagnitudeMode.SkillPower
            ? skillPower * definition.Magnitude
            : definition.Magnitude;
        DamageTags = definition.DamageTags;
        Status = DRPGResolvedStatusSpec.From(definition.Status);
        StatusStacks = definition.StatusStacks;
        StatusDurationOverride = definition.StatusDurationOverride;
        FormationRowDelta = definition.FormationRowDelta;
        TimelineDelta = definition.TimelineDelta;
        TimelinePriorityDelta = definition.TimelinePriorityDelta;
    }

    internal DRPGResolvedActionEffect(
        DRPGActionEffectKind kind,
        DRPGActionEffectTarget target,
        DRPGEffectMagnitudeMode magnitudeMode,
        float configuredMagnitude,
        float skillPower,
        DRPGDamageTag damageTags,
        DRPGResolvedStatusSpec status,
        int statusStacks,
        int statusDurationOverride,
        int formationRowDelta,
        float timelineDelta,
        int timelinePriorityDelta)
    {
        Kind = kind;
        Target = target;
        MagnitudeMode = magnitudeMode;
        SkillPowerMultiplier = magnitudeMode == DRPGEffectMagnitudeMode.SkillPower
            ? configuredMagnitude
            : 0f;
        Magnitude = magnitudeMode == DRPGEffectMagnitudeMode.SkillPower
            ? skillPower * configuredMagnitude
            : configuredMagnitude;
        DamageTags = damageTags;
        Status = status;
        StatusStacks = Mathf.Max(1, statusStacks);
        StatusDurationOverride = Mathf.Max(0, statusDurationOverride);
        FormationRowDelta = Mathf.Clamp(formationRowDelta, -2, 2);
        TimelineDelta = timelineDelta;
        TimelinePriorityDelta = timelinePriorityDelta;
    }

    private DRPGResolvedActionEffect(
        DRPGActionEffectKind kind,
        DRPGActionEffectTarget target,
        DRPGEffectMagnitudeMode magnitudeMode,
        float magnitude,
        float skillPowerMultiplier,
        DRPGDamageTag damageTags)
    {
        Kind = kind;
        Target = target;
        MagnitudeMode = magnitudeMode;
        Magnitude = magnitude;
        SkillPowerMultiplier = skillPowerMultiplier;
        DamageTags = damageTags;
        StatusStacks = 1;
    }

    private DRPGResolvedActionEffect(
        DRPGActionEffectKind kind,
        DRPGActionEffectTarget target,
        DRPGEffectMagnitudeMode magnitudeMode,
        float magnitude,
        float skillPowerMultiplier,
        DRPGDamageTag damageTags,
        DRPGResolvedStatusSpec status,
        int statusStacks,
        int statusDurationOverride,
        int formationRowDelta,
        float timelineDelta,
        int timelinePriorityDelta,
        bool preserveResolvedMagnitude)
    {
        Kind = kind;
        Target = target;
        MagnitudeMode = magnitudeMode;
        Magnitude = magnitude;
        SkillPowerMultiplier = skillPowerMultiplier;
        DamageTags = damageTags;
        Status = status;
        StatusStacks = statusStacks;
        StatusDurationOverride = statusDurationOverride;
        FormationRowDelta = formationRowDelta;
        TimelineDelta = timelineDelta;
        TimelinePriorityDelta = timelinePriorityDelta;
    }

    public DRPGActionEffectKind Kind { get; }
    public DRPGActionEffectTarget Target { get; }
    public DRPGEffectMagnitudeMode MagnitudeMode { get; }
    public float Magnitude { get; }
    public float SkillPowerMultiplier { get; }
    public bool UsesSkillPower =>
        MagnitudeMode == DRPGEffectMagnitudeMode.SkillPower;
    public DRPGDamageTag DamageTags { get; }
    public DRPGResolvedStatusSpec Status { get; }
    public int StatusStacks { get; }
    public int StatusDurationOverride { get; }
    public int FormationRowDelta { get; }
    public float TimelineDelta { get; }
    public int TimelinePriorityDelta { get; }

    public float ResolveImpactMagnitude(DRPGBattleImpactContext context)
    {
        return UsesSkillPower && context.HasBasePowerOverride
            ? context.BasePowerOverride * SkillPowerMultiplier
            : Magnitude;
    }

    internal DRPGResolvedActionEffect WithDamageTags(
        DRPGDamageTag add,
        DRPGDamageTag remove)
    {
        DRPGDamageTag resolved = (DamageTags | add) & ~remove;
        if (resolved == DamageTags)
            return this;
        return new DRPGResolvedActionEffect(
            Kind,
            Target,
            MagnitudeMode,
            Magnitude,
            SkillPowerMultiplier,
            resolved,
            Status,
            StatusStacks,
            StatusDurationOverride,
            FormationRowDelta,
            TimelineDelta,
            TimelinePriorityDelta,
            true);
    }

    public static DRPGResolvedActionEffect Legacy(
        DRPGActionEffectKind kind,
        DRPGActionEffectTarget target,
        float skillPower,
        DRPGDamageTag damageTags = DRPGDamageTag.None)
    {
        return new DRPGResolvedActionEffect(
            kind,
            target,
            DRPGEffectMagnitudeMode.SkillPower,
            skillPower,
            1f,
            damageTags);
    }
}

public sealed class DRPGResolvedGuardSpec
{
    public DRPGResolvedGuardSpec(DRPGSkillSO skill)
    {
        if (skill == null)
            throw new ArgumentNullException(nameof(skill));

        IReadOnlyList<float> source = skill.SpecialImpactTimes;
        var impactTimes = new float[source.Count];
        for (int i = 0; i < source.Count; i++)
            impactTimes[i] = Mathf.Max(0f, source[i]);
        ImpactTimes = Array.AsReadOnly(impactTimes);
        GuardLeadSeconds = skill.GuardLeadSeconds;
        PerfectLeadSeconds = skill.PerfectLeadSeconds;
        GuardedDamageMultiplier = skill.GuardedDamageMultiplier;
        MissedGuardPostureDamage = skill.MissedGuardPostureDamage;
        GuardedPostureDamage = skill.GuardedPostureDamage;
    }

    internal DRPGResolvedGuardSpec(
        IReadOnlyList<float> impactTimes,
        float guardLeadSeconds,
        float perfectLeadSeconds,
        float guardedDamageMultiplier,
        float missedGuardPostureDamage,
        float guardedPostureDamage)
    {
        int count = impactTimes != null ? impactTimes.Count : 0;
        var frozenTimes = new float[count];
        for (int i = 0; i < count; i++)
            frozenTimes[i] = Mathf.Max(0f, impactTimes[i]);
        ImpactTimes = Array.AsReadOnly(frozenTimes);
        GuardLeadSeconds = Mathf.Max(0.01f, guardLeadSeconds);
        PerfectLeadSeconds = Mathf.Clamp(
            perfectLeadSeconds,
            0.01f,
            GuardLeadSeconds);
        GuardedDamageMultiplier = Mathf.Clamp01(guardedDamageMultiplier);
        MissedGuardPostureDamage = Mathf.Max(0f, missedGuardPostureDamage);
        GuardedPostureDamage = Mathf.Max(0f, guardedPostureDamage);
    }

    public IReadOnlyList<float> ImpactTimes { get; }
    public float GuardLeadSeconds { get; }
    public float PerfectLeadSeconds { get; }
    public float GuardedDamageMultiplier { get; }
    public float MissedGuardPostureDamage { get; }
    public float GuardedPostureDamage { get; }
}

public sealed class DRPGResolvedActionSpec
{
    public DRPGResolvedActionSpec(
        DRPGBattleCommand command,
        string skillId,
        string displayName,
        string presentationKey,
        EAnimationLogicState animationIntent,
        DRPGSkillExecutionMode executionMode,
        DRPGSkillTargetRule targetRule,
        DRPGFormationRowMask sourceFormationRows,
        DRPGFormationRowMask targetFormationRows,
        float prepareDelay,
        float recoveryDelay,
        float manaCost,
        float postureCost,
        float resolvedSkillPower,
        float actorDamageMultiplier,
        float actorHealingMultiplier,
        IReadOnlyList<DRPGResolvedActionEffect> effects,
        DRPGResolvedGuardSpec guardSpec,
        DRPGResolvedFreeStrikeSpec freeStrikeSpec,
        bool hasFreeStrikeSpec,
        IReadOnlyList<DRPGActionSpecContributionTrace> contributionTrace = null,
        DRPGPostureBreakInterruptionPolicy postureBreakInterruptionPolicy =
            DRPGPostureBreakInterruptionPolicy.InterruptBeforeExecution,
        DRPGResolvedProgressCost actionProgressCost = null,
        DRPGResolvedProgressCost inputProgressCost = null,
        DRPGSelfPostureDepletionPolicy selfPostureDepletionPolicy =
            DRPGSelfPostureDepletionPolicy.ResolveImpactThenEnterPostureBreak,
        int staminaCost = 0)
    {
        CommandKind = command != null
            ? command.Kind
            : DRPGBattleCommandKind.Wait;
        Stage = command != null
            ? command.Stage
            : DRPGBattleCommandStage.Immediate;
        ActorRuntimeId = command != null ? command.ActorRuntimeId : 0;
        TargetRuntimeId = command != null ? command.TargetRuntimeId : 0;
        SkillId = skillId ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        PresentationKey = presentationKey ?? string.Empty;
        AnimationIntent = animationIntent;
        ExecutionMode = executionMode;
        TargetRule = targetRule;
        SourceFormationRows = sourceFormationRows;
        TargetFormationRows = targetFormationRows;
        PrepareDelay = Mathf.Max(0f, prepareDelay);
        RecoveryDelay = Mathf.Max(0f, recoveryDelay);
        ManaCost = Mathf.Max(0f, manaCost);
        PostureCost = Mathf.Max(0f, postureCost);
        StaminaCost = Mathf.Max(0, staminaCost);
        ResolvedSkillPower = Mathf.Max(0f, resolvedSkillPower);
        ActorDamageMultiplier = Mathf.Max(0f, actorDamageMultiplier);
        ActorHealingMultiplier = Mathf.Max(0f, actorHealingMultiplier);
        Effects = Copy(effects);
        GuardSpec = guardSpec;
        FreeStrikeSpec = freeStrikeSpec;
        HasFreeStrikeSpec = hasFreeStrikeSpec;
        ContributionTrace = CopyTrace(contributionTrace);
        PostureBreakInterruptionPolicy = postureBreakInterruptionPolicy;
        ActionProgressCost = actionProgressCost ??
            DRPGResolvedProgressCost.None;
        InputProgressCost = inputProgressCost ??
            DRPGResolvedProgressCost.None;
        SelfPostureDepletionPolicy = selfPostureDepletionPolicy;
    }

    public DRPGBattleCommandKind CommandKind { get; }
    public DRPGBattleCommandStage Stage { get; }
    public int ActorRuntimeId { get; }
    public int TargetRuntimeId { get; }
    public string SkillId { get; }
    public string DisplayName { get; }
    public string PresentationKey { get; }
    public EAnimationLogicState AnimationIntent { get; }
    public DRPGSkillExecutionMode ExecutionMode { get; }
    public DRPGSkillTargetRule TargetRule { get; }
    public DRPGFormationRowMask SourceFormationRows { get; }
    public DRPGFormationRowMask TargetFormationRows { get; }
    public float PrepareDelay { get; }
    public float RecoveryDelay { get; }
    public float ManaCost { get; }
    public float PostureCost { get; }
    public int StaminaCost { get; }
    public float ResolvedSkillPower { get; }
    public float ActorDamageMultiplier { get; }
    public float ActorHealingMultiplier { get; }
    public IReadOnlyList<DRPGResolvedActionEffect> Effects { get; }
    public DRPGResolvedGuardSpec GuardSpec { get; }
    public DRPGResolvedFreeStrikeSpec FreeStrikeSpec { get; }
    public bool HasFreeStrikeSpec { get; }
    public IReadOnlyList<DRPGActionSpecContributionTrace> ContributionTrace { get; }
    public DRPGPostureBreakInterruptionPolicy
        PostureBreakInterruptionPolicy { get; }
    public DRPGResolvedProgressCost ActionProgressCost { get; }
    public DRPGResolvedProgressCost InputProgressCost { get; }
    public DRPGSelfPostureDepletionPolicy SelfPostureDepletionPolicy { get; }

    public bool CanTarget(
        BattleParticipantRuntime actor,
        BattleParticipantRuntime target)
    {
        if (actor == null || target == null ||
            actor.RuntimeId != ActorRuntimeId ||
            !actor.IsAlive || !actor.IsDeployed ||
            !target.IsAlive || !target.IsDeployed ||
            !SourceFormationRows.Contains(actor.FormationRow) ||
            !TargetFormationRows.Contains(target.FormationRow))
        {
            return false;
        }

        switch (TargetRule)
        {
            case DRPGSkillTargetRule.SingleAlly:
            case DRPGSkillTargetRule.AllAllies:
                return actor.Side == target.Side;
            case DRPGSkillTargetRule.Self:
                return ReferenceEquals(actor, target);
            case DRPGSkillTargetRule.BattlefieldPoint:
                return ExecutionMode == DRPGSkillExecutionMode.FreeStrike &&
                    actor.Side != target.Side;
            case DRPGSkillTargetRule.None:
                return false;
            default:
                return actor.Side != target.Side;
        }
    }

    public DRPGResolvedActionSpec CreateRelease(DRPGBattleCommand releaseCommand)
    {
        if (releaseCommand == null ||
            releaseCommand.Stage != DRPGBattleCommandStage.Release)
        {
            throw new ArgumentException(
                "A telegraphed snapshot can only create a release spec.",
                nameof(releaseCommand));
        }

        return new DRPGResolvedActionSpec(
            releaseCommand,
            SkillId,
            DisplayName,
            PresentationKey,
            AnimationIntent,
            ExecutionMode,
            TargetRule,
            SourceFormationRows,
            TargetFormationRows,
            PrepareDelay,
            RecoveryDelay,
            0f,
            0f,
            ResolvedSkillPower,
            ActorDamageMultiplier,
            ActorHealingMultiplier,
            Effects,
            GuardSpec,
            FreeStrikeSpec,
            HasFreeStrikeSpec,
            ContributionTrace,
            PostureBreakInterruptionPolicy,
            DRPGResolvedProgressCost.None,
            InputProgressCost,
            SelfPostureDepletionPolicy,
            0);
    }

    public DRPGResolvedActionSpec CreateReaction(
        DRPGBattleCommand reactionCommand)
    {
        if (reactionCommand == null)
            throw new ArgumentNullException(nameof(reactionCommand));
        if (reactionCommand.ActorRuntimeId != ActorRuntimeId)
        {
            throw new ArgumentException(
                "A reaction command must preserve the resolved actor.",
                nameof(reactionCommand));
        }

        return new DRPGResolvedActionSpec(
            reactionCommand,
            SkillId,
            DisplayName,
            PresentationKey,
            AnimationIntent,
            DRPGSkillExecutionMode.Standard,
            TargetRule,
            SourceFormationRows,
            TargetFormationRows,
            0f,
            0f,
            0f,
            0f,
            ResolvedSkillPower,
            ActorDamageMultiplier,
            ActorHealingMultiplier,
            Effects,
            null,
            default,
            false,
            ContributionTrace,
            PostureBreakInterruptionPolicy,
            DRPGResolvedProgressCost.None,
            DRPGResolvedProgressCost.None,
            SelfPostureDepletionPolicy,
            0);
    }

    private static IReadOnlyList<DRPGResolvedActionEffect> Copy(
        IReadOnlyList<DRPGResolvedActionEffect> source)
    {
        if (source == null || source.Count == 0)
            return Array.Empty<DRPGResolvedActionEffect>();
        var result = new DRPGResolvedActionEffect[source.Count];
        for (int i = 0; i < source.Count; i++)
            result[i] = source[i];
        return Array.AsReadOnly(result);
    }

    private static IReadOnlyList<DRPGActionSpecContributionTrace> CopyTrace(
        IReadOnlyList<DRPGActionSpecContributionTrace> source)
    {
        if (source == null || source.Count == 0)
            return Array.Empty<DRPGActionSpecContributionTrace>();
        var result = new DRPGActionSpecContributionTrace[source.Count];
        for (int i = 0; i < source.Count; i++)
            result[i] = source[i];
        return Array.AsReadOnly(result);
    }
}

public static class DRPGActionSpecResolver
{
    public static DRPGResolvedActionSpec Resolve(
        DRPGBattleCommand command,
        BattleParticipantRuntime actor)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));
        if (actor == null)
            throw new ArgumentNullException(nameof(actor));
        if (command.ActorRuntimeId != actor.RuntimeId)
        {
            throw new InvalidOperationException(
                "The action command does not belong to the supplied actor.");
        }

        return DRPGActionSpecContributionResolver.Resolve(command, actor);
    }

    internal static IReadOnlyList<DRPGResolvedActionEffect> ResolveEffects(
        DRPGSkillSO skill,
        float skillPower)
    {
        if (skill == null)
            return Array.Empty<DRPGResolvedActionEffect>();

        IReadOnlyList<DRPGActionEffectDefinition> authored = skill.Effects;
        if (authored != null && authored.Count > 0)
        {
            var result = new List<DRPGResolvedActionEffect>(authored.Count);
            for (int i = 0; i < authored.Count; i++)
            {
                if (authored[i] != null)
                    result.Add(new DRPGResolvedActionEffect(authored[i], skillPower));
            }
            return result.AsReadOnly();
        }

        DRPGActionEffectTarget target = ResolveLegacyTarget(skill.TargetRule);
        switch (skill.Purpose)
        {
            case DRPGSkillPurpose.Healing:
                return Array.AsReadOnly(new[]
                {
                    DRPGResolvedActionEffect.Legacy(
                        DRPGActionEffectKind.Heal,
                        target,
                        skillPower)
                });
            case DRPGSkillPurpose.Defense:
            case DRPGSkillPurpose.SpecialDefense:
                return Array.AsReadOnly(new[]
                {
                    DRPGResolvedActionEffect.Legacy(
                        DRPGActionEffectKind.Shield,
                        target,
                        skillPower)
                });
            case DRPGSkillPurpose.Utility:
                return Array.Empty<DRPGResolvedActionEffect>();
            default:
                return Array.AsReadOnly(new[]
                {
                    DRPGResolvedActionEffect.Legacy(
                        DRPGActionEffectKind.Damage,
                        target,
                        skillPower)
                });
        }
    }

    private static DRPGActionEffectTarget ResolveLegacyTarget(
        DRPGSkillTargetRule targetRule)
    {
        switch (targetRule)
        {
            case DRPGSkillTargetRule.Self:
                return DRPGActionEffectTarget.Actor;
            case DRPGSkillTargetRule.AllEnemies:
                return DRPGActionEffectTarget.AllOpponents;
            case DRPGSkillTargetRule.AllAllies:
                return DRPGActionEffectTarget.AllAllies;
            default:
                return DRPGActionEffectTarget.PrimaryTarget;
        }
    }

    private static DRPGResolvedFreeStrikeSpec ResolveFreeStrike(
        DRPGSkillSO skill,
        DRPGResolvedCombatantBuild build)
    {
        float reach = build != null ? build.FreeStrikeReachMultiplier : 1f;
        return new DRPGResolvedFreeStrikeSpec(
            skill.FreeStrikeShape,
            skill.Power,
            skill.FreeStrikeDuration +
                (build != null ? build.FreeStrikeDurationBonus : 0f),
            skill.FreeStrikeCadence *
                (build != null ? build.FreeStrikeCadenceMultiplier : 1f),
            skill.FreeStrikePostureCost *
                (build != null ? build.FreeStrikePostureCostMultiplier : 1f),
            skill.FreeStrikeLineLength * reach,
            skill.FreeStrikeLineWidth * reach,
            skill.FreeStrikeContactPolicy,
            skill.FreeStrikeMaxPrimaryContacts,
            skill.FreeStrikeSecondaryImpactKind,
            skill.FreeStrikeSecondaryRadius * reach,
            skill.FreeStrikeSecondaryDamageMultiplier,
            skill.PresentationKey);
    }
}
