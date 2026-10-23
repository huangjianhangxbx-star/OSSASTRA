#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

internal static class GameRenderClassificationProbe
{
    [MenuItem("Tools/ANKALUTE/GameRender/Run Classification Probe")]
    internal static void Run()
    {
        Check("Character1", true);
        Check("BattleCloseupParticipant", true);
        Check("BattleCloseupComposite", false);
        Check("DamageText", true);
        Check("Wall1", false);
        Check("Object2", false);
        Debug.Log("PASS GameRender classification probe (6/6)");
    }

    private static void Check(string sortingLayer, bool expected)
    {
        int id = SortingLayer.NameToID(sortingLayer);
        if (id == 0 && sortingLayer != "Default")
            throw new Exception("Missing test sorting layer " + sortingLayer);
        var actual = GameRenderClassification.IsProtectedSortingLayer(id);
        if (actual != expected)
            throw new Exception(sortingLayer + ": expected protected=" + expected + ", got " + actual);
    }
}
#endif
