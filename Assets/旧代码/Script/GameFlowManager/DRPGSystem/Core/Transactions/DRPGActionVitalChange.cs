using System;
using System.Collections.Generic;
using System.Text;

// Recovery facts preserve the value at their exact place in the effect list.
// They are not hits and cannot revive a defeated participant.
internal sealed class DRPGActionVitalChange
{
    internal DRPGActionVitalChange(ParticipantId target, DRPGActionEffectKind kind, float requested, float before, float after)
    { Target = target; Kind = kind; Requested = requested; Before = before; After = after; }

    internal ParticipantId Target { get; }
    internal DRPGActionEffectKind Kind { get; }
    internal float Requested { get; }
    internal float Before { get; }
    internal float After { get; }
    internal bool WasApplied => Before != After;
    internal static bool Supports(DRPGActionEffectKind kind) => kind == DRPGActionEffectKind.Heal ||
        kind == DRPGActionEffectKind.Shield || kind == DRPGActionEffectKind.PostureRestore;

    internal static DRPGActionVitalChange Stage(ParticipantMutationBuilder target, DRPGActionEffectKind kind, float requested)
    {
        var change = Resolve(target, kind, requested);
        change.Apply(target);
        return change;
    }

    internal void Replay(ParticipantMutationBuilder target)
    {
        var expected = Resolve(target, Kind, Requested);
        if (Target != expected.Target || Before != expected.Before || After != expected.After)
            throw new InvalidOperationException("Recovery effect differs from its ordered before/after facts.");
        Apply(target);
    }

    private static DRPGActionVitalChange Resolve(ParticipantMutationBuilder target, DRPGActionEffectKind kind, float requested)
    {
        if (!Supports(kind) || float.IsNaN(requested) || float.IsInfinity(requested) || requested < 0f)
            throw new InvalidOperationException("Recovery requires a supported kind and a finite nonnegative amount.");
        if (target.HitPoints <= 0f || !target.IsDeployed)
            throw new InvalidOperationException("Recovery cannot target a defeated or undeployed participant.");
        float before = kind == DRPGActionEffectKind.Heal ? target.HitPoints :
            kind == DRPGActionEffectKind.Shield ? target.Shield : target.Posture;
        float maximum = kind == DRPGActionEffectKind.Heal
            ? target.Snapshot.MaximumHp
            : float.MaxValue;
        double sum = (double)before + requested;
        if (sum > float.MaxValue)
            throw new InvalidOperationException(kind + " amount overflow.");
        float after = (float)Math.Min(maximum, sum);
        return new DRPGActionVitalChange(target.Snapshot.Participant, kind, requested, before, after);
    }

    private void Apply(ParticipantMutationBuilder target)
    {
        if (!WasApplied) return;
        if (Kind == DRPGActionEffectKind.Heal) target.SetHitPoints(After);
        else if (Kind == DRPGActionEffectKind.Shield) target.SetShield(After);
        else target.SetPosture(After);
    }
}

internal sealed partial class DRPGBattleActionResolutionLedgerStore
{
    private static void ApplyExpectedVital(DRPGActionEffectResult effect, DRPGBattleMutationWorkspace expected)
    {
        if (effect.StatusChange != null || effect.TimelineChange != null)
            throw new InvalidOperationException("A recovery fact cannot carry another kind of mutation.");
        var target = expected.For(new ParticipantId(effect.TargetRuntimeId));
        var change = effect.VitalChange;
        if (change == null)
        {
            if (effect.State != DRPGActionEffectResultState.Skipped || (target.HitPoints > 0f && target.IsDeployed) ||
                effect.RequestedAmount != 0f || effect.AppliedAmount != 0f || effect.AuxiliaryAmount != 0f)
                throw new InvalidOperationException("A recovery effect is missing its immutable change.");
            return;
        }
        if (change.Target.Value != effect.TargetRuntimeId || change.Kind != effect.Kind ||
            effect.RequestedAmount != change.Requested || effect.AppliedAmount != change.After - change.Before ||
            effect.AuxiliaryAmount != 0f ||
            effect.State != (change.WasApplied ? DRPGActionEffectResultState.Applied : DRPGActionEffectResultState.Skipped) ||
            (change.Kind == DRPGActionEffectKind.PostureRestore &&
                (effect.PostureBefore != change.Before || effect.PostureAfter != change.After)))
            throw new InvalidOperationException("Recovery summaries differ from their exact change.");
        change.Replay(target);
    }
}

internal static partial class DRPGActionResolutionFingerprint
{
    private static void AppendVitalChanges(StringBuilder builder, IReadOnlyList<DRPGActionEffectResult> effects)
    {
        foreach (var effect in effects)
        {
            var change = effect.VitalChange;
            Append(builder, change != null ? 1 : 0);
            if (change == null) continue;
            Append(builder, change.Target.Value); Append(builder, (int)change.Kind);
            Append(builder, change.Requested); Append(builder, change.Before); Append(builder, change.After);
        }
    }
}
