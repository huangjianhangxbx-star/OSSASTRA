using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

// Action-only restore. No roster release, formation reconciliation, mutation,
// settlement or scheduler authority is reachable from this barrier.
internal sealed class BattleReturnBarrier : IDisposable
{
    private readonly DRPGBattleActionProjectionLease lease;
    private readonly IDRPGBattlePresentationClock clock;
    private readonly Camera camera;
    private readonly BattleActorMotionController motion;
    private readonly BattleCameraStateSnapshot cameraHome;
    private readonly List<PresentationReceipt> participants = new List<PresentationReceipt>();
    private readonly Sequence cameraTween;
    private readonly BattleEnvironmentRestore.Transition environment;
    private readonly float duration;
    private BattleHomeRenderedDefeatBarrier homeRendered;
    private readonly Func<bool> validateDefeats;
    private readonly BattleCloseupScope closeup;
    private bool disposed;
    private readonly HashSet<int> defeatedAtReturn = new HashSet<int>();
    internal bool IncludesDefeat(int runtimeId) => defeatedAtReturn.Contains(runtimeId);

    internal BattleReturnBarrier(ActionIdentity action, DRPGBattleActionProjectionLease lease,
        BattleActorMotionController motion, Camera camera, BattleCameraStateSnapshot cameraHome,
        IDRPGBattlePresentationClock clock, float duration = .38f, Func<bool> validateDefeats = null)
        : this(action, lease, motion, camera, cameraHome, clock, duration, validateDefeats, null)
    {
    }

    internal BattleReturnBarrier(ActionIdentity action, DRPGBattleActionProjectionLease lease,
        BattleActorMotionController motion, Camera camera, BattleCameraStateSnapshot cameraHome,
        IDRPGBattlePresentationClock clock, float duration, Func<bool> validateDefeats,
        BattleCloseupScope closeup)
    {
        this.lease = lease ?? throw new ArgumentNullException(nameof(lease));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.camera = camera; this.cameraHome = cameraHome; this.duration = duration; this.motion = motion;
        this.validateDefeats = validateDefeats;
        this.closeup = closeup;
        Receipt = new PresentationReceipt(action, DRPGBattleActionStage.Return);
        CameraReceipt = new PresentationReceipt(action, DRPGBattleActionStage.Return);
        EnvironmentReceipt = new PresentationReceipt(action, DRPGBattleActionStage.Return);
        StartedAt = clock.Now;
        if (camera == null || !lease.IsActive || lease.Owner.Action != action || motion == null ||
            float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0f || camera.orthographic != cameraHome.Orthographic)
        { Fail("Return requires the matching projection, camera mode and approved duration."); return; }
        if (!lease.TryOwnPresentation(this)) { Fail("Return projection lifetime is unavailable."); return; }
        environment = lease.BeginEnvironmentReturn(this);
        if (environment == null || !environment.IsValid) { Fail("Frozen environment bindings are unavailable."); return; }
        // Snapshot the actual motion owner's moved set, never the live roster.
        Participants = motion.DisplacedParticipants;
        foreach (var actor in lease.LockBaseline.Actors)
            if (lease.TryGetView(actor.RuntimeId, out _) && lease.GetPresented(actor.RuntimeId).DefeatPending)
                defeatedAtReturn.Add(actor.RuntimeId);
        // The tactical displaced set remains exact. Undisplaced spectators also
        // own visual receipts so dim/pose cannot be left behind after the batch.
        foreach (var actor in lease.LockBaseline.Actors)
            if (lease.TryGetView(actor.RuntimeId, out _))
                participants.Add(motion.ReturnHome(new ReturnMotionCommand(action, new ParticipantId(actor.RuntimeId), StartedAt, duration)));
        foreach (var participant in participants)
            if (participant.Completion.IsCompleted && participant.Completion.Result.Kind == PresentationReceiptTerminalKind.Faulted)
            { Fail(participant.Completion.Result.Detail); return; }
        Vector3 position = camera.transform.position; Quaternion rotation = camera.transform.rotation;
        float fov = camera.fieldOfView, size = camera.orthographicSize;
        cameraTween = DOTween.Sequence().SetAutoKill(false).Pause()
            .Append(DOTween.To(() => 0f, progress =>
            {
                if (disposed || !lease.IsActive || camera == null) return;
                camera.transform.SetPositionAndRotation(progress >= 1f ? cameraHome.Position : Vector3.LerpUnclamped(position, cameraHome.Position, progress),
                    progress >= 1f ? cameraHome.Rotation : Quaternion.SlerpUnclamped(rotation, cameraHome.Rotation, progress));
                camera.fieldOfView = progress >= 1f ? cameraHome.FieldOfView : Mathf.LerpUnclamped(fov, cameraHome.FieldOfView, progress);
                camera.orthographicSize = progress >= 1f ? cameraHome.OrthographicSize : Mathf.LerpUnclamped(size, cameraHome.OrthographicSize, progress);
                closeup?.ApplyReturn(progress);
                if (!environment.Apply(progress)) Fail("Environment ownership lost during restore.");
            }, 1f, duration).SetEase(Ease.InOutSine));
    }

    internal PresentationReceipt Receipt { get; }
    internal PresentationReceipt CameraReceipt { get; }
    internal PresentationReceipt EnvironmentReceipt { get; }
    internal IReadOnlyList<ParticipantId> Participants { get; } = Array.Empty<ParticipantId>();
    internal double StartedAt { get; }

    internal void Tick()
    {
        if (disposed || Receipt.Completion.IsCompleted) return;
        if (!lease.IsActive || camera == null || environment == null || !environment.IsValid)
        { Fail("Return projection/camera/environment ownership lost."); return; }
        float elapsed = (float)Math.Max(0d, clock.Now - StartedAt);
        if (!CameraReceipt.Completion.IsCompleted)
        {
            if (cameraTween == null || !cameraTween.IsActive()) { Fail("Owned camera return was interrupted."); return; }
            cameraTween.Goto(Math.Min(elapsed, duration), false);
            if (Receipt.Completion.IsCompleted) return;
            if (elapsed >= duration)
            {
                camera.enabled = cameraHome.Enabled;
                cameraTween.Kill(false);
                CameraReceipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "Camera at frozen home."));
                EnvironmentReceipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "Environment restored without semantic rewind."));
            }
        }
        foreach (var receipt in participants)
        {
            if (!receipt.Completion.IsCompleted) return;
            if (receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Faulted)
            { Fail(receipt.Completion.Result.Detail); return; }
        }
        if (!CameraReceipt.Completion.IsCompleted || !EnvironmentReceipt.Completion.IsCompleted) return;
        if (homeRendered == null) homeRendered = new BattleHomeRenderedDefeatBarrier(lease.Owner, lease, clock,
            () => validateDefeats == null || validateDefeats());
        homeRendered.Tick();
        if (!homeRendered.Receipt.Completion.IsCompleted) return;
        Receipt.TryComplete(homeRendered.Receipt.Completion.Result);
    }

    private void Fail(string reason)
    {
        cameraTween?.Kill(false);
        foreach (var participant in participants)
            if (!participant.Completion.IsCompleted) motion?.CancelOwnedReceipt(participant);
        var terminal = new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, reason);
        CameraReceipt.TryComplete(terminal); EnvironmentReceipt.TryComplete(terminal); Receipt.TryComplete(terminal);
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        homeRendered?.Dispose();
        Fail("Return owner closed before finalization.");
    }
}
