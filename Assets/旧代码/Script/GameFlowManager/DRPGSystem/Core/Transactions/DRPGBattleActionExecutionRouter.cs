using System;
using System.Collections.Generic;

internal readonly struct LegacyActionStartResult
{
    private LegacyActionStartResult(
        bool accepted,
        DRPGBattlePreparedAction action,
        string error)
    {
        IsAccepted = accepted;
        Action = action;
        Error = error ?? string.Empty;
    }

    internal bool IsAccepted { get; }
    internal DRPGBattlePreparedAction Action { get; }
    internal string Error { get; }

    internal static LegacyActionStartResult Accepted(
        DRPGBattlePreparedAction action)
    {
        return new LegacyActionStartResult(
            true,
            action ?? throw new ArgumentNullException(nameof(action)),
            string.Empty);
    }

    internal static LegacyActionStartResult Rejected(string error)
    {
        return new LegacyActionStartResult(false, null, error);
    }
}

internal interface IDRPGLegacyActionRunner
{
    LegacyActionStartResult TryStartLegacy(DRPGBattleCommand command);
}

internal delegate LegacyActionStartResult DRPGLegacyActionStartOperation(
    DRPGBattleCommand command);

internal sealed class DRPGLegacyActionRunnerAdapter : IDRPGLegacyActionRunner
{
    public LegacyActionStartResult TryStartLegacy(DRPGBattleCommand command)
    {
        return LegacyActionStartResult.Rejected(
            "A scoped Legacy action operation is required.");
    }

    internal LegacyActionStartResult TryStartLegacy(
        DRPGBattleCommand command,
        DRPGLegacyActionStartOperation operation)
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));
        return operation(command);
    }
}

internal delegate bool DRPGLegacyResolveAction(
    DRPGBattlePreparedAction action,
    out DRPGBattleActionResolution resolution,
    out string error);

internal delegate bool DRPGLegacyCommitImpactPartition(
    long actionId,
    int impactIndex,
    out DRPGBattleActionResolution resolution,
    out string error);

internal delegate bool DRPGLegacyAdvanceAction(
    DRPGBattlePreparedAction action,
    out string error);

internal sealed class DRPGLegacyActionMutationPort
{
    private readonly BattleRuleMutationLease lease;
    private readonly DRPGBattleSessionState state;
    private readonly DRPGLegacyResolveAction resolve;
    private readonly DRPGLegacyCommitImpactPartition commitPartition;
    private readonly DRPGLegacyAdvanceAction commit;
    private readonly DRPGLegacyAdvanceAction finalize;
    private bool continuationRetained;
    internal void RetainForContinuation()
    {
        if (!lease.Arbiter.Owns(lease, Owner)) throw new InvalidOperationException("Legacy continuation lost its lease.");
        continuationRetained = true;
    }

    internal DRPGLegacyActionMutationPort(
        ActionIdentity identity,
        BattleRuleMutationLease lease,
        DRPGBattleSessionState state,
        DRPGLegacyResolveAction resolve,
        DRPGLegacyCommitImpactPartition commitPartition,
        DRPGLegacyAdvanceAction commit,
        DRPGLegacyAdvanceAction finalize)
    {
        if (!identity.IsValid)
            throw new ArgumentException("Action identity is required.", nameof(identity));
        Identity = identity;
        this.lease = lease ?? throw new ArgumentNullException(nameof(lease));
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        this.resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
        this.commitPartition = commitPartition ??
            throw new ArgumentNullException(nameof(commitPartition));
        this.commit = commit ?? throw new ArgumentNullException(nameof(commit));
        this.finalize = finalize ?? throw new ArgumentNullException(nameof(finalize));
    }

    internal ActionIdentity Identity { get; }
    internal BattleTransactionIdentity Owner => lease.Owner;
    internal bool IsActive => lease.IsActive;
    internal long LeaseSerial => lease.LeaseSerial;

    internal bool TryResolve(
        DRPGBattlePreparedAction action,
        out DRPGBattleActionResolution resolution,
        out string error)
    {
        resolution = null;
        if (!Owns(action, out error))
            return false;
        using (state.OpenCompatibilityMutationScope(lease))
            return resolve(action, out resolution, out error);
    }

    internal bool TryCommitPartition(
        DRPGBattlePreparedAction action,
        int impactIndex,
        out DRPGBattleActionResolution resolution,
        out string error)
    {
        resolution = null;
        if (!Owns(action, out error))
            return false;
        using (state.OpenCompatibilityMutationScope(lease))
        {
            return commitPartition(
                action.ActionId,
                impactIndex,
                out resolution,
                out error);
        }
    }

    internal bool TryCommit(
        DRPGBattlePreparedAction action,
        out string error)
    {
        if (!Owns(action, out error))
            return false;
        using (state.OpenCompatibilityMutationScope(lease))
            return commit(action, out error);
    }

    internal bool TryFinalize(
        DRPGBattlePreparedAction action,
        out string error)
    {
        if (!Owns(action, out error))
            return false;
        bool completed;
        using (state.OpenCompatibilityMutationScope(lease))
            completed = finalize(action, out error);
        if (completed && !continuationRetained)
            lease.Dispose();
        return completed;
    }

    internal bool TryComplete(
        DRPGBattlePreparedAction action,
        out string error)
    {
        return TryCommit(action, out error) && TryFinalize(action, out error);
    }

    internal bool TryOpenScope(
        DRPGBattlePreparedAction action,
        out IDisposable scope,
        out string error)
    {
        scope = null;
        if (!Owns(action, out error))
            return false;
        scope = state.OpenCompatibilityMutationScope(lease);
        return true;
    }

