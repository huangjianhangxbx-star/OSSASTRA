using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents the protagonist-only identity layer without merging it into the
/// career skill loadout. Domain availability remains owned by the session.
/// </summary>
public sealed class BattleResonanceMenu : MonoBehaviour
{
    [SerializeField] private GameObject resonanceRootPanel;
    [SerializeField] private GameObject skillListPanel;
    [SerializeField] private TMP_Text partnerLabel;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private Button openSkillListButton;
    [SerializeField] private TMP_Text openSkillListLabel;
    [SerializeField] private Button commandBackButton;
    [SerializeField] private Button skillListBackButton;
    [SerializeField] private Button activeSkillButton;
    [SerializeField] private TMP_Text activeSkillLabel;
    [SerializeField] private List<Button> partnerButtons = new List<Button>();
    [SerializeField] private List<TMP_Text> partnerButtonLabels =
        new List<TMP_Text>();
    [SerializeField] private Image readyAccent;

    private DRPGBattleSession session;
    private bool inputEnabled;
    private Tween openTween;

    public event Action<DRPGSkillSO> SkillRequested;
    public event Action CommandBackRequested;

    public bool IsSkillListOpen =>
        skillListPanel != null && skillListPanel.activeSelf;
    public bool HasVisibleSkill { get; private set; }
    public bool HasReadySkill { get; private set; }

    public void Bind(DRPGBattleSession battleSession)
    {
        session = battleSession;
        BindListeners();
        ShowRoot(false);
        Refresh();
    }

    public void Unbind()
    {
        openTween?.Kill(false);
        session = null;
        HasVisibleSkill = false;
        HasReadySkill = false;
        SetFeedback(string.Empty);
        SetInputEnabled(false);
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
        RefreshInteractable();
    }

    public void ShowRoot(bool animate = true)
    {
        resonanceRootPanel?.SetActive(true);
        skillListPanel?.SetActive(false);
        Refresh();
        if (animate)
            PlayOpenMotion(resonanceRootPanel);
    }

    public void ShowSkillList(bool animate = true)
    {
        resonanceRootPanel?.SetActive(false);
        skillListPanel?.SetActive(true);
        Refresh();
        if (animate)
            PlayOpenMotion(skillListPanel);
    }

    public bool TryNavigateBack()
    {
        if (!IsSkillListOpen)
            return false;
        ShowRoot();
        return true;
    }

    public void Refresh()
    {
        BattleParticipantRuntime actor = session != null
            ? session.CurrentActor
            : null;
        IDRPGBattleResonanceAccess access = session != null
            ? session.ResonanceAccess
            : null;
        string selectedPartnerId = access != null
            ? access.SelectedPartnerCharacterId
            : string.Empty;
        partnerLabel?.SetText(string.IsNullOrWhiteSpace(selectedPartnerId)
            ? "同律对象：未指定"
            : "同律对象：" + selectedPartnerId);

        IReadOnlyList<DRPGCombatantProfileSO> partners = session != null
            ? session.ResonancePartners
            : Array.Empty<DRPGCombatantProfileSO>();
        for (int i = 0; i < partnerButtons.Count; i++)
        {
            Button button = partnerButtons[i];
            bool visible = i < partners.Count && partners[i] != null;
            if (button != null)
                button.gameObject.SetActive(visible);
            if (!visible || button == null)
                continue;

            DRPGCombatantProfileSO partner = partners[i];
            if (i < partnerButtonLabels.Count)
                partnerButtonLabels[i]?.SetText(partner.DisplayName);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectPartner(partner));
            SetSelected(button, string.Equals(
                selectedPartnerId,
                partner.CombatantId,
                StringComparison.Ordinal));
        }

        IReadOnlyList<DRPGSkillSO> visibleSkills = access != null
            ? access.GetVisibleSkills(actor, session.Participants)
            : Array.Empty<DRPGSkillSO>();
        DRPGSkillSO active = visibleSkills.Count > 0
            ? visibleSkills[0]
            : null;
        HasVisibleSkill = active != null;
        DRPGResonanceSkillAvailability availability = active != null
            ? access.GetAvailability(actor, active, session.Participants)
            : null;
        HasReadySkill = availability != null && availability.IsAvailable;

