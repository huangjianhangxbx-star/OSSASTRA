using Spine;
using Spine.Unity;
using System.Collections.Generic;
using UnityEngine;

public class AnimationHandleSystem : MonoBehaviour, ICharacterSubsystem
{
    private CharacterRoot characterRoot;
    private SkeletonAnimation skeletonAnimation;

    public CharacterAnimationPlan currentPlan;
    private bool _isInitialized;

    private readonly Dictionary<int, TrackRuntimeInfo> trackRuntimeInfos =
    new Dictionary<int, TrackRuntimeInfo>();

    private class TrackRuntimeInfo
    {
        public string animName;
        public bool loop;
        public AnimationIntentSourceStateMachine sourceStateMachine;
        public int replayId;

        public string queuedAnimName;
        public bool queuedLoop;
        public AnimationIntentSourceStateMachine queuedSource;
        public int queuedReplayId;
    }

    private void Awake()
    {
        EventCenter.Instance.AddEventListener<CharacterAnimationPlan>(
            GameEvent.AnimationPlanRequest,
            OnAnimationPlanReceived
        );
    }

    private void OnDestroy()
    {
        if (EventCenter.Instance == null)
            return;

        EventCenter.Instance.RemoveEventListener<CharacterAnimationPlan>(
            GameEvent.AnimationPlanRequest,
            OnAnimationPlanReceived
        );
    }

    public void Initialize(CharacterRoot root)
    {
        if (_isInitialized)
            return;
        characterRoot = root;
        skeletonAnimation = root.skeletonAnimation;
        _isInitialized = true;
        Debug.Log($"[AnimHandle] skeleton={skeletonAnimation.name}, id={skeletonAnimation.GetInstanceID()}", this);
    }

    private void OnAnimationPlanReceived(CharacterAnimationPlan plan)
    {
        //1.收集 used tracks
        //2.处理 clearTracks
        //3.清 stale tracks
        //4.Play clips
        //5.Queue queuedClips
        //6.Apply skeleton

        if (plan == null)
        {
            Debug.LogWarning($"[{name}] 收到空动画计划");
            return;
        }

        if (plan.characterRoot != characterRoot)
            return;

        if (skeletonAnimation == null)
            return;

        ValidatePlan(plan);
        currentPlan = plan;

        Spine.AnimationState state = skeletonAnimation.AnimationState;

        HashSet<int> tracksUsedThisFrame = new HashSet<int>();
        HashSet<int> tracksExplicitlyCleared = new HashSet<int>();

        // 1. 收集计划中明确要播放的轨道
        if (plan.clips != null)
        {
            for (int i = 0; i < plan.clips.Count; i++)
            {
                CharacterAnimationClip clip = plan.clips[i];

                if (clip == null)
                    continue;

                if (clip.track < 0)
                    continue;

                tracksUsedThisFrame.Add(clip.track);
            }
        }

        // 2. 收集计划中明确要排队播放的轨道
        if (plan.queuedClips != null)
        {
            for (int i = 0; i < plan.queuedClips.Count; i++)
            {
                CharacterAnimationQueuedClip queuedClip = plan.queuedClips[i];

                if (queuedClip == null || queuedClip.clip == null)
                    continue;

                CharacterAnimationClip clip = queuedClip.clip;

                if (clip.track < 0)
                    continue;

                tracksUsedThisFrame.Add(clip.track);
            }
        }

        // 3. 收集显式清空轨道
        if (plan.clearTracks != null)
        {
            for (int i = 0; i < plan.clearTracks.Count; i++)
            {
                int track = plan.clearTracks[i];

                if (track < 0)
                    continue;

                if (track == 0)
                {
                    Debug.LogWarning(
                        $"[{name}] 试图 ClearTrack(0)，Track 0 是基础全身层，不应清空。",
                        this
                    );
                    continue;
                }

                tracksExplicitlyCleared.Add(track);
            }
        }

        // 4. 处理显式要清空的轨道
        foreach (int track in tracksExplicitlyCleared)
        {
            ClearTrackImmediate(track);
        }

        // 5. 清理 stale tracks
        List<int> staleTracks = null;

        foreach (KeyValuePair<int, TrackRuntimeInfo> pair in trackRuntimeInfos)
        {
            int track = pair.Key;

            if (track == 0)
                continue;

            if (tracksUsedThisFrame.Contains(track))
                continue;

            if (tracksExplicitlyCleared.Contains(track))
                continue;

            staleTracks ??= new List<int>();
            staleTracks.Add(track);
        }

        if (staleTracks != null)
        {
            for (int i = 0; i < staleTracks.Count; i++)
            {
                ClearTrackImmediate(staleTracks[i]);
            }
        }

        // 6. SetAnimation
        if (plan.clips != null)
        {
            for (int i = 0; i < plan.clips.Count; i++)
            {
                PlayClipInternal(plan.clips[i]);
            }
        }

        // 7. AddAnimation
        if (plan.queuedClips != null)
        {
            for (int i = 0; i < plan.queuedClips.Count; i++)
            {
                QueueClipInternal(plan.queuedClips[i]);
            }
        }

        skeletonAnimation.AnimationState.Apply(skeletonAnimation.Skeleton);
    }


