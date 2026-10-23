#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class DRPGInPlaceBattleProbeRunner
{
    [MenuItem("Tools/ANKALUTE/DRPG/In Place Battle/Run Core Probes")]
    public static void RunFromMenu()
    {
        Debug.Log(DRPGNodePartyFormationProbe.Run());
        Debug.Log(DRPGNodePartyPlayModeProbe.Run());
        Debug.Log(DRPGEncounterPointProbe.Run());
        Debug.Log(DRPGBattleSceneBindingProbe.Run());
        Debug.Log(DRPGInPlaceContinuityProbe.Run());
    }
}
#endif
