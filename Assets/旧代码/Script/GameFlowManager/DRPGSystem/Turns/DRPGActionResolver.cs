using System;
using System.Collections.Generic;

internal enum DRPGWorkspaceResolutionStatus
{
    Succeeded = 0,
    Unsupported = 1,
    Rejected = 2
}

internal readonly struct DRPGWorkspaceResolutionResult
{
    private DRPGWorkspaceResolutionResult(
        DRPGWorkspaceResolutionStatus status,
        DRPGResolvedActionBatch batch,
        string error)
    {
        Status = status;
        Batch = batch;
        Error = error ?? string.Empty;
    }

    internal DRPGWorkspaceResolutionStatus Status { get; }
    internal DRPGResolvedActionBatch Batch { get; }
    internal string Error { get; }

    internal static DRPGWorkspaceResolutionResult Unsupported(string error)
    {
        return new DRPGWorkspaceResolutionResult(
            DRPGWorkspaceResolutionStatus.Unsupported,
            null,
            error);
    }

    internal static DRPGWorkspaceResolutionResult Rejected(string error)
    {
        return new DRPGWorkspaceResolutionResult(
            DRPGWorkspaceResolutionStatus.Rejected,
            null,
            error);
    }

    internal static DRPGWorkspaceResolutionResult Succeeded(
        DRPGResolvedActionBatch batch)
    {
        return new DRPGWorkspaceResolutionResult(
            DRPGWorkspaceResolutionStatus.Succeeded,
            batch ?? throw new ArgumentNullException(nameof(batch)),
            string.Empty);
    }
}

public sealed class DRPGActionResolver
{
    internal DRPGWorkspaceResolutionResult ResolveIntoWorkspace(
        DRPGResolvedActionSpec spec,
        BattleParticipantRuntime actor,
        BattleParticipantRuntime primaryTarget,
        IReadOnlyList<BattleParticipantRuntime> participants,
        DRPGBattleMutationWorkspace workspace,
        DRPGBattleImpactContext context)
    {
        return ResolveIntoWorkspace(
            spec,
            actor,
            primaryTarget,
            participants,
            workspace,
            context,
            ResolveHitOutcome(context));
    }

