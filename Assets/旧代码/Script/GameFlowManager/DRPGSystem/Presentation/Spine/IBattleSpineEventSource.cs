using System;

public interface IBattleSpineEventSource
{
    IDisposable Subscribe(Action<BattleSpineMarker> onMarker);
}

public interface IBattleSpineMarkerGateway
{
    IBattlePresentationHandle WaitFor(
        BattlePresentationScope scope,
        int actorRuntimeId,
        BattleSpineMarker expected,
        float timeoutSeconds,
        Action onMatched,
        Action onTimedOut);
}
