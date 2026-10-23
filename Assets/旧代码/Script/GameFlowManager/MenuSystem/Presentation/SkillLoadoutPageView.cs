using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class SkillLoadoutPageView : MonoBehaviour, IDropHandler
{
    [SerializeField] private SkillLoadoutSlotView[] slots =
        new SkillLoadoutSlotView[DRPGSkillLoadout.SlotCount];
    [SerializeField] private RectTransform poolRoot;
    [SerializeField] private SkillCardView poolCardTemplate;
    [SerializeField] private RectTransform dragLayer;
    [SerializeField] private TMP_Text feedbackText;

    private readonly List<SkillCardView> spawnedPoolCards =
        new List<SkillCardView>();
    private DRPGProgressionService progression;
    private DRPGCombatantProfileSO profile;
    private GameMenuTooltipView tooltip;
    private DRPGSkillSO draggedSkill;
    private int dragSourceSlot = -1;
    private RectTransform dragOrigin;
    private Image dragGhost;
    private Tween dragReturnTween;
    private bool dropHandled;

    public void Bind(
        DRPGProgressionService service,
        DRPGCombatantProfileSO targetProfile,
        GameMenuTooltipView tooltipView)
    {
        progression = service;
        profile = targetProfile;
        tooltip = tooltipView;
        Refresh();
    }

    public void Refresh()
    {
        ClearPool();
        if (progression == null || profile == null)
            return;
        DRPGCharacterProgressionView view = progression.GetView(profile);
        var equippedSkillIds = new HashSet<string>();
        for (int i = 0; i < view.EquippedSkills.Count; i++)
        {
            DRPGSkillSO equipped = view.EquippedSkills[i];
            if (equipped != null)
                equippedSkillIds.Add(equipped.SkillId);
        }
        for (int i = 0; i < slots.Length; i++)
        {
            DRPGSkillSO skill = i < view.EquippedSkills.Count
                ? view.EquippedSkills[i]
                : null;
            slots[i]?.Bind(this, i, skill, tooltip);
        }

        if (poolCardTemplate == null || poolRoot == null)
            return;
        for (int i = 0; i < view.UnlockedSkills.Count; i++)
        {
            DRPGSkillSO skill = view.UnlockedSkills[i];
            if (skill == null || equippedSkillIds.Contains(skill.SkillId))
                continue;
            SkillCardView card = Instantiate(poolCardTemplate, poolRoot);
            card.gameObject.SetActive(true);
            card.Bind(this, skill, -1, tooltip);
            spawnedPoolCards.Add(card);
        }
    }

    public void BeginDrag(DRPGSkillSO skill, int sourceSlot, RectTransform origin)
    {
        if (skill == null || dragLayer == null)
            return;
        DestroyDragGhostImmediate();
        draggedSkill = skill;
        dragSourceSlot = sourceSlot;
        dragOrigin = origin;
        dropHandled = false;
        GameObject ghostObject = new GameObject(
            "SkillDragGhost",
            typeof(RectTransform),
            typeof(CanvasGroup),
            typeof(Image));
        RectTransform ghostRect = ghostObject.GetComponent<RectTransform>();
        ghostRect.SetParent(dragLayer, false);
        ghostRect.sizeDelta = origin != null ? origin.rect.size : new Vector2(220f, 72f);
        dragGhost = ghostObject.GetComponent<Image>();
        dragGhost.sprite = skill.Icon;
        dragGhost.color = skill.Icon != null
            ? Color.white
            : new Color(1f, 0.08f, 0.42f, 0.92f);
        dragGhost.raycastTarget = false;
        CanvasGroup group = ghostObject.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.alpha = 0.9f;
    }

    public void UpdateDrag(Vector2 screenPosition)
    {
        if (dragGhost != null)
            dragGhost.rectTransform.position = screenPosition;
    }

    public void DropOnSlot(int slotIndex)
    {
        if (draggedSkill == null)
            return;
        DRPGProgressionOperationResult result = dragSourceSlot >= 0
            ? progression.TrySwapSkillSlots(profile, dragSourceSlot, slotIndex)
            : progression.TryEquipSkill(profile, draggedSkill, slotIndex);
        feedbackText?.SetText(result.Success ? "EQUIPPED" : result.Message);
        if (result.Success)
        {
            dropHandled = true;
            DestroyDragGhostImmediate();
            ClearDragState();
            Refresh();
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (draggedSkill == null || dragSourceSlot < 0)
            return;
        DRPGProgressionOperationResult result =
            progression.TryEquipSkill(profile, null, dragSourceSlot);
        feedbackText?.SetText(result.Success ? "UNEQUIPPED" : result.Message);
        if (result.Success)
        {
            dropHandled = true;
            DestroyDragGhostImmediate();
            ClearDragState();
            Refresh();
        }
    }

    public void EndDrag()
    {
        if (dragGhost == null)
        {
            ClearDragState();
            return;
        }

        if (!dropHandled && dragOrigin != null)
        {
            dragReturnTween?.Kill(false);
            dragReturnTween = dragGhost.rectTransform
                .DOMove(dragOrigin.position, 0.16f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .OnComplete(DestroyDragGhostImmediate);
        }
        else
        {
            DestroyDragGhostImmediate();
        }
        ClearDragState();
    }

    private void DestroyDragGhostImmediate()
    {
        dragReturnTween?.Kill(false);
        dragReturnTween = null;
        if (dragGhost == null)
            return;

        GameObject ghostObject = dragGhost.gameObject;
        dragGhost = null;
        ghostObject.transform.DOKill(false);
        ghostObject.SetActive(false);
        ghostObject.transform.SetParent(null, false);
        if (Application.isPlaying)
            Destroy(ghostObject);
        else
            DestroyImmediate(ghostObject);
    }

    private void ClearDragState()
    {
        draggedSkill = null;
        dragSourceSlot = -1;
        dragOrigin = null;
        dropHandled = false;
    }

    private void ClearPool()
    {
        for (int i = 0; i < spawnedPoolCards.Count; i++)
        {
            if (spawnedPoolCards[i] != null)
            {
                if (Application.isPlaying)
                    Destroy(spawnedPoolCards[i].gameObject);
                else
                    DestroyImmediate(spawnedPoolCards[i].gameObject);
            }
        }
        spawnedPoolCards.Clear();
    }

    private void OnDisable()
    {
        DestroyDragGhostImmediate();
        ClearDragState();
    }
}