    internal bool TryCommitTestMutation(
        DRPGBattlePreparedAction action,
        Action<DRPGBattleMutationWorkspace> configure,
        out BattleJournalCommitResult result,
        out string error)
    {
        result = default;
        if (!Owns(action, out error))
            return false;
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));
        DRPGBattleMutationWorkspace workspace = state.CreateWorkspace();
        configure(workspace);
        result = state.TryCommitJournal(lease, workspace);
        error = result.Error;
        return result.Succeeded;
    }

    internal void CancelBeforeActivation()
    {
        AbortAndRelease();
    }

    internal void AbortAndRelease()
    {
        lease.Dispose();
    }

    private bool Owns(DRPGBattlePreparedAction action, out string error)
    {
        if (action == null || action.Identity != Identity ||
            !ReferenceEquals(action.LegacyMutationPort, this) ||
            !lease.Arbiter.Owns(lease, Owner))
        {
            error = "The prepared action does not own this Legacy mutation port.";
            return false;
        }
        error = string.Empty;
        return true;
    }
}

internal interface IDRPGBasicAttackSelectionAuthority
{
    TargetSelectionOpenResult TryOpen(in ActorTurnToken turn);
    TargetSelectionCancelResult TryCancel(in TargetSelectionToken selection);
    bool TryFreeze(
        in BasicAttackSelection selection,
        out DRPGBasicAttackFrozenSnapshot snapshot,
        out BasicAttackRejection rejection);
    bool TryPromote(
        in BasicAttackSelection selection,
        Func<DRPGBasicAttackFrozenSnapshot, RootActionOwnershipResult> promote,
        out DRPGBasicAttackFrozenSnapshot snapshot,
        out RootActionOwnershipResult ownership,
        out DRPGBasicAttackTraceCorrelation traceCorrelation,
        out BasicAttackRejection rejection);
    bool TryConfirmLegacy(
        in BasicAttackSelection selection,
        out DRPGBasicAttackFrozenSnapshot snapshot,
        out DRPGBasicAttackTraceCorrelation traceCorrelation,
        out BasicAttackRejection rejection);
    void Restore(in TargetSelectionToken selection);
    void Consume(in TargetSelectionToken selection);
}

internal interface IDRPGConfiguredSkillSelectionAuthority
{
    bool TryPromoteConfigured(
        in DRPGSkillSelection selection,
        Func<DRPGBasicAttackFrozenSnapshot, RootActionOwnershipResult> promote,
        out DRPGBasicAttackFrozenSnapshot snapshot,
        out RootActionOwnershipResult ownership,
        out DRPGBasicAttackTraceCorrelation traceCorrelation,
        out BasicAttackRejection rejection);
}

internal interface IDRPGBasicAttackSelectionState
{
    ActorTurnToken CurrentTurn { get; }
    bool IsBattleEnded { get; }
    bool IsActionBusy { get; }
    bool TryValidateBasicAttackActor(
        in ActorTurnToken turn,
        out DRPGSkillSO basicAttack,
        out BasicAttackRejection rejection);
    bool TryResolveBasicAttackSpec(
        in ActorTurnToken turn,
        ParticipantId target,
        DRPGSkillSO basicAttack,
        out DRPGResolvedActionSpec resolvedSpec,
        out BasicAttackRejection rejection);
    bool TryFreezeBasicAttackSnapshot(
        in ActorTurnToken turn,
        in TargetSelectionToken selection,
        ParticipantId target,
        DRPGSkillSO basicAttack,
        DRPGResolvedActionSpec resolvedSpec,
        out DRPGBasicAttackFrozenSnapshot snapshot,
        out BasicAttackRejection rejection);
    bool TryGetBasicAttackTraceContext(
        in ActorTurnToken turn,
        out DRPGBattleSide side,
        out ActionTracePosition homePosition);
}

internal interface IDRPGConfiguredSkillSelectionState
{
    bool TryFreezeConfiguredSkillSnapshot(
        in DRPGSkillSelection selection,
        in TargetSelectionToken selectionToken,
        out DRPGBasicAttackFrozenSnapshot snapshot,
        out BasicAttackRejection rejection);
}

internal readonly struct DRPGBasicAttackTraceCorrelation
{
    internal DRPGBasicAttackTraceCorrelation(
        string turnEventId,
        string selectionOpenedEventId,
        string selectionConfirmedEventId)
    {
        TurnEventId = turnEventId ?? string.Empty;
        SelectionOpenedEventId = selectionOpenedEventId ?? string.Empty;
        SelectionConfirmedEventId = selectionConfirmedEventId ?? string.Empty;
    }

    internal string TurnEventId { get; }
    internal string SelectionOpenedEventId { get; }
    internal string SelectionConfirmedEventId { get; }
    internal bool IsValid =>
        !string.IsNullOrEmpty(TurnEventId) &&
        !string.IsNullOrEmpty(SelectionOpenedEventId) &&
        !string.IsNullOrEmpty(SelectionConfirmedEventId);
}

