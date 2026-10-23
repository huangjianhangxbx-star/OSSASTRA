#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;

public static class DRPGWeaponUseLedgerProbe
{
    private sealed class MinimumRandom : IDRPGRandomSource
    {
        public float Range(float minimumInclusive, float maximumInclusive)
        {
            return minimumInclusive;
        }

        public int RangeInclusive(int minimumInclusive, int maximumInclusive)
        {
            return minimumInclusive;
        }
    }

    public static string Run()
    {
        DRPGWeaponDefinitionSO ordinaryDefinition = CreateDefinition(
            "ledger_ordinary",
            DRPGWeaponOwnershipKind.General,
            null,
            100);
        DRPGWeaponDefinitionSO fragileDefinition = CreateDefinition(
            "ledger_fragile",
            DRPGWeaponOwnershipKind.General,
            null,
            2);
        DRPGWeaponDefinitionSO signatureDefinition = CreateDefinition(
            "ledger_signature",
            DRPGWeaponOwnershipKind.Signature,
            "ledger_owner",
            2);
        try
        {
            var random = new MinimumRandom();
            DRPGWeaponInstance ordinary = DRPGWeaponInstance.Create(
                "ledger_ordinary_instance",
                ordinaryDefinition,
                random);
            var ledger = new DRPGWeaponUseLedger();
            AssertTrue(ledger.TryRecordCommittedUse(
                "action_a",
                DRPGWeaponUseKind.FirearmShot).Success,
                "A firearm shot must enter the action ledger");
            AssertTrue(ledger.TryRecordCommittedUse(
                "action_a",
                DRPGWeaponUseKind.FreeStrikeInput).Success,
                "Multiple committed inputs in one action must accumulate");
            int before = ordinary.CurrentDurability;
            DRPGWeaponUseCompletionResult completed =
                ledger.CompleteAction("action_a", ordinary);
            AssertTrue(completed.Success, "Pending action must complete");
            AssertEqual(2, completed.AppliedUseUnits,
                "Shot and free-strike input each cost one unit");
            AssertEqual(before - 2, ordinary.CurrentDurability,
                "Accumulated units must apply exactly once");

            DRPGWeaponUseCompletionResult duplicate =
                ledger.CompleteAction("action_a", ordinary);
            AssertTrue(duplicate.AlreadyCompleted,
                "Repeated completion must be recognized");
            AssertEqual(before - 2, ordinary.CurrentDurability,
                "Repeated completion must never double-charge durability");

            AssertTrue(ledger.TryRecordCommittedUse(
                "action_override",
                DRPGWeaponUseKind.MeleeCommittedSwing,
                3).Success,
                "Skill override use units must be accepted");
            DRPGWeaponUseCompletionResult overridden =
                ledger.CompleteAction("action_override", ordinary);
            AssertEqual(3, overridden.AppliedUseUnits,
                "Override must replace the default one-unit cost");

            DRPGWeaponInstance fragile = DRPGWeaponInstance.Create(
                "ledger_fragile_instance",
                fragileDefinition,
                random);
            ledger.TryRecordCommittedUse(
                "action_break",
                DRPGWeaponUseKind.MeleeCommittedSwing,
                9);
            DRPGWeaponUseCompletionResult broken =
                ledger.CompleteAction("action_break", fragile);
            AssertEqual(0, fragile.CurrentDurability,
                "Durability cannot become negative");
            AssertTrue(broken.ProducesWeaponFragments,
                "A newly broken non-signature weapon must request fragment output");
            AssertEqual(DRPGWeaponBreakDisposition.DestroyIntoFragments,
                broken.BreakDisposition,
                "Ordinary break disposition must destroy into fragments");

            DRPGWeaponInstance signature = DRPGWeaponInstance.Create(
                "ledger_signature_instance",
                signatureDefinition,
                random);
            ledger.TryRecordCommittedUse(
                "action_signature_break",
                DRPGWeaponUseKind.FirearmShot,
                9);
            DRPGWeaponUseCompletionResult signatureBroken =
                ledger.CompleteAction("action_signature_break", signature);
            AssertEqual(0, signature.CurrentDurability,
                "Signature durability must still reach zero");
            AssertFalse(signatureBroken.ProducesWeaponFragments,
                "Signature weapon must remain as a retained broken instance");
            AssertEqual(DRPGWeaponBreakDisposition.PreserveSignatureAtZero,
                signatureBroken.BreakDisposition,
                "Signature break disposition must preserve the instance");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(signatureDefinition);
            UnityEngine.Object.DestroyImmediate(fragileDefinition);
            UnityEngine.Object.DestroyImmediate(ordinaryDefinition);
        }

        return "PASS DRPG weapon use ledger";
    }

    private static DRPGWeaponDefinitionSO CreateDefinition(
        string weaponId,
        DRPGWeaponOwnershipKind ownership,
        string ownerId,
        int baseDurability)
    {
        DRPGWeaponDefinitionSO definition =
            ScriptableObject.CreateInstance<DRPGWeaponDefinitionSO>();
        SetField(definition, "weaponId", weaponId);
        SetField(definition, "ownershipKind", ownership);
        SetField(definition, "signatureCharacterId", ownerId);
        SetField(definition, "baseDurability", baseDurability);
        SetField(
            definition,
            "rarity",
            ownership == DRPGWeaponOwnershipKind.Signature
                ? DRPGEquipmentRarity.RedGold
                : DRPGEquipmentRarity.Blue);
        return definition;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, fieldName);
        field.SetValue(target, value);
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    private static void AssertFalse(bool value, string message)
    {
        if (value)
            throw new Exception(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
        {
            throw new Exception(
                message + ". Expected=" + expected + ", Actual=" + actual);
        }
    }
}
#endif
