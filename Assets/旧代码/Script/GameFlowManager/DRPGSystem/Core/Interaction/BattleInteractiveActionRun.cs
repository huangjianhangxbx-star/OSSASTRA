using System;
using System.Collections.Generic;

public sealed class BattleInteractiveActionRun
{
    private readonly DRPGGuardTimingSequence guardSequence;
    private readonly DRPGFreeStrikeTimingSequence strikeSequence;
    private readonly List<DRPGGuardImpactDecision> pendingGuardDecisions =
        new List<DRPGGuardImpactDecision>();

    private BattleInteractiveActionRun(
        DRPGBattleInteractionKind kind,
        DRPGSkillSO skill,
        DRPGGuardTimingSequence guardSequence,
        DRPGFreeStrikeTimingSequence strikeSequence,
        bool guardInputAllowed = true,
        string guardUnavailableReason = "")
    {
        Kind = kind;
        Skill = skill ?? throw new ArgumentNullException(nameof(skill));
        this.guardSequence = guardSequence;
        this.strikeSequence = strikeSequence;
        GuardInputAllowed = guardInputAllowed;
        GuardUnavailableReason = guardInputAllowed
            ? string.Empty
            : guardUnavailableReason ?? string.Empty;
    }

    public DRPGBattleInteractionKind Kind { get; }
    public DRPGSkillSO Skill { get; }
    public float Elapsed { get; private set; }
    public bool IsComplete { get; private set; }
    public DRPGBattleInteractionEndReason EndReason { get; private set; } =
        DRPGBattleInteractionEndReason.Completed;
    public float Duration => Kind == DRPGBattleInteractionKind.Defense
        ? guardSequence.Duration
        : strikeSequence.Duration;
    public float NormalizedRemaining => Duration <= 0f
        ? 0f
        : Math.Max(0f, 1f - Elapsed / Duration);
    public int NextGuardImpactIndex => guardSequence != null
        ? guardSequence.NextImpactIndex
        : -1;
    public float NextGuardImpactTime => guardSequence != null
        ? guardSequence.NextImpactTime
        : 0f;
    public bool GuardInputAllowed { get; }
    public string GuardUnavailableReason { get; }

    public static BattleInteractiveActionRun CreateDefense(DRPGSkillSO skill)
    {
        return CreateDefenseWithAvailability(skill, true, string.Empty);
    }

    public static BattleInteractiveActionRun CreateDefenseWithAvailability(
        DRPGSkillSO skill,
        bool guardInputAllowed,
        string guardUnavailableReason)
    {
        if (skill == null)
            throw new ArgumentNullException(nameof(skill));
        return new BattleInteractiveActionRun(
            DRPGBattleInteractionKind.Defense,
            skill,
            new DRPGGuardTimingSequence(
                skill.SpecialImpactTimes,
                skill.GuardLeadSeconds,
                skill.PerfectLeadSeconds),
            null,
            guardInputAllowed,
            guardUnavailableReason);
    }

    public static BattleInteractiveActionRun CreateDefenseFromSpec(
        DRPGSkillSO skill,
        DRPGResolvedGuardSpec spec)
    {
        return CreateDefenseFromSpecWithAvailability(
            skill,
            spec,
            true,
            string.Empty);
    }

    public static BattleInteractiveActionRun
        CreateDefenseFromSpecWithAvailability(
            DRPGSkillSO skill,
            DRPGResolvedGuardSpec spec,
            bool guardInputAllowed,
            string guardUnavailableReason)
    {
        if (skill == null)
            throw new ArgumentNullException(nameof(skill));
        if (spec == null)
            throw new ArgumentNullException(nameof(spec));
        return new BattleInteractiveActionRun(
            DRPGBattleInteractionKind.Defense,
            skill,
            new DRPGGuardTimingSequence(
                spec.ImpactTimes,
                spec.GuardLeadSeconds,
                spec.PerfectLeadSeconds),
            null,
            guardInputAllowed,
            guardUnavailableReason);
    }

    public static BattleInteractiveActionRun CreateFreeStrike(
        DRPGSkillSO skill,
        DRPGResolvedFreeStrikeSpec spec)
    {
        if (skill == null)
            throw new ArgumentNullException(nameof(skill));
        return new BattleInteractiveActionRun(
            DRPGBattleInteractionKind.FreeStrike,
            skill,
            null,
            new DRPGFreeStrikeTimingSequence(
                spec.Duration,
                spec.Cadence));
    }

    public void Advance(float unscaledDeltaTime)
    {
        if (unscaledDeltaTime < 0f)
            throw new ArgumentOutOfRangeException(nameof(unscaledDeltaTime));
        if (IsComplete)
            return;

        Elapsed += unscaledDeltaTime;
        if (Kind == DRPGBattleInteractionKind.Defense)
        {
            IReadOnlyList<DRPGGuardImpactDecision> decisions =
                guardSequence.Advance(Elapsed);
            for (int i = 0; i < decisions.Count; i++)
                pendingGuardDecisions.Add(decisions[i]);
            if (guardSequence.IsComplete)
                End(DRPGBattleInteractionEndReason.Completed);
        }
        else if (strikeSequence.Advance(Elapsed))
        {
            End(DRPGBattleInteractionEndReason.Completed);
        }
    }

    public bool SubmitGuard()
    {
        return GuardInputAllowed && !IsComplete && guardSequence != null &&
            guardSequence.SubmitGuard(Elapsed);
    }

    public bool TryAcceptStrike()
    {
        return !IsComplete && strikeSequence != null &&
            strikeSequence.TryAccept(Elapsed);
    }

    public IReadOnlyList<DRPGGuardImpactDecision> DrainGuardDecisions()
    {
        if (pendingGuardDecisions.Count == 0)
            return Array.Empty<DRPGGuardImpactDecision>();
        DRPGGuardImpactDecision[] result = pendingGuardDecisions.ToArray();
        pendingGuardDecisions.Clear();
        return result;
    }

    public void End(DRPGBattleInteractionEndReason reason)
    {
        if (IsComplete)
            return;
        IsComplete = true;
        EndReason = reason;
    }
}
