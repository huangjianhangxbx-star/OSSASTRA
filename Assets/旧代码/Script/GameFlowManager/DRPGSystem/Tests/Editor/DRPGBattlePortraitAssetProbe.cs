#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class DRPGBattlePortraitAssetProbe
{
    private const string ConfigRoot = "Assets/GameResources/DRPG/Config/Test/";
    private const string GalRoot = "Assets/GameResources/DRPG/GalActors/";

    private static readonly string[] AllyIds =
    {
        "ankalute_protagonist",
        "ankalute_party_vanguard",
        "ankalute_party_alchemist"
    };

    public static string Run()
    {
        for (int i = 0; i < AllyIds.Length; i++)
        {
            string id = AllyIds[i];
            DRPGCombatantProfileSO profile = AssetDatabase.LoadAssetAtPath<DRPGCombatantProfileSO>(
                ConfigRoot + "DRPGCombatant_" + id + ".asset");
            AssertNotNull(profile, id + " profile");
            AssertNotNull(profile.FullPortrait, id + " normal portrait");
            AssertNotNull(profile.HitPortrait, id + " hit portrait");
            AssertNotNull(profile.DamageBrokenPortrait, id + " broken portrait");
            AssertDifferent(profile.FullPortrait, profile.HitPortrait,
                id + " hit portrait must be a dedicated asset");
            AssertDifferent(profile.FullPortrait, profile.DamageBrokenPortrait,
                id + " broken portrait must be a dedicated asset");

            GalActorSO actor = AssetDatabase.LoadAssetAtPath<GalActorSO>(
                GalRoot + "GalActor_" + id + ".asset");
            AssertNotNull(actor, id + " GalActor");
            AssertSame(profile.FullPortrait, actor.DefaultPortrait,
                id + " battle and Gal default portrait must share one sprite");
            AssertSame(profile.HitPortrait, actor.ResolvePortrait("hit"),
                id + " Gal hit variant must share the battle hit sprite");
            AssertSame(profile.DamageBrokenPortrait, actor.ResolvePortrait("broken"),
                id + " Gal broken variant must share the battle broken sprite");
        }

        return "PASS DRPG allied normal/hit/broken portrait assets";
    }

    private static void AssertNotNull(UnityEngine.Object value, string message)
    {
        if (value == null)
            throw new Exception(message + " is missing");
    }

    private static void AssertDifferent(UnityEngine.Object first, UnityEngine.Object second, string message)
    {
        if (first == second)
            throw new Exception(message);
    }

    private static void AssertSame(UnityEngine.Object expected, UnityEngine.Object actual, string message)
    {
        if (expected != actual)
            throw new Exception(message);
    }
}
#endif