internal sealed class DRPGBasicAttackSelectionRuntimeState :
    IDRPGBasicAttackSelectionState,
    IDRPGConfiguredSkillSelectionState
{
    private readonly IReadOnlyList<BattleParticipantRuntime> participants;
    private readonly DRPGBattleTransactionArbiter arbiter;
    private readonly DRPGBattleProgressPipeline progressPipeline;
    private readonly DRPGBattleTransactionPresentationPortSlot presentationSlot;
    private readonly bool requireBoundPresentationSnapshot;
    private ActorTurnToken currentTurn;
    private bool battleAvailable;

    internal DRPGBasicAttackSelectionRuntimeState(
        IReadOnlyList<BattleParticipantRuntime> participants,
        DRPGBattleTransactionArbiter arbiter,
        DRPGBattleProgressPipeline progressPipeline,
        DRPGBattleTransactionPresentationPortSlot presentationSlot,
        bool requireBoundPresentationSnapshot)
    {
        this.participants = participants ??
            throw new ArgumentNullException(nameof(participants));
        this.arbiter = arbiter ?? throw new ArgumentNullException(nameof(arbiter));
        this.progressPipeline = progressPipeline ??
            throw new ArgumentNullException(nameof(progressPipeline));
        this.presentationSlot = presentationSlot ??
            throw new ArgumentNullException(nameof(presentationSlot));
        this.requireBoundPresentationSnapshot =
            requireBoundPresentationSnapshot;
    }

    public ActorTurnToken CurrentTurn => currentTurn;
    public bool IsBattleEnded => !battleAvailable;
    public bool IsActionBusy => arbiter.HasOwner;

    internal void SetBattleAvailable(bool available)
    {
        battleAvailable = available;
        if (!available)
            currentTurn = default;
    }

    internal void PublishTurn(in ActorTurnToken turn)
    {
        if (!turn.IsValid || turn.BattleGeneration != arbiter.BattleGeneration)
        {
            throw new ArgumentException(
                "The routing turn must belong to this battle.",
                nameof(turn));
        }
        BattleParticipantRuntime actor = Find(turn.Actor);
        if (actor == null || !actor.IsAlive || !actor.IsDeployed)
        {
            throw new InvalidOperationException(
                "The routing turn actor is not active in this battle.");
        }
        currentTurn = turn;
    }

    internal void ClearTurn()
    {
        currentTurn = default;
    }

    public bool TryValidateBasicAttackActor(
        in ActorTurnToken turn,
        out DRPGSkillSO basicAttack,
        out BasicAttackRejection rejection)
    {
        basicAttack = null;
        if (!battleAvailable)
        {
            rejection = BasicAttackRejection.BattleEnded;
            return false;
        }
        if (turn != currentTurn)
        {
            rejection = currentTurn.IsValid && turn.Actor != currentTurn.Actor
                ? BasicAttackRejection.NotCurrentActor
                : BasicAttackRejection.StaleTurnToken;
            return false;
        }
        if (arbiter.HasOwner)
        {
            rejection = BasicAttackRejection.Busy;
            return false;
        }
        BattleParticipantRuntime actor = Find(turn.Actor);
        basicAttack = actor != null && actor.Build != null
            ? actor.Build.BasicAttack
            : null;
        DRPGResolvedActionSpec resolvedSpec = actor != null && basicAttack != null
            ? DRPGActionSpecResolver.Resolve(
                DRPGBattleCommand.BasicAttack(
                    actor.RuntimeId,
                    actor.RuntimeId,
                    basicAttack),
                actor)
            : null;
        if (actor == null || !actor.IsAlive || !actor.IsDeployed ||
            basicAttack == null || resolvedSpec == null ||
            !resolvedSpec.SourceFormationRows.Contains(actor.FormationRow) ||
            !CanAfford(actor, resolvedSpec))
        {
            basicAttack = null;
            rejection = BasicAttackRejection.BasicAttackUnavailable;
            return false;
        }
        rejection = BasicAttackRejection.None;
        return true;
    }

    public bool TryResolveBasicAttackSpec(
        in ActorTurnToken turn,
        ParticipantId target,
        DRPGSkillSO basicAttack,
        out DRPGResolvedActionSpec resolvedSpec,
        out BasicAttackRejection rejection)
    {
        resolvedSpec = null;
        BattleParticipantRuntime actor = Find(turn.Actor);
        BattleParticipantRuntime defender = Find(target);
        if (turn != currentTurn || actor == null || defender == null ||
            basicAttack == null)
        {
            rejection = BasicAttackRejection.IllegalTarget;
            return false;
        }
        resolvedSpec = DRPGActionSpecResolver.Resolve(
            DRPGBattleCommand.BasicAttack(
                actor.RuntimeId,
                defender.RuntimeId,
                basicAttack),
            actor);
        if (!resolvedSpec.CanTarget(actor, defender))
        {
            rejection = BasicAttackRejection.IllegalTarget;
            return false;
        }
        if (!CanAfford(actor, resolvedSpec))
        {
            rejection = BasicAttackRejection.BasicAttackUnavailable;
            return false;
        }
        rejection = BasicAttackRejection.None;
        return true;
    }

    public bool TryFreezeBasicAttackSnapshot(
        in ActorTurnToken turn,
        in TargetSelectionToken selection,
        ParticipantId target,
        DRPGSkillSO basicAttack,
        DRPGResolvedActionSpec resolvedSpec,
        out DRPGBasicAttackFrozenSnapshot snapshot,
        out BasicAttackRejection rejection)
    {
        snapshot = default;
        BattleParticipantRuntime actor = Find(turn.Actor);
        BattleParticipantRuntime defender = Find(target);
        if (turn != currentTurn || actor == null || defender == null ||
            basicAttack == null || resolvedSpec == null ||
            resolvedSpec.CommandKind != DRPGBattleCommandKind.BasicAttack ||
            resolvedSpec.ActorRuntimeId != actor.RuntimeId ||
            resolvedSpec.TargetRuntimeId != defender.RuntimeId ||
            resolvedSpec.SkillId != basicAttack.SkillId ||
            !resolvedSpec.CanTarget(actor, defender) ||
            !CanAfford(actor, resolvedSpec))
        {
            rejection = BasicAttackRejection.IllegalTarget;
            return false;
        }
        DRPGBasicAttackPresentationFreezeInput presentation = default;
        var presentationRequest = new DRPGBasicAttackPresentationFreezeRequest(
            turn.BattleGeneration,
            turn.Actor,
            target,
            basicAttack.SkillId,
            basicAttack.PresentationKey,
            basicAttack.DisplayName);
        bool captured = presentationSlot.TryGet(
                turn.BattleGeneration,
                out IStandardBasicAttackPresentationPort presentationPort) &&
            presentationPort.TryCaptureFrozenSnapshot(
                presentationRequest,
                out presentation) &&
            presentation.IsValid;
        if (!captured)
        {
            if (requireBoundPresentationSnapshot)
            {
                rejection = BasicAttackRejection.PresentationUnavailable;
                return false;
            }
            presentation = CreateLegacyPresentationSnapshot(
                actor,
                defender,
                basicAttack);
        }
        var coverage = new List<ParticipantId>(participants.Count);
        for (int i = 0; i < participants.Count; i++)
        {
            BattleParticipantRuntime participant = participants[i];
            if (participant != null && participant.IsDeployed)
                coverage.Add(new ParticipantId(participant.RuntimeId));
        }
        snapshot = new DRPGBasicAttackFrozenSnapshot(
            turn,
            selection,
            target,
            basicAttack,
            actor.Side,
            defender.Side,
            presentation.ActorHomePosition,
            presentation.ActorConflictPosition,
            presentation.TargetHomePosition,
            presentation.TargetConflictPosition,
            presentation.ActorFacingSign,
            presentation.TargetFacingSign,
            presentation.ActorVisualBaseline,
            presentation.TargetVisualBaseline,
            presentation.Timing,
            presentation.Semantics,
            coverage,
            actor.IsAlive,
            defender.IsAlive,
            defender.IsDeployed,
            true,
            resolvedSpec,
            actor.Profile.DisplayName);
        rejection = BasicAttackRejection.None;
        return true;
    }

    public bool TryGetBasicAttackTraceContext(
        in ActorTurnToken turn,
        out DRPGBattleSide side,
        out ActionTracePosition homePosition)
    {
        BattleParticipantRuntime actor = Find(turn.Actor);
        if (turn != currentTurn || actor == null)
        {
            side = default;
            homePosition = default;
            return false;
        }
        side = actor.Side;
        homePosition = HomeOf(actor);
        return true;
    }

    public bool TryFreezeConfiguredSkillSnapshot(
        in DRPGSkillSelection request,
        in TargetSelectionToken selectionToken,
        out DRPGBasicAttackFrozenSnapshot snapshot,
        out BasicAttackRejection rejection)
    {
        snapshot = default;
        if (!battleAvailable || request.Turn != currentTurn ||
            !selectionToken.IsValid || selectionToken.Actor != request.Turn.Actor ||
            arbiter.HasOwner || !DRPGBattleSkillModePolicy.IsAllowed(request.Skill))
        {
            rejection = BasicAttackRejection.BasicAttackUnavailable;
            return false;
        }
        BattleParticipantRuntime actor = Find(request.Turn.Actor);
        BattleParticipantRuntime target = Find(
            new ParticipantId(request.PrimaryTargetRuntimeId));
        IReadOnlyList<DRPGSkillSO> configured = actor != null && actor.Build != null
            ? actor.Build.ConfiguredSkills
            : null;
        if (actor == null || target == null || !actor.IsAlive || !actor.IsDeployed ||
            !target.IsAlive || !target.IsDeployed || configured == null ||
            request.SlotIndex < 0 || request.SlotIndex >= configured.Count ||
            !ReferenceEquals(configured[request.SlotIndex], request.Skill))
        {
            rejection = BasicAttackRejection.IllegalTarget;
            return false;
        }
        DRPGResolvedActionSpec spec = DRPGActionSpecResolver.Resolve(
            DRPGBattleCommand.UseSkill(actor.RuntimeId, target.RuntimeId, request.Skill),
            actor);
        if (spec == null || !spec.CanTarget(actor, target) || !CanAfford(actor, spec))
        {
            rejection = BasicAttackRejection.IllegalTarget;
            return false;
        }
        DRPGBasicAttackPresentationFreezeInput presentation = default;
        var presentationRequest = new DRPGBasicAttackPresentationFreezeRequest(
            request.Turn.BattleGeneration,
            request.Turn.Actor,
            new ParticipantId(target.RuntimeId),
            request.Skill.SkillId,
            request.Skill.PresentationKey,
            request.Skill.DisplayName);
        bool captured = presentationSlot.TryGet(
                request.Turn.BattleGeneration,
                out IStandardBasicAttackPresentationPort presentationPort) &&
            presentationPort.TryCaptureFrozenSnapshot(
                presentationRequest, out presentation) && presentation.IsValid;
        if (!captured)
        {
            if (requireBoundPresentationSnapshot)
            {
                rejection = BasicAttackRejection.PresentationUnavailable;
                return false;
            }
            presentation = CreateLegacyPresentationSnapshot(actor, target, request.Skill);
        }
        var coverage = new List<ParticipantId>(participants.Count);
        for (int i = 0; i < participants.Count; i++)
            if (participants[i] != null && participants[i].IsDeployed)
                coverage.Add(new ParticipantId(participants[i].RuntimeId));
        snapshot = new DRPGBasicAttackFrozenSnapshot(
            request.Turn, selectionToken, new ParticipantId(target.RuntimeId),
            request.Skill, actor.Side, target.Side,
            presentation.ActorHomePosition, presentation.ActorConflictPosition,
            presentation.TargetHomePosition, presentation.TargetConflictPosition,
            presentation.ActorFacingSign, presentation.TargetFacingSign,
            presentation.ActorVisualBaseline, presentation.TargetVisualBaseline,
            presentation.Timing, presentation.Semantics, coverage,
            actor.IsAlive, target.IsAlive, target.IsDeployed, true, spec,
            actor.Profile.DisplayName);
        rejection = BasicAttackRejection.None;
        return true;
    }

    private bool CanAfford(
        BattleParticipantRuntime actor,
        DRPGResolvedActionSpec spec)
    {
        if (actor.CurrentMp + 0.0001f <
                spec.ManaCost ||
            actor.CurrentPosture + 0.0001f <
                spec.PostureCost ||
            actor.BattleStamina.Available < spec.StaminaCost)
        {
            return false;
        }
        if (actor.Profile == null || spec.ActionProgressCost == null)
            return true;
        return progressPipeline.CanSpend(
            actor.Profile.CombatantId,
            spec.ActionProgressCost,
            DRPGProgressScope.Battle,
            out _);
    }

    private BattleParticipantRuntime Find(ParticipantId id)
    {
        for (int i = 0; i < participants.Count; i++)
        {
            BattleParticipantRuntime participant = participants[i];
            if (participant != null && participant.RuntimeId == id.Value)
                return participant;
        }
        return null;
    }

    private static ActionTracePosition HomeOf(BattleParticipantRuntime actor)
    {
        return new ActionTracePosition(
            (float)actor.FormationRow,
            actor.FormationSlot,
            0f);
    }

    private static ActionTracePosition ConflictOf(BattleParticipantRuntime actor)
    {
        return new ActionTracePosition(
            actor.Side == DRPGBattleSide.Player ? -0.5f : 0.5f,
            0f,
            0f);
    }

    private static int FacingFor(DRPGBattleSide side)
    {
        return side == DRPGBattleSide.Player ? 1 : -1;
    }

    private static DRPGBasicAttackPresentationFreezeInput
        CreateLegacyPresentationSnapshot(
            BattleParticipantRuntime actor,
            BattleParticipantRuntime defender,
            DRPGSkillSO basicAttack)
    {
        string key = basicAttack.PresentationKey;
        return new DRPGBasicAttackPresentationFreezeInput(
            HomeOf(actor),
            ConflictOf(actor),
            HomeOf(defender),
            ConflictOf(defender),
            FacingFor(actor.Side),
            FacingFor(defender.Side),
            DRPGBasicAttackVisualBaseline.DefaultIdle,
            DRPGBasicAttackVisualBaseline.DefaultIdle,
            DRPGBasicAttackTimingSnapshot.ApprovedDefaults,
            new DRPGBasicAttackSemanticSnapshot(
                basicAttack.DisplayName,
                basicAttack.AnimationIntent,
                key,
                key + ".impact.audio",
                key + ".impact.vfx"));
    }
}

