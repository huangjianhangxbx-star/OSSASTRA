using System;
using System.Collections.Generic;

[Flags]
internal enum DRPGBattleMutationDirtySet
{
    None = 0,
    HitPoints = 1 << 0,
    Mana = 1 << 1,
    Posture = 1 << 2,
    Shield = 1 << 3,
    Status = 1 << 4,
    Formation = 1 << 5,
    Deployment = 1 << 6,
    Timeline = 1 << 7,
    Resource = 1 << 8,
    PostureLifecycle = 1 << 9,
    Stamina = 1 << 10
}

internal readonly struct BattleParticipantRuleSnapshot
{
    internal BattleParticipantRuleSnapshot(
        ParticipantId participant,
        float currentHp,
        float maximumHp,
        float currentMp,
        float maximumMp,
        float currentPosture,
        float maximumPosture,
        float currentShield,
        DRPGFormationRow formationRow,
        bool isDeployed,
        IReadOnlyList<DRPGStatusRuntimeSnapshot> statuses,
        string ownerStableId = null,
        DRPGPostureRuleSnapshot? postureState = null,
        bool isDefending = false, DRPGBattleSide side = default, int partyOrder = 0,
        DRPGBattleRosterRole rosterRole = default, float postureRecoveryPerTurn = 0f,
        BattleStaminaValue? stamina = null,
        double staminaRecoveryPerCompletedAction = 0d)
    {
        Participant = participant;
        OwnerStableId = ownerStableId?.Trim() ?? string.Empty;
        CurrentHp = currentHp;
        MaximumHp = maximumHp;
        CurrentMp = currentMp;
        MaximumMp = maximumMp;
        CurrentPosture = currentPosture;
        MaximumPosture = maximumPosture;
        CurrentShield = currentShield;
        FormationRow = formationRow;
        IsDeployed = isDeployed;
        PostureState = postureState ?? DRPGPostureRuleSnapshot.Stable;
        IsDefending = isDefending;
        Side = side; PartyOrder = partyOrder; RosterRole = rosterRole;
        PostureRecoveryPerTurn = postureRecoveryPerTurn;
        Stamina = stamina ?? new BattleStaminaValue(1, 1d, 0d);
        if (double.IsNaN(staminaRecoveryPerCompletedAction) ||
            double.IsInfinity(staminaRecoveryPerCompletedAction) ||
            staminaRecoveryPerCompletedAction < 0d)
            throw new ArgumentOutOfRangeException(
                nameof(staminaRecoveryPerCompletedAction));
        StaminaRecoveryPerCompletedAction = staminaRecoveryPerCompletedAction;
        var statusCopy = new DRPGStatusRuntimeSnapshot[
            statuses != null ? statuses.Count : 0];
        for (int i = 0; i < statusCopy.Length; i++)
            statusCopy[i] = statuses[i];
        // Retain collection identity/counters, including when there are no entries.
        Statuses = statuses is DRPGStatusCollectionSnapshot complete
            ? new DRPGStatusCollectionSnapshot(complete.OwnerParticipantId, complete.NextInsertionOrdinal, statusCopy, complete.ObservedBoundarySerial)
            : DRPGStatusRuntimeCollection.FromSnapshot(statusCopy, participant).Snapshot();
    }

    internal ParticipantId Participant { get; }
    internal string OwnerStableId { get; }
    internal DRPGBattleSide Side { get; }
    internal int PartyOrder { get; }
    internal DRPGBattleRosterRole RosterRole { get; }
    internal DRPGPostureRuleSnapshot PostureState { get; }
    internal bool IsDefending { get; }
    internal float PostureRecoveryPerTurn { get; }
    internal BattleStaminaValue Stamina { get; }
    internal double StaminaRecoveryPerCompletedAction { get; }
    internal float CurrentHp { get; }
    internal float MaximumHp { get; }
    internal float CurrentMp { get; }
    internal float MaximumMp { get; }
    internal float CurrentPosture { get; }
    internal float MaximumPosture { get; }
    internal float CurrentShield { get; }
    internal DRPGFormationRow FormationRow { get; }
    internal bool IsDeployed { get; }
    internal IReadOnlyList<DRPGStatusRuntimeSnapshot> Statuses { get; }
}

