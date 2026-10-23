using System;

public sealed class DRPGPostureBreakRuntime
{
    private DRPGBattleStateMutationCapability stateMutationCapability;
    private int stateMutationScopeDepth;

    public DRPGPostureBreakRuntime(DRPGPostureBreakRules rules)
    {
        Rules = rules ?? throw new ArgumentNullException(nameof(rules));
        State = DRPGPostureBreakState.Stable;
    }

    public DRPGPostureBreakRules Rules { get; }
    internal DRPGPostureRuleSnapshot CaptureRuleState() => new DRPGPostureRuleSnapshot(State, RemainingStabilityOwnActions, Rules);

    internal void ApplyCommittedRuleState(DRPGBattleStateMutationCapability capability, DRPGPostureRuleSnapshot snapshot)
    {
        RequireStateCapability(capability);
        State = snapshot.State;
        RemainingStabilityOwnActions = snapshot.RemainingStability;
    }
    public DRPGPostureBreakState State { get; private set; }
    public int RemainingStabilityOwnActions { get; private set; }
    public int RemainingStabilityOwnTurns => RemainingStabilityOwnActions;

    public bool IsExposed =>
        State == DRPGPostureBreakState.ExposedPendingSkip ||
        State == DRPGPostureBreakState.ExposedAfterSkip;

    public float IncomingDamageMultiplier =>
        IsExposed ? Rules.ExposedIncomingDamageMultiplier : 1f;

    public float IncomingAilmentBuildupMultiplier =>
        IsExposed ? Rules.ExposedAilmentBuildupMultiplier : 1f;

    public float IncomingAilmentBuildUpMultiplier =>
        IncomingAilmentBuildupMultiplier;

    public float IncomingPostureDamageMultiplier =>
        State == DRPGPostureBreakState.RecoveryStability
            ? Rules.StabilityIncomingPostureDamageMultiplier
            : 1f;

    public DRPGPostureBreakTransition EnterBreak()
    {
        RequireDirectMutationAccess();
        return EnterBreakCore();
    }

    internal DRPGPostureBreakTransition EnterBreak(
        DRPGBattleStateMutationCapability capability)
    {
        RequireStateCapability(capability);
        return EnterBreakCore();
    }

    private DRPGPostureBreakTransition EnterBreakCore()
    {
        DRPGPostureBreakState previous = State;
        if (IsExposed)
            return Transition(previous, false, false, 0f);

        State = DRPGPostureBreakState.ExposedPendingSkip;
        RemainingStabilityOwnActions = 0;
        return Transition(previous, true, false, 0f);
    }

    public DRPGPostureBreakTransition Enter()
    {
        RequireDirectMutationAccess();
        return EnterBreakCore();
    }

    internal DRPGPostureBreakTransition Enter(
        DRPGBattleStateMutationCapability capability)
    {
        RequireStateCapability(capability);
        return EnterBreakCore();
    }

    public DRPGPostureBreakTransition ResolveOwnTurnStart()
    {
        RequireDirectMutationAccess();
        return ResolveOwnTurnStartCore();
    }

    internal DRPGPostureBreakTransition ResolveOwnTurnStart(
        DRPGBattleStateMutationCapability capability)
    {
        RequireStateCapability(capability);
        return ResolveOwnTurnStartCore();
    }

    private DRPGPostureBreakTransition ResolveOwnTurnStartCore()
    {
        DRPGPostureBreakState previous = State;
        if (State == DRPGPostureBreakState.ExposedPendingSkip)
        {
            State = DRPGPostureBreakState.ExposedAfterSkip;
            return Transition(previous, false, true, 0f);
        }

        if (State == DRPGPostureBreakState.ExposedAfterSkip)
        {
            RemainingStabilityOwnActions = Rules.StabilityOwnActionCount;
            State = RemainingStabilityOwnActions > 0
                ? DRPGPostureBreakState.RecoveryStability
                : DRPGPostureBreakState.Stable;
            return Transition(
                previous,
                false,
                false,
                Rules.RecoveryPostureRatio);
        }

        return Transition(previous, false, false, 0f);
    }

    public DRPGPostureBreakTransition CompleteOwnAction()
    {
        RequireDirectMutationAccess();
        return CompleteOwnActionCore();
    }

    internal DRPGPostureBreakTransition CompleteOwnAction(
        DRPGBattleStateMutationCapability capability)
    {
        RequireStateCapability(capability);
        return CompleteOwnActionCore();
    }

    private DRPGPostureBreakTransition CompleteOwnActionCore()
    {
        DRPGPostureBreakState previous = State;
        if (State != DRPGPostureBreakState.RecoveryStability)
            return Transition(previous, false, false, 0f);

        RemainingStabilityOwnActions = Math.Max(
            0,
            RemainingStabilityOwnActions - 1);
        if (RemainingStabilityOwnActions == 0)
            State = DRPGPostureBreakState.Stable;
        return Transition(previous, false, false, 0f);
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
                "The posture-break runtime already belongs to another " +
                "battle state.");
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
            throw new InvalidOperationException(
                "Posture-break mutation scope underflow.");
        stateMutationScopeDepth--;
    }

    private void RequireDirectMutationAccess()
    {
        if (stateMutationCapability != null && stateMutationScopeDepth <= 0)
        {
            throw new InvalidOperationException(
                "An attached posture-break runtime can mutate only through " +
                "its owning battle state.");
        }
    }

    private void RequireStateCapability(
        DRPGBattleStateMutationCapability capability)
    {
        if (capability == null || stateMutationCapability == null ||
            !ReferenceEquals(capability, stateMutationCapability))
        {
            throw new InvalidOperationException(
                "The posture-break mutation capability is missing or stale.");
        }
    }

    private DRPGPostureBreakTransition Transition(
        DRPGPostureBreakState previous,
        bool enteredBreak,
        bool skipOwnAction,
        float restorePostureRatio)
    {
        return new DRPGPostureBreakTransition(
            previous,
            State,
            enteredBreak,
            skipOwnAction,
            restorePostureRatio);
    }
}
