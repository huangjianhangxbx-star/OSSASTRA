#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;

internal static class DRPGWaveSessionProbe
{
    internal static void InitialWaveHasNoActiveEnemyBeforePresentation()
    {
        var party = AssetDatabase.LoadAssetAtPath<DRPGPartySetupSO>("Assets/GameResources/DRPG/Progression/Test/Resonance/DRPGParty_ResonanceTest.asset");
        var encounter = AssetDatabase.LoadAssetAtPath<DRPGEncounterDefinitionSO>("Assets/GameResources/DRPG/Config/Test/DRPGEncounter_Test2.asset");
        var roster = DRPGBattleSessionFactory.CreateRoster(party, encounter);
        if (!roster.IsSuccess) throw new InvalidOperationException(roster.ErrorMessage);
        int count = roster.Participants.Count(x => x.Side == DRPGBattleSide.Enemy) + roster.ReserveEnemies.Count;
        var session = DRPGBattleSessionFactory.CreateWaveSession(roster, 42, "first-wave-test", new DRPGEnemyWavePolicySnapshot(2, 2, .75, .3, 6));
        if (session.Participants.Any(x => x.Side == DRPGBattleSide.Enemy) || session.ReserveEnemies.Count != count ||
            session.ReserveEnemies.Any(x => x.IsDeployed) || session.CurrentActor != null || session.Timeline.Count != 0)
            throw new InvalidOperationException("Wave Session exposed enemies or a candidate before its first presentation barrier.");
    }
}
#endif
