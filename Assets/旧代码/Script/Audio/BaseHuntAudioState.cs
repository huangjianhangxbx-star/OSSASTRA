public abstract class BaseHuntAudioState
{
    protected HuntAudioStateMachine stateMachine;
    protected GameFlowRoot gameFlowRoot;
    protected GameAudioRoot audioRoot;
    protected GameEventHub eventHub;

    public abstract string StateName { get; }

    public virtual void Initialize(
        HuntAudioStateMachine stateMachine,
        GameFlowRoot gameFlowRoot,
        GameAudioRoot audioRoot,
        GameEventHub eventHub)
    {
        this.stateMachine = stateMachine;
        this.gameFlowRoot = gameFlowRoot;
        this.audioRoot = audioRoot;
        this.eventHub = eventHub;
    }

    public abstract void Enter();
    public virtual void Exit() { }
    public virtual void Update(float deltaTime) { }

    public abstract bool CanTransitionTo(BaseHuntAudioState newState);
}