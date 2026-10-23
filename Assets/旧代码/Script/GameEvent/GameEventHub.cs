//GameEventHub 是一个容器
//EventBus 是真正的事件总线
//GameFlow / Hunt / UI / Audio 是不同事件领域


public sealed class GameEventHub
{
    public EventBus GameFlow { get; } = new EventBus();
    public EventBus Hunt { get; } = new EventBus();
    public EventBus UI { get; } = new EventBus();
    public EventBus Audio { get; } = new EventBus();
    public EventBus Gal { get; } = new EventBus();

    public void ClearHunt()
    {
        Hunt.Clear();
    }

    public void ClearUI()
    {
        UI.Clear();
    }

    public void ClearAudio()
    {
        Audio.Clear();
    }

    public void ClearGal()
    {
        Gal.Clear();
    }

    public void ClearAll()
    {
        GameFlow.Clear();
        Hunt.Clear();
        UI.Clear();
        Audio.Clear();
        Gal.Clear();
    }
}

public struct GameFlowStateChangedEvent
{
    public GameFlowStateType from;
    public GameFlowStateType to;
}

public struct HuntFlowStateChangedEvent
{
    public GameFlowStateType from;
    public GameFlowStateType to;
}