    internal DRPGWorkspaceResolutionResult ResolveIntoWorkspace(
        DRPGResolvedActionSpec spec,
        BattleParticipantRuntime actor,
        BattleParticipantRuntime primaryTarget,
        IReadOnlyList<BattleParticipantRuntime> participants,
        DRPGBattleMutationWorkspace workspace,
        DRPGBattleImpactContext context,
        DRPGHitOutcome outcome)
    {
        if (spec == null || actor == null || workspace == null)
        {
            return DRPGWorkspaceResolutionResult.Rejected(
                "Workspace resolution requires a spec, actor, and workspace.");
        }
        if (spec.ActorRuntimeId != actor.RuntimeId)
        {
            return DRPGWorkspaceResolutionResult.Rejected(
                "Resolved action spec belongs to another actor.");
        }
        if (!Enum.IsDefined(typeof(DRPGHitOutcome), outcome))
        {
            return DRPGWorkspaceResolutionResult.Rejected(
                "Workspace resolution requires a valid hit outcome.");
        }
        if (!workspace.Snapshot.TryGetParticipant(
                new ParticipantId(actor.RuntimeId),
                out _))
        {
            return DRPGWorkspaceResolutionResult.Rejected(
                "The actor is not captured by the mutation workspace.");
        }
        if (primaryTarget != null &&
            !workspace.Snapshot.TryGetParticipant(
                new ParticipantId(primaryTarget.RuntimeId),
                out _))
        {
            return DRPGWorkspaceResolutionResult.Rejected(
                "The primary target is not captured by the mutation workspace.");
        }
        int damageEffects = 0;
        foreach (var effect in spec.Effects)
        {
            if (effect.Kind == DRPGActionEffectKind.Damage) damageEffects++;
            else if (effect.Kind != DRPGActionEffectKind.ApplyStatus && effect.Kind != DRPGActionEffectKind.TimelineShift && effect.Kind != DRPGActionEffectKind.PostureDamage &&
                !DRPGActionVitalChange.Supports(effect.Kind))
                return DRPGWorkspaceResolutionResult.Unsupported("Workspace action does not yet support this effect kind.");
            if (!Enum.IsDefined(typeof(DRPGActionEffectTarget), effect.Target))
                return DRPGWorkspaceResolutionResult.Rejected("Invalid workspace effect target rule.");
        }
        if (spec.Effects.Count == 0 || damageEffects > 1)
            return DRPGWorkspaceResolutionResult.Unsupported("Workspace action requires nonempty effects and at most one damage effect.");
        if (damageEffects == 0 && outcome != DRPGHitOutcome.Hit)
            return DRPGWorkspaceResolutionResult.Rejected("A pure status step cannot manufacture a guarded or evaded hit.");

        if (outcome == DRPGHitOutcome.Evaded)
        {
            if (primaryTarget == null ||
                !workspace.Snapshot.TryGetParticipant(
                    new ParticipantId(primaryTarget.RuntimeId),
                    out _))
            {
                return DRPGWorkspaceResolutionResult.Rejected(
                    "An Evaded primary impact requires a legal living target.");
            }
            ParticipantMutationBuilder targetMutation = workspace.For(
                new ParticipantId(primaryTarget.RuntimeId));
            if (targetMutation.HitPoints <= 0f || !targetMutation.IsDeployed)
            {
                return DRPGWorkspaceResolutionResult.Rejected(
                    "An Evaded primary impact requires a legal living target.");
            }
            var evaded = new DRPGBattleHitResult(
                actor.RuntimeId,
                primaryTarget.RuntimeId,
                spec.SkillId,
                targetMutation.HitPoints,
                0f,
                targetMutation.HitPoints,
                false,
                false,
                primaryTarget.RosterRole,
                string.Empty,
                targetMutation.Posture,
                0f,
                targetMutation.Posture,
                context.GuardGrade,
                context.StrikeGrade,
                0f,
                null,
                targetMutation.Posture <= 0f,
                DRPGHitOutcome.Evaded);
            return DRPGWorkspaceResolutionResult.Succeeded(
                new DRPGResolvedActionBatch(
                    Array.AsReadOnly(new[] { evaded }),
                    Array.Empty<DRPGActionEffectResult>()));
        }

        var hits = new List<DRPGBattleHitResult>();
        var effectResults = new List<DRPGActionEffectResult>();
        try
        {
            foreach (var effect in spec.Effects)
            {
                var targets = ResolveWorkspaceTargets(effect.Target, actor, primaryTarget, participants, workspace, out string targetError);
                if (!string.IsNullOrEmpty(targetError)) return DRPGWorkspaceResolutionResult.Rejected(targetError);
                if (targets.Count == 0)
                    effectResults.Add(Skipped(effect.Kind, actor.RuntimeId,
                        effect.Target == DRPGActionEffectTarget.Actor ? actor.RuntimeId : primaryTarget != null ? primaryTarget.RuntimeId : 0,
                        "The resolved effect has no legal living target."));
                foreach (var target in targets)
                {
                    if (effect.Kind == DRPGActionEffectKind.Damage)
                        ApplyWorkspaceDamage(spec, effect, actor, target, context, outcome,
                            ReferenceEquals(target, primaryTarget) && context.PostureDamage > 0f, workspace, hits, effectResults);
                    else if (effect.Kind == DRPGActionEffectKind.TimelineShift)
                        effectResults.Add(new DRPGActionEffectResult(actor.RuntimeId, DRPGActionTimelineChange.Stage(workspace,
                            new ParticipantId(target.RuntimeId), effect.TimelineDelta, effect.TimelinePriorityDelta)));
                    else if (DRPGActionVitalChange.Supports(effect.Kind))
                        effectResults.Add(new DRPGActionEffectResult(actor.RuntimeId, DRPGActionVitalChange.Stage(
                            workspace.For(new ParticipantId(target.RuntimeId)), effect.Kind,
                            Math.Max(0f, effect.ResolveImpactMagnitude(context) *
                                (effect.Kind == DRPGActionEffectKind.Heal ? spec.ActorHealingMultiplier : 1f)))));
                    else if (effect.Kind == DRPGActionEffectKind.PostureDamage)
                        effectResults.Add(new DRPGActionEffectResult(actor.RuntimeId, DRPGActionPostureChange.Stage(
                            workspace.For(new ParticipantId(target.RuntimeId)), Math.Max(0f, effect.ResolveImpactMagnitude(context)))));
                    else
                        effectResults.Add(new DRPGActionEffectResult(actor.RuntimeId,
                            DRPGActionStatusChange.Stage(workspace.For(new ParticipantId(target.RuntimeId)), effect, new ParticipantId(actor.RuntimeId))));
                    workspace.For(new ParticipantId(target.RuntimeId)).BuildValidated();
                }
            }
        }
        catch (Exception error) { return DRPGWorkspaceResolutionResult.Rejected(error.Message); }

        return DRPGWorkspaceResolutionResult.Succeeded(
            new DRPGResolvedActionBatch(hits, effectResults));
    }

