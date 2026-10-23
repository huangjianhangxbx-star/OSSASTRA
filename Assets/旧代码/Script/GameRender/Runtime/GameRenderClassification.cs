using UnityEngine;

public static class GameRenderClassification
{
    // Sorting layer positions are authored in the project's renderer. This is a
    // conservative protection range, not a semantic character-name registry.
    public static bool IsProtectedSortingLayer(int sortingLayerId)
    {
        int value = SortingLayer.GetLayerValueFromID(sortingLayerId);
        return (value >= 20 && value <= 24) ||
               (value >= 32 && value <= 35) || value == 37;
    }
}
