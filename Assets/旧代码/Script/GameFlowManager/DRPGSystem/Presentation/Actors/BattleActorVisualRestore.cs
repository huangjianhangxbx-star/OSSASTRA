using System;
using System.Collections.Generic;
using UnityEngine;

// Live bindings stay private to the projection, separate from value checkpoints.
internal sealed class BattleActorVisualRestore
{
    private readonly BattleWorldActorView view;
    private readonly int runtimeId, viewId;
    private readonly Transform[] transforms;
    private readonly Transform[] parents;
    private readonly BattleProjectionPose[] poses;
    private readonly SpriteRenderer[] sprites;
    private readonly SpriteState[] spriteStates;
    private readonly BattleWorldActorSpriteDriver driver;
    private readonly bool hasDriver;

    private readonly struct SpriteState
    {
        internal SpriteState(SpriteRenderer r)
        {
            Sprite = r.sprite; Color = r.color; Enabled = r.enabled;
            FlipX = r.flipX; FlipY = r.flipY; Layer = r.sortingLayerID; Order = r.sortingOrder;
        }
        internal readonly Sprite Sprite;
        internal readonly Color Color;
        internal readonly bool Enabled, FlipX, FlipY;
        internal readonly int Layer, Order;
        internal void Apply(SpriteRenderer r, Color origin, float progress)
        {
            r.color = progress >= 1f ? Color : UnityEngine.Color.LerpUnclamped(origin, Color, progress);
            if (progress < 1f) return;
            r.sprite = Sprite; r.flipX = FlipX; r.flipY = FlipY;
            r.sortingLayerID = Layer; r.sortingOrder = Order; r.enabled = Enabled;
        }
    }
    internal BattleActorVisualRestore(BattleWorldActorView view)
    {
        this.view = view; runtimeId = view.RuntimeId; viewId = view.GetInstanceID();
        var children = new List<Transform>(view.GetComponentsInChildren<Transform>(true));
        children.Remove(view.transform); children.Remove(view.MotionRoot); transforms = children.ToArray();
        poses = new BattleProjectionPose[transforms.Length]; parents = new Transform[transforms.Length];
        for (int i = 0; i < poses.Length; i++) { poses[i] = new BattleProjectionPose(transforms[i]); parents[i] = transforms[i].parent; }
        sprites = view.GetComponentsInChildren<SpriteRenderer>(true);
        spriteStates = new SpriteState[sprites.Length];
        for (int i = 0; i < sprites.Length; i++) spriteStates[i] = new SpriteState(sprites[i]);
        driver = view.GetComponentInChildren<BattleWorldActorSpriteDriver>(true);
        hasDriver = driver != null;
    }
    internal Transition Begin(DRPGBattleActionProjectionLease lease, IDisposable motionOwner, bool defeatPending) =>
        new Transition(this, lease, motionOwner, defeatPending);

    internal sealed class Transition
    {
        private readonly BattleActorVisualRestore home;
        private readonly DRPGBattleActionProjectionLease lease;
        private readonly IDisposable motionOwner;
        private readonly bool defeatPending;
        private readonly BattleProjectionPose[] origins;
        private readonly Color[] colors;
        internal Transition(BattleActorVisualRestore home, DRPGBattleActionProjectionLease lease,
            IDisposable motionOwner, bool defeatPending)
        {
            this.home = home; this.lease = lease; this.motionOwner = motionOwner; this.defeatPending = defeatPending;
            origins = new BattleProjectionPose[home.transforms.Length]; colors = new Color[home.sprites.Length];
            if (!IsValid) return;
            for (int i = 0; i < origins.Length; i++) origins[i] = new BattleProjectionPose(home.transforms[i]);
            for (int i = 0; i < colors.Length; i++) colors[i] = home.sprites[i].color;
        }
        internal bool IsValid
        {
            get
            {
                if (!lease.OwnsMotion(motionOwner) || !lease.TryGetView(home.runtimeId, out var pinned) ||
                    pinned != home.view || pinned.GetInstanceID() != home.viewId) return false;
                if (home.hasDriver && (home.driver == null || !home.driver.BoundPoseBelongsTo(lease.Owner.Action))) return false;
                for (int i = 0; i < home.transforms.Length; i++)
                    if (home.transforms[i] == null || home.transforms[i].parent != home.parents[i] || !home.transforms[i].IsChildOf(pinned.transform)) return false;
                foreach (var r in home.sprites) if (r == null || !r.transform.IsChildOf(pinned.transform)) return false;
                return true;
            }
        }
        internal void Apply(float progress)
        {
            if (!IsValid) return;
            for (int i = 0; i < home.transforms.Length; i++)
            {
                var t = home.transforms[i];
                if (defeatPending && (t == home.view.VisualRoot || t.IsChildOf(home.view.VisualRoot))) continue;
                var end = home.poses[i]; var from = origins[i];
                t.localPosition = progress >= 1 ? end.LocalPosition : Vector3.LerpUnclamped(from.LocalPosition, end.LocalPosition, progress);
                t.localRotation = progress >= 1 ? end.LocalRotation : Quaternion.SlerpUnclamped(from.LocalRotation, end.LocalRotation, progress);
                t.localScale = progress >= 1 ? end.LocalScale : Vector3.LerpUnclamped(from.LocalScale, end.LocalScale, progress);
                if (progress >= 1f) t.gameObject.SetActive(end.ActiveSelf);
                // Activation can synchronously close/rebind the projection.
                if (!IsValid) return;
            }
            // Defeat art remains unchanged through the separate rendered-home fence.
            if (defeatPending) return;
            for (int i = 0; i < home.sprites.Length; i++) home.spriteStates[i].Apply(home.sprites[i], colors[i], progress);
            if (progress >= 1f && home.driver != null) home.driver.SealBoundIntentAtHome(lease, motionOwner, home.view);
        }
    }
}
