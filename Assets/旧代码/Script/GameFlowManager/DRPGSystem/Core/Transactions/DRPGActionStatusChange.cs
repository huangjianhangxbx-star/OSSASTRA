using System;
using System.Collections.Generic;
using System.Text;

// One ordered effect's immutable status request and complete before/after projection.
internal sealed class DRPGActionStatusChange
{
    internal DRPGActionStatusChange(ParticipantId target, DRPGStatusMutation mutation,
        IReadOnlyList<DRPGStatusRuntimeSnapshot> before, IReadOnlyList<DRPGStatusRuntimeSnapshot> after)
    {
        Target = target; Mutation = mutation;
        Before = Copy(before); After = Copy(after);
    }
    internal ParticipantId Target { get; }
    internal DRPGStatusMutation Mutation { get; }
    internal IReadOnlyList<DRPGStatusRuntimeSnapshot> Before { get; }
    internal IReadOnlyList<DRPGStatusRuntimeSnapshot> After { get; }

    internal static DRPGActionStatusChange Stage(ParticipantMutationBuilder target, DRPGResolvedActionEffect effect, ParticipantId source)
    {
        var before = target.CreateStatusProjection().Snapshot();
        var mutation = new DRPGStatusMutation(DRPGStatusMutationKind.Apply, effect.Status,
            effect.Status != null ? effect.Status.StatusId : string.Empty, effect.StatusStacks, effect.StatusDurationOverride,
            source, target.StatusApplicationBoundarySerial);
        var draft = DRPGStatusRuntimeCollection.FromSnapshot(before);
        draft.ApplyDetachedMutations(new[] { mutation });
        var after = draft.Snapshot();
        target.ApplyStatus(mutation.Spec, mutation.Stacks, mutation.Duration, mutation.SourceParticipantId);
        return new DRPGActionStatusChange(target.Snapshot.Participant, mutation, before, after);
    }

    internal void Replay(ParticipantMutationBuilder target)
    {
        if (target.Snapshot.Participant != Target || target.HitPoints <= 0f || !target.IsDeployed ||
            Mutation.Kind != DRPGStatusMutationKind.Apply || Mutation.Spec == null || Mutation.StatusId != Mutation.Spec.StatusId ||
            Mutation.AppliedBoundarySerial != target.StatusApplicationBoundarySerial ||
            !SameSnapshots(Before, target.CreateStatusProjection().Snapshot()))
            throw new InvalidOperationException("Status effect identity or before projection breaks its causal chain.");
        target.ApplyStatus(Mutation.Spec, Mutation.Stacks, Mutation.Duration, Mutation.SourceParticipantId);
        if (!SameSnapshots(After, target.CreateStatusProjection().Snapshot()))
            throw new InvalidOperationException("Status effect after projection does not match its frozen request.");
    }

    internal DRPGStatusRuntimeSnapshot AppliedStatus
    {
        get
        {
            // Legacy/untrusted DTOs may omit source metadata. Reading them is
            // permitted; Replay + the complete-ledger gate reject unresolved
            // sources and compare every identity field before any live write.
            foreach (var status in After)
                if (status.StatusId == Mutation.StatusId &&
                    (status.SourceParticipantId == Mutation.SourceParticipantId || Mutation.SourceParticipantId.Value == 0 ||
                     status.SourceParticipantId.Value == 0)) return status;
            throw new InvalidOperationException("Applied status is missing from the after projection.");
        }
    }

    internal static bool SameMutations(IReadOnlyList<DRPGStatusMutation> left, IReadOnlyList<DRPGStatusMutation> right)
    {
        if (left.Count != right.Count) return false;
        for (int i = 0; i < left.Count; i++)
        {
            var a = left[i]; var b = right[i];
            if (a.Kind != b.Kind || a.StatusId != b.StatusId || a.Stacks != b.Stacks || a.Duration != b.Duration ||
                a.SourceParticipantId != b.SourceParticipantId ||
                a.AppliedBoundarySerial != b.AppliedBoundarySerial ||
                (a.Spec == null ? b.Spec != null : !a.Spec.HasSameValues(b.Spec))) return false;
            if (a.Kind == DRPGStatusMutationKind.ConsumeBoundary &&
                (a.Boundary.Timing != b.Boundary.Timing || a.Boundary.Serial != b.Boundary.Serial ||
                 a.Boundary.Actor != b.Boundary.Actor || !a.BoundaryBefore.Same(b.BoundaryBefore))) return false;
        }
        return true;
    }

