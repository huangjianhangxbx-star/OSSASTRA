using DG.Tweening;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleInteractiveCombatView : MonoBehaviour
{
    [Header("Interaction panel")]
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private RectTransform panelTransform;
    [SerializeField] private Image veilImage;
    [SerializeField] private Image frameImage;
    [SerializeField] private TMP_Text alertText;
    [SerializeField] private TMP_Text modeText;
    [SerializeField] private TMP_Text targetText;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private TMP_Text resourceText;
    [SerializeField] private TMP_Text postureRiskText;
    [SerializeField] private TMP_Text durabilityRiskText;
    [SerializeField] private Image timerFill;
    [SerializeField] private Image postureFill;
    [SerializeField] private Image defenseCrest;
    [SerializeField] private RectTransform timingPulse;
    [SerializeField] private Image timingPulseImage;
    [SerializeField] private RectTransform crosshair;
    [SerializeField] private RectTransform slashPreview;

    [Header("Persistent telegraph")]
    [SerializeField] private CanvasGroup telegraphGroup;
    [SerializeField] private TMP_Text telegraphText;

    [Header("Motion")]
    [SerializeField, Min(0.01f)] private float enterSeconds = 0.22f;
    [SerializeField, Min(0.01f)] private float exitSeconds = 0.16f;

    private static readonly Color DangerColor = new Color(0.95f, 0.04f, 0.22f, 1f);
    private static readonly Color GuardColor = new Color(0.10f, 0.82f, 1f, 1f);
    private static readonly Color PerfectColor = new Color(1f, 0.78f, 0.12f, 1f);
    private static readonly Color StrikeColor = new Color(1f, 0.05f, 0.48f, 1f);

    private Sequence panelSequence;
    private Sequence feedbackSequence;
    private Sequence telegraphSequence;
    private BattleImpactAnchor targetAnchor;
    private DRPGCombatantProfileSO targetProfile;
    private readonly List<BattleFreeStrikeTargetAnchor> freeStrikeTargets =
        new List<BattleFreeStrikeTargetAnchor>();
    private readonly List<DRPGFreeStrikeTargetSurface> targetSurfaces =
        new List<DRPGFreeStrikeTargetSurface>();
    private DRPGBattleInteractionKind activeKind;

    public bool IsVisible => panelGroup != null &&
        panelGroup.gameObject.activeSelf && panelGroup.alpha > 0.001f;

    public void ShowDefense(
        DRPGBattlePreparedAction action,
        BattleParticipantRuntime actor,
        BattleParticipantRuntime target,
        BattleImpactAnchor anchor)
    {
        ShowBase(DRPGBattleInteractionKind.Defense, anchor, target?.Profile);
        SetText(alertText, "DANGER  //  SPECIAL ATTACK");
        SetText(modeText, "防御时机");
        SetText(targetText, target != null
            ? $"{target.Profile.DisplayName}  被锁定"
            : "目标失效");
        SetText(promptText, "SPACE  //  防御");
        SetColor(DangerColor);
        SetActive(crosshair, false);
        SetActive(slashPreview, false);
        SetActive(defenseCrest, true);
        SetActive(timingPulse, true);
        PositionOnAnchor(defenseCrest != null
            ? defenseCrest.rectTransform
            : null, anchor);
        PositionOnAnchor(timingPulse, anchor);
        ClearTelegraph();
    }

    public void ShowFreeStrike(
        DRPGBattlePreparedAction action,
        BattleParticipantRuntime actor,
        IReadOnlyList<BattleFreeStrikeTargetAnchor> targets)
    {
        ShowBase(DRPGBattleInteractionKind.FreeStrike, null, null);
        freeStrikeTargets.Clear();
        if (targets != null)
        {
            for (int i = 0; i < targets.Count; i++)
                freeStrikeTargets.Add(targets[i]);
        }
        SetText(alertText, "SPECIAL ATTACK  //  FREE STRIKE");
        SetText(modeText, action?.Command?.Skill?.FreeStrikeShape ==
            DRPGFreeStrikeShape.Line ? "自由斩击" : "自由射击");
        SetText(targetText, "FULL FIELD  //  FREE AIM");
        SetText(promptText, "LMB  //  连续攻击");
        SetColor(StrikeColor);
        SetActive(crosshair, true);
        SetActive(defenseCrest, false);
        SetActive(
            slashPreview,
            action?.Command?.Skill?.FreeStrikeShape == DRPGFreeStrikeShape.Line);
        SetActive(timingPulse, false);
    }

    public void UpdateWindow(
        BattleInteractiveActionRun run,
        float actorPostureNormalized)
    {
        if (run == null || panelGroup == null)
            return;

        if (timerFill != null)
            timerFill.fillAmount = Mathf.Clamp01(run.NormalizedRemaining);
        if (postureFill != null)
            postureFill.fillAmount = Mathf.Clamp01(actorPostureNormalized);
        SetText(countdownText, Mathf.Max(0f, run.Duration - run.Elapsed)
            .ToString("0.00"));

        if (run.Kind == DRPGBattleInteractionKind.Defense && timingPulse != null)
        {
            float lead = Mathf.Max(0f, run.NextGuardImpactTime - run.Elapsed);
            float normalized = Mathf.Clamp01(lead / run.Skill.GuardLeadSeconds);
            timingPulse.localScale = Vector3.one * Mathf.Lerp(0.72f, 1.55f, normalized);
            if (timingPulseImage != null)
            {
                timingPulseImage.color = lead <= run.Skill.PerfectLeadSeconds
                    ? PerfectColor
                    : GuardColor;
            }
        }
    }

    public bool TryCreateStrikeSample(
        Vector2 screenPosition,
        DRPGResolvedFreeStrikeSpec spec,
        out DRPGFreeStrikeSample sample,
        out DRPGFreeStrikeBodyContact[] contacts)
    {
        sample = default;
        contacts = System.Array.Empty<DRPGFreeStrikeBodyContact>();
        if (panelTransform == null)
        {
            return false;
        }

        Camera camera = ResolveCanvasCamera(panelTransform);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                panelTransform,
                screenPosition,
                camera,
                out Vector2 localPoint))
        {
            return false;
        }

        Rect rect = panelTransform.rect;
        Vector2 normalized = new Vector2(
            rect.width > 0.001f ? (localPoint.x - rect.xMin) / rect.width : 0.5f,
            rect.height > 0.001f ? (localPoint.y - rect.yMin) / rect.height : 0.5f);
        Vector2 direction = new Vector2(1f, 0.16f).normalized;
        sample = new DRPGFreeStrikeSample(
            spec.Shape,
            normalized,
            direction,
            spec.LineLength,
            spec.LineWidth);
        BuildBattlefieldTargetSurfaces(rect);
        contacts = DRPGFreeStrikeContactResolver.Resolve(
            sample,
            spec,
            targetSurfaces);
        UpdatePointerVisual(screenPosition, spec.Shape, camera);
        return true;
    }

    public void ShowGuardResult(DRPGGuardGrade grade)
    {
        switch (grade)
        {
            case DRPGGuardGrade.Perfect:
                ShowFeedback("PERFECT GUARD", PerfectColor, 1.18f);
                break;
            case DRPGGuardGrade.Guard:
                ShowFeedback("GUARD", GuardColor, 1.08f);
                break;
            default:
                ShowFeedback("BREAK THROUGH", DangerColor, 1.12f);
                break;
        }
    }

    public void ShowStrikeResult(DRPGStrikeGrade grade)
    {
        switch (grade)
        {
            case DRPGStrikeGrade.Weak:
                ShowFeedback("WEAK POINT", PerfectColor, 1.15f);
                break;
            case DRPGStrikeGrade.Part:
                ShowFeedback("PART HIT", GuardColor, 1.11f);
                break;
            case DRPGStrikeGrade.Body:
                ShowFeedback("HIT", StrikeColor, 1.07f);
                break;
            default:
                ShowFeedback("MISS", Color.white, 1.03f);
                break;
        }
    }

    public void ShowInputRejected(string reason)
    {
        ShowFeedback(
            string.IsNullOrWhiteSpace(reason) ? "INPUT REJECTED" : reason,
            DangerColor,
            1.04f);
    }

    public void UpdateInputResources(
        DRPGInteractiveInputResourceSnapshot snapshot)
    {
        if (snapshot == null)
        {
            SetText(resourceText, string.Empty);
            SetText(postureRiskText, string.Empty);
            SetText(durabilityRiskText, string.Empty);
            return;
        }

        SetText(resourceText, snapshot.HasProgress
            ? "CHARGE  " + snapshot.Progress.CurrentValue.ToString("0.##") +
              " / " + snapshot.Progress.Capacity.ToString("0.##")
            : "CHARGE  —");
        SetText(postureRiskText,
            "POSTURE  " + snapshot.CurrentPosture.ToString("0.##") +
            " / " + snapshot.MaximumPosture.ToString("0.##"));
        SetText(durabilityRiskText, snapshot.HasWeaponDurability
            ? "DURABILITY  " + snapshot.ProjectedWeaponDurability +
              " / " + snapshot.MaximumWeaponDurability
            : "DURABILITY  —");
    }

    public void ShowTelegraph(
        BattleParticipantRuntime actor,
        BattleParticipantRuntime target,
        DRPGSkillSO skill)
    {
        if (telegraphGroup == null)
            return;
        KillTelegraphSequence();
        telegraphGroup.gameObject.SetActive(true);
        telegraphGroup.alpha = 0f;
        RectTransform telegraphRect =
            telegraphGroup.transform as RectTransform;
        if (telegraphRect != null)
            telegraphRect.localScale = new Vector3(1.08f, 0.72f, 1f);
        SetText(
            telegraphText,
            $"⚠ {actor?.Profile?.DisplayName ?? "ENEMY"} 锁定 " +
            $"{target?.Profile?.DisplayName ?? "TARGET"}  //  " +
            (skill?.DisplayName ?? "SPECIAL"));
        telegraphSequence = DOTween.Sequence().SetUpdate(true)
            .Append(telegraphGroup.DOFade(1f, 0.10f));
        if (telegraphRect != null)
        {
            telegraphSequence.Join(
                telegraphRect.DOScale(1f, 0.22f).SetEase(Ease.OutBack));
            telegraphSequence.Append(
                telegraphRect.DOPunchScale(
                    new Vector3(0.025f, 0.08f, 0f),
                    0.34f,
                    5,
                    0.35f));
        }
    }

    public void ClearTelegraph()
    {
        if (telegraphGroup == null)
            return;
        KillTelegraphSequence();
        telegraphGroup.alpha = 0f;
        telegraphGroup.gameObject.SetActive(false);
    }

    public void HideAnimated()
    {
        KillPanelSequence();
        if (panelGroup == null)
            return;
        panelSequence = DOTween.Sequence().SetUpdate(true)
            .Append(panelGroup.DOFade(0f, exitSeconds).SetEase(Ease.InQuad));
        if (panelTransform != null)
        {
            panelSequence.Join(
                panelTransform.DOScale(1.04f, exitSeconds)
                    .SetEase(Ease.InQuad));
        }
        panelSequence.OnComplete(HideImmediate);
    }

    public void HideImmediate()
    {
        KillPanelSequence();
        KillFeedbackSequence();
        targetAnchor = null;
        targetProfile = null;
        freeStrikeTargets.Clear();
        targetSurfaces.Clear();
        if (panelGroup != null)
        {
            panelGroup.alpha = 0f;
            panelGroup.blocksRaycasts = false;
            panelGroup.interactable = false;
            panelGroup.gameObject.SetActive(false);
        }
        if (panelTransform != null)
            panelTransform.localScale = Vector3.one;
    }

    private void ShowBase(
        DRPGBattleInteractionKind kind,
        BattleImpactAnchor anchor,
        DRPGCombatantProfileSO profile)
    {
        HideImmediate();
        activeKind = kind;
        targetAnchor = anchor;
        targetProfile = profile;
        if (panelGroup == null)
            return;

        panelGroup.gameObject.SetActive(true);
        panelGroup.blocksRaycasts = false;
        panelGroup.interactable = false;
        panelGroup.alpha = 0f;
        if (panelTransform != null)
            panelTransform.localScale = new Vector3(0.88f, 1.08f, 1f);
        if (veilImage != null)
        {
            veilImage.color = kind == DRPGBattleInteractionKind.Defense
                ? new Color(0.055f, 0.005f, 0.025f, 0.38f)
                : new Color(0.015f, 0.018f, 0.050f, 0.24f);
        }
        SetText(feedbackText, string.Empty);
        panelSequence = DOTween.Sequence().SetUpdate(true)
            .Append(panelGroup.DOFade(1f, enterSeconds * 0.55f));
        if (panelTransform != null)
        {
            panelSequence.Join(
                panelTransform.DOScale(1f, enterSeconds)
                    .SetEase(Ease.OutBack));
        }
    }

    private void ShowFeedback(string value, Color color, float punchScale)
    {
        SetText(feedbackText, value);
        if (feedbackText != null)
            feedbackText.color = color;
        KillFeedbackSequence();
        if (panelTransform == null)
            return;
        feedbackSequence = DOTween.Sequence().SetUpdate(true)
            .Append(panelTransform.DOPunchScale(
                Vector3.one * Mathf.Max(0f, punchScale - 1f),
                0.18f,
                5,
                0.45f));
    }

    private void SetColor(Color color)
    {
        if (frameImage != null)
            frameImage.color = color;
        if (timerFill != null)
            timerFill.color = color;
    }

    private void UpdatePointerVisual(
        Vector2 screenPosition,
        DRPGFreeStrikeShape shape,
        Camera camera)
    {
        RectTransform root = panelTransform;
        if (root == null)
            return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                root,
                screenPosition,
                camera,
                out Vector2 local))
        {
            return;
        }
        if (crosshair != null)
            crosshair.anchoredPosition = local;
        if (slashPreview != null)
        {
            slashPreview.anchoredPosition = local;
            slashPreview.localEulerAngles = new Vector3(0f, 0f, 9f);
            slashPreview.gameObject.SetActive(shape == DRPGFreeStrikeShape.Line);
        }
    }

    private void BuildBattlefieldTargetSurfaces(Rect rootRect)
    {
        targetSurfaces.Clear();
        if (rootRect.width <= 0.001f || rootRect.height <= 0.001f)
            return;

        for (int i = 0; i < freeStrikeTargets.Count; i++)
        {
            BattleFreeStrikeTargetAnchor binding = freeStrikeTargets[i];
            BattleParticipantRuntime participant = binding.Participant;
            RectTransform targetRect = binding.Anchor?.RectTransform;
            if (participant == null || !participant.IsAlive ||
                targetRect == null ||
                !targetRect.gameObject.activeInHierarchy)
            {
                continue;
            }

            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                panelTransform,
                targetRect);
            Rect normalizedTarget = new Rect(
                (bounds.min.x - rootRect.xMin) / rootRect.width,
                (bounds.min.y - rootRect.yMin) / rootRect.height,
                bounds.size.x / rootRect.width,
                bounds.size.y / rootRect.height);
            Rect authoredBody = participant.Profile.FreeStrikeBodyBounds;
            targetSurfaces.Add(new DRPGFreeStrikeTargetSurface(
                participant.RuntimeId,
                ApplyNormalizedSubRect(normalizedTarget, authoredBody)));
        }
    }

    private static Rect ApplyNormalizedSubRect(Rect parent, Rect child)
    {
        return new Rect(
            parent.xMin + child.xMin * parent.width,
            parent.yMin + child.yMin * parent.height,
            child.width * parent.width,
            child.height * parent.height);
    }

    private static Camera ResolveCanvasCamera(RectTransform rect)
    {
        Canvas canvas = rect != null ? rect.GetComponentInParent<Canvas>() : null;
        return canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera;
    }

    private static void PositionOnAnchor(
        RectTransform visual,
        BattleImpactAnchor anchor)
    {
        if (visual == null || anchor?.RectTransform == null)
            return;
        visual.position = anchor.RectTransform.TransformPoint(
            anchor.RectTransform.rect.center);
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
            target.text = value ?? string.Empty;
    }

    private static void SetActive(Component target, bool value)
    {
        if (target != null)
            target.gameObject.SetActive(value);
    }

    private void KillPanelSequence()
    {
        Sequence sequence = panelSequence;
        panelSequence = null;
        if (sequence != null && sequence.IsActive())
            sequence.Kill(false);
    }

    private void KillFeedbackSequence()
    {
        Sequence sequence = feedbackSequence;
        feedbackSequence = null;
        if (sequence != null && sequence.IsActive())
            sequence.Kill(false);
    }

    private void KillTelegraphSequence()
    {
        Sequence sequence = telegraphSequence;
        telegraphSequence = null;
        if (sequence != null && sequence.IsActive())
            sequence.Kill(false);
    }

    private void OnDisable()
    {
        HideImmediate();
        ClearTelegraph();
    }
}
