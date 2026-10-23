using System;
using System.Collections.Generic;
using UnityEngine;

public enum GalPresentationMode
{
    PausedOverlay,
    FullscreenExclusive,
    ConcurrentOverlay
}

public enum GalPortraitSlot
{
    Left,
    Center,
    Right
}

[Serializable]
public sealed class GalLine
{
    [SerializeField] private GalActorSO actor;
    [SerializeField] private string portraitVariantKey;
    [SerializeField] private Sprite portraitOverride;
    [SerializeField] private string speakerNameOverride;
    [SerializeField, TextArea(2, 6)] private string text;
    [SerializeField] private GalPortraitSlot portraitSlot;
    [SerializeField] private Sprite backgroundOverride;
    [SerializeField, Min(0f)] private float autoAdvanceSecondsOverride;

    public GalActorSO Actor => actor;
    public string PortraitVariantKey => portraitVariantKey ?? string.Empty;
    public Sprite PortraitOverride => portraitOverride;
    public string SpeakerNameOverride => speakerNameOverride ?? string.Empty;
    public string Text => text ?? string.Empty;
    public GalPortraitSlot PortraitSlot => portraitSlot;
    public Sprite BackgroundOverride => backgroundOverride;
    public float AutoAdvanceSecondsOverride =>
        Mathf.Max(0f, autoAdvanceSecondsOverride);

    public GalLine(
        GalActorSO actor,
        string text,
        string portraitVariantKey = "",
        Sprite portraitOverride = null,
        string speakerNameOverride = "",
        GalPortraitSlot portraitSlot = GalPortraitSlot.Left,
        Sprite backgroundOverride = null,
        float autoAdvanceSecondsOverride = 0f)
    {
        this.actor = actor;
        this.text = text;
        this.portraitVariantKey = portraitVariantKey;
        this.portraitOverride = portraitOverride;
        this.speakerNameOverride = speakerNameOverride;
        this.portraitSlot = portraitSlot;
        this.backgroundOverride = backgroundOverride;
        this.autoAdvanceSecondsOverride = Mathf.Max(
            0f,
            autoAdvanceSecondsOverride);
    }
}

[CreateAssetMenu(
    fileName = "GalSequence_",
    menuName = "Project ANOIR/Gal/Sequence")]
public sealed class GalSequenceSO : ScriptableObject
{
    [SerializeField] private string sequenceId;
    [SerializeField] private GalPresentationMode defaultPresentationMode =
        GalPresentationMode.PausedOverlay;
    [SerializeField] private Sprite defaultBackground;
    [SerializeField] private List<GalLine> lines = new();

    public string SequenceId => sequenceId ?? string.Empty;
    public GalPresentationMode DefaultPresentationMode => defaultPresentationMode;
    public Sprite DefaultBackground => defaultBackground;
    public IReadOnlyList<GalLine> Lines => lines;
    public bool HasLines => lines != null && lines.Count > 0;
}
