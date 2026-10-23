using System;
using System.Collections.Generic;
using UnityEngine;

// Action-owned close-up geometry and visibility. MotionRoot remains exclusively
// owned by BattleActorMotionController; this scope only touches VisualRoot.
internal sealed class BattleCloseupScope : IDisposable
{
    private readonly struct VisualBaseline
    {
        internal VisualBaseline(BattleWorldActorView view)
        {
            View = view;
            Root = view != null ? view.VisualRoot : null;
            LocalPosition = Root != null ? Root.localPosition : default;
            LocalScale = Root != null ? Root.localScale : default;
        }
        internal BattleWorldActorView View { get; }
        internal Transform Root { get; }
        internal Vector3 LocalPosition { get; }
        internal Vector3 LocalScale { get; }
    }

    private readonly ActionIdentity owner;
    private readonly DRPGBattleActionProjectionLease lease;
    private readonly Camera camera;
    private readonly BattleCloseupProfile profile;
    private readonly BattleCloseupParticipants participants;
    private readonly BattleCloseupFloatingUiGate floatingUi;
    private readonly Dictionary<int, VisualBaseline> visuals = new Dictionary<int, VisualBaseline>();
    private readonly Dictionary<int, BattleWorldActorView> views = new Dictionary<int, BattleWorldActorView>();
    private readonly Dictionary<SpriteRenderer, Color> spriteColors = new Dictionary<SpriteRenderer, Color>();
    private readonly Dictionary<int, DefeatReturn> defeatReturns = new Dictionary<int, DefeatReturn>();
    private readonly float homeFov;
    private readonly float homeOrthographicSize;
    private bool entered;
    private bool returning;
    private bool disposed;
    private BattleCloseupRenderLayer renderLayer;

    private readonly struct DefeatReturn
    {
        internal DefeatReturn(Transform root, Vector3 position, Vector3 scale,
            Vector3 endPosition, Vector3 endScale)
        { Root = root; Position = position; Scale = scale; EndPosition = endPosition; EndScale = endScale; }
        internal Transform Root { get; }
        internal Vector3 Position { get; }
        internal Vector3 Scale { get; }
        internal Vector3 EndPosition { get; }
        internal Vector3 EndScale { get; }
    }

    internal BattleCloseupScope(DRPGBasicAttackFrozenSnapshot snapshot,
        DRPGBattleActionProjectionLease lease, Camera camera,
        IReadOnlyList<CanvasGroup> floatingGroups, BattleCloseupProfile profile)
    {
        if (!snapshot.Identity.IsValid) throw new ArgumentException("A frozen action is required.", nameof(snapshot));
        this.lease = lease ?? throw new ArgumentNullException(nameof(lease));
        if (!lease.IsActive || lease.Owner.Action != snapshot.Identity)
            throw new InvalidOperationException("Close-up requires the matching active projection lease.");
        this.camera = camera ?? throw new ArgumentNullException(nameof(camera));
        this.profile = profile;
        owner = snapshot.Identity;
        participants = new BattleCloseupParticipants(owner, snapshot.Turn.Actor, snapshot.Target);
        floatingUi = new BattleCloseupFloatingUiGate(owner, floatingGroups);
        homeFov = camera.fieldOfView;
        homeOrthographicSize = camera.orthographicSize;
        foreach (var pin in lease.LockBaseline.Actors)
        {
            if (!lease.TryGetView(pin.RuntimeId, out BattleWorldActorView view) || view == null) continue;
            views[pin.RuntimeId] = view;
            visuals[pin.RuntimeId] = new VisualBaseline(view);
            foreach (SpriteRenderer renderer in view.GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer != null && !spriteColors.ContainsKey(renderer)) spriteColors.Add(renderer, renderer.color);
        }
        if (!lease.TryOwnPresentation(this)) throw new InvalidOperationException("Close-up projection ownership is unavailable.");
    }

    internal void Enter(ActionIdentity action)
    {
        if (disposed || entered || action != owner || !lease.IsActive) return;
        entered = true;
        floatingUi.Hide(action);
        if (camera.orthographic)
            camera.orthographicSize = BattleCloseupGeometry.OrthographicSize(homeOrthographicSize, profile.CameraMagnification);
        else
            camera.fieldOfView = BattleCloseupGeometry.PerspectiveFov(homeFov, profile.CameraMagnification);
        ApplyDepthComposition();
        renderLayer = new BattleCloseupRenderLayer(camera, views, participants.Ordered, profile);
    }

