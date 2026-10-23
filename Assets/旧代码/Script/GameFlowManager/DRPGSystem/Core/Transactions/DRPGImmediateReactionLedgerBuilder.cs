using System;
using System.Collections.Generic;
using System.Globalization;

internal enum DRPGImmediateReactionCategory
{
    DefenseOrEvade = 0,
    Counterattack = 1,
    Reflected = 2,
    Pursuit = 3,
    OtherImmediate = 4
}

internal enum DRPGImmediateReactionState
{
    Resolved = 0,
    SkippedBudget = 1,
    SkippedDuplicate = 2,
    SkippedTypeMatrix = 3,
    SkippedIneligible = 4
}

// Sources receive values, never the writable workspace or a Session mutation port.
// Registered implementations must retain resolved immutable rule specifications;
// Collect may select responses to this context, but must not reread live assets.
internal interface IDRPGImmediateReactionSource
{
    string SourceId { get; }
    ParticipantId Participant { get; }
    int HomeSlot { get; }
    bool IsPassive { get; }
    void Collect(DRPGImmediateReactionContext context,
        ICollection<DRPGImmediateReactionRequest> requests);
}

internal sealed class DRPGImmediateReactionContext
{
    internal DRPGImmediateReactionContext(ActionIdentity identity, string eventId,
        int depth, DRPGImmediateReactionCategory? category, DRPGResolvedActionBatch batch,
        ParticipantId sourceParticipant, ParticipantId targetParticipant)
        : this(BattleTransactionIdentity.ForRootAction(identity), eventId, depth, category, batch,
            sourceParticipant, targetParticipant) { }

    internal DRPGImmediateReactionContext(BattleTransactionIdentity owner, string eventId,
        int depth, DRPGImmediateReactionCategory? category, DRPGResolvedActionBatch batch,
        ParticipantId sourceParticipant, ParticipantId targetParticipant)
    {
        Owner = owner;
        EventId = eventId;
        Depth = depth;
        Category = category;
        if (batch == null || batch.Hits.Count > 1)
            throw new ArgumentException("Reaction context requires one hit or a non-hit effect batch.", nameof(batch));
        Hit = batch.Hits.Count == 1 ? batch.Hits[0] : null;
        Effects = batch.Effects;
        SourceParticipant = sourceParticipant;
        TargetParticipant = targetParticipant;
    }

    internal ActionIdentity Identity => Owner.Action;
    internal BattleTransactionIdentity Owner { get; }
    internal string EventId { get; }
    internal int Depth { get; }
    internal DRPGImmediateReactionCategory? Category { get; }
    internal DRPGBattleHitResult Hit { get; }
    internal bool HasHit => Hit != null;
    internal IReadOnlyList<DRPGActionEffectResult> Effects { get; }
    internal ParticipantId SourceParticipant { get; }
    internal ParticipantId TargetParticipant { get; }
    internal DRPGSettlementStatusStep Settlement { get; }
    internal ParticipantId DefeatedParticipant => Settlement != null && Settlement.RuleDefeated ? Settlement.Owner :
        HasHit && Hit.WasDefeated ? new ParticipantId(Hit.TargetRuntimeId) : default;

    internal DRPGImmediateReactionContext(DRPGSettlementStatusStep settlement)
    {
        Settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
        Owner = settlement.Transaction; EventId = settlement.EventId;
        SourceParticipant = settlement.Source; TargetParticipant = settlement.Owner;
        Effects = Array.Empty<DRPGActionEffectResult>();
    }
}

internal sealed class DRPGImmediateReactionRequest
{
    internal DRPGImmediateReactionRequest(ActionIdentity identity, string parentEventId,
        string reactionId, DRPGImmediateReactionCategory category, int priority,
        DRPGResolvedActionSpec spec, DRPGBattleImpactContext impactContext,
        DRPGHitOutcome outcome = DRPGHitOutcome.Hit, bool onDefeat = false)
        : this(identity.IsValid ? BattleTransactionIdentity.ForRootAction(identity) : default, parentEventId,
            reactionId, category, priority, spec, impactContext, outcome, onDefeat) { }

