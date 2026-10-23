using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct DRPGFormationLayoutItem
{
    public DRPGFormationLayoutItem(int runtimeId, float visualWidth)
    {
        if (runtimeId <= 0)
            throw new ArgumentOutOfRangeException(nameof(runtimeId));
        RuntimeId = runtimeId;
        VisualWidth = Mathf.Max(0.25f, visualWidth);
    }

    public int RuntimeId { get; }
    public float VisualWidth { get; }
}

public readonly struct DRPGFormationLayoutPlacement
{
    public DRPGFormationLayoutPlacement(
        int runtimeId,
        float centerX,
        float width)
    {
        RuntimeId = runtimeId;
        CenterX = centerX;
        Width = width;
    }

    public int RuntimeId { get; }
    public float CenterX { get; }
    public float Width { get; }
}

public static class DRPGFormationLayoutCalculator
{
    public static IReadOnlyList<DRPGFormationLayoutPlacement> Calculate(
        IReadOnlyList<DRPGFormationLayoutItem> items,
        float availableWidth,
        float baseWidth,
        float spacing)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));
        if (items.Count == 0)
            return Array.Empty<DRPGFormationLayoutPlacement>();

        float safeAvailable = Mathf.Max(1f, availableWidth);
        float safeBaseWidth = Mathf.Max(1f, baseWidth);
        float safeSpacing = Mathf.Max(0f, spacing);
        var widths = new float[items.Count];
        float contentWidth = safeSpacing * Mathf.Max(0, items.Count - 1);
        for (int i = 0; i < items.Count; i++)
        {
            widths[i] = safeBaseWidth * Mathf.Max(0.25f, items[i].VisualWidth);
            contentWidth += widths[i];
        }

        float scale = contentWidth > safeAvailable
            ? safeAvailable / contentWidth
            : 1f;
        float resolvedSpacing = safeSpacing * scale;
        float resolvedWidth = contentWidth * scale;
        float cursor = -resolvedWidth * 0.5f;
        var result = new DRPGFormationLayoutPlacement[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            float width = widths[i] * scale;
            float center = cursor + width * 0.5f;
            result[i] = new DRPGFormationLayoutPlacement(
                items[i].RuntimeId,
                center,
                width);
            cursor += width + resolvedSpacing;
        }

        return Array.AsReadOnly(result);
    }
}
