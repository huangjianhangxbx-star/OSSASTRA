#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;

public static class WorldDropPolicyProbe
{
    public static string Run()
    {
        var policyType = Type.GetType("WorldDropPolicySO, Assembly-CSharp");
        if (policyType == null)
            throw new Exception("WorldDropPolicySO is missing");

        var policy = ScriptableObject.CreateInstance(policyType);
        try
        {
            var allow = policyType.GetMethod("AllowCategory", BindingFlags.Public | BindingFlags.Instance);
            var disallow = policyType.GetMethod("DisallowCategory", BindingFlags.Public | BindingFlags.Instance);
            var canDrop = policyType.GetMethod("CanDrop", BindingFlags.Public | BindingFlags.Instance);
            if (allow == null || disallow == null || canDrop == null)
                throw new Exception("WorldDropPolicySO API is incomplete");

            if ((bool)canDrop.Invoke(policy, new object[] { null }))
                throw new Exception("Policy accepted a null item");
            if (!(bool)allow.Invoke(policy, new object[] { ItemCategory.Weapon }))
                throw new Exception("Policy did not add Weapon");
            if ((bool)allow.Invoke(policy, new object[] { ItemCategory.Weapon }))
                throw new Exception("Policy added duplicate Weapon");

            var definition = ScriptableObject.CreateInstance<WeaponItemDefinitionSO>();
            try
            {
                var definitionSO = new UnityEditor.SerializedObject(definition);
                definitionSO.FindProperty("category").enumValueIndex = (int)ItemCategory.Weapon;
                definitionSO.ApplyModifiedPropertiesWithoutUndo();
                var item = new ItemInstance(definition);
                if (!(bool)canDrop.Invoke(policy, new object[] { item }))
                    throw new Exception("Policy rejected allowed Weapon");
                if (!(bool)disallow.Invoke(policy, new object[] { ItemCategory.Weapon }))
                    throw new Exception("Policy did not remove Weapon");
                if ((bool)canDrop.Invoke(policy, new object[] { item }))
                    throw new Exception("Policy accepted removed Weapon");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(policy);
        }

        return "PASS world drop policy";
    }
}
#endif
