using System;

/// <summary>
/// Pure application coordinator that keeps Strategic Hub navigation and the
/// top-level ANKALUTE phase state on the same pair of domain instances.
/// Unity adapters may observe it, but it owns no Unity object or presentation.
/// </summary>
public sealed class AnkaluteGameLoopRuntimeCoordinator
{
    private readonly StrategicHubFlow strategicHub;
    private readonly AnkaluteGameLoopState gameLoop;

    public AnkaluteGameLoopSnapshot Current => gameLoop.Current;
    public StrategicHubSnapshot HubCurrent => strategicHub.Current;

    public AnkaluteGameLoopRuntimeCoordinator()
    {
        strategicHub = new StrategicHubFlow();
        gameLoop = new AnkaluteGameLoopState(strategicHub);
    }

    public AnkaluteGameLoopRequestResult EnterStrategicHub(
        SessionEntryReason reason)
    {
        return gameLoop.Request(
            AnkaluteGameLoopIntent.EnterStrategicHub(reason));
    }

    public StrategicHubRequestResult OpenDestination(
        StrategicHubRoute route)
    {
        return strategicHub.Request(
            StrategicHubIntent.OpenDestination(route));
    }

    public StrategicHubRequestResult Back()
    {
        return strategicHub.Request(StrategicHubIntent.Back());
    }

    public bool TryLaunchExpedition(
        ExpeditionStartRequest request,
        out StrategicHubRequestResult confirmation,
        out AnkaluteGameLoopRequestResult transition)
    {
        confirmation = strategicHub.Request(
            StrategicHubIntent.ConfirmExpedition(request));
        transition = default;
        if (confirmation.Status != StrategicHubRequestStatus.Applied ||
            !confirmation.HasExpeditionRequest)
        {
            return false;
        }

        transition = gameLoop.Request(
            AnkaluteGameLoopIntent.LaunchExpedition(
                confirmation.ExpeditionRequest));
        return transition.Status == AnkaluteGameLoopRequestStatus.Applied;
    }

    public bool TryLaunchExpeditionWithActivation(
        ExpeditionStartRequest request,
        Func<bool> tryActivateHost,
        out StrategicHubRequestResult confirmation,
        out AnkaluteGameLoopRequestResult transition)
    {
        if (!TryLaunchExpedition(request, out confirmation, out transition))
            return false;

        bool activated;
        try
        {
            activated = tryActivateHost != null && tryActivateHost();
        }
        catch
        {
            gameLoop.Request(AnkaluteGameLoopIntent.EndExpedition(
                ExpeditionExitReason.Retreated));
            return false;
        }

        if (activated)
            return true;

        gameLoop.Request(AnkaluteGameLoopIntent.EndExpedition(
            ExpeditionExitReason.Retreated));
        return false;
    }

    public AnkaluteGameLoopRequestResult BeginBattle(
        EncounterStartRequest request)
    {
        return gameLoop.Request(
            AnkaluteGameLoopIntent.BeginBattle(request));
    }

    public AnkaluteGameLoopRequestResult ResolveBattle(
        BattleResolution resolution)
    {
        return gameLoop.Request(
            AnkaluteGameLoopIntent.ResolveBattle(resolution));
    }

    public AnkaluteGameLoopRequestResult CompleteBattleSettlement()
    {
        return gameLoop.Request(
            AnkaluteGameLoopIntent.CompleteBattleSettlement());
    }

    public AnkaluteGameLoopRequestResult EndExpedition(
        ExpeditionExitReason reason)
    {
        return gameLoop.Request(
            AnkaluteGameLoopIntent.EndExpedition(reason));
    }

    public AnkaluteGameLoopRequestResult ReturnToTitle()
    {
        return gameLoop.Request(AnkaluteGameLoopIntent.ReturnToTitle());
    }
}
