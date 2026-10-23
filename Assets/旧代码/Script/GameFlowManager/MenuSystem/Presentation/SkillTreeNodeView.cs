using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class SkillTreeNodeView : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [SerializeField] private Image glow;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;
    [SerializeField] private TMP_Text costLabel;

    private DRPGSkillTreeNodeDefinition definition;
    private GameMenuTooltipView tooltip;
    private Tween glowTween;

    public void Bind(
        DRPGSkillTreeNodeDefinition node,
        bool unlocked,
        bool available,
        Action<DRPGSkillTreeNodeDefinition> selected,
        GameMenuTooltipView tooltipView)
    {
        definition = node;
        tooltip = tooltipView;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => selected?.Invoke(definition));
        button.interactable = !unlocked;
        label?.SetText(node.DisplayName);
        costLabel?.SetText(node.PointCost.ToString());

        Sprite rewardIcon = node.RewardKind == DRPGSkillTreeRewardKind.Skill &&
                            node.Skill != null
            ? node.Skill.Icon
            : null;
        if (icon != null)
        {
            icon.sprite = rewardIcon;
            icon.enabled = rewardIcon != null;
        }

        Color lockedColor = new Color(0.045f, 0.035f, 0.055f, 0.98f);
        Color availableColor = new Color(0.30f, 0.025f, 0.11f, 0.99f);
        Color unlockedColor = new Color(0.67f, 0.018f, 0.25f, 1f);
        if (background != null)
            background.color = unlocked
                ? unlockedColor
                : available ? availableColor : lockedColor;
        if (label != null)
            label.color = available || unlocked
                ? new Color(0.98f, 0.95f, 0.94f, 1f)
                : new Color(0.48f, 0.40f, 0.44f, 1f);

        glowTween?.Kill(false);
        if (glow != null)
        {
            glow.gameObject.SetActive(unlocked || available);
            glow.color = new Color(1f, 0.08f, 0.42f, unlocked ? 0.86f : 0.38f);
            if (unlocked)
            {
                glowTween = glow.DOFade(0.3f, 0.72f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine)
                    .SetUpdate(true);
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        tooltip?.ShowNode(definition);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        tooltip?.Hide();
    }

    private void OnDestroy()
    {
        glowTween?.Kill(false);
    }
}
