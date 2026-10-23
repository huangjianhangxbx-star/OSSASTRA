#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class DRPGFreeStrikeSpecProbe
{
    public static string Run()
    {
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        try
        {
            var serialized = new SerializedObject(skill);
            serialized.FindProperty("skillId").stringValue = "probe_spec";
            serialized.FindProperty("power").floatValue = 17f;
            serialized.FindProperty("freeStrikeContactPolicy").intValue =
                (int)DRPGFreeStrikeContactPolicy.LimitedContacts;
            serialized.FindProperty("freeStrikeMaxPrimaryContacts").intValue = 0;
            serialized.FindProperty("freeStrikeSecondaryImpactKind").intValue =
                (int)DRPGSecondaryImpactKind.RadialSplash;
            serialized.FindProperty("freeStrikeSecondaryRadius").floatValue = -2f;
            serialized.FindProperty("freeStrikeSecondaryDamageMultiplier").floatValue = 0.35f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            DRPGResolvedFreeStrikeSpec result =
                DRPGFreeStrikeSpecResolver.Resolve(skill);
            Require(result.BasePower == 17f, "Resolved base power changed");
            Require(result.ContactPolicy ==
                DRPGFreeStrikeContactPolicy.LimitedContacts,
                "Contact policy was not preserved");
            Require(result.MaxPrimaryContacts == 1,
                "Limited contacts must clamp to one");
            Require(result.SecondaryImpactKind ==
                DRPGSecondaryImpactKind.RadialSplash,
                "Secondary impact kind was lost");
            Require(result.SecondaryRadius == 0f,
                "Negative secondary radius must clamp to zero");
            Require(Math.Abs(result.SecondaryDamageMultiplier - 0.35f) < 0.0001f,
                "Secondary multiplier changed");
            Require(typeof(DRPGResolvedFreeStrikeSpec).GetProperty(
                    "PartDurabilityDamage") == null,
                "Body-only free strikes must not retain part durability semantics");
            Require(typeof(DRPGResolvedFreeStrikeSpec).GetProperty(
                    "WeakPointDamageMultiplier") == null,
                "Body-only free strikes must not retain spatial weak-point semantics");
            return "PASS resolved free-strike specification";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(skill);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
