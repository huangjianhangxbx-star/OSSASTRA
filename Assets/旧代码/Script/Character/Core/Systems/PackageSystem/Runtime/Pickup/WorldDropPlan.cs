using UnityEngine;

public readonly struct WorldDropPlan
{
    public readonly Vector3 spawnPosition;

    public WorldDropPlan(Vector3 spawnPosition)
    {
        this.spawnPosition = spawnPosition;
    }
}
