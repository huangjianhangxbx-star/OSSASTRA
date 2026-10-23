using UnityEngine;

public sealed class DRPGBattleDebugLauncher : MonoBehaviour
{
    [SerializeField] private DRPGBattleFlowRoot battleFlow;
    [SerializeField] private DRPGPartySetupSO party;
    [SerializeField] private DRPGEncounterDefinitionSO encounter;
    [SerializeField] private KeyCode testKey = KeyCode.F8;
    [SerializeField] private DRPGPartySetupSO defeatTestParty;
    [SerializeField] private DRPGEncounterDefinitionSO defeatTestEncounter;
    [SerializeField] private KeyCode defeatTestKey = KeyCode.F9;
    [SerializeField] private DRPGNodeBattleCoordinator inPlaceCoordinator;

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKeyDown(testKey))
            StartBattleV2ForDevelopment();
        else if (Input.GetKeyDown(defeatTestKey))
            StartDefeatTestBattle();
#endif
    }

    public DRPGBattleStartResult StartTestBattle()
    {
        ResolveBattleFlow();

        return battleFlow != null
            ? battleFlow.RequestBattle(party, encounter)
            : DRPGBattleStartResult.FlowUnavailable;
    }

    public DRPGBattleStartResult StartBattleV2ForDevelopment()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ResolveInPlaceCoordinator();
        DRPGBattleStartResult result = inPlaceCoordinator != null
            ? inPlaceCoordinator.TryStart(encounter)
            : DRPGBattleStartResult.InvalidConfiguration;
        if (result != DRPGBattleStartResult.Started)
        {
            string error = inPlaceCoordinator != null
                ? inPlaceCoordinator.LastStartError
                : "F8 requires an authored in-place battle context.";
            Debug.LogError("[DRPG F8] " + error, this);
        }
        if (result == DRPGBattleStartResult.Started)
        {
            DRPGBattlePresentationRoot presentation =
                UnityEngine.Object.FindFirstObjectByType<DRPGBattlePresentationRoot>();
            presentation?.SetBattleV2PreviewEnabledForDevelopment(true);
        }
        return result;
#else
        return DRPGBattleStartResult.InvalidConfiguration;
#endif
    }

    public DRPGBattleStartResult StartBasicAttackAcceptanceForDevelopment()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ResolveBattleFlow();
        // The authored encounter must already enable and bind the Standard path.
        // Never mutate its flag, loadouts or the normal F8 request here.
        DRPGBattleStartResult result = battleFlow != null
            ? battleFlow.RequestBattle(party, encounter,
                environmentContext: default,
                acceptanceOptions: new BasicAttackOnlyAcceptanceOptions(
                    42,
                    "normal-hit"))
            : DRPGBattleStartResult.FlowUnavailable;
        if (result == DRPGBattleStartResult.Started)
        {
            DRPGBattlePresentationRoot presentation =
                UnityEngine.Object.FindFirstObjectByType<DRPGBattlePresentationRoot>();
            presentation?.SetBattleV2PreviewEnabledForDevelopment(true);
        }
        return result;
#else
        return DRPGBattleStartResult.InvalidConfiguration;
#endif
    }

    public bool VerifyInvalidAcceptanceBoundaryForDevelopment()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var request = new DRPGBattleStartRequest(party, encounter, default, 42,
            new BasicAttackOnlyAcceptanceOptions(43, "invalid-seed"));
        try
        {
            DRPGBattleSessionFactory.ValidateAcceptanceRequest(request,
                new DRPGBattleTransactionPolicy(true));
            return false;
        }
        catch (System.ArgumentException) { return true; }
#else
        return false;
#endif
    }

    public DRPGBattleStartResult StartDefeatTestBattle()
    {
        ResolveBattleFlow();

        return battleFlow != null
            ? battleFlow.RequestBattle(defeatTestParty, defeatTestEncounter)
            : DRPGBattleStartResult.FlowUnavailable;
    }

    private void ResolveBattleFlow()
    {
        if (battleFlow == null && GameFlowRoot.Instance != null)
            battleFlow = GameFlowRoot.Instance.drpgBattleFlow;
        if (battleFlow == null)
            battleFlow = UnityEngine.Object.FindFirstObjectByType<DRPGBattleFlowRoot>();
    }

    private void ResolveInPlaceCoordinator()
    {
        if (inPlaceCoordinator == null)
            inPlaceCoordinator = UnityEngine.Object.FindFirstObjectByType<DRPGNodeBattleCoordinator>();
    }
}
