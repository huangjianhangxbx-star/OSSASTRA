using System;
using System.Collections.Generic;

// Rule-side half of two-phase deployment. Staged visuals cannot mutate State;
// the Wave runner gives their exact receipts to this single atomic commit gate.
internal sealed class DRPGEnemyWaveDeploymentBatch
{
    private readonly DRPGBattleSessionState state;
    private readonly BattleRuleMutationLease lease;
    private readonly ParticipantId[] members;
    private readonly PresentationReceipt[] ready;
    private readonly int perRowLimit;
    private readonly float deploymentTime;
    private readonly long revision;
    private readonly EnemyDeploymentId[] deploymentIdentities;
    private WaveTransitionCompletion sealedCompletion;
    private bool committed, cancelled, committing;
    public PresentationReceipt[] ReadyReceipts => (PresentationReceipt[])ready.Clone();
    public bool IsCommitted => committed;
    internal IReadOnlyList<ParticipantId> Members => Array.AsReadOnly(members);
    internal bool TryGetCompletion(out WaveTransitionCompletion completion)
    { completion = sealedCompletion; return committed && sealedCompletion.Identity.IsValid; }

    internal DRPGEnemyWaveDeploymentBatch(DRPGBattleSessionState state, BattleRuleMutationLease lease,
        IReadOnlyList<ParticipantId> members, int perRowLimit, float deploymentTime,
        IReadOnlyList<EnemyDeploymentId> deploymentIdentities = null)
    {
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        if (lease == null || lease.Owner.Kind != BattleTransactionKind.WaveTransition ||
            !state.OwnsRuleMutationLease(lease,lease.Owner)) throw new ArgumentException("An active matching Wave lease is required.");
        if (members == null || members.Count == 0 || perRowLimit <= 0 || members.Count > (long)perRowLimit*3)
            throw new ArgumentException("The entire batch must fit unique formation slots.");
        if (deploymentIdentities != null)
        {
            if (deploymentIdentities.Count != members.Count) throw new ArgumentException("Every runtime member requires its frozen authored identity.");
            this.deploymentIdentities = new EnemyDeploymentId[members.Count];
            var authoredIds = new HashSet<EnemyDeploymentId>();
            for (int i = 0; i < members.Count; i++)
            {
                var id = deploymentIdentities[i];
                if (string.IsNullOrWhiteSpace(id.EncounterId) || !authoredIds.Add(id) ||
                    (i > 0 && id.EncounterId != this.deploymentIdentities[0].EncounterId))
                    throw new ArgumentException("Deployment identities must be unique members of one encounter.");
                this.deploymentIdentities[i] = id;
            }
        }
        var snapshot = state.CaptureRuleSnapshot();
        if (float.IsNaN(deploymentTime) || float.IsInfinity(deploymentTime) || deploymentTime < snapshot.CurrentLogicalTime)
            throw new ArgumentOutOfRangeException(nameof(deploymentTime));
        this.members = new ParticipantId[members.Count];
        ready = new PresentationReceipt[members.Count];
        var seen = new HashSet<ParticipantId>();
        for (int i = 0; i < members.Count; i++)
        {
            var member = members[i];
            if (!seen.Add(member) || !snapshot.TryGetParticipant(member,out var participant) ||
                participant.Side != DRPGBattleSide.Enemy || participant.CurrentHp <= 0 || participant.IsDeployed)
                throw new ArgumentException("Wave requires unique living undeployed enemy identities.");
            this.members[i] = member;
            ready[i] = new PresentationReceipt(lease.Owner,DRPGBattleActionStage.TurnBegin,i+1);
        }
        foreach (var participant in snapshot.Participants)
            if (participant.Side == DRPGBattleSide.Enemy && participant.IsDeployed)
                throw new ArgumentException("The previous wave must depart before another batch stages.");
        this.lease = lease; this.perRowLimit = perRowLimit; this.deploymentTime = deploymentTime;
        revision = state.BattleRevision;
    }

    public bool TryCommit(out string error)
    {
        error = string.Empty;
        if (committed) return true;
        if (cancelled || committing || !state.OwnsRuleMutationLease(lease,lease.Owner) || state.BattleRevision != revision)
        { error = "Wave staging was cancelled or its physical owner/revision changed."; return false; }
        foreach (var receipt in ready)
        {
            if (!receipt.Completion.IsCompleted) { error = "Whole-wave readiness barrier is pending."; return false; }
            var kind = receipt.Completion.Result.Kind;
            if (kind != PresentationReceiptTerminalKind.Completed && kind != PresentationReceiptTerminalKind.Degraded)
            { error = "A wave member failed readiness."; return false; }
        }
        committing = true;
        try
        {
            var draft = state.CreateWorkspace();
            for (int i = 0; i < members.Length; i++)
            {
                draft.For(members[i]).SetFormationRow((DRPGFormationRow)(i/perRowLimit)).SetDeployed(true);
                draft.ScheduleAt(members[i],deploymentTime,0);
            }
            var result = state.TryCommitJournal(lease,draft);
            if (!result.Succeeded) { error = result.Error; return false; }
            if (deploymentIdentities != null)
                sealedCompletion = new WaveTransitionCompletion(lease.Owner.WaveTransition, deploymentIdentities, state.BattleRevision, lease);
            committed = true;
            return true;
        }
        finally { committing = false; }
    }
    internal void Cancel() { if (!committed) cancelled = true; }
}
