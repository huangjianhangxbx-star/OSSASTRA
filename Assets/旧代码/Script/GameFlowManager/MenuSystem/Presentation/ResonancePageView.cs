using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Protagonist-only identity page. Career progression remains in the existing
/// skill-tree/loadout pages; this page owns friend branches and resonance only.
/// </summary>
public sealed class ResonancePageView : MonoBehaviour
{
    [SerializeField] private TMP_Text identityTitle;
    [SerializeField] private TMP_Text partnerSummary;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private List<Button> partnerButtons = new List<Button>();
    [SerializeField] private List<TMP_Text> partnerLabels = new List<TMP_Text>();
    [SerializeField] private List<Button> nodeButtons = new List<Button>();
    [SerializeField] private List<TMP_Text> nodeLabels = new List<TMP_Text>();

    private DRPGResonanceService service;
    private DRPGPartyCompositionState party;
    private DRPGCombatantProfileSO profile;
    private GameMenuTooltipView tooltip;
    private DRPGResonanceBranchSO selectedBranch;

    public string SelectedBranchId => selectedBranch != null
        ? selectedBranch.BranchId
        : string.Empty;

    public void Bind(
        DRPGResonanceService resonanceService,
        DRPGPartyCompositionState partyComposition,
        DRPGCombatantProfileSO currentProfile,
        GameMenuTooltipView tooltipView)
    {
        service = resonanceService;
        party = partyComposition;
        profile = currentProfile;
        tooltip = tooltipView;
        ResolveSelectedBranch();
        Refresh();
    }

    public void Unbind()
    {
        service = null;
        party = null;
        profile = null;
        selectedBranch = null;
        tooltip?.Hide();
        tooltip = null;
    }

    public void Refresh()
    {
        bool valid = service != null && party != null && profile != null &&
            service.Definition.IsProtagonist(profile);
        identityTitle?.SetText(valid
            ? "IDENTITY / 同律调律者"
            : "IDENTITY / 主角专属");
        if (!valid)
        {
            SetFeedback("同律身份层仅属于主角，且需要有效队伍配置。");
            SetAllButtonsHidden();
            return;
        }

        SetFeedback(string.Empty);
        RefreshPartners();
        RefreshNodes();
        string selectedPartner = service.State.ResonancePartnerCharacterId;
        partnerSummary?.SetText(string.IsNullOrWhiteSpace(selectedPartner)
            ? "同律对象：未指定"
            : "同律对象：" + selectedPartner + "（队伍 2 号位）");
    }

    private void ResolveSelectedBranch()
    {
        selectedBranch = null;
        if (service == null)
            return;
        string selectedPartner = service.State.ResonancePartnerCharacterId;
        selectedBranch = service.Definition.FindBranchByPartnerId(
            selectedPartner);
        if (selectedBranch != null)
            return;
        IReadOnlyList<DRPGResonanceBranchSO> branches =
            service.Definition.Branches;
        for (int i = 0; i < branches.Count; i++)
        {
            DRPGResonanceBranchSO branch = branches[i];
            if (IsBranchSelectable(branch))
            {
                selectedBranch = branch;
                return;
            }
        }
    }

