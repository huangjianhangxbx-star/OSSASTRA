using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

internal enum DRPGLedgerDisposition
{
    Presentable = 0,
    RuleOnly = 1,
    SkippedDiagnostic = 2
}

internal enum DRPGPrimaryImpactCommitStatus
{
    Committed = 0,
    AlreadyCommitted = 1,
    FingerprintConflict = 2,
    Rejected = 3
}

internal readonly struct DRPGPrimaryImpactKey :
    IEquatable<DRPGPrimaryImpactKey>
{
    internal DRPGPrimaryImpactKey(
        ActionIdentity identity,
        int impactOrdinal)
    {
        if (!identity.IsValid)
            throw new ArgumentException("Action identity is required.", nameof(identity));
        if (impactOrdinal < 0)
            throw new ArgumentOutOfRangeException(nameof(impactOrdinal));
        Identity = identity;
        ImpactOrdinal = impactOrdinal;
    }

    internal ActionIdentity Identity { get; }
    internal int ImpactOrdinal { get; }

    public bool Equals(DRPGPrimaryImpactKey other) =>
        Identity == other.Identity && ImpactOrdinal == other.ImpactOrdinal;

    public override bool Equals(object obj) =>
        obj is DRPGPrimaryImpactKey other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return (Identity.GetHashCode() * 397) ^ ImpactOrdinal;
        }
    }
}

internal sealed class DRPGPrimaryImpactEntry
{
    internal DRPGPrimaryImpactEntry(
        ActionIdentity identity,
        int impactOrdinal,
        string eventId,
        string parentEventId,
        DRPGHitOutcome outcome,
        DRPGBattleHitResult hit,
        IReadOnlyList<DRPGActionEffectResult> effects,
        ulong fingerprint)
    {
        if (!identity.IsValid)
            throw new ArgumentException("Action identity is required.", nameof(identity));
        if (impactOrdinal < 0)
            throw new ArgumentOutOfRangeException(nameof(impactOrdinal));
        if (string.IsNullOrWhiteSpace(eventId))
            throw new ArgumentException("Impact event identity is required.", nameof(eventId));
        if (string.IsNullOrWhiteSpace(parentEventId))
            throw new ArgumentException("Impact parent event identity is required.", nameof(parentEventId));
        if (!Enum.IsDefined(typeof(DRPGHitOutcome), outcome))
            throw new ArgumentOutOfRangeException(nameof(outcome));
        Identity = identity;
        ImpactOrdinal = impactOrdinal;
        EventId = eventId;
        ParentEventId = parentEventId;
        Outcome = outcome;
        Hit = hit ?? throw new ArgumentNullException(nameof(hit));
        Effects = Copy(effects);
        Fingerprint = fingerprint != 0UL
            ? fingerprint
            : throw new ArgumentOutOfRangeException(nameof(fingerprint));
    }

    internal ActionIdentity Identity { get; }
    internal int ImpactOrdinal { get; }
    internal string EventId { get; }
    internal string ParentEventId { get; }
    internal DRPGHitOutcome Outcome { get; }
    internal DRPGBattleHitResult Hit { get; }
    internal IReadOnlyList<DRPGActionEffectResult> Effects { get; }
    internal ulong Fingerprint { get; }
    internal DRPGLedgerDisposition Disposition =>
        DRPGLedgerDisposition.Presentable;
    internal bool RequiresPresentation => true;
    internal int DamageNumberCount => Outcome == DRPGHitOutcome.Evaded ? 0 : 1;
    internal int GenericHitCueCount => Outcome == DRPGHitOutcome.Evaded ? 0 : 1;
    internal bool RequiresRecoil => Outcome != DRPGHitOutcome.Evaded;
    internal bool RequiresFlash => Outcome != DRPGHitOutcome.Evaded;
    internal bool RequiresHitStop => Outcome != DRPGHitOutcome.Evaded;

    private static IReadOnlyList<DRPGActionEffectResult> Copy(
        IReadOnlyList<DRPGActionEffectResult> source)
    {
        if (source == null || source.Count == 0)
            return Array.Empty<DRPGActionEffectResult>();
        var copy = new DRPGActionEffectResult[source.Count];
        for (int i = 0; i < copy.Length; i++)
            copy[i] = source[i];
        return Array.AsReadOnly(copy);
    }
}

internal sealed class DRPGBattleActionResolutionLedger
{
    internal DRPGBattleActionResolutionLedger(
        ActionIdentity identity,
        in DRPGBasicAttackFrozenSnapshot frozenAction,
        DRPGPrimaryImpactEntry primaryImpact,
        IReadOnlyList<DRPGReactionLedgerEntry> reactions,
        DRPGBattleMutationJournal mutationJournal,
        ulong fingerprint)
    {
        if (!identity.IsValid)
            throw new ArgumentException("Action identity is required.", nameof(identity));
        if (frozenAction.Identity != identity)
            throw new ArgumentException("Frozen action identity does not match the ledger.");
        Identity = identity;
        FrozenAction = frozenAction;
        PrimaryImpact = primaryImpact ??
            throw new ArgumentNullException(nameof(primaryImpact));
        if (primaryImpact.Identity != identity)
            throw new ArgumentException("Primary impact belongs to another action.");
        Reactions = Copy(reactions);
        MutationJournal = mutationJournal ??
            throw new ArgumentNullException(nameof(mutationJournal));
        Fingerprint = fingerprint != 0UL
            ? fingerprint
            : throw new ArgumentOutOfRangeException(nameof(fingerprint));
        Events = DRPGCommittedActionEvent.Build(this);
        Facts = new DRPGCommittedActionFacts(this);
    }

    internal ActionIdentity Identity { get; }
    internal DRPGBasicAttackFrozenSnapshot FrozenAction { get; }
    internal DRPGPrimaryImpactEntry PrimaryImpact { get; }
    internal IReadOnlyList<DRPGReactionLedgerEntry> Reactions { get; }
    internal DRPGBattleMutationJournal MutationJournal { get; }
    internal IReadOnlyList<DRPGCommittedActionEvent> Events { get; }
    internal DRPGCommittedActionFacts Facts { get; }
    internal ulong Fingerprint { get; }

    private static IReadOnlyList<DRPGReactionLedgerEntry> Copy(
        IReadOnlyList<DRPGReactionLedgerEntry> source)
    {
        if (source == null || source.Count == 0)
            return Array.Empty<DRPGReactionLedgerEntry>();
        var copy = new DRPGReactionLedgerEntry[source.Count];
        for (int i = 0; i < copy.Length; i++)
            copy[i] = source[i];
        return Array.AsReadOnly(copy);
    }
}

