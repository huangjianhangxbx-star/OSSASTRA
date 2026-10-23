using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleSkillMenuEntryView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;
    [SerializeField] private BattleFormationRangeIndicator rangeIndicator;
    [SerializeField] private GameObject grantedBadge;
    [SerializeField] private TMP_Text grantedBadgeText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text panelText;
    [SerializeField] private TMP_Text unavailableReasonText;

    private DRPGSkillSO skill;
    private Action<DRPGSkillSO> selected;

    public DRPGSkillSO Skill => skill;
    public string RenderedCost => costText != null ? costText.text : string.Empty;
    public string RenderedPanel => panelText != null ? panelText.text : string.Empty;

    public void Bind(
        DRPGSkillSO value,
        bool isGranted,
        bool inputEnabled,
        Action<DRPGSkillSO> onSelected,
        string displayLabel = null,
        string unavailableReason = null)
    {
        skill = value;
        selected = onSelected;
        if (button == null)
            button = GetComponent<Button>();
        button?.onClick.RemoveAllListeners();
        label?.SetText(skill != null
            ? string.IsNullOrWhiteSpace(displayLabel)
                ? skill.DisplayName
                : displayLabel
            : string.Empty);
        rangeIndicator?.Bind(skill != null
            ? skill.TargetFormationRows
            : DRPGFormationRowMask.None);
        if (grantedBadge != null)
            grantedBadge.SetActive(isGranted);
        if (grantedBadgeText != null)
            grantedBadgeText.SetText(isGranted ? "武器固有" : string.Empty);
        if (button != null)
        {
            button.interactable = inputEnabled && skill != null;
            button.onClick.AddListener(() => selected?.Invoke(skill));
        }
        gameObject.SetActive(skill != null);
    }

    public void BindCell(
        BattleSkillCell cell,
        bool inputEnabled,
        Action<int> onSelected)
    {
        skill = cell.Skill;
        if (button == null)
            button = GetComponent<Button>();
        button?.onClick.RemoveAllListeners();
        label?.SetText(skill != null ? skill.DisplayName : "空槽位");
        costText?.SetText(cell.Cost.HasValue ? cell.Cost.Value.ToString() : "—");
        panelText?.SetText(cell.Panel.HasValue ? cell.Panel.Value.ToString() : "—");
        unavailableReasonText?.SetText(cell.Enabled ? string.Empty : cell.Reason);
        rangeIndicator?.Bind(skill != null
            ? skill.TargetFormationRows
            : DRPGFormationRowMask.None);
        if (grantedBadge != null)
            grantedBadge.SetActive(false);
        if (grantedBadgeText != null)
            grantedBadgeText.SetText(string.Empty);
        if (button != null)
        {
            button.interactable = inputEnabled && cell.Enabled;
            int slotIndex = cell.SlotIndex;
            button.onClick.AddListener(() => onSelected?.Invoke(slotIndex));
        }
        gameObject.SetActive(true);
    }

    public void SetInputEnabled(bool value)
    {
        if (button == null)
            button = GetComponent<Button>();
        if (button != null)
            button.interactable = value && skill != null;
    }
}