internal sealed class DRPGBasicAttackSelectionAuthority :
    IDRPGBasicAttackSelectionAuthority,
    IDRPGConfiguredSkillSelectionAuthority
{
    private readonly DRPGBattleIdentityAuthority identityAuthority;
    private readonly IDRPGBasicAttackSelectionState state;
    private readonly DRPGBattleActionTraceWriter trace;
    private readonly object selectionGate = new object();
    private TargetSelectionToken activeSelection;
    private DRPGSkillSO activeBasicAttack;
    private string activeSelectionOpenedEventId;
    private bool promotionInProgress;

    internal DRPGBasicAttackSelectionAuthority(
        DRPGBattleIdentityAuthority identityAuthority,
        IDRPGBasicAttackSelectionState state,
        DRPGBattleActionTraceWriter trace)
    {
        this.identityAuthority = identityAuthority ??
            throw new ArgumentNullException(nameof(identityAuthority));
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        this.trace = trace ?? throw new ArgumentNullException(nameof(trace));
    }

    public TargetSelectionOpenResult TryOpen(in ActorTurnToken turn)
    {
        lock (selectionGate)
        {
            if (promotionInProgress || activeSelection.IsValid)
                return TargetSelectionOpenResult.Rejected(BasicAttackRejection.Busy);
            if (turn != state.CurrentTurn)
            {
                return TargetSelectionOpenResult.Rejected(
                    turn.Actor == state.CurrentTurn.Actor
                        ? BasicAttackRejection.StaleTurnToken
                        : BasicAttackRejection.NotCurrentActor);
            }
            if (!state.TryValidateBasicAttackActor(
                    turn,
                    out DRPGSkillSO basicAttack,
                    out BasicAttackRejection rejection))
            {
                return TargetSelectionOpenResult.Rejected(rejection);
            }

            activeSelection = identityAuthority.AllocateSelection(turn);
            activeBasicAttack = basicAttack;
            if (!state.TryGetBasicAttackTraceContext(
                    turn,
                    out DRPGBattleSide side,
                    out ActionTracePosition homePosition))
            {
                activeSelection = default;
                activeBasicAttack = null;
                return TargetSelectionOpenResult.Rejected(
                    BasicAttackRejection.StaleTurnToken);
            }
            activeSelectionOpenedEventId =
                DRPGBasicAttackTrace.RecordSelectionOpened(
                    trace,
                    turn,
                    activeSelection,
                    side,
                    homePosition);
            return TargetSelectionOpenResult.Accepted(activeSelection);
        }
    }

    public TargetSelectionCancelResult TryCancel(
        in TargetSelectionToken selection)
    {
        lock (selectionGate)
        {
            if (promotionInProgress)
            {
                return TargetSelectionCancelResult.Rejected(
                    BasicAttackRejection.Busy);
            }
            if (!selection.IsValid || selection != activeSelection)
            {
                return TargetSelectionCancelResult.Rejected(
                    BasicAttackRejection.StaleSelection);
            }
            activeSelection = default;
            activeBasicAttack = null;
            activeSelectionOpenedEventId = string.Empty;
            return TargetSelectionCancelResult.Cancelled();
        }
    }

    public bool TryFreeze(
        in BasicAttackSelection selection,
        out DRPGBasicAttackFrozenSnapshot snapshot,
        out BasicAttackRejection rejection)
    {
        lock (selectionGate)
            return TryFreezeCore(selection, out snapshot, out rejection);
    }

    private bool TryFreezeCore(
        in BasicAttackSelection selection,
        out DRPGBasicAttackFrozenSnapshot snapshot,
        out BasicAttackRejection rejection,
        bool allowOwnedBattle = false)
    {
        snapshot = default;
        if (!selection.Selection.IsValid ||
            selection.Selection != activeSelection)
        {
            rejection = BasicAttackRejection.StaleSelection;
            return false;
        }
        ActorTurnToken turn = state.CurrentTurn;
        if (!turn.IsValid ||
            selection.Selection.BattleGeneration != turn.BattleGeneration ||
            selection.Selection.TurnSerial != turn.TurnSerial ||
            selection.Selection.Actor != turn.Actor)
        {
            rejection = BasicAttackRejection.StaleTurnToken;
            return false;
        }
        DRPGSkillSO basicAttack = activeBasicAttack;
        if (!allowOwnedBattle &&
            !state.TryValidateBasicAttackActor(
                turn,
                out basicAttack,
                out rejection))
        {
            return false;
        }
        if (!ReferenceEquals(basicAttack, activeBasicAttack))
        {
            rejection = BasicAttackRejection.BasicAttackUnavailable;
            return false;
        }
        if (!state.TryResolveBasicAttackSpec(
                turn,
                selection.Target,
                basicAttack,
                out DRPGResolvedActionSpec resolvedSpec,
                out rejection))
        {
            return false;
        }

        bool frozen = state.TryFreezeBasicAttackSnapshot(
            turn,
            selection.Selection,
            selection.Target,
            basicAttack,
            resolvedSpec,
            out snapshot,
            out rejection);
        if (!frozen)
            return false;
        if (selection.Selection != activeSelection ||
            state.CurrentTurn != turn)
        {
            snapshot = default;
            rejection = BasicAttackRejection.StaleSelection;
            return false;
        }
        return true;
    }

    public void Restore(in TargetSelectionToken selection)
    {
    }

    public bool TryPromote(
        in BasicAttackSelection selection,
        Func<DRPGBasicAttackFrozenSnapshot, RootActionOwnershipResult> promote,
        out DRPGBasicAttackFrozenSnapshot snapshot,
        out RootActionOwnershipResult ownership,
        out DRPGBasicAttackTraceCorrelation traceCorrelation,
        out BasicAttackRejection rejection)
    {
        if (promote == null)
            throw new ArgumentNullException(nameof(promote));
        lock (selectionGate)
        {
            snapshot = default;
            ownership = default;
            traceCorrelation = default;
            rejection = BasicAttackRejection.None;
            if (promotionInProgress)
            {
                rejection = BasicAttackRejection.Busy;
                return false;
            }
            promotionInProgress = true;
            try
            {
                if (!TryFreezeCore(selection, out snapshot, out rejection))
                    return false;
                ownership = promote(snapshot);
                if (!ownership.IsAccepted)
                {
                    rejection = BasicAttackRejection.Busy;
                    return false;
                }
                traceCorrelation = ConfirmTrace(snapshot);
                activeSelection = default;
                activeBasicAttack = null;
                activeSelectionOpenedEventId = string.Empty;
                rejection = BasicAttackRejection.None;
                return true;
            }
            finally
            {
                promotionInProgress = false;
            }
        }
    }

    public bool TryConfirmLegacy(
        in BasicAttackSelection selection,
        out DRPGBasicAttackFrozenSnapshot snapshot,
        out DRPGBasicAttackTraceCorrelation traceCorrelation,
        out BasicAttackRejection rejection)
    {
        lock (selectionGate)
        {
            snapshot = default;
            traceCorrelation = default;
            rejection = BasicAttackRejection.None;
            if (promotionInProgress)
            {
                rejection = BasicAttackRejection.Busy;
                return false;
            }
            promotionInProgress = true;
            try
            {
                if (!TryFreezeCore(
                        selection,
                        out snapshot,
                        out rejection,
                        allowOwnedBattle: true))
                {
                    return false;
                }
                traceCorrelation = ConfirmTrace(snapshot);
                activeSelection = default;
                activeBasicAttack = null;
                activeSelectionOpenedEventId = string.Empty;
                rejection = BasicAttackRejection.None;
                return true;
            }
            finally
            {
                promotionInProgress = false;
            }
        }
    }

    public void Consume(in TargetSelectionToken selection)
    {
        lock (selectionGate)
        {
            if (selection == activeSelection)
            {
                activeSelection = default;
                activeBasicAttack = null;
                activeSelectionOpenedEventId = string.Empty;
            }
        }
    }

    public bool TryPromoteConfigured(
        in DRPGSkillSelection selection,
        Func<DRPGBasicAttackFrozenSnapshot, RootActionOwnershipResult> promote,
        out DRPGBasicAttackFrozenSnapshot snapshot,
        out RootActionOwnershipResult ownership,
        out DRPGBasicAttackTraceCorrelation traceCorrelation,
        out BasicAttackRejection rejection)
    {
        if (promote == null)
            throw new ArgumentNullException(nameof(promote));
        lock (selectionGate)
        {
            snapshot = default;
            ownership = default;
            traceCorrelation = default;
            rejection = BasicAttackRejection.None;
            if (promotionInProgress || activeSelection.IsValid)
            {
                rejection = BasicAttackRejection.Busy;
                return false;
            }
            promotionInProgress = true;
            try
            {
                TargetSelectionToken token =
                    identityAuthority.AllocateSelection(selection.Turn);
                if (!state.TryGetBasicAttackTraceContext(selection.Turn,
                        out DRPGBattleSide side,
                        out ActionTracePosition home))
                {
                    rejection = BasicAttackRejection.StaleTurnToken;
                    return false;
                }
                activeSelectionOpenedEventId =
                    DRPGBasicAttackTrace.RecordSelectionOpened(
                        trace, selection.Turn, token, side, home);
                if (!(state is IDRPGConfiguredSkillSelectionState configuredState) ||
                    !configuredState.TryFreezeConfiguredSkillSnapshot(
                        selection, token, out snapshot, out rejection))
                    return false;
                ownership = promote(snapshot);
                if (!ownership.IsAccepted)
                {
                    rejection = BasicAttackRejection.Busy;
                    return false;
                }
                traceCorrelation = ConfirmTrace(snapshot);
                activeSelectionOpenedEventId = string.Empty;
                rejection = BasicAttackRejection.None;
                return true;
            }
            finally
            {
                promotionInProgress = false;
            }
        }
    }

    private DRPGBasicAttackTraceCorrelation ConfirmTrace(
        in DRPGBasicAttackFrozenSnapshot snapshot)
    {
        string turnEventId = DRPGBasicAttackTrace.TurnEventId(snapshot.Turn);
        string openedEventId = activeSelectionOpenedEventId;
        string confirmedEventId = DRPGBasicAttackTrace.RecordSelectionConfirmed(
            trace,
            snapshot,
            openedEventId);
        return new DRPGBasicAttackTraceCorrelation(
            turnEventId,
            openedEventId,
            confirmedEventId);
    }
}

