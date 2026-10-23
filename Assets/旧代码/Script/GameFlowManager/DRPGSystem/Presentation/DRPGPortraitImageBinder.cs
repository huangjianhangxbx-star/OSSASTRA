using UnityEngine;
using UnityEngine.UI;

public static class DRPGPortraitImageBinder
{
    public static void Bind(
        Image image,
        DRPGBattleParticipantViewModel viewModel,
        bool cropToFill = false,
        DRPGBattlePortraitState portraitState = DRPGBattlePortraitState.Normal)
    {
        if (image == null)
            return;

        Sprite portrait = viewModel != null
            ? viewModel.ResolvePortrait(portraitState)
            : null;
        image.sprite = portrait;
        image.enabled = portrait != null;
        image.preserveAspect = true;

        AspectRatioFitter fitter = image.GetComponent<AspectRatioFitter>();
        if (fitter != null)
            fitter.enabled = false;

        RectTransform rect = image.rectTransform;
        float portraitScale = viewModel != null ? viewModel.PortraitScale : 1f;
        rect.localScale = Vector3.one * portraitScale;

        if (viewModel == null)
        {
            rect.anchoredPosition = Vector2.zero;
            return;
        }

        Vector2 focus = viewModel.PortraitFocus;
        RectTransform parent = rect.parent as RectTransform;
        Vector2 viewportSize = parent != null ? parent.rect.size : rect.rect.size;
        if (cropToFill && portrait != null &&
            viewportSize.x > 0f && viewportSize.y > 0f &&
            portrait.rect.height > 0f)
        {
            float spriteAspect = portrait.rect.width / portrait.rect.height;
            float viewportAspect = viewportSize.x / viewportSize.y;
            Vector2 cropSize = spriteAspect >= viewportAspect
                ? new Vector2(viewportSize.y * spriteAspect, viewportSize.y)
                : new Vector2(viewportSize.x, viewportSize.x / spriteAspect);
            cropSize *= viewModel.AvatarZoom;

            rect.anchorMin = Vector2.one * 0.5f;
            rect.anchorMax = Vector2.one * 0.5f;
            rect.pivot = Vector2.one * 0.5f;
            rect.sizeDelta = cropSize;
            rect.anchoredPosition = new Vector2(
                (0.5f - focus.x) * cropSize.x,
                (0.5f - focus.y) * cropSize.y);
            return;
        }

        rect.anchoredPosition = new Vector2(
            (0.5f - focus.x) * viewportSize.x,
            (0.5f - focus.y) * viewportSize.y);
    }
}