    internal static bool SameSnapshots(IReadOnlyList<DRPGStatusRuntimeSnapshot> left, IReadOnlyList<DRPGStatusRuntimeSnapshot> right)
    {
        if (left == null || right == null || left.Count != right.Count) return false;
        var a = left as DRPGStatusCollectionSnapshot; var b = right as DRPGStatusCollectionSnapshot;
        if ((a == null) != (b == null) || (a != null &&
            (a.OwnerParticipantId != b.OwnerParticipantId || a.NextInsertionOrdinal != b.NextInsertionOrdinal ||
             a.ObservedBoundarySerial != b.ObservedBoundarySerial))) return false;
        for (int i = 0; i < left.Count; i++)
            if (!left[i].Same(right[i])) return false;
        return true;
    }

    private static IReadOnlyList<DRPGStatusRuntimeSnapshot> Copy(IReadOnlyList<DRPGStatusRuntimeSnapshot> source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        var result = new DRPGStatusRuntimeSnapshot[source.Count];
        for (int i = 0; i < result.Length; i++) result[i] = source[i];
        return source is DRPGStatusCollectionSnapshot complete
            ? new DRPGStatusCollectionSnapshot(complete.OwnerParticipantId, complete.NextInsertionOrdinal, result, complete.ObservedBoundarySerial)
            : Array.AsReadOnly(result);
    }
}

internal static partial class DRPGActionResolutionFingerprint
{
    private static void AppendStatusChange(StringBuilder builder, DRPGActionStatusChange change)
    {
        Append(builder, change != null ? 1 : 0);
        if (change == null) return;
        Append(builder, change.Target.Value);
        Append(builder, (int)change.Mutation.Kind);
        Append(builder, change.Mutation.StatusId);
        Append(builder, change.Mutation.Stacks);
        Append(builder, change.Mutation.Duration);
        Append(builder, change.Mutation.SourceParticipantId.Value);
        Append(builder, change.Mutation.AppliedBoundarySerial);
        AppendResolvedStatus(builder, change.Mutation.Spec);
        AppendBoundaryMutation(builder, change.Mutation);
        AppendStatusSnapshots(builder, change.Before);
        AppendStatusSnapshots(builder, change.After);
    }

    private static void AppendBoundaryMutation(StringBuilder builder, DRPGStatusMutation mutation)
    {
        if (mutation.Kind != DRPGStatusMutationKind.ConsumeBoundary) return;
        Append(builder, (int)mutation.Boundary.Timing);
        Append(builder, mutation.Boundary.Serial);
        Append(builder, mutation.Boundary.Actor.Value);
        AppendStatusSnapshots(builder, new[] { mutation.BoundaryBefore });
    }

    private static void AppendStatusSnapshots(StringBuilder builder, IReadOnlyList<DRPGStatusRuntimeSnapshot> statuses)
    {
        var complete = statuses as DRPGStatusCollectionSnapshot;
        Append(builder, complete != null ? 1 : 0);
        if (complete != null)
        { Append(builder, complete.OwnerParticipantId.Value); Append(builder, complete.NextInsertionOrdinal); Append(builder, complete.ObservedBoundarySerial); }
        Append(builder, statuses.Count);
        foreach (var status in statuses)
        {
            AppendResolvedStatus(builder, status.Spec);
            Append(builder, status.Stacks);
            Append(builder, status.RemainingTurns);
            Append(builder, status.StatusInstanceId);
            Append(builder, status.OwnerParticipantId.Value);
            Append(builder, status.SourceParticipantId.Value);
            Append(builder, status.StableInsertionOrdinal);
            Append(builder, status.AppliedBoundarySerial);
            Append(builder, status.EligibleFromBoundarySerial);
            Append(builder, status.LastConsumedBoundarySerial);
        }
    }
}
