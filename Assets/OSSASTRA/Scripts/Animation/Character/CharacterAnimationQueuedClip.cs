/* AM02：迁自旧代码原实现；保留作者字段和分组，生产类型/GUID独立。 */
namespace Ossastra.CharacterAnimation
{
using CharacterRoot = Ossastra.Characters.CharacterRoot;
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
    [NonSerialized] public bool runtimeAfterPreviousEnd;

    public CharacterAnimationQueuedClip Clone()
    {
        CharacterAnimationQueuedClip copy = new CharacterAnimationQueuedClip();

        copy.delay = delay;
        copy.runtimeAfterPreviousEnd = runtimeAfterPreviousEnd;

        if (clip != null)
        {
            copy.clip = clip.Clone();
        }

        return copy;
    }
}
}