    private void ValidatePlan(CharacterAnimationPlan plan)
    {
        if (plan == null)
            return;

        // 1. 警告：清 Track 0
        if (plan.clearTracks != null)
        {
            for (int i = 0; i < plan.clearTracks.Count; i++)
            {
                int track = plan.clearTracks[i];

                if (track == 0)
                {
                    Debug.LogWarning(
                        $"[AnimationHandleSystem] 危险操作：FinalPlan 包含 Clear Track 0，Track 0 是基础全身层，通常不应该被清空。",
                        this
                    );
                }
            }
        }

        // 2. 警告：Movement 动画放到 Track 0 以外
        if (plan.clips != null)
        {
            for (int i = 0; i < plan.clips.Count; i++)
            {
                CharacterAnimationClip clip = plan.clips[i];

                if (clip == null)
                    continue;

                if (!string.IsNullOrEmpty(clip.animaName) &&
                    clip.animaName.StartsWith("Movement/") &&
                    clip.track != 0)
                {
                    Debug.LogWarning(
                        $"[AnimationHandleSystem] 危险操作：移动动画 {clip.animaName} 被放到 Track {clip.track}，通常所有 Movement 动画应该使用 Track 0。",
                        this
                    );
                }
            }
        }
    }



    private void PlayClipInternal(CharacterAnimationClip clip)
    {
        if (clip == null)
            return;

        if (string.IsNullOrEmpty(clip.animaName))
        {
            Debug.LogWarning($"[{name}] 收到空动画名");
            return;
        }

        if (clip.track < 0)
        {
            Debug.LogWarning($"[{name}] 动画 Track 不合法: {clip.track}");
            return;
        }

        Spine.AnimationState state = skeletonAnimation.AnimationState;

        int track = clip.track;

        bool sameRuntimeCommand = false;

        if (trackRuntimeInfos.TryGetValue(track, out TrackRuntimeInfo info))
        {
            sameRuntimeCommand =
                info.animName == clip.animaName &&
                info.loop == clip.loop &&
                info.sourceStateMachine == clip.runtimeSourceStateMachine &&
                info.replayId == clip.runtimeReplayId;
        }

        TrackEntry currentTrack = state.GetTrack(track);

        bool spineActuallySame =
            currentTrack != null &&
            currentTrack.Animation != null &&
            currentTrack.Animation.Name == clip.animaName &&
            currentTrack.Loop == clip.loop;

        // 关键：
        // 同一个 Track、同一个动画、同一个来源、同一个 replayId，
        // 说明只是 Plan 重新分发，不需要重新播放。
        if (sameRuntimeCommand && spineActuallySame)
        {
            currentTrack.MixDuration = clip.mixDuration;
            currentTrack.Additive = clip.isAdditive;
            return;
        }

        // replayId 不同，或者动画不同，都需要 SetAnimation
        TrackEntry trackEntry = state.SetAnimation(
            track,
            clip.animaName,
            clip.loop
        );

        trackEntry.MixDuration = clip.mixDuration;
        trackEntry.Additive = clip.isAdditive;

        trackRuntimeInfos[track] = new TrackRuntimeInfo
        {
            animName = clip.animaName,
            loop = clip.loop,
            sourceStateMachine = clip.runtimeSourceStateMachine,
            replayId = clip.runtimeReplayId,
            queuedAnimName = null,
            queuedLoop = false,
            queuedSource = AnimationIntentSourceStateMachine.None,
            queuedReplayId = 0
        };
    }

