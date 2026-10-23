using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Optional node UI for two-click party swaps and restoring the node-entry
/// order. It never mutates combat formation while a battle owns the party.
/// </summary>
[DisallowMultipleComponent]
public sealed class DRPGExplorationFormationView : MonoBehaviour
{
    [Serializable]
    private sealed class MemberButton
    {
        public string stableId;
        public Button button;
    }

    [SerializeField] private DRPGNodePartyRoot party;
    [SerializeField] private DRPGNodeBattleCoordinator coordinator;
    [SerializeField] private List<MemberButton> memberButtons =
        new List<MemberButton>();
    [SerializeField] private Button restoreDefaultButton;

    private string selectedStableId = string.Empty;
    private bool battleActive;

    private void OnEnable()
    {
        BindButtons();
        if (coordinator != null)
        {
            coordinator.BattleBegan += NotifyBattleStarted;
            coordinator.BattleFinished += HandleBattleFinished;
        }
    }

    private void OnDisable()
    {
        if (coordinator != null)
        {
            coordinator.BattleBegan -= NotifyBattleStarted;
            coordinator.BattleFinished -= HandleBattleFinished;
        }
        UnbindButtons();
    }

    public void NotifyBattleStarted()
    {
        battleActive = true;
        selectedStableId = string.Empty;
        RefreshInteractable();
    }

    private void HandleBattleFinished(DRPGBattleCompletionContext _)
    {
        battleActive = false;
        RefreshInteractable();
    }

    private void Select(string stableId)
    {
        if (battleActive || party == null || string.IsNullOrWhiteSpace(stableId))
            return;
        if (string.Equals(selectedStableId, stableId, StringComparison.Ordinal))
        {
            selectedStableId = string.Empty;
            return;
        }
        if (string.IsNullOrEmpty(selectedStableId))
        {
            selectedStableId = stableId;
            return;
        }
        party.TrySwap(selectedStableId, stableId);
        selectedStableId = string.Empty;
    }

    private void RestoreDefault()
    {
        if (battleActive || party == null)
            return;
        selectedStableId = string.Empty;
        party.RestoreDefaultFormation();
    }

    private void BindButtons()
    {
        for (int i = 0; i < memberButtons.Count; i++)
        {
            MemberButton item = memberButtons[i];
            if (item?.button == null)
                continue;
            string captured = item.stableId;
            item.button.onClick.RemoveAllListeners();
            item.button.onClick.AddListener(() => Select(captured));
        }
        if (restoreDefaultButton != null)
        {
            restoreDefaultButton.onClick.RemoveAllListeners();
            restoreDefaultButton.onClick.AddListener(RestoreDefault);
        }
        RefreshInteractable();
    }

    private void UnbindButtons()
    {
        for (int i = 0; i < memberButtons.Count; i++)
            memberButtons[i]?.button?.onClick.RemoveAllListeners();
        restoreDefaultButton?.onClick.RemoveAllListeners();
    }

    private void RefreshInteractable()
    {
        bool enabled = !battleActive && party != null && party.ExplorationEnabled;
        for (int i = 0; i < memberButtons.Count; i++)
            if (memberButtons[i]?.button != null)
                memberButtons[i].button.interactable = enabled;
        if (restoreDefaultButton != null)
            restoreDefaultButton.interactable = enabled;
    }
}
