using System;
using System.Collections.Generic;

internal sealed class DRPGActionRuleSources
{
    private readonly IReadOnlyList<IDRPGBattleProgressSource> sources;
    internal DRPGActionRuleSources(DRPGBattleProgressPipeline progress)
    { sources = (progress ?? throw new ArgumentNullException(nameof(progress))).CaptureActionSources(); }

    internal DRPGImmediateReactionClosureResult Stage(ActionIdentity identity, DRPGWorkspaceResolutionResult primary,
        DRPGImmediateReactionClosureResult closure)
    {
        try
        {
            if (!identity.IsValid || closure == null || !closure.Succeeded || primary.Status != DRPGWorkspaceResolutionStatus.Succeeded ||
                closure.Workspace.Snapshot.BattleGeneration != identity.BattleGeneration || primary.Batch == null || primary.Batch.Hits.Count != 1)
                throw new InvalidOperationException("Event rules require a complete, matching detached action closure.");
            var workspace = closure.Workspace.Fork();
            if (workspace.ActionResources == null) throw new InvalidOperationException("Event rules require captured action resources.");
            var batches = DRPGActionLedgerEventProjection.Build(identity, primary.Batch, closure.Entries);
            var collected = new List<Collected>();
            int insertion = 0;
            foreach (var batch in batches)
            {
                var context = new DRPGBattleProgressContext(batch, workspace);
                foreach (var source in sources)
                {
                    var requests = new List<DRPGProgressMutationRequest>(); source.Collect(context, requests);
                    foreach (var request in requests)
                    {
                        if (request == null || request.SourceId != source.SourceId || !context.IsKnownOwner(request.OwnerStableId))
                            throw new InvalidOperationException("An event source returned a null or foreign progress request.");
                        if (batch.Depth == 0 || request.IncludeReactionDepth) collected.Add(new Collected(request, batch.Depth, insertion++));
                    }
                }
            }
            collected.Sort((a, b) =>
            {
                int order = b.Request.Priority.CompareTo(a.Request.Priority);
                if (order == 0) order = StringComparer.Ordinal.Compare(a.Request.SourceId, b.Request.SourceId);
                if (order == 0) order = StringComparer.Ordinal.Compare(a.Request.ResourceId, b.Request.ResourceId);
                if (order == 0) order = StringComparer.Ordinal.Compare(a.Request.DeduplicationKey, b.Request.DeduplicationKey);
                if (order == 0) order = a.Depth.CompareTo(b.Depth);
                return order == 0 ? a.Insertion.CompareTo(b.Insertion) : order;
            });
            foreach (var item in collected)
            {
                if (!workspace.ActionResources.TryGetProgress(item.Request.OwnerStableId, item.Request.ResourceId, out _))
                    throw new InvalidOperationException("Event progress resource is unregistered: " + item.Request.ResourceId);
                if (!workspace.ActionResources.TryStageProgress(identity, item.Request, out _, out string error))
                    throw new InvalidOperationException(error);
            }
            workspace.ActionResources.ResonanceRules?.Stage(identity, batches, workspace.ActionResources);
            return new DRPGImmediateReactionClosureResult(workspace, closure.Entries, null);
        }
        catch (Exception error) { return new DRPGImmediateReactionClosureResult(null, null, error.Message); }
    }

    private sealed class Collected
    {
        internal Collected(DRPGProgressMutationRequest request, int depth, int insertion)
        { Request = request; Depth = depth; Insertion = insertion; }
        internal readonly DRPGProgressMutationRequest Request;
        internal readonly int Depth, Insertion;
    }
}

// Event IDs are local to the complete ActionIdentity, never broadcast through
// the legacy live-mutation pipeline. One factory gives stable IDs within a draft.
internal static class DRPGActionLedgerEventProjection
{
    internal static IReadOnlyList<DRPGBattleEventBatch> Build(ActionIdentity identity, DRPGResolvedActionBatch primary,
        IReadOnlyList<DRPGReactionLedgerEntry> reactions)
    {
        var factory = new DRPGBattleEventFactory(); var hit = primary.Hits[0];
        var batches = new List<DRPGBattleEventBatch> { factory.CreateBatch(identity.ActionId, 0, hit.ActorRuntimeId,
            hit.TargetRuntimeId, hit.SkillId, primary.Hits, primary.Effects) };
        foreach (var entry in reactions)
        {
            if (entry.State != DRPGImmediateReactionState.Resolved || !entry.RequiresPresentation) continue;
            if (entry.Identity != identity || string.IsNullOrWhiteSpace(entry.SkillId))
                throw new InvalidOperationException("Resolved reaction event is missing its frozen skill or Root identity.");
            int actor = entry.Batch.Hits.Count == 1 ? entry.Batch.Hits[0].ActorRuntimeId : entry.Batch.Effects[0].ActorRuntimeId;
            batches.Add(factory.CreateBatch(identity.ActionId, entry.Depth, actor, entry.TargetParticipant.Value,
                entry.SkillId, entry.Batch.Hits, entry.Batch.Effects));
        }
        return Array.AsReadOnly(batches.ToArray());
    }
}
