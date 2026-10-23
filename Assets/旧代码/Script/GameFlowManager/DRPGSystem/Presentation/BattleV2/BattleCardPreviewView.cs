using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class BattleCardPreviewView : MonoBehaviour,
    IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private RectTransform motionRoot;
    [SerializeField] private UnityEngine.UI.Image artwork;
    [SerializeField] private TextMeshProUGUI title;
    [SerializeField] private TextMeshProUGUI description;
    [SerializeField] private TextMeshProUGUI targetRule;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float hoverLift = 12f;
    private Vector2 restPosition;
    private Tween hoverTween;
    private bool hasCard;
    private bool isHovered;
    private bool ownsPosition;

    public event Action RightClicked;

    private void Awake()
    {
        if (motionRoot == null)
            motionRoot = transform as RectTransform;
        if (motionRoot != null)
            restPosition = motionRoot.anchoredPosition;
    }

    private void OnDisable()
    {
        RestoreMotion();
    }

    public void Bind(DRPGBattleCardPreviewSO card)
    {
        RestoreMotion();
        hasCard = card != null;
        if (title != null)
            title.text = hasCard ? card.DisplayName : "空卡位";
        if (description != null)
            description.text = hasCard ? card.Description : "";
        if (targetRule != null)
            targetRule.text = hasCard ? TargetLabel(card.TargetRule) : "";
        if (artwork != null)
        {
            artwork.sprite = hasCard ? card.Artwork : null;
            artwork.enabled = hasCard && card.Artwork != null;
        }
        if (canvasGroup != null)
            canvasGroup.alpha = hasCard ? 1f : .34f;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (hasCard && eventData.button == PointerEventData.InputButton.Right)
            RightClicked?.Invoke();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!hasCard || motionRoot == null || isHovered)
            return;
        // Layout groups resolve their children after Awake. Capture the
        // card's actual laid-out position at the interaction boundary rather
        // than restoring every slot to the shared serialized origin.
        if (!ownsPosition)
            restPosition = motionRoot.anchoredPosition;
        ownsPosition = true;
        isHovered = true;
        StopMotion();
        hoverTween = motionRoot.DOAnchorPos(restPosition + Vector2.up * hoverLift, .12f)
            .SetEase(Ease.OutCubic).SetUpdate(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (motionRoot == null || !isHovered || !ownsPosition)
            return;
        isHovered = false;
        StopMotion();
        hoverTween = motionRoot.DOAnchorPos(restPosition, .12f)
            .SetEase(Ease.InCubic).SetUpdate(true)
            .OnComplete(() => { ownsPosition = false; hoverTween = null; });
    }

    private void RestoreMotion()
    {
        StopMotion();
        if (ownsPosition && motionRoot != null)
            motionRoot.anchoredPosition = restPosition;
        ownsPosition = false;
        isHovered = false;
    }

    private void StopMotion()
    {
        if (hoverTween != null && hoverTween.IsActive())
            hoverTween.Kill(false);
        hoverTween = null;
    }

    private static string TargetLabel(BattleCardTargetRule rule)
    {
        switch (rule)
        {
            case BattleCardTargetRule.Ally: return "目标：己方";
            case BattleCardTargetRule.Enemy: return "目标：敌方";
            default: return "目标：任意存活单位";
        }
    }
}
