#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class PackageWorldDropTransactionProbe
{
    public static string Run()
    {
        var backpack = new CharacterBackpack(3);
        var service = new InventoryTransferService(backpack, new CharacterWeaponSlots());
        var definition = ScriptableObject.CreateInstance<WeaponItemDefinitionSO>();
        var policy = ScriptableObject.CreateInstance<WorldDropPolicySO>();

        try
        {
            var definitionSO = new SerializedObject(definition);
            definitionSO.FindProperty("category").enumValueIndex = (int)ItemCategory.Weapon;
            definitionSO.ApplyModifiedPropertiesWithoutUndo();
            policy.AllowCategory(ItemCategory.Weapon);

            var original = new ItemInstance(definition);
            service.TryAddPickup(original);

            var extract = typeof(InventoryTransferService).GetMethod(
                "TryExtractBackpackItem",
                BindingFlags.Public | BindingFlags.Instance);
            var restore = typeof(InventoryTransferService).GetMethod(
                "TryRestoreBackpackItem",
                BindingFlags.Public | BindingFlags.Instance);
            if (extract == null || restore == null)
                throw new Exception("World-drop transaction API is missing");

            object[] extractArgs = { 0, policy, null };
            if (!(bool)extract.Invoke(service, extractArgs))
                throw new Exception("Extraction failed");

            var extracted = (ItemInstance)extractArgs[2];
            if (!ReferenceEquals(original, extracted) || backpack.GetItem(0) != null)
                throw new Exception("Extraction lost identity or did not clear source");

            if (!(bool)restore.Invoke(service, new object[] { 0, extracted }))
                throw new Exception("Restoration failed");
            if (!ReferenceEquals(original, backpack.GetItem(0)))
                throw new Exception("Restoration changed identity");

            policy.DisallowCategory(ItemCategory.Weapon);
            object[] rejectedArgs = { 0, policy, null };
            if ((bool)extract.Invoke(service, rejectedArgs) || !ReferenceEquals(original, backpack.GetItem(0)))
                throw new Exception("Rejected category mutated inventory");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(definition);
            UnityEngine.Object.DestroyImmediate(policy);
        }

        return "PASS world drop transaction";
    }
}
#endif
