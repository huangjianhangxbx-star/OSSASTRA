using System;
using System.Collections.Generic;
using UnityEngine;

public static class BattleCameraFramingCalculator
{
    public static BattleCameraShotDefinition CreateFramedShot(
        BattleCameraShotDefinition baseShot,
        Vector3 authoredFocus,
        IReadOnlyList<Vector3> focusPoints,
        float aspect,
        float horizontalPadding,
        float verticalPadding,
        float desiredFieldOfView,
        float transitionSeconds)
    {
        if (baseShot == null)
            throw new ArgumentNullException(nameof(baseShot));

        float safeDesiredFov = Mathf.Clamp(
            desiredFieldOfView,
            BattleCameraShotDefinition.MinimumFieldOfView,
            BattleCameraShotDefinition.MaximumFieldOfView);
        if (focusPoints == null || focusPoints.Count == 0)
        {
            return new BattleCameraShotDefinition(
                baseShot.ShotId + ".fallback",
                baseShot.LocalPosition,
                baseShot.LocalEulerAngles,
                safeDesiredFov,
                transitionSeconds);
        }

        Vector3 minimum = focusPoints[0];
        Vector3 maximum = focusPoints[0];
        for (int i = 1; i < focusPoints.Count; i++)
        {
            minimum = Vector3.Min(minimum, focusPoints[i]);
            maximum = Vector3.Max(maximum, focusPoints[i]);
        }

        Vector3 center = (minimum + maximum) * 0.5f;
        Vector3 framedPosition = baseShot.LocalPosition;
        framedPosition.x += center.x - authoredFocus.x;
        framedPosition.y += center.y - authoredFocus.y;

        float safeAspect = Mathf.Max(0.1f, aspect);
        float halfWidth = Mathf.Max(0f, (maximum.x - minimum.x) * 0.5f) +
            Mathf.Max(0f, horizontalPadding);
        float halfHeight = Mathf.Max(0f, (maximum.y - minimum.y) * 0.5f) +
            Mathf.Max(0f, verticalPadding);
        float requiredVerticalHalfExtent = Mathf.Max(
            halfHeight,
            halfWidth / safeAspect);
        float authoredDepth = Mathf.Max(
            0.1f,
            Mathf.Abs(baseShot.LocalPosition.z - authoredFocus.z));
        float requiredFov = Mathf.Rad2Deg * 2f * Mathf.Atan(
            requiredVerticalHalfExtent / authoredDepth);
        float framedFov = Mathf.Clamp(
            Mathf.Max(safeDesiredFov, requiredFov),
            BattleCameraShotDefinition.MinimumFieldOfView,
            BattleCameraShotDefinition.MaximumFieldOfView);

        return new BattleCameraShotDefinition(
            baseShot.ShotId + ".framed",
            framedPosition,
            baseShot.LocalEulerAngles,
            framedFov,
            transitionSeconds);
    }
}
