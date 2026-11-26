/* 保存角色动画的主轨、移动替换与排队片段，克隆时保持各片段独立的来源和播放参数。 */
namespace Ossastra.CharacterAnimation
{
using CharacterRoot = Ossastra.Characters.CharacterRoot;
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

    [FoldoutGroup("移动时的循环轨道", false)]
    [Tooltip("持续动作移动时使用的整组循环轨道。留空沿用正常轨道。")]
    public CharacterAnimationClip[] movingLoopClips = System.Array.Empty<CharacterAnimationClip>();

    [FoldoutGroup("Queued Clips", false)]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 5)]
    public List<CharacterAnimationQueuedClip> queuedClips = new List<CharacterAnimationQueuedClip>();

    [FoldoutGroup("Clear Tracks", false)]
    [Tooltip("播放剪辑之前需要清空的 Spine Track，例如混合动画时清空 Track 1")]
    [ValueDropdown("@CharacterAnimationPlan.GetTrackOptions()", IsUniqueList = true)]
    public List<int> clearTracks = new List<int>();

    [HideInInspector] public CharacterRoot characterRoot;
    [System.NonSerialized] public EAnimationLogicState runtimeLogicId = EAnimationLogicState.None;
    [System.NonSerialized] public AnimationIntentSourceStateMachine runtimeSource;
    [System.NonSerialized] public EAnimSemantic runtimeSemantic;

    public static IEnumerable<ValueDropdownItem<int>> GetTrackOptions()
    {
        for (int i = 0; i <= 9; i++)
            yield return new ValueDropdownItem<int> { Text = i.ToString(), Value = i };
    }

    public CharacterAnimationPlan CloneFor(CharacterRoot root)
    {
        CharacterAnimationPlan copyPlan = new CharacterAnimationPlan();

        copyPlan.characterRoot = root;
        copyPlan.runtimeLogicId = runtimeLogicId;
        copyPlan.runtimeSource = runtimeSource;
        copyPlan.runtimeSemantic = runtimeSemantic;

        if (movingLoopClips != null && movingLoopClips.Length > 0)
        {
            copyPlan.movingLoopClips = System.Array.ConvertAll(movingLoopClips, source =>
            {
                var clip = source?.Clone();
                if (clip != null) clip.characterRoot = root;
                return clip;
            });
        }

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

    public bool HasOperations => (clips != null && clips.Exists(c => c != null && c.track >= 0 && !string.IsNullOrEmpty(c.animaName)))
        || (clearTracks != null && clearTracks.Exists(t => t >= 0))
        || (queuedClips != null && queuedClips.Exists(q => q != null && q.clip != null && q.clip.track >= 0 && !string.IsNullOrEmpty(q.clip.animaName)));
    public IEnumerable<CharacterAnimationClip> EnumerateClips()
    {
        if (clips != null)
            foreach (var clip in clips)
                if (clip != null) yield return clip;
        if (movingLoopClips != null)
            foreach (var clip in movingLoopClips)
                if (clip != null) yield return clip;
        if (queuedClips != null)
            foreach (var item in queuedClips)
                if (item?.clip != null) yield return item.clip;
    }

    internal void SetMappingSource(ScriptableObject mapping, EAnimationLogicState logic)
    {
        foreach (var clip in EnumerateClips())
        {
            clip.runtimeMapping = mapping;
            clip.runtimeMappingLogic = logic;
        }
    }

#if UNITY_EDITOR
    internal void EnsureClipIdentities(HashSet<string> used)
    {
        foreach (var clip in EnumerateClips())
        {
            if (!string.IsNullOrEmpty(clip.eventClipId) && used.Add(clip.eventClipId)) continue;
            // 排序与改名时保留原标识；新增或复制的重复条目取得新标识。
            clip.eventClipId = System.Guid.NewGuid().ToString("N");
            used.Add(clip.eventClipId);
        }
    }
#endif

    public void SetSkeletonDataAsset(SkeletonDataAsset skeletonDataAsset)
    {
        if (movingLoopClips != null)
            foreach (var clip in movingLoopClips)
                if (clip != null) clip.skeletonDataAsset = skeletonDataAsset;
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
}