internal readonly struct BattleTimelineRuleSnapshot
{
    internal BattleTimelineRuleSnapshot(
        ParticipantId participant,
        float scheduledTime,
        int priority)
    {
        Participant = participant;
        ScheduledTime = scheduledTime;
        Priority = priority;
    }

    internal ParticipantId Participant { get; }
    internal float ScheduledTime { get; }
    internal int Priority { get; }
}

internal readonly struct BattleResourceRuleSnapshot
{
    internal BattleResourceRuleSnapshot(string resourceId, int value)
    {
        ResourceId = resourceId ?? string.Empty;
        Value = value;
    }

    internal string ResourceId { get; }
    internal int Value { get; }
}

internal sealed class BattleRuleSnapshot
{
    private readonly Dictionary<ParticipantId, BattleParticipantRuleSnapshot>
        participantById;
    private readonly Dictionary<string, int> resourceById;

    internal BattleRuleSnapshot(
        long battleGeneration,
        long revision,
        float currentLogicalTime,
        IReadOnlyList<BattleParticipantRuleSnapshot> participants,
        IReadOnlyList<BattleTimelineRuleSnapshot> timeline,
        IReadOnlyList<BattleResourceRuleSnapshot> resources,
        long statusApplicationBoundarySerial = 0)
    {
        BattleGeneration = battleGeneration;
        Revision = revision;
        CurrentLogicalTime = currentLogicalTime;
        if (statusApplicationBoundarySerial < 0) throw new ArgumentOutOfRangeException(nameof(statusApplicationBoundarySerial));
        StatusApplicationBoundarySerial = statusApplicationBoundarySerial;

        var participantCopy = new BattleParticipantRuleSnapshot[
            participants != null ? participants.Count : 0];
        participantById = new Dictionary<
            ParticipantId,
            BattleParticipantRuleSnapshot>();
        for (int i = 0; i < participantCopy.Length; i++)
        {
            BattleParticipantRuleSnapshot participant = participants[i];
            participantCopy[i] = participant;
            participantById.Add(participant.Participant, participant);
        }
        Participants = Array.AsReadOnly(participantCopy);

        var timelineCopy = new BattleTimelineRuleSnapshot[
            timeline != null ? timeline.Count : 0];
        for (int i = 0; i < timelineCopy.Length; i++)
            timelineCopy[i] = timeline[i];
        Timeline = Array.AsReadOnly(timelineCopy);

        var resourceCopy = new BattleResourceRuleSnapshot[
            resources != null ? resources.Count : 0];
        resourceById = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < resourceCopy.Length; i++)
        {
            BattleResourceRuleSnapshot resource = resources[i];
            resourceCopy[i] = resource;
            resourceById.Add(resource.ResourceId, resource.Value);
        }
        Resources = Array.AsReadOnly(resourceCopy);
    }

    internal long BattleGeneration { get; }
    internal long Revision { get; }
    internal float CurrentLogicalTime { get; }
    internal long StatusApplicationBoundarySerial { get; }
    internal IReadOnlyList<BattleParticipantRuleSnapshot> Participants { get; }
    internal IReadOnlyList<BattleTimelineRuleSnapshot> Timeline { get; }
    internal IReadOnlyList<BattleResourceRuleSnapshot> Resources { get; }

    internal bool TryGetParticipant(
        ParticipantId participant,
        out BattleParticipantRuleSnapshot snapshot)
    {
        return participantById.TryGetValue(participant, out snapshot);
    }

    internal bool TryGetResource(string resourceId, out int value)
    {
        return resourceById.TryGetValue(resourceId ?? string.Empty, out value);
    }
}

internal enum DRPGStatusMutationKind
{
    Apply = 0,
    Remove = 1,
    ConsumeBoundary = 2
}

