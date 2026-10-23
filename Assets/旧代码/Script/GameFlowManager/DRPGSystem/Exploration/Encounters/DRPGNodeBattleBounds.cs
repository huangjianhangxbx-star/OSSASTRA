using System;
using UnityEngine;

/// <summary>Authoritative horizontal safe region for an in-place battle.</summary>
[Serializable]
public sealed class DRPGNodeBattleBounds
{
    [SerializeField] private float minimumX = -100f;
    [SerializeField] private float maximumX = 100f;

    public DRPGNodeBattleBounds()
    {
    }

    public DRPGNodeBattleBounds(float minX, float maxX)
    {
        Configure(minX, maxX);
    }

    public float MinimumX => minimumX;
    public float MaximumX => maximumX;
    public bool IsConfigured => float.IsFinite(minimumX) &&
        float.IsFinite(maximumX) && minimumX < maximumX;

    public void Configure(float minX, float maxX)
    {
        if (!float.IsFinite(minX) || !float.IsFinite(maxX) || minX >= maxX)
            throw new ArgumentOutOfRangeException(nameof(minX));
        minimumX = minX;
        maximumX = maxX;
    }

    public bool ContainsBattle(
        float partyX,
        float leftExtent,
        float rightExtent)
    {
        if (!IsConfigured || !float.IsFinite(partyX) ||
            !float.IsFinite(leftExtent) || !float.IsFinite(rightExtent) ||
            leftExtent < 0f || rightExtent < 0f)
        {
            return false;
        }
        return partyX - leftExtent >= minimumX &&
            partyX + rightExtent <= maximumX;
    }
}
