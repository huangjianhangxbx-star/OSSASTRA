#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

public static class DRPGFreeStrikeBodyContactProbe
{
    public static string Run()
    {
        Type contactType = RequireType("DRPGFreeStrikeBodyContact");
        Type surfaceType = RequireType("DRPGFreeStrikeTargetSurface");
        ConstructorInfo surfaceConstructor = surfaceType.GetConstructor(
            new[] { typeof(int), typeof(Rect) });
        Require(surfaceConstructor != null,
            "A target surface must be defined only by runtime ID and body bounds.");

        PropertyInfo targetId = RequireProperty(contactType, "TargetRuntimeId");
        PropertyInfo hitPoint = RequireProperty(contactType, "NormalizedHitPoint");
        PropertyInfo contactOrder = RequireProperty(contactType, "ContactOrder");
        PropertyInfo role = RequireProperty(contactType, "Role");
        PropertyInfo hasSpatialMetadata = RequireProperty(
            contactType,
            "HasSpatialWeakPointMetadata");
        Require(contactType.GetProperty("TargetZoneId") == null,
            "Body contacts must not expose a target-zone ID.");
        Require(contactType.GetProperty("ZoneKind") == null,
            "Body contacts must not expose a target-zone kind.");
        Require(contactType.GetProperty("PartDurabilityDamage") == null,
            "Body contacts must not carry part-durability damage.");

        object near = surfaceConstructor.Invoke(new object[]
        {
            2,
            new Rect(0.10f, 0.30f, 0.20f, 0.40f)
        });
        object far = surfaceConstructor.Invoke(new object[]
        {
            3,
            new Rect(0.65f, 0.30f, 0.20f, 0.40f)
        });
        Array targets = Array.CreateInstance(surfaceType, 2);
        targets.SetValue(far, 0);
        targets.SetValue(near, 1);

        var line = new DRPGFreeStrikeSample(
            DRPGFreeStrikeShape.Line,
            new Vector2(0.48f, 0.5f),
            Vector2.right,
            0.82f,
            0.04f);
        Array first = Resolve(
            line,
            CreateSpec(DRPGFreeStrikeContactPolicy.FirstContact, 1),
            targets);
        Require(first.Length == 1,
            "FirstContact must retain exactly the nearest body contact.");
        object firstContact = first.GetValue(0);
        Require((int)targetId.GetValue(firstContact) == 2,
            "FirstContact must sort by trace distance, not input order.");
        Require((int)contactOrder.GetValue(firstContact) == 0,
            "The nearest accepted contact must have order zero.");
        Require(string.Equals(
                "Primary",
                role.GetValue(firstContact).ToString(),
                StringComparison.Ordinal),
            "The direct body contact must remain primary.");
        Require(!(bool)hasSpatialMetadata.GetValue(firstContact),
            "A body contact must never claim spatial weak-point metadata.");
        Require((Vector2)hitPoint.GetValue(firstContact) != default,
            "A body contact must retain its normalized hit point.");

        Array all = Resolve(
            line,
            CreateSpec(DRPGFreeStrikeContactPolicy.AllContacts, 1),
            targets);
        Require(all.Length == 2,
            "AllContacts must retain every intersected body.");
        Require((int)targetId.GetValue(all.GetValue(0)) == 2 &&
                (int)targetId.GetValue(all.GetValue(1)) == 3,
            "AllContacts must remain deterministically ordered.");
        Require((int)contactOrder.GetValue(all.GetValue(0)) == 0 &&
                (int)contactOrder.GetValue(all.GetValue(1)) == 1,
            "Accepted body contacts need deterministic order metadata.");

        Array limited = Resolve(
            line,
            CreateSpec(DRPGFreeStrikeContactPolicy.LimitedContacts, 1),
            targets);
        Require(limited.Length == 1 &&
                (int)targetId.GetValue(limited.GetValue(0)) == 2,
            "LimitedContacts must still clamp the ordered primary set.");

        Array pointTargets = Array.CreateInstance(surfaceType, 1);
        pointTargets.SetValue(surfaceConstructor.Invoke(new object[]
        {
            9,
            new Rect(0.35f, 0.25f, 0.30f, 0.50f)
        }), 0);
        Array pointHit = Resolve(
            new DRPGFreeStrikeSample(
                DRPGFreeStrikeShape.Point,
                new Vector2(0.5f, 0.5f),
                Vector2.right,
                0f,
                0f),
            CreateSpec(DRPGFreeStrikeContactPolicy.AllContacts, 1),
            pointTargets);
        Array pointMiss = Resolve(
            new DRPGFreeStrikeSample(
                DRPGFreeStrikeShape.Point,
                new Vector2(0.95f, 0.95f),
                Vector2.right,
                0f,
                0f),
            CreateSpec(DRPGFreeStrikeContactPolicy.AllContacts, 1),
            pointTargets);
        Require(pointHit.Length == 1 && pointMiss.Length == 0,
            "Point input must distinguish full-body contact from a miss.");

        return "PASS body-only free-strike contacts and policies";
    }

    private static Array Resolve(
        DRPGFreeStrikeSample sample,
        object spec,
        Array targets)
    {
        MethodInfo resolve = typeof(DRPGFreeStrikeContactResolver).GetMethod(
            "Resolve",
            BindingFlags.Public | BindingFlags.Static);
        Require(resolve != null, "Missing body-contact resolver entry point.");
        return (Array)resolve.Invoke(null, new[] { (object)sample, spec, targets });
    }

    private static object CreateSpec(
        DRPGFreeStrikeContactPolicy policy,
        int maxContacts)
    {
        return Activator.CreateInstance(
            typeof(DRPGResolvedFreeStrikeSpec),
            new object[]
            {
                DRPGFreeStrikeShape.Line,
                10f,
                1f,
                0.1f,
                0f,
                0.82f,
                0.04f,
                policy,
                maxContacts,
                DRPGSecondaryImpactKind.RadialSplash,
                0.2f,
                0.35f,
                "probe_body_contact"
            });
    }

    private static Type RequireType(string typeName)
    {
        Type type = Type.GetType(typeName + ", Assembly-CSharp");
        Require(type != null, "Missing required type " + typeName + ".");
        return type;
    }

    private static PropertyInfo RequireProperty(Type type, string name)
    {
        PropertyInfo property = type.GetProperty(
            name,
            BindingFlags.Public | BindingFlags.Instance);
        Require(property != null,
            "Missing required property " + type.Name + "." + name + ".");
        return property;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