internal readonly struct DRPGStatusMutation
{
    internal DRPGStatusMutation(
        DRPGStatusMutationKind kind,
        DRPGResolvedStatusSpec spec,
        string statusId,
        int stacks,
        int duration,
        ParticipantId sourceParticipantId = default,
        long appliedBoundarySerial = 0)
    {
        Kind = kind;
        Spec = spec;
        StatusId = statusId ?? string.Empty;
        Stacks = stacks;
        Duration = duration;
        SourceParticipantId = sourceParticipantId;
        AppliedBoundarySerial = appliedBoundarySerial;
        Boundary = default;
        BoundaryBefore = default;
    }

    internal DRPGStatusMutation(DRPGStatusBoundary boundary, DRPGStatusRuntimeSnapshot before)
        : this(DRPGStatusMutationKind.ConsumeBoundary, before.Spec, before.StatusId,
            before.Stacks, before.RemainingTurns, before.SourceParticipantId, before.AppliedBoundarySerial)
    {
        Boundary = boundary;
        BoundaryBefore = before;
    }

    internal DRPGStatusMutationKind Kind { get; }
    internal DRPGResolvedStatusSpec Spec { get; }
    internal string StatusId { get; }
    internal int Stacks { get; }
    internal int Duration { get; }
    internal ParticipantId SourceParticipantId { get; }
    internal long AppliedBoundarySerial { get; }
    internal DRPGStatusBoundary Boundary { get; }
    internal DRPGStatusRuntimeSnapshot BoundaryBefore { get; }
}

internal readonly struct DRPGActionResourceMutationCheckpoint
{
    internal DRPGActionResourceMutationCheckpoint(
        ParticipantId participant,
        float mana,
        float posture,
        bool manaWasDirty,
        bool postureWasDirty,
        BattleStaminaValue stamina,
        bool staminaWasDirty,
        DRPGPostureRuleSnapshot postureState,
        bool isDefending,
        bool lifecycleWasDirty)
    {
        Participant = participant;
        Mana = mana;
        Posture = posture;
        ManaWasDirty = manaWasDirty;
        PostureWasDirty = postureWasDirty;
        Stamina = stamina;
        StaminaWasDirty = staminaWasDirty;
        PostureState = postureState; IsDefending = isDefending; LifecycleWasDirty = lifecycleWasDirty;
    }

    internal ParticipantId Participant { get; }
    internal float Mana { get; }
    internal float Posture { get; }
    internal bool ManaWasDirty { get; }
    internal bool PostureWasDirty { get; }
    internal BattleStaminaValue Stamina { get; }
    internal bool StaminaWasDirty { get; }
    internal DRPGPostureRuleSnapshot PostureState { get; }
    internal bool IsDefending { get; }
    internal bool LifecycleWasDirty { get; }
}

internal sealed class ParticipantMutationBuilder
{
    private readonly List<DRPGStatusMutation> statuses =
        new List<DRPGStatusMutation>();

    internal ParticipantMutationBuilder(BattleParticipantRuleSnapshot snapshot, long statusApplicationBoundarySerial = 0)
    {
        Snapshot = snapshot;
        StatusApplicationBoundarySerial = statusApplicationBoundarySerial;
        HitPoints = snapshot.CurrentHp;
        Mana = snapshot.CurrentMp;
        Posture = snapshot.CurrentPosture;
        Stamina = snapshot.Stamina;
        Shield = snapshot.CurrentShield;
        FormationRow = snapshot.FormationRow;
        IsDeployed = snapshot.IsDeployed;
        PostureState = snapshot.PostureState;
        IsDefending = snapshot.IsDefending;
    }

    internal BattleParticipantRuleSnapshot Snapshot { get; }
    internal long StatusApplicationBoundarySerial { get; }
    internal float HitPoints { get; private set; }
    internal float Mana { get; private set; }
    internal float Posture { get; private set; }
    internal BattleStaminaValue Stamina { get; private set; }
    internal float Shield { get; private set; }
    internal DRPGPostureRuleSnapshot PostureState { get; private set; }
    internal bool IsDefending { get; private set; }
    internal float IncomingDefenseMultiplier => (IsDefending ? 0.5f : 1f) * PostureState.DamageMultiplier;
    internal DRPGFormationRow FormationRow { get; private set; }
    internal bool IsDeployed { get; private set; }
    internal DRPGBattleMutationDirtySet DirtySet { get; private set; }