internal sealed class DRPGPrimaryImpactCommitReceipt
{
    internal DRPGPrimaryImpactCommitReceipt(
        DRPGBattleActionResolutionLedger ledger,
        long committedRevision,
        ulong requestFingerprint)
    {
        Ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        if (committedRevision <= ledger.MutationJournal.BaseRevision)
        {
            throw new ArgumentOutOfRangeException(nameof(committedRevision));
        }
        CommittedRevision = committedRevision;
        RequestFingerprint = requestFingerprint != 0UL
            ? requestFingerprint
            : throw new ArgumentOutOfRangeException(nameof(requestFingerprint));
    }

    internal DRPGBattleActionResolutionLedger Ledger { get; }
    internal long CommittedRevision { get; }
    internal ulong RequestFingerprint { get; }
}

internal readonly struct DRPGPrimaryImpactCommitResult
{
    private DRPGPrimaryImpactCommitResult(
        DRPGPrimaryImpactCommitStatus status,
        DRPGPrimaryImpactCommitReceipt receipt,
        string error)
    {
        Status = status;
        Receipt = receipt;
        Error = error ?? string.Empty;
    }

    internal DRPGPrimaryImpactCommitStatus Status { get; }
    internal DRPGPrimaryImpactCommitReceipt Receipt { get; }
    internal string Error { get; }

    internal static DRPGPrimaryImpactCommitResult Committed(
        DRPGPrimaryImpactCommitReceipt receipt) =>
        new DRPGPrimaryImpactCommitResult(
            DRPGPrimaryImpactCommitStatus.Committed,
            receipt,
            string.Empty);

    internal static DRPGPrimaryImpactCommitResult AlreadyCommitted(
        DRPGPrimaryImpactCommitReceipt receipt) =>
        new DRPGPrimaryImpactCommitResult(
            DRPGPrimaryImpactCommitStatus.AlreadyCommitted,
            receipt,
            string.Empty);

    internal static DRPGPrimaryImpactCommitResult Conflict(string error) =>
        new DRPGPrimaryImpactCommitResult(
            DRPGPrimaryImpactCommitStatus.FingerprintConflict,
            null,
            error);

    internal static DRPGPrimaryImpactCommitResult Rejected(string error) =>
        new DRPGPrimaryImpactCommitResult(
            DRPGPrimaryImpactCommitStatus.Rejected,
            null,
            error);
}

internal sealed partial class DRPGBattleActionResolutionLedgerStore
{
    private readonly object commitGate = new object();
    private long conflictSequence;
    private readonly DRPGBattleSessionState state;
    private readonly DRPGBattleActionTraceWriter trace;
    private readonly Dictionary<
        DRPGPrimaryImpactKey,
        DRPGPrimaryImpactCommitReceipt> committed =
            new Dictionary<DRPGPrimaryImpactKey, DRPGPrimaryImpactCommitReceipt>();

    internal DRPGBattleActionResolutionLedgerStore(
        DRPGBattleSessionState state,
        DRPGBattleActionTraceWriter trace)
    {
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        this.trace = trace ?? throw new ArgumentNullException(nameof(trace));
    }

