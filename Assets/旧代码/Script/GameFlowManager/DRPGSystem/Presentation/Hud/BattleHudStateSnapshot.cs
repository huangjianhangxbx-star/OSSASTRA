using System.Collections.Generic;

public readonly struct BattleHudGroupState
{
    public BattleHudGroupState(
        BattleHudGroupKind kind,
        bool activeSelf,
        float alpha,
        bool interactable,
        bool blocksRaycasts)
    {
        Kind = kind;
        ActiveSelf = activeSelf;
        Alpha = alpha;
        Interactable = interactable;
        BlocksRaycasts = blocksRaycasts;
    }

    public BattleHudGroupKind Kind { get; }
    public bool ActiveSelf { get; }
    public float Alpha { get; }
    public bool Interactable { get; }
    public bool BlocksRaycasts { get; }
}

public sealed class BattleHudStateSnapshot
{
    private readonly Dictionary<BattleHudGroupKind, BattleHudGroupState> states;

    public BattleHudStateSnapshot(
        IEnumerable<BattleHudGroupState> groupStates)
    {
        states = new Dictionary<BattleHudGroupKind, BattleHudGroupState>();
        if (groupStates == null)
            return;
        foreach (BattleHudGroupState state in groupStates)
            states[state.Kind] = state;
    }

    public bool TryGet(
        BattleHudGroupKind kind,
        out BattleHudGroupState state) => states.TryGetValue(kind, out state);
}
