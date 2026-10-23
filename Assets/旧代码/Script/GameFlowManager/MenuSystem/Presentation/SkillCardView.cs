using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class SkillCardView : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [SerializeField] private Image background;
    [SerializeField] private Image glow;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;

    private SkillLoadoutPageView owner;
    private DRPGSkillSO skill;
    private int sourceSlot = -1;
    private GameMenuTooltipView tooltip;

    public DRPGSkillSO Skill => skill;
    public int SourceSlot => sourceSlot;

    public void Bind(
        SkillLoadoutPageView page,
        DRPGSkillSO value,
        int slot,
        GameMenuTooltipView tooltipView)
    {
        owner = page;
        skill = value;
        sourceSlot = slot;
        tooltip = tooltipView;
        label?.SetText(skill != null ? skill.DisplayName : "EMPTY");
        if (icon != null)
        {
            icon.sprite = skill != null ? skill.Icon : null;
            icon.enabled = icon.sprite != null;
        }
        if (background != null)
            background.color = skill != null
                ? new Color(0.31f, 0.018f, 0.105f, 0.99f)
                : new Color(0.045f, 0.025f, 0.045f, 0.92f);
        if (glow != null)
        {
            glow.gameObject.SetActive(skill != null);
            glow.color = new Color(1f, 0.05f, 0.36f, 0.52f);
        }
        if (label != null)
            label.color = skill != null
                ? new Color(0.98f, 0.95f, 0.94f, 1f)
                : new Color(0.48f, 0.40f, 0.44f, 1f);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (skill != null)
            owner?.BeginDrag(skill, sourceSlot, (RectTransform)transform);
    }

    public void OnDrag(PointerEventData eventData)
    {
        owner?.UpdateDrag(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        owner?.EndDrag();
    }

    public void OnPointerEnter(PointerEventData eventData) =>
        tooltip?.ShowSkill(skill);

    public void OnPointerExit(PointerEventData eventData) => tooltip?.Hide();
}