    internal DRPGPrimaryImpactCommitResult TryCommitPrimaryImpact(
        ActionIdentity identity,
        int impactOrdinal,
        string parentEventId,
        in DRPGBasicAttackFrozenSnapshot frozenAction,
        DRPGWorkspaceResolutionResult resolution,
        DRPGBattleMutationWorkspace workspace,
        BattleRuleMutationLease lease,
        DRPGActionResourceTransaction resources = null)
    {
        if (!identity.IsValid || identity.BattleGeneration != state.BattleGeneration)
            return DRPGPrimaryImpactCommitResult.Rejected(
                "The primary impact belongs to another battle.");
        if (impactOrdinal != 0)
            return DRPGPrimaryImpactCommitResult.Rejected(
                "A standard basic attack has only primary impact ordinal zero.");
        if (string.IsNullOrWhiteSpace(parentEventId))
            return DRPGPrimaryImpactCommitResult.Rejected(
                "The primary impact requires its parent event identity.");
        BattleTransactionIdentity owner =
            BattleTransactionIdentity.ForRootAction(identity);
        if (lease == null || !lease.Arbiter.Owns(lease, owner))
            return DRPGPrimaryImpactCommitResult.Rejected(
                "The Root Action mutation lease is required.");
        if (workspace == null ||
            workspace.Snapshot.BattleGeneration != identity.BattleGeneration)
        {
            return DRPGPrimaryImpactCommitResult.Rejected(
                "The primary-impact workspace belongs to another battle.");
        }
        if (resolution.Status != DRPGWorkspaceResolutionStatus.Succeeded ||
            resolution.Batch == null ||
            resolution.Batch.Hits.Count != 1)
        {
            return DRPGPrimaryImpactCommitResult.Rejected(
                string.IsNullOrEmpty(resolution.Error)
                    ? "A primary impact requires one resolved hit outcome."
                    : resolution.Error);
        }

        DRPGBattleHitResult hit = resolution.Batch.Hits[0];
        if (!ValidateFrozenRequest(
                identity,
                frozenAction,
                hit,
                resolution.Batch.Effects,
                resources,
                out string requestError))
        {
            return DRPGPrimaryImpactCommitResult.Rejected(requestError);
        }

        var key = new DRPGPrimaryImpactKey(identity, impactOrdinal);
        if (resources != null &&
            !resources.TryBindPrimaryImpact(key, out string bindError))
        {
            return DRPGPrimaryImpactCommitResult.Rejected(bindError);
        }
        ulong requestFingerprint =
            DRPGActionResolutionFingerprint.ComputeRequest(
                key,
                parentEventId,
                frozenAction,
                hit,
                resolution.Batch.Effects,
                resources);

        string eventId = string.Format(
            CultureInfo.InvariantCulture,
            "action-{0}-{1}-{2}-impact-{3}",
            identity.BattleGeneration,
            identity.TurnSerial,
            identity.ActionId,
            impactOrdinal);
        lock (commitGate)
        {
            if (committed.TryGetValue(
                    key,
                    out DRPGPrimaryImpactCommitReceipt existing))
            {
                if (existing.RequestFingerprint == requestFingerprint)
                {
                    DRPGBattleMutationJournal replayJournal;
                    try
                    {
                        replayJournal = workspace.BuildValidatedJournal();
                    }
                    catch (Exception exception)
                    {
                        return DRPGPrimaryImpactCommitResult.Rejected(exception.Message);
                    }
                    // An empty workspace is a receipt lookup after commit. A supplied
                    // journal must describe the original commit, never a new mutation.
                    if (replayJournal.DirtySet == DRPGBattleMutationDirtySet.None)
                        return DRPGPrimaryImpactCommitResult.AlreadyCommitted(existing);
                    ulong replayFingerprint = DRPGActionResolutionFingerprint.ComputeLedger(
                        key, parentEventId, frozenAction, hit, resolution.Batch.Effects,
                        replayJournal, resources != null ? resources.Receipt : null);
                    if (existing.Ledger.Fingerprint == replayFingerprint)
                        return DRPGPrimaryImpactCommitResult.AlreadyCommitted(existing);
                    return TraceFingerprintConflict(owner, impactOrdinal, eventId,
                        parentEventId, replayFingerprint);
                }
                return TraceFingerprintConflict(
                    owner,
                    impactOrdinal,
                    eventId,
                    parentEventId,
                    requestFingerprint);
            }

            DRPGActionResourceReceipt stagedResources = null;
            if (hit.Outcome != DRPGHitOutcome.Evaded &&
                !Near(hit.DamageBreakdown.BaseMagnitude,
                    Math.Max(0f, frozenAction.Effects[0].ResolvedMagnitude)))
            {
                return DRPGPrimaryImpactCommitResult.Rejected(
                    "The primary impact magnitude does not match its frozen action.");
            }
            if (resources != null &&
                !resources.TryStageIntoWorkspace(
                    key,
                    workspace,
                    out stagedResources,
                    out string resourceError))
            {
                return DRPGPrimaryImpactCommitResult.Rejected(resourceError);
            }

            DRPGBattleMutationJournal journal;
            try
            {
                journal = workspace.BuildValidatedJournal();
            }
            catch (Exception exception)
            {
                resources?.ReleaseWorkspaceStage(key, workspace);
                return DRPGPrimaryImpactCommitResult.Rejected(exception.Message);
            }
            if (!ValidateJournalMatchesResolution(
                    frozenAction,
                    hit,
                    resolution.Batch.Effects,
                    journal,
                    workspace.Snapshot,
                    stagedResources,
                    out string journalError))
            {
                resources?.ReleaseWorkspaceStage(key, workspace);
                return DRPGPrimaryImpactCommitResult.Rejected(journalError);
            }

            ulong fingerprint =
                DRPGActionResolutionFingerprint.ComputeLedger(
                    key,
                    parentEventId,
                    frozenAction,
                    hit,
                    resolution.Batch.Effects,
                    journal,
                    stagedResources);
            DRPGPrimaryImpactEntry entry;
            DRPGBattleActionResolutionLedger ledger;
            try
            {
                entry = new DRPGPrimaryImpactEntry(
                    identity,
                    impactOrdinal,
                    eventId,
                    parentEventId,
                    hit.Outcome,
                    hit,
                    resolution.Batch.Effects,
                    fingerprint);
                ledger = new DRPGBattleActionResolutionLedger(
                    identity,
                    frozenAction,
                    entry,
                    Array.Empty<DRPGReactionLedgerEntry>(),
                    journal,
                    fingerprint);
            }
            catch (Exception exception)
            {
                resources?.ReleaseWorkspaceStage(key, workspace);
                return DRPGPrimaryImpactCommitResult.Rejected(exception.Message);
            }

            BattleJournalCommitResult commit =
                state.TryCommitJournal(lease, workspace);
            if (!commit.Succeeded)
            {
                resources?.ReleaseWorkspaceStage(key, workspace);
                return DRPGPrimaryImpactCommitResult.Rejected(commit.Error);
            }

            resources?.CompleteWorkspaceCommit(key, workspace, stagedResources);
            var receipt = new DRPGPrimaryImpactCommitReceipt(
                ledger,
                commit.CommittedRevision,
                requestFingerprint);
            committed.Add(key, receipt);
            bool targetWasDeployed = true;
            if (workspace.Snapshot.TryGetParticipant(
                    new ParticipantId(hit.TargetRuntimeId),
                    out BattleParticipantRuleSnapshot targetSnapshot))
            {
                targetWasDeployed = targetSnapshot.IsDeployed;
            }
            trace.Record(
                0d,
                0d,
                ActionTraceIdentity.ForTransaction(owner),
                ActionTraceEventKind.ImpactCommitted,
                DRPGBattleActionStage.ImpactAndReactions,
                impactOrdinal,
                eventId,
                parentEventId,
                new ActionTraceContext(
                    actor: new ParticipantId(hit.ActorRuntimeId),
                    target: new ParticipantId(hit.TargetRuntimeId),
                    source: new ParticipantId(hit.ActorRuntimeId),
                    fingerprint: fingerprint.ToString(
                        "X16",
                        CultureInfo.InvariantCulture),
                    hitOutcome: ToTraceOutcome(hit.Outcome),
                    beforeProjection: new ActionTraceProjection(
                        ToTraceValue(hit.HpBefore),
                        ToTraceValue(hit.PostureBefore),
                        hit.HpBefore > 0f,
                        targetWasDeployed),
                    afterProjection: new ActionTraceProjection(
                        ToTraceValue(hit.HpAfter),
                        ToTraceValue(hit.PostureAfter),
                        hit.HpAfter > 0f,
                        targetWasDeployed),
                    leaseId: lease.LeaseSerial.ToString(
                        CultureInfo.InvariantCulture),
                    detail: hit.Outcome.ToString()));
            return DRPGPrimaryImpactCommitResult.Committed(receipt);
        }
    }

    private DRPGPrimaryImpactCommitResult TraceFingerprintConflict(
        BattleTransactionIdentity owner,
        int impactOrdinal,
        string eventId,
        string parentEventId,
        ulong fingerprint)
    {
        string conflict = "Primary-impact fingerprint conflict for " +
            eventId + ".";
        trace.Record(
            0d,
            0d,
            owner,
            ActionTraceEventKind.StaleCallbackDropped,
            DRPGBattleActionStage.ImpactAndReactions,
            impactOrdinal,
            eventId: eventId + "-conflict-" +
                (++conflictSequence).ToString(CultureInfo.InvariantCulture),
            parentEventId: parentEventId,
            fingerprint: fingerprint.ToString("X16", CultureInfo.InvariantCulture),
            detail: conflict);
        return DRPGPrimaryImpactCommitResult.Conflict(conflict);
    }

