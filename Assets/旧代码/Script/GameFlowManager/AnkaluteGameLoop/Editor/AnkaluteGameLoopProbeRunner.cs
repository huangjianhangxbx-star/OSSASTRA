#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class AnkaluteGameLoopProbeRunner
{
    [MenuItem("Tools/ANKALUTE/Game Loop/Run Foundation Probes")]
    public static void RunAll()
    {
        Debug.Log("[AnkaluteGameLoopProbes] " +
            AnkaluteGameLoopRuntimeCoordinatorProbe.RunInitialHubEntry());
        Debug.Log("[AnkaluteGameLoopProbes] " +
            AnkaluteGameLoopRuntimeCoordinatorProbe.RunTypedExpeditionLaunch());
        Debug.Log("[AnkaluteGameLoopProbes] " +
            AnkaluteGameLoopRuntimeCoordinatorProbe.RunActivationFailureCompensation());
        Debug.Log("[AnkaluteGameLoopProbes] " +
            AnkaluteGameLoopRuntimeCoordinatorProbe.RunBattleAndTerminalMapping());
        Debug.Log("[AnkaluteGameLoopProbes] " +
            AnkaluteExplorationPhaseGateProbe.RunOwnership());
        Debug.Log("[AnkaluteGameLoopProbes] " +
            StrategicHubFlowProbe.RunRouteStack());
        Debug.Log("[AnkaluteGameLoopProbes] " +
            StrategicHubFlowProbe.RunActivityLifecycle());
        Debug.Log("[AnkaluteGameLoopProbes] " +
            StrategicHubFlowProbe.RunExpeditionConfirmationAndEventSafety());
        Debug.Log("[AnkaluteGameLoopProbes] " +
            AnkaluteGameLoopStateProbe.RunHappyPath());
        Debug.Log("[AnkaluteGameLoopProbes] " +
            AnkaluteGameLoopStateProbe.RunTerminalReturns());
        Debug.Log("[AnkaluteGameLoopProbes] " +
            AnkaluteGameLoopStateProbe.RunReturnToTitleAndValidation());
        Debug.Log("[AnkaluteGameLoopProbes] " +
            AnkaluteGameLoopStateProbe.RunEventRecovery());
        Debug.Log("[AnkaluteGameLoopProbes] " +
            AnkaluteGameLoopStateProbe.RunHubResetRejection());
    }
}
#endif
