using Sirenix.OdinInspector;
using Spine.Unity;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CharacterAnimationPlan
{
    [FoldoutGroup("Clips", false)]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 5)]
    public List<CharacterAnimationClip> clips = new List<CharacterAnimationClip>();

    [FoldoutGroup("Queued Clips", false)]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 5)]
    public List<CharacterAnimationQueuedClip> queuedClips = new List<CharacterAnimationQueuedClip>();

    [FoldoutGroup("Clear Tracks", false)]
    [Tooltip("播放剪辑之前需要清空的 Spine Track，例如混合动画时清空 Track 1")]
    [ValueDropdown("@CharacterAnimationPlan.GetTrackOptions()", IsUniqueList = true)]
    public List<int> clearTracks = new List<int>();

    [HideInInspector] public CharacterRoot characterRoot;

    public static IEnumerable<ValueDropdownItem<int>> GetTrackOptions()
    {
        for (int i = 0; i <= 9; i++)
            yield return new ValueDropdownItem<int> { Text = i.ToString(), Value = i };
    }

    public CharacterAnimationPlan CloneFor(CharacterRoot root)
    {
        CharacterAnimationPlan copyPlan = new CharacterAnimationPlan();

        copyPlan.characterRoot = root;

        if (clips != null)
        {
            for (int i = 0; i < clips.Count; i++)
            {
                CharacterAnimationClip sourceClip = clips[i];

                if (sourceClip == null)
                    continue;

                CharacterAnimationClip clipCopy = sourceClip.Clone();
                clipCopy.characterRoot = root;

                copyPlan.clips.Add(clipCopy);
            }
        }

        if (queuedClips != null)
        {
            for (int i = 0; i < queuedClips.Count; i++)
            {
                CharacterAnimationQueuedClip sourceQueuedClip = queuedClips[i];

                if (sourceQueuedClip == null)
                    continue;

                CharacterAnimationQueuedClip queuedCopy = sourceQueuedClip.Clone();

                if (queuedCopy.clip != null)
                    queuedCopy.clip.characterRoot = root;

                copyPlan.queuedClips.Add(queuedCopy);
            }
        }

        if (clearTracks != null)
        {
            for (int i = 0; i < clearTracks.Count; i++)
            {
                copyPlan.clearTracks.Add(clearTracks[i]);
            }
        }

        return copyPlan;
    }

    public void SetSkeletonDataAsset(SkeletonDataAsset skeletonDataAsset)
    {
        if (clips != null)
        {
            for (int i = 0; i < clips.Count; i++)
            {
                if (clips[i] != null)
                {
                    clips[i].skeletonDataAsset = skeletonDataAsset;
                }
            }
        }

        if (queuedClips != null)
        {
            for (int i = 0; i < queuedClips.Count; i++)
            {
                if (queuedClips[i] != null && queuedClips[i].clip != null)
                {
                    queuedClips[i].clip.skeletonDataAsset = skeletonDataAsset;
                }
            }
        }
    }
}