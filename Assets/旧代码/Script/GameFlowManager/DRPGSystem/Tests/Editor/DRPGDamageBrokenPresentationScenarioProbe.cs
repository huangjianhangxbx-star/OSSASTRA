#if UNITY_EDITOR
using System;
using UnityEditor;

public static class DRPGDamageBrokenPresentationScenarioProbe
{
    private const string PartyPath =
        "Assets/GameResources/DRPG/Config/Test/DRPGParty_DebugBreak.asset";
    private const string EncounterPath =
        "Assets/GameResources/DRPG/Config/Test/DRPGEncounter_DebugBreak.asset";

    public static string Run()
    {
        DRPGPartySetupSO party =
            AssetDatabase.LoadAssetAtPath<DRPGPartySetupSO>(PartyPath);
        DRPGEncounterDefinitionSO encounter =
            AssetDatabase.LoadAssetAtPath<DRPGEncounterDefinitionSO>(EncounterPath);

        AssertTrue(party != null,
            "Debug break party asset is missing: " + PartyPath);
        AssertTrue(encounter != null,
            "Debug break encounter asset is missing: " + EncounterPath);
        AssertTrue(party.Protagonist != null,
            "Debug break party must contain the required deployed protagonist.");
        AssertTrue(party.PartyMembers != null && party.PartyMembers.Count == 2,
            "Debug break party must contain two ordinary teammates.");
        AssertTrue(encounter.Enemies != null && encounter.Enemies.Count == 2,
            "Debug break encounter must contain two enemies.");

        for (int i = 0; i < encounter.Enemies.Count; i++)
        {
            DRPGCombatantProfileSO enemy = encounter.Enemies[i];
            AssertTrue(enemy != null,
                "Debug break encounter contains a null enemy profile at index " + i);
            AssertTrue(enemy.MaxHp >= 500f,
                "Debug break enemies must survive long enough to show the break cut-in.");
            AssertTrue(enemy.Skills != null && enemy.Skills.Count > 0 &&
                enemy.Skills[0] != null,
                "Debug break enemy " + i + " has no attack skill.");
            AssertTrue(enemy.Skills[0].Power >= 45f,
                "Debug break enemy attack must be strong enough to cross the ally break threshold.");
        }

        return "PASS DRPG break presentation scenario";
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

}
#endif
