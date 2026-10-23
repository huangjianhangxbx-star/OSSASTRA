using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class BattleCameraSequenceDefinition
{
    [SerializeField] private string defaultShotId = "default";
    [SerializeField] private List<BattleCameraShotDefinition> shots =
        new List<BattleCameraShotDefinition>();

    public BattleCameraSequenceDefinition(
        string defaultShotId,
        IEnumerable<BattleCameraShotDefinition> shots)
    {
        if (string.IsNullOrWhiteSpace(defaultShotId))
            throw new ArgumentException("Default camera shot ID cannot be blank.", nameof(defaultShotId));

        this.defaultShotId = defaultShotId.Trim();
        this.shots = shots == null
            ? new List<BattleCameraShotDefinition>()
            : new List<BattleCameraShotDefinition>(shots);

        ValidateOrThrow();
    }

    public string DefaultShotId => string.IsNullOrWhiteSpace(defaultShotId)
        ? string.Empty
        : defaultShotId.Trim();
    public IReadOnlyList<BattleCameraShotDefinition> Shots => shots;

    public bool TryGetDefault(out BattleCameraShotDefinition shot) =>
        TryGetShot(DefaultShotId, out shot);

    public bool TryGetShot(
        string shotId,
        out BattleCameraShotDefinition shot)
    {
        string wanted = string.IsNullOrWhiteSpace(shotId)
            ? string.Empty
            : shotId.Trim();
        if (shots != null)
        {
            for (int i = 0; i < shots.Count; i++)
            {
                BattleCameraShotDefinition candidate = shots[i];
                if (candidate != null &&
                    string.Equals(candidate.ShotId, wanted, StringComparison.Ordinal))
                {
                    shot = candidate;
                    return true;
                }
            }
        }

        shot = null;
        return false;
    }

    public bool TryValidate(out string error)
    {
        try
        {
            ValidateOrThrow();
            error = string.Empty;
            return true;
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private void ValidateOrThrow()
    {
        if (string.IsNullOrWhiteSpace(DefaultShotId))
            throw new ArgumentException("Default camera shot ID cannot be blank.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (shots != null)
        {
            for (int i = 0; i < shots.Count; i++)
            {
                BattleCameraShotDefinition shot = shots[i];
                if (shot == null || string.IsNullOrWhiteSpace(shot.ShotId))
                    throw new ArgumentException($"Camera shot at index {i} is invalid.");
                if (!ids.Add(shot.ShotId))
                    throw new ArgumentException($"Duplicate camera shot ID: {shot.ShotId}");
            }
        }

        if (!ids.Contains(DefaultShotId))
            throw new ArgumentException($"Default camera shot does not exist: {DefaultShotId}");
    }
}