    internal DRPGStatusRuntimeCollection CreateStatusProjection()
    {
        var projection = DRPGStatusRuntimeCollection.FromSnapshot(Snapshot.Statuses);
        projection.ApplyDetachedMutations(statuses);
        return projection;
    }

    internal ParticipantMutationBuilder Fork()
    {
        var copy = new ParticipantMutationBuilder(Snapshot, StatusApplicationBoundarySerial)
        {
            HitPoints = HitPoints,
            Mana = Mana,
            Posture = Posture,
            Stamina = Stamina,
            Shield = Shield,
            FormationRow = FormationRow,
            IsDeployed = IsDeployed,
            PostureState = PostureState,
            IsDefending = IsDefending,
            DirtySet = DirtySet
        };
        copy.statuses.AddRange(statuses);
        return copy;
    }

    internal ParticipantMutationBuilder SetHitPoints(float value)
    {
        HitPoints = value;
        DirtySet |= DRPGBattleMutationDirtySet.HitPoints;
        return this;
    }

    internal ParticipantMutationBuilder SetMana(float value)
    {
        Mana = value;
        DirtySet |= DRPGBattleMutationDirtySet.Mana;
        return this;
    }

    internal ParticipantMutationBuilder SetPosture(float value)
    {
        Posture = value;
        DirtySet |= DRPGBattleMutationDirtySet.Posture;
        return this;
    }

    internal ParticipantMutationBuilder SetStamina(BattleStaminaValue value)
    {
        if (value.Capacity != Snapshot.Stamina.Capacity)
            throw new InvalidOperationException(
                "Stamina mutation cannot change battle capacity.");
        Stamina = value;
        DirtySet |= DRPGBattleMutationDirtySet.Stamina;
        return this;
    }

    internal void ClearDefendForTurnEntry()
    {
        if (!IsDefending) return;
        IsDefending = false;
        DirtySet |= DRPGBattleMutationDirtySet.PostureLifecycle;
    }

    internal DRPGTurnEntryLifecycle ResolveTurnEntryLifecycle()
    {
        var before = PostureState;
        float postureBefore = Posture;
        bool skip = false;
        BattleStaminaValue creditedStamina = Stamina.CreditOwnTurn();
        if (creditedStamina != Stamina)
            SetStamina(creditedStamina);
        // Battle V2 does not skip, expose, amplify, reset, or passively refill a
        // turn when posture reaches zero. Recovery is an explicit action/card.
        if (!PostureState.Same(before)) DirtySet |= DRPGBattleMutationDirtySet.PostureLifecycle;
        return new DRPGTurnEntryLifecycle(Snapshot.Participant, skip, HitPoints > 0f && IsDeployed,
            Snapshot.IsDefending, before, PostureState, postureBefore, Posture);
    }

    internal DRPGActionResourceMutationCheckpoint CaptureActionResources()
    {
        return new DRPGActionResourceMutationCheckpoint(
            Snapshot.Participant,
            Mana,
            Posture,
            (DirtySet & DRPGBattleMutationDirtySet.Mana) != 0,
            (DirtySet & DRPGBattleMutationDirtySet.Posture) != 0,
            Stamina,
            (DirtySet & DRPGBattleMutationDirtySet.Stamina) != 0,
            PostureState, IsDefending, (DirtySet & DRPGBattleMutationDirtySet.PostureLifecycle) != 0);
    }

    internal void RestoreActionResources(
        in DRPGActionResourceMutationCheckpoint checkpoint)
    {
        if (checkpoint.Participant != Snapshot.Participant)
        {
            throw new InvalidOperationException(
                "An action-resource checkpoint belongs to another participant.");
        }

        Mana = checkpoint.Mana;
        Posture = checkpoint.Posture;
        Stamina = checkpoint.Stamina;
        PostureState = checkpoint.PostureState;
        IsDefending = checkpoint.IsDefending;
        DirtySet &= ~(DRPGBattleMutationDirtySet.Mana |
            DRPGBattleMutationDirtySet.Posture | DRPGBattleMutationDirtySet.PostureLifecycle |
            DRPGBattleMutationDirtySet.Stamina);
        if (checkpoint.LifecycleWasDirty) DirtySet |= DRPGBattleMutationDirtySet.PostureLifecycle;
        if (checkpoint.ManaWasDirty)
            DirtySet |= DRPGBattleMutationDirtySet.Mana;
        if (checkpoint.PostureWasDirty)
            DirtySet |= DRPGBattleMutationDirtySet.Posture;
        if (checkpoint.StaminaWasDirty)
            DirtySet |= DRPGBattleMutationDirtySet.Stamina;
    }

