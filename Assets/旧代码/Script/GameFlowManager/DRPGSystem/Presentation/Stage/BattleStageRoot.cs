using System.Collections.Generic;
using UnityEngine;

public sealed class BattleStageRoot : MonoBehaviour
{
    [SerializeField] private Transform stageContainer;

    private readonly Dictionary<BattleStageProfileSO, GameObject> instances =
        new Dictionary<BattleStageProfileSO, GameObject>();

    public BattleStageProfileSO ActiveProfile { get; private set; }
    public GameObject ActiveInstance { get; private set; }
    public Transform StageContainer => stageContainer != null
        ? stageContainer
        : transform;

    public void Configure(Transform container)
    {
        stageContainer = container != null ? container : transform;
    }

    public BattleStageStateSnapshot Capture() =>
        new BattleStageStateSnapshot(
            ActiveProfile,
            ActiveInstance,
            ActiveInstance != null && ActiveInstance.activeSelf);

    public void Activate(BattleStageProfileSO profile)
    {
        if (profile == null)
        {
            DeactivateCurrent();
            return;
        }

        if (ActiveProfile == profile && ActiveInstance != null)
        {
            ActiveInstance.SetActive(true);
            return;
        }

        DeactivateCurrent();
        if (!instances.TryGetValue(profile, out GameObject instance) ||
            instance == null)
        {
            instance = CreateStageInstance(profile);
            instances[profile] = instance;
        }

        ActiveProfile = profile;
        ActiveInstance = instance;
        ActiveInstance.SetActive(true);
    }

    public void Restore(BattleStageStateSnapshot snapshot)
    {
        if (ActiveInstance != null && ActiveInstance != snapshot.StageInstance)
            ActiveInstance.SetActive(false);

        ActiveProfile = snapshot.Profile;
        ActiveInstance = snapshot.StageInstance;
        if (ActiveInstance != null)
            ActiveInstance.SetActive(snapshot.WasActive);
    }

    public bool TryGetAnchor(
        string anchorId,
        out Transform anchor)
    {
        anchor = null;
        if (ActiveProfile == null || ActiveInstance == null ||
            !ActiveProfile.TryGetAnchor(
                anchorId,
                out BattleStageAnchorDefinition definition))
        {
            return false;
        }

        string objectName = $"Anchor_{definition.AnchorId}";
        Transform existing = ActiveInstance.transform.Find(objectName);
        if (existing != null)
        {
            anchor = existing;
            return true;
        }

        var anchorObject = new GameObject(objectName);
        anchor = anchorObject.transform;
        anchor.SetParent(ActiveInstance.transform, false);
        anchor.localPosition = definition.LocalPosition;
        anchor.localEulerAngles = definition.LocalEulerAngles;
        anchor.localScale = definition.LocalScale;
        return true;
    }

    private GameObject CreateStageInstance(BattleStageProfileSO profile)
    {
        GameObject instance = profile.StagePrefab != null
            ? Instantiate(profile.StagePrefab, StageContainer)
            : new GameObject($"BattleStage_{profile.ProfileId}");
        if (instance.transform.parent != StageContainer)
            instance.transform.SetParent(StageContainer, false);
        instance.name = $"BattleStage_{profile.ProfileId}";
        instance.SetActive(false);
        return instance;
    }

    private void DeactivateCurrent()
    {
        if (ActiveInstance != null)
            ActiveInstance.SetActive(false);
        ActiveProfile = null;
        ActiveInstance = null;
    }
}
