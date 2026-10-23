using System;
using System.Collections.Generic;
using UnityEngine;

internal enum DRPGBattleProjectionCheckpointKind
{
    LockBaseline, ConflictCheckpoint, PostReturnImpactReconcileCheckpoint,
    PostSettlementReconcileCheckpoint
}

// Checkpoints contain values, never live participants, view models or Transforms.
internal readonly struct BattleProjectionPose
{
    internal BattleProjectionPose(Transform source) : this()
    {
        InstanceId = source.GetInstanceID();
        Position = source.position;
        Rotation = source.rotation;
        LocalPosition = source.localPosition;
        LocalRotation = source.localRotation;
        LocalScale = source.localScale;
        ActiveSelf = source.gameObject.activeSelf;
        if (source is RectTransform rect)
        {
            IsRect = true;
            AnchorMin = rect.anchorMin; AnchorMax = rect.anchorMax;
            Pivot = rect.pivot; SizeDelta = rect.sizeDelta;
            AnchoredPosition = rect.anchoredPosition3D;
        }
    }
    internal int InstanceId { get; }
    internal Vector3 Position { get; }
    internal Quaternion Rotation { get; }
    internal Vector3 LocalPosition { get; }
    internal Quaternion LocalRotation { get; }
    internal Vector3 LocalScale { get; }
    internal bool ActiveSelf { get; }
    internal bool IsRect { get; }
    internal Vector2 AnchorMin { get; }
    internal Vector2 AnchorMax { get; }
    internal Vector2 Pivot { get; }
    internal Vector2 SizeDelta { get; }
    internal Vector3 AnchoredPosition { get; }
}

internal readonly struct BattleProjectionCanvasGroup
{
    internal BattleProjectionCanvasGroup(CanvasGroup source)
    {
        InstanceId = source.GetInstanceID(); Alpha = source.alpha;
        Interactable = source.interactable; BlocksRaycasts = source.blocksRaycasts;
        IgnoreParentGroups = source.ignoreParentGroups; Enabled = source.enabled;
    }
    internal int InstanceId { get; }
    internal float Alpha { get; }
    internal bool Interactable { get; }
    internal bool BlocksRaycasts { get; }
    internal bool IgnoreParentGroups { get; }
    internal bool Enabled { get; }
}

internal readonly struct BattleProjectionRenderer
{
    internal BattleProjectionRenderer(Renderer source)
    {
        InstanceId = source.GetInstanceID(); Enabled = source.enabled;
        SortingLayerId = source.sortingLayerID; SortingOrder = source.sortingOrder;
    }
    internal int InstanceId { get; }
    internal bool Enabled { get; }
    internal int SortingLayerId { get; }
    internal int SortingOrder { get; }
}

internal readonly struct BattleProjectionGraphic
{
    internal BattleProjectionGraphic(int instanceId, Color color, bool enabled,
        int spriteId = 0, int sortingOrder = 0)
    {
        InstanceId = instanceId; Color = color; Enabled = enabled;
        SpriteId = spriteId; SortingOrder = sortingOrder;
    }
    internal int InstanceId { get; }
    internal Color Color { get; }
    internal bool Enabled { get; }
    internal int SpriteId { get; }
    internal int SortingOrder { get; }
}

internal readonly struct BattleProjectedParticipant
{
    internal BattleProjectedParticipant(int runtimeId, float hp, float maxHp)
        : this()
    {
        if (runtimeId <= 0 || float.IsNaN(hp) || float.IsInfinity(hp) ||
            float.IsNaN(maxHp) || float.IsInfinity(maxHp) || maxHp <= 0f ||
            hp < 0f || hp > maxHp)
            throw new ArgumentException("Invalid projected participant values.");
        RuntimeId = runtimeId; Hp = hp; MaxHp = maxHp;
    }
    internal BattleProjectedParticipant(int runtimeId, float hp, float maxHp,
        float posture, float maxPosture, DRPGStatusCollectionSnapshot statuses)
        : this(runtimeId, hp, maxHp)
    {
        if (float.IsNaN(posture) || float.IsInfinity(posture) ||
            float.IsNaN(maxPosture) || float.IsInfinity(maxPosture) ||
            maxPosture < 0f || posture < 0f ||
            statuses == null || statuses.OwnerParticipantId.Value != runtimeId)
            throw new ArgumentException("Invalid projected settlement values or status owner.");
        Posture = posture; MaxPosture = maxPosture; Statuses = statuses;
    }
    internal int RuntimeId { get; }
    internal float Hp { get; }
    internal float MaxHp { get; }
    internal bool DefeatPending => Hp <= 0f;
    internal float Posture { get; }
    internal float MaxPosture { get; }
    internal DRPGStatusCollectionSnapshot Statuses { get; }
    internal bool HasSettlementState => Statuses != null;

    internal BattleProjectedParticipant WithSettlement(float hp, float posture, DRPGStatusCollectionSnapshot statuses) =>
        new BattleProjectedParticipant(RuntimeId, hp, MaxHp, posture, MaxPosture, statuses);

    // The existing Stage7 commands are HP-only patches, not requests to clear
    // unrelated status metadata. Full semantic commands must compare all fields.
    internal BattleProjectedParticipant WithHp(float hp, float maxHp) => HasSettlementState
        ? new BattleProjectedParticipant(RuntimeId, hp, maxHp, Posture, MaxPosture, Statuses)
        : new BattleProjectedParticipant(RuntimeId, hp, maxHp);

    internal bool MatchesBefore(BattleProjectedParticipant before) => RuntimeId == before.RuntimeId &&
        Hp == before.Hp && MaxHp == before.MaxHp && (!before.HasSettlementState ||
        HasSettlementState && Posture == before.Posture && MaxPosture == before.MaxPosture &&
        DRPGActionStatusChange.SameSnapshots(Statuses, before.Statuses));
}

