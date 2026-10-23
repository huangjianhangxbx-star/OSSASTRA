using UnityEngine;
using UnityEngine.UI;

public enum DRPGBattleTargetFeedback
{
    None = 0,
    Hovered = 1,
    Selected = 2,
    EnemyIntent = 3
}

public static class DRPGBattleTargetFeedbackVisual
{
    private static readonly Color HoverColor =
        new Color32(166, 246, 255, 245);
    private static readonly Color SelectedColor =
        new Color32(255, 47, 134, 255);
    private static readonly Color EnemyIntentColor =
        new Color32(255, 58, 78, 255);

    public static void Apply(
        Outline outline,
        Graphic overlay,
        RectTransform scaleTarget,
        DRPGBattleTargetFeedback feedback)
    {
        if (outline != null)
        {
            outline.enabled = feedback != DRPGBattleTargetFeedback.None;
            switch (feedback)
            {
                case DRPGBattleTargetFeedback.Hovered:
                    outline.effectColor = HoverColor;
                    outline.effectDistance = new Vector2(2f, -2f);
                    break;
                case DRPGBattleTargetFeedback.Selected:
                    outline.effectColor = SelectedColor;
                    outline.effectDistance = new Vector2(5f, -5f);
                    break;
                case DRPGBattleTargetFeedback.EnemyIntent:
                    outline.effectColor = EnemyIntentColor;
                    outline.effectDistance = new Vector2(6f, -6f);
                    break;
            }
        }

        if (overlay != null)
        {
            overlay.enabled = feedback != DRPGBattleTargetFeedback.None;
            switch (feedback)
            {
                case DRPGBattleTargetFeedback.Hovered:
                    overlay.color = new Color32(166, 246, 255, 210);
                    break;
                case DRPGBattleTargetFeedback.Selected:
                    overlay.color = new Color32(255, 47, 134, 235);
                    break;
                case DRPGBattleTargetFeedback.EnemyIntent:
                    overlay.color = new Color32(255, 58, 78, 240);
                    break;
            }
        }

        if (scaleTarget != null)
        {
            float scale = feedback == DRPGBattleTargetFeedback.None
                ? 1f
                : feedback == DRPGBattleTargetFeedback.Hovered ? 1.02f : 1.04f;
            scaleTarget.localScale = Vector3.one * scale;
        }
    }
}
