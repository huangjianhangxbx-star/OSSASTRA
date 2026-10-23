using System;
using System.Collections.Generic;
using UnityEngine;

public static class DRPGWorldFormationLayoutCalculator
{
    public static IReadOnlyList<DRPGWorldFormationPlacement> Calculate(
        IReadOnlyList<DRPGWorldFormationItem> items,
        Vector3 frontRowOrigin,
        float availableWidth = 8f,
        float baseWidth = 1f,
        float spacing = 0.3f,
        float rowDepthStep = 1.8f,
        float rowHorizontalStep = 0.42f,
        float middleScale = 0.94f,
        float rearScale = 0.88f)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));
        if (items.Count == 0)
            return Array.Empty<DRPGWorldFormationPlacement>();

        // Logical rows remain combat data, but a DD1-style side-on stage owns
        // one horizontal visual sequence per side.  Laying out each row around
        // the same origin made different rows reuse almost identical screen
        // positions and visually collapse six actors into a smaller cluster.
        var orderedItems = new List<DRPGWorldFormationItem>(items.Count);
        for (int i = 0; i < items.Count; i++)
            orderedItems.Add(items[i]);
        orderedItems.Sort(CompareForSideOnSequence);

        var horizontalItems = new DRPGFormationLayoutItem[orderedItems.Count];
        for (int i = 0; i < orderedItems.Count; i++)
        {
            horizontalItems[i] = new DRPGFormationLayoutItem(
                orderedItems[i].RuntimeId,
                orderedItems[i].VisualWidth);
        }

        IReadOnlyList<DRPGFormationLayoutPlacement> horizontal =
            DRPGFormationLayoutCalculator.Calculate(
                horizontalItems,
                availableWidth,
                baseWidth,
                spacing);
        var result = new List<DRPGWorldFormationPlacement>(items.Count);
        for (int i = 0; i < horizontal.Count; i++)
        {
            DRPGWorldFormationItem item = orderedItems[i];
            DRPGFormationRow row = item.Row;
            int rowIndex = Mathf.Clamp((int)row, 0, 2);
            DRPGFormationLayoutPlacement placement = horizontal[i];
            float scale = ResolveScale(row, middleScale, rearScale);
            int sortingBase = 300 - rowIndex * 100;
            result.Add(new DRPGWorldFormationPlacement(
                placement.RuntimeId,
                row,
                frontRowOrigin + new Vector3(
                    placement.CenterX,
                    0f,
                    rowIndex * Mathf.Max(0f, rowDepthStep)),
                scale,
                placement.Width,
                sortingBase + i));
        }

        // Kept in the signature for serialized callers and source
        // compatibility. Horizontal row staggering is intentionally retired:
        // depth and scale communicate the logical row without stealing a slot.
        _ = rowHorizontalStep;

        return result.AsReadOnly();
    }

    private static int CompareForSideOnSequence(
        DRPGWorldFormationItem left,
        DRPGWorldFormationItem right)
    {
        int row = left.Row.CompareTo(right.Row);
        if (row != 0)
            return row;
        int slot = left.Slot.CompareTo(right.Slot);
        return slot != 0 ? slot : left.RuntimeId.CompareTo(right.RuntimeId);
    }

    private static float ResolveScale(
        DRPGFormationRow row,
        float middleScale,
        float rearScale)
    {
        if (row == DRPGFormationRow.Front)
            return 1f;
        if (row == DRPGFormationRow.Middle)
            return Mathf.Clamp(middleScale, 0.1f, 1f);
        return Mathf.Clamp(rearScale, 0.1f, 1f);
    }
}
