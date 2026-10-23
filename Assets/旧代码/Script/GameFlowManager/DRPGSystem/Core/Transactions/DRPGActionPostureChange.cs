using System;
using System.Text;

internal sealed class DRPGActionPostureChange
{
    internal DRPGActionPostureChange(ParticipantId target, float requested, float before, float after,
        DRPGPostureRuleSnapshot beforeState, DRPGPostureRuleSnapshot afterState, bool beforeDefending, bool afterDefending, bool isImpact)
    {
        Target = target; Requested = requested; Before = before; After = after; BeforeState = beforeState; AfterState = afterState;
        BeforeDefending = beforeDefending; AfterDefending = afterDefending; IsImpact = isImpact;
    }
    internal ParticipantId Target { get; }
    internal float Requested { get; }
    internal float Before { get; }
    internal float After { get; }
    internal DRPGPostureRuleSnapshot BeforeState { get; }
    internal DRPGPostureRuleSnapshot AfterState { get; }
    internal bool BeforeDefending { get; }
    internal bool AfterDefending { get; }
    internal bool IsImpact { get; }
    internal bool WasApplied => Before != After;

    internal static DRPGActionPostureChange Stage(ParticipantMutationBuilder target, float requested, bool isImpact = false)
    {
        if (float.IsNaN(requested) || float.IsInfinity(requested) || requested < 0f)
            throw new InvalidOperationException("Posture damage must be finite and nonnegative.");
        var beforeState = target.PostureState; float before = target.Posture; bool defending = target.IsDefending;
        float amount = requested * beforeState.PostureMultiplier;
        if (float.IsInfinity(amount) || float.IsNaN(amount)) throw new InvalidOperationException("Posture damage overflow.");
        float after = Math.Max(0f, before - amount);
        // Impact posture remains an explicit hit component even if zero. A
        // standalone no-op has no mutation and does not seed another reaction.
        if (isImpact || before != after) target.SetPosture(after);
        return new DRPGActionPostureChange(target.Snapshot.Participant, requested, before, after, beforeState,
            target.PostureState, defending, target.IsDefending, isImpact);
    }
    internal void Replay(ParticipantMutationBuilder target)
    {
        if (Target != target.Snapshot.Participant || Before != target.Posture || !BeforeState.Same(target.PostureState) || BeforeDefending != target.IsDefending)
            throw new InvalidOperationException("Posture change before state breaks its causal order.");
        var expected = Stage(target, Requested, IsImpact);
        if (After != expected.After || !AfterState.Same(expected.AfterState) || AfterDefending != expected.AfterDefending)
            throw new InvalidOperationException("Posture change after state differs from its immutable request.");
    }
}

internal sealed partial class DRPGBattleActionResolutionLedgerStore
{
    private static void ApplyExpectedPosture(DRPGActionEffectResult effect, DRPGBattleMutationWorkspace expected, bool impact = false)
    {
        if (effect.StatusChange != null || effect.TimelineChange != null || effect.VitalChange != null)
            throw new InvalidOperationException("Posture change cannot carry a foreign mutation.");
        var target = expected.For(new ParticipantId(effect.TargetRuntimeId)); var change = effect.PostureChange;
        if (change == null)
        {
            if (impact || effect.State != DRPGActionEffectResultState.Skipped || (target.HitPoints > 0f && target.IsDeployed) ||
                effect.RequestedAmount != 0f || effect.AppliedAmount != 0f || effect.AuxiliaryAmount != 0f)
                throw new InvalidOperationException("Posture loss requires its typed lifecycle change.");
            return;
        }
        if ((!impact && (target.HitPoints <= 0f || !target.IsDeployed)) || change.IsImpact != impact ||
            change.Target.Value != effect.TargetRuntimeId || change.Requested != effect.RequestedAmount ||
            change.Before != effect.PostureBefore || change.After != effect.PostureAfter ||
            change.Before - change.After != effect.AppliedAmount || effect.AuxiliaryAmount != (impact ? effect.AppliedAmount : 0f) ||
            effect.State != (impact || change.WasApplied ? DRPGActionEffectResultState.Applied : DRPGActionEffectResultState.Skipped))
            throw new InvalidOperationException("Posture summary differs from its exact lifecycle change.");
        change.Replay(target);
    }
}

internal static partial class DRPGActionResolutionFingerprint
{
    private static void AppendPostureChange(StringBuilder b, DRPGActionPostureChange c)
    {
        Append(b, c != null ? 1 : 0); if (c == null) return;
        Append(b, c.Target.Value); Append(b, c.Requested); Append(b, c.Before); Append(b, c.After);
        AppendPostureState(b, c.BeforeState); AppendPostureState(b, c.AfterState);
        Append(b, c.BeforeDefending ? 1 : 0); Append(b, c.AfterDefending ? 1 : 0); Append(b, c.IsImpact ? 1 : 0);
    }
}
