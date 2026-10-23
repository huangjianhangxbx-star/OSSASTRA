using System.Collections.Generic;
using UnityEngine;
using System;

public sealed class BattlePartyHud : MonoBehaviour
{
    [SerializeField] private List<BattlePartyMemberView> memberViews =
        new List<BattlePartyMemberView>();
    private bool listenersBound;
    private DRPGBattleProjectionLeaseArbiter projectionGuard;
    internal void SetProjectionGuard(DRPGBattleProjectionLeaseArbiter guard)
    {
        projectionGuard = guard;
        foreach (var view in memberViews) view?.SetProjectionGuard(guard);
    }
    internal void ApplyLeasedParticipant(BattleProjectedParticipant participant)
    {
        foreach (var view in memberViews)
            if (view != null && view.BoundRuntimeId == participant.RuntimeId)
                view.ApplyLeasedParticipant(participant);
    }

    public event Action<int> TargetClicked;

    public void Bind(IReadOnlyList<DRPGBattleParticipantViewModel> members)
        => BindCanonical(members, null);

    internal void BindCanonical(IReadOnlyList<DRPGBattleParticipantViewModel> members,
        BattleCanonicalProjectionPermit permit)
    {
        if (projectionGuard != null &&
            projectionGuard.DeferCanonical("PartyHud.Bind", 0, permit)) return;
        BindListeners();
        for (int i = 0; i < memberViews.Count; i++)
        {
            DRPGBattleParticipantViewModel model =
                members != null && i < members.Count ? members[i] : null;
            memberViews[i]?.BindCanonical(model, permit);
        }
    }

    public void SetTargetingEnabled(bool enabled)
    {
        BindListeners();
        for (int i = 0; i < memberViews.Count; i++)
            memberViews[i]?.SetTargetingEnabled(enabled);
    }

    public void SetFormationOverlayVisible(bool visible)
    {
        for (int i = 0; i < memberViews.Count; i++)
            memberViews[i]?.SetFormationOverlayVisible(visible);
    }

    private void BindListeners()
    {
        if (listenersBound)
            return;
        for (int i = 0; i < memberViews.Count; i++)
        {
            if (memberViews[i] != null)
                memberViews[i].TargetClicked += OnMemberTargetClicked;
        }
        listenersBound = true;
    }

    private void OnMemberTargetClicked(int runtimeId)
    {
        TargetClicked?.Invoke(runtimeId);
    }

    private void OnDestroy()
    {
        if (!listenersBound)
            return;
        for (int i = 0; i < memberViews.Count; i++)
        {
            if (memberViews[i] != null)
                memberViews[i].TargetClicked -= OnMemberTargetClicked;
        }
    }

    public void SetTargetFeedback(
        int runtimeId,
        DRPGBattleTargetFeedback feedback)
    {
        for (int i = 0; i < memberViews.Count; i++)
        {
            BattlePartyMemberView view = memberViews[i];
            if (view != null && view.BoundRuntimeId == runtimeId)
                view.SetTargetFeedback(feedback);
        }
    }

    public void ClearTargetFeedback()
    {
        for (int i = 0; i < memberViews.Count; i++)
            memberViews[i]?.SetTargetFeedback(DRPGBattleTargetFeedback.None);
    }

    public bool TryResolveImpactAnchor(
        int runtimeId,
        out BattleImpactAnchor anchor)
    {
        for (int i = 0; i < memberViews.Count; i++)
        {
            BattlePartyMemberView view = memberViews[i];
            if (view != null && view.TryResolveImpactAnchor(runtimeId, out anchor))
                return true;
        }

        anchor = null;
        return false;
    }
}
