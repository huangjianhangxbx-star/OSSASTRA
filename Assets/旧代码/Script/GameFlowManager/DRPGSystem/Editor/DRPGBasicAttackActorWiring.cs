#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;

internal static class DRPGBasicAttackActorWiring
{
    internal static void Wire(BattleWorldActorView view)
    {
        if (view == null) throw new ArgumentNullException(nameof(view));
        var anchors = new[] { view.VisualRoot, view.ShadowAnchor, view.VfxAnchor,
            view.TargetAnchor, view.StatusAnchor, view.PresentationAnchor };
        var root = view.transform.Find("ActorMotionRoot");
        var seen = new System.Collections.Generic.HashSet<Transform>();
        foreach (var anchor in anchors)
            if (anchor == null || !seen.Add(anchor) ||
                (anchor.parent != view.transform && (root == null || anchor.parent != root)))
                throw new InvalidOperationException("All six unique authored anchors must be direct children before wiring.");
        if (root != null && (root.localPosition != Vector3.zero || root.localRotation != Quaternion.identity || root.localScale != Vector3.one))
            throw new InvalidOperationException("Existing motion root must have its neutral authored pose.");
        if (root == null)
        {
            root = new GameObject("ActorMotionRoot").transform;
            root.SetParent(view.transform, false);
        }
        // Identity parent preserves local offsets; keep original object references.
        var ordered = new System.Collections.Generic.List<Transform>(anchors);
        ordered.Sort((a, b) => a.GetSiblingIndex().CompareTo(b.GetSiblingIndex()));
        foreach (var anchor in ordered)
            if (anchor.parent != root) anchor.SetParent(root, false);
        var serialized = new SerializedObject(view);
        var property = serialized.FindProperty("actorMotionRoot");
        if (property == null) throw new InvalidOperationException("ActorMotionRoot serialized binding is missing.");
        property.objectReferenceValue = root;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
