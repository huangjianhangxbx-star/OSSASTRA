using System;
using UnityEngine;

public static class BattleStageAnchorIds
{
    public const string PlayerFront = "player.front";
    public const string PlayerMiddle = "player.middle";
    public const string PlayerRear = "player.rear";
    public const string EnemyFront = "enemy.front";
    public const string EnemyMiddle = "enemy.middle";
    public const string EnemyRear = "enemy.rear";
    public const string PresentationCenter = "presentation.center";
}

[Serializable]
public sealed class BattleStageAnchorDefinition
{
    [SerializeField] private string anchorId;
    [SerializeField] private Vector3 localPosition;
    [SerializeField] private Vector3 localEulerAngles;
    [SerializeField] private Vector3 localScale = Vector3.one;

    public BattleStageAnchorDefinition(
        string anchorId,
        Vector3 localPosition,
        Vector3 localEulerAngles)
        : this(anchorId, localPosition, localEulerAngles, Vector3.one)
    {
    }

    public BattleStageAnchorDefinition(
        string anchorId,
        Vector3 localPosition,
        Vector3 localEulerAngles,
        Vector3 localScale)
    {
        if (string.IsNullOrWhiteSpace(anchorId))
            throw new ArgumentException("Stage anchor ID cannot be blank.", nameof(anchorId));

        this.anchorId = anchorId.Trim();
        this.localPosition = localPosition;
        this.localEulerAngles = localEulerAngles;
        this.localScale = localScale == Vector3.zero ? Vector3.one : localScale;
    }

    public string AnchorId => string.IsNullOrWhiteSpace(anchorId)
        ? string.Empty
        : anchorId.Trim();
    public Vector3 LocalPosition => localPosition;
    public Vector3 LocalEulerAngles => localEulerAngles;
    public Vector3 LocalScale => localScale == Vector3.zero ? Vector3.one : localScale;
}
