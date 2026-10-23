#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class BattleFixedSkillBarProbe
{
    private const string PrefabPath =
        "Assets/GameResources/DRPG/Prefabs/PF_DRPGBattlePresentation.prefab";

    public static void RunAll()
    {
        RealPrefabOwnsExactlyFourStableSlots();
        LockedBarDisablesSkillAndRecoveryButtons();
        FormalPrefabDoesNotBlockSkillOrRecoveryPointerRaycasts();
    }

    private static void RealPrefabOwnsExactlyFourStableSlots()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Check(prefab != null, "battle presentation prefab missing");
        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        try
        {
            BattleFixedSkillBar bar =
                instance.GetComponentInChildren<BattleFixedSkillBar>(true);
            Check(bar != null, "fixed skill bar missing from real prefab");
            Check(bar.VisibleSlotCount == DRPGSkillLoadout.SlotCount,
                "real prefab does not expose exactly four skill slots");
            Check(bar.GetComponentsInChildren<BattleSkillMenuEntryView>(true).Length == 4,
                "real prefab skill slot hierarchy is not stable");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static void LockedBarDisablesSkillAndRecoveryButtons()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        try
        {
            BattleFixedSkillBar bar =
                instance.GetComponentInChildren<BattleFixedSkillBar>(true);
            Check(bar != null, "fixed skill bar missing from real prefab");
            bar.SetInputEnabled(false);
            Check(!bar.GetComponentsInChildren<UnityEngine.UI.Button>(true)
                    .Any(button => button.interactable),
                "locked skill or recovery input remains active");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static void FormalPrefabDoesNotBlockSkillOrRecoveryPointerRaycasts()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        try
        {
            BattleFixedSkillBar bar =
                instance.GetComponentInChildren<BattleFixedSkillBar>(true);
            Check(bar != null, "fixed skill bar missing from real prefab");
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(
                instance.transform as RectTransform);

            Button[] buttons = bar.GetComponentsInChildren<Button>(true);
            Check(buttons.Length == DRPGSkillLoadout.SlotCount + 1,
                "formal fixed bar must own four skills and one recovery button");
            for (int i = 0; i < buttons.Length; i++)
            {
                RectTransform rect = buttons[i].transform as RectTransform;
                Vector3[] corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                Canvas canvas = buttons[i].GetComponentInParent<Canvas>();
                Camera camera = canvas != null &&
                    canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.worldCamera
                    : null;
                Vector2 center = RectTransformUtility.WorldToScreenPoint(
                    camera, (corners[0] + corners[2]) * .5f);
                Graphic target = buttons[i].targetGraphic;
                Check(target != null && target.raycastTarget &&
                        target.Raycast(center, camera),
                    "button target graphic rejects pointer raycasts at its visual center: " +
                    buttons[i].gameObject.name);
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
