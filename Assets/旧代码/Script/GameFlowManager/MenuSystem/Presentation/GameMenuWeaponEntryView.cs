using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GameMenuWeaponEntryView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text detailText;
    [SerializeField] private TMP_Text reasonText;
    [SerializeField] private Image rarityRail;

    public void Bind(
        DRPGWeaponInstance weapon,
        DRPGEquipmentOperationResult eligibility,
        Action<DRPGWeaponInstance> selected)
    {
        button ??= GetComponent<Button>();
        DRPGWeaponDefinitionSO definition = weapon?.Definition;
        if (icon != null)
        {
            icon.sprite = definition != null ? definition.Icon : null;
            icon.enabled = icon.sprite != null;
            icon.preserveAspect = true;
        }
        nameText?.SetText(definition != null ? definition.DisplayName : "UNKNOWN");
        detailText?.SetText(weapon != null && definition != null
            ? $"耐久 {weapon.CurrentDurability}/{weapon.CurrentMaxDurability}  " +
              $"负重 {definition.Weight:0.#}  " +
              $"职业 {(eligibility.ResolvedProfession != null ? eligibility.ResolvedProfession.DisplayName : "—")}" 
            : string.Empty);
        reasonText?.SetText(eligibility.Success ? "可装备" : eligibility.Message);
        if (rarityRail != null)
            rarityRail.color = ResolveRarity(definition?.Rarity ?? DRPGEquipmentRarity.Blue);
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.interactable = weapon != null && eligibility.Success;
            button.onClick.AddListener(() => selected?.Invoke(weapon));
        }
        gameObject.SetActive(weapon != null);
    }

    private static Color ResolveRarity(DRPGEquipmentRarity rarity)
    {
        switch (rarity)
        {
            case DRPGEquipmentRarity.Purple:
                return new Color32(160, 68, 255, 255);
            case DRPGEquipmentRarity.Gold:
                return new Color32(255, 190, 42, 255);
            case DRPGEquipmentRarity.RedGold:
                return new Color32(255, 38, 80, 255);
            default:
                return new Color32(49, 165, 255, 255);
        }
    }
}