        activeSkillButton?.gameObject.SetActive(active != null);
        if (active != null)
        {
            activeSkillLabel?.SetText(
                active.DisplayName + "  " +
                availability.CurrentProgress + "/" +
                availability.RequiredProgress);
            activeSkillButton.onClick.RemoveAllListeners();
            activeSkillButton.onClick.AddListener(() => RequestSkill(active));
            SetFeedback(availability.IsAvailable
                ? string.Empty
                : availability.Reason);
        }
        else
        {
            activeSkillLabel?.SetText("尚未装备同律主动技能");
            SetFeedback(string.Empty);
        }

        openSkillListLabel?.SetText(HasReadySkill
            ? "同律技能  READY"
            : "同律技能");
        if (readyAccent != null)
        {
            readyAccent.color = HasReadySkill
                ? new Color(1f, 0.08f, 0.42f, 1f)
                : new Color(0.15f, 0.35f, 0.42f, 0.55f);
        }
        RefreshInteractable();
    }

    public void SetFeedback(string value)
    {
        if (feedbackText == null)
            return;
        feedbackText.text = value ?? string.Empty;
        feedbackText.gameObject.SetActive(!string.IsNullOrWhiteSpace(value));
    }

    private void BindListeners()
    {
        openSkillListButton?.onClick.RemoveAllListeners();
        openSkillListButton?.onClick.AddListener(() => ShowSkillList());
        commandBackButton?.onClick.RemoveAllListeners();
        commandBackButton?.onClick.AddListener(
            () => CommandBackRequested?.Invoke());
        skillListBackButton?.onClick.RemoveAllListeners();
        skillListBackButton?.onClick.AddListener(() => ShowRoot());
    }

    private void SelectPartner(DRPGCombatantProfileSO partner)
    {
        if (!inputEnabled || session == null)
            return;
        if (!session.TrySelectResonancePartner(partner, out string error))
        {
            SetFeedback(error);
            return;
        }
        SetFeedback(string.Empty);
        Refresh();
    }

    private void RequestSkill(DRPGSkillSO skill)
    {
        if (!inputEnabled || !HasReadySkill || skill == null)
            return;
        SetFeedback(string.Empty);
        SkillRequested?.Invoke(skill);
    }

    private void RefreshInteractable()
    {
        if (openSkillListButton != null)
            openSkillListButton.interactable = inputEnabled && HasVisibleSkill;
        if (activeSkillButton != null)
            activeSkillButton.interactable = inputEnabled && HasReadySkill;
        for (int i = 0; i < partnerButtons.Count; i++)
        {
            Button button = partnerButtons[i];
            if (button != null && button.gameObject.activeSelf)
                button.interactable = inputEnabled;
        }
    }

    private void PlayOpenMotion(GameObject target)
    {
        if (target == null)
            return;
        openTween?.Kill(false);
        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null)
            group = target.AddComponent<CanvasGroup>();
        RectTransform rect = target.transform as RectTransform;
        Vector2 rest = rect != null ? rect.anchoredPosition : Vector2.zero;
        group.alpha = 0f;
        if (rect != null)
            rect.anchoredPosition = rest + new Vector2(-18f, 0f);
        Sequence sequence = DOTween.Sequence().SetUpdate(true)
            .Join(group.DOFade(1f, 0.10f).SetEase(Ease.OutQuad));
        if (rect != null)
            sequence.Join(rect.DOAnchorPos(rest, 0.15f).SetEase(Ease.OutCubic));
        openTween = sequence;
    }

    private static void SetSelected(Button button, bool selected)
    {
        if (button == null)
            return;
        ColorBlock colors = button.colors;
        colors.normalColor = selected
            ? new Color(0.92f, 0.04f, 0.32f, 1f)
            : new Color(0.08f, 0.12f, 0.16f, 0.94f);
        button.colors = colors;
    }

    private void OnDisable()
    {
        openTween?.Kill(false);
    }
}
