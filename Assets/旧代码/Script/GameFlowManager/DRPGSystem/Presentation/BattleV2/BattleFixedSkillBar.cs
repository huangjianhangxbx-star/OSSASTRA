using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleFixedSkillBar : MonoBehaviour
{
    [SerializeField] private List<BattleSkillMenuEntryView> slots =
        new List<BattleSkillMenuEntryView>();
    [SerializeField] private Button postureRecoveryButton;

    private BattleHudSnapshot snapshot;
    private bool inputEnabled = true;

    public int VisibleSlotCount => slots.Count;
    public int SelectedSlot { get; private set; } = -1;
    public event Action<int> SkillSlotRequested;
    public event Action PostureRecoveryRequested;

    private void OnEnable()
    {
        BindRecoveryButton();
    }

    public void Bind(BattleHudSnapshot value)
    {
        snapshot = value ?? throw new ArgumentNullException(nameof(value));
        if (slots.Count != DRPGSkillLoadout.SlotCount)
            throw new InvalidOperationException("Fixed skill bar requires four slot views.");

        SelectedSlot = -1;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null)
                throw new InvalidOperationException("Fixed skill slot reference is missing: " + i);
            int slotIndex = i;
            slots[i].BindCell(snapshot.Skills[i], inputEnabled,
                _ => RequestSkill(slotIndex));
        }
        BindRecoveryButton();
    }

    public void SetInputEnabled(bool value)
    {
        inputEnabled = value;
        BindRecoveryButton();
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
                slots[i].SetInputEnabled(value && snapshot != null &&
                    snapshot.Skills[i].Enabled);
        }
        if (postureRecoveryButton != null)
            postureRecoveryButton.interactable = value;
    }

    private void RequestSkill(int slotIndex)
    {
        if (!inputEnabled || snapshot == null || slotIndex < 0 ||
            slotIndex >= snapshot.Skills.Count ||
            !snapshot.Skills[slotIndex].Enabled)
            return;

        SelectedSlot = slotIndex;
        SkillSlotRequested?.Invoke(slotIndex);
    }

    private void BindRecoveryButton()
    {
        if (postureRecoveryButton == null)
            return;
        postureRecoveryButton.onClick.RemoveListener(RequestRecovery);
        postureRecoveryButton.onClick.AddListener(RequestRecovery);
        postureRecoveryButton.interactable = inputEnabled;
    }

    private void RequestRecovery()
    {
        if (inputEnabled)
            PostureRecoveryRequested?.Invoke();
    }

    private void OnDestroy()
    {
        if (postureRecoveryButton != null)
            postureRecoveryButton.onClick.RemoveListener(RequestRecovery);
    }
}
