using System;
using UnityEngine;

[Serializable]
public sealed class BattleCameraShotDefinition
{
    public const float MinimumFieldOfView = 15f;
    public const float MaximumFieldOfView = 90f;

    [SerializeField] private string shotId = "default";
    [SerializeField] private Vector3 localPosition = new Vector3(0f, 1.2f, -8f);
    [SerializeField] private Vector3 localEulerAngles;
    [SerializeField, Range(MinimumFieldOfView, MaximumFieldOfView)]
    private float fieldOfView = 45f;
    [SerializeField, Min(0f)] private float durationSeconds = 0.25f;

    public BattleCameraShotDefinition(
        string shotId,
        Vector3 localPosition,
        Vector3 localEulerAngles,
        float fieldOfView,
        float durationSeconds)
    {
        if (string.IsNullOrWhiteSpace(shotId))
            throw new ArgumentException("Camera shot ID cannot be blank.", nameof(shotId));

        this.shotId = shotId.Trim();
        this.localPosition = localPosition;
        this.localEulerAngles = localEulerAngles;
        this.fieldOfView = Mathf.Clamp(
            fieldOfView,
            MinimumFieldOfView,
            MaximumFieldOfView);
        this.durationSeconds = Mathf.Max(0f, durationSeconds);
    }

    public string ShotId => string.IsNullOrWhiteSpace(shotId)
        ? string.Empty
        : shotId.Trim();
    public Vector3 LocalPosition => localPosition;
    public Vector3 LocalEulerAngles => localEulerAngles;
    public float FieldOfView => Mathf.Clamp(
        fieldOfView,
        MinimumFieldOfView,
        MaximumFieldOfView);
    public float DurationSeconds => Mathf.Max(0f, durationSeconds);
}
