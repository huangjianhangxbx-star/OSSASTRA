using UnityEngine;

public readonly struct BattleCameraStateSnapshot
{
    public BattleCameraStateSnapshot(
        Vector3 position,
        Quaternion rotation,
        float fieldOfView,
        bool orthographic,
        float orthographicSize,
        bool enabled)
    {
        Position = position;
        Rotation = rotation;
        FieldOfView = fieldOfView;
        Orthographic = orthographic;
        OrthographicSize = orthographicSize;
        Enabled = enabled;
    }

    public Vector3 Position { get; }
    public Quaternion Rotation { get; }
    public float FieldOfView { get; }
    public bool Orthographic { get; }
    public float OrthographicSize { get; }
    public bool Enabled { get; }
}
