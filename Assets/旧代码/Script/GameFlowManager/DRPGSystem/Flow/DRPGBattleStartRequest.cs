public sealed class DRPGBattleStartRequest : FlowRequest
{
    public DRPGBattleStartRequest(
        DRPGPartySetupSO party,
        DRPGEncounterDefinitionSO encounter,
        DRPGBattleEnvironmentContext environmentContext,
        int battleSeed,
        BasicAttackOnlyAcceptanceOptions acceptanceOptions = default)
        : this(party, encounter, environmentContext, battleSeed,
            acceptanceOptions, null, null)
    {
    }

    public DRPGBattleStartRequest(
        DRPGPartySetupSO party,
        DRPGEncounterDefinitionSO encounter,
        DRPGBattleEnvironmentContext environmentContext,
        int battleSeed,
        BasicAttackOnlyAcceptanceOptions acceptanceOptions,
        DRPGPartyRosterSnapshot explicitRosterSnapshot,
        DRPGNodeBattleContext nodeContext)
        : base()
    {
        flowType = GameFlowStateType.BattleStart;
        Party = party;
        if (explicitRosterSnapshot != null)
        {
            RosterSnapshot = explicitRosterSnapshot;
            HasExplicitRosterSnapshot = true;
        }
        else if (party != null)
        {
            try
            {
                RosterSnapshot = DRPGPartyCompositionState
                    .FromSetup(party)
                    .CreateSnapshot();
            }
            catch (System.ArgumentException exception)
            {
                RosterSnapshotError = exception.Message;
            }
        }
        Encounter = encounter;
        EnvironmentContext = environmentContext;
        BattleSeed = battleSeed;
        AcceptanceOptions = acceptanceOptions;
        NodeContext = nodeContext;
        Data = this;
    }

    public DRPGPartySetupSO Party { get; }
    public DRPGPartyRosterSnapshot RosterSnapshot { get; }
    public string RosterSnapshotError { get; } = string.Empty;
    public DRPGEncounterDefinitionSO Encounter { get; }
    public DRPGBattleEnvironmentContext EnvironmentContext { get; }
    public int BattleSeed { get; }
    public BasicAttackOnlyAcceptanceOptions AcceptanceOptions { get; }
    public bool HasExplicitRosterSnapshot { get; }
    public DRPGNodeBattleContext NodeContext { get; }
}

public enum DRPGBattleStartResult
{
    Started = 0,
    Busy = 1,
    InvalidConfiguration = 2,
    FlowUnavailable = 3,
    SuspensionUnavailable = 4,
    PresentationFailed = 5
}
