#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

internal static class DRPGProductionCompositionProbe
{
    internal static void EnabledPolicyCreatesFrozenWaveSessionAndRequiresBinding()
    {
        var party = AssetDatabase.LoadAssetAtPath<DRPGPartySetupSO>("Assets/GameResources/DRPG/Progression/Test/Resonance/DRPGParty_ResonanceTest.asset");
        var encounter = AssetDatabase.LoadAssetAtPath<DRPGEncounterDefinitionSO>("Assets/GameResources/DRPG/Config/Test/DRPGEncounter_Test2.asset");
        var config = ScriptableObject.CreateInstance<DRPGBattlePresentationSO>();
        try
        {
            var field = typeof(DRPGBattlePresentationSO).GetField("standardBasicAttackTransactionEnabled", BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(config, true);
            var frozen = new DRPGBattleTransactionPolicy(config.StandardBasicAttackTransactionEnabled);
            field.SetValue(config, false);
            var roster = DRPGBattleSessionFactory.CreateRoster(party, encounter);
            var session = DRPGBattleSessionFactory.CreateConfiguredSession(roster, new DRPGBattleStartRequest(party, encounter, default, 42), frozen);
            if (!session.StandardBasicAttackEnabled || session.Participants.Any(p => p.Side == DRPGBattleSide.Enemy) ||
                session.ReserveEnemies.Count == 0 || session.CurrentActor != null)
                throw new InvalidOperationException("Enabled frozen production policy did not create the undeployed wave session.");
            if (session.PrepareStart(new DRPGSettlementPresentationProbe.FixtureClock()) != BattleStartupPrepareStatus.PresentationUnavailable ||
                session.TransactionArbiter.HasOwner || session.CurrentActor != null)
                throw new InvalidOperationException("Unbound production startup acquired an owner or published an actor.");
        }
        finally { UnityEngine.Object.DestroyImmediate(config); }
    }

    internal static void DisabledPolicyPreservesLegacyComposition()
    {
        var party = AssetDatabase.LoadAssetAtPath<DRPGPartySetupSO>("Assets/GameResources/DRPG/Progression/Test/Resonance/DRPGParty_ResonanceTest.asset");
        var encounter = AssetDatabase.LoadAssetAtPath<DRPGEncounterDefinitionSO>("Assets/GameResources/DRPG/Config/Test/DRPGEncounter_Test2.asset");
        var session = DRPGBattleSessionFactory.CreateConfiguredSession(DRPGBattleSessionFactory.CreateRoster(party, encounter),
            new DRPGBattleStartRequest(party, encounter, default, 42), new DRPGBattleTransactionPolicy(false));
        if (session.StandardBasicAttackEnabled || !session.Participants.Any(p => p.Side == DRPGBattleSide.Enemy && p.IsDeployed))
            throw new InvalidOperationException("Disabled production policy changed the legacy roster.");
    }
}
#endif
