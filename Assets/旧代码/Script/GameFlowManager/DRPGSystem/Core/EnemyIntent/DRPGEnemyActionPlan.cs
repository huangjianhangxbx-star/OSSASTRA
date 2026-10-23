using System;

public sealed class DRPGEnemyActionPlan
{
    private DRPGEnemyActionPlan(
        long planId,
        int actorRuntimeId,
        DRPGEnemyPlannedActionKind actionKind,
        DRPGSkillSO skill,
        float plannedAtTime,
        float readyAtTime,
        int maximumWarningValue,
        int currentWarningValue,
        int committedTargetRuntimeId,
        long warningZeroPresentedAtPlayerSerial,
        DRPGEnemyPlanState state,
        DRPGFormationRow? destinationFormationRow)
    {
        PlanId = planId;
        ActorRuntimeId = actorRuntimeId;
        ActionKind = actionKind;
        Skill = skill;
        PlannedAtTime = plannedAtTime;
        ReadyAtTime = readyAtTime;
        MaximumWarningValue = maximumWarningValue;
        CurrentWarningValue = currentWarningValue;
        CommittedTargetRuntimeId = committedTargetRuntimeId;
        WarningZeroPresentedAtPlayerSerial =
            warningZeroPresentedAtPlayerSerial;
        State = state;
        DestinationFormationRow = destinationFormationRow;
    }

    public long PlanId { get; }
    public int ActorRuntimeId { get; }
    public DRPGEnemyPlannedActionKind ActionKind { get; }
    public DRPGSkillSO Skill { get; }
    public float PlannedAtTime { get; }
    public float ReadyAtTime { get; }
    public int MaximumWarningValue { get; }
    public int CurrentWarningValue { get; }
    public DRPGEnemyPlanState State { get; }
    public DRPGFormationRow? DestinationFormationRow { get; }
    internal int CommittedTargetRuntimeId { get; }
    internal long WarningZeroPresentedAtPlayerSerial { get; }
    public bool CanBeInterruptedByPostureBreak =>
        ActionKind == DRPGEnemyPlannedActionKind.Attack &&
        Skill != null &&
        DRPGPostureBreakInterruptionRules.CanInterrupt(
            Skill.PostureBreakInterruptionPolicy,
            State);

