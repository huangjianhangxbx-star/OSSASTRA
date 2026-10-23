#if UNITY_EDITOR
using System;
using DG.Tweening;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class BattleArrowAndPipProbe
{
    private const string PresentationPrefabPath =
        "Assets/GameResources/DRPG/Prefabs/PF_DRPGBattlePresentation.prefab";

    public static void RunAll()
    {
        ArrowSamplingUsesStableQuadraticCurve();
        PipRowRendersWholeVisibleCapacity();
        FormalPrefabContainsFourCardsAndArrow();
        CardHoverExitReturnsEveryCardToItsOwnLayoutPosition();
    }

    private static void ArrowSamplingUsesStableQuadraticCurve()
    {
        Vector2 point = BattleCardTargetArrow.Sample(
            new Vector2(0f, 0f),
            new Vector2(35f, 140f),
            new Vector2(70f, 0f),
            .5f);
        Check(Vector2.Distance(point, new Vector2(35f, 70f)) < .001f,
            "card arrow does not use the specified quadratic curve");
    }

    private static void PipRowRendersWholeVisibleCapacity()
    {
        var root = new GameObject("PipProbe", typeof(RectTransform));
        try
        {
            BattlePipRow row = root.AddComponent<BattlePipRow>();
            row.Bind(2, 5, 1);
            Check(row.VisiblePipCount == 5, "pip row did not expose five whole pips");
            Check(row.LitPipCount == 2, "pip row lit count does not match available stamina");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void FormalPrefabContainsFourCardsAndArrow()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PresentationPrefabPath);
        Check(prefab != null, "formal battle presentation prefab is missing");
        BattleCardHandView hand = prefab.GetComponentInChildren<BattleCardHandView>(true);
        BattleCardTargetArrow arrow = prefab.GetComponentInChildren<BattleCardTargetArrow>(true);
        Check(hand != null && hand.VisibleSlotCount == 4,
            "formal prefab does not contain exactly four fixed card slots");
        Check(arrow != null, "formal prefab does not contain the card target arrow");
    }

    private static void CardHoverExitReturnsEveryCardToItsOwnLayoutPosition()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PresentationPrefabPath);
        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        try
        {
            BattleCardHandView hand =
                instance.GetComponentInChildren<BattleCardHandView>(true);
            Check(hand != null, "formal card hand is missing");
            DRPGBattleUGUISurface surface =
                instance.GetComponentInChildren<DRPGBattleUGUISurface>(true);
            Check(surface != null, "formal battle surface is missing");
            hand.Bind(surface.BattleV2PreviewCards);
            Canvas.ForceUpdateCanvases();
            RectTransform handRect = hand.transform as RectTransform;
            LayoutRebuilder.ForceRebuildLayoutImmediate(handRect);

            for (int i = 0; i < hand.VisibleSlotCount; i++)
            {
                RectTransform slot = hand.GetSlotRect(i);
                Check(slot != null, "formal card slot is missing: " + i);
                Vector2 expectedRest = slot.anchoredPosition;
                BattleCardPreviewView view =
                    slot.GetComponent<BattleCardPreviewView>();
                Check(view != null, "formal card preview view is missing: " + i);

                view.Bind(null);
                view.OnPointerEnter(new PointerEventData(null));
                view.OnPointerExit(new PointerEventData(null));
                slot.DOComplete();
                Check(Vector2.Distance(slot.anchoredPosition, expectedRest) < .01f,
                    "empty card pointer exit moved the laid-out slot: " + i);
                view.Bind(surface.BattleV2PreviewCards[i]);

                view.OnPointerEnter(new PointerEventData(null));
                slot.DOComplete();

                view.OnPointerEnter(new PointerEventData(null));
                slot.DOComplete();
                view.OnPointerExit(new PointerEventData(null));
                // Re-enter while the return tween still owns the position.
                view.OnPointerEnter(new PointerEventData(null));
                slot.DOComplete();
                view.OnPointerExit(new PointerEventData(null));
                slot.DOComplete();
                view.OnPointerExit(new PointerEventData(null));
                slot.DOComplete();

                Check(Vector2.Distance(slot.anchoredPosition, expectedRest) < .01f,
                    "card hover exit did not restore its own layout position: " + i +
                    "; expected=" + expectedRest + "; actual=" + slot.anchoredPosition);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
