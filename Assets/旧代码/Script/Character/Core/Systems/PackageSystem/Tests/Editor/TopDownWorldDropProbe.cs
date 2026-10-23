#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;

public static class TopDownWorldDropProbe
{
    public static string Run()
    {
        var spawnerObject = new GameObject("TopDownWorldDropSpawnerProbe");
        var pickupPrefabObject = new GameObject("TopDownWorldDropPickupProbe");
        var cameraObject = new GameObject("TopDownWorldDropCameraProbe");
        WeaponItemDefinitionSO definition = null;
        WorldPickupItem spawnedPickup = null;

        try
        {
            pickupPrefabObject.AddComponent<SpriteRenderer>();
            pickupPrefabObject.AddComponent<BoxCollider2D>();
            var pickupPrefab = pickupPrefabObject.AddComponent<WorldPickupItem>();
            var spawner = spawnerObject.AddComponent<WorldItemDropSpawner>();
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.pixelRect = new Rect(0f, 0f, 100f, 100f);

            SetPrivate(spawner, "pickupPrefab", pickupPrefab);
            SetPrivate(spawner, "worldCamera", camera);

            if (!spawner.TryResolveDrop(new Vector2(50f, 50f), out WorldDropPlan resolved))
                throw new Exception("TopDown pointer position did not resolve without a ground raycast");
            if (resolved.spawnPosition.sqrMagnitude > 0.001f)
                throw new Exception($"Resolved planar position is wrong: {resolved.spawnPosition}");

            ConstructorInfo directPlan = typeof(WorldDropPlan).GetConstructor(
                new[] { typeof(Vector3) });
            if (directPlan == null)
                throw new Exception("Direct world-drop plan constructor is missing");

            var expected = new Vector3(4f, -3f, 0f);
            var plan = (WorldDropPlan)directPlan.Invoke(new object[] { expected });
            definition = ScriptableObject.CreateInstance<WeaponItemDefinitionSO>();
            var item = new ItemInstance(definition);

            spawnedPickup = spawner.Spawn(item, plan);
            if (spawnedPickup == null)
                throw new Exception("Direct world drop still requires a settling-motion component");
            if ((spawnedPickup.transform.position - expected).sqrMagnitude > 0.0001f)
                throw new Exception("Spawned pickup moved away from its final planar position");
            if (!ReferenceEquals(spawnedPickup.Instance, item))
                throw new Exception("Direct world drop lost ItemInstance identity");
        }
        finally
        {
            if (spawnedPickup != null)
                UnityEngine.Object.DestroyImmediate(spawnedPickup.gameObject);
            if (definition != null)
                UnityEngine.Object.DestroyImmediate(definition);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(pickupPrefabObject);
            UnityEngine.Object.DestroyImmediate(spawnerObject);
        }

        return "PASS TopDown direct world drop";
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
}
#endif
