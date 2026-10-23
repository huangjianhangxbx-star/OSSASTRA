using UnityEngine;

/// <summary>
/// Resolves pointer positions to ground targets and creates world pickup instances.
/// </summary>
public sealed class WorldItemDropSpawner : MonoBehaviour
{
    [SerializeField] private WorldPickupItem pickupPrefab;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private float gameplayPlaneZ = 0f;

    public bool TryResolveDrop(Vector2 screenPosition, out WorldDropPlan plan)
    {
        plan = default;
        var cameraToUse = worldCamera != null ? worldCamera : Camera.main;
        if (cameraToUse == null || pickupPrefab == null)
            return false;

        float depth = Mathf.Abs(gameplayPlaneZ - cameraToUse.transform.position.z);
        Vector3 world = cameraToUse.ScreenToWorldPoint(
            new Vector3(screenPosition.x, screenPosition.y, depth));
        plan = new WorldDropPlan(
            new Vector3(world.x, world.y, gameplayPlaneZ));
        return true;
    }

    public WorldPickupItem Spawn(ItemInstance item, WorldDropPlan plan)
    {
        if (item?.definition == null || pickupPrefab == null)
            return null;

        var pickup = Instantiate(
            pickupPrefab,
            plan.spawnPosition,
            Quaternion.identity);
        pickup.name = $"Dropped_{item.definition.name}_{item.instanceId}";
        pickup.InjectInstance(item);
        return pickup;
    }
}
