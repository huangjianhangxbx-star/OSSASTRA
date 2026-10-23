using System;
using System.Collections.Generic;

public sealed class DRPGTurnScheduler
{
    private const float TimelineReferenceSpeed = 10f;
    private List<DRPGTurnScheduleEntry> entries =
        new List<DRPGTurnScheduleEntry>();
    private float playerMedianSpeed = TimelineReferenceSpeed;
    private float enemyMedianSpeed = TimelineReferenceSpeed;
    private DRPGBattleStateMutationCapability stateMutationCapability;
    private int stateMutationScopeDepth;

    public float CurrentTime { get; private set; }
    public int Count => entries.Count;

    // Build and sort off-line. Applying a validated State journal only swaps
    // the prepared list; it must not filter a newly defeated actor or allocate
    // and throw after participant values have already been committed.
    internal PreparedJournal PrepareJournal(DRPGBattleStateMutationCapability capability,
        IReadOnlyList<DRPGTimelineMutationRecord> mutations,
        IReadOnlyDictionary<ParticipantId, BattleParticipantRuntime> participants, float? logicalTime = null)
    {
        RequireStateCapability(capability);
        if (mutations.Count == 0 && !logicalTime.HasValue) return null;
        return new PreparedJournal(this, mutations, participants, logicalTime);
    }

    internal sealed class PreparedJournal
    {
        private readonly DRPGTurnScheduler owner;
        private readonly List<DRPGTurnScheduleEntry> replacement;
        private readonly float logicalTime;
        internal PreparedJournal(DRPGTurnScheduler owner, IReadOnlyList<DRPGTimelineMutationRecord> mutations,
            IReadOnlyDictionary<ParticipantId, BattleParticipantRuntime> participants, float? logicalTime = null)
        {
            this.owner = owner;
            this.logicalTime = logicalTime ?? owner.CurrentTime;
            if (float.IsNaN(this.logicalTime) || float.IsInfinity(this.logicalTime) || this.logicalTime < owner.CurrentTime)
                throw new InvalidOperationException("Cannot prepare invalid logical time.");
            replacement = new List<DRPGTurnScheduleEntry>(owner.entries);
            foreach (var mutation in mutations)
            {
                if (!participants.TryGetValue(mutation.Participant, out var participant) || participant == null ||
                    float.IsNaN(mutation.ScheduledTime) || float.IsInfinity(mutation.ScheduledTime) ||
                    mutation.ScheduledTime < this.logicalTime)
                    throw new InvalidOperationException("Cannot prepare an invalid timeline mutation.");
                for (int i = replacement.Count - 1; i >= 0; i--)
                    if (replacement[i].Participant.RuntimeId == mutation.Participant.Value) replacement.RemoveAt(i);
                if (!mutation.IsRemoval)
                    replacement.Add(new DRPGTurnScheduleEntry(participant, mutation.ScheduledTime, mutation.Priority));
            }
            replacement.Sort(CompareEntries);
        }
        internal void Apply(DRPGBattleStateMutationCapability capability)
        {
            owner.RequireStateCapability(capability);
            owner.entries = replacement;
            owner.CurrentTime = logicalTime;
        }
    }

    public void Initialize(IEnumerable<BattleParticipantRuntime> participants)
    {
        RequireDirectMutationAccess();
        if (participants == null)
            throw new ArgumentNullException(nameof(participants));

        ClearCore();
        var snapshot = new List<BattleParticipantRuntime>();
        var playerSpeeds = new List<float>();
        var enemySpeeds = new List<float>();
        foreach (BattleParticipantRuntime participant in participants)
        {
            if (participant == null || !participant.IsAlive)
                continue;
            snapshot.Add(participant);
            if (participant.Side == DRPGBattleSide.Player)
                playerSpeeds.Add(participant.EffectiveAgility);
            else
                enemySpeeds.Add(participant.EffectiveAgility);
        }
        playerMedianSpeed = DRPGRelativeSpeedPolicy.Median(
            playerSpeeds,
            enemySpeeds.Count > 0 ? DRPGRelativeSpeedPolicy.Median(enemySpeeds, TimelineReferenceSpeed) : TimelineReferenceSpeed);
        enemyMedianSpeed = DRPGRelativeSpeedPolicy.Median(
            enemySpeeds,
            playerMedianSpeed);
        for (int i = 0; i < snapshot.Count; i++)
            ScheduleAtCore(snapshot[i], CalculateInitialDelay(snapshot[i]), 0);
    }

    public void Clear()
    {
        RequireDirectMutationAccess();
        ClearCore();
    }

    private void ClearCore()
    {
        entries.Clear();
        CurrentTime = 0f;
    }

    public void ScheduleAt(
        BattleParticipantRuntime participant,
        float scheduledTime,
        int priority)
    {
        RequireDirectMutationAccess();
        ScheduleAtCore(participant, scheduledTime, priority);
    }

    internal void ScheduleAt(
        DRPGBattleStateMutationCapability capability,
        BattleParticipantRuntime participant,
        float scheduledTime,
        int priority)
    {
        RequireStateCapability(capability);
        ScheduleAtCore(participant, scheduledTime, priority);
    }

