using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class BattleCloseupFloatingUiGate : IDisposable
{
    private static readonly HashSet<int> Suppressed = new HashSet<int>();
    private readonly struct Baseline
    {
        internal Baseline(CanvasGroup group)
        {
            Group = group;
            Alpha = group.alpha;
            Interactable = group.interactable;
            BlocksRaycasts = group.blocksRaycasts;
        }
        internal CanvasGroup Group { get; }
        internal float Alpha { get; }
        internal bool Interactable { get; }
        internal bool BlocksRaycasts { get; }
    }

    private readonly ActionIdentity owner;
    private readonly List<Baseline> baselines = new List<Baseline>();
    private bool hidden;
    private bool restored;

    internal BattleCloseupFloatingUiGate(ActionIdentity owner, IReadOnlyList<CanvasGroup> groups)
    {
        if (!owner.IsValid) throw new ArgumentException("A valid action owner is required.", nameof(owner));
        this.owner = owner;
        if (groups == null) return;
        var seen = new HashSet<CanvasGroup>();
        for (int i = 0; i < groups.Count; i++)
            if (groups[i] != null && seen.Add(groups[i])) baselines.Add(new Baseline(groups[i]));
    }

    internal void Hide(ActionIdentity action)
    {
        if (action != owner || restored) return;
        hidden = true;
        for (int i = 0; i < baselines.Count; i++)
        {
            CanvasGroup group = baselines[i].Group;
            if (group == null) continue;
            Suppressed.Add(group.GetInstanceID());
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }
    }

    internal void RestoreAtReturnStart(ActionIdentity action)
    {
        if (action != owner || restored) return;
        Restore();
    }

    public void Dispose()
    {
        if (!restored && hidden) Restore();
    }

    private void Restore()
    {
        restored = true;
        for (int i = 0; i < baselines.Count; i++)
        {
            Baseline baseline = baselines[i];
            if (baseline.Group == null) continue;
            Suppressed.Remove(baseline.Group.GetInstanceID());
            baseline.Group.alpha = baseline.Alpha;
            baseline.Group.interactable = baseline.Interactable;
            baseline.Group.blocksRaycasts = baseline.BlocksRaycasts;
        }
    }

    internal static bool IsSuppressed(CanvasGroup group) =>
        group != null && Suppressed.Contains(group.GetInstanceID());
}
