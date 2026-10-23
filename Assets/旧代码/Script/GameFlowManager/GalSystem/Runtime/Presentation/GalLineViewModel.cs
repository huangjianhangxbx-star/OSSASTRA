using UnityEngine;

public readonly struct GalLineViewModel
{
    public int LineIndex { get; }
    public string SpeakerName { get; }
    public string Text { get; }
    public Sprite Portrait { get; }
    public GalPortraitSlot PortraitSlot { get; }
    public Sprite Background { get; }
    public float AutoAdvanceSecondsOverride { get; }

    public GalLineViewModel(
        int lineIndex,
        string speakerName,
        string text,
        Sprite portrait,
        GalPortraitSlot portraitSlot,
        Sprite background = null,
        float autoAdvanceSecondsOverride = 0f)
    {
        LineIndex = lineIndex;
        SpeakerName = speakerName ?? string.Empty;
        Text = text ?? string.Empty;
        Portrait = portrait;
        PortraitSlot = portraitSlot;
        Background = background;
        AutoAdvanceSecondsOverride = Mathf.Max(
            0f,
            autoAdvanceSecondsOverride);
    }
}
