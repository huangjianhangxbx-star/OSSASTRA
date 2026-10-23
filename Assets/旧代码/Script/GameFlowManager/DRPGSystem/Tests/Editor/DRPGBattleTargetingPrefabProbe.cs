#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class DRPGBattleTargetingPrefabProbe
{
    private const string PrefabPath =
        "Assets/GameResources/DRPG/Prefabs/PF_DRPGBattlePresentation.prefab";
    private const string PresentationPath =
        "Assets/GameResources/DRPG/Config/Test/DRPGBattlePresentation_Test.asset";

    public static string Run()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        AssertTrue(prefab != null, "The DRPG battle presentation prefab is missing");

        DRPGBattlePresentationRoot presentation =
            prefab.GetComponent<DRPGBattlePresentationRoot>();
        AssertTrue(presentation != null,
            "The DRPG presentation root component is missing");

        DRPGBattleUGUISurface surface =
            prefab.GetComponent<DRPGBattleUGUISurface>();
        AssertTrue(surface != null,
            "The DRPG presentation surface component is missing");
        var surfaceSo = new SerializedObject(surface);
        BattlefieldEnemyRosterView roster = surfaceSo
            .FindProperty("enemyRoster").objectReferenceValue as
            BattlefieldEnemyRosterView;
        AssertTrue(roster != null,
            "The presentation prefab must serialize a central enemy roster");
        AssertTrue(roster.Views.Count >= 2,
            "The central enemy roster must provide at least two target slots");
        AssertTrue(surfaceSo.FindProperty("targetPrompt")
            .objectReferenceValue != null,
            "The presentation prefab must serialize a target-selection prompt");
        DRPGBattleAudioCueSink audioSink = surfaceSo
            .FindProperty("audioCueSinkComponent").objectReferenceValue as
            DRPGBattleAudioCueSink;
        AssertTrue(audioSink != null,
            "The presentation prefab must serialize a concrete DRPG audio sink");
        AssertTrue(audioSink.gameObject == prefab,
            "The DRPG audio sink must live on the battle presentation root");

        DRPGBattlePresentationSO presentationAsset =
            AssetDatabase.LoadAssetAtPath<DRPGBattlePresentationSO>(
                PresentationPath);
        AssertTrue(presentationAsset != null,
            "The test battle presentation asset is missing");
        var presentationAssetSo = new SerializedObject(presentationAsset);
        AssertTrue(presentationAssetSo.FindProperty("audioPalette")
            .objectReferenceValue is DRPGBattleAudioPaletteSO,
            "The test battle presentation must serialize an audio palette");

        BattlefieldEnemyView firstView = roster.Views[0];
        AssertTrue(firstView != null, "The first enemy target view is missing");
        var viewSo = new SerializedObject(firstView);
        AssertTrue(viewSo.FindProperty("targetButton").objectReferenceValue != null,
            "Enemy target views must expose a UGUI button");
        AssertTrue(viewSo.FindProperty("targetOutline").objectReferenceValue != null,
            "Enemy target views must expose visible target feedback");
        AssertTrue(viewSo.FindProperty("warningRoot").objectReferenceValue != null,
            "Enemy target views must serialize a warning badge root");
        AssertTrue(viewSo.FindProperty("warningBackground").objectReferenceValue != null,
            "Enemy target views must serialize a warning badge background");
        AssertTrue(viewSo.FindProperty("warningLabel").objectReferenceValue != null,
            "Enemy target views must serialize a warning value label");
        AssertTrue(viewSo.FindProperty("warningPulseTarget").objectReferenceValue != null,
            "Enemy target views must serialize an isolated warning pulse target");

        return "PASS DRPG targeting prefab";
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }
}
#endif
