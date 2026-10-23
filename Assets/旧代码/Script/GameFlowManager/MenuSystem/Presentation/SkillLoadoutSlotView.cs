using UnityEngine;
using UnityEngine.EventSystems;

public sealed class SkillLoadoutSlotView : MonoBehaviour, IDropHandler
{
    [SerializeField] private int slotIndex;
    [SerializeField] private SkillCardView card;
    private SkillLoadoutPageView owner;

    public int SlotIndex => slotIndex;

    public void Bind(
        SkillLoadoutPageView page,
        int index,
        DRPGSkillSO skill,
        GameMenuTooltipView tooltip)
    {
        owner = page;
        slotIndex = index;
        card?.Bind(page, skill, index, tooltip);
    }

    public void OnDrop(PointerEventData eventData)
    {
        owner?.DropOnSlot(slotIndex);
    }
}
