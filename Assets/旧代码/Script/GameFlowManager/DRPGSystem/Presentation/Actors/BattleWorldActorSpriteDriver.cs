using DG.Tweening;
using UnityEngine;

public sealed class BattleWorldActorSpriteDriver : MonoBehaviour,
    IBattleWorldActorVisualDriver
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField, Min(0.1f)] private float targetWorldHeight = 4.2f;
    [SerializeField, Min(0f)] private float anticipationDistance = 0.08f;
    [SerializeField, Min(0f)] private float lungeDistance = 0.62f;
    [SerializeField, Min(0.01f)] private float anticipationSeconds = 0.045f;
    [SerializeField, Min(0.01f)] private float lungeSeconds = 0.13f;
    [SerializeField, Min(0f)] private float impactPoseHoldSeconds = 0.30f;
    [SerializeField] private bool explicitNodeFloorPose;

    private DRPGBattleParticipantViewModel boundActor;
    private Sprite idleSprite;
    private Sprite attackSprite;
    private bool canonicalPoseCaptured;
    private Vector3 canonicalLocalPosition;
    private Vector3 canonicalLocalScale = Vector3.one;
    private Vector3 idleLocalPosition;
    private Vector3 idleLocalScale = Vector3.one;
    private Sequence activeSequence;
    private bool intentPoseActive;
    private ActionIdentity boundIntentOwner;
    private Sprite normalizedBoundSprite;
    private Bounds boundsBeforeBoundPose;

    public void Configure(SpriteRenderer renderer, float worldHeight)
    {
        spriteRenderer = renderer;
        targetWorldHeight = Mathf.Max(0.1f, worldHeight);
    }

    // Persist the unfitted floor baseline, not the already-fitted editor preview.
    // Otherwise a domain reload makes the first battle bind apply the fit twice.
    public void ConfigureNodePreview(SpriteRenderer renderer, float worldHeight)
    {
        Configure(renderer, worldHeight);
        explicitNodeFloorPose = true;
        canonicalPoseCaptured = false;
        CaptureCanonicalPose();
        idleSprite = renderer.sprite;
        ApplyIdleVisual();
    }

    internal System.IDisposable PlayBoundIntent(AnimationPlaybackIdentity identity, EAnimationLogicState intent)
    {
        if (!identity.IsValid || boundActor == null || spriteRenderer == null || intent == EAnimationLogicState.None)
            return null;
        activeSequence?.Kill(false);
        activeSequence = null;
        intentPoseActive = true;
        boundIntentOwner = identity.Action;
        Sprite pose = attackSprite != null ? attackSprite : idleSprite;
        ReleaseNormalizedBoundSprite();
        boundsBeforeBoundPose = spriteRenderer.localBounds;
        if (pose != null && pose != idleSprite)
        {
            // Bake only the private frame mesh into the idle coordinate space.
            // UVs/texture/triangles remain the authored values (including atlases).
            // VisualRoot no longer changes size/pivot to fit a different frame,
            // so returning it cannot inflate the art or drop its feet.
            if (Mathf.Abs(idleLocalScale.x) < .00001f || Mathf.Abs(idleLocalScale.y) < .00001f)
                throw new System.InvalidOperationException("Bound sprite requires a nonzero idle visual scale.");
            normalizedBoundSprite = Instantiate(pose);
            normalizedBoundSprite.hideFlags = HideFlags.HideAndDontSave;
            Vector3 fitted = ResolveFittedScale(pose);
            Vector3 offset = ResolveFloorAlignedPosition(pose, fitted) - idleLocalPosition;
            Vector2[] vertices = pose.vertices;
            var normalized = new Unity.Collections.NativeArray<Vector3>(vertices.Length, Unity.Collections.Allocator.Temp);
            try
            {
                var bounds = new Bounds();
                for (int i = 0; i < vertices.Length; i++)
                {
                    var vertex = new Vector3((vertices[i].x * fitted.x + offset.x) / idleLocalScale.x,
                        (vertices[i].y * fitted.y + offset.y) / idleLocalScale.y, 0);
                    normalized[i] = vertex;
                    if (i == 0) bounds = new Bounds(vertex, Vector3.zero); else bounds.Encapsulate(vertex);
                }
                UnityEngine.U2D.SpriteDataAccessExtensions.SetVertexAttribute(normalizedBoundSprite,
                    UnityEngine.Rendering.VertexAttribute.Position, normalized);
                // This Unity version recalculates UVs when positions change;
                // restore the authored atlas coordinates explicitly afterwards.
                var uv = new Unity.Collections.NativeArray<Vector2>(pose.uv, Unity.Collections.Allocator.Temp);
                try { UnityEngine.U2D.SpriteDataAccessExtensions.SetVertexAttribute(normalizedBoundSprite,
                    UnityEngine.Rendering.VertexAttribute.TexCoord0, uv); }
                finally { uv.Dispose(); }
                spriteRenderer.sprite = normalizedBoundSprite;
                spriteRenderer.localBounds = bounds;
            }
            finally { normalized.Dispose(); }
        }
        else spriteRenderer.sprite = pose;
        spriteRenderer.enabled = pose != null;
        transform.localScale = idleLocalScale;
        transform.localPosition = idleLocalPosition;
        // This immutable token belongs to this frame playback. Sealing it must
        // not return the actor: Phase 8 owns tactical and pose restoration.
        return new BoundSpritePlayback(identity);
    }

    private sealed class BoundSpritePlayback : System.IDisposable
    {
        internal readonly AnimationPlaybackIdentity Identity;
        internal BoundSpritePlayback(AnimationPlaybackIdentity identity) { Identity = identity; }
        public void Dispose() { }
    }

    internal void SealBoundIntentAtHome(DRPGBattleActionProjectionLease lease, System.IDisposable motionOwner,
        BattleWorldActorView view)
    {
        if (!lease.OwnsMotion(motionOwner) || !lease.TryGetView(view.RuntimeId, out var pinned) || pinned != view ||
            !transform.IsChildOf(view.transform) || lease.GetPresented(view.RuntimeId).DefeatPending ||
            (intentPoseActive && boundIntentOwner != lease.Owner.Action))
            throw new System.InvalidOperationException("Only the matching surviving actor's return may seal its bound pose.");
        // The motion owner has written all endpoints. RestoreVisual would add
        // another transform write at this boundary and is deliberately not called.
        intentPoseActive = false; boundIntentOwner = default;
        ReleaseNormalizedBoundSprite();
    }

    internal bool BoundPoseBelongsTo(ActionIdentity action) => !intentPoseActive || boundIntentOwner == action;

    public void Bind(DRPGBattleParticipantViewModel actor)
    {
        EnsureRenderer();
        CaptureCanonicalPose();
        bool sameActor = boundActor != null && actor != null &&
            boundActor.RuntimeId == actor.RuntimeId;

        boundActor = actor;
        idleSprite = actor != null ? actor.FullPortrait : null;
        attackSprite = actor != null ? actor.AttackCutInPortrait : null;
        if (sameActor && intentPoseActive)
        {
            spriteRenderer.sprite = normalizedBoundSprite != null ? normalizedBoundSprite : attackSprite != null
                ? attackSprite
                : idleSprite;
            spriteRenderer.enabled = spriteRenderer.sprite != null;
            return;
        }

        activeSequence?.Kill(false);
        activeSequence = null;
        intentPoseActive = false;
        ReleaseNormalizedBoundSprite();
        ApplyIdleVisual();
    }

    public IBattlePresentationHandle PlayIntent(EAnimationLogicState intent)
    {
        if (boundActor == null || spriteRenderer == null ||
            intent == EAnimationLogicState.None)
        {
            return Settled(BattlePresentationOutcome.Skipped);
        }

        activeSequence?.Kill(false);
        ReleaseNormalizedBoundSprite();
        intentPoseActive = true;
        Sprite intentSprite = attackSprite != null
            ? attackSprite
            : idleSprite;
        spriteRenderer.sprite = intentSprite;
        spriteRenderer.enabled = intentSprite != null;
        Vector3 intentLocalScale = ResolveFittedScale(intentSprite);
        Vector3 intentLocalPosition = ResolveFloorAlignedPosition(
            intentSprite,
            intentLocalScale);
        float direction = boundActor.IsEnemy ? -1f : 1f;
        transform.localPosition = intentLocalPosition;
        transform.localScale = intentLocalScale;

        BattlePresentationHandle handle = null;
        handle = new BattlePresentationHandle(reason =>
        {
            activeSequence?.Kill(false);
            activeSequence = null;
            RestoreVisual();
            handle.TrySettle(BattlePresentationHandle.MapCancellation(reason));
        });

        Vector3 anticipation = intentLocalPosition +
            Vector3.right * (-direction * anticipationDistance);
        Vector3 peak = intentLocalPosition +
            Vector3.right * (direction * lungeDistance);
        activeSequence = DOTween.Sequence()
            .SetUpdate(true)
            .Append(transform.DOLocalMove(
                    anticipation,
                    anticipationSeconds)
                .SetEase(Ease.OutQuad))
            .Join(transform.DOScale(
                    Vector3.Scale(intentLocalScale, new Vector3(0.97f, 1.025f, 1f)),
                    anticipationSeconds)
                .SetEase(Ease.OutQuad))
            .Append(transform.DOLocalMove(peak, lungeSeconds)
                .SetEase(Ease.OutExpo))
            .Join(transform.DOScale(
                    Vector3.Scale(intentLocalScale, new Vector3(1.045f, 0.98f, 1f)),
                    lungeSeconds)
                .SetEase(Ease.OutBack))
            .AppendInterval(impactPoseHoldSeconds)
            .OnComplete(() =>
            {
                activeSequence = null;
                handle.TrySettle(BattlePresentationOutcome.Completed);
            });
        return handle;
    }

    public void RestoreVisual()
    {
        activeSequence?.Kill(false);
        activeSequence = null;
        intentPoseActive = false;
        boundIntentOwner = default;
        ReleaseNormalizedBoundSprite();
        transform.localPosition = idleLocalPosition;
        transform.localScale = idleLocalScale;
        if (spriteRenderer == null)
            return;
        spriteRenderer.sprite = idleSprite;
        spriteRenderer.enabled = idleSprite != null;
    }

    public void ResetVisual()
    {
        activeSequence?.Kill(false);
        activeSequence = null;
        intentPoseActive = false;
        boundIntentOwner = default;
        ReleaseNormalizedBoundSprite();
        boundActor = null;
        idleSprite = null;
        attackSprite = null;
        transform.localPosition = canonicalPoseCaptured
            ? canonicalLocalPosition
            : Vector3.zero;
        transform.localScale = canonicalPoseCaptured
            ? canonicalLocalScale
            : Vector3.one;
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = null;
            spriteRenderer.enabled = false;
        }
    }

    private static IBattlePresentationHandle Settled(
        BattlePresentationOutcome outcome)
    {
        var handle = new BattlePresentationHandle();
        handle.TrySettle(outcome);
        return handle;
    }

    private void ReleaseNormalizedBoundSprite()
    {
        if (normalizedBoundSprite == null) return;
        var owned = normalizedBoundSprite; normalizedBoundSprite = null;
        if (spriteRenderer != null) spriteRenderer.localBounds = boundsBeforeBoundPose;
        if (Application.isPlaying) Destroy(owned); else DestroyImmediate(owned);
    }
    private void OnDestroy() => ReleaseNormalizedBoundSprite();

    private void EnsureRenderer()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
    }

    private void CaptureCanonicalPose()
    {
        if (canonicalPoseCaptured)
            return;
        canonicalPoseCaptured = true;
        canonicalLocalPosition = explicitNodeFloorPose ? Vector3.zero : transform.localPosition;
        canonicalLocalScale = explicitNodeFloorPose ? Vector3.one : transform.localScale;
    }

    private void ApplyIdleVisual()
    {
        spriteRenderer.sprite = idleSprite;
        spriteRenderer.enabled = spriteRenderer.sprite != null;
        idleLocalScale = ResolveFittedScale(idleSprite);
        idleLocalPosition = ResolveFloorAlignedPosition(
            idleSprite,
            idleLocalScale);
        transform.localPosition = idleLocalPosition;
        transform.localScale = idleLocalScale;
    }

    private Vector3 ResolveFittedScale(Sprite sprite)
    {
        if (sprite == null)
            return canonicalLocalScale;
        float height = sprite.bounds.size.y;
        float scale = height > 0.001f ? targetWorldHeight / height : 1f;
        return Vector3.Scale(
            canonicalLocalScale,
            Vector3.one * scale);
    }

    private Vector3 ResolveFloorAlignedPosition(
        Sprite sprite,
        Vector3 resolvedScale)
    {
        Vector3 position = canonicalLocalPosition;
        if (sprite == null)
            return position;

        // canonicalLocalPosition is the authored floor anchor.  Source art may
        // use a bottom, center or custom pivot, so compensate by the sprite's
        // scaled lower bound whenever a frame is swapped.
        position.y -= sprite.bounds.min.y * resolvedScale.y;
        return position;
    }
}