internal sealed class BattleLeasedActorCheckpoint
{
    internal BattleLeasedActorCheckpoint(BattleWorldActorView view,
        BattleProjectionPose home, BattleProjectedParticipant presented)
    {
        RuntimeId = view.RuntimeId;
        ViewInstanceId = view.GetInstanceID();
        Home = home;
        Motion = new BattleProjectionPose(view.MotionRoot);
        Visual = new BattleProjectionPose(view.VisualRoot);
        Presented = presented;
        var poses = new List<BattleProjectionPose>();
        foreach (Transform item in view.GetComponentsInChildren<Transform>(true))
            poses.Add(new BattleProjectionPose(item));
        Poses = poses.AsReadOnly();
        var graphics = new List<BattleProjectionGraphic>();
        foreach (SpriteRenderer item in view.GetComponentsInChildren<SpriteRenderer>(true))
            graphics.Add(new BattleProjectionGraphic(item.GetInstanceID(),
                item.color, item.enabled, item.sprite != null ? item.sprite.GetInstanceID() : 0,
                item.sortingOrder));
        Graphics = graphics.AsReadOnly();
    }
    internal int RuntimeId { get; }
    internal int ViewInstanceId { get; }
    internal BattleProjectionPose Home { get; }
    internal BattleProjectionPose Motion { get; }
    internal BattleProjectionPose Visual { get; }
    internal BattleProjectedParticipant Presented { get; }
    internal IReadOnlyList<BattleProjectionPose> Poses { get; }
    internal IReadOnlyList<BattleProjectionGraphic> Graphics { get; }
}

internal sealed class DRPGBattleProjectionCheckpoint
{
    internal DRPGBattleProjectionCheckpoint(BattleTransactionIdentity owner,
        DRPGBattleProjectionCheckpointKind kind, long version,
        IEnumerable<BattleLeasedActorCheckpoint> actors, Transform uiRoot,
        Transform worldRoot = null, Camera camera = null)
    {
        Owner = owner; Kind = kind; Version = version;
        Actors = new List<BattleLeasedActorCheckpoint>(actors).AsReadOnly();
        var poses = new List<BattleProjectionPose>();
        var graphics = new List<BattleProjectionGraphic>();
        if (uiRoot != null)
        {
            foreach (RectTransform item in uiRoot.GetComponentsInChildren<RectTransform>(true))
                poses.Add(new BattleProjectionPose(item));
            foreach (UnityEngine.UI.Graphic item in uiRoot.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                graphics.Add(new BattleProjectionGraphic(item.GetInstanceID(), item.color,
                    item.enabled, item is UnityEngine.UI.Image img && img.sprite != null
                        ? img.sprite.GetInstanceID() : 0));
        }
        UiPoses = poses.AsReadOnly();
        UiGraphics = graphics.AsReadOnly();
        var groups = new List<BattleProjectionCanvasGroup>();
        if (uiRoot != null)
            foreach (var item in uiRoot.GetComponentsInChildren<CanvasGroup>(true))
                groups.Add(new BattleProjectionCanvasGroup(item));
        CanvasGroups = groups.AsReadOnly();
        var renderers = new List<BattleProjectionRenderer>();
        var worldPoses = new List<BattleProjectionPose>();
        var worldGraphics = new List<BattleProjectionGraphic>();
        if (worldRoot != null)
        {
            foreach (var item in worldRoot.GetComponentsInChildren<Transform>(true))
                worldPoses.Add(new BattleProjectionPose(item));
            foreach (var item in worldRoot.GetComponentsInChildren<Renderer>(true))
                renderers.Add(new BattleProjectionRenderer(item));
            foreach (var item in worldRoot.GetComponentsInChildren<SpriteRenderer>(true))
                worldGraphics.Add(new BattleProjectionGraphic(item.GetInstanceID(), item.color,
                    item.enabled, item.sprite != null ? item.sprite.GetInstanceID() : 0, item.sortingOrder));
        }
        WorldRenderers = renderers.AsReadOnly();
        WorldPoses = worldPoses.AsReadOnly();
        WorldGraphics = worldGraphics.AsReadOnly();
        if (camera != null)
        {
            CameraInstanceId = camera.GetInstanceID();
            Camera = new BattleCameraStateSnapshot(camera.transform.position, camera.transform.rotation,
                camera.fieldOfView, camera.orthographic, camera.orthographicSize, camera.enabled);
        }
    }
    internal BattleTransactionIdentity Owner { get; }
    internal DRPGBattleProjectionCheckpointKind Kind { get; }
    internal long Version { get; }
    internal IReadOnlyList<BattleLeasedActorCheckpoint> Actors { get; }
    internal IReadOnlyList<BattleProjectionPose> UiPoses { get; }
    internal IReadOnlyList<BattleProjectionGraphic> UiGraphics { get; }
    internal IReadOnlyList<BattleProjectionCanvasGroup> CanvasGroups { get; }
    internal IReadOnlyList<BattleProjectionRenderer> WorldRenderers { get; }
    internal IReadOnlyList<BattleProjectionPose> WorldPoses { get; }
    internal IReadOnlyList<BattleProjectionGraphic> WorldGraphics { get; }
    internal int CameraInstanceId { get; }
    internal BattleCameraStateSnapshot Camera { get; }
}
