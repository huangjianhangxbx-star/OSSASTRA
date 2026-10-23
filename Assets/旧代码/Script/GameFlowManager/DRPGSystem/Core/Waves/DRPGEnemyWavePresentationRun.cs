using System;

internal sealed class DRPGEnemyWavePresentationRun
{
    private readonly DRPGEnemyWaveLifecycle lifecycle;
    private readonly BattleRuleMutationLease lease;
    private readonly IWaveTransitionPresentationPort port;
    private readonly IDRPGBattlePresentationClock clock;
    private readonly DRPGEnemyWavePolicySnapshot policy;
    private readonly DRPGEnemyWaveDeploymentBatch batch;
    private readonly double timeout;
    private readonly WaveTransitionIdentity identity;
    private readonly BattlePresentationWatchdog watchdog;
    private PresentationReceipt receipt;
    private PresentationCleanupReceipt cleanup;
    private int phase;
    private double issuedAt, lastTime;
    private long issuedFrame;
    private bool ticking, closingFault;
    internal DRPGEnemyWavePresentationRun(DRPGEnemyWaveLifecycle lifecycle, BattleRuleMutationLease lease,
        IWaveTransitionPresentationPort port, IDRPGBattlePresentationClock clock,
        DRPGEnemyWavePolicySnapshot policy, int perRowLimit, float deploymentTime, double receiptTimeout)
    {
        this.lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        this.lease = lease ?? throw new ArgumentNullException(nameof(lease));
        this.port = port ?? throw new ArgumentNullException(nameof(port));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        if (!Finite(clock.Now) || clock.Now < 0 || !Finite(receiptTimeout) || receiptTimeout <= policy.AnnouncementDuration ||
            !Finite(policy.AnnouncementDuration) || policy.AnnouncementDuration <= 0 || policy.EntryStaggerMaximum < 0 || policy.EntryStaggerMaximum > .5)
            throw new ArgumentException("Wave presentation requires frozen valid timing and a longer finite timeout.");
        this.policy = policy; timeout = receiptTimeout; lastTime = clock.Now;
        watchdog = new BattlePresentationWatchdog(clock);
        if (!lifecycle.TryStage(lease, perRowLimit, deploymentTime, out batch, out var error)) throw new ArgumentException(error);
        identity = lease.Owner.WaveTransition;
    }
    internal bool CompletionSubmitted { get; private set; }
    internal bool FaultCleanupCompleted { get; private set; }
    internal string Fault { get; private set; } = string.Empty;
    internal void Tick()
    {
        if (ticking || CompletionSubmitted || FaultCleanupCompleted) return;
        ticking = true;
        try
        {
            if (!Finite(clock.Now) || clock.Now < lastTime) Fail("Wave presentation clock moved backwards or became invalid.");
            else lastTime = clock.Now;
            if (!Owns) Fail("Wave lost its original mutation lease.");
            // Expiry must win over callbacks pumped on this same late frame.
            watchdog.Tick();
            if (phase == 4 && (cleanup == null || !cleanup.Completion.IsCompleted)) CheckTimeout();
            port.Tick();
            if (!Owns) Fail("Wave presentation pump changed mutation ownership.");
            if (Fault.Length != 0) { PollFaultCleanup(); return; }
            if (phase == 0)
            {
                phase = 1; MarkIssue();
                receipt = port.AnnounceWave(new WaveAnnouncementCommand(identity, policy.AnnouncementDuration));
                Validate(receipt, DRPGBattleActionStage.Announcement); return;
            }
            if (phase == 4)
            {
                if (cleanup == null || !cleanup.Completion.IsCompleted) { CheckTimeout(); return; }
                if (!cleanup.Completion.Result) { Fail("Wave projection cleanup was not acknowledged."); return; }
                if (!lifecycle.TryCommitReady(out _, out var error)) { Fail(error); return; }
                CompletionSubmitted = true; return;
            }
            if (!Ready(receipt)) { CheckTimeout(); return; }
            if (phase == 1)
            {
                if (clock.Now - issuedAt < policy.AnnouncementDuration || clock.RenderedFrame - issuedFrame < 2)
                { CheckTimeout(); return; }
                phase = 2; MarkIssue();
                var staged = port.StageBatch(new WaveBatchStagingCommand(identity, batch.Members,
                    Array.AsReadOnly(batch.ReadyReceipts), policy.EntryStaggerMaximum));
                foreach (var member in batch.ReadyReceipts) Watch(member, "wave-member-ready");
                receipt = staged?.Receipt; Validate(receipt, DRPGBattleActionStage.ConflictComposition); return;
            }
            if (phase == 2)
            {
                phase = 3; MarkIssue();
                receipt = port.AwaitBatchReady(new WaveBatchReadyCommand(identity));
                Validate(receipt, DRPGBattleActionStage.TurnBegin); return;
            }
            foreach (var member in batch.ReadyReceipts)
                if (!Ready(member)) { CheckTimeout(); return; }
            phase = 4; MarkIssue(); BeginClose(false);
        }
        catch (Exception error) { Fail(error.ToString()); }
        finally
        {
            if (Fault.Length != 0 && !closingFault) BeginClose(true);
            ticking = false;
        }
    }
    internal void Cancel(string reason)
    {
        if (CompletionSubmitted || FaultCleanupCompleted) return;
        Fail(string.IsNullOrEmpty(reason) ? "Wave cancelled." : reason);
        // A callback cannot start cleanup while another presentation operation
        // is still adding handles. The outer Tick closes after that call returns.
        if (!ticking)
        {
            ticking = true;
            try { if (!closingFault) BeginClose(true); PollFaultCleanup(); }
            finally { ticking = false; }
        }
    }
    private bool Owns => lease.Arbiter.Owns(lease, lease.Owner);
    private void MarkIssue() { issuedAt = clock.Now; issuedFrame = clock.RenderedFrame; }
    private bool Ready(PresentationReceipt value)
    {
        if (value == null) { Fail("Wave presentation omitted its receipt."); return false; }
        if (!value.Completion.IsCompleted) return false;
        var kind = value.Completion.Result.Kind;
        if (kind == PresentationReceiptTerminalKind.Completed || kind == PresentationReceiptTerminalKind.Degraded) return true;
        Fail("Wave presentation receipt failed: " + value.Completion.Result.Detail); return false;
    }
    private void Validate(PresentationReceipt value, DRPGBattleActionStage stage)
    {
        if (value == null || value.Owner != lease.Owner || value.Stage != stage || value.StepOrdinal != 0)
            Fail("Wave received an unrelated identity/stage/step receipt.");
        if (!Owns) Fail("Wave presentation issue changed mutation ownership.");
        if (Fault.Length == 0) Watch(value, "wave-" + stage);
    }
    private void Watch(PresentationReceipt value, string checkpoint)
    {
        watchdog.Watch(value, issuedAt + timeout, checkpoint, () => Fail("Wave presentation timed out at " + checkpoint));
    }
    private void CheckTimeout() { if (clock.Now - issuedAt >= timeout) Fail("Wave presentation receipt timed out."); }
    private void Fail(string reason)
    {
        if (Fault.Length == 0) Fault = string.IsNullOrEmpty(reason) ? "Wave presentation failed." : reason;
        lifecycle.CancelPending();
    }
    private void BeginClose(bool retract)
    {
        try
        {
            var result = port.CloseWave(identity, retract);
            if (!retract && Fault.Length != 0) return;
            cleanup = result; closingFault = retract;
            if (result == null) Fail("Wave cleanup returned no receipt.");
        }
        catch (Exception error) { Fail("Wave cleanup failed: " + error.Message); }
    }
    private void PollFaultCleanup()
    {
        if (!closingFault) BeginClose(true);
        if (cleanup != null && cleanup.Completion.IsCompleted && cleanup.Completion.Result) FaultCleanupCompleted = true;
    }
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
