using System;
using System.Collections.Generic;

// Builds a whole presentation beat before its first visible write. This is a
// read-only projection of committed typed facts, never a second rule resolver.
internal sealed class BattleOutcomeProjectionBuilder
{
    private readonly DRPGBattleActionProjectionLease lease;
    private readonly Dictionary<int, BattleProjectedParticipant> staged = new Dictionary<int, BattleProjectedParticipant>();
    private readonly List<BattleProjectionCursor> cursors = new List<BattleProjectionCursor>();
    internal BattleOutcomeProjectionBuilder(DRPGBattleActionProjectionLease lease)
    { this.lease = lease ?? throw new ArgumentNullException(nameof(lease)); }
    internal List<BattleProjectionCursor> Cursors => cursors;

    internal void AppendSettlement(DRPGSettlementStatusStep step)
    {
        var before = Read(step.Owner.Value);
        if (before.Hp != step.HpBefore || before.Posture != step.PostureBefore ||
            !DRPGActionStatusChange.SameSnapshots(before.Statuses, step.StatusesBefore))
            throw new InvalidOperationException("Settlement projection disagrees with its committed before-state.");
        Write(before, before.WithSettlement(step.HpAfter, step.PostureAfter, step.StatusesAfter));
    }

    internal void AppendTurnEntryLifecycle(DRPGTurnEntryLifecycle lifecycle)
    {
        if (lifecycle == null || lease.Owner.Kind != BattleTransactionKind.TurnEntry || !lifecycle.CandidateAlive)
            throw new InvalidOperationException("Lifecycle projection requires a living candidate's frozen facts.");
        var before = Read(lifecycle.Actor.Value);
        if (before.DefeatPending || before.Posture != lifecycle.PostureBefore)
            throw new InvalidOperationException("Lifecycle projection disagrees with its post-status before-state.");
        Write(before, before.WithSettlement(before.Hp, lifecycle.PostureAfter, before.Statuses));
    }

    internal void AppendCosts(DRPGActionResourceReceipt costs)
    {
        if (costs == null || costs.SpentPosture == 0f) return;
        var before = Read(costs.ActorRuntimeId);
        Write(before, before.WithSettlement(before.Hp, before.Posture - costs.SpentPosture, before.Statuses));
    }

    internal void AppendWeaponCompletions(IReadOnlyList<DRPGCommittedWeaponFact> weapons)
    {
        foreach (var weapon in weapons)
        {
            if (!weapon.BuildWasReconciled) continue;
            var before = Read(weapon.Participant.Value);
            if (before.Hp != weapon.HpBefore || before.Posture != weapon.PostureBefore)
                throw new InvalidOperationException("Weapon completion disagrees with its committed before-state.");
            Write(before, new BattleProjectedParticipant(before.RuntimeId, weapon.HpAfter, weapon.MaxHpAfter,
                weapon.PostureAfter, weapon.MaxPostureAfter, before.Statuses));
        }
    }

    internal void AppendOutcome(DRPGBattleHitResult hit, IReadOnlyList<DRPGActionEffectResult> effects)
    {
        bool hpApplied = false, postureApplied = false;
        foreach (var effect in effects)
        {
            if (!effect.WasApplied) continue;
            if (effect.Kind == DRPGActionEffectKind.Damage && hit != null && !hpApplied)
            {
                AppendHp(hit.TargetRuntimeId, hit.HpBefore, hit.HpAfter); hpApplied = true;
            }
            else if (effect.StatusChange != null)
            {
                var change = effect.StatusChange; var before = Read(change.Target.Value);
                if (!DRPGActionStatusChange.SameSnapshots(before.Statuses, change.Before) ||
                    !(change.After is DRPGStatusCollectionSnapshot after))
                    throw new InvalidOperationException("Status projection disagrees with the committed effect.");
                Write(before, before.WithSettlement(before.Hp, before.Posture, after));
            }
            else if (effect.PostureChange != null)
            {
                var change = effect.PostureChange;
                AppendPosture(change.Target.Value, change.Before, change.After);
                postureApplied |= change.IsImpact;
            }
            else if (effect.VitalChange != null)
            {
                var change = effect.VitalChange;
                if (change.Kind == DRPGActionEffectKind.Heal) AppendHp(change.Target.Value, change.Before, change.After);
                else if (change.Kind == DRPGActionEffectKind.PostureRestore) AppendPosture(change.Target.Value, change.Before, change.After);
            }
        }
        if (hit == null) return;
        if (!hpApplied) AppendHp(hit.TargetRuntimeId, hit.HpBefore, hit.HpAfter);
        if (!postureApplied) AppendPosture(hit.TargetRuntimeId, hit.PostureBefore, hit.PostureAfter);
    }

    private void AppendHp(int id, float hpBefore, float hpAfter)
    {
        var before = Read(id);
        if (before.Hp != hpBefore) throw new InvalidOperationException("HP projection disagrees with the committed effect.");
        Write(before, before.WithSettlement(hpAfter, before.Posture, before.Statuses));
    }
    private void AppendPosture(int id, float postureBefore, float postureAfter)
    {
        var before = Read(id);
        if (before.Posture != postureBefore) throw new InvalidOperationException("Posture projection disagrees with the committed effect.");
        Write(before, before.WithSettlement(before.Hp, postureAfter, before.Statuses));
    }
    private BattleProjectedParticipant Read(int id)
    {
        if (!lease.Contains(id)) throw new InvalidOperationException("Outcome participant is outside the projection lease.");
        var value = staged.TryGetValue(id, out var prior) ? prior : lease.GetPresented(id);
        if (!value.HasSettlementState) throw new InvalidOperationException("Outcome requires a complete frozen semantic baseline.");
        return value;
    }
    private void Write(BattleProjectedParticipant before, BattleProjectedParticipant after)
    {
        cursors.Add(new BattleProjectionCursor(lease.Owner, lease.NextCursorOrdinal + cursors.Count, before, after));
        staged[before.RuntimeId] = after;
    }
}
