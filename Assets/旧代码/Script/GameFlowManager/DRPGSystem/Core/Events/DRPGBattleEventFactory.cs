using System;
using System.Collections.Generic;

public sealed class DRPGBattleEventFactory
{
    private long nextEventId = 1L;

    public DRPGBattleEventBatch CreateRoot(
        DRPGBattleActionResolution resolution)
    {
        if (resolution == null)
            throw new ArgumentNullException(nameof(resolution));

        DRPGBattlePreparedAction action = resolution.PreparedAction;
        return CreateBatch(
            action.ActionId,
            0,
            action.ActorRuntimeId,
            action.TargetRuntimeId,
            action.Spec.SkillId,
            resolution.Hits,
            resolution.Effects);
    }

    public DRPGBattleEventBatch CreateChild(
        long rootActionId,
        int depth,
        int actorRuntimeId,
        int primaryTargetRuntimeId,
        string skillId,
        DRPGResolvedActionBatch resolution)
    {
        if (resolution == null)
            throw new ArgumentNullException(nameof(resolution));
        if (depth <= 0)
            throw new ArgumentOutOfRangeException(nameof(depth));

        return CreateBatch(
            rootActionId,
            depth,
            actorRuntimeId,
            primaryTargetRuntimeId,
            skillId,
            resolution.Hits,
            resolution.Effects);
    }

    public DRPGBattleEvent CreateLifecycleEvent(
        DRPGBattleEventKind kind,
        int actorRuntimeId,
        string semanticId = "")
    {
        if (kind != DRPGBattleEventKind.PostureBreakTurnSkipped &&
            kind != DRPGBattleEventKind.PostureBreakRecovered &&
            kind != DRPGBattleEventKind.PostureBreakEntered &&
            kind != DRPGBattleEventKind.PreparedActionInterrupted)
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        return new DRPGBattleEvent(
            NextEventId(),
            0L,
            0,
            kind,
            actorRuntimeId,
            actorRuntimeId,
            string.Empty,
            semanticId: semanticId);
    }

    internal DRPGBattleEventBatch CreateBatch(
        long rootActionId,
        int depth,
        int actorRuntimeId,
        int primaryTargetRuntimeId,
        string skillId,
        IReadOnlyList<DRPGBattleHitResult> hits,
        IReadOnlyList<DRPGActionEffectResult> effects)
    {
        if (rootActionId <= 0)
            throw new ArgumentOutOfRangeException(nameof(rootActionId));
        if (depth < 0)
            throw new ArgumentOutOfRangeException(nameof(depth));
        if (actorRuntimeId <= 0)
            throw new ArgumentOutOfRangeException(nameof(actorRuntimeId));

        var events = new List<DRPGBattleEvent>();
        if (effects != null)
        {
            for (int i = 0; i < effects.Count; i++)
            {
                DRPGActionEffectResult effect = effects[i];
                if (effect == null || !effect.WasApplied)
                    continue;

                events.Add(new DRPGBattleEvent(
                    NextEventId(),
                    rootActionId,
                    depth,
                    DRPGBattleEventKind.EffectApplied,
                    actorRuntimeId,
                    effect.TargetRuntimeId,
                    skillId,
                    effect.Kind,
                    effect.RequestedAmount,
                    effect.AppliedAmount,
                    effect.AuxiliaryAmount,
                    effect.DamageTags,
                    effect.SemanticId));
                if (effect.CausedPostureBreak)
                {
                    events.Add(new DRPGBattleEvent(
                        NextEventId(),
                        rootActionId,
                        depth,
                        DRPGBattleEventKind.PostureBreakEntered,
                        actorRuntimeId,
                        effect.TargetRuntimeId,
                        skillId,
                        DRPGActionEffectKind.PostureDamage,
                        effect.RequestedAmount,
                        effect.AppliedAmount,
                        effect.AuxiliaryAmount,
                        effect.DamageTags,
                        effect.SemanticId));
                }
            }
        }

        if (hits != null)
        {
            for (int i = 0; i < hits.Count; i++)
            {
                DRPGBattleHitResult hit = hits[i];
                if (hit == null)
                    continue;

                float requested = hit.DamageBreakdown != null
                    ? hit.DamageBreakdown.RequestedDamage
                    : hit.AppliedDamage + hit.ShieldAbsorbed;
                if (requested > 0f || hit.AppliedDamage > 0f ||
                    hit.ShieldAbsorbed > 0f)
                {
                    events.Add(new DRPGBattleEvent(
                        NextEventId(),
                        rootActionId,
                        depth,
                        DRPGBattleEventKind.DamageApplied,
                        hit.ActorRuntimeId,
                        hit.TargetRuntimeId,
                        hit.SkillId,
                        DRPGActionEffectKind.Damage,
                        requested,
                        hit.AppliedDamage,
                        hit.ShieldAbsorbed,
                        hit.DamageBreakdown != null
                            ? hit.DamageBreakdown.Tags
                            : DRPGDamageTag.None));
                }
                if (hit.WasDefeated)
                {
                    events.Add(new DRPGBattleEvent(
                        NextEventId(),
                        rootActionId,
                        depth,
                        DRPGBattleEventKind.ParticipantDefeated,
                        hit.ActorRuntimeId,
                        hit.TargetRuntimeId,
                        hit.SkillId,
                        DRPGActionEffectKind.Damage,
                        requested,
                        hit.AppliedDamage,
                        hit.ShieldAbsorbed));
                }
            }
        }

        events.Add(new DRPGBattleEvent(
            NextEventId(),
            rootActionId,
            depth,
            DRPGBattleEventKind.ActionImpactResolved,
            actorRuntimeId,
            primaryTargetRuntimeId,
            skillId));
        return new DRPGBattleEventBatch(rootActionId, depth, events);
    }

    private long NextEventId()
    {
        return nextEventId++;
    }
}
