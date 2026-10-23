using System;
using System.Collections.Generic;
using UnityEngine;

// Restores presentation containers/overlays and scene sprite tint only. It
// intentionally does not snapshot HP fill, labels, status icons or input rules.
internal sealed class BattleEnvironmentRestore
{
    private readonly CanvasGroup[] groups;
    private readonly BattleProjectionCanvasGroup[] groupStates;
    private readonly BattleProjectionPose[] groupPoses;
    private readonly Transform[] groupParents;
    private readonly SpriteRenderer[] backgrounds;
    private readonly Color[] backgroundColors;
    private readonly BattleProjectionRenderer[] backgroundStates;
    private readonly Transform[] backgroundParents;

    internal BattleEnvironmentRestore(Transform uiRoot, Transform worldRoot, IReadOnlyList<BattleWorldActorView> actors)
    {
        bool IsExcluded(Transform t)
        {
            foreach (var actor in actors) if (t == actor.transform || t.IsChildOf(actor.transform)) return true;
            return t.GetComponentInParent<Camera>(true) != null;
        }
        var foundGroups = new HashSet<CanvasGroup>();
        foreach (var root in new[] { uiRoot, worldRoot })
            if (root != null)
                foreach (var g in root.GetComponentsInChildren<CanvasGroup>(true)) if (!IsExcluded(g.transform)) foundGroups.Add(g);
        groups = new List<CanvasGroup>(foundGroups).ToArray();
        groupStates = new BattleProjectionCanvasGroup[groups.Length]; groupPoses = new BattleProjectionPose[groups.Length];
        groupParents = new Transform[groups.Length];
        for (int i = 0; i < groups.Length; i++)
        {
            groupStates[i] = new BattleProjectionCanvasGroup(groups[i]); groupPoses[i] = new BattleProjectionPose(groups[i].transform);
            groupParents[i] = groups[i].transform.parent;
        }
        var foundSprites = new List<SpriteRenderer>();
        if (worldRoot != null)
            foreach (var r in worldRoot.GetComponentsInChildren<SpriteRenderer>(true))
                // Inactive authored models are templates, not the rendered
                // battlefield. Restoration must not repair/alter their binding.
                if (r.gameObject.activeInHierarchy && !IsExcluded(r.transform)) foundSprites.Add(r);
        backgrounds = foundSprites.ToArray(); backgroundColors = new Color[backgrounds.Length];
        backgroundStates = new BattleProjectionRenderer[backgrounds.Length]; backgroundParents = new Transform[backgrounds.Length];
        for (int i = 0; i < backgrounds.Length; i++)
        {
            backgroundColors[i] = backgrounds[i].color; backgroundStates[i] = new BattleProjectionRenderer(backgrounds[i]);
            backgroundParents[i] = backgrounds[i].transform.parent;
        }
    }
    internal Transition Begin(DRPGBattleActionProjectionLease lease, IDisposable owner) => new Transition(this, lease, owner);

    internal sealed class Transition
    {
        private readonly BattleEnvironmentRestore home;
        private readonly DRPGBattleActionProjectionLease lease;
        private readonly IDisposable owner;
        private readonly float[] alpha;
        private readonly BattleProjectionPose[] origins;
        private readonly Color[] colors;
        internal Transition(BattleEnvironmentRestore home, DRPGBattleActionProjectionLease lease, IDisposable owner)
        {
            this.home = home; this.lease = lease; this.owner = owner;
            alpha = new float[home.groups.Length]; origins = new BattleProjectionPose[alpha.Length];
            colors = new Color[home.backgrounds.Length];
            if (!IsValid) return;
            for (int i = 0; i < alpha.Length; i++) { alpha[i] = home.groups[i].alpha; origins[i] = new BattleProjectionPose(home.groups[i].transform); }
            for (int i = 0; i < colors.Length; i++) colors[i] = home.backgrounds[i].color;
        }
        internal bool IsValid
        {
            get
            {
                if (!lease.OwnsPresentation(owner)) return false;
                for (int i = 0; i < home.groups.Length; i++)
                    if (home.groups[i] == null || home.groups[i].transform.parent != home.groupParents[i]) return false;
                for (int i = 0; i < home.backgrounds.Length; i++)
                    if (home.backgrounds[i] == null || home.backgrounds[i].transform.parent != home.backgroundParents[i]) return false;
                return true;
            }
        }
        internal bool Apply(float progress)
        {
            if (!IsValid) return false;
            for (int i = 0; i < home.groups.Length; i++)
            {
                var g = home.groups[i]; var end = home.groupStates[i]; var pose = home.groupPoses[i]; var from = origins[i];
                g.alpha = progress >= 1 ? end.Alpha : Mathf.LerpUnclamped(alpha[i], end.Alpha, progress);
                if (pose.ActiveSelf && !g.gameObject.activeSelf) g.gameObject.SetActive(true);
                if (!IsValid) return false; // OnEnable may close the owner.
                var t = g.transform;
                // Layout/CanvasScaler owns driven rectangles, never this cue.
                if (!(t is RectTransform rect) || rect.drivenByObject == null)
                {
                    if (t is RectTransform r)
                    {
                        r.anchorMin = progress >= 1 ? pose.AnchorMin : Vector2.LerpUnclamped(from.AnchorMin, pose.AnchorMin, progress);
                        r.anchorMax = progress >= 1 ? pose.AnchorMax : Vector2.LerpUnclamped(from.AnchorMax, pose.AnchorMax, progress);
                        r.pivot = progress >= 1 ? pose.Pivot : Vector2.LerpUnclamped(from.Pivot, pose.Pivot, progress);
                        r.sizeDelta = progress >= 1 ? pose.SizeDelta : Vector2.LerpUnclamped(from.SizeDelta, pose.SizeDelta, progress);
                        r.anchoredPosition3D = progress >= 1 ? pose.AnchoredPosition : Vector3.LerpUnclamped(from.AnchoredPosition, pose.AnchoredPosition, progress);
                    }
                    else t.localPosition = progress >= 1 ? pose.LocalPosition : Vector3.LerpUnclamped(from.LocalPosition, pose.LocalPosition, progress);
                    t.localRotation = progress >= 1 ? pose.LocalRotation : Quaternion.SlerpUnclamped(from.LocalRotation, pose.LocalRotation, progress);
                    t.localScale = progress >= 1 ? pose.LocalScale : Vector3.LerpUnclamped(from.LocalScale, pose.LocalScale, progress);
                }
                if (progress < 1) continue;
                g.ignoreParentGroups = end.IgnoreParentGroups; g.interactable = end.Interactable;
                g.blocksRaycasts = end.BlocksRaycasts; g.enabled = end.Enabled;
                if (!IsValid) return false;
                g.gameObject.SetActive(pose.ActiveSelf);
                if (!IsValid) return false;
            }
            for (int i = 0; i < home.backgrounds.Length; i++)
            {
                var r = home.backgrounds[i]; var end = home.backgroundStates[i];
                r.color = progress >= 1 ? home.backgroundColors[i] : Color.LerpUnclamped(colors[i], home.backgroundColors[i], progress);
                if (progress < 1) continue;
                r.sortingLayerID = end.SortingLayerId; r.sortingOrder = end.SortingOrder; r.enabled = end.Enabled;
                // Background placement follows the returning camera in its own
                // existing LateUpdate; writing its transform here would fight it.
            }
            return IsValid;
        }
    }
}
