using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleActorFloatingStatusView : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text actorName;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private Image hpFill;
    [SerializeField] private Image postureFill;
    [SerializeField] private GameObject warningRoot;
    [SerializeField] private GameObject detailRoot;

    public int RuntimeId { get; private set; }

    public void Bind(DRPGBattleParticipantViewModel actor)
    {
        RuntimeId = actor != null ? actor.RuntimeId : 0;
        if (actorName != null)
            actorName.text = actor != null ? actor.DisplayName : string.Empty;
        if (hpText != null)
        {
            hpText.text = actor != null
                ? $"{Mathf.CeilToInt(actor.CurrentHp)}/{Mathf.CeilToInt(actor.MaxHp)}"
                : string.Empty;
        }
        if (hpFill != null)
            hpFill.fillAmount = actor != null ? Mathf.Clamp01(actor.HpRatio) : 0f;
        if (postureFill != null)
        {
            postureFill.fillAmount = actor != null && actor.IsPostureBreakExposed
                ? 0f
                : 1f;
        }
        if (warningRoot != null)
            warningRoot.SetActive(actor != null && actor.HasEnemyWarning);
        if (canvasGroup != null)
            canvasGroup.alpha = actor != null ? 1f : 0f;
    }

    public void SetExpanded(bool expanded)
    {
        if (detailRoot != null)
            detailRoot.SetActive(expanded);
        if (actorName != null)
            actorName.gameObject.SetActive(expanded);
    }
}
