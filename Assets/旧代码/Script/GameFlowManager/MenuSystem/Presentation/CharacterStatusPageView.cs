using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CharacterStatusPageView : MonoBehaviour
{
    [SerializeField] private Image portrait;
    [SerializeField] private TMP_Text professionText;
    [SerializeField] private TMP_Text statText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text experienceText;
    [SerializeField] private TMP_Text attributePointsText;
    [SerializeField] private TMP_Text skillPointsText;
    [SerializeField] private TMP_Text breakthroughText;
    [SerializeField] private TMP_Text derivedPreviewText;
    [SerializeField] private TMP_Text[] attributeValueTexts = new TMP_Text[6];
    [SerializeField] private Button[] attributePlusButtons = new Button[6];
    [SerializeField] private Button[] attributeMinusButtons = new Button[6];
    [SerializeField] private Button confirmAttributesButton;
    [SerializeField] private Button resetDraftButton;
    [SerializeField] private TMP_Text[] equippedSkillTexts = new TMP_Text[4];

    private DRPGProgressionService progression;
    private DRPGCombatantProfileSO profile;
    private DRPGCharacterProgressionView view;
    private DRPGAttributeAllocationDraft draft;
    private bool listenersBound;

    public void Bind(
        DRPGProgressionService service,
        DRPGCombatantProfileSO targetProfile,
        DRPGCharacterProgressionView targetView)
    {
        progression = service;
        profile = targetProfile;
        Bind(targetProfile, targetView);
    }

    public void Bind(
        DRPGCombatantProfileSO targetProfile,
        DRPGCharacterProgressionView targetView)
    {
        profile = targetProfile;
        view = targetView;
        if (profile == null || view == null)
            return;

        BindListeners();
        draft = progression != null
            ? progression.BeginAttributeDraft(profile)
            : null;
        if (portrait != null)
        {
            portrait.sprite = profile.FullPortrait;
            portrait.enabled = portrait.sprite != null;
            portrait.preserveAspect = true;
        }

        professionText?.SetText(
            view.ActiveProfession != null
                ? view.ActiveProfession.DisplayName
                : "LEGACY");
        levelText?.SetText($"LV {view.Level:00}");
        experienceText?.SetText(
            view.ExperienceToNext > 0
                ? $"EXP  {view.Experience:N0} / {view.ExperienceToNext:N0}"
                : "EXP  MAX");
        skillPointsText?.SetText(
            $"SKILL POINTS  {view.AvailableSkillPoints:00}");
        breakthroughText?.SetText(
            view.AllowsLevelBreakthrough
                ? view.BreakthroughUnlocked
                    ? "BREAKTHROUGH // ACTIVE"
                    : "BREAKTHROUGH // LOCKED"
                : "NORMAL CAP // 80");

        for (int i = 0; i < equippedSkillTexts.Length; i++)
        {
            DRPGSkillSO skill = i < view.EquippedSkills.Count
                ? view.EquippedSkills[i]
                : null;
            equippedSkillTexts[i]?.SetText(
                skill != null ? skill.DisplayName : "— EMPTY —");
        }
        RefreshDraft();
    }

    private void BindListeners()
    {
        if (listenersBound)
            return;
        for (int i = 0; i < attributePlusButtons.Length; i++)
        {
            int captured = i;
            attributePlusButtons[i]?.onClick.AddListener(
                () => ChangeAttribute((DRPGCoreAttributeId)captured, 1));
        }
        for (int i = 0; i < attributeMinusButtons.Length; i++)
        {
            int captured = i;
            attributeMinusButtons[i]?.onClick.AddListener(
                () => ChangeAttribute((DRPGCoreAttributeId)captured, -1));
        }
        confirmAttributesButton?.onClick.AddListener(CommitDraft);
        resetDraftButton?.onClick.AddListener(ResetDraft);
        listenersBound = true;
    }

    private void ChangeAttribute(DRPGCoreAttributeId attribute, int direction)
    {
        if (draft == null)
            return;
        bool changed = direction > 0
            ? draft.TryAdd(attribute)
            : draft.TryRemove(attribute);
        if (changed)
            RefreshDraft();
    }

    private void CommitDraft()
    {
        if (progression == null || profile == null || draft == null ||
            draft.SpentPoints <= 0)
        {
            return;
        }
        DRPGProgressionOperationResult result =
            progression.TryCommitAttributeDraft(profile, draft);
        if (!result.Success)
            return;
        Bind(progression, profile, progression.GetView(profile));
    }

    private void ResetDraft()
    {
        draft?.Reset();
        RefreshDraft();
    }

    private void RefreshDraft()
    {
        if (view == null || profile == null)
            return;
        DRPGCoreAttributes attributes = draft != null
            ? draft.Candidate
            : view.Attributes;
        DRPGDerivedStats derived = DRPGDerivedStatResolver.Resolve(
            profile,
            profile.ProgressionDefinition != null
                ? profile.ProgressionDefinition.InitialAttributes
                : DRPGCoreAttributes.Default,
            attributes,
            view.Level);
        string[] labels = { "体魄", "筋力", "耐力", "技巧", "式络", "反应" };
        for (int i = 0; i < attributeValueTexts.Length; i++)
        {
            if (attributeValueTexts[i] != null)
            {
                attributeValueTexts[i].SetText(
                    $"{labels[i]}   {attributes.Get((DRPGCoreAttributeId)i):00}");
            }
            if (i < attributePlusButtons.Length && attributePlusButtons[i] != null)
            {
                attributePlusButtons[i].interactable = draft != null &&
                    draft.RemainingPoints > 0 &&
                    attributes.Get((DRPGCoreAttributeId)i) < DRPGGrowthRules.AttributeCap;
            }
            if (i < attributeMinusButtons.Length && attributeMinusButtons[i] != null)
            {
                attributeMinusButtons[i].interactable = draft != null &&
                    attributes.Get((DRPGCoreAttributeId)i) >
                    draft.Original.Get((DRPGCoreAttributeId)i);
            }
        }

        int remaining = draft != null
            ? draft.RemainingPoints
            : view.AvailableAttributePoints;
        attributePointsText?.SetText($"ATTRIBUTE POINTS  {remaining:00}");
        statText?.SetText(
            $"HP  {derived.MaxHp:0}     MP  {derived.MaxMp:0}\n" +
            $"POSTURE  {derived.MaxPosture:0}     ENERGY  {derived.ExpeditionEnergy:0}");
        derivedPreviewText?.SetText(
            $"SPEED {derived.SpeedScore:0.0}   ACC +{derived.AccuracyBonus * 100f:0.#}%\n" +
            $"WEAK ×{derived.WeakPointDamageMultiplier:0.00}   GUARD ×{derived.GuardWindowMultiplier:0.00}");
        if (confirmAttributesButton != null)
            confirmAttributesButton.interactable = draft != null && draft.SpentPoints > 0;
        if (resetDraftButton != null)
            resetDraftButton.interactable = draft != null && draft.SpentPoints > 0;
    }
}
