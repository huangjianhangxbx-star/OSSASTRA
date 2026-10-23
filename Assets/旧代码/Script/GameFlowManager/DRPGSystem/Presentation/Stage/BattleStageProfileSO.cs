using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "BattleStageProfile",
    menuName = "ANKALUTE/DRPG/Presentation/Battle Stage Profile")]
public sealed class BattleStageProfileSO : ScriptableObject
{
    [SerializeField] private string profileId = "default";
    [SerializeField] private GameObject stagePrefab;
    [SerializeField] private List<BattleStageAnchorDefinition> anchors =
        new List<BattleStageAnchorDefinition>();
    [SerializeField] private BattleCameraSequenceDefinition cameraSequence;

    public string ProfileId => string.IsNullOrWhiteSpace(profileId)
        ? name
        : profileId.Trim();
    public GameObject StagePrefab => stagePrefab;
    public IReadOnlyList<BattleStageAnchorDefinition> Anchors => anchors;
    public BattleCameraSequenceDefinition CameraSequence => cameraSequence;

    public void Configure(
        string id,
        GameObject prefab,
        IEnumerable<BattleStageAnchorDefinition> stageAnchors,
        BattleCameraSequenceDefinition sequence)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Stage profile ID cannot be blank.", nameof(id));

        profileId = id.Trim();
        stagePrefab = prefab;
        anchors = stageAnchors == null
            ? new List<BattleStageAnchorDefinition>()
            : new List<BattleStageAnchorDefinition>(stageAnchors);
        cameraSequence = sequence;
        if (!TryValidate(out string error))
            throw new ArgumentException(error, nameof(stageAnchors));
    }

    public bool TryGetAnchor(
        string anchorId,
        out BattleStageAnchorDefinition anchor)
    {
        string wanted = string.IsNullOrWhiteSpace(anchorId)
            ? string.Empty
            : anchorId.Trim();
        if (anchors != null)
        {
            for (int i = 0; i < anchors.Count; i++)
            {
                BattleStageAnchorDefinition candidate = anchors[i];
                if (candidate != null &&
                    string.Equals(candidate.AnchorId, wanted, StringComparison.Ordinal))
                {
                    anchor = candidate;
                    return true;
                }
            }
        }

        anchor = null;
        return false;
    }

    public bool TryValidate(out string error)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (anchors != null)
        {
            for (int i = 0; i < anchors.Count; i++)
            {
                BattleStageAnchorDefinition anchor = anchors[i];
                if (anchor == null || string.IsNullOrWhiteSpace(anchor.AnchorId))
                {
                    error = $"Stage anchor at index {i} is invalid.";
                    return false;
                }

                if (!ids.Add(anchor.AnchorId))
                {
                    error = $"Duplicate stage anchor ID: {anchor.AnchorId}";
                    return false;
                }
            }
        }

        if (cameraSequence != null && !cameraSequence.TryValidate(out error))
            return false;

        error = string.Empty;
        return true;
    }
}
