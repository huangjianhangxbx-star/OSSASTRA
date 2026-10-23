using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Presentation adapter for employee field-recovery bags. It never mutates
/// inventory directly; collection is delegated to DRPGFieldRecoveryService.
/// </summary>
public sealed class BattleRecoveryBagView : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private BattleRecoveryItemView itemTemplate;
    [SerializeField] private RectTransform dropZone;
    [SerializeField] private RectTransform dragLayer;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text emptyText;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private CanvasGroup canvasGroup;

    private readonly List<BattleRecoveryItemView> entries =
        new List<BattleRecoveryItemView>();
    private DRPGFieldRecoveryService service;
    private Tween transitionTween;
    private Tween feedbackTween;
    private Vector2 restPosition;
    private bool restPositionCaptured;

    public int VisibleItemCount { get; private set; }

    public void Bind(DRPGEquipmentRoot equipmentRoot)
    {
        Unbind();
        service = equipmentRoot?.FieldRecovery;
        if (service != null)
            service.Changed += Refresh;
        Refresh();
    }

    public void Unbind()
    {
        if (service != null)
            service.Changed -= Refresh;
        service = null;
        HideImmediate();
    }

    public void Refresh()
    {
        HideEntries();
        VisibleItemCount = 0;
        if (service == null)
        {
            HideImmediate();
            return;
        }

        foreach (DRPGFieldRecoveryBagState bag in service.ActiveBags)
        {
            if (bag == null || bag.IsResolved || bag.IsEmpty)
                continue;
            if (bag.Weapon != null)
            {
                DRPGWeaponInstance weapon = bag.Weapon;
                AddEntry(
                    weapon.Definition != null
                        ? weapon.Definition.DisplayName
                        : weapon.InstanceId,
                    "武器",
                    weapon.Definition?.Icon,
                    () => CollectWeapon(bag.BagId));
            }
            for (int i = 0; i < bag.Accessories.Count; i++)
            {
                DRPGRecoveredAccessoryItem accessory = bag.Accessories[i];
                string bagId = bag.BagId;
                string instanceId = accessory.InstanceId;
                AddEntry(
                    accessory.Definition != null
                        ? accessory.Definition.DisplayName
                        : instanceId,
                    "挂饰",
                    accessory.Definition?.Icon,
                    () => CollectAccessory(bagId, instanceId));
            }
        }

        bool visible = VisibleItemCount > 0;
        emptyText?.gameObject.SetActive(!visible);
        titleText?.SetText($"FIELD RECOVERY / 战场回收  ×{VisibleItemCount}");
        if (visible)
            ShowAnimated();
        else
            HideImmediate();
    }

    private void AddEntry(
        string displayName,
        string kind,
        Sprite icon,
        Action collect)
    {
        EnsureEntry(VisibleItemCount + 1);
        if (VisibleItemCount >= entries.Count)
            return;
        entries[VisibleItemCount].Bind(
            displayName,
            kind,
            icon,
            dropZone,
            dragLayer,
            collect);
        VisibleItemCount++;
    }

    private void CollectWeapon(string bagId)
    {
        SetResult(service?.TryCollectWeapon(bagId) ??
            DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.RecoveryBagNotFound,
                "回收服务不可用"));
    }

    private void CollectAccessory(string bagId, string instanceId)
    {
        SetResult(service?.TryCollectAccessory(bagId, instanceId) ??
            DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.RecoveryBagNotFound,
                "回收服务不可用"));
    }

    private void SetResult(DRPGEquipmentOperationResult result)
    {
        SetFeedback(result.Success ? "已转移到团队背包" : result.Message);
    }

    private void EnsureEntry(int required)
    {
        if (itemTemplate == null || contentRoot == null)
            return;
        if (entries.Count == 0)
        {
            entries.Add(itemTemplate);
            itemTemplate.gameObject.SetActive(false);
        }
        while (entries.Count < required)
        {
            BattleRecoveryItemView entry = Instantiate(itemTemplate, contentRoot);
            entry.name = "RecoveryItem_" + entries.Count;
            entries.Add(entry);
        }
    }

    private void HideEntries()
    {
        for (int i = 0; i < entries.Count; i++)
            entries[i]?.gameObject.SetActive(false);
    }

    private void ShowAnimated()
    {
        if (panelRoot == null)
            panelRoot = gameObject;
        bool wasActive = panelRoot.activeSelf;
        panelRoot.SetActive(true);
        if (wasActive || canvasGroup == null)
            return;
        transitionTween?.Kill(false);
        canvasGroup.alpha = 0f;
        RectTransform rect = panelRoot.transform as RectTransform;
        if (rect != null && !restPositionCaptured)
        {
            restPosition = rect.anchoredPosition;
            restPositionCaptured = true;
        }
        if (rect != null)
            rect.anchoredPosition = restPosition + new Vector2(34f, -18f);
        Sequence sequence = DOTween.Sequence().SetUpdate(true)
            .Join(canvasGroup.DOFade(1f, 0.18f).SetEase(Ease.OutQuad));
        if (rect != null)
        {
            sequence.Join(rect.DOAnchorPos(restPosition, 0.24f)
                .SetEase(Ease.OutBack, 1.08f));
        }
        transitionTween = sequence;
    }

    private void HideImmediate()
    {
        transitionTween?.Kill(false);
        feedbackTween?.Kill(false);
        if (restPositionCaptured && panelRoot != null &&
            panelRoot.transform is RectTransform rect)
        {
            rect.anchoredPosition = restPosition;
        }
        if (panelRoot != null)
            panelRoot.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    private void SetFeedback(string value)
    {
        if (feedbackText == null)
            return;
        feedbackTween?.Kill(false);
        feedbackText.SetText(value ?? string.Empty);
        feedbackText.alpha = string.IsNullOrWhiteSpace(value) ? 0f : 1f;
        if (!string.IsNullOrWhiteSpace(value))
        {
            feedbackTween = feedbackText.DOFade(0.55f, 0.55f)
                .SetLoops(2, LoopType.Yoyo)
                .SetUpdate(true);
        }
    }

    private void OnDestroy()
    {
        Unbind();
    }
}