    public static DRPGEnemyActionPlan CreateAttack(
        long planId,
        int actorRuntimeId,
        DRPGSkillSO skill,
        float plannedAtTime,
        float readyAtTime,
        int maximumWarningValue)
    {
        ValidateIdentityAndTime(
            planId,
            actorRuntimeId,
            plannedAtTime,
            readyAtTime);
        if (skill == null)
            throw new ArgumentNullException(nameof(skill));
        if (maximumWarningValue < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumWarningValue));
        }

        int initialWarning = CalculateWarningValue(
            plannedAtTime,
            plannedAtTime,
            readyAtTime,
            maximumWarningValue);
        return new DRPGEnemyActionPlan(
            planId,
            actorRuntimeId,
            DRPGEnemyPlannedActionKind.Attack,
            skill,
            plannedAtTime,
            readyAtTime,
            maximumWarningValue,
            initialWarning,
            0,
            -1L,
            DRPGEnemyPlanState.Windup,
            null);
    }

    public static DRPGEnemyActionPlan CreateReposition(
        long planId,
        int actorRuntimeId,
        float plannedAtTime,
        float readyAtTime,
        DRPGFormationRow destinationFormationRow)
    {
        ValidateIdentityAndTime(
            planId,
            actorRuntimeId,
            plannedAtTime,
            readyAtTime);
        return new DRPGEnemyActionPlan(
            planId,
            actorRuntimeId,
            DRPGEnemyPlannedActionKind.Reposition,
            null,
            plannedAtTime,
            readyAtTime,
            0,
            0,
            0,
            -1L,
            DRPGEnemyPlanState.Scheduled,
            destinationFormationRow);
    }

    public static DRPGEnemyActionPlan CreateSupportSkill(
        long planId,
        int actorRuntimeId,
        DRPGSkillSO skill,
        int targetRuntimeId,
        float plannedAtTime,
        float readyAtTime)
    {
        ValidateIdentityAndTime(
            planId,
            actorRuntimeId,
            plannedAtTime,
            readyAtTime);
        if (skill == null)
            throw new ArgumentNullException(nameof(skill));
        if (targetRuntimeId <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetRuntimeId));

        return new DRPGEnemyActionPlan(
            planId,
            actorRuntimeId,
            DRPGEnemyPlannedActionKind.SupportSkill,
            skill,
            plannedAtTime,
            readyAtTime,
            0,
            0,
            targetRuntimeId,
            -1L,
            DRPGEnemyPlanState.Scheduled,
            null);
    }

    public static DRPGEnemyActionPlan CreateWait(
        long planId,
        int actorRuntimeId,
        float plannedAtTime,
        float readyAtTime)
    {
        ValidateIdentityAndTime(
            planId,
            actorRuntimeId,
            plannedAtTime,
            readyAtTime);
        return new DRPGEnemyActionPlan(
            planId,
            actorRuntimeId,
            DRPGEnemyPlannedActionKind.Wait,
            null,
            plannedAtTime,
            readyAtTime,
            0,
            0,
            0,
            -1L,
            DRPGEnemyPlanState.Scheduled,
            null);
    }

    public int SampleWarningValue(float sampleTime)
    {
        if (ActionKind != DRPGEnemyPlannedActionKind.Attack)
            return 0;
        return CalculateWarningValue(
            sampleTime,
            PlannedAtTime,
            ReadyAtTime,
            MaximumWarningValue);
    }

    public DRPGEnemyActionPlan WithSampledWarning(float sampleTime)
    {
        EnsureState(DRPGEnemyPlanState.Windup, "sample warning");
        return Copy(
            currentWarningValue: SampleWarningValue(sampleTime));
    }

    public DRPGEnemyActionPlan CommitTarget(
        int targetRuntimeId,
        long presentedAtPlayerSerial)
    {
        EnsureAttack();
        EnsureState(DRPGEnemyPlanState.Windup, "commit target");
        if (targetRuntimeId <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetRuntimeId));
        if (presentedAtPlayerSerial < 0L)
        {
            throw new ArgumentOutOfRangeException(
                nameof(presentedAtPlayerSerial));
        }

        return Copy(
            currentWarningValue: 0,
            committedTargetRuntimeId: targetRuntimeId,
            warningZeroPresentedAtPlayerSerial: presentedAtPlayerSerial,
            state: DRPGEnemyPlanState.AwaitingPlayerResponse);
    }

    public DRPGEnemyActionPlan WithSatisfiedPlayerResponse(
        long completedPlayerActionSerial)
    {
        EnsureState(
            DRPGEnemyPlanState.AwaitingPlayerResponse,
            "satisfy player response");
        if (completedPlayerActionSerial <=
            WarningZeroPresentedAtPlayerSerial)
        {
            return this;
        }

        return Copy(state: DRPGEnemyPlanState.Executable);
    }

    public DRPGEnemyActionPlan MarkExecuting()
    {
        EnsureState(DRPGEnemyPlanState.Executable, "mark executing");
        return Copy(state: DRPGEnemyPlanState.Executing);
    }

    public DRPGEnemyActionPlan MarkReady()
    {
        EnsureState(DRPGEnemyPlanState.Scheduled, "mark ready");
        return Copy(state: DRPGEnemyPlanState.Executable);
    }

    public DRPGEnemyActionPlan ReturnToExecutableAfterForcedSkip()
    {
        EnsureState(
            DRPGEnemyPlanState.Executing,
            "return to executable after forced skip");
        return Copy(state: DRPGEnemyPlanState.Executable);
    }

    public DRPGEnemyActionPlan ConvertToWait()
    {
        if (State != DRPGEnemyPlanState.Windup &&
            State != DRPGEnemyPlanState.AwaitingPlayerResponse)
        {
            throw new InvalidOperationException(
                $"Cannot convert plan state {State} to Wait.");
        }

        return new DRPGEnemyActionPlan(
            PlanId,
            ActorRuntimeId,
            DRPGEnemyPlannedActionKind.Wait,
            null,
            PlannedAtTime,
            ReadyAtTime,
            0,
            0,
            0,
            -1L,
            DRPGEnemyPlanState.Executable,
            null);
    }

    public DRPGEnemyActionPlanSnapshot CreatePublicSnapshot()
    {
        int disclosedTargetRuntimeId = 0;
        if (ActionKind == DRPGEnemyPlannedActionKind.Attack &&
            CommittedTargetRuntimeId > 0 &&
            Skill != null &&
            Skill.EnemyTargetDisclosureMode ==
                DRPGEnemyTargetDisclosureMode.RevealAtCommit)
        {
            disclosedTargetRuntimeId = CommittedTargetRuntimeId;
        }

        DRPGDamageTag attribute = DRPGDamageTag.None;
        if (Skill != null)
        {
            for (int i = 0; i < Skill.Effects.Count; i++)
            {
                DRPGActionEffectDefinition effect = Skill.Effects[i];
                if (effect != null && effect.Kind == DRPGActionEffectKind.Damage)
                {
                    attribute = effect.DamageTags;
                    break;
                }
            }
        }
        int? panel = Skill != null &&
            ActionKind == DRPGEnemyPlannedActionKind.Attack
                ? BattleEffectPanel.Calculate(Skill.Power, 0d, 0d)
                : (int?)null;
        return new DRPGEnemyActionPlanSnapshot(
            PlanId,
            ActorRuntimeId,
            ActionKind,
            State,
            CurrentWarningValue,
            MaximumWarningValue,
            disclosedTargetRuntimeId,
            Skill != null ? Skill.SkillId : string.Empty,
            Skill != null ? Skill.DisplayName : string.Empty,
            Skill != null ? Skill.Icon : null,
            attribute,
            panel);
    }

    private DRPGEnemyActionPlan Copy(
        int? currentWarningValue = null,
        int? committedTargetRuntimeId = null,
        long? warningZeroPresentedAtPlayerSerial = null,
        DRPGEnemyPlanState? state = null)
    {
        return new DRPGEnemyActionPlan(
            PlanId,
            ActorRuntimeId,
            ActionKind,
            Skill,
            PlannedAtTime,
            ReadyAtTime,
            MaximumWarningValue,
            currentWarningValue ?? CurrentWarningValue,
            committedTargetRuntimeId ?? CommittedTargetRuntimeId,
            warningZeroPresentedAtPlayerSerial ??
                WarningZeroPresentedAtPlayerSerial,
            state ?? State,
            DestinationFormationRow);
    }

    private static int CalculateWarningValue(
        float sampleTime,
        float plannedAtTime,
        float readyAtTime,
        int maximumWarningValue)
    {
        if (maximumWarningValue <= 0 ||
            sampleTime >= readyAtTime ||
            readyAtTime <= plannedAtTime)
        {
            return 0;
        }

        float remainingRatio = Math.Max(
            0f,
            Math.Min(
                1f,
                (readyAtTime - sampleTime) /
                (readyAtTime - plannedAtTime)));
        int warning = (int)Math.Ceiling(
            maximumWarningValue * remainingRatio);
        return Math.Max(1, Math.Min(maximumWarningValue, warning));
    }

    private static void ValidateIdentityAndTime(
        long planId,
        int actorRuntimeId,
        float plannedAtTime,
        float readyAtTime)
    {
        if (planId <= 0L)
            throw new ArgumentOutOfRangeException(nameof(planId));
        if (actorRuntimeId <= 0)
            throw new ArgumentOutOfRangeException(nameof(actorRuntimeId));
        if (float.IsNaN(plannedAtTime) ||
            float.IsInfinity(plannedAtTime))
        {
            throw new ArgumentOutOfRangeException(nameof(plannedAtTime));
        }
        if (float.IsNaN(readyAtTime) ||
            float.IsInfinity(readyAtTime) ||
            readyAtTime < plannedAtTime)
        {
            throw new ArgumentOutOfRangeException(nameof(readyAtTime));
        }
    }

    private void EnsureAttack()
    {
        if (ActionKind != DRPGEnemyPlannedActionKind.Attack)
        {
            throw new InvalidOperationException(
                "Only attack plans support this transition.");
        }
    }

    private void EnsureState(DRPGEnemyPlanState expected, string operation)
    {
        if (State != expected)
        {
            throw new InvalidOperationException(
                $"Cannot {operation} while plan state is {State}.");
        }
    }
}
