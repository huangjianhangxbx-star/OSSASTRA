using System;
using System.Collections.Generic;

internal sealed class DRPGEnemyWaveLifecycle
{
    private readonly DRPGBattleSessionState state;
    private readonly DRPGBattleContinuationInbox inbox;
    private readonly DRPGEnemyWavePlan plan;
    private readonly Dictionary<int, BattleParticipantRuntime> runtimeMembers = new Dictionary<int, BattleParticipantRuntime>();
    private readonly Dictionary<EnemyDeploymentId, ParticipantId> identities = new Dictionary<EnemyDeploymentId, ParticipantId>();
    private DRPGEnemyWaveDeploymentBatch pending;
    private WaveTransitionCompletion sealedCompletion;
    private DRPGEnemyWaveDepartureLedger departures;
    private bool cancelled;
    internal int CompletedWaveCount { get; private set; }
    internal DRPGEnemyWaveLifecycle(DRPGBattleSessionState state, DRPGBattleContinuationInbox inbox,
        DRPGEnemyWavePlan plan, IReadOnlyList<BattleParticipantRuntime> authoredEnemies)
    {
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        this.inbox = inbox ?? throw new ArgumentNullException(nameof(inbox));
        this.plan = plan ?? throw new ArgumentNullException(nameof(plan));
        if (authoredEnemies == null) throw new ArgumentNullException(nameof(authoredEnemies));
        foreach (var actor in authoredEnemies) if (actor != null) runtimeMembers.Add(actor.RuntimeId, actor);
        int index = 0;
        foreach (var wave in plan.Waves)
            foreach (var member in wave)
            {
                if (index >= authoredEnemies.Count || authoredEnemies[index] == null ||
                    !ReferenceEquals(authoredEnemies[index].Spec, member.Spec) || member.Id.AuthoredOrdinal != index)
                    throw new ArgumentException("Frozen wave order must match the authored runtime roster exactly.");
                identities.Add(member.Id, new ParticipantId(authoredEnemies[index++].RuntimeId));
            }
        if (index != authoredEnemies.Count) throw new ArgumentException("Wave plan omitted authored runtime members.");
    }
    internal bool TryStage(BattleRuleMutationLease lease, int perRowLimit, float deploymentTime,
        out DRPGEnemyWaveDeploymentBatch batch, out string error)
    {
        batch = null; error = string.Empty;
        if (cancelled || pending != null && !pending.IsCommitted || CompletedWaveCount >= plan.Waves.Count ||
            lease == null || lease.Owner.Kind != BattleTransactionKind.WaveTransition ||
            lease.Owner.WaveTransition.WaveId != CompletedWaveCount + 1 || !state.OwnsRuleMutationLease(lease, lease.Owner))
        { error = "Wave is cancelled, already staging, exhausted or not owned in planned order."; return false; }
        if (CompletedWaveCount > 0 && (departures == null || !departures.AllDeparted))
        { error = "The previous wave has not completed every exact typed departure."; return false; }
        bool playerAlive = false;
        foreach (var participant in state.CaptureRuleSnapshot().Participants)
            if (participant.Side == DRPGBattleSide.Player && participant.IsDeployed && participant.CurrentHp > 0)
                playerAlive = true;
        if (!playerAlive) { error = "Party defeat takes precedence over reinforcements."; return false; }
        var wave = plan.Waves[CompletedWaveCount];
        var runtime = new ParticipantId[wave.Count]; var authored = new EnemyDeploymentId[wave.Count];
        for (int i = 0; i < wave.Count; i++) { authored[i] = wave[i].Id; runtime[i] = identities[authored[i]]; }
        try { batch = new DRPGEnemyWaveDeploymentBatch(state, lease, runtime, perRowLimit, deploymentTime, authored); }
        catch (ArgumentException exception) { error = exception.Message; return false; }
        pending = batch; sealedCompletion = default; return true;
    }
    internal IReadOnlyList<BattleParticipantRuntime> NextMembers()
    {
        if (cancelled || CompletedWaveCount >= plan.Waves.Count)
            throw new InvalidOperationException("No remaining wave can be opened.");
        var members = new List<BattleParticipantRuntime>();
        foreach (var member in plan.Waves[CompletedWaveCount]) members.Add(runtimeMembers[identities[member.Id].Value]);
        return members.AsReadOnly();
    }
    internal bool TryCommitReady(out WaveTransitionCompletion completion, out string error)
    {
        completion = default; error = string.Empty;
        if (sealedCompletion.Identity.IsValid) { completion = sealedCompletion; return true; }
        if (cancelled || pending == null) { error = "No active wave staging exists."; return false; }
        if (!pending.TryCommit(out error)) return false;
        if (!pending.TryGetCompletion(out completion)) { error = "Committed wave has no frozen completion."; return false; }
        var submitted = inbox.Submit(completion);
        if (!submitted.IsAccepted) { cancelled = true; error = "Shared Inbox rejected the committed wave."; return false; }
        departures = new DRPGEnemyWaveDepartureLedger(completion, identities);
        sealedCompletion = completion; CompletedWaveCount++; return true;
    }
    internal bool TryObserveDeparture(DRPGBattleContinuationInbox.Entry entry, out string error)
    {
        error = string.Empty;
        if (cancelled || departures == null) { error = "No wave is active."; return false; }
        return departures.TryObserve(entry, out error);
    }
    internal void CancelPending() { cancelled = true; pending?.Cancel(); }
}
