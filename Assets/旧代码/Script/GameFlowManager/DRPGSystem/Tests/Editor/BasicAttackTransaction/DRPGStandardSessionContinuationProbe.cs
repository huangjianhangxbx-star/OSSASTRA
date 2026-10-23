#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;

internal static class DRPGStandardSessionContinuationProbe
{
    internal static void RealAssetSessionRequiresExplicitStandardComposition()
    {
        var party = AssetDatabase.LoadAssetAtPath<DRPGPartySetupSO>("Assets/GameResources/DRPG/Progression/Test/Resonance/DRPGParty_ResonanceTest.asset");
        var encounter = AssetDatabase.LoadAssetAtPath<DRPGEncounterDefinitionSO>("Assets/GameResources/DRPG/Config/Test/DRPGEncounter_Test2.asset");
        var roster = DRPGBattleSessionFactory.CreateRoster(party, encounter);
        if (!roster.IsSuccess) throw new InvalidOperationException(roster.ErrorMessage);
        var factory = typeof(DRPGBattleSessionFactory).GetMethod("CreateStandardSession", BindingFlags.Static | BindingFlags.NonPublic);
        if (factory == null) throw new InvalidOperationException("Actual roster has no explicit standard Session composition.");
        var session = (DRPGBattleSession)factory.Invoke(null, new object[] { roster });
        var method = typeof(DRPGBattleSession).GetMethod("TryStartStandardBasicAttack", BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null) throw new InvalidOperationException("Session cannot own a standard action and its continuation.");
        var result = (RootActionStartResult)method.Invoke(session, new object[] { new ParticipantId(roster.Participants[roster.Participants.Count - 1].RuntimeId) });
        if (result.IsAccepted || session.TransactionArbiter.HasOwner)
            throw new InvalidOperationException("Inactive/unbound Session started an action.");
    }
}
#endif
