using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Visual navigation for the six command categories. It emits intent only;
/// DRPGBattleSession remains the sole rule owner.
/// </summary>
public sealed class BattleCommandMenu : MonoBehaviour
{
    [SerializeField] private GameObject commandRoot;
    [SerializeField] private GameObject skillRoot;
    [SerializeField] private GameObject itemRoot;
    [SerializeField] private GameObject weaponRoot;
    [SerializeField] private GameObject resonanceRoot;
    [SerializeField] private Button basicAttackButton;
    [SerializeField] private Button skillButton;
    [SerializeField] private Button itemButton;
    [SerializeField] private Button weaponButton;
    [SerializeField] private Button resonanceButton;
    [SerializeField] private Button defendButton;
    [SerializeField] private Button escapeButton;
    [SerializeField] private Button skillBackButton;
    [SerializeField] private Button itemBackButton;
    [SerializeField] private Button weaponBackButton;
    [SerializeField] private BattleWeaponMenu weaponMenu;
    [SerializeField] private BattleResonanceMenu resonanceMenu;
    [SerializeField] private List<Button> itemButtons = new List<Button>();
    [SerializeField] private List<TMP_Text> itemLabels = new List<TMP_Text>();
    [SerializeField] private TMP_Text itemEmptyText;

    private readonly List<DRPGBattleItemDefinition> boundItems =
        new List<DRPGBattleItemDefinition>();
    private DRPGBattleSession session;
    private bool inputEnabled;
    private Tween transitionTween;
    private bool rootRestPositionsCaptured;
    private Vector2 commandRootRestPosition;
    private Vector2 skillRootRestPosition;
    private Vector2 itemRootRestPosition;
    private Vector2 weaponRootRestPosition;
    private Vector2 resonanceRootRestPosition;
    private GameObject activeRoot;

    public event Action BasicAttackRequested;
    public event Action<DRPGBattleItemDefinition> ItemRequested;
    public event Action DefendRequested;
    public event Action EscapeRequested;

    public void Bind(DRPGBattleSession battleSession)
    {
        gameObject.SetActive(true);
        session = battleSession;
        BindListeners();
        RefreshItems();
        weaponMenu?.Bind(session);
        if (resonanceMenu != null)
        {
            resonanceMenu.CommandBackRequested -= ShowCommandRoot;
            resonanceMenu.CommandBackRequested += ShowCommandRoot;
            resonanceMenu.Bind(session);
        }
        ShowCommandRoot();
    }

    public void Unbind()
    {
        transitionTween?.Kill(false);
        session = null;
        boundItems.Clear();
        weaponMenu?.Unbind();
        if (resonanceMenu != null)
        {
            resonanceMenu.CommandBackRequested -= ShowCommandRoot;
            resonanceMenu.Unbind();
        }
        SetInputEnabled(false);
        gameObject.SetActive(false);
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
        SetInteractable(basicAttackButton, enabled);
        SetInteractable(skillButton, enabled);
        SetInteractable(defendButton, enabled);
        SetInteractable(
            escapeButton,
            enabled && session != null && session.EscapeRules.Allowed);
        SetInteractable(itemButton, enabled && boundItems.Count > 0);
        SetInteractable(
            weaponButton,
            enabled && session != null &&
                session.GetCurrentWeaponCandidates().Count > 0);
        weaponMenu?.SetInputEnabled(enabled);
        resonanceMenu?.SetInputEnabled(enabled);
        SetInteractable(
            resonanceButton,
            enabled && resonanceMenu != null &&
                resonanceMenu.HasVisibleSkill);
        RefreshResonanceEntryVisual();
        for (int i = 0; i < itemButtons.Count; i++)
        {
            if (itemButtons[i] != null)
            {
                itemButtons[i].interactable = enabled &&
                    session != null &&
                    i < boundItems.Count &&
                    session.GetItemQuantity(boundItems[i]) > 0;
            }
        }
    }

    public void ShowCommandRoot()
    {
        SwitchRoots(commandRoot);
    }

    public void SetTargetSelectionMode(bool enabled)
    {
        if (enabled)
            SwitchRoots(null);
        else if (activeRoot == null)
            ShowCommandRoot();
    }

    public bool TryNavigateBack()
    {
        if (ReferenceEquals(activeRoot, resonanceRoot))
        {
            if (resonanceMenu != null && resonanceMenu.TryNavigateBack())
                return true;
            ShowCommandRoot();
            return true;
        }
        if (ReferenceEquals(activeRoot, skillRoot) ||
            ReferenceEquals(activeRoot, itemRoot) ||
            ReferenceEquals(activeRoot, weaponRoot))
        {
            ShowCommandRoot();
            return true;
        }
        return false;
    }

    private void ShowSkillRoot()
    {
        if (!inputEnabled)
            return;
        SwitchRoots(skillRoot);
    }

    private void ShowItemRoot()
    {
        if (!inputEnabled)
            return;
        RefreshItems();
        SwitchRoots(itemRoot);
    }

    private void ShowWeaponRoot()
    {
        if (!inputEnabled)
            return;
        weaponMenu?.Refresh();
        SwitchRoots(weaponRoot);
    }

    public void ShowResonanceRoot()
    {
        if (!inputEnabled || resonanceMenu == null)
            return;
        resonanceMenu.ShowRoot(false);
        SwitchRoots(resonanceRoot);
    }

    public void ShowResonanceSkillList()
    {
        if (resonanceMenu == null)
            return;
        SwitchRoots(resonanceRoot);
        resonanceMenu.ShowSkillList();
    }

