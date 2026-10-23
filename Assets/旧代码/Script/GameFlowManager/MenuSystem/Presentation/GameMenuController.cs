using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GameMenuController : MonoBehaviour
{
    [Header("Shell")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform frame;
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private TMP_Text pageTitleText;
    [SerializeField] private Button previousCharacterButton;
    [SerializeField] private Button nextCharacterButton;
    [SerializeField] private Button statusButton;
    [SerializeField] private Button skillTreeButton;
    [SerializeField] private Button loadoutButton;
    [SerializeField] private Button equipmentButton;
    [SerializeField] private Button resonanceButton;
    [SerializeField] private Button closeButton;

    [Header("Pages")]
    [SerializeField] private CharacterStatusPageView statusPage;
    [SerializeField] private SkillTreePageView skillTreePage;
    [SerializeField] private SkillLoadoutPageView loadoutPage;
    [SerializeField] private EquipmentPageView equipmentPage;
    [SerializeField] private ResonancePageView resonancePage;
    [SerializeField] private GameMenuTooltipView tooltip;

    [Header("Characters")]
    [SerializeField] private List<DRPGCombatantProfileSO> selectableProfiles =
        new List<DRPGCombatantProfileSO>();
    [SerializeField] private int defaultProfileIndex;

    private static string lastProfileId;
    private static GameMenuPageId lastPage = GameMenuPageId.CharacterStatus;
    private DRPGProgressionService progression;
    private DRPGEquipmentRoot equipmentRoot;
    private DRPGPartyCompositionState partyComposition;
    private int profileIndex;
    private Sequence ownedTween;
    private Tween pageTween;
    private Vector2 frameRestPosition;
    private bool frameRestCaptured;
    private bool listenersBound;

    public bool IsOpen { get; private set; }
    public DRPGCombatantProfileSO CurrentProfile =>
        selectableProfiles.Count > 0 &&
        profileIndex >= 0 && profileIndex < selectableProfiles.Count
            ? selectableProfiles[profileIndex]
            : null;
    public IReadOnlyList<DRPGCombatantProfileSO> SelectableProfiles =>
        selectableProfiles;

    public void Initialize(DRPGProgressionService service)
    {
        Initialize(service, null);
    }

    public void Initialize(
        DRPGProgressionService service,
        DRPGEquipmentRoot equipment)
    {
        Initialize(service, equipment, null);
    }

    public void Initialize(
        DRPGProgressionService service,
        DRPGEquipmentRoot equipment,
        DRPGPartyCompositionState party)
    {
        progression = service ?? throw new ArgumentNullException(nameof(service));
        equipmentRoot = equipment;
        partyComposition = party;
        BindListeners();
        CaptureFrameRestPosition();
        ResolveInitialProfile();
        HideImmediate();
    }

    public void Show()
    {
        if (progression == null)
            throw new InvalidOperationException("GameMenuController is not initialized.");
        ResolveInitialProfile();
        gameObject.SetActive(true);
        IsOpen = true;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        Refresh(lastPage);

        KillTween();
        canvasGroup.alpha = 0f;
        CaptureFrameRestPosition();
        frame.anchoredPosition = frameRestPosition + new Vector2(-72f, 0f);
        frame.localScale = new Vector3(0.985f, 0.985f, 1f);
        ownedTween = DOTween.Sequence().SetUpdate(true)
            .Join(canvasGroup.DOFade(1f, 0.14f).SetEase(Ease.OutQuad))
            .Join(frame.DOAnchorPos(frameRestPosition, 0.20f).SetEase(Ease.OutCubic))
            .Join(frame.DOScale(1f, 0.22f).SetEase(Ease.OutBack, 1.15f));
    }

    public void HideAnimated(Action completed)
    {
        if (!IsOpen)
        {
            completed?.Invoke();
            return;
        }

        IsOpen = false;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        KillTween();
        ownedTween = DOTween.Sequence().SetUpdate(true)
            .Join(canvasGroup.DOFade(0f, 0.12f).SetEase(Ease.InQuad))
            .Join(frame.DOScale(0.975f, 0.12f).SetEase(Ease.InQuad))
            .OnComplete(() =>
            {
                gameObject.SetActive(false);
                completed?.Invoke();
            });
    }

    public void HideImmediate()
    {
        KillTween();
        IsOpen = false;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        if (frame != null)
        {
            frame.localScale = Vector3.one;
            CaptureFrameRestPosition();
            frame.anchoredPosition = frameRestPosition;
        }
        gameObject.SetActive(false);
    }

    public bool TryNavigateBack()
    {
        if (lastPage == GameMenuPageId.CharacterStatus)
            return false;
        Refresh(GameMenuPageId.CharacterStatus);
        return true;
    }

    private void BindListeners()
    {
        if (listenersBound)
            return;
        previousCharacterButton?.onClick.AddListener(() => ChangeCharacter(-1));
        nextCharacterButton?.onClick.AddListener(() => ChangeCharacter(1));
        statusButton?.onClick.AddListener(
            () => Refresh(GameMenuPageId.CharacterStatus));
        skillTreeButton?.onClick.AddListener(
            () => Refresh(GameMenuPageId.SkillTree));
        loadoutButton?.onClick.AddListener(
            () => Refresh(GameMenuPageId.SkillLoadout));
        equipmentButton?.onClick.AddListener(
            () => Refresh(GameMenuPageId.Equipment));
        resonanceButton?.onClick.AddListener(
            () => Refresh(GameMenuPageId.Resonance));
        closeButton?.onClick.AddListener(RequestClose);
        listenersBound = true;
    }

    private void RequestClose()
    {
        GameMenuInputRouter router = GetComponentInParent<GameMenuInputRouter>();
        if (router == null && GameUIRoot.Instance != null)
            router = GameUIRoot.Instance.GetComponent<GameMenuInputRouter>();
        router?.CloseAnimated();
    }

    private void ResolveInitialProfile()
    {
        if (selectableProfiles.Count == 0)
        {
            profileIndex = -1;
            return;
        }

        profileIndex = Mathf.Clamp(defaultProfileIndex, 0, selectableProfiles.Count - 1);
        if (string.IsNullOrWhiteSpace(lastProfileId))
            return;
        for (int i = 0; i < selectableProfiles.Count; i++)
        {
            DRPGCombatantProfileSO profile = selectableProfiles[i];
            if (profile != null && profile.CombatantId == lastProfileId)
            {
                profileIndex = i;
                return;
            }
        }
    }

    private void ChangeCharacter(int delta)
    {
        if (selectableProfiles.Count == 0)
            return;
        profileIndex = (profileIndex + delta + selectableProfiles.Count) %
            selectableProfiles.Count;
        tooltip?.Hide();
        Refresh(lastPage);
    }

    private void Refresh(GameMenuPageId page)
    {
        DRPGCombatantProfileSO profile = CurrentProfile;
        if (profile == null || profile.ProgressionDefinition == null)
        {
            characterNameText?.SetText("NO PROGRESSION PROFILE");
            return;
        }

        if (page == GameMenuPageId.Resonance &&
            !CanShowResonance(profile))
        {
            page = GameMenuPageId.CharacterStatus;
        }
        lastPage = page;

        lastProfileId = profile.CombatantId;
        DRPGCharacterProgressionView view = progression.GetView(profile);
        characterNameText?.SetText(profile.DisplayName);
        pageTitleText?.SetText(PageTitle(page));
        statusPage?.gameObject.SetActive(page == GameMenuPageId.CharacterStatus);
        skillTreePage?.gameObject.SetActive(page == GameMenuPageId.SkillTree);
        loadoutPage?.gameObject.SetActive(page == GameMenuPageId.SkillLoadout);
        equipmentPage?.gameObject.SetActive(page == GameMenuPageId.Equipment);
        resonancePage?.gameObject.SetActive(page == GameMenuPageId.Resonance);
        if (resonanceButton != null)
            resonanceButton.gameObject.SetActive(CanShowResonance(profile));
        SetNavigationVisual(page);

        if (page == GameMenuPageId.CharacterStatus)
            statusPage?.Bind(progression, profile, view);
        else if (page == GameMenuPageId.SkillTree)
            skillTreePage?.Bind(progression, profile, tooltip);
        else if (page == GameMenuPageId.SkillLoadout)
            loadoutPage?.Bind(progression, profile, tooltip);
        else if (page == GameMenuPageId.Resonance)
            resonancePage?.Bind(
                progression.ResonanceService,
                partyComposition,
                profile,
                tooltip);
        else
            equipmentPage?.Bind(progression, equipmentRoot, profile);
        AnimatePage(page);
    }

    private static string PageTitle(GameMenuPageId page)
    {
        switch (page)
        {
            case GameMenuPageId.SkillTree: return "SKILL TREE / 技能树";
            case GameMenuPageId.SkillLoadout: return "SKILL LOADOUT / 技能配置";
            case GameMenuPageId.Equipment: return "EQUIPMENT / 装备";
            case GameMenuPageId.Resonance: return "RESONANCE / 同律";
            default: return "CHARACTER STATUS / 角色状态";
        }
    }

    private void KillTween()
    {
        ownedTween?.Kill(false);
        ownedTween = null;
    }

    private void CaptureFrameRestPosition()
    {
        if (frameRestCaptured || frame == null)
            return;
        frameRestPosition = frame.anchoredPosition;
        frameRestCaptured = true;
    }

    private void AnimatePage(GameMenuPageId page)
    {
        GameObject target;
        switch (page)
        {
            case GameMenuPageId.SkillTree:
                target = skillTreePage?.gameObject;
                break;
            case GameMenuPageId.SkillLoadout:
                target = loadoutPage?.gameObject;
                break;
            case GameMenuPageId.Equipment:
                target = equipmentPage?.gameObject;
                break;
            case GameMenuPageId.Resonance:
                target = resonancePage?.gameObject;
                break;
            default:
                target = statusPage?.gameObject;
                break;
        }
        if (target == null)
            return;
        pageTween?.Complete(true);
        pageTween?.Kill(false);
        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null)
            group = target.AddComponent<CanvasGroup>();
        RectTransform rect = target.transform as RectTransform;
        Vector2 rest = rect != null ? rect.anchoredPosition : Vector2.zero;
        group.alpha = 0f;
        if (rect != null)
            rect.anchoredPosition = rest + new Vector2(24f, 0f);
        Sequence sequence = DOTween.Sequence().SetUpdate(true)
            .Join(group.DOFade(1f, 0.12f).SetEase(Ease.OutQuad));
        if (rect != null)
            sequence.Join(rect.DOAnchorPos(rest, 0.18f).SetEase(Ease.OutCubic));
        pageTween = sequence;
    }

    private void SetNavigationVisual(GameMenuPageId page)
    {
        SetButtonSelected(statusButton, page == GameMenuPageId.CharacterStatus);
        SetButtonSelected(skillTreeButton, page == GameMenuPageId.SkillTree);
        SetButtonSelected(loadoutButton, page == GameMenuPageId.SkillLoadout);
        SetButtonSelected(equipmentButton, page == GameMenuPageId.Equipment);
        SetButtonSelected(resonanceButton, page == GameMenuPageId.Resonance);
    }

    private bool CanShowResonance(DRPGCombatantProfileSO profile)
    {
        return progression != null && progression.ResonanceService != null &&
            partyComposition != null &&
            progression.ResonanceService.Definition.IsProtagonist(profile);
    }

    private static void SetButtonSelected(Button button, bool selected)
    {
        if (button == null)
            return;
        ColorBlock colors = button.colors;
        colors.normalColor = selected
            ? new Color(1f, 0.035f, 0.36f, 1f)
            : new Color(0.10f, 0.025f, 0.055f, 1f);
        button.colors = colors;
    }

    private void OnDestroy()
    {
        KillTween();
        pageTween?.Kill(false);
        resonancePage?.Unbind();
    }
}
