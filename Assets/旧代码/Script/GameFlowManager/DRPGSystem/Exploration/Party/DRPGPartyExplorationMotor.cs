using UnityEngine;

/// <summary>Moves only the queue root; it never flips or reorders members.</summary>
public sealed class DRPGPartyExplorationMotor
{
    private readonly Transform queueRoot;
    private float speed;
    private float minimumX;
    private float maximumX;

    internal DRPGPartyExplorationMotor(
        Transform root,
        float movementSpeed,
        float minX,
        float maxX)
    {
        queueRoot = root != null
            ? root
            : throw new System.ArgumentNullException(nameof(root));
        Configure(movementSpeed, minX, maxX);
    }

    public bool Enabled { get; set; } = true;

    internal void Configure(float movementSpeed, float minX, float maxX)
    {
        if (!float.IsFinite(movementSpeed) || movementSpeed < 0f)
            throw new System.ArgumentOutOfRangeException(nameof(movementSpeed));
        if (!float.IsFinite(minX) || !float.IsFinite(maxX) || minX > maxX)
            throw new System.ArgumentOutOfRangeException(nameof(minX));
        speed = movementSpeed;
        minimumX = minX;
        maximumX = maxX;
    }

    public void Step(float horizontal, float deltaTime)
    {
        if (!Enabled || !float.IsFinite(horizontal) ||
            !float.IsFinite(deltaTime) || deltaTime <= 0f)
        {
            return;
        }
        float x = Mathf.Clamp(
            queueRoot.position.x + horizontal * speed * deltaTime,
            minimumX,
            maximumX);
        Vector3 position = queueRoot.position;
        position.x = x;
        queueRoot.position = position;
    }
}
