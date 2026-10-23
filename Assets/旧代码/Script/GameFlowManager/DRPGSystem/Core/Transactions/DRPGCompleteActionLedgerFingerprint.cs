using System;
using System.Collections.Generic;
using System.Text;

internal static partial class DRPGActionResolutionFingerprint
{
    internal static ulong ComputeComplete(DRPGPrimaryImpactKey key, string parentEventId,
        in DRPGBasicAttackFrozenSnapshot frozen, DRPGBattleHitResult primary,
        IReadOnlyList<DRPGActionEffectResult> effects, IReadOnlyList<DRPGReactionLedgerEntry> reactions,
        DRPGBattleMutationJournal journal)
    {
        var builder = new StringBuilder(4096);
        Append(builder, "complete-action-ledger-v1");
        AppendKey(builder, key);
        Append(builder, parentEventId);
        AppendFrozenAction(builder, frozen);
        AppendHit(builder, primary);
        AppendEffects(builder, effects);
        AppendReactions(builder, reactions);
        AppendJournal(builder, journal);
        AppendActionResources(builder, journal.ActionResources);
        Append(builder, journal.ActionCosts != null ? 1 : 0);
        if (journal.ActionCosts != null)
        {
            var costs = journal.ActionCosts;
            Append(builder, costs.ReservationId);
            Append(builder, costs.Identity.BattleGeneration);
            Append(builder, costs.Identity.TurnSerial);
            Append(builder, costs.Identity.ActionId);
            Append(builder, costs.Resources.ActorRuntimeId);
            Append(builder, costs.Resources.SpentMana);
            Append(builder, costs.Resources.SpentPosture);
            // Progress cost is required to be the first exact mutation in the
            // typed resource journal, which is already fully fingerprinted above.
            Append(builder, costs.Progress != null ? 1 : 0);
        }
        return Hash(builder);
    }

    private static void AppendReactions(StringBuilder builder, IReadOnlyList<DRPGReactionLedgerEntry> reactions)
    {
        Append(builder, reactions.Count);
        foreach (var entry in reactions)
        {
            if (entry == null) throw new InvalidOperationException("A reaction ledger entry is missing.");
            Append(builder, entry.Identity.BattleGeneration);
            Append(builder, entry.Identity.TurnSerial);
            Append(builder, entry.Identity.ActionId);
            Append(builder, entry.ParentEventId);
            Append(builder, entry.EventId);
            Append(builder, entry.ReactionId);
            Append(builder, entry.SourceParticipant.Value);
            Append(builder, entry.TargetParticipant.Value);
            Append(builder, entry.SourceId);
            Append(builder, entry.SkillId);
            Append(builder, (int)entry.Category);
            Append(builder, entry.Depth);
            Append(builder, entry.InsertionOrdinal);
            Append(builder, (int)entry.State);
            Append(builder, (int)entry.Disposition);
            Append(builder, entry.Batch != null ? 1 : 0);
            if (entry.Batch != null)
            {
                Append(builder, entry.Batch.Hits.Count);
                foreach (var hit in entry.Batch.Hits) AppendHit(builder, hit);
                AppendEffects(builder, entry.Batch.Effects);
                AppendTimelineChanges(builder, entry.Batch.Effects);
                AppendVitalChanges(builder, entry.Batch.Effects);
            }
        }
    }

