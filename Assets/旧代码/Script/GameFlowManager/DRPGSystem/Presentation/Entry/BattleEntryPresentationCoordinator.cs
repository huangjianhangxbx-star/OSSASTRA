using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public sealed class BattleEntryPresentationCoordinator : MonoBehaviour
{
    [SerializeField] private CanvasGroup hudRoot;
    [SerializeField] private BattleWorldRosterPresenter worldRoster;
    [SerializeField] private BattleCameraPresenter cameraPresenter;

    private Sequence sequence;
    private BattlePresentationHandle activeHandle;
    private bool inPlaceMode;

    public bool IsPlaying => activeHandle != null && !activeHandle.IsSettled;

    public void Configure(
        CanvasGroup hud,
        BattleWorldRosterPresenter roster,
        BattleCameraPresenter camera)
    {
        hudRoot = hud;
        worldRoster = roster;
        cameraPresenter = camera;
    }

    public void SetInPlaceMode(bool value) => inPlaceMode = value;

    public void PrepareHidden()
    {
        if (hudRoot == null)
            return;
        hudRoot.DOKill(false);
        hudRoot.alpha = 0f;
        hudRoot.interactable = false;
        hudRoot.blocksRaycasts = false;
    }

    public IBattlePresentationHandle Play(
        BattleEntryPresentationRecipeSO recipe)
    {
        Cancel(BattlePresentationCancelReason.Replaced);
        activeHandle = new BattlePresentationHandle(Cancel);

        if (inPlaceMode)
        {
            RevealImmediate();
            activeHandle.TrySettle(BattlePresentationOutcome.Completed);
            return activeHandle;
        }

        if (recipe == null)
        {
            RevealImmediate();
            activeHandle.TrySettle(BattlePresentationOutcome.Skipped);
            return activeHandle;
        }

        cameraPresenter?.Play(recipe.SpawnShot, true);
        IReadOnlyList<BattleWorldActorView> actors = worldRoster != null
            ? worldRoster.ActiveViews
            : Array.Empty<BattleWorldActorView>();
        sequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        for (int i = 0; i < actors.Count; i++)
        {
            BattleWorldActorView actor = actors[i];
            if (actor == null)
                continue;
            Vector3 finalScale = actor.transform.localScale;
            actor.transform.localScale = finalScale * recipe.SpawnStartScale;
            SetActorAlpha(actor, 0f);
            sequence.Join(actor.transform.DOScale(
                    finalScale,
                    recipe.SpawnDuration)
                .SetEase(Ease.OutBack));
            SpriteRenderer[] renderers =
                actor.GetComponentsInChildren<SpriteRenderer>(true);
            for (int rendererIndex = 0;
                rendererIndex < renderers.Length;
                rendererIndex++)
            {
                sequence.Join(renderers[rendererIndex]
                    .DOFade(1f, recipe.SpawnDuration)
                    .SetEase(Ease.OutCubic));
            }
        }

        sequence.AppendInterval(recipe.HoldDuration);
        sequence.AppendCallback(() =>
            cameraPresenter?.Play(recipe.ReadyShot));
        if (hudRoot != null)
        {
            sequence.Append(hudRoot.DOFade(1f, recipe.HudRevealDuration)
                .SetEase(Ease.OutCubic));
        }
        sequence.OnComplete(() =>
        {
            RevealImmediate();
            activeHandle?.TrySettle(BattlePresentationOutcome.Completed);
            sequence = null;
        });

        if (sequence.Duration(false) <= 0f)
        {
            sequence.Complete(true);
        }
        return activeHandle;
    }

    public void Cancel(BattlePresentationCancelReason reason)
    {
        if (sequence != null && sequence.IsActive())
            sequence.Kill(false);
        sequence = null;
        RevealImmediate();
        activeHandle?.TrySettle(BattlePresentationHandle.MapCancellation(reason));
    }

    private void RevealImmediate()
    {
        if (hudRoot == null)
            return;
        hudRoot.DOKill(false);
        hudRoot.alpha = 1f;
        hudRoot.interactable = true;
        hudRoot.blocksRaycasts = true;
    }

    private static void SetActorAlpha(BattleWorldActorView actor, float alpha)
    {
        SpriteRenderer[] renderers =
            actor.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Color color = renderers[i].color;
            color.a = alpha;
            renderers[i].color = color;
        }
    }

    private void OnDisable() =>
        Cancel(BattlePresentationCancelReason.BattleHidden);
}