    private void ScheduleAtCore(
        BattleParticipantRuntime participant,
        float scheduledTime,
        int priority)
    {
        if (participant == null)
            throw new ArgumentNullException(nameof(participant));
        if (scheduledTime < CurrentTime)
            throw new ArgumentOutOfRangeException(nameof(scheduledTime));

        RemoveParticipantCore(participant.RuntimeId);
        if (!participant.IsAlive)
            return;

        entries.Add(new DRPGTurnScheduleEntry(participant, scheduledTime, priority));
        entries.Sort(CompareEntries);
    }

    public BattleParticipantRuntime TakeNext()
    {
        DRPGTurnScheduleEntry? entry = TakeNextEntry();
        return entry.HasValue ? entry.Value.Participant : null;
    }

    public DRPGTurnScheduleEntry? PeekNextEntry()
    {
        return entries.Count > 0
            ? entries[0]
            : (DRPGTurnScheduleEntry?)null;
    }

    public DRPGTurnScheduleEntry? TakeNextEntry()
    {
        RequireDirectMutationAccess();
        RemoveDeadParticipants();
        if (entries.Count == 0)
            return null;

        DRPGTurnScheduleEntry entry = entries[0];
        entries.RemoveAt(0);
        CurrentTime = entry.ScheduledTime;
        return entry;
    }

    internal bool TryConsumeReservedEntry(DRPGTurnScheduleEntry reserved)
    {
        RequireDirectMutationAccess();
        return TryConsumeReservedEntryCore(reserved);
    }

    internal bool TryConsumeReservedEntry(
        DRPGBattleStateMutationCapability capability,
        DRPGTurnScheduleEntry reserved)
    {
        RequireStateCapability(capability);
        return TryConsumeReservedEntryCore(reserved);
    }

    private bool TryConsumeReservedEntryCore(DRPGTurnScheduleEntry reserved)
    {
        if (entries.Count == 0)
            return false;

        DRPGTurnScheduleEntry current = entries[0];
        if (!ReferenceEquals(current.Participant, reserved.Participant) ||
            Math.Abs(current.ScheduledTime - reserved.ScheduledTime) > 0.0001f ||
            current.Priority != reserved.Priority)
        {
            return false;
        }

        entries.RemoveAt(0);
        CurrentTime = current.ScheduledTime;
        return true;
    }

    public float CalculatePreparationDelay(
        BattleParticipantRuntime actor,
        float authoredPrepareDelay)
    {
        if (actor == null)
            throw new ArgumentNullException(nameof(actor));
        if (authoredPrepareDelay < 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(authoredPrepareDelay));
        }
        if (authoredPrepareDelay <= 0f)
            return 0f;

