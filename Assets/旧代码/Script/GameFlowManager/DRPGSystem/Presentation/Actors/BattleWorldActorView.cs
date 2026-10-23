using UnityEngine;

public sealed class BattleWorldActorView : MonoBehaviour
{
    [Header("World-space presentation anchors")]
    [SerializeField] private Transform actorMotionRoot;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Transform shadowAnchor;
    [SerializeField] private Transform vfxAnchor;
    [SerializeField] private Transform targetAnchor;
    [SerializeField] private Transform statusAnchor;
    [SerializeField] private Transform presentationAnchor;
    [SerializeField] private MonoBehaviour visualDriverComponent;

    private IBattleWorldActorVisualDriver visualDriver;
    private DRPGBattleProjectionLeaseArbiter projectionGuard;
    internal void SetProjectionGuard(DRPGBattleProjectionLeaseArbiter guard) =>
        projectionGuard = guard;

    public int RuntimeId { get; private set; }
    public DRPGBattleParticipantViewModel BoundActor { get; private set; }
    public Transform VisualRoot => visualRoot;
    // Unmigrated Legacy prefabs retain their existing root until the asset pass.
    public Transform MotionRoot => actorMotionRoot != null ? actorMotionRoot : transform;
    public Transform ShadowAnchor => shadowAnchor;
    public Transform VfxAnchor => vfxAnchor;
    public Transform TargetAnchor => targetAnchor;
    public Transform StatusAnchor => statusAnchor;
    public Transform PresentationAnchor => presentationAnchor;

    public void EnsureAnchors()
    {
        visualRoot = EnsureAnchor(visualRoot, "VisualRoot");
        shadowAnchor = EnsureAnchor(shadowAnchor, "ShadowAnchor");
        vfxAnchor = EnsureAnchor(vfxAnchor, "VFXAnchor");
        targetAnchor = EnsureAnchor(targetAnchor, "TargetAnchor");
        statusAnchor = EnsureAnchor(statusAnchor, "StatusAnchor");
        presentationAnchor = EnsureAnchor(
            presentationAnchor,
            "PresentationAnchor");
    }

    public void ConfigureVisualDriver(MonoBehaviour component)
    {
        visualDriverComponent = component;
        visualDriver = component as IBattleWorldActorVisualDriver;
    }

    public void Bind(
        int runtimeId,
        DRPGBattleParticipantViewModel actor = null)
        => BindCanonical(runtimeId, actor, null);

    internal void BindCanonical(int runtimeId, DRPGBattleParticipantViewModel actor,
        BattleCanonicalProjectionPermit permit)
    {
        if (projectionGuard != null &&
            projectionGuard.DeferCanonical("Actor.Bind", RuntimeId, permit)) return;
        bool sameVisual = HasSameVisualBinding(RuntimeId, actor);
        if (sameVisual)
        {
            BoundActor = actor;
            gameObject.SetActive(true);
            return;
        }
        EnsureAnchors();
        ResetMotionRoot();
        RuntimeId = runtimeId;
        BoundActor = actor;
        ResolveVisualDriver();
        visualDriver?.Bind(actor);
        BattleWorldHitFlash.Prepare(this);
        gameObject.SetActive(true);
    }

    private bool HasSameVisualBinding(
        int runtimeId,
        DRPGBattleParticipantViewModel actor)
    {
        return gameObject.activeSelf && RuntimeId == runtimeId &&
            BoundActor != null && actor != null &&
            BoundActor.Profile == actor.Profile &&
            BoundActor.FullPortrait == actor.FullPortrait &&
            BoundActor.AttackCutInPortrait == actor.AttackCutInPortrait &&
            BoundActor.LowHpState == actor.LowHpState &&
            BoundActor.IsAlive == actor.IsAlive;
    }

    public IBattlePresentationHandle PlayIntent(EAnimationLogicState intent)
    {
        if (projectionGuard != null &&
            projectionGuard.DeferCanonical("Actor.LegacyIntent", RuntimeId))
        {
            var rejected = new BattlePresentationHandle();
            rejected.TrySettle(BattlePresentationOutcome.Skipped);
            return rejected;
        }
        ResolveVisualDriver();
        if (visualDriver == null)
        {
            var skipped = new BattlePresentationHandle();
            skipped.TrySettle(BattlePresentationOutcome.Skipped);
            return skipped;
        }

        return visualDriver.PlayIntent(intent);
    }

    public void RestoreVisual()
    {
        if (projectionGuard != null &&
            projectionGuard.DeferCanonical("Actor.RestoreVisual", RuntimeId)) return;
        ResolveVisualDriver();
        visualDriver?.RestoreVisual();
    }

    public void ResetForPool()
        => ResetForPoolCanonical(null);

    internal void ResetForPoolCanonical(BattleCanonicalProjectionPermit permit)
    {
        if (projectionGuard != null &&
            projectionGuard.DeferCanonical("Actor.ResetForPool", RuntimeId, permit)) return;
        ResetForPoolCore();
    }

    internal void ResetForDeparture(BattleDepartureProjectionPermit permit)
    {
        if (projectionGuard?.Active == null || !projectionGuard.Active.OwnsDeparturePermit(permit, this))
            throw new System.InvalidOperationException("Actor departure requires its exact view capability.");
        ResetForPoolCore();
    }

    internal void UnbindBorrowedCanonical(BattleCanonicalProjectionPermit permit)
    {
        if (projectionGuard != null &&
            projectionGuard.DeferCanonical("Actor.UnbindBorrowed", RuntimeId, permit))
        {
            return;
        }
        UnbindBorrowedCore();
    }

    internal void UnbindBorrowedForDeparture(BattleDepartureProjectionPermit permit)
    {
        if (projectionGuard?.Active == null ||
            !projectionGuard.Active.OwnsDeparturePermit(permit, this))
        {
            throw new System.InvalidOperationException(
                "Borrowed departure requires its exact view capability.");
        }
        UnbindBorrowedCore();
    }

    private void UnbindBorrowedCore()
    {
        ResetMotionRoot();
        ResolveVisualDriver();
        visualDriver?.ResetVisual();
        RuntimeId = 0;
        BoundActor = null;
    }

    private void ResetForPoolCore()
    {
        ResetMotionRoot();
        ResolveVisualDriver();
        visualDriver?.ResetVisual();
        RuntimeId = 0;
        BoundActor = null;
        transform.SetParent(transform.parent, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        gameObject.SetActive(false);
    }

    private Transform EnsureAnchor(Transform anchor, string anchorName)
    {
        if (anchor != null)
            return anchor;

        Transform existing = MotionRoot.Find(anchorName);
        if (existing != null)
            return existing;

        var child = new GameObject(anchorName);
        child.transform.SetParent(MotionRoot, false);
        return child.transform;
    }

    private void ResetMotionRoot()
    {
        if (actorMotionRoot == null) return;
        actorMotionRoot.localPosition = Vector3.zero;
        actorMotionRoot.localRotation = Quaternion.identity;
        actorMotionRoot.localScale = Vector3.one;
    }

    private void ResolveVisualDriver()
    {
        if (visualDriver == null)
            visualDriver = visualDriverComponent as IBattleWorldActorVisualDriver;
        if (visualDriver != null)
            return;

        MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IBattleWorldActorVisualDriver driver)
            {
                visualDriverComponent = behaviours[i];
                visualDriver = driver;
                return;
            }
        }
    }
}
