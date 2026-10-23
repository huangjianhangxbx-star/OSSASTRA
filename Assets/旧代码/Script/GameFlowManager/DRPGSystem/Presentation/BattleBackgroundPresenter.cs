using UnityEngine;
using UnityEngine.UI;

public sealed class BattleBackgroundPresenter : MonoBehaviour
{
    [SerializeField] private Image background;

    public void Bind(Sprite sprite)
    {
        if (background == null)
            return;

        background.sprite = sprite;
        background.enabled = sprite != null;
        background.preserveAspect = true;

        AspectRatioFitter fitter = background.GetComponent<AspectRatioFitter>();
        if (fitter != null && sprite != null && sprite.rect.height > 0f)
            fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
    }
}