    private void SwitchRoots(GameObject target)
    {
        transitionTween?.Kill(false);
        CaptureRootRestPositions();
        commandRoot?.SetActive(ReferenceEquals(target, commandRoot));
        skillRoot?.SetActive(ReferenceEquals(target, skillRoot));
        itemRoot?.SetActive(ReferenceEquals(target, itemRoot));
        weaponRoot?.SetActive(ReferenceEquals(target, weaponRoot));
        resonanceRoot?.SetActive(ReferenceEquals(target, resonanceRoot));
        activeRoot = target;
        if (target == null)
            return;
        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null)
            group = target.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        RectTransform rect = target.transform as RectTransform;
        Vector2 end = GetRootRestPosition(target, rect);
        if (rect != null)
            rect.anchoredPosition = end + new Vector2(-22f, 0f);
        Sequence sequence = DOTween.Sequence().SetUpdate(true)
            .Join(group.DOFade(1f, 0.12f).SetEase(Ease.OutQuad));
        if (rect != null)
            sequence.Join(rect.DOAnchorPos(end, 0.16f).SetEase(Ease.OutCubic));
        transitionTween = sequence;
    }

    private void CaptureRootRestPositions()
    {
        if (rootRestPositionsCaptured)
            return;

        commandRootRestPosition = GetAnchoredPosition(commandRoot);
        skillRootRestPosition = GetAnchoredPosition(skillRoot);
        itemRootRestPosition = GetAnchoredPosition(itemRoot);
        weaponRootRestPosition = GetAnchoredPosition(weaponRoot);
        resonanceRootRestPosition = GetAnchoredPosition(resonanceRoot);
        rootRestPositionsCaptured = true;
    }

    private Vector2 GetRootRestPosition(
        GameObject target,
        RectTransform rect)
    {
        if (ReferenceEquals(target, commandRoot))
            return commandRootRestPosition;
        if (ReferenceEquals(target, skillRoot))
            return skillRootRestPosition;
        if (ReferenceEquals(target, itemRoot))
            return itemRootRestPosition;
        if (ReferenceEquals(target, weaponRoot))
            return weaponRootRestPosition;
        if (ReferenceEquals(target, resonanceRoot))
            return resonanceRootRestPosition;
        return rect != null ? rect.anchoredPosition : Vector2.zero;
    }

    private static Vector2 GetAnchoredPosition(GameObject target)
    {
        RectTransform rect = target != null
            ? target.transform as RectTransform
            : null;
        return rect != null ? rect.anchoredPosition : Vector2.zero;
    }

    private void BindListeners()
    {
        basicAttackButton?.onClick.RemoveAllListeners();
        skillButton?.onClick.RemoveAllListeners();
        itemButton?.onClick.RemoveAllListeners();
        weaponButton?.onClick.RemoveAllListeners();
        resonanceButton?.onClick.RemoveAllListeners();
        defendButton?.onClick.RemoveAllListeners();
        escapeButton?.onClick.RemoveAllListeners();
        skillBackButton?.onClick.RemoveAllListeners();
        itemBackButton?.onClick.RemoveAllListeners();
        weaponBackButton?.onClick.RemoveAllListeners();
        basicAttackButton?.onClick.AddListener(
            () => BasicAttackRequested?.Invoke());
        skillButton?.onClick.AddListener(ShowSkillRoot);
        itemButton?.onClick.AddListener(ShowItemRoot);
        weaponButton?.onClick.AddListener(ShowWeaponRoot);
        resonanceButton?.onClick.AddListener(ShowResonanceRoot);
        defendButton?.onClick.AddListener(() => DefendRequested?.Invoke());
        escapeButton?.onClick.AddListener(() => EscapeRequested?.Invoke());
        skillBackButton?.onClick.AddListener(ShowCommandRoot);
        itemBackButton?.onClick.AddListener(ShowCommandRoot);
        weaponBackButton?.onClick.AddListener(ShowCommandRoot);
    }

    private void RefreshItems()
    {
        boundItems.Clear();
        if (session != null)
        {
            IReadOnlyList<DRPGBattleItemDefinition> items = session.BattleItems;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null)
                    boundItems.Add(items[i]);
            }
        }
        for (int i = 0; i < itemButtons.Count; i++)
        {
            bool visible = i < boundItems.Count;
            itemButtons[i].gameObject.SetActive(visible);
            if (!visible)
                continue;
            DRPGBattleItemDefinition item = boundItems[i];
            int quantity = session.GetItemQuantity(item);
            if (i < itemLabels.Count)
                itemLabels[i]?.SetText($"{item.DisplayName}   ×{quantity}");
            itemButtons[i].onClick.RemoveAllListeners();
            itemButtons[i].onClick.AddListener(() => ItemRequested?.Invoke(item));
        }
        if (itemEmptyText != null)
            itemEmptyText.gameObject.SetActive(boundItems.Count == 0);
        SetInputEnabled(inputEnabled);
    }

    private void OnDisable()
    {
        transitionTween?.Kill(false);
    }

    private static void SetInteractable(Button button, bool value)
    {
        if (button != null)
            button.interactable = value;
    }

    private void RefreshResonanceEntryVisual()
    {
        if (resonanceButton == null)
            return;
        ColorBlock colors = resonanceButton.colors;
        colors.normalColor = resonanceMenu != null && resonanceMenu.HasReadySkill
            ? new Color(1f, 0.055f, 0.34f, 1f)
            : new Color(0.09f, 0.12f, 0.16f, 0.96f);
        resonanceButton.colors = colors;
    }
}
