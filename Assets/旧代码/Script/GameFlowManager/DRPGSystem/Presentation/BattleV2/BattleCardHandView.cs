using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BattleCardHandView : MonoBehaviour
{
    [SerializeField] private List<BattleCardPreviewView> slots =
        new List<BattleCardPreviewView>();

    public event Action<int> CardRightClicked;
    public int VisibleSlotCount => slots.Count;

    public RectTransform GetSlotRect(int slot)
    {
        return slot >= 0 && slot < slots.Count && slots[slot] != null
            ? slots[slot].transform as RectTransform
            : null;
    }

    private void Awake()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            int slot = i;
            if (slots[i] != null)
                slots[i].RightClicked += () => CardRightClicked?.Invoke(slot);
        }
    }

    public void Bind(IReadOnlyList<DRPGBattleCardPreviewSO> cards)
    {
        for (int i = 0; i < slots.Count; i++)
            if (slots[i] != null)
                slots[i].Bind(cards != null && i < cards.Count ? cards[i] : null);
    }

    public void ClearSlot(int slot)
    {
        if (slot >= 0 && slot < slots.Count && slots[slot] != null)
            slots[slot].Bind(null);
    }
}