        return NormalizeTimelineDelay(actor, authoredPrepareDelay);
    }

    public float CalculateNextDelay(
        BattleParticipantRuntime actor,
        DRPGSkillSO skill,
        IReadOnlyList<IDRPGActionDelayModifier> modifiers)
    {
        if (actor == null)
            throw new ArgumentNullException(nameof(actor));

        float skillRecovery = skill != null ? skill.RecoveryDelay : 0f;
        float baseDelay = NormalizeTimelineDelay(
            actor,
            actor.Profile.BaseTimelineDelay +
            actor.EffectiveBaseRecoveryDelay +
            skillRecovery);

        var context = new DRPGActionDelayContext(actor, skill, baseDelay);
        float delay = baseDelay;
        if (modifiers != null)
        {
            for (int i = 0; i < modifiers.Count; i++)
            {
                IDRPGActionDelayModifier modifier = modifiers[i];
                if (modifier != null)
                    delay = modifier.ModifyDelay(context, delay);
            }
        }

        return Math.Max(0.001f, delay);
    }

    public float CalculateNextDelayFromSpec(
        BattleParticipantRuntime actor,
        DRPGResolvedActionSpec spec,
        IReadOnlyList<IDRPGActionDelayModifier> modifiers)
    {
        if (actor == null)
            throw new ArgumentNullException(nameof(actor));

        float recoveryDelay = spec != null ? spec.RecoveryDelay : 0f;
        float baseDelay = NormalizeTimelineDelay(
            actor,
            actor.Profile.BaseTimelineDelay +
            actor.EffectiveBaseRecoveryDelay +
            recoveryDelay);
        var context = new DRPGActionDelayContext(
            actor,
            null,
            baseDelay);
        float delay = baseDelay;
        if (modifiers != null)
        {
            for (int i = 0; i < modifiers.Count; i++)
            {
                IDRPGActionDelayModifier modifier = modifiers[i];
                if (modifier != null)
                    delay = modifier.ModifyDelay(context, delay);
            }
        }
        return Math.Max(0.001f, delay);
    }

    public float CalculateInitialDelay(BattleParticipantRuntime actor)
    {
        if (actor == null)
            throw new ArgumentNullException(nameof(actor));
        return NormalizeTimelineDelay(actor, actor.Profile.BaseTimelineDelay);
    }

    public void ScheduleNext(
        BattleParticipantRuntime actor,
        DRPGSkillSO skill,
        IReadOnlyList<IDRPGActionDelayModifier> modifiers,
        int priority = 0)
    {
        float delay = CalculateNextDelay(actor, skill, modifiers);
        ScheduleAt(actor, CurrentTime + delay, priority);
    }

    public void ScheduleNextFromSpec(
        BattleParticipantRuntime actor,
        DRPGResolvedActionSpec spec,
        IReadOnlyList<IDRPGActionDelayModifier> modifiers,
        int priority = 0)
    {
        float delay = CalculateNextDelayFromSpec(actor, spec, modifiers);
        ScheduleAt(actor, CurrentTime + delay, priority);
    }

    public bool TryShiftParticipant(
        int runtimeId,
        float timelineDelta,
        int priorityDelta,
        out float before,
        out float after)
    {
        RequireDirectMutationAccess();
        before = 0f;
        after = 0f;
        for (int i = 0; i < entries.Count; i++)
        {
            DRPGTurnScheduleEntry entry = entries[i];
            if (entry.Participant.RuntimeId != runtimeId)
                continue;

            before = entry.ScheduledTime;
            after = Math.Max(CurrentTime, before + timelineDelta);
            entries.RemoveAt(i);
            entries.Add(new DRPGTurnScheduleEntry(
                entry.Participant,
                after,
                entry.Priority + priorityDelta));
            entries.Sort(CompareEntries);
            return true;
        }
        return false;
    }

    public IReadOnlyList<DRPGTurnScheduleEntry> GetTimelineSnapshot()
    {
        return entries.ToArray();
    }

    public void RemoveParticipant(int runtimeId)
    {
        RequireDirectMutationAccess();
        RemoveParticipantCore(runtimeId);
    }

    internal void RemoveParticipant(
        DRPGBattleStateMutationCapability capability,
        int runtimeId)
    {
        RequireStateCapability(capability);
        RemoveParticipantCore(runtimeId);
    }

    private void RemoveParticipantCore(int runtimeId)
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i].Participant.RuntimeId == runtimeId)
                entries.RemoveAt(i);
        }
    }

    private void RemoveDeadParticipants()
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            BattleParticipantRuntime participant = entries[i].Participant;
            if (participant == null || !participant.IsAlive)
                entries.RemoveAt(i);
        }
    }

    internal void AttachStateMutationCapability(
        DRPGBattleStateMutationCapability capability)
    {
        ValidateStateMutationCapabilityAttach(capability);
        stateMutationCapability = capability;
    }

    internal void ValidateStateMutationCapabilityAttach(
        DRPGBattleStateMutationCapability capability)
    {
        if (capability == null)
            throw new ArgumentNullException(nameof(capability));
        if (stateMutationCapability != null &&
            !ReferenceEquals(stateMutationCapability, capability))
        {
            throw new InvalidOperationException(
                "The scheduler already belongs to another battle state.");
        }
    }

    internal void EnterStateMutationScope(
        DRPGBattleStateMutationCapability capability)
    {
        RequireStateCapability(capability);
        stateMutationScopeDepth++;
    }

    internal void ExitStateMutationScope(
        DRPGBattleStateMutationCapability capability)
    {
        RequireStateCapability(capability);
        if (stateMutationScopeDepth <= 0)
            throw new InvalidOperationException("Scheduler mutation scope underflow.");
        stateMutationScopeDepth--;
    }

    private void RequireDirectMutationAccess()
    {
        if (stateMutationCapability != null && stateMutationScopeDepth <= 0)
        {
            throw new InvalidOperationException(
                "An attached scheduler can mutate only through its owning " +
                "battle state.");
        }
    }

    private void RequireStateCapability(
        DRPGBattleStateMutationCapability capability)
    {
        if (capability == null || stateMutationCapability == null ||
            !ReferenceEquals(capability, stateMutationCapability))
        {
            throw new InvalidOperationException(
                "The scheduler mutation capability is missing or stale.");
        }
    }

    private float NormalizeTimelineDelay(
        BattleParticipantRuntime actor,
        float authoredDelay)
    {
        float opposingMedian = actor.Side == DRPGBattleSide.Player
            ? enemyMedianSpeed
            : playerMedianSpeed;
        float speedMultiplier = DRPGRelativeSpeedPolicy.ResolveDelayMultiplier(
            actor.EffectiveAgility,
            opposingMedian);
        return Math.Max(
            0.001f,
            authoredDelay / TimelineReferenceSpeed * speedMultiplier);
    }

    private static int CompareEntries(
        DRPGTurnScheduleEntry left,
        DRPGTurnScheduleEntry right)
    {
        int result = left.ScheduledTime.CompareTo(right.ScheduledTime);
        if (result != 0)
            return result;

        result = left.Priority.CompareTo(right.Priority);
        if (result != 0)
            return result;

        result = left.Participant.FormationSlot.CompareTo(
            right.Participant.FormationSlot);
        if (result != 0)
            return result;

        return left.Participant.RuntimeId.CompareTo(
            right.Participant.RuntimeId);
    }
}