internal readonly struct DRPGBattleTransactionPolicy
{
    internal DRPGBattleTransactionPolicy(bool standardBasicAttackEnabled)
    {
        StandardBasicAttackEnabled = standardBasicAttackEnabled;
    }

    internal bool StandardBasicAttackEnabled { get; }
    internal static DRPGBattleTransactionPolicy ProductionLegacy =>
        new DRPGBattleTransactionPolicy(false);
    internal static DRPGBattleTransactionPolicy StandardBasicAttackTest =>
        new DRPGBattleTransactionPolicy(true);
}

public sealed class DRPGBattleActionExecutionRouter :
    IStandardBasicAttackTransactions
{
    private readonly DRPGBattleTransactionPolicy policy;
    private readonly IDRPGBasicAttackSelectionAuthority selectionAuthority;
    private readonly IDRPGLegacyActionRunner legacyRunner;
    private readonly IDRPGStandardBasicAttackRunner standardRunner;
    private readonly DRPGBattleIdentityAuthority identityAuthority;
    private readonly DRPGBattleTransactionArbiter arbiter;

    internal DRPGBattleActionExecutionRouter(
        DRPGBattleTransactionPolicy policy,
        IDRPGBasicAttackSelectionAuthority selectionAuthority,
        IDRPGLegacyActionRunner legacyRunner,
        IDRPGStandardBasicAttackRunner standardRunner,
        DRPGBattleIdentityAuthority identityAuthority,
        DRPGBattleTransactionArbiter arbiter)
    {
        this.policy = policy;
        this.selectionAuthority = selectionAuthority ??
            throw new ArgumentNullException(nameof(selectionAuthority));
        this.legacyRunner = legacyRunner ??
            throw new ArgumentNullException(nameof(legacyRunner));
        this.standardRunner = standardRunner ??
            throw new ArgumentNullException(nameof(standardRunner));
        this.identityAuthority = identityAuthority ??
            throw new ArgumentNullException(nameof(identityAuthority));
        this.arbiter = arbiter ?? throw new ArgumentNullException(nameof(arbiter));
    }

    public TargetSelectionOpenResult TryOpenBasicAttackSelection(
        in ActorTurnToken actorTurn)
    {
        return selectionAuthority.TryOpen(actorTurn);
    }

    public TargetSelectionCancelResult TryCancel(
        in TargetSelectionToken selection)
    {
        return selectionAuthority.TryCancel(selection);
    }

    public RootActionStartResult TryStart(in BasicAttackSelection selection)
    {
        if (!policy.StandardBasicAttackEnabled)
        {
            return RootActionStartResult.Rejected(
                BasicAttackRejection.BasicAttackUnavailable);
        }
        BasicAttackRejection availability = BasicAttackRejection.None;
        TargetSelectionToken selectionToken = selection.Selection;
        if (!selectionAuthority.TryPromote(
                selection,
                snapshot =>
                {
                    availability = standardRunner.CheckAvailability(
                        snapshot.Turn.BattleGeneration);
                    return availability == BasicAttackRejection.None
                        ? arbiter.TryPromoteSelection(
                            selectionToken,
                            () => identityAuthority.AllocateAction(snapshot.Turn))
                        : RootActionOwnershipResult.Rejected(
                            "The Standard Basic Attack runner is unavailable.");
                },
                out DRPGBasicAttackFrozenSnapshot snapshot,
                out RootActionOwnershipResult ownership,
                out DRPGBasicAttackTraceCorrelation traceCorrelation,
                out BasicAttackRejection rejection))
        {
            return RootActionStartResult.Rejected(
                availability != BasicAttackRejection.None
                    ? availability
                    : rejection);
        }
        snapshot = snapshot.WithIdentity(ownership.Identity);
        string lockedEventId = DRPGBasicAttackTrace.RecordActionLocked(
            arbiter.Trace,
            snapshot,
            BattleTransactionIdentity.ForRootAction(ownership.Identity),
            ownership.Lease.LeaseSerial,
            traceCorrelation.SelectionConfirmedEventId);
        return standardRunner.TryStart(
            snapshot,
            ownership.Identity,
            ownership.Lease,
            lockedEventId);
    }

    public RootActionStartResult TryStart(in DRPGSkillSelection selection)
    {
        if (!policy.StandardBasicAttackEnabled)
            return RootActionStartResult.Rejected(
                BasicAttackRejection.BasicAttackUnavailable);
        BasicAttackRejection availability = BasicAttackRejection.None;
        if (!(selectionAuthority is IDRPGConfiguredSkillSelectionAuthority configuredAuthority))
            return RootActionStartResult.Rejected(
                BasicAttackRejection.BasicAttackUnavailable);
        if (!configuredAuthority.TryPromoteConfigured(
                selection,
                snapshot =>
                {
                    availability = standardRunner.CheckAvailability(
                        snapshot.Turn.BattleGeneration);
                    return availability == BasicAttackRejection.None
                        ? arbiter.TryPromoteSelection(
                            snapshot.Selection,
                            () => identityAuthority.AllocateAction(snapshot.Turn))
                        : RootActionOwnershipResult.Rejected(
                            "The configured Skill runner is unavailable.");
                },
                out DRPGBasicAttackFrozenSnapshot snapshot,
                out RootActionOwnershipResult ownership,
                out DRPGBasicAttackTraceCorrelation correlation,
                out BasicAttackRejection rejection))
        {
            return RootActionStartResult.Rejected(
                availability != BasicAttackRejection.None ? availability : rejection);
        }
        snapshot = snapshot.WithIdentity(ownership.Identity);
        string lockedEventId = DRPGBasicAttackTrace.RecordActionLocked(
            arbiter.Trace, snapshot,
            BattleTransactionIdentity.ForRootAction(ownership.Identity),
            ownership.Lease.LeaseSerial,
            correlation.SelectionConfirmedEventId);
        return standardRunner.TryStart(snapshot, ownership.Identity,
            ownership.Lease, lockedEventId);
    }

    internal LegacyActionStartResult TryStartLegacy(DRPGBattleCommand command)
    {
        if (arbiter.HasOwner)
        {
            return LegacyActionStartResult.Rejected(
                "Another battle transaction already owns action execution.");
        }
        return legacyRunner.TryStartLegacy(command);
    }

    internal LegacyActionStartResult TryStartLegacy(
        DRPGBattleCommand command,
        DRPGLegacyActionStartOperation operation)
    {
        if (arbiter.HasOwner)
        {
            return LegacyActionStartResult.Rejected(
                "Another battle transaction already owns action execution.");
        }
        if (!(legacyRunner is DRPGLegacyActionRunnerAdapter adapter))
        {
            return LegacyActionStartResult.Rejected(
                "The configured Legacy runner does not accept scoped operations.");
        }
        return adapter.TryStartLegacy(command, operation);
    }

    internal bool TryConfirmLegacyBasicAttack(
        in BasicAttackSelection selection,
        out DRPGBasicAttackFrozenSnapshot snapshot,
        out DRPGBasicAttackTraceCorrelation traceCorrelation,
        out BasicAttackRejection rejection)
    {
        return selectionAuthority.TryConfirmLegacy(
            selection,
            out snapshot,
            out traceCorrelation,
            out rejection);
    }

    internal bool TryFreezeLegacyBasicAttack(
        in BasicAttackSelection selection,
        out DRPGBasicAttackFrozenSnapshot snapshot,
        out BasicAttackRejection rejection)
    {
        return selectionAuthority.TryFreeze(
            selection,
            out snapshot,
            out rejection);
    }
}

