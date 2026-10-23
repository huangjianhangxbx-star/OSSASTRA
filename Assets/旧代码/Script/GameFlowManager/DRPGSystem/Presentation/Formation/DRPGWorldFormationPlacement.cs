using System;
using UnityEngine;

public readonly struct DRPGWorldFormationItem
{
    public DRPGWorldFormationItem(
        int runtimeId,
        DRPGFormationRow row,
        int slot,
        float visualWidth)
    {
        if (runtimeId <= 0)
            throw new ArgumentOutOfRangeException(nameof(runtimeId));
        RuntimeId = runtimeId;
        Row = row;
        Slot = Mathf.Max(0, slot);
        VisualWidth = Mathf.Max(0.25f, visualWidth);
    }

    public int RuntimeId { get; }
    public DRPGFormationRow Row { get; }
    public int Slot { get; }
    public float VisualWidth { get; }
}

public readonly struct DRPGWorldFormationPlacement
{
    public DRPGWorldFormationPlacement(
        int runtimeId,
        DRPGFormationRow row,
        Vector3 worldPosition,
        float uniformScale,
        float resolvedWidth,
        int sortingOrder)
    {
        RuntimeId = runtimeId;
        Row = row;
        WorldPosition = worldPosition;
        UniformScale = uniformScale;
        ResolvedWidth = resolvedWidth;
        SortingOrder = sortingOrder;
    }

    public int RuntimeId { get; }
    public DRPGFormationRow Row { get; }
    public Vector3 WorldPosition { get; }
    public float UniformScale { get; }
    public float ResolvedWidth { get; }
    public int SortingOrder { get; }
}
