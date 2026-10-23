#if UNITY_EDITOR
using System;
using UnityEngine;

public static class AimTrackingPolicyProbe
{
    public static string Run()
    {
        AssertTrue(
            AimIKController.ShouldWriteProgrammaticTarget(AimTrackingMode.FollowCursor),
            "FollowCursor must write the programmatic IK target");
        AssertTrue(
            AimIKController.ShouldWriteProgrammaticTarget(AimTrackingMode.Hold),
            "Hold must keep writing a character-relative IK target");
        AssertFalse(
            AimIKController.ShouldWriteProgrammaticTarget(AimTrackingMode.AnimationDriven),
            "AnimationDriven must not overwrite animation IK");

        Vector2 held = AimIKController.CalculateHeldAimWorldPosition(
            new Vector2(10f, 20f),
            Vector2.right,
            3f);
        if ((held - new Vector2(13f, 20f)).sqrMagnitude > 0.0001f)
            throw new Exception("Hold target did not preserve its character-relative direction");

        return "PASS aim tracking policy";
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    private static void AssertFalse(bool value, string message)
    {
        AssertTrue(!value, message);
    }
}
#endif