internal static class DRPGBasicAttackTrace
{
    internal static string TurnEventId(in ActorTurnToken turn)
    {
        return "basic-attack-turn-" + turn.BattleGeneration + "-" +
            turn.TurnSerial + "-" + turn.Actor.Value;
    }

    internal static string RecordTurnBegin(
        DRPGBattleActionTraceWriter trace,
        in ActorTurnToken turn,
        DRPGBattleSide side,
        ActionTracePosition homePosition)
    {
        if (trace == null)
            throw new ArgumentNullException(nameof(trace));
        string eventId = TurnEventId(turn);
        trace.Record(
            0d,
            0d,
            ActionTraceIdentity.ForTurn(turn),
            ActionTraceEventKind.PhaseEntered,
            DRPGBattleActionStage.TurnBegin,
            0,
            eventId,
            null,
            new ActionTraceContext(
                actor: turn.Actor,
                side: side,
                homePosition: homePosition));
        return eventId;
    }

    internal static string RecordSelectionOpened(
        DRPGBattleActionTraceWriter trace,
        in ActorTurnToken turn,
        in TargetSelectionToken selection,
        DRPGBattleSide side,
        ActionTracePosition homePosition)
    {
        if (trace == null)
            throw new ArgumentNullException(nameof(trace));
        string eventId = "basic-attack-selection-opened-" +
            selection.BattleGeneration + "-" + selection.TurnSerial + "-" +
            selection.SelectionId;
        trace.Record(
            0d,
            0d,
            ActionTraceIdentity.ForSelection(selection),
            ActionTraceEventKind.SelectionOpened,
            DRPGBattleActionStage.Selection,
            0,
            eventId,
            TurnEventId(turn),
            new ActionTraceContext(
                precursorTurn: turn,
                actor: turn.Actor,
                side: side,
                homePosition: homePosition));
        return eventId;
    }

