using Sirenix.OdinInspector;
using System;

[System.Serializable]
public class CharacterAnimationQueuedClip
{
    [HorizontalGroup("Queued", Width = 220)]
    [LabelWidth(30)]
    public CharacterAnimationClip clip;

    [HorizontalGroup("Queued", Width = 70)]
    [LabelWidth(35)]
    public float delay = 0f;

    public CharacterAnimationQueuedClip Clone()
    {
        CharacterAnimationQueuedClip copy = new CharacterAnimationQueuedClip();

        copy.delay = delay;

        if (clip != null)
        {
            copy.clip = clip.Clone();
        }

        return copy;
    }
}