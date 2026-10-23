using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class BattleWeaponMenuEntryView : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [SerializeField] private Button button;
    [SerializeField] private Image icon;
    [SerializeField] private Image rarityRail;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text detailText;
    [SerializeField] private TMP_Text stateText;
    [SerializeField] private CanvasGroup canvasGroup;

    private DRPGBattleWeaponCandidate candidate;
    private Action<DRPGBattleWeaponCandidate> selected;
    private Action<string> feedback;

    public DRPGBattleWeaponCandidate Candidate => candidate;

    public void Bind(
        DRPGBattleWeaponCandidate value,
        bool inputEnabled,
        Action<DRPGBattleWeaponCandidate> onSelected,
        Action<string> onFeedback)
    {
        candidate = value;
        selected = onSelected;
        feedback = onFeedback;
        button ??= GetComponent<Button>();
        canvasGroup ??= GetComponent<CanvasGroup>();
        button?.onClick.RemoveAllListeners();

        DRPGWeaponInstance weapon = value?.Weapon;
        DRPGWeaponDefinitionSO definition = weapon?.Definition;
        if (icon != null)
        {
            icon.sprite = definition != null ? definition.Icon : null;
            icon.enabled = icon.sprite != null;
            icon.preserveAspect = true;
        }
        if (rarityRail != null)
            rarityRail.color = ResolveRarity(definition != null
                ? definition.Rarity
                : DRPGEquipmentRarity.Blue);
        nameText?.SetText(definition != null
            ? definition.DisplayName
            : value?.WeaponInstanceId ?? "UNKNOWN");

        string profession = value?.ResolvedProfession != null
            ? value.ResolvedProfession.DisplayName
            : "—";
        string durability = weapon != null
            ? $"{weapon.CurrentDurability}/{weapon.CurrentMaxDurability}"
            : "—";
        string weight = definition != null
            ? definition.Weight.ToString("0.#")
            : "—";
        detailText?.SetText(
            $"职业 {profession}   耐久 {durability}   负重 {weight}");

        bool eligible = value != null && value.IsEligible;
        string state = eligible
            ? value.ReturnsPreviousBeyondCapacity
                ? "可装备｜旧武器容量豁免返包"
                : "可装备｜消耗本回合"
            : value?.RejectionReason ?? "不可装备";
        stateText?.SetText(state);
        if (button != null)
        {
            button.interactable = inputEnabled && eligible;
            button.onClick.AddListener(() => selected?.Invoke(candidate));
        }
        if (canvasGroup != null)
            canvasGroup.alpha = eligible ? 1f : 0.48f;
        gameObject.SetActive(true);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (candidate != null && !candidate.IsEligible)
            feedback?.Invoke(candidate.RejectionReason);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        feedback?.Invoke(string.Empty);
    }

    private static Color ResolveRarity(DRPGEquipmentRarity rarity)
    {
        switch (rarity)
        {
            case DRPGEquipmentRarity.Purple:
                return new Color32(166, 74, 255, 255);
            case DRPGEquipmentRarity.Gold:
                return new Color32(255, 190, 42, 255);
            case DRPGEquipmentRarity.RedGold:
                return new Color32(255, 42, 86, 255);
            default:
                return new Color32(53, 164, 255, 255);
        }
    }
}
