using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public sealed class BattleConflictPresentationContext : MonoBehaviour
{
    [SerializeField] private BattleHudPresenter hud;
    [SerializeField] private BattleCameraPresenter cameraPresenter;
    [SerializeField] private BattleStagePresenter stagePresenter;
    [SerializeField] private BattleWorldActorPool actorPool;
    [SerializeField] private BattleBackgroundEffectPresenter backgroundPresenter;

    private readonly Dictionary<BattlePresentationScope, BattleConflictPresentationScope> scopes =
        new Dictionary<BattlePresentationScope, BattleConflictPresentationScope>();
    private readonly Dictionary<BattlePresentationScope, List<ActorState>> actorStates =
        new Dictionary<BattlePresentationScope, List<ActorState>>();

    public int ActiveScopeCount => scopes.Count;
    public BattleStageProfileSO ActiveStage =>
        stagePresenter != null ? stagePresenter.ActiveProfile : null;

    public void Configure(
        BattleHudPresenter hudPresenter,
        BattleCameraPresenter battleCamera,
        BattleStagePresenter battleStage,
        BattleWorldActorPool worldActors,
        BattleBackgroundEffectPresenter background)
    {
        hud = hudPresenter;
        cameraPresenter = battleCamera;
        stagePresenter = battleStage;
        actorPool = worldActors;
        backgroundPresenter = background;
    }

    public BattleConflictPresentationScope Ensure(BattlePresentationRequest request)
    {
        if (request == null || hud == null || cameraPresenter == null ||
            stagePresenter == null)
        {
            return null;
        }
        if (scopes.TryGetValue(request.Scope, out BattleConflictPresentationScope scope))
            return scope;

        scope = new BattleConflictPresentationScope(
            hud,
            cameraPresenter,
            stagePresenter);
        scope.Begin();
        scopes.Add(request.Scope, scope);
        backgroundPresenter?.Capture(request.Scope);
        return scope;
    }

    public void ApplyHud(
        BattlePresentationRequest request,
        BattleHudVisibilityPolicy policy)
    {
        if (Ensure(request) != null)
            hud.Apply(policy);
    }

    public void SwitchStage(
        BattlePresentationRequest request,
        BattleStageProfileSO profile)
    {
        if (profile != null && Ensure(request) != null)
            stagePresenter.SwitchTemporary(profile);
    }

    public void PlayCamera(
        BattlePresentationRequest request,
        BattleCameraShotDefinition shot)
    {
        if (shot != null && Ensure(request) != null)
            cameraPresenter.Play(shot);
    }

    public void PlayCamera(
        BattlePresentationRequest request,
        BattleCameraShotDefinition shot,
        BattleCameraFramingMode framingMode,
        float horizontalPadding,
        float verticalPadding,
        float desiredFieldOfView,
        float transitionSeconds)
    {
        if (shot == null || Ensure(request) == null)
            return;

        if (framingMode != BattleCameraFramingMode.FrameActorAndTargets)
        {
            cameraPresenter.Play(new BattleCameraShotDefinition(
                shot.ShotId,
                shot.LocalPosition,
                shot.LocalEulerAngles,
                desiredFieldOfView,
                transitionSeconds));
            return;
        }

        var focusPoints = new List<Vector3>();
        var runtimeIds = new HashSet<int>();
        AddFocusPoint(request.Actor, runtimeIds, focusPoints);
        for (int i = 0; i < request.Targets.Count; i++)
            AddFocusPoint(request.Targets[i], runtimeIds, focusPoints);

        Vector3 authoredFocus = Vector3.zero;
        if (ActiveStage != null && ActiveStage.TryGetAnchor(
                BattleStageAnchorIds.PresentationCenter,
                out BattleStageAnchorDefinition anchor))
        {
            authoredFocus = anchor.LocalPosition;
        }

        BattleCameraShotDefinition framed =
            BattleCameraFramingCalculator.CreateFramedShot(
                shot,
                authoredFocus,
                focusPoints,
                cameraPresenter.Aspect,
                horizontalPadding,
                verticalPadding,
                desiredFieldOfView,
                transitionSeconds);
        cameraPresenter.Play(framed);
    }

    public void ApplyActors(
        BattlePresentationRequest request,
        float attackerScale,
        float targetScale,
        float targetRetreat,
        BattleOtherTargetPresentationPolicy others)
    {
        if (actorPool != null && actorPool.RejectForeignProjectionWrite("LegacyConflict.ApplyActors")) return;
        if (actorPool == null || Ensure(request) == null)
            return;

        if (!actorStates.ContainsKey(request.Scope))
            actorStates.Add(request.Scope, CaptureActorStates());
        var targetIds = new HashSet<int>();
        for (int i = 0; i < request.Targets.Count; i++)
            targetIds.Add(request.Targets[i].RuntimeId);

        for (int i = 0; i < actorStates[request.Scope].Count; i++)
        {
            ActorState state = actorStates[request.Scope][i];
            bool attacker = request.Actor != null &&
                state.View.RuntimeId == request.Actor.RuntimeId;
            bool target = targetIds.Contains(state.View.RuntimeId);
            if (attacker)
                state.View.transform.localScale = state.Scale * Mathf.Max(0.01f, attackerScale);
            else if (target)
            {
                state.View.transform.localScale = state.Scale * Mathf.Max(0.01f, targetScale);
                state.View.transform.localPosition = state.Position +
                    ResolveRetreatOffset(request, state.View, targetRetreat);
            }
            else
            {
                ApplyOtherPolicy(state.View, others);
            }
        }
    }

    public void ApplyAttacker(
        BattlePresentationRequest request,
        bool visible,
        float scale)
    {
        if (actorPool != null && actorPool.RejectForeignProjectionWrite("LegacyConflict.ApplyAttacker")) return;
        if (actorPool == null || Ensure(request) == null ||
            request.Actor == null ||
            !actorPool.TryGet(request.Actor.RuntimeId, out BattleWorldActorView view))
        {
            return;
        }
        EnsureActorStates(request.Scope);
        ActorState state = FindState(request.Scope, view.RuntimeId);
        if (state == null)
            return;
        view.gameObject.SetActive(visible);
        view.transform.localScale = state.Scale * Mathf.Max(0.01f, scale);
    }

    public void ApplyTargets(
        BattlePresentationRequest request,
        bool focus,
        float scale,
        float retreat,
        BattleOtherTargetPresentationPolicy others)
    {
        if (actorPool != null && actorPool.RejectForeignProjectionWrite("LegacyConflict.ApplyTargets")) return;
        if (actorPool == null || Ensure(request) == null)
            return;
        EnsureActorStates(request.Scope);
        var targetIds = new HashSet<int>();
        for (int i = 0; i < request.Targets.Count; i++)
            targetIds.Add(request.Targets[i].RuntimeId);

        List<ActorState> states = actorStates[request.Scope];
        for (int i = 0; i < states.Count; i++)
        {
            ActorState state = states[i];
            bool isAttacker = request.Actor != null &&
                state.View.RuntimeId == request.Actor.RuntimeId;
            if (targetIds.Contains(state.View.RuntimeId))
            {
                if (focus)
                {
                    state.View.transform.localScale =
                        state.Scale * Mathf.Max(0.01f, scale);
                    state.View.transform.localPosition =
                        state.Position +
                        ResolveRetreatOffset(request, state.View, retreat);
                }
            }
            else if (!isAttacker)
            {
                ApplyOtherPolicy(state.View, others);
            }
        }
    }

    public IBattlePresentationHandle PlayActorIntent(
        BattlePresentationRequest request)
    {
        if (request == null || request.Actor == null || actorPool == null ||
            !actorPool.TryGet(request.Actor.RuntimeId, out BattleWorldActorView actor))
        {
            var skipped = new BattlePresentationHandle();
            skipped.TrySettle(BattlePresentationOutcome.Skipped);
            return skipped;
        }
        Ensure(request);
        return actor.PlayIntent(request.AnimationIntent);
    }

    public void ApplyBackground(
        BattlePresentationRequest request,
        BattleBackgroundPresentationPolicy policy,
        float intensity)
    {
        if (Ensure(request) != null)
            backgroundPresenter?.Apply(policy, intensity);
    }

    public IBattlePresentationHandle PlayImpactFeedback(
        BattlePresentationRequest request,
        BattleFeedbackPresentationDefinition definition)
    {
        if (actorPool != null && actorPool.RejectForeignProjectionWrite("LegacyConflict.ImpactFeedback"))
            return Settled(BattlePresentationOutcome.Skipped);
        if (request == null || definition == null || Ensure(request) == null)
            return Settled(BattlePresentationOutcome.Skipped);

        var targets = new List<ImpactTargetState>();
        if (actorPool != null)
        {
            for (int i = 0; i < request.Targets.Count; i++)
            {
                BattleParticipantPresentationSnapshot target =
                    request.Targets[i];
                if (target != null && actorPool.TryGet(
                        target.RuntimeId,
                        out BattleWorldActorView view) &&
                    view != null)
                {
                    targets.Add(new ImpactTargetState(view));
                }
            }
        }

        Sequence sequence = null;
        BattlePresentationHandle handle = null;
        bool completed = false;
        handle = new BattlePresentationHandle(reason =>
        {
            if (sequence != null && sequence.IsActive())
                sequence.Kill(false);
            RestoreImpactTargets(targets);
            handle.TrySettle(BattlePresentationHandle.MapCancellation(reason));
        });

        sequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        const float recoilOutSeconds = 0.065f;
        const float recoilReturnSeconds = 0.115f;
        float reactionSeconds = recoilOutSeconds + recoilReturnSeconds;

        if (targets.Count > 0)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                ImpactTargetState state = targets[i];
                Vector3 recoil = state.LocalPosition + ResolveRetreatOffset(
                    request,
                    state.View,
                    definition.TargetRecoilDistance);
                Sequence reaction = DOTween.Sequence()
                    .Append(state.View.transform.DOLocalMove(
                            recoil,
                            recoilOutSeconds)
                        .SetEase(Ease.OutCubic))
                    .Append(state.View.transform.DOLocalMove(
                            state.LocalPosition,
                            recoilReturnSeconds)
                        .SetEase(Ease.InOutSine));
                if (definition.FlashTarget)
                    state.AddFlash(reaction);

                if (i == 0)
                    sequence.Append(reaction);
                else
                    sequence.Join(reaction);
            }
        }

        float remainingHold = Mathf.Max(
            0f,
            definition.HoldSeconds -
            (targets.Count > 0 ? reactionSeconds : 0f));
        if (remainingHold > 0f)
            sequence.AppendInterval(remainingHold);
        sequence.OnComplete(() =>
            {
                completed = true;
                RestoreImpactTargets(targets);
                handle.TrySettle(BattlePresentationOutcome.Completed);
            })
            .OnKill(() =>
            {
                if (completed || handle.IsSettled)
                    return;
                RestoreImpactTargets(targets);
                handle.TrySettle(BattlePresentationOutcome.Cancelled);
            });
        return handle;
    }

    public void Restore(BattlePresentationRequest request)
    {
        if (request == null)
            return;
        Restore(request.Scope);
    }

    public IBattlePresentationHandle PlayRestore(
        BattlePresentationRequest request,
        BattleExitPresentationDefinition definition)
    {
        if (actorPool != null && actorPool.RejectForeignProjectionWrite("LegacyConflict.PlayRestore"))
            return Settled(BattlePresentationOutcome.Skipped);
        if (request == null)
            return Settled(BattlePresentationOutcome.Skipped);

        const float readableRestoreFloor = 0.30f;
        float seconds = Mathf.Max(
            readableRestoreFloor,
            definition != null ? definition.RestoreSeconds : 0f);
        actorStates.TryGetValue(
            request.Scope,
            out List<ActorState> states);

        Sequence sequence = null;
        BattlePresentationHandle handle = null;
        bool completed = false;
        handle = new BattlePresentationHandle(reason =>
        {
            if (sequence != null && sequence.IsActive())
                sequence.Kill(false);
            Restore(request.Scope);
            handle.TrySettle(BattlePresentationHandle.MapCancellation(reason));
        });

        sequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        bool hasActorMotion = false;
        if (states != null)
        {
            for (int i = 0; i < states.Count; i++)
            {
                ActorState state = states[i];
                if (state?.View == null)
                    continue;
                Tween move = state.View.transform.DOLocalMove(
                        state.Position,
                        seconds)
                    .SetEase(Ease.InOutCubic);
                Tween scale = state.View.transform.DOScale(
                        state.Scale,
                        seconds)
                    .SetEase(Ease.OutCubic);
                if (!hasActorMotion)
                {
                    sequence.Append(move);
                    sequence.Join(scale);
                    hasActorMotion = true;
                }
                else
                {
                    sequence.Join(move);
                    sequence.Join(scale);
                }
                state.AppendRestoreTweens(sequence, seconds);
            }
        }
        if (!hasActorMotion)
            sequence.AppendInterval(seconds);

        sequence.OnComplete(() =>
            {
                completed = true;
                Restore(request.Scope);
                handle.TrySettle(BattlePresentationOutcome.Completed);
            })
            .OnKill(() =>
            {
                if (completed || handle.IsSettled)
                    return;
                Restore(request.Scope);
                handle.TrySettle(BattlePresentationOutcome.Cancelled);
            });
        return handle;
    }

    private Vector3 ResolveRetreatOffset(
        BattlePresentationRequest request,
        BattleWorldActorView target,
        float distance)
    {
        if (Mathf.Approximately(distance, 0f) || target == null)
            return Vector3.zero;

        float direction = target.BoundActor != null &&
            target.BoundActor.IsEnemy
                ? 1f
                : -1f;
        if (request?.Actor != null && actorPool != null &&
            actorPool.TryGet(request.Actor.RuntimeId, out BattleWorldActorView attacker))
        {
            float delta = target.transform.position.x -
                attacker.transform.position.x;
            if (!Mathf.Approximately(delta, 0f))
                direction = Mathf.Sign(delta);
        }
        return Vector3.right * direction * Mathf.Abs(distance);
    }

    public void Restore(BattlePresentationScope scopeKey)
    {
        if (actorPool != null && actorPool.RejectForeignProjectionWrite("LegacyConflict.Restore")) return;
        RestoreActors(scopeKey);
        backgroundPresenter?.Restore(scopeKey);
        if (scopes.TryGetValue(scopeKey, out BattleConflictPresentationScope scope))
        {
            scope.Restore();
            scopes.Remove(scopeKey);
        }
    }

    public void RestoreAll()
    {
        BattlePresentationScope[] scopeKeys =
            new BattlePresentationScope[scopes.Count];
        scopes.Keys.CopyTo(scopeKeys, 0);
        for (int i = 0; i < scopeKeys.Length; i++)
            Restore(scopeKeys[i]);
    }

    private void OnDisable() => RestoreAll();

    private List<ActorState> CaptureActorStates()
    {
        var result = new List<ActorState>();
        if (actorPool == null)
            return result;
        BattleWorldActorView[] views = actorPool.GetComponentsInChildren<BattleWorldActorView>(true);
        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] != null && views[i].RuntimeId > 0)
                result.Add(new ActorState(views[i]));
        }
        return result;
    }

    private void EnsureActorStates(BattlePresentationScope scopeKey)
    {
        if (!actorStates.ContainsKey(scopeKey))
            actorStates.Add(scopeKey, CaptureActorStates());
    }

    private ActorState FindState(
        BattlePresentationScope scopeKey,
        int runtimeId)
    {
        if (!actorStates.TryGetValue(scopeKey, out List<ActorState> states))
            return null;
        for (int i = 0; i < states.Count; i++)
        {
            if (states[i].View != null && states[i].View.RuntimeId == runtimeId)
                return states[i];
        }
        return null;
    }

    private void RestoreActors(BattlePresentationScope scopeKey)
    {
        if (!actorStates.TryGetValue(scopeKey, out List<ActorState> states))
            return;
        for (int i = 0; i < states.Count; i++)
            states[i].Restore();
        actorStates.Remove(scopeKey);
    }

    private void AddFocusPoint(
        BattleParticipantPresentationSnapshot participant,
        ISet<int> runtimeIds,
        ICollection<Vector3> destination)
    {
        if (participant == null || actorPool == null ||
            !runtimeIds.Add(participant.RuntimeId) ||
            !actorPool.TryGet(
                participant.RuntimeId,
                out BattleWorldActorView view) ||
            view == null)
        {
            return;
        }

        Transform anchor = view.PresentationAnchor != null
            ? view.PresentationAnchor
            : view.transform;
        destination.Add(cameraPresenter.ToShotLocal(anchor.position));
    }

    private static void ApplyOtherPolicy(
        BattleWorldActorView view,
        BattleOtherTargetPresentationPolicy policy)
    {
        SpriteRenderer[] renderers = view.GetComponentsInChildren<SpriteRenderer>(true);
        float alpha = policy == BattleOtherTargetPresentationPolicy.Hide
            ? 0f
            : policy == BattleOtherTargetPresentationPolicy.Dim ? 0.3f : 1f;
        for (int i = 0; i < renderers.Length; i++)
        {
            Color color = renderers[i].color;
            color.a = alpha;
            renderers[i].color = color;
        }
    }

    private static IBattlePresentationHandle Settled(
        BattlePresentationOutcome outcome)
    {
        var handle = new BattlePresentationHandle();
        handle.TrySettle(outcome);
        return handle;
    }

    private static void RestoreImpactTargets(
        IReadOnlyList<ImpactTargetState> targets)
    {
        for (int i = 0; i < targets.Count; i++)
            targets[i].Restore();
    }

    private sealed class ImpactTargetState
    {
        private readonly SpriteRenderer[] renderers;
        private readonly Color[] colors;

        public ImpactTargetState(BattleWorldActorView view)
        {
            View = view;
            LocalPosition = view.transform.localPosition;
            LocalScale = view.transform.localScale;
            renderers = view.GetComponentsInChildren<SpriteRenderer>(true);
            colors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                colors[i] = renderers[i].color;
        }

        public BattleWorldActorView View { get; }
        public Vector3 LocalPosition { get; }
        public Vector3 LocalScale { get; }

        public void AddFlash(Sequence sequence)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null)
                    continue;
                Color hitColor = new Color(
                    1f,
                    0.38f,
                    0.5f,
                    colors[i].a);
                sequence.Join(renderer.DOColor(hitColor, 0.045f)
                    .SetLoops(2, LoopType.Yoyo));
            }
        }

        public void Restore()
        {
            if (View == null)
                return;
            View.transform.localPosition = LocalPosition;
            View.transform.localScale = LocalScale;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].color = colors[i];
            }
        }
    }

    private sealed class ActorState
    {
        private readonly Color[] colors;
        private readonly SpriteRenderer[] renderers;
        private readonly bool active;
        private readonly Transform visualRoot;
        private readonly Vector3 visualLocalPosition;
        private readonly Vector3 visualLocalScale;

        public ActorState(BattleWorldActorView view)
        {
            View = view;
            Position = view.transform.localPosition;
            Scale = view.transform.localScale;
            active = view.gameObject.activeSelf;
            visualRoot = view.VisualRoot;
            visualLocalPosition = visualRoot != null
                ? visualRoot.localPosition
                : Vector3.zero;
            visualLocalScale = visualRoot != null
                ? visualRoot.localScale
                : Vector3.one;
            renderers = view.GetComponentsInChildren<SpriteRenderer>(true);
            colors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                colors[i] = renderers[i].color;
        }

        public BattleWorldActorView View { get; }
        public Vector3 Position { get; }
        public Vector3 Scale { get; }

        public void AppendRestoreTweens(Sequence sequence, float seconds)
        {
            if (View == null || sequence == null)
                return;
            if (active && !View.gameObject.activeSelf)
                View.gameObject.SetActive(true);
            if (visualRoot != null)
            {
                sequence.Join(visualRoot.DOLocalMove(
                        visualLocalPosition,
                        seconds)
                    .SetEase(Ease.InOutCubic));
                sequence.Join(visualRoot.DOScale(
                        visualLocalScale,
                        seconds)
                    .SetEase(Ease.OutCubic));
            }
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;
                sequence.Join(renderers[i].DOColor(colors[i], seconds)
                    .SetEase(Ease.InOutCubic));
            }
        }

        public void Restore()
        {
            if (View == null)
                return;
            View.RestoreVisual();
            View.transform.localPosition = Position;
            View.transform.localScale = Scale;
            View.gameObject.SetActive(active);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].color = colors[i];
            }
        }
    }
}