    private static void AppendActionResources(StringBuilder builder, DRPGBattleActionResourceJournal journal)
    {
        Append(builder, journal != null ? 1 : 0);
        if (journal == null) return;
        AppendResonance(builder, journal);
        var snapshot = journal.Snapshot;
        Append(builder, snapshot.BindingId);
        Append(builder, snapshot.BattleGeneration);
        Append(builder, snapshot.ProgressRevision);
        Append(builder, snapshot.WeaponUseRevision);
        Append(builder, snapshot.Equipment != null ? snapshot.Equipment.Id : 0L);
        Append(builder, journal.Identity.BattleGeneration);
        Append(builder, journal.Identity.TurnSerial);
        Append(builder, journal.Identity.ActionId);
        Append(builder, snapshot.Progress.Count);
        foreach (var progress in snapshot.Progress)
        {
            Append(builder, progress.OwnerStableId);
            Append(builder, progress.ResourceId);
            Append(builder, (int)progress.Scope);
            Append(builder, progress.Capacity);
            Append(builder, progress.CurrentValue);
            Append(builder, progress.Definition.InitialValue);
            Append(builder, progress.Definition.BaseCapacity);
            Append(builder, progress.Definition.EquipmentCapacityModifier);
        }
        Append(builder, snapshot.Weapons.Count);
        foreach (var weapon in snapshot.Weapons)
        {
            Append(builder, weapon.Participant.Value);
            Append(builder, weapon.WeaponId);
            Append(builder, weapon.CurrentDurability);
            Append(builder, weapon.MaximumDurability);
            Append(builder, weapon.Revision);
            Append(builder, (int)weapon.BreakDisposition);
        }
        Append(builder, journal.ProgressMutations.Count);
        foreach (var mutation in journal.ProgressMutations)
        {
            var request = mutation.Request;
            Append(builder, request.SourceId);
            Append(builder, request.OwnerStableId);
            Append(builder, request.ResourceId);
            Append(builder, request.DeduplicationKey);
            Append(builder, request.Priority);
            Append(builder, (int)request.Kind);
            Append(builder, (int)request.Scope);
            Append(builder, request.Amount);
            Append(builder, request.IncludeReactionDepth ? 1 : 0);
            Append(builder, request.Definition != null ? 1 : 0);
            if (request.Definition != null)
            {
                Append(builder, request.Definition.ResourceId);
                Append(builder, (int)request.Definition.Scope);
                Append(builder, request.Definition.Capacity);
                Append(builder, request.Definition.InitialValue);
                Append(builder, request.Definition.BaseCapacity);
                Append(builder, request.Definition.EquipmentCapacityModifier);
            }
            Append(builder, mutation.WasApplied ? 1 : 0);
            Append(builder, mutation.ValueBefore);
            Append(builder, mutation.ValueAfter);
            Append(builder, mutation.Capacity);
            Append(builder, mutation.AppliedAmount);
            Append(builder, mutation.OverflowAmount);
            Append(builder, mutation.Message);
        }
        Append(builder, journal.WeaponUses.Count);
        foreach (var use in journal.WeaponUses)
        {
            Append(builder, use.WeaponId);
            Append(builder, use.Units);
            Append(builder, use.DurabilityAfter);
        }
        Append(builder, journal.WeaponEvents.Count);
        foreach (var use in journal.WeaponEvents)
        {
            Append(builder, use.WeaponId);
            Append(builder, use.EventId);
            Append(builder, use.Units);
            Append(builder, use.UseKind.HasValue ? (int)use.UseKind.Value : -1);
        }
        Append(builder, journal.WeaponCompletions.Count);
        foreach (var completion in journal.WeaponCompletions)
        {
            var plan = completion.Plan;
            Append(builder, plan.Source.Id);
            Append(builder, plan.Actor.RuntimeId);
            Append(builder, plan.CharacterId);
            Append(builder, plan.WeaponId);
            Append(builder, plan.DefinitionId);
            Append(builder, plan.Units);
            Append(builder, plan.DurabilityAfter);
            Append(builder, plan.RemovesWeapon ? 1 : 0);
            Append(builder, (int)plan.Disposition);
            Append(builder, plan.EquipmentRevisionAfter);
            Append(builder, completion.HpBefore); Append(builder, completion.HpAfter);
            Append(builder, completion.ManaBefore); Append(builder, completion.ManaAfter);
            Append(builder, completion.PostureBefore); Append(builder, completion.PostureAfter);
            Append(builder, plan.ReplacementBuild.BasicAttack != null ? plan.ReplacementBuild.BasicAttack.SkillId : string.Empty);
            Append(builder, plan.ReplacementBuild.EffectiveMaxHp);
            Append(builder, plan.ReplacementBuild.EffectiveMaxMp);
            Append(builder, plan.ReplacementBuild.EffectiveMaxPosture);
        }
        Append(builder, journal.WeaponProgressReconciliations.Count);
        foreach (var fact in journal.WeaponProgressReconciliations)
        {
            Append(builder, fact.Identity.BattleGeneration); Append(builder, fact.Identity.TurnSerial); Append(builder, fact.Identity.ActionId);
            Append(builder, fact.WeaponId);
            AppendCapacitySnapshot(builder, fact.Before); AppendCapacitySnapshot(builder, fact.After);
            var mutation = fact.Mutation;
            Append(builder, mutation.Request.SourceId); Append(builder, mutation.Request.DeduplicationKey);
            Append(builder, mutation.Request.OwnerStableId); Append(builder, mutation.Request.ResourceId);
            Append(builder, (int)mutation.Request.Scope); Append(builder, (int)mutation.Request.Kind);
            Append(builder, mutation.Request.Priority); Append(builder, mutation.Request.Amount);
            Append(builder, mutation.Request.IncludeReactionDepth ? 1 : 0);
            Append(builder, mutation.Request.Definition != null ? 1 : 0);
            if (mutation.Request.Definition != null)
                AppendCapacitySnapshot(builder, new DRPGProgressSnapshot(mutation.Request.OwnerStableId, mutation.Request.Definition, mutation.ValueAfter));
            Append(builder, (int)mutation.State); Append(builder, mutation.Message);
            Append(builder, mutation.ValueBefore); Append(builder, mutation.ValueAfter); Append(builder, mutation.Capacity);
            Append(builder, mutation.AppliedAmount); Append(builder, mutation.OverflowAmount);
        }
    }

    private static void AppendCapacitySnapshot(StringBuilder builder, DRPGProgressSnapshot value)
    {
        Append(builder, value.OwnerStableId); Append(builder, value.ResourceId); Append(builder, (int)value.Scope);
        Append(builder, value.CurrentValue); Append(builder, value.Capacity); Append(builder, value.Definition.InitialValue);
        Append(builder, value.Definition.BaseCapacity); Append(builder, value.Definition.EquipmentCapacityModifier);
    }
}
