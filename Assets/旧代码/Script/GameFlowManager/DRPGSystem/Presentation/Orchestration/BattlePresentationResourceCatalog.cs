using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BattlePresentationResourceCatalog : MonoBehaviour
{
    [SerializeField] private List<BattleStageProfileSO> stageProfiles =
        new List<BattleStageProfileSO>();
    [SerializeField] private List<BattleCameraShotDefinition> cameraShots =
        new List<BattleCameraShotDefinition>();

    public IReadOnlyList<BattleStageProfileSO> StageProfiles => stageProfiles;

    public void Configure(
        IEnumerable<BattleStageProfileSO> profiles,
        IEnumerable<BattleCameraShotDefinition> shots)
    {
        stageProfiles = profiles == null
            ? new List<BattleStageProfileSO>()
            : new List<BattleStageProfileSO>(profiles);
        cameraShots = shots == null
            ? new List<BattleCameraShotDefinition>()
            : new List<BattleCameraShotDefinition>(shots);
    }

    public bool TryGetStage(string profileKey, out BattleStageProfileSO profile)
    {
        string key = profileKey?.Trim() ?? string.Empty;
        for (int i = 0; i < stageProfiles.Count; i++)
        {
            BattleStageProfileSO candidate = stageProfiles[i];
            if (candidate != null && string.Equals(
                    candidate.ProfileId,
                    key,
                    StringComparison.Ordinal))
            {
                profile = candidate;
                return true;
            }
        }
        profile = null;
        return false;
    }

    public bool TryGetShot(
        string shotKey,
        BattleStageProfileSO activeStage,
        out BattleCameraShotDefinition shot)
    {
        string key = shotKey?.Trim() ?? string.Empty;
        if (activeStage != null && activeStage.CameraSequence != null)
        {
            if (!string.IsNullOrEmpty(key) &&
                activeStage.CameraSequence.TryGetShot(key, out shot))
            {
                return true;
            }
            if (string.IsNullOrEmpty(key) &&
                activeStage.CameraSequence.TryGetDefault(out shot))
            {
                return true;
            }
        }

        for (int i = 0; i < cameraShots.Count; i++)
        {
            BattleCameraShotDefinition candidate = cameraShots[i];
            if (candidate != null && string.Equals(
                    candidate.ShotId,
                    key,
                    StringComparison.Ordinal))
            {
                shot = candidate;
                return true;
            }
        }
        shot = null;
        return false;
    }
}