    internal static string RecordSelectionConfirmed(
        DRPGBattleActionTraceWriter trace,
        in DRPGBasicAttackFrozenSnapshot snapshot,
        string selectionOpenedEventId)
    {
        if (trace == null)
            throw new ArgumentNullException(nameof(trace));
        string eventId = "basic-attack-selection-confirmed-" +
            snapshot.Selection.BattleGeneration + "-" +
            snapshot.Selection.TurnSerial + "-" +
            snapshot.Selection.SelectionId;
        trace.Record(
            0d,
            0d,
            ActionTraceIdentity.ForSelection(snapshot.Selection),
            ActionTraceEventKind.SelectionConfirmed,
            DRPGBattleActionStage.Selection,
            1,
            eventId,
            selectionOpenedEventId,
            new ActionTraceContext(
                precursorTurn: snapshot.Turn,
                actor: snapshot.Turn.Actor,
                target: snapshot.Target,
                side: snapshot.ActorSide,
                homePosition: snapshot.ActorHomePosition,
                conflictPosition: snapshot.ActorConflictPosition));
        return eventId;
    }

    internal static string RecordActionLocked(
        DRPGBattleActionTraceWriter trace,
        in DRPGBasicAttackFrozenSnapshot snapshot,
        BattleTransactionIdentity owner,
        long leaseSerial,
        string selectionConfirmedEventId)
    {
        if (trace == null)
            throw new ArgumentNullException(nameof(trace));
        string eventId = "basic-attack-action-locked-" +
            owner.BattleGeneration + "-" + owner.Action.TurnSerial + "-" +
            owner.Action.ActionId;
        trace.Record(
            0d,
            0d,
            ActionTraceIdentity.ForTransaction(owner),
            ActionTraceEventKind.ActionLocked,
            DRPGBattleActionStage.Locked,
            0,
            eventId,
            selectionConfirmedEventId,
            new ActionTraceContext(
                precursorTurn: snapshot.Turn,
                precursorSelection: snapshot.Selection,
                actor: snapshot.Turn.Actor,
                target: snapshot.Target,
                source: snapshot.Turn.Actor,
                side: snapshot.ActorSide,
                homePosition: snapshot.ActorHomePosition,
                conflictPosition: snapshot.ActorConflictPosition,
                leaseId: "rule:" + leaseSerial));
        return eventId;
    }
}