    internal ParticipantMutationBuilder SetShield(float value)
    {
        Shield = value;
        DirtySet |= DRPGBattleMutationDirtySet.Shield;
        return this;
    }

    internal ParticipantMutationBuilder SetFormationRow(DRPGFormationRow value)
    {
        FormationRow = value;
        DirtySet |= DRPGBattleMutationDirtySet.Formation;
        return this;
    }

    internal ParticipantMutationBuilder SetDeployed(bool value)
    {
        IsDeployed = value;
        DirtySet |= DRPGBattleMutationDirtySet.Deployment;
        return this;
    }

    internal ParticipantMutationBuilder ApplyStatus(
        DRPGResolvedStatusSpec spec,
        int stacks = 1,
        int duration = 0,
        ParticipantId sourceParticipantId = default)
    {
        statuses.Add(new DRPGStatusMutation(
            DRPGStatusMutationKind.Apply,
            spec,
            spec != null ? spec.StatusId : string.Empty,
            stacks,
            duration,
            sourceParticipantId.Value > 0 ? sourceParticipantId : Snapshot.Participant,
            StatusApplicationBoundarySerial));
        DirtySet |= DRPGBattleMutationDirtySet.Status;
        return this;
    }

    internal bool TryConsumeStatusBoundary(DRPGStatusBoundary boundary, string instanceId, out DRPGStatusBoundaryStep step)
    {
        var projection = CreateStatusProjection();
        if (!projection.TryConsumeBoundaryInstance(boundary, instanceId, out step)) return false;
        statuses.Add(new DRPGStatusMutation(boundary, step.Before));
        DirtySet |= DRPGBattleMutationDirtySet.Status;
        return true;
    }

    internal ParticipantMutationBuilder RemoveStatus(string statusId)
    {
        statuses.Add(new DRPGStatusMutation(
            DRPGStatusMutationKind.Remove,
            null,
            statusId,
            0,
            0));
        DirtySet |= DRPGBattleMutationDirtySet.Status;
        return this;
    }

    internal DRPGParticipantMutationRecord BuildValidated()
    {
        PostureState.Validate();
        CreateStatusProjection();
        RequireFiniteRange(HitPoints, 0f, Snapshot.MaximumHp, "HP");
        RequireFiniteRange(Mana, 0f, Snapshot.MaximumMp, "MP");
        RequireFiniteRange(Posture, 0f, float.MaxValue, "posture");
        if (Stamina.Capacity != Snapshot.Stamina.Capacity)
            throw new InvalidOperationException("Stamina capacity changed inside a battle journal.");
        RequireFiniteRange(Shield, 0f, float.MaxValue, "shield");
        if (!Enum.IsDefined(typeof(DRPGFormationRow), FormationRow))
        {
            throw new InvalidOperationException(
                "Formation row mutation is invalid.");
        }
        for (int i = 0; i < statuses.Count; i++)
        {
            DRPGStatusMutation status = statuses[i];
            if (status.Kind == DRPGStatusMutationKind.Apply &&
                (status.Spec == null || status.Stacks <= 0 ||
                 status.Duration < 0))
            {
                throw new InvalidOperationException(
                    "An applied status mutation is invalid.");
            }
            if (status.Kind == DRPGStatusMutationKind.Remove &&
                string.IsNullOrWhiteSpace(status.StatusId))
            {
                throw new InvalidOperationException(
                    "A removed status mutation requires an ID.");
            }
        }

        return new DRPGParticipantMutationRecord(
            Snapshot.Participant,
            HitPoints,
            Mana,
            Posture,
            Shield,
            FormationRow,
            IsDeployed,
            DirtySet,
            statuses, PostureState, IsDefending, Stamina);
    }

