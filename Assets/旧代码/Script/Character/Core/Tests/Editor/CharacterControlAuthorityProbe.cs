#if UNITY_EDITOR
using System;

public static class CharacterControlAuthorityProbe
{
    public static string Run()
    {
        var authority = new CharacterControlAuthority();
        object attackOwner = new object();
        object statusOwner = new object();

        authority.SetOverride(
            attackOwner,
            CharacterControlOverride.CommittedAttack(priority: 100));

        AssertFalse(
            authority.IsAllowed(CharacterControlChannel.Move),
            "Committed attacks must block ordinary movement");
        AssertEqual(
            CharacterFacingMode.Hold,
            authority.ResolveFacingMode(CharacterFacingMode.Cursor),
            "Committed attacks must hold facing");
        AssertEqual(
            AimTrackingMode.AnimationDriven,
            authority.ResolveAimTrackingMode(AimTrackingMode.FollowCursor),
            "Committed attacks must yield IK ownership to animation");

        authority.SetOverride(
            statusOwner,
            new CharacterControlOverride(
                CharacterControlChannel.WeaponSwitch,
                priority: 10));

        AssertFalse(
            authority.IsAllowed(CharacterControlChannel.WeaponSwitch),
            "Blocked channels from multiple owners must be aggregated");

        authority.ReleaseOverride(attackOwner);
        AssertTrue(
            authority.IsAllowed(CharacterControlChannel.Move),
            "Releasing the attack owner must restore movement");
        AssertFalse(
            authority.IsAllowed(CharacterControlChannel.WeaponSwitch),
            "Releasing one owner must preserve other owners");

        authority.ReleaseOverride(attackOwner);
        authority.Clear();
        AssertTrue(
            authority.IsAllowed(CharacterControlChannel.WeaponSwitch),
            "Release and Clear must be idempotent");

        return "PASS character control authority";
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

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
            throw new Exception($"{message}. Expected={expected}, Actual={actual}");
    }
}
#endif
