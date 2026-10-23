using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class BattlefieldEnemyView : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [SerializeField] private Image portrait;
    [SerializeField] private TMP_Text displayName;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private Slider hpBar;
    [SerializeField] private BattleParticipantStatusWidgets statusWidgets;
    [SerializeField] private Button targetButton;
    [SerializeField] private Outline targetOutline;
    [SerializeField] private Image targetOverlay;
    [SerializeField] private RectTransform scaleTarget;
    [SerializeField] private Image formationTint;
    [SerializeField] private Image formationRule;
    [SerializeField] private GameObject warningRoot;
    [SerializeField] private Image warningBackground;
    [SerializeField] private TMP_Text warningLabel;
    [SerializeField] private RectTransform warningPulseTarget;
    [SerializeField] private GameObject postureBreakIndicator;
    [SerializeField] private GameObject postureStabilityIndicator;
    [SerializeField] private TMP_Text postureStabilityLabel;

    private DRPGBattleParticipantViewModel boundModel;
    private Action<BattleParticipantRuntime> clicked;
    private DRPGBattleTargetFeedback feedback;
    private bool isSelectable;
    private float formationScale = 1f;
    private bool hasBoundWarning;
    private int boundWarningValue = int.MinValue;
    private int warningRuntimeId;
    private Sequence warningPulseSequence;
    private bool hasBoundPostureState;
    private int boundPostureRuntimeId;
    private DRPGPostureBreakState boundPostureState;
    private Sequence postureFeedbackSequence;
    private bool worldPresentationMode;
    private CanvasGroup worldPresentationCanvasGroup;
    private DRPGBattleProjectionLeaseArbiter projectionGuard;
    internal void SetProjectionGuard(DRPGBattleProjectionLeaseArbiter guard) => projectionGuard = guard;
    internal void ApplyLeasedParticipant(BattleProjectedParticipant participant)
    {
        if (hpText != null) hpText.text = "HP " + Mathf.CeilToInt(participant.Hp) + " / " + Mathf.CeilToInt(participant.MaxHp);
        if (hpBar != null) hpBar.SetValueWithoutNotify(participant.Hp / participant.MaxHp);
        statusWidgets?.Apply(participant);
    }

    private static readonly Color WarningTwoColor =
        new Color32(70, 167, 204, 245);
    private static readonly Color WarningOneColor =
        new Color32(237, 173, 57, 250);
    private static readonly Color WarningZeroColor =
        new Color32(255, 45, 94, 255);

    public int BoundRuntimeId => boundModel != null ? boundModel.RuntimeId : 0;
    public bool IsSelectable => isSelectable;
    public DRPGBattleTargetFeedback CurrentFeedback => feedback;
    public DRPGFormationRow BoundFormationRow => boundModel != null
        ? boundModel.FormationRow
        : DRPGFormationRow.Front;
    public float BoundVisualWidth => boundModel != null
        ? boundModel.FormationVisualWidth
        : 1f;
    public RectTransform RectTransform => transform as RectTransform;
    public BattleParticipantRuntime BoundParticipant =>
        boundModel != null ? boundModel.Participant : null;

    public void Bind(
        DRPGBattleParticipantViewModel viewModel,
        Action<BattleParticipantRuntime> onClicked)
        => BindCanonical(viewModel, onClicked, null);

    internal void BindCanonical(DRPGBattleParticipantViewModel viewModel,
        Action<BattleParticipantRuntime> onClicked, BattleCanonicalProjectionPermit permit)
    {
        if (projectionGuard != null && projectionGuard.DeferCanonical("EnemyView.Bind", BoundRuntimeId, permit)) return;
        if (targetButton != null)
            targetButton.onClick.RemoveListener(HandleClick);

        bool previouslyHadWarning = hasBoundWarning;
        int previousWarningValue = boundWarningValue;
        int previousWarningRuntimeId = warningRuntimeId;
        bool samePostureOwner = hasBoundPostureState && viewModel != null &&
            boundPostureRuntimeId == viewModel.RuntimeId;
        DRPGPostureBreakState previousPostureState = boundPostureState;
        boundModel = viewModel;
        clicked = onClicked;
        gameObject.SetActive(viewModel != null);
        if (viewModel == null)
        {
            statusWidgets?.Clear();
            ApplyEnemyWarning(null, false, int.MinValue, 0);
            ResetPosturePresentation();
            SetSelectable(false);
            SetTargetFeedback(DRPGBattleTargetFeedback.None);
            return;
        }

        statusWidgets?.Apply(viewModel.ProjectionBaseline);
        SetPortraitState(DRPGBattlePortraitState.Normal);
        ApplyWorldPresentationMode();
        if (formationTint != null)
            formationTint.sprite = portrait != null ? portrait.sprite : null;
        if (displayName != null)
            displayName.text = viewModel.DisplayName;
        if (hpText != null)
        {
            hpText.text =
                $"HP {Mathf.CeilToInt(viewModel.CurrentHp)} / " +
                $"{Mathf.CeilToInt(viewModel.MaxHp)}";
        }
        if (hpBar != null)
            hpBar.value = viewModel.HpRatio;

        ApplyEnemyWarning(
            viewModel,
            previouslyHadWarning,
            previousWarningValue,
            previousWarningRuntimeId);
        ApplyPosturePresentation(
            viewModel,
            samePostureOwner,
            previousPostureState);

        if (targetButton != null)
            targetButton.onClick.AddListener(HandleClick);
        SetSelectable(false);
        SetTargetFeedback(DRPGBattleTargetFeedback.None);
    }

    public void ApplyFormationLayout(
        Vector2 anchoredPosition,
        float width,
        float scale,
        bool animated)
    {
        RectTransform rect = RectTransform;
        if (rect == null)
            return;
        formationScale = Mathf.Max(0.1f, scale);
        Vector2 size = rect.sizeDelta;
        size.x = Mathf.Max(40f, width);
        rect.sizeDelta = size;
        rect.DOKill(false);
        if (animated && gameObject.activeInHierarchy)
        {
            rect.DOAnchorPos(anchoredPosition, 0.24f)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true);
            rect.DOScale(Vector3.one * formationScale, 0.22f)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
        }
        else
        {
            rect.anchoredPosition = anchoredPosition;
            rect.localScale = Vector3.one * formationScale;
        }
        ApplyTargetFeedbackVisual(feedback);
    }

    public void SetFormationOverlayVisible(bool visible)
    {
        if (formationRule != null)
        {
            formationRule.enabled = visible && boundModel != null &&
                !worldPresentationMode;
            formationRule.color = DRPGFormationColorPalette.Resolve(
                BoundFormationRow,
                0.95f);
        }
        if (formationTint != null)
        {
            formationTint.enabled = visible && boundModel != null &&
                !worldPresentationMode;
            formationTint.color = DRPGFormationColorPalette.Resolve(
                BoundFormationRow,
                0.34f);
        }
    }

    public void SetWorldPresentationMode(bool enabled)
    {
        worldPresentationMode = enabled;
        ApplyWorldPresentationMode();
    }

    public void SetSelectable(bool value)
    {
        isSelectable = value && boundModel != null &&
            boundModel.IsAlive;
        if (targetButton != null)
            targetButton.interactable = isSelectable;
    }

    public void SetTargetFeedback(DRPGBattleTargetFeedback value)
    {
        feedback = value;
        ApplyTargetFeedbackVisual(feedback);
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
            BattleImpactAnchorKind.EnemyField,
            scaleTarget != null ? scaleTarget : transform as RectTransform,
            portrait,
            SetPortraitState);
        return true;
    }

    public void SetPortraitState(DRPGBattlePortraitState state)
    {
        if (state == DRPGBattlePortraitState.Normal && boundModel != null &&
            boundModel.LowHpState == DRPGLowHpPresentationState.DamageBroken)
        {
            state = DRPGBattlePortraitState.Broken;
        }
        DRPGPortraitImageBinder.Bind(portrait, boundModel, false, state);
        if (formationTint != null)
            formationTint.sprite = portrait != null ? portrait.sprite : null;
        ApplyWorldPresentationMode();
    }

    private void ApplyWorldPresentationMode()
    {
        if (portrait != null)
            portrait.enabled = !worldPresentationMode;
        if (formationTint != null && worldPresentationMode)
            formationTint.enabled = false;
        if (formationRule != null && worldPresentationMode)
            formationRule.enabled = false;
        if (worldPresentationCanvasGroup == null)
        {
            worldPresentationCanvasGroup = GetComponent<CanvasGroup>();
            if (worldPresentationCanvasGroup == null)
                worldPresentationCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        worldPresentationCanvasGroup.alpha = worldPresentationMode ? 0f : 1f;
        worldPresentationCanvasGroup.blocksRaycasts = !worldPresentationMode;
        worldPresentationCanvasGroup.interactable = !worldPresentationMode;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isSelectable && feedback == DRPGBattleTargetFeedback.None)
        {
            ApplyTargetFeedbackVisual(DRPGBattleTargetFeedback.Hovered);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ApplyTargetFeedbackVisual(feedback);
    }

    private void HandleClick()
    {
        if (!isSelectable || boundModel == null)
            return;

        SetTargetFeedback(DRPGBattleTargetFeedback.Selected);
        clicked?.Invoke(boundModel.Participant);
    }

    private void OnDisable()
    {
        if (targetButton != null)
            targetButton.onClick.RemoveListener(HandleClick);
        warningPulseSequence?.Kill(false);
        warningPulseSequence = null;
        postureFeedbackSequence?.Kill(false);
        postureFeedbackSequence = null;
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

    private void ApplyEnemyWarning(
        DRPGBattleParticipantViewModel viewModel,
        bool previouslyHadWarning,
        int previousWarningValue,
        int previousWarningRuntimeId)
    {
        bool visible = viewModel != null && viewModel.HasEnemyWarning;
        hasBoundWarning = visible;
        boundWarningValue = visible
            ? Mathf.Max(0, viewModel.EnemyWarningValue)
            : int.MinValue;
        warningRuntimeId = visible ? viewModel.RuntimeId : 0;

        if (warningRoot != null)
            warningRoot.SetActive(visible);
        if (!visible)
        {
            warningPulseSequence?.Kill(false);
            warningPulseSequence = null;
            if (warningLabel != null)
                warningLabel.text = string.Empty;
            return;
        }

        if (warningLabel != null)
            warningLabel.text = boundWarningValue.ToString();
        if (warningBackground != null)
        {
            warningBackground.color = boundWarningValue <= 0
                ? WarningZeroColor
                : boundWarningValue == 1
                    ? WarningOneColor
                    : WarningTwoColor;
        }

        bool enteredWarningZero = boundWarningValue == 0 &&
            (!previouslyHadWarning ||
             previousWarningRuntimeId != warningRuntimeId ||
             previousWarningValue != 0);
        if (enteredWarningZero)
            PlayWarningZeroPulse();
    }

    private void PlayWarningZeroPulse()
    {
        RectTransform target = warningPulseTarget ??
            (warningRoot != null
                ? warningRoot.transform as RectTransform
                : null);
        if (target == null)
            return;

        warningPulseSequence?.Kill(false);
        target.localScale = Vector3.one;
        warningPulseSequence = DOTween.Sequence()
            .SetUpdate(true)
            .Append(target.DOScale(1.16f, 0.08f).SetEase(Ease.OutQuad))
            .Append(target.DOScale(1f, 0.14f).SetEase(Ease.OutBack));
    }

    private void ApplyTargetFeedbackVisual(
        DRPGBattleTargetFeedback value)
    {
        RectTransform target = scaleTarget != null
            ? scaleTarget
            : transform as RectTransform;
        DRPGBattleTargetFeedbackVisual.Apply(
            targetOutline,
            targetOverlay,
            target,
            value);
        if (target != null)
        {
            float feedbackScale = value == DRPGBattleTargetFeedback.None
                ? 1f
                : value == DRPGBattleTargetFeedback.Hovered ? 1.02f : 1.04f;
            target.localScale = Vector3.one * formationScale * feedbackScale;
        }
    }
}
