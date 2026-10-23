public interface IAudioStateMachine
{
    string AudioStateMachineName { get; }
    string CurrentStateName { get; }
    int Priority { get; }
    bool IsActive { get; }

    void Initialize(GameFlowRoot root, GameAudioRoot audioRoot, GameEventHub eventHub);
    bool RequestState(object stateData);
    void Enter();
    void Exit();
    void Update(float deltaTime);
}