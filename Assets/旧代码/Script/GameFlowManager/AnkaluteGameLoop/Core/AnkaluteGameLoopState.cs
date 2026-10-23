using System;

public sealed class AnkaluteGameLoopState : IAnkaluteGameLoop
{
    private readonly IStrategicHubFlow strategicHub;
    private Action<AnkaluteGameLoopTransition> transitioned;
    private bool isProcessing;

    public AnkaluteGameLoopSnapshot Current { get; private set; }
    public event Action<AnkaluteGameLoopTransition> Transitioned
    {
        add { transitioned += value; }
        remove { transitioned -= value; }
    }

    public AnkaluteGameLoopState(IStrategicHubFlow strategicHub)
    {
        this.strategicHub = strategicHub ??
            throw new ArgumentNullException(nameof(strategicHub));
        Current = new AnkaluteGameLoopSnapshot(
            AnkaluteGameLoopPhase.Title,
            false,
            default,
            false,
            0L);
    }

    public AnkaluteGameLoopRequestResult Request(
        AnkaluteGameLoopIntent intent)
    {
        if (isProcessing)
            return Reject(AnkaluteGameLoopRequestError.Busy);

        isProcessing = true;
        try
        {
            return Apply(intent);
        }
        finally
        {
            isProcessing = false;
        }
    }

    private AnkaluteGameLoopRequestResult Apply(
        AnkaluteGameLoopIntent intent)
    {
        if (intent.Kind == AnkaluteGameLoopIntentKind.EnterStrategicHub)
        {
            if (Current.Phase != AnkaluteGameLoopPhase.Title)
                return Reject(AnkaluteGameLoopRequestError.InvalidPhase);
            StrategicHubEntryReason hubReason;
            switch (intent.SessionEntryReason)
            {
                case SessionEntryReason.NewGame:
                    hubReason = StrategicHubEntryReason.NewGame;
                    break;
                case SessionEntryReason.LoadedGame:
                    hubReason = StrategicHubEntryReason.LoadedGame;
                    break;
                case SessionEntryReason.ReenterAfterTitle:
                    hubReason = StrategicHubEntryReason.ReenterAfterTitle;
                    break;
                default:
                    return Reject(AnkaluteGameLoopRequestError.InvalidPayload);
            }
            StrategicHubRequestResult hubResult = strategicHub.Request(
                StrategicHubIntent.ResetForEntry(hubReason));
            if (hubResult.Status != StrategicHubRequestStatus.Applied &&
                hubResult.Status != StrategicHubRequestStatus.NoOp)
            {
                return RejectHub(hubResult);
            }
            return Commit(
                AnkaluteGameLoopPhase.StrategicHub,
                false,
                default,
                intent);
        }

        if (intent.Kind == AnkaluteGameLoopIntentKind.LaunchExpedition)
        {
            if (Current.Phase != AnkaluteGameLoopPhase.StrategicHub)
                return Reject(AnkaluteGameLoopRequestError.InvalidPhase);
            if (!intent.ExpeditionRequest.IsValid)
                return Reject(AnkaluteGameLoopRequestError.InvalidPayload);
            return Commit(
                AnkaluteGameLoopPhase.ExpeditionExploration,
                true,
                intent.ExpeditionRequest.ExpeditionId,
                intent);
        }

        if (intent.Kind == AnkaluteGameLoopIntentKind.BeginBattle)
        {
            if (Current.Phase !=
                AnkaluteGameLoopPhase.ExpeditionExploration)
            {
                return Reject(AnkaluteGameLoopRequestError.InvalidPhase);
            }
            if (!intent.EncounterRequest.IsValid)
                return Reject(AnkaluteGameLoopRequestError.InvalidPayload);
            return Commit(
                AnkaluteGameLoopPhase.Battle,
                true,
                Current.ExpeditionId,
                intent);
        }

        if (intent.Kind == AnkaluteGameLoopIntentKind.ResolveBattle)
        {
            if (Current.Phase != AnkaluteGameLoopPhase.Battle)
                return Reject(AnkaluteGameLoopRequestError.InvalidPhase);
            if (intent.Resolution == BattleResolution.Victory)
            {
                return Commit(
                    AnkaluteGameLoopPhase.BattleSettlement,
                    true,
                    Current.ExpeditionId,
                    intent);
            }
            if (intent.Resolution == BattleResolution.Escaped ||
                intent.Resolution == BattleResolution.Aborted)
            {
                return Commit(
                    AnkaluteGameLoopPhase.ExpeditionExploration,
                    true,
                    Current.ExpeditionId,
                    intent);
            }
            if (intent.Resolution == BattleResolution.PartyDefeated)
            {
                StrategicHubRequestResult hubResult = strategicHub.Request(
                    StrategicHubIntent.ResetForEntry(
                        StrategicHubEntryReason.PartyDefeated));
                if (hubResult.Status !=
                        StrategicHubRequestStatus.Applied &&
                    hubResult.Status != StrategicHubRequestStatus.NoOp)
                {
                    return RejectHub(hubResult);
                }
                return Commit(
                    AnkaluteGameLoopPhase.StrategicHub,
                    false,
                    default,
                    intent);
            }
            return Reject(AnkaluteGameLoopRequestError.InvalidPayload);
        }

        if (intent.Kind ==
            AnkaluteGameLoopIntentKind.CompleteBattleSettlement)
        {
            if (Current.Phase != AnkaluteGameLoopPhase.BattleSettlement)
                return Reject(AnkaluteGameLoopRequestError.InvalidPhase);
            return Commit(
                AnkaluteGameLoopPhase.ExpeditionExploration,
                true,
                Current.ExpeditionId,
                intent);
        }

        if (intent.Kind == AnkaluteGameLoopIntentKind.EndExpedition)
        {
            if (Current.Phase !=
                AnkaluteGameLoopPhase.ExpeditionExploration)
            {
                return Reject(AnkaluteGameLoopRequestError.InvalidPhase);
            }
            StrategicHubEntryReason hubReason;
            switch (intent.ExitReason)
            {
                case ExpeditionExitReason.Completed:
                    hubReason = StrategicHubEntryReason.ExpeditionCompleted;
                    break;
                case ExpeditionExitReason.Retreated:
                    hubReason = StrategicHubEntryReason.ExpeditionRetreated;
                    break;
                default:
                    return Reject(AnkaluteGameLoopRequestError.InvalidPayload);
            }
            StrategicHubRequestResult hubResult = strategicHub.Request(
                StrategicHubIntent.ResetForEntry(hubReason));
            if (hubResult.Status != StrategicHubRequestStatus.Applied &&
                hubResult.Status != StrategicHubRequestStatus.NoOp)
            {
                return RejectHub(hubResult);
            }
            return Commit(
                AnkaluteGameLoopPhase.StrategicHub,
                false,
                default,
                intent);
        }

        if (intent.Kind == AnkaluteGameLoopIntentKind.ReturnToTitle)
        {
            if (Current.Phase == AnkaluteGameLoopPhase.Title)
                return NoOp();
            if (Current.Phase == AnkaluteGameLoopPhase.StrategicHub)
            {
                return Commit(
                    AnkaluteGameLoopPhase.Title,
                    false,
                    default,
                    intent);
            }
            return Reject(AnkaluteGameLoopRequestError.InvalidPhase);
        }

        return Reject(AnkaluteGameLoopRequestError.InvalidIntent);
    }