    private static bool ValidateFrozenRequest(
        ActionIdentity identity,
        in DRPGBasicAttackFrozenSnapshot frozen,
        DRPGBattleHitResult hit,
        IReadOnlyList<DRPGActionEffectResult> effects,
        DRPGActionResourceTransaction resources,
        out string error)
    {
        if (frozen.Identity != identity ||
            frozen.Turn.Actor.Value != hit.ActorRuntimeId ||
            frozen.Target.Value != hit.TargetRuntimeId ||
            !string.Equals(frozen.SkillId, hit.SkillId, StringComparison.Ordinal))
        {
            error = "The primary impact does not match its frozen action snapshot.";
            return false;
        }
        if (!frozen.ActorWasAlive || !frozen.TargetWasAlive ||
            !frozen.TargetWasDeployed || !frozen.TargetWasLegal ||
            frozen.Turn.Actor == frozen.Target ||
            !Contains(frozen.ProjectionCoverage, frozen.Turn.Actor) ||
            !Contains(frozen.ProjectionCoverage, frozen.Target))
        {
            error = "The frozen primary-impact participants are not legal or covered.";
            return false;
        }
        if (frozen.Effects.Count != 1 ||
            frozen.Effects[0].Kind != DRPGActionEffectKind.Damage ||
            frozen.Effects[0].Target != DRPGActionEffectTarget.PrimaryTarget)
        {
            error = "A standard primary impact requires one frozen target damage effect.";
            return false;
        }
        if (!Enum.IsDefined(typeof(DRPGHitOutcome), hit.Outcome))
        {
            error = "The primary impact has an invalid hit outcome.";
            return false;
        }
        if (!ValidateEffectResults(frozen, hit, effects, out error))
            return false;
        if (resources == null)
        {
            if (!Near(frozen.ManaCost, 0f) || !Near(frozen.PostureCost, 0f) ||
                frozen.StaminaCost != 0)
            {
                error = "The frozen action requires a matching resource reservation.";
                return false;
            }
        }
        else if (resources.ActorRuntimeId != hit.ActorRuntimeId ||
                 !Near(resources.ManaCost, frozen.ManaCost) ||
                 !Near(resources.PostureCost, frozen.PostureCost) ||
                 resources.StaminaCost != frozen.StaminaCost)
        {
            error = "The action resource reservation does not match the frozen action.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidateEffectResults(
        in DRPGBasicAttackFrozenSnapshot frozen,
        DRPGBattleHitResult hit,
        IReadOnlyList<DRPGActionEffectResult> effects,
        out string error)
    {
        if (hit.Outcome == DRPGHitOutcome.Evaded)
        {
            if (!Near(hit.AppliedDamage, 0f) ||
                !Near(hit.AppliedPostureDamage, 0f) ||
                !Near(hit.HpAfter, hit.HpBefore) ||
                !Near(hit.PostureAfter, hit.PostureBefore) ||
                !Near(hit.ShieldAbsorbed, 0f) ||
                hit.WasDefeated || hit.TriggeredDamageBroken ||
                hit.DamageBreakdown != null ||
                (effects != null && effects.Count != 0))
            {
                error = "An Evaded outcome cannot carry damage, defeat, break, " +
                    "shield, breakdown, or effect results.";
                return false;
            }
            error = string.Empty;
            return true;
        }

        if (hit.DamageBreakdown == null ||
            !Near(hit.DamageBreakdown.ActorMultiplier,
                frozen.ActorDamageMultiplier) ||
            hit.DamageBreakdown.Tags != frozen.Effects[0].DamageTags)
        {
            error = "The resolved damage does not match the frozen action values.";
            return false;
        }

        int damageCount = 0;
        int postureCount = 0;
        int count = effects != null ? effects.Count : 0;
        for (int i = 0; i < count; i++)
        {
            DRPGActionEffectResult effect = effects[i];
            if (effect.State != DRPGActionEffectResultState.Applied ||
                effect.ActorRuntimeId != hit.ActorRuntimeId ||
                effect.TargetRuntimeId != hit.TargetRuntimeId)
            {
                error = "The primary-impact effect result belongs to another request.";
                return false;
            }
            if (effect.Kind == DRPGActionEffectKind.Damage)
            {
                damageCount++;
                if (!Near(effect.RequestedAmount,
                        hit.DamageBreakdown.RequestedDamage) ||
                    !Near(effect.AppliedAmount, hit.AppliedDamage) ||
                    !Near(effect.AuxiliaryAmount, hit.ShieldAbsorbed) ||
                    effect.DamageTags != frozen.Effects[0].DamageTags ||
                    !string.Equals(
                        effect.SemanticId,
                        frozen.SkillId,
                        StringComparison.Ordinal))
                {
                    error = "The damage effect result does not match its hit outcome.";
                    return false;
                }
            }
            else if (effect.Kind == DRPGActionEffectKind.PostureDamage)
            {
                postureCount++;
                if (!Near(effect.AppliedAmount, hit.AppliedPostureDamage) ||
                    !Near(effect.PostureBefore, hit.PostureBefore) ||
                    !Near(effect.PostureAfter, hit.PostureAfter))
                {
                    error = "The posture effect result does not match its hit outcome.";
                    return false;
                }
            }
            else
            {
                error = "The narrow primary-impact ledger received a non-impact effect.";
                return false;
            }
        }
        if (damageCount != 1 || postureCount > 1)
        {
            error = "A primary impact requires one damage result and at most one " +
                "impact-posture result.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidateJournalMatchesResolution(
        in DRPGBasicAttackFrozenSnapshot frozen,
        DRPGBattleHitResult hit,
        IReadOnlyList<DRPGActionEffectResult> effects,
        DRPGBattleMutationJournal journal,
        BattleRuleSnapshot snapshot,
        DRPGActionResourceReceipt resources,
        out string error)
    {
        if (!snapshot.TryGetParticipant(
                new ParticipantId(hit.ActorRuntimeId),
                out BattleParticipantRuleSnapshot actorBefore) ||
            !snapshot.TryGetParticipant(
                new ParticipantId(hit.TargetRuntimeId),
                out BattleParticipantRuleSnapshot targetBefore))
        {
            error = "The impact participants are absent from the captured workspace.";
            return false;
        }
        if (!Near(hit.HpBefore, targetBefore.CurrentHp) ||
            !Near(hit.PostureBefore, targetBefore.CurrentPosture))
        {
            error = "The hit before-state does not match the workspace snapshot.";
            return false;
        }
        if (journal.TimelineMutations.Count != 0 ||
            journal.ResourceMutations.Count != 0 ||
            (journal.ActionResources != null && journal.ActionResources.HasChanges))
        {
            error = "A primary-only impact cannot include timeline or battle-resource writes; these require the complete action ledger.";
            return false;
        }

        bool hasTarget = TryFind(
            journal.ParticipantMutations,
            hit.TargetRuntimeId,
            out DRPGParticipantMutationRecord targetMutation);
        if (hit.Outcome == DRPGHitOutcome.Evaded)
        {
            if (hasTarget && targetMutation.DirtySet !=
                DRPGBattleMutationDirtySet.None)
            {
                error = "An Evaded outcome cannot mutate its target journal.";
                return false;
            }
        }
        else
        {
            DRPGBattleMutationDirtySet allowedTarget =
                DRPGBattleMutationDirtySet.HitPoints |
                DRPGBattleMutationDirtySet.Posture |
                DRPGBattleMutationDirtySet.Shield |
                DRPGBattleMutationDirtySet.PostureLifecycle;
            if (!hasTarget ||
                (targetMutation.DirtySet & ~allowedTarget) != 0 ||
                !MatchesDerivedPostureState(targetBefore, targetMutation) ||
                !Near(targetMutation.HitPoints, hit.HpAfter) ||
                !Near(targetMutation.Posture, hit.PostureAfter) ||
                !Near(targetBefore.CurrentHp - targetMutation.HitPoints,
                    hit.AppliedDamage) ||
                !Near(targetBefore.CurrentPosture - targetMutation.Posture,
                    hit.AppliedPostureDamage) ||
                !Near(targetBefore.CurrentShield - targetMutation.Shield,
                    hit.ShieldAbsorbed) ||
                hit.WasDefeated != (targetMutation.HitPoints <= 0f))
            {
                error = "The primary-impact target journal does not match its hit outcome.";
                return false;
            }
        }

        bool hasActor = TryFind(
            journal.ParticipantMutations,
            hit.ActorRuntimeId,
            out DRPGParticipantMutationRecord actorMutation);
        if (resources == null)
        {
            if (hasActor && actorMutation.DirtySet !=
                DRPGBattleMutationDirtySet.None)
            {
                error = "The primary impact contains an unreserved actor mutation.";
                return false;
            }
        }
        else
        {
            DRPGBattleMutationDirtySet allowedActor =
                DRPGBattleMutationDirtySet.Mana |
                DRPGBattleMutationDirtySet.Posture |
                DRPGBattleMutationDirtySet.PostureLifecycle |
                DRPGBattleMutationDirtySet.Stamina;
            if (resources.ActorRuntimeId != hit.ActorRuntimeId ||
                !Near(resources.SpentMana, frozen.ManaCost) ||
                !Near(resources.SpentPosture, frozen.PostureCost) ||
                resources.SpentStamina != frozen.StaminaCost ||
                ((resources.SpentMana > 0f || resources.SpentPosture > 0f ||
                  resources.SpentStamina > 0) &&
                  !hasActor) ||
                (hasActor &&
                 ((actorMutation.DirtySet & ~allowedActor) != 0 ||
                  !MatchesDerivedPostureState(actorBefore, actorMutation) ||
                  !Near(actorMutation.Mana,
                      actorBefore.CurrentMp - resources.SpentMana) ||
                   !Near(actorMutation.Posture,
                       actorBefore.CurrentPosture - resources.SpentPosture) ||
                   actorMutation.Stamina.Available !=
                       actorBefore.Stamina.Available - resources.SpentStamina)))
            {
                error = "The actor resource journal does not match its reservation.";
                return false;
            }
        }

        for (int i = 0; i < journal.ParticipantMutations.Count; i++)
        {
            int participant = journal.ParticipantMutations[i].Participant.Value;
            if (participant != hit.ActorRuntimeId &&
                participant != hit.TargetRuntimeId)
            {
                error = "The primary impact journal contains an unrelated participant.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static bool MatchesDerivedPostureState(BattleParticipantRuleSnapshot before, DRPGParticipantMutationRecord after)
    {
        var expected = new ParticipantMutationBuilder(before);
        expected.SetPosture(after.Posture);
        return expected.PostureState.Same(after.PostureState) && expected.IsDefending == after.IsDefending &&
            (expected.DirtySet & DRPGBattleMutationDirtySet.PostureLifecycle) == (after.DirtySet & DRPGBattleMutationDirtySet.PostureLifecycle);
    }

    private static bool TryFind(
        IReadOnlyList<DRPGParticipantMutationRecord> mutations,
        int runtimeId,
        out DRPGParticipantMutationRecord result)
    {
        for (int i = 0; i < mutations.Count; i++)
        {
            if (mutations[i].Participant.Value == runtimeId)
            {
                result = mutations[i];
                return true;
            }
        }
        result = default;
        return false;
    }

    private static bool Contains(
        IReadOnlyList<ParticipantId> participants,
        ParticipantId participant)
    {
        for (int i = 0; i < participants.Count; i++)
        {
            if (participants[i] == participant)
                return true;
        }
        return false;
    }

    private static bool Near(float left, float right) =>
        Math.Abs(left - right) <= 0.0001f;

    private static ActionTraceHitOutcome ToTraceOutcome(DRPGHitOutcome outcome)
    {
        switch (outcome)
        {
            case DRPGHitOutcome.Guarded:
                return ActionTraceHitOutcome.Guarded;
            case DRPGHitOutcome.Evaded:
                return ActionTraceHitOutcome.Evaded;
            default:
                return ActionTraceHitOutcome.Hit;
        }
    }

    private static int ToTraceValue(float value)
    {
        return (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }
}

internal static partial class DRPGActionResolutionFingerprint
{
    private const ulong Offset = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    internal static ulong ComputeRequest(
        DRPGPrimaryImpactKey key,
        string parentEventId,
        in DRPGBasicAttackFrozenSnapshot frozen,
        DRPGBattleHitResult hit,
        IReadOnlyList<DRPGActionEffectResult> effects,
        DRPGActionResourceTransaction resources)
    {
        var builder = new StringBuilder(2048);
        Append(builder, "primary-impact-request-v2");
        AppendKey(builder, key);
        Append(builder, parentEventId);
        AppendFrozenAction(builder, frozen);
        AppendHit(builder, hit);
        AppendEffects(builder, effects);
        Append(builder, resources != null ? 1 : 0);
        if (resources != null)
        {
            Append(builder, resources.ActorRuntimeId);
            Append(builder, resources.ManaCost);
            Append(builder, resources.PostureCost);
        }
        return Hash(builder);
    }

    internal static ulong ComputeLedger(
        DRPGPrimaryImpactKey key,
        string parentEventId,
        in DRPGBasicAttackFrozenSnapshot frozen,
        DRPGBattleHitResult hit,
        IReadOnlyList<DRPGActionEffectResult> effects,
        DRPGBattleMutationJournal journal,
        DRPGActionResourceReceipt resources)
    {
        var builder = new StringBuilder(3072);
        Append(builder, "primary-impact-ledger-v2");
        AppendKey(builder, key);
        Append(builder, parentEventId);
        AppendFrozenAction(builder, frozen);
        AppendHit(builder, hit);
        AppendEffects(builder, effects);
        AppendJournal(builder, journal);
        Append(builder, resources != null ? 1 : 0);
        if (resources != null)
        {
            Append(builder, resources.ActorRuntimeId);
            Append(builder, resources.SpentMana);
            Append(builder, resources.SpentPosture);
        }
        return Hash(builder);
    }

    private static void AppendKey(
        StringBuilder builder,
        DRPGPrimaryImpactKey key)
    {
        Append(builder, key.Identity.BattleGeneration);
        Append(builder, key.Identity.TurnSerial);
        Append(builder, key.Identity.ActionId);
        Append(builder, key.ImpactOrdinal);
    }

    private static void AppendFrozenAction(
        StringBuilder builder,
        in DRPGBasicAttackFrozenSnapshot frozen)
    {
        Append(builder, frozen.Identity.BattleGeneration);
        Append(builder, frozen.Identity.TurnSerial);
        Append(builder, frozen.Identity.ActionId);
        Append(builder, frozen.Turn.BattleGeneration);
        Append(builder, frozen.Turn.TurnSerial);
        Append(builder, frozen.Turn.Actor.Value);
        Append(builder, frozen.Selection.BattleGeneration);
        Append(builder, frozen.Selection.TurnSerial);
        Append(builder, frozen.Selection.SelectionId);
        Append(builder, frozen.Selection.Actor.Value);
        Append(builder, frozen.Target.Value);
        Append(builder, (int)frozen.ActorSide);
        Append(builder, (int)frozen.TargetSide);
        AppendPosition(builder, frozen.ActorHomePosition);
        AppendPosition(builder, frozen.ActorConflictPosition);
        AppendPosition(builder, frozen.TargetHomePosition);
        AppendPosition(builder, frozen.TargetConflictPosition);
        Append(builder, frozen.ActorFacingSign);
        Append(builder, frozen.TargetFacingSign);
        AppendVisualBaseline(builder, frozen.ActorVisualBaseline);
        AppendVisualBaseline(builder, frozen.TargetVisualBaseline);
        AppendTiming(builder, frozen.Timing);
        AppendSemantics(builder, frozen.Semantics);
        Append(builder, frozen.ProjectionCoverage != null
            ? frozen.ProjectionCoverage.Count
            : 0);
        if (frozen.ProjectionCoverage != null)
        {
            for (int i = 0; i < frozen.ProjectionCoverage.Count; i++)
                Append(builder, frozen.ProjectionCoverage[i].Value);
        }
        Append(builder, frozen.ActorWasAlive ? 1 : 0);
        Append(builder, frozen.TargetWasAlive ? 1 : 0);
        Append(builder, frozen.TargetWasDeployed ? 1 : 0);
        Append(builder, frozen.TargetWasLegal ? 1 : 0);
        Append(builder, frozen.SkillId);
        Append(builder, frozen.DisplayName);
        Append(builder, frozen.Description);
        Append(builder, frozen.PresentationKey);
        Append(builder, frozen.SkillPower);
        Append(builder, frozen.PrepareDelay);
        Append(builder, frozen.RecoveryDelay);
        Append(builder, (int)frozen.AnimationIntent);
        Append(builder, (int)frozen.SourceFormationRows);
        Append(builder, (int)frozen.TargetFormationRows);
        Append(builder, (int)frozen.Purpose);
        Append(builder, (int)frozen.TargetRule);
        Append(builder, (int)frozen.ExecutionMode);
        Append(builder, frozen.ImpactDamageMultipliers != null
            ? frozen.ImpactDamageMultipliers.Count
            : 0);
        if (frozen.ImpactDamageMultipliers != null)
        {
            for (int i = 0; i < frozen.ImpactDamageMultipliers.Count; i++)
                Append(builder, frozen.ImpactDamageMultipliers[i]);
        }
        Append(builder, frozen.EnemyWarningSteps);
        Append(builder, (int)frozen.EnemyTargetDisclosureMode);
        Append(builder, (int)frozen.PostureBreakInterruptionPolicy);
        Append(builder, frozen.IgnorePostureBreakProtection ? 1 : 0);
        Append(builder, frozen.RequiresFunctionalWeapon ? 1 : 0);
        Append(builder, frozen.ManaCost);
        Append(builder, frozen.PostureCost);
        Append(builder, frozen.StaminaCost);
        Append(builder, frozen.ActorDamageMultiplier);
        Append(builder, frozen.ActorHealingMultiplier);
        AppendProgressCost(builder, frozen.ActionProgressCost);
        AppendProgressCost(builder, frozen.InputProgressCost);
        Append(builder, (int)frozen.SelfPostureDepletionPolicy);
        AppendFrozenEffects(builder, frozen.Effects);
        AppendContributions(builder, frozen.ContributionTrace);
    }

    private static void AppendPosition(
        StringBuilder builder,
        ActionTracePosition position)
    {
        Append(builder, position.IsSpecified ? 1 : 0);
        Append(builder, position.X);
        Append(builder, position.Y);
        Append(builder, position.Z);
    }

    private static void AppendVisualBaseline(
        StringBuilder builder,
        DRPGBasicAttackVisualBaseline baseline)
    {
        AppendPosition(builder, baseline.LocalScale);
        Append(builder, baseline.Red);
        Append(builder, baseline.Green);
        Append(builder, baseline.Blue);
        Append(builder, baseline.Alpha);
        Append(builder, baseline.PoseSemantic);
    }

    private static void AppendTiming(
        StringBuilder builder,
        DRPGBasicAttackTimingSnapshot timing)
    {
        Append(builder, timing.AnnouncementSeconds);
        Append(builder, timing.ConflictHoldSeconds);
        Append(builder, timing.WindupMinimumSeconds);
        Append(builder, timing.RecoilOutSeconds);
        Append(builder, timing.RecoilReturnSeconds);
        Append(builder, timing.ReturnSeconds);
        Append(builder, timing.SettlementItemSeconds);
        Append(builder, timing.EmptySettlementSeconds);
    }

    private static void AppendSemantics(
        StringBuilder builder,
        DRPGBasicAttackSemanticSnapshot semantics)
    {
        Append(builder, semantics.ActionName);
        Append(builder, (int)semantics.AnimationIntent);
        Append(builder, semantics.PresentationKey);
        Append(builder, semantics.ImpactAudioSemantic);
        Append(builder, semantics.ImpactVfxSemantic);
    }

    private static void AppendProgressCost(
        StringBuilder builder,
        DRPGResolvedProgressCost cost)
    {
        Append(builder, cost != null ? 1 : 0);
        if (cost == null)
            return;
        Append(builder, cost.ResourceId);
        Append(builder, cost.Amount);
    }

    private static void AppendFrozenEffects(
        StringBuilder builder,
        IReadOnlyList<DRPGBasicAttackEffectSnapshot> effects)
    {
        Append(builder, effects != null ? effects.Count : 0);
        if (effects == null)
            return;
        for (int i = 0; i < effects.Count; i++)
        {
            DRPGBasicAttackEffectSnapshot effect = effects[i];
            Append(builder, (int)effect.Kind);
            Append(builder, (int)effect.Target);
            Append(builder, (int)effect.MagnitudeMode);
            Append(builder, effect.Magnitude);
            Append(builder, effect.ResolvedMagnitude);
            Append(builder, (int)effect.DamageTags);
            AppendStatusDefinition(builder, effect.Status);
            Append(builder, effect.StatusStacks);
            Append(builder, effect.StatusDurationOverride);
            Append(builder, effect.FormationRowDelta);
            Append(builder, effect.TimelineDelta);
            Append(builder, effect.TimelinePriorityDelta);
        }
    }

    private static void AppendStatusDefinition(
        StringBuilder builder,
        DRPGBasicAttackStatusDefinitionSnapshot status)
    {
        Append(builder, status.IsSpecified ? 1 : 0);
        Append(builder, status.StatusId);
        Append(builder, status.DisplayName);
        Append(builder, status.Description);
        Append(builder, status.IconResourceKey);
        Append(builder, status.DurationTurns);
        Append(builder, status.MaxStacks);
        Append(builder, (int)status.RefreshPolicy);
        Append(builder, (int)status.TriggerTiming);
        Append(builder, status.Priority);
        Append(builder, status.PersistsAfterSourceDefeat ? 1 : 0);
        Append(builder, status.OutgoingDamageMultiplier);
        Append(builder, status.IncomingDamageMultiplier);
        Append(builder, status.OutgoingHealingMultiplier);
        Append(builder, status.ManaCostMultiplier);
        Append(builder, status.PostureCostMultiplier);
        Append(builder, status.TurnStartHpDelta);
        Append(builder, status.TurnStartPostureDelta);
    }

    private static void AppendContributions(
        StringBuilder builder,
        IReadOnlyList<DRPGBasicAttackContributionSnapshot> contributions)
    {
        Append(builder, contributions != null ? contributions.Count : 0);
        if (contributions == null)
            return;
        for (int i = 0; i < contributions.Count; i++)
        {
            DRPGBasicAttackContributionSnapshot contribution = contributions[i];
            Append(builder, contribution.SourceId);
            Append(builder, (int)contribution.SourceKind);
            Append(builder, contribution.SourceSequence);
            Append(builder, contribution.WasApplied ? 1 : 0);
            Append(builder, contribution.SkipReason);
            Append(builder, contribution.BeforeSummary);
            Append(builder, contribution.AfterSummary);
        }
    }

    private static void AppendHit(StringBuilder builder, DRPGBattleHitResult hit)
    {
        Append(builder, (int)hit.Outcome);
        Append(builder, hit.ActorRuntimeId);
        Append(builder, hit.TargetRuntimeId);
        Append(builder, hit.SkillId);
        Append(builder, hit.HpBefore);
        Append(builder, hit.AppliedDamage);
        Append(builder, hit.HpAfter);
        Append(builder, hit.WasDefeated ? 1 : 0);
        Append(builder, hit.TriggeredDamageBroken ? 1 : 0);
        Append(builder, (int)hit.TargetRosterRole);
        Append(builder, hit.RedirectReason);
        Append(builder, hit.PostureBefore);
        Append(builder, hit.AppliedPostureDamage);
        Append(builder, hit.PostureAfter);
        Append(builder, (int)hit.GuardGrade);
        Append(builder, (int)hit.StrikeGrade);
        Append(builder, hit.ShieldAbsorbed);
        Append(builder, hit.WasPostureBreakExposed ? 1 : 0);
        Append(builder, hit.DamageBreakdown != null ? 1 : 0);
        if (hit.DamageBreakdown != null)
        {
            DRPGDamageBreakdown damage = hit.DamageBreakdown;
            Append(builder, damage.BaseMagnitude);
            Append(builder, damage.ActorMultiplier);
            Append(builder, damage.ImpactMultiplier);
            Append(builder, damage.AffinityMultiplier);
            Append(builder, damage.StatusMultiplier);
            Append(builder, damage.DefenseMultiplier);
            Append(builder, damage.RequestedDamage);
            Append(builder, damage.ShieldAbsorbed);
            Append(builder, damage.HpDamage);
            Append(builder, (int)damage.Tags);
        }
    }

    private static void AppendEffects(
        StringBuilder builder,
        IReadOnlyList<DRPGActionEffectResult> effects)
    {
        Append(builder, effects != null ? effects.Count : 0);
        if (effects == null)
            return;
        for (int i = 0; i < effects.Count; i++)
        {
            DRPGActionEffectResult effect = effects[i];
            Append(builder, (int)effect.Kind);
            Append(builder, (int)effect.State);
            Append(builder, effect.ActorRuntimeId);
            Append(builder, effect.TargetRuntimeId);
            Append(builder, effect.RequestedAmount);
            Append(builder, effect.AppliedAmount);
            Append(builder, effect.AuxiliaryAmount);
            Append(builder, effect.SemanticId);
            Append(builder, effect.Message);
            Append(builder, (int)effect.DamageTags);
            AppendNullableFormation(builder, effect.FormationBefore);
            AppendNullableFormation(builder, effect.FormationAfter);
            Append(builder, effect.TimelineBefore);
            Append(builder, effect.TimelineAfter);
            Append(builder, effect.PostureBefore);
            Append(builder, effect.PostureAfter);
            AppendStatusChange(builder, effect.StatusChange);
            AppendPostureChange(builder, effect.PostureChange);
        }
    }

    private static void AppendJournal(
        StringBuilder builder,
        DRPGBattleMutationJournal journal)
    {
        Append(builder, journal.BaseRevision);
        Append(builder, journal.LogicalTime.HasValue ? 1 : 0);
        if (journal.LogicalTime.HasValue) Append(builder, journal.LogicalTime.Value);
        Append(builder, (int)journal.DirtySet);
        var participants = new List<DRPGParticipantMutationRecord>(
            journal.ParticipantMutations);
        participants.Sort((left, right) =>
            left.Participant.Value.CompareTo(right.Participant.Value));
        Append(builder, participants.Count);
        for (int i = 0; i < participants.Count; i++)
        {
            DRPGParticipantMutationRecord mutation = participants[i];
            Append(builder, mutation.Participant.Value);
            Append(builder, mutation.HitPoints);
            Append(builder, mutation.Mana);
            Append(builder, mutation.Posture);
            Append(builder, mutation.Stamina.Capacity);
            Append(builder, mutation.Stamina.Current);
            Append(builder, mutation.Stamina.Pending);
            Append(builder, mutation.Shield);
            Append(builder, (int)mutation.FormationRow);
            Append(builder, mutation.IsDeployed ? 1 : 0);
            Append(builder, mutation.IsDefending ? 1 : 0);
            AppendPostureState(builder, mutation.PostureState);
            Append(builder, (int)mutation.DirtySet);
            Append(builder, mutation.Statuses.Count);
            for (int statusIndex = 0;
                 statusIndex < mutation.Statuses.Count;
                 statusIndex++)
            {
                DRPGStatusMutation status = mutation.Statuses[statusIndex];
                Append(builder, (int)status.Kind);
                Append(builder, status.StatusId);
                Append(builder, status.Stacks);
                Append(builder, status.Duration);
                Append(builder, status.SourceParticipantId.Value);
                Append(builder, status.AppliedBoundarySerial);
                AppendResolvedStatus(builder, status.Spec);
                AppendBoundaryMutation(builder, status);
            }
        }
        var timeline = new List<DRPGTimelineMutationRecord>(
            journal.TimelineMutations);
        timeline.Sort((left, right) =>
            left.Participant.Value.CompareTo(right.Participant.Value));
        Append(builder, timeline.Count);
        for (int i = 0; i < timeline.Count; i++)
        {
            Append(builder, timeline[i].Participant.Value);
            Append(builder, timeline[i].IsRemoval ? 1 : 0);
            Append(builder, timeline[i].ScheduledTime);
            Append(builder, timeline[i].Priority);
        }
        var resources = new List<BattleResourceRuleSnapshot>(
            journal.ResourceMutations);
        resources.Sort((left, right) => string.CompareOrdinal(
            left.ResourceId,
            right.ResourceId));
        Append(builder, resources.Count);
        for (int i = 0; i < resources.Count; i++)
        {
            Append(builder, resources[i].ResourceId);
            Append(builder, resources[i].Value);
        }
    }

    private static void AppendResolvedStatus(
        StringBuilder builder,
        DRPGResolvedStatusSpec status)
    {
        Append(builder, status != null ? 1 : 0);
        if (status == null)
            return;
        Append(builder, status.StatusId);
        Append(builder, status.DisplayName);
        Append(builder, status.IconName);
        Append(builder, status.IconInstanceId);
        Append(builder, status.DurationTurns);
        Append(builder, status.MaxStacks);
        Append(builder, (int)status.RefreshPolicy);
        Append(builder, (int)status.TriggerTiming);
        Append(builder, status.Priority);
        Append(builder, status.PersistsAfterSourceDefeat ? 1 : 0);
        Append(builder, status.OutgoingDamageMultiplier);
        Append(builder, status.IncomingDamageMultiplier);
        Append(builder, status.OutgoingHealingMultiplier);
        Append(builder, status.ManaCostMultiplier);
        Append(builder, status.PostureCostMultiplier);
        Append(builder, status.TurnStartHpDelta);
        Append(builder, status.TurnStartPostureDelta);
    }

    private static void AppendNullableFormation(
        StringBuilder builder,
        DRPGFormationRow? row)
    {
        Append(builder, row.HasValue ? 1 : 0);
        if (row.HasValue)
            Append(builder, (int)row.Value);
    }

    private static ulong Hash(StringBuilder builder)
    {
        ulong hash = Offset;
        byte[] bytes = Encoding.UTF8.GetBytes(builder.ToString());
        for (int i = 0; i < bytes.Length; i++)
        {
            hash ^= bytes[i];
            hash *= Prime;
        }
        return hash == 0UL ? Offset : hash;
    }

    private static void Append(StringBuilder builder, string value)
    {
        string text = value ?? string.Empty;
        builder.Append(text.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(text)
            .Append('|');
    }

    private static void Append(StringBuilder builder, int value) =>
        Append(builder, value.ToString(CultureInfo.InvariantCulture));

    private static void Append(StringBuilder builder, long value) =>
        Append(builder, value.ToString(CultureInfo.InvariantCulture));

    private static void Append(StringBuilder builder, float value)
    {
        string text;
        if (float.IsNaN(value))
            text = "NaN";
        else if (float.IsPositiveInfinity(value))
            text = "+Infinity";
        else if (float.IsNegativeInfinity(value))
            text = "-Infinity";
        else
            text = value.ToString("R", CultureInfo.InvariantCulture);
        Append(builder, text);
    }

    private static void Append(StringBuilder builder, double value)
    {
        string text;
        if (double.IsNaN(value))
            text = "NaN";
        else if (double.IsPositiveInfinity(value))
            text = "+Infinity";
        else if (double.IsNegativeInfinity(value))
            text = "-Infinity";
        else
            text = value.ToString("R", CultureInfo.InvariantCulture);
        Append(builder, text);
    }
}
