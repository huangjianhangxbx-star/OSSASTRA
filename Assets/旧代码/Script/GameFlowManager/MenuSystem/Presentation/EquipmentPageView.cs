using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class EquipmentPageView : MonoBehaviour
{
    [Header("Current")]
    [SerializeField] private Image currentWeaponIcon;
    [SerializeField] private TMP_Text currentWeaponName;
    [SerializeField] private TMP_Text currentWeaponMeta;
    [SerializeField] private TMP_Text currentWeaponBuild;
    [SerializeField] private TMP_Text outfitText;
    [SerializeField] private TMP_Text accessoryText;

    [Header("Candidates")]
    [SerializeField] private RectTransform candidateRoot;
    [SerializeField] private GameMenuWeaponEntryView candidateTemplate;
    [SerializeField] private TMP_Text feedbackText;

    private readonly List<GameMenuWeaponEntryView> entries =
        new List<GameMenuWeaponEntryView>();
    private DRPGProgressionService progression;
    private DRPGEquipmentRoot equipmentRoot;
    private DRPGCombatantProfileSO profile;
    private DRPGWeaponEquipService equipService;
    private Tween feedbackTween;

    public void Bind(
        DRPGProgressionService progressionService,
        DRPGEquipmentRoot root,
        DRPGCombatantProfileSO targetProfile)
    {
        progression = progressionService;
        equipmentRoot = root;
        profile = targetProfile;
        equipService = progression != null && equipmentRoot != null
            ? new DRPGWeaponEquipService(progression, equipmentRoot)
            : null;
        Refresh();
    }

    public void Refresh()
    {
        HideEntries();
        if (progression == null || equipmentRoot == null || profile == null ||
            !equipmentRoot.TryGetCharacter(
                profile.CombatantId,
                out _,
                out DRPGCharacterEquipmentState equipment))
        {
            SetFeedback("该角色尚未建立装备档案");
            BindCurrent(null);
            return;
        }

        DRPGCharacterProgressionView view = progression.GetView(profile);
        BindCurrent(equipment);
        var eligibility = new DRPGEquipmentEligibilityService(progression);
        int index = 0;
        foreach (DRPGWeaponInstance weapon in
                 equipmentRoot.PartyInventory.BackpackWeapons)
        {
            EnsureEntry(index + 1);
            if (index >= entries.Count)
                break;
            DRPGEquipmentOperationResult result = eligibility.Evaluate(
                profile,
                equipment,
                weapon,
                view);
            if (result.Success)
            {
                DRPGInventoryReplacementPreview preview =
                    equipmentRoot.PartyInventory.PreviewWeaponReplacement(
                        weapon.InstanceId,
                        equipment.EquippedWeapon);
                if (!preview.Success)
                    result = DRPGEquipmentOperationResult.Fail(
                        preview.Error,
                        preview.Message);
            }
            entries[index].Bind(weapon, result, Equip);
            index++;
        }
        SetFeedback(index == 0 ? "团队背包中没有武器" : string.Empty);
    }

    private void Equip(DRPGWeaponInstance weapon)
    {
        if (weapon == null || equipService == null)
            return;
        DRPGEquipmentOperationResult result = equipService.TryEquipWeapon(
            profile.CombatantId,
            weapon.InstanceId);
        SetFeedback(result.Success ? "装备完成" : result.Message);
        if (result.Success)
            Refresh();
    }

    private void BindCurrent(DRPGCharacterEquipmentState equipment)
    {
        DRPGWeaponInstance weapon = equipment?.EquippedWeapon;
        DRPGWeaponDefinitionSO definition = weapon?.Definition;
        if (currentWeaponIcon != null)
        {
            currentWeaponIcon.sprite = definition != null ? definition.Icon : null;
            currentWeaponIcon.enabled = currentWeaponIcon.sprite != null;
            currentWeaponIcon.preserveAspect = true;
        }
        currentWeaponName?.SetText(definition != null
            ? definition.DisplayName
            : "未装备武器");
        currentWeaponMeta?.SetText(weapon != null && definition != null
            ? $"{definition.Rarity}  DUR {weapon.CurrentDurability}/" +
              $"{weapon.CurrentMaxDurability}  WT {definition.Weight:0.#}"
            : "—");
        currentWeaponBuild?.SetText(weapon != null
            ? "词条  " + Join(weapon.AffixIds) + "\n" +
              "武器卡  " + Join(weapon.CardIds) + "\n" +
              "配件  " + Join(weapon.AttachmentIds) + "  (2槽)"
            : "词条 —\n武器卡 —\n配件 —");
        outfitText?.SetText(equipment?.CurrentOutfit != null
            ? "服装词条  " + equipment.CurrentOutfit.DisplayName
            : "服装词条  —");
        if (equipment == null)
        {
            accessoryText?.SetText("挂饰  — / — / —");
            return;
        }
        var labels = new string[DRPGCharacterEquipmentState.AccessorySlotCount];
        for (int i = 0; i < labels.Length; i++)
        {
            labels[i] = i < equipment.Accessories.Count &&
                equipment.Accessories[i] != null
                    ? equipment.Accessories[i].DisplayName
                    : "—";
        }
        accessoryText?.SetText("挂饰  " + string.Join(" / ", labels));
    }

    private void EnsureEntry(int required)
    {
        if (candidateTemplate == null || candidateRoot == null)
            return;
        if (entries.Count == 0)
        {
            entries.Add(candidateTemplate);
            candidateTemplate.gameObject.SetActive(false);
        }
        while (entries.Count < required)
        {
            GameMenuWeaponEntryView entry = Instantiate(
                candidateTemplate,
                candidateRoot);
            entry.name = "WeaponCandidate_" + entries.Count;
            entries.Add(entry);
        }
    }

    private void HideEntries()
    {
        for (int i = 0; i < entries.Count; i++)
            entries[i]?.gameObject.SetActive(false);
    }

    private void SetFeedback(string message)
    {
        if (feedbackText == null)
            return;
        feedbackTween?.Kill(false);
        feedbackText.SetText(message ?? string.Empty);
        feedbackText.alpha = string.IsNullOrWhiteSpace(message) ? 0f : 1f;
        if (!string.IsNullOrWhiteSpace(message))
        {
            feedbackTween = feedbackText.DOFade(0.55f, 0.65f)
                .SetLoops(2, LoopType.Yoyo)
                .SetUpdate(true);
        }
    }

    private static string Join(IReadOnlyList<string> values)
    {
        return values != null && values.Count > 0
            ? string.Join(" / ", values)
            : "—";
    }

    private void OnDisable()
    {
        feedbackTween?.Kill(false);
    }
}
