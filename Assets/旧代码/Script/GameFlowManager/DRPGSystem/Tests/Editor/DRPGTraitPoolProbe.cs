#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

public static class DRPGTraitPoolProbe
{
    public static string Run()
    {
        Type poolType = FindType("DRPGTraitPoolState");
        if (poolType == null)
            throw new Exception("DRPGTraitPoolState is missing.");

        object pool = Activator.CreateInstance(poolType);
        MethodInfo addRegular = RequireMethod(poolType, "TryAddRegularTrait");
        MethodInfo removeRegular = RequireMethod(poolType, "TryRemoveRegularTrait");
        MethodInfo bindSource = RequireMethod(poolType, "TryBindSourceTrait");
        MethodInfo removeSource = RequireMethod(poolType, "TryRemoveSourceTrait");
        MethodInfo exportSnapshot = RequireMethod(poolType, "ExportSnapshot");
        MethodInfo importSnapshot = RequireMethod(poolType, "ImportSnapshot");

        for (int i = 0; i < 10; i++)
        {
            AssertSuccess(
                addRegular.Invoke(pool, new object[] { "regular_" + i }),
                "Ten regular trait slots must be usable.");
        }
        AssertFailure(
            addRegular.Invoke(pool, new object[] { "regular_overflow" }),
            "An eleventh regular trait must be rejected.");
        AssertFailure(
            addRegular.Invoke(pool, new object[] { "regular_0" }),
            "A duplicate regular trait must be rejected.");

        AssertSuccess(
            bindSource.Invoke(
                pool,
                new object[] { "outfit:uniform_a", "outfit_trait_a" }),
            "An outfit-bound trait must bind outside regular capacity.");
        AssertEqual(10, CountProperty(pool, "RegularTraitIds"),
            "Source traits must not consume regular capacity.");
        AssertEqual(11, CountProperty(pool, "ResolvedTraitIds"),
            "Resolved traits must include the outfit trait.");
        AssertFailure(
            removeRegular.Invoke(pool, new object[] { "outfit_trait_a" }),
            "A source-bound trait cannot be removed as a regular trait.");

        AssertSuccess(
            bindSource.Invoke(
                pool,
                new object[] { "outfit:uniform_b", "outfit_trait_b" }),
            "A second source may bind its own trait.");
        AssertSuccess(
            removeSource.Invoke(pool, new object[] { "outfit:uniform_a" }),
            "Removing an outfit source must remove only that source.");
        AssertEqual(11, CountProperty(pool, "ResolvedTraitIds"),
            "The second source trait must survive first-source removal.");

        object snapshot = exportSnapshot.Invoke(pool, null);
        object restored = Activator.CreateInstance(poolType);
        AssertSuccess(
            importSnapshot.Invoke(restored, new[] { snapshot }),
            "Trait snapshot must restore.");
        AssertEqual(10, CountProperty(restored, "RegularTraitIds"),
            "Restored regular traits must be complete.");
        AssertEqual(11, CountProperty(restored, "ResolvedTraitIds"),
            "Restored source traits must be complete.");

        var outfitA = ScriptableObject.CreateInstance<DRPGOutfitDefinitionSO>();
        var outfitB = ScriptableObject.CreateInstance<DRPGOutfitDefinitionSO>();
        try
        {
            SetField(outfitA, "outfitId", "uniform_a");
            SetField(outfitA, "grantedTraitId", "outfit_trait_a");
            SetField(outfitB, "outfitId", "uniform_b");
            SetField(outfitB, "grantedTraitId", "outfit_trait_b");

            var equipment = new DRPGCharacterEquipmentState("trait_actor");
            AssertSuccess(equipment.TrySetOutfit(outfitA),
                "Equipping the first outfit must bind its trait.");
            AssertEqual(1, CountProperty(equipment, "ResolvedTraitIds"),
                "Equipment must expose the first outfit trait.");
            AssertSuccess(equipment.TrySetOutfit(outfitB),
                "Replacing an outfit must atomically replace its source trait.");
            AssertEqual(1, CountProperty(equipment, "ResolvedTraitIds"),
                "Outfit replacement must not leak the previous source trait.");
            AssertSuccess(equipment.TrySetOutfit(null),
                "Removing the outfit must unbind its source trait.");
            AssertEqual(0, CountProperty(equipment, "ResolvedTraitIds"),
                "Removing an outfit must remove only its bound trait.");
            AssertEqual(3, equipment.Accessories.Count,
                "Trait integration must preserve exactly three accessory slots.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(outfitB);
            UnityEngine.Object.DestroyImmediate(outfitA);
        }

        return "PASS DRPG trait pool";
    }

    private static Type FindType(string name)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type type = assemblies[i].GetType(name, false);
            if (type != null)
                return type;
        }
        return null;
    }

    private static MethodInfo RequireMethod(Type type, string name)
    {
        MethodInfo method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public);
        if (method == null)
            throw new MissingMethodException(type.Name, name);
        return method;
    }

    private static int CountProperty(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public);
        if (property == null)
            throw new MissingMemberException(target.GetType().Name, propertyName);
        var enumerable = property.GetValue(target) as IEnumerable;
        if (enumerable == null)
            throw new Exception(propertyName + " must be enumerable.");
        int count = 0;
        foreach (object ignored in enumerable)
            count++;
        return count;
    }

    private static void AssertSuccess(object result, string message)
    {
        PropertyInfo success = result?.GetType().GetProperty("Success");
        if (success == null || !(bool)success.GetValue(result))
            throw new Exception(message);
    }

    private static void AssertFailure(object result, string message)
    {
        PropertyInfo success = result?.GetType().GetProperty("Success");
        if (success == null || (bool)success.GetValue(result))
            throw new Exception(message);
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
