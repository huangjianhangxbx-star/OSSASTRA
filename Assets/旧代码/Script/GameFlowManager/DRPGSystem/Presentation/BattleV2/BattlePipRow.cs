using System.Collections.Generic;
using UnityEngine;

public sealed class BattlePipRow : MonoBehaviour
{
    [SerializeField] private List<UnityEngine.UI.Image> pips = new List<UnityEngine.UI.Image>();
    [SerializeField] private Sprite filledSprite;
    [SerializeField] private Sprite emptySprite;
    [SerializeField] private Color filledColor = Color.white;
    [SerializeField] private Color emptyColor = new Color(.42f, .45f, .47f, .78f);
    [SerializeField] private Color committedColor = new Color(.55f, .39f, .12f, .74f);

    public int VisiblePipCount { get; private set; }
    public int LitPipCount { get; private set; }

    public void Bind(int available, int capacity, int previewCost)
    {
        VisiblePipCount = Mathf.Max(0, capacity);
        LitPipCount = Mathf.Clamp(available, 0, VisiblePipCount);
        int committedFrom = Mathf.Max(0, LitPipCount - Mathf.Max(0, previewCost));

        for (int i = 0; i < pips.Count; i++)
        {
            UnityEngine.UI.Image pip = pips[i];
            if (pip == null)
                continue;
            bool visible = i < VisiblePipCount;
            pip.gameObject.SetActive(visible);
            if (!visible)
                continue;
            bool lit = i < LitPipCount;
            pip.sprite = lit ? filledSprite : emptySprite;
            pip.color = lit && i >= committedFrom ? committedColor :
                lit ? filledColor : emptyColor;
        }
    }
}
