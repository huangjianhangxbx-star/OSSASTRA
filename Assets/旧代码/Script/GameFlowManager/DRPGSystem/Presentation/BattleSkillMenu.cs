using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleSkillMenu : MonoBehaviour
{
    [SerializeField] private List<Button> buttons = new List<Button>();
    [SerializeField] private List<TMP_Text> labels = new List<TMP_Text>();
    [SerializeField] private List<BattleFormationRangeIndicator> rangeIndicators =
        new List<BattleFormationRangeIndicator>();
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private BattleSkillMenuEntryView entryTemplate;

    private readonly List<DRPGSkillSO> boundSkills =
        new List<DRPGSkillSO>();
    private readonly List<DRPGBattleSkillAvailability> boundAvailability =
        new List<DRPGBattleSkillAvailability>();
    private DRPGBattleSession session;
    private bool inputEnabled = true;
    private readonly List<BattleSkillMenuEntryView> dynamicEntries =
        new List<BattleSkillMenuEntryView>();

    public int VisibleEntryCount => entryTemplate != null
        ? boundSkills.Count
        : Mathf.Min(boundSkills.Count, buttons.Count);

    public event Action<DRPGSkillSO> SkillRequested;

    public void Bind(
        DRPGBattleSession battleSession,
        IReadOnlyList<DRPGSkillSO> skills)
    {
        var availability = new List<DRPGBattleSkillAvailability>();
        if (skills != null)
        {
            for (int i = 0; i < skills.Count; i++)
            {
                availability.Add(battleSession != null
                    ? battleSession.GetSkillAvailability(skills[i])
                    : new DRPGBattleSkillAvailability(
                        skills[i],
                        false,
                        "Battle session is unavailable.",
                        null));
            }
        }
        BindAvailability(battleSession, availability);
    }

    public void BindAvailability(
        DRPGBattleSession battleSession,
        IReadOnlyList<DRPGBattleSkillAvailability> availability)
    {
        session = battleSession;
        boundSkills.Clear();
        boundAvailability.Clear();
        if (availability != null)
        {
            for (int i = 0; i < availability.Count; i++)
            {
                DRPGBattleSkillAvailability entry = availability[i];
                if (entry == null || entry.Skill == null)
                    continue;
                boundSkills.Add(entry.Skill);
                boundAvailability.Add(entry);
            }
        }

        if (entryTemplate != null && contentRoot != null)
        {
            BindDynamicEntries();
        }
        else
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                Button button = buttons[i];
                if (button == null)
                    continue;

                button.onClick.RemoveAllListeners();
                DRPGSkillSO skill = i < boundSkills.Count
                    ? boundSkills[i]
                    : null;
                button.gameObject.SetActive(skill != null);

                if (i < labels.Count && labels[i] != null)
                    labels[i].text = skill != null
                        ? FormatAvailabilityLabel(GetAvailability(i, skill))
                        : string.Empty;
                if (i < rangeIndicators.Count && rangeIndicators[i] != null)
                {
                    rangeIndicators[i].Bind(skill != null
                        ? skill.TargetFormationRows
                        : DRPGFormationRowMask.None);
                }

                if (skill != null)
                {
                    DRPGSkillSO capturedSkill = skill;
                    button.onClick.AddListener(() => RequestSkill(capturedSkill));
                }
            }
        }

        ApplyInteractivity();
        SetFeedback(string.Empty);
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
        ApplyInteractivity();
    }

    public void SetFeedback(string value)
    {
        if (feedbackText != null)
            feedbackText.text = value ?? string.Empty;
    }

    private void RequestSkill(DRPGSkillSO skill)
    {
        if (!CanRequestSkill() || skill == null)
            return;
        DRPGBattleSkillAvailability availability =
            session.GetSkillAvailability(skill);
        if (!availability.IsAvailable)
        {
            SetFeedback(availability.UnavailableReason);
            return;
        }
        SkillRequested?.Invoke(skill);
    }

    private void ApplyInteractivity()
    {
        bool acceptsPlayerInput = CanRequestSkill();
        if (entryTemplate != null && contentRoot != null)
        {
            BindDynamicEntries();
            return;
        }
        for (int i = 0; i < buttons.Count; i++)
        {
            Button button = buttons[i];
            if (button == null)
                continue;
            DRPGSkillSO skill = i < boundSkills.Count
                ? boundSkills[i]
                : null;
            DRPGBattleSkillAvailability availability =
                GetAvailability(i, skill);
            button.interactable = acceptsPlayerInput && skill != null &&
                availability.IsAvailable;
        }
    }

    private bool CanRequestSkill()
    {
        return inputEnabled &&
            session != null &&
            session.CurrentActor != null &&
            session.CurrentActor.Side == DRPGBattleSide.Player &&
            session.IsActive &&
            session.Phase == DRPGBattlePhase.AwaitingCommand;
    }

    public void Unbind()
    {
        for (int i = 0; i < buttons.Count; i++)
            buttons[i]?.onClick.RemoveAllListeners();
        session = null;
        boundSkills.Clear();
        boundAvailability.Clear();
    }

    private void BindDynamicEntries()
    {
        EnsureDynamicPool(boundSkills.Count);
        bool acceptsPlayerInput = CanRequestSkill();
        IReadOnlyList<DRPGSkillSO> granted =
            session?.CurrentActor?.Build?.GrantedSkills;
        for (int i = 0; i < dynamicEntries.Count; i++)
        {
            DRPGSkillSO skill = i < boundSkills.Count
                ? boundSkills[i]
                : null;
            DRPGBattleSkillAvailability availability =
                GetAvailability(i, skill);
            dynamicEntries[i].Bind(
                skill,
                Contains(granted, skill),
                acceptsPlayerInput && availability.IsAvailable,
                RequestSkill,
                FormatAvailabilityLabel(availability),
                availability.UnavailableReason);
        }
    }

    private DRPGBattleSkillAvailability GetAvailability(
        int index,
        DRPGSkillSO skill)
    {
        if (index >= 0 && index < boundAvailability.Count &&
            ReferenceEquals(boundAvailability[index].Skill, skill))
        {
            return session != null
                ? session.GetSkillAvailability(skill)
                : boundAvailability[index];
        }
        return session != null
            ? session.GetSkillAvailability(skill)
            : new DRPGBattleSkillAvailability(
                skill,
                false,
                "Battle session is unavailable.",
                null);
    }

    public static string FormatAvailabilityLabel(
        DRPGBattleSkillAvailability availability)
    {
        if (availability == null || availability.Skill == null)
            return string.Empty;
        string label = availability.Skill.DisplayName;
        if (availability.Progress != null)
        {
            label += "  [" +
                availability.Progress.CurrentValue.ToString("0.##") + "/" +
                availability.Progress.Capacity.ToString("0.##") + "]";
        }
        if (!availability.IsAvailable &&
            !string.IsNullOrWhiteSpace(availability.UnavailableReason))
        {
            label += "  — " + availability.UnavailableReason;
        }
        return label;
    }

    private void EnsureDynamicPool(int required)
    {
        if (dynamicEntries.Count == 0)
        {
            dynamicEntries.Add(entryTemplate);
            entryTemplate.gameObject.SetActive(false);
        }
        while (dynamicEntries.Count < required)
        {
            BattleSkillMenuEntryView entry = Instantiate(
                entryTemplate,
                contentRoot);
            entry.name = "SkillEntry_" + dynamicEntries.Count;
            dynamicEntries.Add(entry);
        }
    }

    private static bool Contains(
        IReadOnlyList<DRPGSkillSO> source,
        DRPGSkillSO skill)
    {
        if (source == null || skill == null)
            return false;
        for (int i = 0; i < source.Count; i++)
        {
            if (ReferenceEquals(source[i], skill))
                return true;
        }
        return false;
    }

    private void OnDestroy()
    {
        Unbind();
    }
}