    private void RefreshPartners()
    {
        IReadOnlyList<DRPGResonanceBranchSO> branches =
            service.Definition.Branches;
        int visibleIndex = 0;
        for (int i = 0; i < branches.Count && visibleIndex < partnerButtons.Count;
             i++)
        {
            DRPGResonanceBranchSO branch = branches[i];
            if (!IsBranchSelectable(branch))
                continue;
            Button button = partnerButtons[visibleIndex];
            TMP_Text label = visibleIndex < partnerLabels.Count
                ? partnerLabels[visibleIndex]
                : null;
            if (button != null)
            {
                button.gameObject.SetActive(true);
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectBranch(branch));
                SetSelected(button, ReferenceEquals(branch, selectedBranch));
            }
            label?.SetText(branch.FriendProfile.DisplayName);
            visibleIndex++;
        }
        for (int i = visibleIndex; i < partnerButtons.Count; i++)
            partnerButtons[i]?.gameObject.SetActive(false);
    }

    private void RefreshNodes()
    {
        IReadOnlyList<DRPGResonanceNodeDefinition> nodes = selectedBranch != null
            ? selectedBranch.Nodes
            : Array.Empty<DRPGResonanceNodeDefinition>();
        for (int i = 0; i < nodeButtons.Count; i++)
        {
            Button button = nodeButtons[i];
            TMP_Text label = i < nodeLabels.Count ? nodeLabels[i] : null;
            bool visible = i < nodes.Count && nodes[i] != null;
            if (button != null)
                button.gameObject.SetActive(visible);
            if (!visible || button == null)
                continue;

            DRPGResonanceNodeDefinition node = nodes[i];
            bool unlocked = service.State.IsNodeUnlocked(
                selectedBranch.BranchId,
                node.NodeId);
            bool equipped = node.RewardKind ==
                    DRPGResonanceNodeRewardKind.ActiveSkill &&
                node.ActiveSkill != null &&
                string.Equals(
                    service.State.GetEquippedActiveSkillId(
                        selectedBranch.FriendCharacterId),
                    node.ActiveSkill.SkillId,
                    StringComparison.Ordinal);
            label?.SetText(BuildNodeLabel(node, unlocked, equipped));
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => ActivateNode(node));
            button.interactable = !unlocked ||
                node.RewardKind == DRPGResonanceNodeRewardKind.ActiveSkill;
            SetNodeVisual(button, unlocked, equipped);
            BindTooltip(button, node);
        }
    }

    private void SelectBranch(DRPGResonanceBranchSO branch)
    {
        if (!IsBranchSelectable(branch))
            return;
        DRPGResonanceOperationResult result = service.TrySelectPartner(
            branch.FriendProfile,
            party);
        if (!result.Success)
        {
            SetFeedback(result.Message);
            return;
        }
        selectedBranch = branch;
        Refresh();
    }

    private void ActivateNode(DRPGResonanceNodeDefinition node)
    {
        if (selectedBranch == null || node == null)
            return;
        if (!service.State.IsNodeUnlocked(selectedBranch.BranchId, node.NodeId))
        {
            DRPGResonanceOperationResult unlock = service.TryUnlockNode(
                selectedBranch.FriendCharacterId,
                node.NodeId);
            if (!unlock.Success)
            {
                SetFeedback(unlock.Message);
                return;
            }
        }

        if (node.RewardKind == DRPGResonanceNodeRewardKind.ActiveSkill)
        {
            DRPGResonanceOperationResult equip = service.TryEquipActiveSkill(
                selectedBranch.FriendCharacterId,
                node.ActiveSkill);
            if (!equip.Success)
            {
                SetFeedback(equip.Message);
                return;
            }
        }
        SetFeedback(node.RewardKind == DRPGResonanceNodeRewardKind.Passive
            ? "被动节点已解锁并自动生效。"
            : "同律主动技能已装备；同分支其他主动技能会被替换。");
        RefreshNodes();
    }

    private bool IsBranchSelectable(DRPGResonanceBranchSO branch)
    {
        return branch != null && branch.FriendProfile != null &&
            party != null &&
            party.ContainsCharacterId(branch.FriendCharacterId);
    }

    private static string BuildNodeLabel(
        DRPGResonanceNodeDefinition node,
        bool unlocked,
        bool equipped)
    {
        string state;
        if (!unlocked)
            state = "LOCKED  关系条件 " + node.RequiredConditionValue;
        else if (node.RewardKind == DRPGResonanceNodeRewardKind.Passive)
            state = "PASSIVE  AUTO";
        else
            state = equipped ? "ACTIVE  EQUIPPED" : "ACTIVE  UNLOCKED";
        return node.DisplayName + "\n" + state;
    }

    private void BindTooltip(
        Button button,
        DRPGResonanceNodeDefinition node)
    {
        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = button.gameObject.AddComponent<EventTrigger>();
        trigger.triggers = new List<EventTrigger.Entry>();
        AddTrigger(trigger, EventTriggerType.PointerEnter, _ =>
        {
            string reward = node.RewardKind ==
                    DRPGResonanceNodeRewardKind.ActiveSkill
                ? node.ActiveSkill != null ? node.ActiveSkill.DisplayName : "—"
                : node.PassiveModifier != null
                    ? node.PassiveModifier.DisplayName
                    : "—";
            tooltip?.Show(
                node.DisplayName,
                node.Description + "\n\nRELATION " +
                node.RequiredConditionValue + "   REWARD " + reward);
        });
        AddTrigger(trigger, EventTriggerType.PointerExit, _ => tooltip?.Hide());
    }

    private static void AddTrigger(
        EventTrigger trigger,
        EventTriggerType type,
        Action<BaseEventData> callback)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(data => callback(data));
        trigger.triggers.Add(entry);
    }

    private void SetFeedback(string value)
    {
        if (feedbackText == null)
            return;
        feedbackText.text = value ?? string.Empty;
        feedbackText.gameObject.SetActive(!string.IsNullOrWhiteSpace(value));
    }

    private void SetAllButtonsHidden()
    {
        for (int i = 0; i < partnerButtons.Count; i++)
            partnerButtons[i]?.gameObject.SetActive(false);
        for (int i = 0; i < nodeButtons.Count; i++)
            nodeButtons[i]?.gameObject.SetActive(false);
    }

    private static void SetSelected(Button button, bool selected)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = selected
            ? new Color(1f, 0.035f, 0.36f, 1f)
            : new Color(0.09f, 0.13f, 0.17f, 1f);
        button.colors = colors;
    }

    private static void SetNodeVisual(
        Button button,
        bool unlocked,
        bool equipped)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = equipped
            ? new Color(0.96f, 0.03f, 0.32f, 1f)
            : unlocked
                ? new Color(0.05f, 0.40f, 0.48f, 1f)
                : new Color(0.08f, 0.10f, 0.14f, 0.88f);
        button.colors = colors;
    }
}
