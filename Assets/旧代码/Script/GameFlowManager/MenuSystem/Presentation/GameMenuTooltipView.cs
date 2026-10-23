using DG.Tweening;
using TMPro;
using UnityEngine;

public sealed class GameMenuTooltipView : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    private Tween ownedTween;

    public void ShowSkill(DRPGSkillSO skill)
    {
        if (skill == null)
        {
            Hide();
            return;
        }
        Show(skill.DisplayName,
            skill.Description +
            $"\n\nPOWER {skill.Power:0}   MP {skill.ManaCost:0}" +
            $"\nTARGET {skill.TargetRule}   TYPE {skill.Purpose}");
    }

    public void ShowNode(DRPGSkillTreeNodeDefinition node)
    {
        if (node == null)
        {
            Hide();
            return;
        }
        string reward = node.RewardKind == DRPGSkillTreeRewardKind.Skill &&
                        node.Skill != null
            ? node.Skill.DisplayName
            : node.PassiveModifier != null
                ? node.PassiveModifier.DisplayName
                : "—";
        Show(node.DisplayName,
            node.Description + $"\n\nCOST {node.PointCost}   REWARD {reward}");
    }

    public void Show(string title, string body)
    {
        titleText?.SetText(title ?? string.Empty);
        bodyText?.SetText(body ?? string.Empty);
        gameObject.SetActive(true);
        ownedTween?.Kill(false);
        canvasGroup.alpha = 0f;
        ownedTween = canvasGroup.DOFade(1f, 0.1f).SetUpdate(true);
    }

    public void Hide()
    {
        ownedTween?.Kill(false);
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        ownedTween?.Kill(false);
    }
}
