using TMPro;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

public sealed class BattlePartyMemberView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image portrait;
    [SerializeField] private TMP_Text displayName;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private Slider hpBar;
    [SerializeField] private BattleParticipantStatusWidgets statusWidgets;
    [SerializeField] private GameObject currentActorMarker;
    [SerializeField] private Outline targetOutline;
    [SerializeField] private Image targetOverlay;
    [SerializeField] private Image formationUnderlay;
    [SerializeField] private GameObject postureBreakIndicator;
    [SerializeField] private GameObject postureStabilityIndicator;
    [SerializeField] private TMP_Text postureStabilityLabel;

    private DRPGBattleParticipantViewModel boundModel;
    private bool targetingEnabled;
    private bool hasBoundPostureState;
    private int boundPostureRuntimeId;
    private DRPGPostureBreakState boundPostureState;
    private Sequence postureFeedbackSequence;
    private DRPGBattleProjectionLeaseArbiter projectionGuard;
    internal void SetProjectionGuard(DRPGBattleProjectionLeaseArbiter guard) =>
        projectionGuard = guard;
    internal void ApplyLeasedParticipant(BattleProjectedParticipant participant)
    {
        if (hpText != null)
            hpText.text = Mathf.CeilToInt(participant.Hp) + " / " +
                Mathf.CeilToInt(participant.MaxHp);
        if (hpBar != null) hpBar.SetValueWithoutNotify(participant.Hp / participant.MaxHp);
        statusWidgets?.Apply(participant);
    }

    public event Action<int> TargetClicked;

    public int BoundRuntimeId { get; private set; }

    public void Bind(DRPGBattleParticipantViewModel viewModel)
        => BindCanonical(viewModel, null);

    internal void BindCanonical(DRPGBattleParticipantViewModel viewModel,
        BattleCanonicalProjectionPermit permit)
    {
        if (projectionGuard != null &&
            projectionGuard.DeferCanonical("PartyMember.Bind", BoundRuntimeId, permit)) return;
        bool samePostureOwner = hasBoundPostureState && viewModel != null &&
            boundPostureRuntimeId == viewModel.RuntimeId;
        DRPGPostureBreakState previousPostureState = boundPostureState;
        boundModel = viewModel;
        gameObject.SetActive(viewModel != null);
        if (viewModel == null)
        {
            BoundRuntimeId = 0;
            statusWidgets?.Clear();
            ResetPosturePresentation();
            SetTargetFeedback(DRPGBattleTargetFeedback.None);
            return;
        }

        BoundRuntimeId = viewModel.RuntimeId;
        statusWidgets?.Apply(viewModel.ProjectionBaseline);

        SetPortraitStateCanonical(DRPGBattlePortraitState.Normal, permit);
        if (displayName != null)
            displayName.text = viewModel.DisplayName;
        if (hpText != null)
        {
            hpText.text =
                $"{Mathf.CeilToInt(viewModel.CurrentHp)} / " +
                $"{Mathf.CeilToInt(viewModel.MaxHp)}";
        }
        if (hpBar != null)
            hpBar.value = viewModel.HpRatio;
        if (currentActorMarker != null)
            currentActorMarker.SetActive(viewModel.IsCurrentActor);
        if (formationUnderlay != null)
            formationUnderlay.color = DRPGFormationColorPalette.Resolve(
                viewModel.FormationRow,
                0.42f);
        ApplyPosturePresentation(
            viewModel,
            samePostureOwner,
            previousPostureState);
    }

    public void SetFormationOverlayVisible(bool visible)
    {
        if (formationUnderlay != null)
            formationUnderlay.enabled = visible && boundModel != null;
    }

    public void SetTargetFeedback(DRPGBattleTargetFeedback feedback)
    {
        DRPGBattleTargetFeedbackVisual.Apply(
            targetOutline,
            targetOverlay,
            transform as RectTransform,
            feedback);
    }

    public void SetTargetingEnabled(bool enabled)
    {
        targetingEnabled = enabled;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (targetingEnabled && boundModel != null && BoundRuntimeId > 0)
            TargetClicked?.Invoke(BoundRuntimeId);
    }

    public bool TryResolveImpactAnchor(
        int runtimeId,
        out BattleImpactAnchor anchor)
    {
        if (boundModel == null || BoundRuntimeId != runtimeId)
        {
            anchor = null;
            return false;
        }

        anchor = new BattleImpactAnchor(
            runtimeId,
            BattleImpactAnchorKind.PartyHud,
            transform as RectTransform,
            portrait,
            SetPortraitState);
        return true;
    }

    public void SetPortraitState(DRPGBattlePortraitState state)
        => SetPortraitStateCanonical(state, null);

    private void SetPortraitStateCanonical(DRPGBattlePortraitState state,
        BattleCanonicalProjectionPermit permit)
    {
        if (projectionGuard != null &&
            projectionGuard.DeferCanonical("PartyMember.Portrait", BoundRuntimeId, permit)) return;
        if (state == DRPGBattlePortraitState.Normal && boundModel != null &&
            boundModel.LowHpState == DRPGLowHpPresentationState.DamageBroken)
        {
            state = DRPGBattlePortraitState.Broken;
        }
        DRPGPortraitImageBinder.Bind(portrait, boundModel, true, state);
    }

    private void ApplyPosturePresentation(
        DRPGBattleParticipantViewModel viewModel,
        bool sameOwner,
        DRPGPostureBreakState previousState)
    {
        bool exposed = viewModel.IsPostureBreakExposed;
        bool stability = viewModel.PostureBreakState ==
            DRPGPostureBreakState.RecoveryStability;
        if (postureBreakIndicator != null)
            postureBreakIndicator.SetActive(exposed);
        if (postureStabilityIndicator != null)
            postureStabilityIndicator.SetActive(stability);
        if (postureStabilityLabel != null)
        {
            postureStabilityLabel.text = stability
                ? "STABLE " +
                    viewModel.PostureBreakStabilityOwnActionsRemaining
                : string.Empty;
        }

        bool changed = !sameOwner ||
            previousState != viewModel.PostureBreakState;
        hasBoundPostureState = true;
        boundPostureRuntimeId = viewModel.RuntimeId;
        boundPostureState = viewModel.PostureBreakState;
        if (!changed)
            return;

        RectTransform feedbackTarget = exposed
            ? postureBreakIndicator != null
                ? postureBreakIndicator.transform as RectTransform
                : null
            : stability && postureStabilityIndicator != null
                ? postureStabilityIndicator.transform as RectTransform
                : null;
        PlayPostureFeedback(feedbackTarget, exposed);
    }

    private void PlayPostureFeedback(RectTransform target, bool exposed)
    {
        postureFeedbackSequence?.Kill(false);
        postureFeedbackSequence = null;
        if (target == null || !gameObject.activeInHierarchy)
            return;

        target.DOKill(false);
        target.localScale = Vector3.one;
        postureFeedbackSequence = DOTween.Sequence()
            .SetUpdate(true)
            .Append(target.DOScale(exposed ? 1.18f : 1.1f, 0.08f)
                .SetEase(Ease.OutQuad))
            .Append(target.DOScale(1f, 0.16f)
                .SetEase(Ease.OutBack));
    }

    private void ResetPosturePresentation()
    {
        postureFeedbackSequence?.Kill(false);
        postureFeedbackSequence = null;
        hasBoundPostureState = false;
        boundPostureRuntimeId = 0;
        boundPostureState = DRPGPostureBreakState.Stable;
        if (postureBreakIndicator != null)
            postureBreakIndicator.SetActive(false);
        if (postureStabilityIndicator != null)
            postureStabilityIndicator.SetActive(false);
        if (postureStabilityLabel != null)
            postureStabilityLabel.text = string.Empty;
    }

    private void OnDisable()
    {
        postureFeedbackSequence?.Kill(false);
        postureFeedbackSequence = null;
    }
}