    private static void RequireFiniteRange(
        float value,
        float minimum,
        float maximum,
        string name)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) ||
            value < minimum || value > maximum)
        {
            throw new InvalidOperationException(
                name + " mutation is outside the captured rule bounds.");
        }
    }
}

internal readonly struct DRPGParticipantMutationRecord
{
    internal DRPGParticipantMutationRecord(
        ParticipantId participant,
        float hitPoints,
        float mana,
        float posture,
        float shield,
        DRPGFormationRow formationRow,
        bool isDeployed,
        DRPGBattleMutationDirtySet dirtySet,
        IReadOnlyList<DRPGStatusMutation> statuses,
        DRPGPostureRuleSnapshot? postureState = null,
        bool isDefending = false,
        BattleStaminaValue? stamina = null)
    {
        Participant = participant;
        HitPoints = hitPoints;
        Mana = mana;
        Posture = posture;
        Shield = shield;
        FormationRow = formationRow;
        IsDeployed = isDeployed;
        DirtySet = dirtySet;
        PostureState = postureState ?? DRPGPostureRuleSnapshot.Stable;
        IsDefending = isDefending;
        Stamina = stamina ?? new BattleStaminaValue(1, 1d, 0d);
        var copy = new DRPGStatusMutation[
            statuses != null ? statuses.Count : 0];
        for (int i = 0; i < copy.Length; i++)
            copy[i] = statuses[i];
        Statuses = Array.AsReadOnly(copy);
    }

    internal ParticipantId Participant { get; }
    internal float HitPoints { get; }
    internal float Mana { get; }
    internal float Posture { get; }
    internal float Shield { get; }
    internal DRPGFormationRow FormationRow { get; }
    internal bool IsDeployed { get; }
    internal DRPGBattleMutationDirtySet DirtySet { get; }
    internal DRPGPostureRuleSnapshot PostureState { get; }
    internal bool IsDefending { get; }
    internal BattleStaminaValue Stamina { get; }
    internal IReadOnlyList<DRPGStatusMutation> Statuses { get; }
}

internal readonly struct DRPGTimelineMutationRecord
{
    internal DRPGTimelineMutationRecord(
        ParticipantId participant,
        float scheduledTime,
        int priority,
        bool isRemoval = false)
    {
        Participant = participant;
        ScheduledTime = scheduledTime;
        Priority = priority;
        IsRemoval = isRemoval;
    }

    internal ParticipantId Participant { get; }
    internal float ScheduledTime { get; }
    internal int Priority { get; }
    internal bool IsRemoval { get; }
}

internal sealed class DRPGBattleMutationJournal
{
    internal DRPGBattleMutationJournal(
        long baseRevision,
        IReadOnlyList<DRPGParticipantMutationRecord> participants,
        IReadOnlyList<DRPGTimelineMutationRecord> timeline,
        IReadOnlyList<BattleResourceRuleSnapshot> resources,
        DRPGBattleMutationDirtySet dirtySet,
        DRPGBattleActionResourceJournal actionResources = null,
        DRPGActionCostReceipt actionCosts = null, float? logicalTime = null)
    {
        BaseRevision = baseRevision;
        ParticipantMutations = participants;
        TimelineMutations = timeline;
        ResourceMutations = resources;
        DirtySet = dirtySet;
        ActionResources = actionResources;
        ActionCosts = actionCosts;
        LogicalTime = logicalTime;
    }

    internal long BaseRevision { get; }
    internal IReadOnlyList<DRPGParticipantMutationRecord>
        ParticipantMutations { get; }
    internal IReadOnlyList<DRPGTimelineMutationRecord> TimelineMutations { get; }
    internal IReadOnlyList<BattleResourceRuleSnapshot> ResourceMutations { get; }
    internal DRPGBattleMutationDirtySet DirtySet { get; }
    internal DRPGBattleActionResourceJournal ActionResources { get; }
    internal DRPGActionCostReceipt ActionCosts { get; }
    internal float? LogicalTime { get; }
}

