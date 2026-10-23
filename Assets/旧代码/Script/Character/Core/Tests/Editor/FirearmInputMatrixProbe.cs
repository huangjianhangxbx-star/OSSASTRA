#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;

public static class FirearmInputMatrixProbe
{
    public static string Run()
    {
        AssertDecision(true, true, true,
            HoundCombatActionStateMachine.CombatInputDecision.Reload);
        AssertDecision(false, true, false,
            HoundCombatActionStateMachine.CombatInputDecision.Fire);
        AssertDecision(false, true, true,
            HoundCombatActionStateMachine.CombatInputDecision.Fire);
        AssertDecision(false, false, true,
            HoundCombatActionStateMachine.CombatInputDecision.EnterAim);
        AssertDecision(false, false, false,
            HoundCombatActionStateMachine.CombatInputDecision.None);

        AssertEqual(
            FirearmStance.HipFire,
            HoundCombatActionStateMachine.ResolveFirearmStance(false),
            "Left-only fire must use HipFire");
        AssertEqual(
            FirearmStance.Ads,
            HoundCombatActionStateMachine.ResolveFirearmStance(true),
            "Right-held fire must use ADS");

        AssertEqual(
            FirearmFireContinuation.ContinueFire,
            HoundCombatActionStateMachine.ResolveFireContinuation(
                attackHeld: true,
                aimHeld: false,
                firePoseExpired: true),
            "Releasing right while holding left must continue hip fire");
        AssertEqual(
            FirearmFireContinuation.Aim,
            HoundCombatActionStateMachine.ResolveFireContinuation(
                attackHeld: false,
                aimHeld: true,
                firePoseExpired: true),
            "Releasing left while holding right must return to Aim");
        AssertEqual(
            FirearmFireContinuation.Idle,
            HoundCombatActionStateMachine.ResolveFireContinuation(
                attackHeld: false,
                aimHeld: false,
                firePoseExpired: true),
            "Releasing both buttons must return to Idle");
        AssertEqual(
            FirearmFireContinuation.WaitForPose,
            HoundCombatActionStateMachine.ResolveFireContinuation(
                attackHeld: false,
                aimHeld: false,
                firePoseExpired: false),
            "Fire pose must finish before leaving Fire");

        VerifyHipFireEligibilityDoesNotRequireAim();
        return "PASS firearm input matrix";
    }

    private static void AssertDecision(
        bool reloadPressed,
        bool attackPressed,
        bool aimHeld,
        HoundCombatActionStateMachine.CombatInputDecision expected)
    {
        var actual = HoundCombatActionStateMachine.ResolveIdleFirearmInput(
            reloadPressed,
            attackPressed,
            aimHeld);
        AssertEqual(expected, actual, "Unexpected firearm idle decision");
    }

    private static void VerifyHipFireEligibilityDoesNotRequireAim()
    {
        var config = ScriptableObject.CreateInstance<WeaponConfigSO>();
        var profile = ScriptableObject.CreateInstance<FirearmActionProfileSO>();
        try
        {
            config.weaponType = WeaponType.Firearm;
            config.weaponActionProfile = profile;

            var bus = new CharacterDataBus
            {
                isAlive = true,
                isInSafeZone = false,
                weaponEquipState = WeaponEquipState.Equipped,
                currentWeaponRuntimeData = new WeaponRuntimeData(config),
                aimHeld = false,
                isAiming = false
            };
            var machine = new HoundCombatActionStateMachine();
            SetPrivate(machine, "dataBus", bus);

            if (!machine.CanFireCurrentFirearm())
                throw new Exception("Hip fire eligibility still requires ADS");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(config);
        }
    }

    private static void SetPrivate(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
            throw new Exception($"Missing field: {fieldName}");
        field.SetValue(target, value);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
            throw new Exception($"{message}. Expected={expected}, Actual={actual}");
    }
}
#endif
