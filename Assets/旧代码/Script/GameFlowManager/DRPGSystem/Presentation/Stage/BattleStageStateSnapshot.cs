using UnityEngine;

public readonly struct BattleStageStateSnapshot
{
    public BattleStageStateSnapshot(
        BattleStageProfileSO profile,
        GameObject stageInstance,
        bool wasActive)
    {
        Profile = profile;
        StageInstance = stageInstance;
        WasActive = wasActive;
    }

    public BattleStageProfileSO Profile { get; }
    public GameObject StageInstance { get; }
    public bool WasActive { get; }
}