    internal bool IncludeReaction(ActionIdentity action, ParticipantId participant)
    {
        if (disposed || !entered || returning || !participants.IncludeReaction(action, participant)) return false;
        ApplyParticipant(participant.Value, true, true);
        renderLayer?.IncludeParticipant(participant.Value);
        return true;
    }

    internal void BeginReturn(ActionIdentity action)
    {
        if (disposed || returning || action != owner) return;
        returning = true;
        floatingUi.RestoreAtReturnStart(action);
        foreach (ParticipantId participant in participants.Ordered)
        {
            if (!visuals.TryGetValue(participant.Value, out VisualBaseline baseline) || baseline.Root == null ||
                !lease.GetPresented(participant.Value).DefeatPending) continue;
            Vector3 originPosition = baseline.Root.localPosition;
            Vector3 originScale = baseline.Root.localScale;
            Vector3 closeupOffset = CloseupLocalOffset(baseline);
            Vector3 endScale = new Vector3(originScale.x / profile.ParticipantScale,
                originScale.y / profile.ParticipantScale, originScale.z / profile.ParticipantScale);
            defeatReturns[participant.Value] = new DefeatReturn(baseline.Root, originPosition, originScale,
                originPosition - closeupOffset, endScale);
        }
    }

    internal void Tick()
    {
        if (entered && !returning)
            for (int i = 0; i < participants.Ordered.Count; i++)
            {
                int runtimeId = participants.Ordered[i].Value;
                bool preserveDefeatPose = lease.GetPresented(runtimeId).DefeatPending;
                ApplyParticipant(runtimeId, !preserveDefeatPose, false);
            }
        renderLayer?.Tick();
    }

    internal void ApplyReturn(float progress)
    {
        progress = Mathf.Clamp01(progress);
        foreach (DefeatReturn item in defeatReturns.Values)
        {
            if (item.Root == null) continue;
            item.Root.localPosition = progress >= 1f ? item.EndPosition :
                Vector3.LerpUnclamped(item.Position, item.EndPosition, progress);
            item.Root.localScale = progress >= 1f ? item.EndScale :
                Vector3.LerpUnclamped(item.Scale, item.EndScale, progress);
        }
        renderLayer?.ApplyReturn(progress);
    }

    private void ApplyDepthComposition()
    {
        foreach (var pair in visuals)
        {
            if (participants.Contains(new ParticipantId(pair.Key))) ApplyParticipant(pair.Key, true, true);
            else ApplySpectator(pair.Key);
        }
    }

    private void ApplyParticipant(int runtimeId, bool restorePosition, bool restoreColor)
    {
        if (!visuals.TryGetValue(runtimeId, out VisualBaseline baseline) || baseline.Root == null) return;
        if (restorePosition) baseline.Root.localPosition = baseline.LocalPosition + CloseupLocalOffset(baseline);
        baseline.Root.localScale = Vector3.Scale(baseline.LocalScale, Vector3.one * profile.ParticipantScale);
        if (restoreColor)
            foreach (SpriteRenderer renderer in baseline.View.GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer != null && spriteColors.TryGetValue(renderer, out Color color)) renderer.color = color;
    }

    private Vector3 CloseupLocalOffset(VisualBaseline baseline)
    {
        float viewportHeight = camera.orthographic
            ? homeOrthographicSize * 2f
            : Mathf.Tan(homeFov * .5f * Mathf.Deg2Rad) * 2f *
              Mathf.Max(camera.nearClipPlane, Vector3.Dot(baseline.Root.position - camera.transform.position, camera.transform.forward));
        float downward = BattleCloseupGeometry.DownwardWorldDistance(viewportHeight, .70f, profile.DownwardBattleFraction);
        Vector3 worldStep = -camera.transform.up * downward;
        return baseline.Root.parent != null ? baseline.Root.parent.InverseTransformVector(worldStep) : worldStep;
    }

    private void ApplySpectator(int runtimeId)
    {
        if (!visuals.TryGetValue(runtimeId, out VisualBaseline baseline) || baseline.View == null) return;
        foreach (SpriteRenderer renderer in baseline.View.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer == null || !spriteColors.TryGetValue(renderer, out Color color)) continue;
            renderer.color = new Color(color.r * profile.SpectatorDim, color.g * profile.SpectatorDim,
                color.b * profile.SpectatorDim, color.a);
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        floatingUi.Dispose();
        renderLayer?.Dispose();
        renderLayer = null;
    }
}
