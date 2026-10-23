public sealed class DRPGPostureBreakTransition
{
    public DRPGPostureBreakTransition(
        DRPGPostureBreakState previousState,
        DRPGPostureBreakState currentState,
        bool enteredBreak,
        bool skipOwnAction,
        float restorePostureRatio)
    {
        PreviousState = previousState;
        CurrentState = currentState;
        EnteredBreak = enteredBreak;
        SkipOwnAction = skipOwnAction;
        RestorePostureRatio = restorePostureRatio;
    }

    public DRPGPostureBreakState PreviousState { get; }
    public DRPGPostureBreakState CurrentState { get; }
    public bool EnteredBreak { get; }
    public bool SkipOwnAction { get; }
    public bool SkipAction => SkipOwnAction;
    public bool CanAct => !SkipOwnAction;
    public float RestorePostureRatio { get; }
    public bool StateChanged => PreviousState != CurrentState;
}
