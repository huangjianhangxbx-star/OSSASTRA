using System;
using System.Collections.Generic;

internal interface IBattleWavePresentationScope : IDisposable
{
    IWaveTransitionPresentationPort Port { get; }
    IDRPGBattlePresentationClock Clock { get; }
    bool IsActive { get; }
    bool TryAdoptDeployed();
}

internal interface IBattleWavePresentationFactory
{
    bool TryOpen(BattleTransactionIdentity owner, IReadOnlyList<BattleParticipantRuntime> members,
        int perRowLimit, out IBattleWavePresentationScope scope, out string error);
}

internal readonly struct WaveAnnouncementCommand
{
    internal WaveAnnouncementCommand(WaveTransitionIdentity identity, double duration)
    { Identity = identity; Duration = duration; }
    internal WaveTransitionIdentity Identity { get; }
    internal double Duration { get; }
}

internal readonly struct WaveBatchStagingCommand
{
    internal WaveBatchStagingCommand(WaveTransitionIdentity identity, IReadOnlyList<ParticipantId> members,
        IReadOnlyList<PresentationReceipt> ready, double stagger)
    { Identity = identity; Members = members; MemberReady = ready; StaggerMaximum = stagger; }
    internal WaveTransitionIdentity Identity { get; }
    internal IReadOnlyList<ParticipantId> Members { get; }
    internal IReadOnlyList<PresentationReceipt> MemberReady { get; }
    internal double StaggerMaximum { get; }
}

internal readonly struct WaveBatchReadyCommand
{
    internal WaveBatchReadyCommand(WaveTransitionIdentity identity) { Identity = identity; }
    internal WaveTransitionIdentity Identity { get; }
}

internal sealed class WaveStagingReceipt
{
    internal WaveStagingReceipt(PresentationReceipt receipt) { Receipt = receipt; }
    internal PresentationReceipt Receipt { get; }
}

internal interface IWaveTransitionPresentationPort
{
    PresentationReceipt AnnounceWave(in WaveAnnouncementCommand command);
    WaveStagingReceipt StageBatch(in WaveBatchStagingCommand command);
    PresentationReceipt AwaitBatchReady(in WaveBatchReadyCommand command);
    void Tick();
    // Normal close preserves ready actors, but restores temporary projection/UI.
    // Fault close retracts every staged actor. Neither path mutates battle rules.
    PresentationCleanupReceipt CloseWave(WaveTransitionIdentity identity, bool retractActors);
}
