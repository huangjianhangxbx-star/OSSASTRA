using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleDamageBrokenCutInView : MonoBehaviour
{
    [SerializeField] private RectTransform panel;
    [SerializeField] private Image portrait;
    [SerializeField] private TMP_Text displayName;
    [SerializeField] private CanvasGroup canvasGroup;

    public RectTransform Panel => panel != null ? panel : transform as RectTransform;
    public CanvasGroup CanvasGroup => canvasGroup;

    public void Bind(
        BattleParticipantPresentationSnapshot participant,
        float width,
        float height)
    {
        RectTransform rect = Panel;
        if (rect != null)
            rect.sizeDelta = new Vector2(width, height);

        Sprite sprite = participant != null
            ? participant.DamageBrokenPortrait != null
                ? participant.DamageBrokenPortrait
                : participant.FullPortrait
            : null;
        if (portrait != null)
        {
            portrait.sprite = sprite;
            portrait.enabled = sprite != null;
            portrait.preserveAspect = true;
        }
        if (displayName != null)
        {
            displayName.text = participant != null
                ? participant.DisplayName
                : string.Empty;
        }
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
