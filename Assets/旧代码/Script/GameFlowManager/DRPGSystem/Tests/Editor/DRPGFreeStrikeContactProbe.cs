#if UNITY_EDITOR
using System;
using UnityEngine;

public static class DRPGFreeStrikeContactProbe
{
    public static string Run()
    {
        VerifyDirectContactPolicies();
        VerifyRadialSplashRemainsASecondaryBodyImpact();
        return "PASS ordered body contacts, policies, and radial splash";
    }

    private static void VerifyDirectContactPolicies()
    {
        var target3 = new DRPGFreeStrikeTargetSurface(
            3,
            new Rect(0.65f, 0.30f, 0.20f, 0.40f));
        var target2 = new DRPGFreeStrikeTargetSurface(
            2,
            new Rect(0.10f, 0.30f, 0.20f, 0.40f));
        var reversedSurfaces = new[] { target3, target2 };
        var lineSample = new DRPGFreeStrikeSample(
            DRPGFreeStrikeShape.Line,
            new Vector2(0.48f, 0.5f),
            Vector2.right,
            0.82f,
            0.04f);

        DRPGFreeStrikeBodyContact[] first = DRPGFreeStrikeContactResolver.Resolve(
            lineSample,
            CreateSpec(
                DRPGFreeStrikeContactPolicy.FirstContact,
                1,
                DRPGSecondaryImpactKind.None,
                0f),
            reversedSurfaces);
        Require(first.Length == 1 && first[0].TargetRuntimeId == 2,
            "FirstContact did not choose the nearest enemy body");
        Require(first[0].Role == DRPGFreeStrikeImpactRole.Primary,
            "Direct body contact was mislabeled");

        DRPGFreeStrikeBodyContact[] all = DRPGFreeStrikeContactResolver.Resolve(
            lineSample,
            CreateSpec(
                DRPGFreeStrikeContactPolicy.AllContacts,
                1,
                DRPGSecondaryImpactKind.None,
                0f),
            reversedSurfaces);
        Require(all.Length == 2 && all[0].TargetRuntimeId == 2 &&
                all[1].TargetRuntimeId == 3,
            "AllContacts was not sorted by trace distance");

        DRPGFreeStrikeBodyContact[] limited =
            DRPGFreeStrikeContactResolver.Resolve(
                lineSample,
                CreateSpec(
                    DRPGFreeStrikeContactPolicy.LimitedContacts,
                    1,
                    DRPGSecondaryImpactKind.None,
                    0f),
                reversedSurfaces);
        Require(limited.Length == 1 && limited[0].TargetRuntimeId == 2,
            "LimitedContacts did not clamp the ordered primary set");
    }

    private static void VerifyRadialSplashRemainsASecondaryBodyImpact()
    {
        var direct = new DRPGFreeStrikeTargetSurface(
            10,
            new Rect(0.20f, 0.35f, 0.15f, 0.30f));
        var adjacent = new DRPGFreeStrikeTargetSurface(
            11,
            new Rect(0.42f, 0.35f, 0.13f, 0.30f));
        var distant = new DRPGFreeStrikeTargetSurface(
            12,
            new Rect(0.75f, 0.35f, 0.13f, 0.30f));
        var point = new DRPGFreeStrikeSample(
            DRPGFreeStrikeShape.Point,
            new Vector2(0.30f, 0.50f),
            Vector2.right,
            0f,
            0f);

        DRPGFreeStrikeBodyContact[] contacts =
            DRPGFreeStrikeContactResolver.Resolve(
                point,
                CreateSpec(
                    DRPGFreeStrikeContactPolicy.FirstContact,
                    1,
                    DRPGSecondaryImpactKind.RadialSplash,
                    0.15f),
                new[] { distant, adjacent, direct });

        Require(contacts.Length == 2,
            "Radial splash must append nearby bodies without hitting distant bodies");
        Require(contacts[0].TargetRuntimeId == 10 &&
                contacts[0].Role == DRPGFreeStrikeImpactRole.Primary,
            "The directly hit body must remain the sole primary contact");
        Require(contacts[1].TargetRuntimeId == 11 &&
                contacts[1].Role == DRPGFreeStrikeImpactRole.Secondary,
            "The nearby body must be emitted as a secondary impact");
        Require(contacts[1].ContactOrder == 1,
            "Secondary contacts must continue deterministic contact ordering");
    }

    private static DRPGResolvedFreeStrikeSpec CreateSpec(
        DRPGFreeStrikeContactPolicy policy,
        int maxContacts,
        DRPGSecondaryImpactKind secondaryKind,
        float secondaryRadius)
    {
        return new DRPGResolvedFreeStrikeSpec(
            DRPGFreeStrikeShape.Line,
            10f,
            1f,
            0.1f,
            0f,
            0.82f,
            0.04f,
            policy,
            maxContacts,
            secondaryKind,
            secondaryRadius,
            0.35f,
            "probe_contact");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
