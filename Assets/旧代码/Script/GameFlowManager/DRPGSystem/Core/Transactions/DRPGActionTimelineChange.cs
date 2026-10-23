using System;
using System.Collections.Generic;
using System.Text;

internal enum DRPGTimelineShiftDisposition { Applied, Unscheduled, Ineligible, Unchanged }

// Exact detached facts, including integer priority (the legacy float summary
// cannot represent every priority). No scheduler or live participant is exposed.
internal sealed class DRPGActionTimelineChange
{
    internal DRPGActionTimelineChange(ParticipantId target, float delta, int priorityDelta,
        BattleTimelineRuleSnapshot? before, BattleTimelineRuleSnapshot? after, DRPGTimelineShiftDisposition disposition)
    {
        Target = target; Delta = delta; PriorityDelta = priorityDelta;
        Before = before; After = after; Disposition = disposition;
    }
    internal ParticipantId Target { get; }
    internal float Delta { get; }
    internal int PriorityDelta { get; }
    internal BattleTimelineRuleSnapshot? Before { get; }
    internal BattleTimelineRuleSnapshot? After { get; }
    internal DRPGTimelineShiftDisposition Disposition { get; }
    internal bool WasApplied => Disposition == DRPGTimelineShiftDisposition.Applied;

    internal static DRPGActionTimelineChange Stage(DRPGBattleMutationWorkspace workspace,
        ParticipantId target, float delta, int priorityDelta)
    {
        var change = Resolve(workspace, target, delta, priorityDelta);
        change.Apply(workspace);
        return change;
    }

    internal void Replay(DRPGBattleMutationWorkspace workspace)
    {
        var expected = Resolve(workspace, Target, Delta, PriorityDelta);
        if (Disposition != expected.Disposition || !Same(Before, expected.Before) || !Same(After, expected.After))
            throw new InvalidOperationException("Timeline effect does not match its ordered before/after facts.");
        Apply(workspace);
    }

    private static DRPGActionTimelineChange Resolve(DRPGBattleMutationWorkspace workspace,
        ParticipantId target, float delta, int priorityDelta)
    {
        if (float.IsNaN(delta) || float.IsInfinity(delta))
            throw new InvalidOperationException("Timeline delta must be finite.");
        var participant = workspace.For(target);
        BattleTimelineRuleSnapshot? before = workspace.TryGetScheduledProjection(target, out var captured)
            ? captured : (BattleTimelineRuleSnapshot?)null;
        if (participant.HitPoints <= 0f || !participant.IsDeployed)
            return new DRPGActionTimelineChange(target, delta, priorityDelta, before, before, DRPGTimelineShiftDisposition.Ineligible);
        if (!before.HasValue)
            return new DRPGActionTimelineChange(target, delta, priorityDelta, null, null, DRPGTimelineShiftDisposition.Unscheduled);
        float time = before.Value.ScheduledTime + delta;
        if (float.IsNaN(time) || float.IsInfinity(time))
            throw new InvalidOperationException("Timeline time overflow.");
        int priority;
        try { priority = checked(before.Value.Priority + priorityDelta); }
        catch (OverflowException) { throw new InvalidOperationException("Timeline priority overflow."); }
        var after = new BattleTimelineRuleSnapshot(target, Math.Max(workspace.CurrentLogicalTime, time), priority);
        return new DRPGActionTimelineChange(target, delta, priorityDelta, before, after,
            Same(before, after) ? DRPGTimelineShiftDisposition.Unchanged : DRPGTimelineShiftDisposition.Applied);
    }

    private void Apply(DRPGBattleMutationWorkspace workspace)
    {
        if (WasApplied) workspace.ScheduleAt(Target, After.Value.ScheduledTime, After.Value.Priority);
    }

    private static bool Same(BattleTimelineRuleSnapshot? left, BattleTimelineRuleSnapshot? right) =>
        left.HasValue == right.HasValue && (!left.HasValue ||
            (left.Value.Participant == right.Value.Participant && left.Value.ScheduledTime == right.Value.ScheduledTime &&
             left.Value.Priority == right.Value.Priority));
}

internal sealed partial class DRPGBattleActionResolutionLedgerStore
{
    private static void ApplyExpectedTimeline(DRPGActionEffectResult effect, DRPGBattleMutationWorkspace expected)
    {
        if (effect.StatusChange != null || effect.VitalChange != null)
            throw new InvalidOperationException("A timeline fact cannot carry another kind of mutation.");
        var change = effect.TimelineChange;
        if (change == null)
        {
            var target = expected.For(new ParticipantId(effect.TargetRuntimeId));
            if (effect.State != DRPGActionEffectResultState.Skipped || (target.HitPoints > 0f && target.IsDeployed) ||
                effect.RequestedAmount != 0f || effect.AppliedAmount != 0f || effect.AuxiliaryAmount != 0f ||
                effect.TimelineBefore != 0f || effect.TimelineAfter != 0f)
                throw new InvalidOperationException("A timeline effect is missing its exact immutable change.");
            return;
        }
        float before = change.Before?.ScheduledTime ?? 0f, after = change.After?.ScheduledTime ?? 0f;
        if (change.Target.Value != effect.TargetRuntimeId || effect.RequestedAmount != change.Delta ||
            effect.AppliedAmount != after - before || effect.AuxiliaryAmount != (float)change.PriorityDelta ||
            effect.TimelineBefore != before || effect.TimelineAfter != after ||
            effect.State != (change.WasApplied ? DRPGActionEffectResultState.Applied : DRPGActionEffectResultState.Skipped))
            throw new InvalidOperationException("Timeline effect summaries differ from the frozen change.");
        change.Replay(expected);
    }

    private static void ValidateExpectedTimeline(IReadOnlyList<DRPGTimelineMutationRecord> expected,
        IReadOnlyList<DRPGTimelineMutationRecord> actual)
    {
        if (expected.Count != actual.Count)
            throw new InvalidOperationException("Timeline journal has missing or unaccounted mutations.");
        foreach (var record in actual)
        {
            bool found = false;
            foreach (var wanted in expected)
                if (wanted.Participant == record.Participant && wanted.ScheduledTime == record.ScheduledTime && wanted.Priority == record.Priority)
                { found = true; break; }
            if (!found) throw new InvalidOperationException("Timeline journal differs from the ordered action outcomes.");
        }
    }
}

internal static partial class DRPGActionResolutionFingerprint
{
    private static void AppendTimelineChanges(StringBuilder builder, IReadOnlyList<DRPGActionEffectResult> effects)
    {
        foreach (var effect in effects)
        {
            var change = effect.TimelineChange;
            Append(builder, change != null ? 1 : 0);
            if (change == null) continue;
            Append(builder, change.Target.Value); Append(builder, change.Delta); Append(builder, change.PriorityDelta);
            Append(builder, (int)change.Disposition);
            AppendTimelineValue(builder, change.Before); AppendTimelineValue(builder, change.After);
        }
    }
    private static void AppendTimelineValue(StringBuilder builder, BattleTimelineRuleSnapshot? value)
    {
        Append(builder, value.HasValue ? 1 : 0);
        if (!value.HasValue) return;
        Append(builder, value.Value.Participant.Value); Append(builder, value.Value.ScheduledTime); Append(builder, value.Value.Priority);
    }
}