    internal DRPGImmediateReactionRequest(BattleTransactionIdentity owner, string parentEventId,
        string reactionId, DRPGImmediateReactionCategory category, int priority,
        DRPGResolvedActionSpec spec, DRPGBattleImpactContext impactContext,
        DRPGHitOutcome outcome = DRPGHitOutcome.Hit, bool onDefeat = false)
    {
        Owner = owner;
        ParentEventId = parentEventId;
        ReactionId = reactionId;
        Category = category;
        Priority = priority;
        Spec = spec;
        ImpactContext = impactContext;
        Outcome = outcome;
        OnDefeat = onDefeat;
    }

    internal ActionIdentity Identity => Owner.Action;
    internal BattleTransactionIdentity Owner { get; }
    internal string ParentEventId { get; }
    internal string ReactionId { get; }
    internal DRPGImmediateReactionCategory Category { get; }
    internal int Priority { get; }
    internal DRPGResolvedActionSpec Spec { get; }
    internal DRPGBattleImpactContext ImpactContext { get; }
    internal DRPGHitOutcome Outcome { get; }
    internal bool OnDefeat { get; }
}

internal sealed class DRPGReactionLedgerEntry
{
    internal DRPGReactionLedgerEntry(ActionIdentity identity, string parentEventId,
        string eventId, string reactionId, ParticipantId sourceParticipant,
        string sourceId, DRPGImmediateReactionCategory category, int depth,
        long insertionOrdinal, DRPGImmediateReactionState state, DRPGResolvedActionBatch batch,
        ParticipantId targetParticipant = default, string skillId = null)
        : this(BattleTransactionIdentity.ForRootAction(identity), parentEventId, eventId, reactionId, sourceParticipant,
            sourceId, category, depth, insertionOrdinal, state, batch, targetParticipant, skillId) { }

    internal DRPGReactionLedgerEntry(BattleTransactionIdentity owner, string parentEventId,
        string eventId, string reactionId, ParticipantId sourceParticipant,
        string sourceId, DRPGImmediateReactionCategory category, int depth,
        long insertionOrdinal, DRPGImmediateReactionState state, DRPGResolvedActionBatch batch,
        ParticipantId targetParticipant = default, string skillId = null)
    {
        Owner = owner;
        ParentEventId = parentEventId;
        EventId = eventId;
        ReactionId = reactionId;
        SourceParticipant = sourceParticipant;
        SourceId = sourceId;
        Category = category;
        Depth = depth;
        InsertionOrdinal = insertionOrdinal;
        State = state;
        Batch = batch;
        SkillId = skillId ?? (batch != null && batch.Hits.Count == 1 ? batch.Hits[0]?.SkillId : string.Empty) ?? string.Empty;
        TargetParticipant = targetParticipant.Value > 0 ? targetParticipant : batch != null && batch.Hits.Count == 1 && batch.Hits[0] != null
            ? new ParticipantId(batch.Hits[0].TargetRuntimeId) : default;
    }

    internal ActionIdentity Identity => Owner.Action;
    internal BattleTransactionIdentity Owner { get; }
    internal string ParentEventId { get; }
    internal string EventId { get; }
    internal string ReactionId { get; }
    internal ParticipantId SourceParticipant { get; }
    internal ParticipantId TargetParticipant { get; }
    internal string SourceId { get; }
    internal string SkillId { get; }
    internal DRPGImmediateReactionCategory Category { get; }
    internal int Depth { get; }
    internal long InsertionOrdinal { get; }
    internal DRPGImmediateReactionState State { get; }
    internal DRPGResolvedActionBatch Batch { get; }
    internal bool RequiresPresentation
    {
        get
        {
            if (State != DRPGImmediateReactionState.Resolved || Batch == null) return false;
            if (Batch.Hits.Count != 0) return true;
            foreach (var effect in Batch.Effects)
                if (effect != null && effect.State == DRPGActionEffectResultState.Applied) return true;
            return false;
        }
    }
    internal DRPGLedgerDisposition Disposition => State != DRPGImmediateReactionState.Resolved
        ? DRPGLedgerDisposition.SkippedDiagnostic
        : RequiresPresentation ? DRPGLedgerDisposition.Presentable : DRPGLedgerDisposition.RuleOnly;
}

