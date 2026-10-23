using System;

public sealed class DRPGBattleRecoveryBridge : IDisposable
{
    private readonly DRPGBattleSession session;
    private readonly DRPGFieldRecoveryService recoveryService;
    private readonly string battleRunId;

    public DRPGBattleRecoveryBridge(
        DRPGBattleSession session,
        DRPGFieldRecoveryService recoveryService,
        string battleRunId)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.recoveryService = recoveryService ??
            throw new ArgumentNullException(nameof(recoveryService));
        if (string.IsNullOrWhiteSpace(battleRunId))
            throw new ArgumentException("Battle run ID is required.", nameof(battleRunId));
        this.battleRunId = battleRunId;
        session.ParticipantChanged += HandleParticipantChanged;
    }

    public void Dispose()
    {
        session.ParticipantChanged -= HandleParticipantChanged;
    }

    private void HandleParticipantChanged(BattleParticipantRuntime participant)
    {
        if (participant == null || participant.IsAlive ||
            participant.Profile == null)
        {
            return;
        }
        recoveryService.TryHandlePermanentDeath(
            battleRunId + ":participant:" + participant.RuntimeId,
            participant.Profile.CombatantId,
            out _);
    }
}
