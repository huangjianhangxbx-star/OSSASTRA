using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleImpactFeedbackPresenter : MonoBehaviour,
    IBattleParticipantAnchorResolver
{
    [SerializeField] private BattlePartyHud partyHud;
    [SerializeField] private BattlefieldEnemyRosterView enemyRoster;
    [SerializeField] private BattleWorldTargetingPresenter worldTargeting;
    [SerializeField] private RectTransform feedbackLayer;
    [SerializeField] private BattleDamageNumberView damageNumberTemplate;
    [SerializeField] private UnityEngine.UI.Graphic defeatMarkerTemplate;

    private readonly List<BattleDamageNumberView> pooledNumbers =
        new List<BattleDamageNumberView>();
    private readonly List<BattleDamageNumberView> activeNumbers =
        new List<BattleDamageNumberView>();
    private readonly List<Sequence> impactSequences = new List<Sequence>();
    private readonly List<IBattlePresentationHandle> activeImpactHandles =
        new List<IBattlePresentationHandle>();

    internal void PrepareForBattle()
    {
        if (activeNumbers.Count + pooledNumbers.Count > 0 ||
            damageNumberTemplate == null)
        {
            return;
        }

        Transform parent = feedbackLayer != null
            ? feedbackLayer
            : damageNumberTemplate.transform.parent;
        BattleDamageNumberView view = Instantiate(
            damageNumberTemplate,
            parent,
            false);
        view.gameObject.SetActive(false);
        pooledNumbers.Add(view);
    }

    internal UnityEngine.UI.Graphic CreateDefeatMarker(Vector3 worldPosition, Camera battleCamera)
    {
        if (defeatMarkerTemplate == null || feedbackLayer == null || battleCamera == null) return null;
        var marker = Instantiate(defeatMarkerTemplate, feedbackLayer, false);
        marker.name = "DefeatPendingMarker";
        marker.raycastTarget = false;
        var color = marker.color; color.a = 1f; marker.color = color;
        marker.gameObject.SetActive(true);
        if (PositionWorldGraphic(marker, worldPosition, battleCamera)) return marker;
        marker.gameObject.SetActive(false); DestroyOwnedMarker(marker); return null;
    }

    internal static bool PositionWorldGraphic(UnityEngine.UI.Graphic graphic, Vector3 worldPosition, Camera battleCamera)
    {
        if (graphic == null || battleCamera == null || !(graphic.transform.parent is RectTransform parent)) return false;
        var canvas = parent.GetComponentInParent<Canvas>();
        var screen = battleCamera.WorldToScreenPoint(worldPosition);
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (screen.z <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCamera, out var point)) return false;
        graphic.rectTransform.anchorMin = graphic.rectTransform.anchorMax = parent.pivot;
        graphic.rectTransform.anchoredPosition = point;
        return true;
    }

    internal static void DestroyOwnedMarker(UnityEngine.UI.Graphic marker)
    {
        if (marker == null) return;
        marker.gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(marker.gameObject); else DestroyImmediate(marker.gameObject);
    }

    internal IBattlePresentationHandle PresentStandardOutcome(DRPGBattleHitResult hit,
        Vector3 worldPosition, Camera battleCamera, IDRPGBattlePresentationClock clock, double issuedAt)
    {
        if (hit == null) return Settled(BattlePresentationOutcome.Skipped);
        float displayDamage = hit.DamageBreakdown != null
            ? hit.DamageBreakdown.RequestedDamage
            : hit.AppliedDamage;
        string label = hit.Outcome == DRPGHitOutcome.Evaded ? "闪避" :
            (hit.Outcome == DRPGHitOutcome.Guarded ? "防御 " : "") + "-" + Mathf.CeilToInt(Mathf.Max(0f, displayDamage));
        return PresentStandardLabel(label, worldPosition, battleCamera, clock, issuedAt);
    }

    internal IBattlePresentationHandle PresentStandardLabel(string label,
        Vector3 worldPosition, Camera battleCamera, IDRPGBattlePresentationClock clock, double issuedAt)
    {
        // Deliberately does not resolve a Party HUD portrait anchor: both sides
        // use the same world-to-overlay route and receive only one result label.
        if (string.IsNullOrEmpty(label) || battleCamera == null || feedbackLayer == null || clock == null)
            return Settled(BattlePresentationOutcome.Skipped);
        var canvas = feedbackLayer.GetComponentInParent<Canvas>();
        var screen = battleCamera.WorldToScreenPoint(worldPosition);
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (screen.z <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(feedbackLayer, screen, uiCamera, out var point))
            return Settled(BattlePresentationOutcome.Skipped);
        var view = TakeNumber();
        if (view == null) return Settled(BattlePresentationOutcome.Skipped);
        var rect = view.transform as RectTransform;
        if (rect != null) { rect.anchorMin = rect.anchorMax = feedbackLayer.pivot; rect.anchoredPosition = point + Vector2.up * 24f; }
        var handle = view.PlayOutcomeLabel(label, clock, issuedAt, .45f);
        handle.WhenSettled(_ => ReturnNumber(view));
        return handle;
    }

    internal void TickStandardOutcomes()
    {
        foreach (var number in activeNumbers.ToArray())
            if (number != null) number.TickOutcomeLabel();
    }

    public bool TryResolve(int runtimeId, out BattleImpactAnchor anchor)
    {
        if (partyHud != null &&
            partyHud.TryResolveImpactAnchor(runtimeId, out anchor))
        {
            return true;
        }
        if (worldTargeting != null &&
            worldTargeting.TryResolveImpactAnchor(runtimeId, out anchor))
        {
            return true;
        }
        if (enemyRoster != null &&
            enemyRoster.TryResolveImpactAnchor(runtimeId, out anchor))
        {
            return true;
        }
        anchor = null;
        return false;
    }

    public IBattlePresentationHandle Present(
        IReadOnlyList<BattleHitPresentationSnapshot> hits,
        float holdSeconds)
    {
        if (hits == null)
            throw new ArgumentNullException(nameof(hits));

        var children = new List<IBattlePresentationHandle>();
        for (int i = 0; i < hits.Count; i++)
        {
            BattleHitPresentationSnapshot hit = hits[i];
            if (hit == null ||
                !TryResolve(hit.TargetRuntimeId, out BattleImpactAnchor anchor))
            {
                continue;
            }

            // Enemy world actors already receive recoil/tint from the semantic
            // conflict layer.  The legacy impact layer only owns their damage
            // number; animating the EnemyField proxy here caused a second,
            // delayed flash after the number appeared.
            if (anchor.Kind != BattleImpactAnchorKind.EnemyField)
            {
                anchor.SetPortraitState(DRPGBattlePortraitState.Hit);
                children.Add(PlayAnchorImpact(anchor, holdSeconds));
            }
            IBattlePresentationHandle number = PlayDamageNumber(
                anchor,
                hit.AppliedDamage,
                holdSeconds);
            if (number != null)
                children.Add(number);
        }

        if (children.Count == 0)
            return Settled(BattlePresentationOutcome.Skipped);
        return new BattlePresentationCompositeHandle(children);
    }

    public void CancelAll(
        BattlePresentationCancelReason reason =
            BattlePresentationCancelReason.ScopeEnded)
    {
        IBattlePresentationHandle[] impacts = activeImpactHandles.ToArray();
        for (int i = 0; i < impacts.Length; i++)
        {
            if (!impacts[i].IsSettled)
                impacts[i].Cancel(reason);
        }

        BattleDamageNumberView[] numbers = activeNumbers.ToArray();
        for (int i = 0; i < numbers.Length; i++)
        {
            BattleDamageNumberView view = numbers[i];
            if (view != null)
                view.Cancel(reason);
        }
    }

    private IBattlePresentationHandle PlayAnchorImpact(
        BattleImpactAnchor anchor,
        float holdSeconds)
    {
        RectTransform rect = anchor.RectTransform;
        Graphic portrait = anchor.PortraitGraphic;
        Vector3 baseScale = rect != null ? rect.localScale : Vector3.one;
        Color baseColor = portrait != null ? portrait.color : Color.white;
        Sequence owned = DOTween.Sequence().SetUpdate(true);
        impactSequences.Add(owned);
        BattlePresentationHandle handle = null;
        handle = new BattlePresentationHandle(
            reason => CancelAnchorImpact(
                owned,
                handle,
                anchor,
                rect,
                portrait,
                baseScale,
                baseColor,
                reason));
        activeImpactHandles.Add(handle);

        if (rect != null)
        {
            owned.Append(rect.DOScale(baseScale * 0.94f, 0.10f));
            owned.Append(rect.DOScale(baseScale, 0.24f).SetEase(Ease.OutBack));
        }
        if (portrait != null)
        {
            owned.Insert(0f, portrait.DOColor(
                new Color(1f, 0.36f, 0.42f, baseColor.a),
                0.12f));
            owned.Insert(0.12f, portrait.DOColor(baseColor, 0.20f));
        }

        float feedbackMotionSeconds = rect != null ? 0.34f : 0.32f;
        owned.AppendInterval(Mathf.Max(0f, holdSeconds - feedbackMotionSeconds));
        owned.OnComplete(() => FinishAnchorImpact(
            owned,
            handle,
            anchor,
            rect,
            portrait,
            baseScale,
            baseColor,
            BattlePresentationOutcome.Completed));
        owned.OnKill(() => FinishAnchorImpact(
            owned,
            handle,
            anchor,
            rect,
            portrait,
            baseScale,
            baseColor,
            BattlePresentationOutcome.Cancelled));
        return handle;
    }

    private void CancelAnchorImpact(
        Sequence owned,
        BattlePresentationHandle handle,
        BattleImpactAnchor anchor,
        RectTransform rect,
        Graphic portrait,
        Vector3 baseScale,
        Color baseColor,
        BattlePresentationCancelReason reason)
    {
        bool wasOwned = impactSequences.Remove(owned);
        activeImpactHandles.Remove(handle);
        if (wasOwned && owned != null && owned.IsActive())
            owned.Kill(false);
        RestoreAnchor(anchor, rect, portrait, baseScale, baseColor);
        handle.TrySettle(BattlePresentationHandle.MapCancellation(reason));
    }

    private void FinishAnchorImpact(
        Sequence owned,
        BattlePresentationHandle handle,
        BattleImpactAnchor anchor,
        RectTransform rect,
        Graphic portrait,
        Vector3 baseScale,
        Color baseColor,
        BattlePresentationOutcome outcome)
    {
        if (!impactSequences.Remove(owned))
            return;

        activeImpactHandles.Remove(handle);
        RestoreAnchor(anchor, rect, portrait, baseScale, baseColor);
        handle.TrySettle(outcome);
    }

    private IBattlePresentationHandle PlayDamageNumber(
        BattleImpactAnchor anchor,
        float damage,
        float holdSeconds)
    {
        BattleDamageNumberView view = TakeNumber();
        if (view == null)
            return null;

        RectTransform rect = view.transform as RectTransform;
        if (rect != null && anchor.RectTransform != null)
        {
            Vector3 world = anchor.RectTransform.TransformPoint(
                anchor.RectTransform.rect.center);
            rect.position = world;
            rect.anchoredPosition += new Vector2(0f, 24f);
        }

        IBattlePresentationHandle handle = view.Play(damage, holdSeconds);
        handle.WhenSettled(_ => ReturnNumber(view));
        return handle;
    }

    private BattleDamageNumberView TakeNumber()
    {
        BattleDamageNumberView view = null;
        if (pooledNumbers.Count > 0)
        {
            int index = pooledNumbers.Count - 1;
            view = pooledNumbers[index];
            pooledNumbers.RemoveAt(index);
        }
        else if (damageNumberTemplate != null)
        {
            Transform parent = feedbackLayer != null
                ? feedbackLayer
                : damageNumberTemplate.transform.parent;
            view = Instantiate(damageNumberTemplate, parent, false);
        }

        if (view == null)
            return null;

        activeNumbers.Add(view);
        view.gameObject.SetActive(true);
        return view;
    }

    private void ReturnNumber(BattleDamageNumberView view)
    {
        if (view == null || !activeNumbers.Remove(view))
            return;

        view.gameObject.SetActive(false);
        if (!pooledNumbers.Contains(view))
            pooledNumbers.Add(view);
    }

    private static void RestoreAnchor(
        BattleImpactAnchor anchor,
        RectTransform rect,
        Graphic portrait,
        Vector3 baseScale,
        Color baseColor)
    {
        if (rect != null)
            rect.localScale = baseScale;
        if (portrait != null)
            portrait.color = baseColor;
        anchor.SetPortraitState(DRPGBattlePortraitState.Normal);
    }

    private static IBattlePresentationHandle Settled(
        BattlePresentationOutcome outcome)
    {
        var handle = new BattlePresentationHandle();
        handle.TrySettle(outcome);
        return handle;
    }

    private void OnDisable()
    {
        CancelAll(BattlePresentationCancelReason.ScopeEnded);
    }
}