internal sealed class DRPGImmediateReactionClosureResult
{
    internal DRPGImmediateReactionClosureResult(DRPGBattleMutationWorkspace workspace,
        IReadOnlyList<DRPGReactionLedgerEntry> entries, string error,
        DRPGImmediateReactionLedgerBuilder.Continuation continuation = null)
    {
        Workspace = workspace;
        Error = error ?? string.Empty;
        var copy = new DRPGReactionLedgerEntry[entries != null ? entries.Count : 0];
        for (int i = 0; i < copy.Length; i++) copy[i] = entries[i];
        Entries = Array.AsReadOnly(copy);
        Continuation = continuation;
    }

    internal bool Succeeded => Workspace != null && Error.Length == 0;
    internal DRPGBattleMutationWorkspace Workspace { get; }
    internal IReadOnlyList<DRPGReactionLedgerEntry> Entries { get; }
    internal string Error { get; }
    internal DRPGImmediateReactionLedgerBuilder.Continuation Continuation { get; }
}

internal sealed class DRPGImmediateReactionLedgerBuilder
{
    // TurnEntry captures a fresh causal budget directly. It has no preceding primary attack.
    internal static Continuation CaptureTurnEntry(TurnEntryIdentity identity, DRPGBattleMutationWorkspace workspace,
        IReadOnlyList<BattleParticipantRuntime> participants, IEnumerable<IDRPGImmediateReactionSource> sources,
        DRPGActionResolver resolver)
    {
        if (!identity.IsValid || workspace == null || resolver == null ||
            workspace.Snapshot.BattleGeneration != identity.BattleGeneration ||
            workspace.Snapshot.StatusApplicationBoundarySerial != identity.CandidateSerial ||
            !workspace.Snapshot.TryGetParticipant(identity.Actor, out var candidate) ||
            !candidate.IsDeployed || candidate.CurrentHp <= 0f)
            throw new InvalidOperationException("TurnEntry reactions require the matching captured living candidate and boundary.");
        return new Continuation(new Expansion(BattleTransactionIdentity.ForTurnEntry(identity), null,
            workspace, participants, sources, resolver));
    }

    internal DRPGImmediateReactionClosureResult Build(ActionIdentity identity,
        string primaryEventId, DRPGWorkspaceResolutionResult primary,
        DRPGBattleMutationWorkspace workspace,
        IReadOnlyList<BattleParticipantRuntime> participants,
        IEnumerable<IDRPGImmediateReactionSource> sources, DRPGActionResolver resolver)
    {
        try
        {
            if (!identity.IsValid || workspace == null || resolver == null ||
                string.IsNullOrWhiteSpace(primaryEventId) ||
                workspace.Snapshot.BattleGeneration != identity.BattleGeneration ||
                primary.Status != DRPGWorkspaceResolutionStatus.Succeeded ||
                primary.Batch == null || primary.Batch.Hits.Count != 1 ||
                primary.Batch.Hits[0] == null)
                throw new InvalidOperationException("Reaction closure requires one successful primary hit and matching root workspace.");

            var run = new Expansion(identity, primaryEventId, workspace.Fork(),
                participants, sources, resolver);
            run.Expand(new DRPGImmediateReactionContext(identity, primaryEventId,
                0, null, primary.Batch, new ParticipantId(primary.Batch.Hits[0].ActorRuntimeId),
                new ParticipantId(primary.Batch.Hits[0].TargetRuntimeId)));
            run.Workspace.BuildValidatedJournal();
            return new DRPGImmediateReactionClosureResult(run.Workspace, run.Entries, null, new Continuation(run));
        }
        catch (Exception error)
        {
            return new DRPGImmediateReactionClosureResult(null, null, error.Message);
        }
    }

