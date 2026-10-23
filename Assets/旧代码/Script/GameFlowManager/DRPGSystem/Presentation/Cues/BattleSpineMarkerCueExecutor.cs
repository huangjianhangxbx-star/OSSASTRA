using System;
using UnityEngine;

public sealed class BattleSpineMarkerCueExecutor :
    MonoBehaviour,
    IBattlePresentationCueExecutor
{
    private IBattleSpineMarkerGateway gateway;
    private DRPGBattleSession session;

    public BattlePresentationCueKind Kind =>
        BattlePresentationCueKind.SpineMarker;

    public void Bind(
        IBattleSpineMarkerGateway markerGateway,
        DRPGBattleSession battleSession)
    {
        gateway = markerGateway;
        session = battleSession;
    }

    public void Unbind()
    {
        gateway = null;
        session = null;
    }

    public IBattlePresentationHandle Play(
        BattlePresentationCueSO cue,
        BattlePresentationRequest request)
    {
        // Standard playback is bound by BattleAnimationPlaybackRouter. This
        // executor's live rule bridge exists exclusively for the Legacy route.
        if (request != null && request.IsStandardBasicAttackLeaf)
            return Settled(BattlePresentationOutcome.Skipped);
        DRPGBattleSession capturedSession = session;
        DRPGBattlePreparedAction capturedAction = capturedSession?.ActiveAction;
        if (!(cue is BattleSpineMarkerCueSO markerCue) ||
            request == null || gateway == null || capturedSession == null ||
            capturedAction == null ||
            !Matches(request.Scope, capturedAction.Identity) ||
            capturedSession.CurrentActionIdentity != capturedAction.Identity)
        {
            return Settled(BattlePresentationOutcome.Skipped);
        }

        ActionIdentity capturedIdentity = capturedAction.Identity;
        int actorRuntimeId = capturedAction.ActorRuntimeId;
        bool callbackConsumed = false;
        void SettleRuleBoundary()
        {
            if (callbackConsumed)
                return;
            callbackConsumed = true;
            ApplyRuleBoundary(
                markerCue,
                capturedSession,
                capturedAction,
                capturedIdentity);
        }

        return gateway.WaitFor(
            request.Scope,
            actorRuntimeId,
            markerCue.Marker,
            markerCue.TimeoutSeconds,
            SettleRuleBoundary,
            SettleRuleBoundary);
    }

    private void ApplyRuleBoundary(
        BattleSpineMarkerCueSO cue,
        DRPGBattleSession capturedSession,
        DRPGBattlePreparedAction capturedAction,
        ActionIdentity capturedIdentity)
    {
        if (!ReferenceEquals(session, capturedSession) ||
            !ReferenceEquals(capturedSession.ActiveAction, capturedAction) ||
            capturedSession.CurrentActionIdentity != capturedIdentity ||
            capturedAction.Identity != capturedIdentity ||
            capturedAction.LegacyMutationPort == null ||
            !capturedAction.LegacyMutationPort.IsActive)
            return;

        if (string.Equals(
                cue.MarkerName,
                BattleSpineMarkerNames.Impact,
                StringComparison.Ordinal))
        {
            if (!capturedAction.LegacyMutationPort.TryCommitPartition(
                    capturedAction,
                    cue.MarkerIndex,
                    out _,
                    out string error))
            {
                throw new InvalidOperationException(error);
            }
            return;
        }

        if (string.Equals(
            cue.MarkerName,
            BattleSpineMarkerNames.PresentationEnd,
            StringComparison.Ordinal) &&
            capturedAction.ImpactLedger.PendingIndices.Count > 0)
        {
            if (!capturedAction.LegacyMutationPort.TryResolve(
                    capturedAction,
                    out _,
                    out string error))
                throw new InvalidOperationException(error);
        }
    }

    private static bool Matches(
        BattlePresentationScope scope,
        ActionIdentity identity)
    {
        return identity.IsValid &&
            scope.BattleGeneration == identity.BattleGeneration &&
            scope.TurnSerial == identity.TurnSerial &&
            scope.ActionId == identity.ActionId;
    }

    private static IBattlePresentationHandle Settled(
        BattlePresentationOutcome outcome)
    {
        var handle = new BattlePresentationHandle();
        handle.TrySettle(outcome);
        return handle;
    }
}