    internal DRPGBattleHitResult Resolve(
        DRPGBattleCommand command,
        BattleParticipantRuntime actor,
        BattleParticipantRuntime target)
    {
        return Resolve(
            command,
            actor,
            target,
            DRPGBattleImpactContext.ForStrike(
                DRPGStrikeGrade.Body,
                1f));
    }

    internal DRPGBattleHitResult Resolve(
        DRPGBattleCommand command,
        BattleParticipantRuntime actor,
        BattleParticipantRuntime target,
        DRPGBattleImpactContext context)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));
        if (actor == null)
            throw new ArgumentNullException(nameof(actor));
        if (target == null)
            throw new ArgumentNullException(nameof(target));

        DRPGResolvedActionBatch batch = Resolve(
            DRPGActionSpecResolver.Resolve(command, actor),
            actor,
            target,
            new CompatibilityEnvironment(actor, target),
            context);
        if (batch.Hits.Count > 0)
            return batch.Hits[0];

        return new DRPGBattleHitResult(
            actor.RuntimeId,
            target.RuntimeId,
            command.Skill != null ? command.Skill.SkillId : string.Empty,
            target.CurrentHp,
            0f,
            target.CurrentHp,
            !target.IsAlive,
            false,
            target.RosterRole,
            string.Empty,
            target.CurrentPosture,
            0f,
            target.CurrentPosture,
            context.GuardGrade,
            context.StrikeGrade,
            outcome: ResolveHitOutcome(context));
    }

    internal DRPGResolvedActionBatch Resolve(
        DRPGResolvedActionSpec spec,
        BattleParticipantRuntime actor,
        BattleParticipantRuntime primaryTarget,
        IDRPGBattleEffectEnvironment environment,
        DRPGBattleImpactContext context)
    {
        if (spec == null)
            throw new ArgumentNullException(nameof(spec));
        if (actor == null)
            throw new ArgumentNullException(nameof(actor));
        if (environment == null)
            throw new ArgumentNullException(nameof(environment));
        if (spec.ActorRuntimeId != actor.RuntimeId)
        {
            throw new InvalidOperationException(
                "Resolved action spec belongs to another actor.");
        }

        var hits = new List<DRPGBattleHitResult>();
        var effectResults = new List<DRPGActionEffectResult>();
        bool impactPostureApplied = false;

        for (int i = 0; i < spec.Effects.Count; i++)
        {
            DRPGResolvedActionEffect effect = spec.Effects[i];
            IReadOnlyList<BattleParticipantRuntime> targets = ResolveTargets(
                effect.Target,
                actor,
                primaryTarget,
                environment.Participants);
            if (targets.Count == 0)
            {
                effectResults.Add(Skipped(
                    effect.Kind,
                    actor.RuntimeId,
                    primaryTarget != null ? primaryTarget.RuntimeId : 0,
                    "The resolved effect has no legal living target."));
                continue;
            }

            for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
            {
                BattleParticipantRuntime target = targets[targetIndex];
                bool applyImpactPosture = !impactPostureApplied &&
                    context.PostureDamage > 0f &&
                    ReferenceEquals(target, primaryTarget) &&
                    effect.Kind == DRPGActionEffectKind.Damage;
                ApplyEffect(
                    spec,
                    effect,
                    actor,
                    target,
                    environment,
                    context,
                    applyImpactPosture,
                    hits,
                    effectResults);
                if (applyImpactPosture)
                    impactPostureApplied = true;
            }
        }

        if (!impactPostureApplied && context.PostureDamage > 0f &&
            primaryTarget != null && primaryTarget.IsAlive)
        {
            float before = primaryTarget.CurrentPosture;
            float applied = primaryTarget.ReducePosture(context.PostureDamage);
            effectResults.Add(new DRPGActionEffectResult(
                DRPGActionEffectKind.PostureDamage,
                DRPGActionEffectResultState.Applied,
                actor.RuntimeId,
                primaryTarget.RuntimeId,
                context.PostureDamage,
                applied,
                before - primaryTarget.CurrentPosture,
                "impact_posture",
                postureBefore: before,
                postureAfter: primaryTarget.CurrentPosture));
        }

        return new DRPGResolvedActionBatch(hits, effectResults);
    }

    private static IReadOnlyList<BattleParticipantRuntime>
        ResolveWorkspaceTargets(
            DRPGActionEffectTarget targetRule,
            BattleParticipantRuntime actor,
            BattleParticipantRuntime primaryTarget,
            IReadOnlyList<BattleParticipantRuntime> participants,
            DRPGBattleMutationWorkspace workspace,
            out string error)
    {
        error = string.Empty;
        if (targetRule == DRPGActionEffectTarget.Actor)
        {
            return IsWorkspaceTargetAlive(actor, workspace, out error)
                ? Array.AsReadOnly(new[] { actor })
                : Array.Empty<BattleParticipantRuntime>();
        }
        if (targetRule == DRPGActionEffectTarget.PrimaryTarget)
        {
            return primaryTarget != null &&
                   IsWorkspaceTargetAlive(primaryTarget, workspace, out error)
                ? Array.AsReadOnly(new[] { primaryTarget })
                : Array.Empty<BattleParticipantRuntime>();
        }

        if (participants == null)
        {
            error = "Area effects require the captured participant roster.";
            return Array.Empty<BattleParticipantRuntime>();
        }
        bool allies = targetRule == DRPGActionEffectTarget.AllAllies;
        var result = new List<BattleParticipantRuntime>();
        for (int i = 0; i < participants.Count; i++)
        {
            BattleParticipantRuntime candidate = participants[i];
            if (candidate == null)
                continue;
            ParticipantMutationBuilder builder;
            try
            {
                builder = workspace.For(new ParticipantId(candidate.RuntimeId));
            }
            catch (InvalidOperationException exception)
            {
                error = exception.Message;
                return Array.Empty<BattleParticipantRuntime>();
            }
            if (builder.HitPoints <= 0f || !builder.IsDeployed)
                continue;
            if ((candidate.Side == actor.Side) == allies)
                result.Add(candidate);
        }
        return result.AsReadOnly();
    }

    private static bool IsWorkspaceTargetAlive(
        BattleParticipantRuntime target,
        DRPGBattleMutationWorkspace workspace,
        out string error)
    {
        error = string.Empty;
        if (target == null)
            return false;
        try
        {
            var draft = workspace.For(new ParticipantId(target.RuntimeId));
            return draft.HitPoints > 0f && draft.IsDeployed;
        }
        catch (InvalidOperationException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static void ApplyWorkspaceDamage(
        DRPGResolvedActionSpec spec,
        DRPGResolvedActionEffect effect,
        BattleParticipantRuntime actor,
        BattleParticipantRuntime target,
        DRPGBattleImpactContext context,
        DRPGHitOutcome outcome,
        bool applyImpactPosture,
        DRPGBattleMutationWorkspace workspace,
        ICollection<DRPGBattleHitResult> hits,
        ICollection<DRPGActionEffectResult> effectResults)
    {
        ParticipantMutationBuilder targetMutation = workspace.For(
            new ParticipantId(target.RuntimeId));
        float hpBefore = targetMutation.HitPoints;
        float postureBefore = targetMutation.Posture;
        float shieldBefore = targetMutation.Shield;
        bool wasPostureBreakExposed = postureBefore <= 0f;
        bool wasDamageBroken = target.LowHpState ==
                DRPGLowHpPresentationState.DamageBroken ||
            hpBefore / target.EffectiveMaxHp <=
                target.Profile.DamageBrokenHpRatio;
        float baseMagnitude = Math.Max(
            0f,
            effect.ResolveImpactMagnitude(context));
        float impactMultiplier = context.DealsDamage ? 1f : 0f;
        float affinityMultiplier =
            target.ResolveDamageResponseMultiplier(effect.DamageTags);
        float statusMultiplier = targetMutation.CreateStatusProjection()
            .ResolveActionModifiers()
            .IncomingDamageMultiplier;
        float defenseMultiplier = targetMutation.IncomingDefenseMultiplier;
        int panel = context.DealsDamage
            ? BattleEffectPanel.Calculate(
                baseMagnitude,
                0d,
                Math.Max(0f, spec.ActorDamageMultiplier) - 1d)
            : 0;
        int targetAdjustedPanel = BattleIntegerMath.RoundNonnegative(
            (double)panel * affinityMultiplier * statusMultiplier * defenseMultiplier);
        int resistance = target.ResolveBattleResistance(effect.DamageTags);
        DRPGPostureRuleSnapshot postureStateBefore = targetMutation.PostureState;
        bool defendingBefore = targetMutation.IsDefending;
        BattleDamageResult layered = BattleLayeredHealthMutation.Apply(
            targetMutation,
            targetAdjustedPanel,
            resistance);
        float requestedDamage = layered.Damage;
        float shieldAbsorbed = 0f;
        float appliedDamage = layered.HpDamage;
        float hpAfter = layered.HpAfter;
        float postureAfter = layered.PostureAfter;
        float appliedPosture = postureBefore - postureAfter;
        DRPGActionPostureChange postureChange = appliedPosture > 0f
            ? new DRPGActionPostureChange(
                targetMutation.Snapshot.Participant,
                appliedPosture,
                postureBefore,
                postureAfter,
                postureStateBefore,
                targetMutation.PostureState,
                defendingBefore,
                targetMutation.IsDefending,
                true)
            : null;

        bool triggersDamageBroken = !wasDamageBroken &&
            hpAfter / target.EffectiveMaxHp <=
                target.Profile.DamageBrokenHpRatio;
        var breakdown = new DRPGDamageBreakdown(
            baseMagnitude,
            spec.ActorDamageMultiplier,
            impactMultiplier,
            affinityMultiplier,
            statusMultiplier,
            defenseMultiplier,
            requestedDamage,
            shieldAbsorbed,
            appliedDamage,
            effect.DamageTags);
        hits.Add(new DRPGBattleHitResult(
            actor.RuntimeId,
            target.RuntimeId,
            spec.SkillId,
            hpBefore,
            appliedDamage,
            hpAfter,
            hpAfter <= 0f,
            triggersDamageBroken,
            target.RosterRole,
            string.Empty,
            postureBefore,
            appliedPosture,
            postureAfter,
            context.GuardGrade,
            context.StrikeGrade,
            shieldAbsorbed,
            breakdown,
            wasPostureBreakExposed,
            postureBefore > 0f && layered.Damage > 0
                ? DRPGHitOutcome.Guarded
                : outcome));
        effectResults.Add(new DRPGActionEffectResult(
            effect.Kind,
            DRPGActionEffectResultState.Applied,
            actor.RuntimeId,
            target.RuntimeId,
            requestedDamage,
            appliedDamage,
            shieldAbsorbed,
            spec.SkillId,
            string.Empty,
            effect.DamageTags));
        if (postureChange != null)
        {
            effectResults.Add(new DRPGActionEffectResult(actor.RuntimeId, postureChange));
        }
    }

    private static DRPGHitOutcome ResolveHitOutcome(
        DRPGBattleImpactContext context)
    {
        return context.GuardGrade == DRPGGuardGrade.Guard ||
               context.GuardGrade == DRPGGuardGrade.Perfect
            ? DRPGHitOutcome.Guarded
            : DRPGHitOutcome.Hit;
    }

    private static void ApplyEffect(
        DRPGResolvedActionSpec spec,
        DRPGResolvedActionEffect effect,
        BattleParticipantRuntime actor,
        BattleParticipantRuntime target,
        IDRPGBattleEffectEnvironment environment,
        DRPGBattleImpactContext context,
        bool applyImpactPosture,
        ICollection<DRPGBattleHitResult> hits,
        ICollection<DRPGActionEffectResult> effectResults)
    {
        switch (effect.Kind)
        {
            case DRPGActionEffectKind.Damage:
                ApplyDamage(
                    spec,
                    effect,
                    actor,
                    target,
                    context,
                    applyImpactPosture,
                    hits,
                    effectResults);
                return;
            case DRPGActionEffectKind.Heal:
            {
                float requested = Math.Max(
                    0f,
                    effect.ResolveImpactMagnitude(context) *
                    spec.ActorHealingMultiplier);
                float applied = target.RestoreHp(requested);
                effectResults.Add(Applied(
                    effect.Kind,
                    actor,
                    target,
                    requested,
                    applied));
                return;
            }
            case DRPGActionEffectKind.Shield:
            {
                float requested = Math.Max(
                    0f,
                    effect.ResolveImpactMagnitude(context));
                float applied = target.AddShield(requested);
                effectResults.Add(Applied(
                    effect.Kind,
                    actor,
                    target,
                    requested,
                    applied));
                return;
            }
            case DRPGActionEffectKind.PostureDamage:
            {
                float requested = Math.Max(
                    0f,
                    effect.ResolveImpactMagnitude(context));
                float before = target.CurrentPosture;
                float applied = target.ReducePosture(requested);
                effectResults.Add(new DRPGActionEffectResult(
                    effect.Kind,
                    DRPGActionEffectResultState.Applied,
                    actor.RuntimeId,
                    target.RuntimeId,
                    requested,
                    applied,
                    postureBefore: before,
                    postureAfter: target.CurrentPosture));
                return;
            }
            case DRPGActionEffectKind.PostureRestore:
            {
                float requested = Math.Max(
                    0f,
                    effect.ResolveImpactMagnitude(context));
                float applied = target.RestorePosture(requested);
                effectResults.Add(Applied(
                    effect.Kind,
                    actor,
                    target,
                    requested,
                    applied));
                return;
            }
            case DRPGActionEffectKind.ApplyStatus:
            {
                if (effect.Status == null)
                {
                    effectResults.Add(Skipped(
                        effect.Kind,
                        actor.RuntimeId,
                        target.RuntimeId,
                        "The status effect has no status definition."));
                    return;
                }
                DRPGStatusRuntimeSnapshot applied = target.Statuses.Apply(
                    effect.Status,
                    effect.StatusStacks,
                    effect.StatusDurationOverride,
                    new ParticipantId(actor.RuntimeId));
                effectResults.Add(new DRPGActionEffectResult(
                    effect.Kind,
                    DRPGActionEffectResultState.Applied,
                    actor.RuntimeId,
                    target.RuntimeId,
                    effect.StatusStacks,
                    applied.Stacks,
                    applied.RemainingTurns,
                    applied.StatusId));
                return;
            }
            case DRPGActionEffectKind.FormationShift:
            {
                bool changed = environment.TryApplyFormationShift(
                    target,
                    effect.FormationRowDelta,
                    out DRPGFormationRow before,
                    out DRPGFormationRow after,
                    out string error);
                effectResults.Add(new DRPGActionEffectResult(
                    effect.Kind,
                    changed
                        ? DRPGActionEffectResultState.Applied
                        : DRPGActionEffectResultState.Skipped,
                    actor.RuntimeId,
                    target.RuntimeId,
                    effect.FormationRowDelta,
                    (int)after - (int)before,
                    0f,
                    string.Empty,
                    error,
                    DRPGDamageTag.None,
                    before,
                    after));
                return;
            }
            case DRPGActionEffectKind.TimelineShift:
            {
                bool changed = environment.TryApplyTimelineShift(
                    target,
                    effect.TimelineDelta,
                    effect.TimelinePriorityDelta,
                    out float before,
                    out float after,
                    out string error);
                effectResults.Add(new DRPGActionEffectResult(
                    effect.Kind,
                    changed
                        ? DRPGActionEffectResultState.Applied
                        : DRPGActionEffectResultState.Skipped,
                    actor.RuntimeId,
                    target.RuntimeId,
                    effect.TimelineDelta,
                    after - before,
                    effect.TimelinePriorityDelta,
                    string.Empty,
                    error,
                    DRPGDamageTag.None,
                    null,
                    null,
                    before,
                    after));
                return;
            }
            default:
                effectResults.Add(Skipped(
                    effect.Kind,
                    actor.RuntimeId,
                    target.RuntimeId,
                    "Unsupported action effect kind."));
                return;
        }
    }

    private static void ApplyDamage(
        DRPGResolvedActionSpec spec,
        DRPGResolvedActionEffect effect,
        BattleParticipantRuntime actor,
        BattleParticipantRuntime target,
        DRPGBattleImpactContext context,
        bool applyImpactPosture,
        ICollection<DRPGBattleHitResult> hits,
        ICollection<DRPGActionEffectResult> effectResults)
    {
        float hpBefore = target.CurrentHp;
        float postureBefore = target.CurrentPosture;
        bool wasPostureBreakExposed = target.PostureBreak.IsExposed;
        DRPGLowHpPresentationState lowHpStateBefore = target.LowHpState;
        float baseMagnitude = Math.Max(
            0f,
            effect.ResolveImpactMagnitude(context));
        float impactMultiplier = context.DealsDamage
            ? context.DamageMultiplier
            : 0f;
        float affinityMultiplier =
            target.ResolveDamageResponseMultiplier(effect.DamageTags);
        float statusMultiplier = target.Statuses
            .ResolveActionModifiers()
            .IncomingDamageMultiplier;
        float defenseMultiplier = target.IncomingDefenseMultiplier;
        float requestedDamage = baseMagnitude *
            spec.ActorDamageMultiplier *
            impactMultiplier *
            affinityMultiplier *
            statusMultiplier *
            defenseMultiplier;
        float appliedDamage = target.ApplyDamage(
            requestedDamage,
            out float shieldAbsorbed);
        float appliedPosture = applyImpactPosture
            ? target.ReducePosture(context.PostureDamage)
            : 0f;

        var breakdown = new DRPGDamageBreakdown(
            baseMagnitude,
            spec.ActorDamageMultiplier,
            impactMultiplier,
            affinityMultiplier,
            statusMultiplier,
            defenseMultiplier,
            requestedDamage,
            shieldAbsorbed,
            appliedDamage,
            effect.DamageTags);
        hits.Add(new DRPGBattleHitResult(
            actor.RuntimeId,
            target.RuntimeId,
            spec.SkillId,
            hpBefore,
            appliedDamage,
            target.CurrentHp,
            !target.IsAlive,
            lowHpStateBefore != DRPGLowHpPresentationState.DamageBroken &&
                target.LowHpState == DRPGLowHpPresentationState.DamageBroken,
            target.RosterRole,
            string.Empty,
            postureBefore,
            appliedPosture,
            target.CurrentPosture,
            context.GuardGrade,
            context.StrikeGrade,
            shieldAbsorbed,
            breakdown,
            wasPostureBreakExposed,
            ResolveHitOutcome(context)));
        effectResults.Add(new DRPGActionEffectResult(
            effect.Kind,
            DRPGActionEffectResultState.Applied,
            actor.RuntimeId,
            target.RuntimeId,
            requestedDamage,
            appliedDamage,
            shieldAbsorbed,
            spec.SkillId,
            string.Empty,
            effect.DamageTags));
        if (applyImpactPosture)
        {
            effectResults.Add(new DRPGActionEffectResult(
                DRPGActionEffectKind.PostureDamage,
                DRPGActionEffectResultState.Applied,
                actor.RuntimeId,
                target.RuntimeId,
                context.PostureDamage,
                appliedPosture,
                postureBefore - target.CurrentPosture,
                "impact_posture",
                postureBefore: postureBefore,
                postureAfter: target.CurrentPosture));
        }
    }

    private static IReadOnlyList<BattleParticipantRuntime> ResolveTargets(
        DRPGActionEffectTarget targetRule,
        BattleParticipantRuntime actor,
        BattleParticipantRuntime primaryTarget,
        IReadOnlyList<BattleParticipantRuntime> participants)
    {
        if (targetRule == DRPGActionEffectTarget.Actor)
        {
            return actor.IsAlive
                ? Array.AsReadOnly(new[] { actor })
                : Array.Empty<BattleParticipantRuntime>();
        }
        if (targetRule == DRPGActionEffectTarget.PrimaryTarget)
        {
            return primaryTarget != null && primaryTarget.IsAlive
                ? Array.AsReadOnly(new[] { primaryTarget })
                : Array.Empty<BattleParticipantRuntime>();
        }

        var result = new List<BattleParticipantRuntime>();
        if (participants == null)
            return result;
        bool allies = targetRule == DRPGActionEffectTarget.AllAllies;
        for (int i = 0; i < participants.Count; i++)
        {
            BattleParticipantRuntime candidate = participants[i];
            if (candidate == null || !candidate.IsAlive || !candidate.IsDeployed)
                continue;
            if ((candidate.Side == actor.Side) == allies)
                result.Add(candidate);
        }
        return result.AsReadOnly();
    }

    private static DRPGActionEffectResult Applied(
        DRPGActionEffectKind kind,
        BattleParticipantRuntime actor,
        BattleParticipantRuntime target,
        float requested,
        float applied)
    {
        return new DRPGActionEffectResult(
            kind,
            DRPGActionEffectResultState.Applied,
            actor.RuntimeId,
            target.RuntimeId,
            requested,
            applied);
    }

    private static DRPGActionEffectResult Skipped(
        DRPGActionEffectKind kind,
        int actorRuntimeId,
        int targetRuntimeId,
        string message)
    {
        return new DRPGActionEffectResult(
            kind,
            DRPGActionEffectResultState.Skipped,
            actorRuntimeId,
            targetRuntimeId,
            0f,
            0f,
            0f,
            string.Empty,
            message);
    }

    private sealed class CompatibilityEnvironment : IDRPGBattleEffectEnvironment
    {
        private readonly IReadOnlyList<BattleParticipantRuntime> participants;

        public CompatibilityEnvironment(
            BattleParticipantRuntime actor,
            BattleParticipantRuntime target)
        {
            participants = Array.AsReadOnly(new[] { actor, target });
        }

        public IReadOnlyList<BattleParticipantRuntime> Participants =>
            participants;

        public bool TryApplyFormationShift(
            BattleParticipantRuntime target,
            int rowDelta,
            out DRPGFormationRow before,
            out DRPGFormationRow after,
            out string error)
        {
            before = target.FormationRow;
            after = before;
            error = "Formation effects require a battle session environment.";
            return false;
        }

        public bool TryApplyTimelineShift(
            BattleParticipantRuntime target,
            float timelineDelta,
            int priorityDelta,
            out float before,
            out float after,
            out string error)
        {
            before = 0f;
            after = 0f;
            error = "Timeline effects require a battle session environment.";
            return false;
        }
    }
}