    private sealed class SourceBinding
    {
        internal SourceBinding(IDRPGImmediateReactionSource source)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Id = source.SourceId;
            Participant = source.Participant;
            HomeSlot = source.HomeSlot;
            IsPassive = source.IsPassive;
        }
        internal IDRPGImmediateReactionSource Source { get; }
        internal string Id { get; }
        internal ParticipantId Participant { get; }
        internal int HomeSlot { get; }
        internal bool IsPassive { get; }
    }

    private sealed class PendingReaction
    {
        internal SourceBinding Source;
        internal DRPGImmediateReactionRequest Request;
        internal long InsertionOrdinal;
    }

    // Kept inside the rule layer. Every settlement attempt clones the sealed
    // Stage7 run; a rejected speculative child cannot spend the Root's budget.
    internal sealed class Continuation
    {
        private readonly Expansion captured;
        internal Continuation(Expansion captured) { this.captured = captured; }
        internal Expansion BeginSettlement(ActionIdentity identity, DRPGBattleMutationWorkspace workspace) =>
            BeginSettlement(BattleTransactionIdentity.ForRootAction(identity), workspace);
        internal Expansion BeginSettlement(BattleTransactionIdentity owner, DRPGBattleMutationWorkspace workspace) =>
            new Expansion(captured, owner, workspace);
    }

    internal sealed class Expansion
    {
        private const int MaximumDepth = 8;
        private const int MaximumResolvedSteps = 32;
        private readonly BattleTransactionIdentity owner;
        private readonly DRPGActionResolver resolver;
        private readonly List<BattleParticipantRuntime> participants = new List<BattleParticipantRuntime>();
        private readonly Dictionary<int, BattleParticipantRuntime> participantById = new Dictionary<int, BattleParticipantRuntime>();
        private readonly List<SourceBinding> sources = new List<SourceBinding>();
        private readonly HashSet<(ParticipantId, string)> consumedPassives = new HashSet<(ParticipantId, string)>();
        private readonly HashSet<(ParticipantId, string, string, string)> consumedParents =
            new HashSet<(ParticipantId, string, string, string)>();
        private readonly HashSet<string> eventIds = new HashSet<string>(StringComparer.Ordinal);
        private long nextInsertionOrdinal;
        private long nextEventSerial;
        private int resolvedSteps;
        private readonly bool settlementRun;

        internal Expansion(ActionIdentity identity, string primaryEventId,
            DRPGBattleMutationWorkspace workspace,
            IReadOnlyList<BattleParticipantRuntime> participants,
            IEnumerable<IDRPGImmediateReactionSource> sources, DRPGActionResolver resolver)
            : this(BattleTransactionIdentity.ForRootAction(identity), primaryEventId, workspace, participants, sources, resolver) { }

        internal Expansion(BattleTransactionIdentity owner, string primaryEventId,
            DRPGBattleMutationWorkspace workspace,
            IReadOnlyList<BattleParticipantRuntime> participants,
            IEnumerable<IDRPGImmediateReactionSource> sources, DRPGActionResolver resolver)
        {
            this.owner = owner;
            this.resolver = resolver;
            Workspace = workspace;
            settlementRun = owner.Kind == BattleTransactionKind.TurnEntry;
            if (primaryEventId != null) eventIds.Add(primaryEventId);
            if (participants == null)
                throw new ArgumentNullException(nameof(participants));
            foreach (var participant in participants)
            {
                if (participant == null || !workspace.Snapshot.TryGetParticipant(
                    new ParticipantId(participant.RuntimeId), out _))
                    throw new InvalidOperationException("Reaction participant is not captured in the workspace.");
                participantById.Add(participant.RuntimeId, participant);
                this.participants.Add(participant);
            }
            var unique = new HashSet<(ParticipantId, string)>();
            if (sources != null)
            {
                foreach (var source in sources)
                {
                    var binding = new SourceBinding(source);
                    if (string.IsNullOrWhiteSpace(binding.Id) || binding.HomeSlot < 0 ||
                        !participantById.ContainsKey(binding.Participant.Value) ||
                        !unique.Add((binding.Participant, binding.Id)))
                        throw new InvalidOperationException("Reaction sources require a unique owner/source identity and a captured Home Slot.");
                    this.sources.Add(binding);
                }
            }
            // Stable collection order only breaks otherwise identical keys. It
            // cannot override category, priority, Home Slot or definition order.
            this.sources.Sort((left, right) =>
            {
                int comparison = left.Participant.Value.CompareTo(right.Participant.Value);
                return comparison != 0 ? comparison : StringComparer.Ordinal.Compare(left.Id, right.Id);
            });
        }

        internal DRPGBattleMutationWorkspace Workspace { get; }
        internal List<DRPGReactionLedgerEntry> Entries { get; } = new List<DRPGReactionLedgerEntry>();

        internal Expansion(Expansion captured, BattleTransactionIdentity owner, DRPGBattleMutationWorkspace workspace)
        {
            bool turnEntry = owner.Kind == BattleTransactionKind.TurnEntry;
            if (captured == null || captured.owner != owner || workspace == null ||
                workspace.Snapshot.BattleGeneration != owner.BattleGeneration ||
                workspace.Snapshot.StatusApplicationBoundarySerial != (turnEntry ? owner.TurnEntry.CandidateSerial : owner.Action.TurnSerial) ||
                (turnEntry ? workspace.BaseRevision != captured.Workspace.BaseRevision : workspace.BaseRevision <= captured.Workspace.BaseRevision))
                throw new InvalidOperationException("Settlement continuation requires its exact transaction and captured boundary revision.");
            this.owner = owner; resolver = captured.resolver; Workspace = workspace; settlementRun = true;
            foreach (var participant in captured.participants)
            {
                if (!workspace.Snapshot.TryGetParticipant(new ParticipantId(participant.RuntimeId), out _))
                    throw new InvalidOperationException("A continuation participant is missing from Settlement.");
                participants.Add(participant); participantById.Add(participant.RuntimeId, participant);
            }
            sources.AddRange(captured.sources);
            consumedPassives.UnionWith(captured.consumedPassives); consumedParents.UnionWith(captured.consumedParents);
            eventIds.UnionWith(captured.eventIds);
            nextInsertionOrdinal = captured.nextInsertionOrdinal; nextEventSerial = captured.nextEventSerial;
            resolvedSteps = captured.resolvedSteps;
        }

        internal IReadOnlyList<DRPGReactionLedgerEntry> ExpandSettlement(DRPGSettlementStatusStep step)
        {
            if (step == null || step.Transaction != owner || !eventIds.Add(step.EventId))
                throw new InvalidOperationException("Settlement reaction root identity is invalid or duplicated.");
            int start = Entries.Count;
            Expand(new DRPGImmediateReactionContext(step));
            return Array.AsReadOnly(Entries.GetRange(start, Entries.Count - start).ToArray());
        }

        internal void Expand(DRPGImmediateReactionContext context)
        {
            var pending = new List<PendingReaction>();
            foreach (var source in sources)
            {
                var requests = new List<DRPGImmediateReactionRequest>();
                source.Source.Collect(context, requests);
                foreach (var request in requests)
                {
                    ValidateRequest(context, request);
                    if (settlementRun && request.Spec.ActorRuntimeId != source.Participant.Value)
                        throw new InvalidOperationException("Settlement reaction actor must belong to its frozen source.");
                    pending.Add(new PendingReaction {
                        Source = source, Request = request,
                        InsertionOrdinal = checked(++nextInsertionOrdinal)
                    });
                }
            }
            pending.Sort(Compare);
            foreach (var candidate in pending)
                Resolve(context, candidate);
        }

        private void ValidateRequest(DRPGImmediateReactionContext parent, DRPGImmediateReactionRequest request)
        {
            if (request == null || request.Owner != owner ||
                !StringComparer.Ordinal.Equals(request.ParentEventId, parent.EventId) ||
                string.IsNullOrWhiteSpace(request.ReactionId) || request.Spec == null ||
                !Enum.IsDefined(typeof(DRPGImmediateReactionCategory), request.Category) ||
                !Enum.IsDefined(typeof(DRPGHitOutcome), request.Outcome) ||
                !participantById.ContainsKey(request.Spec.ActorRuntimeId) ||
                !participantById.ContainsKey(request.Spec.TargetRuntimeId))
                throw new InvalidOperationException("Reaction request does not belong to its root, parent event or captured participants.");
        }

        private static int Compare(PendingReaction left, PendingReaction right)
        {
            int comparison = left.Request.Category.CompareTo(right.Request.Category);
            if (comparison == 0) comparison = right.Request.Priority.CompareTo(left.Request.Priority);
            if (comparison == 0) comparison = left.Source.HomeSlot.CompareTo(right.Source.HomeSlot);
            if (comparison == 0) comparison = left.Source.Participant.Value.CompareTo(right.Source.Participant.Value);
            if (comparison == 0) comparison = StringComparer.Ordinal.Compare(left.Request.ReactionId, right.Request.ReactionId);
            return comparison != 0 ? comparison : left.InsertionOrdinal.CompareTo(right.InsertionOrdinal);
        }

        private void Resolve(DRPGImmediateReactionContext parent, PendingReaction candidate)
        {
            var request = candidate.Request;
            var source = candidate.Source;
            int depth = parent.Depth + 1;
            var passiveKey = (source.Participant, source.Id);
            var parentKey = (source.Participant, source.Id, parent.EventId, request.ReactionId);
            DRPGImmediateReactionState state;
            if (depth > MaximumDepth || resolvedSteps >= MaximumResolvedSteps)
                state = DRPGImmediateReactionState.SkippedBudget;
            else if ((parent.Category == DRPGImmediateReactionCategory.Counterattack &&
                    request.Category == DRPGImmediateReactionCategory.Counterattack) ||
                (parent.Category == DRPGImmediateReactionCategory.Reflected &&
                    request.Category == DRPGImmediateReactionCategory.Reflected))
                state = DRPGImmediateReactionState.SkippedTypeMatrix;
            else if ((source.IsPassive && consumedPassives.Contains(passiveKey)) || consumedParents.Contains(parentKey))
                state = DRPGImmediateReactionState.SkippedDuplicate;
            else if (!IsEligible(parent, candidate))
                state = DRPGImmediateReactionState.SkippedIneligible;
            else
                state = DRPGImmediateReactionState.Resolved;

            DRPGResolvedActionBatch batch = null;
            if (state == DRPGImmediateReactionState.Resolved)
            {
                var result = resolver.ResolveIntoWorkspace(request.Spec,
                    participantById[request.Spec.ActorRuntimeId], participantById[request.Spec.TargetRuntimeId],
                    participants, Workspace, request.ImpactContext, request.Outcome);
                if (result.Status != DRPGWorkspaceResolutionStatus.Succeeded ||
                    result.Batch == null || result.Batch.Hits.Count > 1 ||
                    (result.Batch.Hits.Count == 0 && result.Batch.Effects.Count == 0))
                    throw new InvalidOperationException("Reaction resolution failed: " + result.Error);
                batch = result.Batch;
                resolvedSteps++;
                consumedParents.Add(parentKey);
                if (source.IsPassive) consumedPassives.Add(passiveKey);
            }

            string eventId;
            do
            {
                long serial = checked(++nextEventSerial);
                eventId = owner.Kind == BattleTransactionKind.TurnEntry
                    ? string.Format(CultureInfo.InvariantCulture, "turn-entry-reaction:{0}:{1}:{2}:{3}",
                        owner.BattleGeneration, owner.TurnEntry.CandidateSerial, owner.TurnEntry.Actor.Value, serial)
                    : string.Format(CultureInfo.InvariantCulture, "reaction:{0}:{1}:{2}:{3}",
                        owner.BattleGeneration, owner.Action.ActionId, owner.Action.TurnSerial, serial);
            }
            while (!eventIds.Add(eventId));
            var entry = new DRPGReactionLedgerEntry(owner, parent.EventId, eventId,
                request.ReactionId, source.Participant, source.Id, request.Category, depth,
                candidate.InsertionOrdinal, state, batch, new ParticipantId(request.Spec.TargetRuntimeId), request.Spec.SkillId);
            Entries.Add(entry);
            if (entry.RequiresPresentation)
            {
                // A child outcome and its immediate consequences finish before
                // the next sibling is rechecked against the updated workspace.
                Expand(new DRPGImmediateReactionContext(owner, eventId, depth, request.Category, batch,
                    new ParticipantId(request.Spec.ActorRuntimeId), new ParticipantId(request.Spec.TargetRuntimeId)));
            }
        }

        private bool IsEligible(DRPGImmediateReactionContext parent, PendingReaction candidate)
        {
            var request = candidate.Request;
            var source = Workspace.For(candidate.Source.Participant);
            var actor = Workspace.For(new ParticipantId(request.Spec.ActorRuntimeId));
            var target = Workspace.For(new ParticipantId(request.Spec.TargetRuntimeId));
            bool ownDefeat = request.OnDefeat && parent.DefeatedParticipant == candidate.Source.Participant &&
                source.HitPoints <= 0f &&
                request.Spec.ActorRuntimeId == candidate.Source.Participant.Value;
            return source.IsDeployed && actor.IsDeployed && target.IsDeployed &&
                target.HitPoints > 0f &&
                (request.OnDefeat ? ownDefeat : source.HitPoints > 0f && actor.HitPoints > 0f);
        }
    }
}
