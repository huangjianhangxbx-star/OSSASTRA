using System;
using System.Collections.Generic;
using UnityEngine;

public interface IBattleHudPresenter
{
    BattleHudStateSnapshot Capture();
    IBattlePresentationHandle Apply(BattleHudVisibilityPolicy policy);
    void Restore(BattleHudStateSnapshot snapshot);
}

public sealed class BattleHudPresenter : MonoBehaviour, IBattleHudPresenter
{
    [SerializeField] private List<BattleHudGroupView> groups =
        new List<BattleHudGroupView>();

    public BattleHudVisibilityPolicy CurrentPolicy { get; private set; } =
        BattleHudVisibilityPolicy.KeepAll;

    public void Configure(IEnumerable<BattleHudGroupView> groupViews)
    {
        groups.Clear();
        if (groupViews == null)
            return;
        foreach (BattleHudGroupView view in groupViews)
        {
            if (view != null && !ContainsKind(view.Kind))
                groups.Add(view);
        }
    }

    public BattleHudStateSnapshot Capture()
    {
        var states = new List<BattleHudGroupState>(groups.Count);
        for (int i = 0; i < groups.Count; i++)
        {
            if (groups[i] != null)
                states.Add(groups[i].Capture());
        }
        return new BattleHudStateSnapshot(states);
    }

    public IBattlePresentationHandle Apply(BattleHudVisibilityPolicy policy)
    {
        CurrentPolicy = policy;
        for (int i = 0; i < groups.Count; i++)
        {
            BattleHudGroupView group = groups[i];
            if (group != null)
                group.ApplyVisible(ShouldShow(group.Kind, policy));
        }

        var handle = new BattlePresentationHandle();
        handle.TrySettle(BattlePresentationOutcome.Completed);
        return handle;
    }

    public void Restore(BattleHudStateSnapshot snapshot)
    {
        if (snapshot == null)
            return;
        for (int i = 0; i < groups.Count; i++)
        {
            BattleHudGroupView group = groups[i];
            if (group != null && snapshot.TryGet(group.Kind, out BattleHudGroupState state))
                group.Restore(state);
        }
        CurrentPolicy = BattleHudVisibilityPolicy.KeepAll;
    }

    private static bool ShouldShow(
        BattleHudGroupKind kind,
        BattleHudVisibilityPolicy policy)
    {
        if (kind == BattleHudGroupKind.PresentationFeedback)
            return true;
        if (policy == BattleHudVisibilityPolicy.KeepAll)
            return true;
        if (policy == BattleHudVisibilityPolicy.HideCommand)
        {
            return kind != BattleHudGroupKind.CommandPanel &&
                   kind != BattleHudGroupKind.TargetingPrompt;
        }
        return false;
    }

    private bool ContainsKind(BattleHudGroupKind kind)
    {
        for (int i = 0; i < groups.Count; i++)
        {
            if (groups[i] != null && groups[i].Kind == kind)
                return true;
        }
        return false;
    }
}
