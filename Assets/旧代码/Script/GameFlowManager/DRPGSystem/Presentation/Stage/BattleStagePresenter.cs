using UnityEngine;

public interface IBattleStagePresenter
{
    BattleStageStateSnapshot Capture();
    void Activate(BattleStageProfileSO profile);
    void SwitchTemporary(BattleStageProfileSO profile);
    void Restore(BattleStageStateSnapshot snapshot);
}

public sealed class BattleStagePresenter : MonoBehaviour, IBattleStagePresenter
{
    [SerializeField] private BattleStageRoot stageRoot;

    public BattleStageProfileSO ActiveProfile =>
        stageRoot != null ? stageRoot.ActiveProfile : null;

    public void Configure(BattleStageRoot root)
    {
        stageRoot = root != null ? root : GetComponent<BattleStageRoot>();
    }

    public BattleStageStateSnapshot Capture()
    {
        EnsureRoot();
        return stageRoot.Capture();
    }

    public void Activate(BattleStageProfileSO profile)
    {
        EnsureRoot();
        stageRoot.Activate(profile);
    }

    public void SwitchTemporary(BattleStageProfileSO profile) => Activate(profile);

    public void Restore(BattleStageStateSnapshot snapshot)
    {
        EnsureRoot();
        stageRoot.Restore(snapshot);
    }

    private void EnsureRoot()
    {
        if (stageRoot == null)
            stageRoot = GetComponent<BattleStageRoot>();
        if (stageRoot == null)
            stageRoot = gameObject.AddComponent<BattleStageRoot>();
    }
}
