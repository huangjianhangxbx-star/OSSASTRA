using UnityEngine;

public sealed class GalSequencePlayer
{
    private GalSequenceSO activeSequence;
    private int currentLineIndex = -1;

    public bool HasActiveSequence => activeSequence != null;
    public int CurrentLineIndex => currentLineIndex;

    public bool TryBegin(
        GalSequenceSO sequence,
        out GalLineViewModel firstLine)
    {
        firstLine = default;

        if (sequence == null || !sequence.HasLines)
        {
            Clear();
            return false;
        }

        activeSequence = sequence;
        currentLineIndex = 0;
        firstLine = ResolveLine(
            activeSequence,
            activeSequence.Lines[currentLineIndex],
            currentLineIndex);
        return true;
    }

    public bool TryAdvance(out GalLineViewModel nextLine)
    {
        nextLine = default;

        if (activeSequence == null)
            return false;

        int nextIndex = currentLineIndex + 1;
        if (nextIndex >= activeSequence.Lines.Count)
        {
            Clear();
            return false;
        }

        currentLineIndex = nextIndex;
        nextLine = ResolveLine(
            activeSequence,
            activeSequence.Lines[currentLineIndex],
            currentLineIndex);
        return true;
    }

    public void Clear()
    {
        activeSequence = null;
        currentLineIndex = -1;
    }

    private static GalLineViewModel ResolveLine(
        GalSequenceSO sequence,
        GalLine line,
        int lineIndex)
    {
        Sprite background = line != null && line.BackgroundOverride != null
            ? line.BackgroundOverride
            : sequence != null
                ? sequence.DefaultBackground
                : null;

        if (line == null)
        {
            return new GalLineViewModel(
                lineIndex,
                string.Empty,
                string.Empty,
                null,
                GalPortraitSlot.Left,
                background,
                0f);
        }

        GalActorSO actor = line.Actor;
        string speakerName = !string.IsNullOrWhiteSpace(line.SpeakerNameOverride)
            ? line.SpeakerNameOverride
            : actor != null
                ? actor.DisplayName
                : string.Empty;

        Sprite portrait = line.PortraitOverride != null
            ? line.PortraitOverride
            : actor != null
                ? actor.ResolvePortrait(line.PortraitVariantKey)
                : null;

        return new GalLineViewModel(
            lineIndex,
            speakerName,
            line.Text,
            portrait,
            line.PortraitSlot,
            background,
            line.AutoAdvanceSecondsOverride);
    }
}