    public TrackEntry PlayPrepAnimation(CharacterAnimationClip clip, System.Action onPrepComplete)
    {
        if (clip == null || string.IsNullOrEmpty(clip.animaName) || skeletonAnimation == null)
            return null;

        Spine.AnimationState state = skeletonAnimation.AnimationState;

        TrackEntry trackEntry = state.SetAnimation(
            clip.track,
            clip.animaName,
            clip.loop
        );

        trackEntry.MixDuration = clip.mixDuration;
        trackEntry.Additive = clip.isAdditive;

        trackRuntimeInfos[clip.track] = new TrackRuntimeInfo
        {
            animName = clip.animaName,
            loop = clip.loop,
            sourceStateMachine = clip.runtimeSourceStateMachine,
            replayId = clip.runtimeReplayId,
            queuedAnimName = null,
            queuedLoop = false,
            queuedSource = AnimationIntentSourceStateMachine.None,
            queuedReplayId = 0
        };

        trackEntry.Complete += _ =>
        {
            onPrepComplete?.Invoke();
        };

        return trackEntry;
    }

    private void DebugPrintTracks()
    {
        if (skeletonAnimation == null)
            return;

        Spine.AnimationState state = skeletonAnimation.AnimationState;

        for (int i = 0; i < 4; i++)
        {
            TrackEntry entry = state.GetTrack(i);

            string animName = entry != null && entry.Animation != null
                ? entry.Animation.Name
                : "NULL";

            Debug.Log(
                $"[Spine Track Debug] Track {i}: {animName}, " +
                $"Loop={(entry != null ? entry.Loop.ToString() : "-")}, " +
                $"TrackTime={(entry != null ? entry.TrackTime.ToString("F2") : "-")}",
                this
            );
        }
    }

    private void ClearTrackImmediate(int track)
    {
        if (skeletonAnimation == null)
            return;

        if (track <= 0)
            return;

        Spine.AnimationState state = skeletonAnimation.AnimationState;

        TrackEntry current = state.GetTrack(track);

        bool hasRuntimeInfo = trackRuntimeInfos.ContainsKey(track);

        // 如果 Spine 层已经没有动画，且记录也没有，
        // 就不需要重复 SetEmptyAnimation。
        if (current == null && !hasRuntimeInfo)
            return;

        TrackEntry empty = state.SetEmptyAnimation(track, 0f);

        if (empty != null)
        {
            empty.TrackEnd = 0f;
        }

        state.Update(0f);
        state.Apply(skeletonAnimation.Skeleton);

        trackRuntimeInfos.Remove(track);
    }

    private void QueueClipInternal(CharacterAnimationQueuedClip queuedClip)
    {
        if (queuedClip == null || queuedClip.clip == null)
            return;

        CharacterAnimationClip clip = queuedClip.clip;

        if (string.IsNullOrEmpty(clip.animaName))
        {
            Debug.LogWarning($"[{name}] 收到空排队动画名");
            return;
        }

        if (clip.track < 0)
        {
            Debug.LogWarning($"[{name}] 排队动画 Track 不合法: {clip.track}");
            return;
        }

        Spine.AnimationState state = skeletonAnimation.AnimationState;

        // 去重：如果同一轨道上已经排队了相同的动画（同名、同 loop、同来源、同 replayId），
        // 就不再重复 AddAnimation，避免多次排队导致动画被重启。
        if (trackRuntimeInfos.TryGetValue(clip.track, out TrackRuntimeInfo qInfo))
        {
            bool sameQueued =
                qInfo.queuedAnimName == clip.animaName &&
                qInfo.queuedLoop == clip.loop &&
                qInfo.queuedSource == clip.runtimeSourceStateMachine &&
                qInfo.queuedReplayId == clip.runtimeReplayId;

            if (sameQueued)
                return;
        }

        TrackEntry trackEntry = state.AddAnimation(
            clip.track,
            clip.animaName,
            clip.loop,
            queuedClip.delay
        );

        trackEntry.MixDuration = clip.mixDuration;
        trackEntry.Additive = clip.isAdditive;

        // 记录已排队的动画信息，供下次去重判断使用。
        if (!trackRuntimeInfos.TryGetValue(clip.track, out TrackRuntimeInfo qRec))
        {
            qRec = new TrackRuntimeInfo();
            trackRuntimeInfos[clip.track] = qRec;
        }
        qRec.queuedAnimName = clip.animaName;
        qRec.queuedLoop = clip.loop;
        qRec.queuedSource = clip.runtimeSourceStateMachine;
        qRec.queuedReplayId = clip.runtimeReplayId;
    }
}