internal sealed class DRPGBattleMutationWorkspace
{
    internal DRPGBattleActionResourceWorkspace ActionResources { get; private set; }
    internal DRPGActionCostReservation ActionCosts { get; private set; }

    internal void AttachActionCosts(DRPGActionCostReservation costs)
    {
        if (ActionCosts != null || costs == null || costs.StagedCosts == null)
            throw new InvalidOperationException("A workspace can carry only one staged root cost reservation.");
        ActionCosts = costs;
    }

    private readonly Dictionary<ParticipantId, ParticipantMutationBuilder>
        participantBuilders = new Dictionary<
            ParticipantId,
            ParticipantMutationBuilder>();
    private readonly Dictionary<ParticipantId, DRPGTimelineMutationRecord>
        timelineMutations = new Dictionary<
            ParticipantId,
            DRPGTimelineMutationRecord>();
    private readonly Dictionary<string, int> resourceMutations =
        new Dictionary<string, int>(StringComparer.Ordinal);

    internal DRPGBattleMutationWorkspace(
        long baseRevision,
        BattleRuleSnapshot snapshot,
        DRPGBattleActionResourceSnapshot actionResources = null)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        if (baseRevision < 0L || snapshot.Revision != baseRevision)
        {
            throw new ArgumentOutOfRangeException(nameof(baseRevision));
        }
        BaseRevision = baseRevision;
        ActionResources = actionResources != null ? new DRPGBattleActionResourceWorkspace(actionResources, this) : null;
    }

    internal long BaseRevision { get; }
    internal BattleRuleSnapshot Snapshot { get; }
    private float? logicalTime;
    internal float CurrentLogicalTime => logicalTime ?? Snapshot.CurrentLogicalTime;

    internal void AdvanceTurnEntryLogicalTime(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < CurrentLogicalTime)
            throw new InvalidOperationException("TurnEntry logical time must be finite and monotonic.");
        logicalTime = value;
    }

    // Preserve pending writes, including dirty flags, without sharing builders.
    // A failed rule expansion can discard this fork without compensating writes.
    internal DRPGBattleMutationWorkspace Fork()
    {
        var copy = new DRPGBattleMutationWorkspace(BaseRevision, Snapshot);
        foreach (var pair in participantBuilders)
            copy.participantBuilders.Add(pair.Key, pair.Value.Fork());
        foreach (var pair in timelineMutations)
            copy.timelineMutations.Add(pair.Key, pair.Value);
        foreach (var pair in resourceMutations)
            copy.resourceMutations.Add(pair.Key, pair.Value);
        copy.ActionResources = ActionResources != null ? ActionResources.Fork(copy) : null;
        copy.ActionCosts = ActionCosts;
        copy.logicalTime = logicalTime;
        return copy;
    }

    internal ParticipantMutationBuilder For(ParticipantId participant)
    {
        if (!Snapshot.TryGetParticipant(
                participant,
                out BattleParticipantRuleSnapshot participantSnapshot))
        {
            throw new InvalidOperationException(
                "The mutation workspace does not contain participant " +
                participant + ".");
        }

        if (!participantBuilders.TryGetValue(
                participant,
                out ParticipantMutationBuilder builder))
        {
            builder = new ParticipantMutationBuilder(participantSnapshot, Snapshot.StatusApplicationBoundarySerial);
            participantBuilders.Add(participant, builder);
        }
        return builder;
    }

    internal bool TryGetScheduledProjection(ParticipantId participant, out BattleTimelineRuleSnapshot result)
    {
        if (timelineMutations.TryGetValue(participant, out var pending))
        {
            if (pending.IsRemoval) { result = default; return false; }
            result = new BattleTimelineRuleSnapshot(participant, pending.ScheduledTime, pending.Priority);
            return true;
        }
        foreach (var entry in Snapshot.Timeline)
            if (entry.Participant == participant) { result = entry; return true; }
        result = default;
        return false;
    }

    internal void RemoveFromTimeline(ParticipantId participant)
    {
        if (!Snapshot.TryGetParticipant(participant, out _))
            throw new InvalidOperationException("A timeline departure requires a captured participant.");
        timelineMutations[participant] = new DRPGTimelineMutationRecord(
            participant, CurrentLogicalTime, 0, true);
    }

    internal void ScheduleAt(
        ParticipantId participant,
        float scheduledTime,
        int priority)
    {
        if (!Snapshot.TryGetParticipant(participant, out _))
        {
            throw new InvalidOperationException(
                "A timeline mutation requires a captured participant.");
        }
        timelineMutations[participant] = new DRPGTimelineMutationRecord(
            participant,
            scheduledTime,
            priority);
    }

    internal void SetResource(string resourceId, int value)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            throw new ArgumentException(
                "A resource mutation requires an ID.",
                nameof(resourceId));
        }
        resourceMutations[resourceId] = value;
    }

    internal DRPGBattleMutationJournal BuildValidatedJournal()
    {
        var participants = new List<DRPGParticipantMutationRecord>();
        DRPGBattleMutationDirtySet dirty = DRPGBattleMutationDirtySet.None;
        foreach (ParticipantMutationBuilder builder in
            participantBuilders.Values)
        {
            DRPGParticipantMutationRecord record = builder.BuildValidated();
            if (record.DirtySet == DRPGBattleMutationDirtySet.None)
                continue;
            participants.Add(record);
            dirty |= record.DirtySet;
        }

        var timeline = new List<DRPGTimelineMutationRecord>();
        foreach (DRPGTimelineMutationRecord record in timelineMutations.Values)
        {
            if (float.IsNaN(record.ScheduledTime) ||
                float.IsInfinity(record.ScheduledTime) ||
                record.ScheduledTime < CurrentLogicalTime)
            {
                throw new InvalidOperationException(
                    "A timeline mutation cannot precede captured logical time.");
            }
            timeline.Add(record);
            dirty |= DRPGBattleMutationDirtySet.Timeline;
        }

        var resources = new List<BattleResourceRuleSnapshot>();
        foreach (KeyValuePair<string, int> pair in resourceMutations)
        {
            if (pair.Value < 0)
            {
                throw new InvalidOperationException(
                    "A resource mutation cannot be negative.");
            }
            resources.Add(new BattleResourceRuleSnapshot(pair.Key, pair.Value));
            dirty |= DRPGBattleMutationDirtySet.Resource;
        }

        DRPGBattleActionResourceJournal actionResources = ActionResources != null ? ActionResources.BuildJournal() : null;
        if (actionResources != null && actionResources.HasChanges)
            dirty |= DRPGBattleMutationDirtySet.Resource;
        if (logicalTime.HasValue) dirty |= DRPGBattleMutationDirtySet.Timeline;
        return new DRPGBattleMutationJournal(
            BaseRevision,
            Array.AsReadOnly(participants.ToArray()),
            Array.AsReadOnly(timeline.ToArray()),
            Array.AsReadOnly(resources.ToArray()),
            dirty,
            actionResources,
            ActionCosts?.StagedCosts, logicalTime);
    }
}

internal readonly struct BattleJournalCommitResult
{
    private BattleJournalCommitResult(
        bool succeeded,
        long committedRevision,
        DRPGBattleMutationDirtySet dirtySet,
        string error)
    {
        Succeeded = succeeded;
        CommittedRevision = committedRevision;
        DirtySet = dirtySet;
        Error = error ?? string.Empty;
    }

    internal bool Succeeded { get; }
    internal long CommittedRevision { get; }
    internal DRPGBattleMutationDirtySet DirtySet { get; }
    internal string Error { get; }

    internal static BattleJournalCommitResult Success(
        long committedRevision,
        DRPGBattleMutationDirtySet dirtySet)
    {
        return new BattleJournalCommitResult(
            true,
            committedRevision,
            dirtySet,
            string.Empty);
    }

    internal static BattleJournalCommitResult Failure(
        long currentRevision,
        string error)
    {
        return new BattleJournalCommitResult(
            false,
            currentRevision,
            DRPGBattleMutationDirtySet.None,
            error);
    }
}