    private AnkaluteGameLoopRequestResult Commit(
        AnkaluteGameLoopPhase phase,
        bool hasExpedition,
        ExpeditionId expeditionId,
        AnkaluteGameLoopIntent intent)
    {
        AnkaluteGameLoopSnapshot before = Current;
        Current = new AnkaluteGameLoopSnapshot(
            phase,
            hasExpedition,
            expeditionId,
            false,
            before.Revision + 1L);
        transitioned?.Invoke(
            new AnkaluteGameLoopTransition(before, Current, intent));
        return new AnkaluteGameLoopRequestResult(
            AnkaluteGameLoopRequestStatus.Applied,
            AnkaluteGameLoopRequestError.None,
            Current);
    }

    private AnkaluteGameLoopRequestResult Reject(
        AnkaluteGameLoopRequestError error)
    {
        return new AnkaluteGameLoopRequestResult(
            AnkaluteGameLoopRequestStatus.Rejected,
            error,
            Current);
    }

    private AnkaluteGameLoopRequestResult RejectHub(
        StrategicHubRequestResult hubResult)
    {
        return Reject(
            hubResult.Error == StrategicHubRequestError.Busy
                ? AnkaluteGameLoopRequestError.Busy
                : AnkaluteGameLoopRequestError.StrategicHubRejected);
    }

    private AnkaluteGameLoopRequestResult NoOp()
    {
        return new AnkaluteGameLoopRequestResult(
            AnkaluteGameLoopRequestStatus.NoOp,
            AnkaluteGameLoopRequestError.None,
            Current);
    }
}
