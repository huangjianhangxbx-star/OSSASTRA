using PixelCrushers.QuestMachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class CharacterAnimationComposer : MonoBehaviour, ICharacterSubsystem,ICharacterTickable
{
    private CharacterRoot characterRoot;
    private CharacterAnimationConfigManager configManager;

    private readonly Dictionary<AnimationIntentSourceStateMachine, AnimationIntent> activeIntents =
        new Dictionary<AnimationIntentSourceStateMachine, AnimationIntent>();

    private readonly Dictionary<AnimationIntentSourceStateMachine, EAnimationLogicState> lastRoutedLogicBySource =
    new Dictionary<AnimationIntentSourceStateMachine, EAnimationLogicState>();
    private AnimationTransitionRouter transitionRouter = new AnimationTransitionRouter();
    private CharacterAnimationPlan currentOutputPlan;
    public CharacterAnimationPlan CurrentOutputPlan => currentOutputPlan;
    private bool _isInitialized;
    [Header("Debug")]
    public bool debugCompose = true;
    public bool debugEveryRebuild = true;
    private List<ResolvedAnimationPlan> lastResolvedPlans;

    public enum TrackOperationType
    {
        None,
        Play,
        Clear
    }

    public void Initialize(CharacterRoot root)
    {
        if (_isInitialized)
            return;

        characterRoot = root;
        configManager = root.characterAnimationConfigManager;

        _isInitialized = true;
    }

    private class TrackOperation
    {
        public TrackOperationType type;

        public int track;
        public int priority;

        public CharacterAnimationClip clip;

        public AnimationIntentSourceStateMachine sourceStateMachine;
        public int replayId;
    }

    public void ManagedUpdate(float deltaTime)
    {
        RemoveExpiredOneShotIntents();
    }

    private void TryApplyTrackOperation(Dictionary<int, TrackOperation> trackOperations,TrackOperation newOperation)
    {
        if (newOperation == null)
            return;

        if (newOperation.track < 0)
            return;

        int track = newOperation.track;

        if (!trackOperations.TryGetValue(track, out TrackOperation current))
        {
            trackOperations[track] = newOperation;
            return;
        }

        if (ShouldReplaceTrackOperation(current, newOperation))
        {
            trackOperations[track] = newOperation;
        }
    }
    private bool ShouldReplaceTrackOperation(
        TrackOperation current,
       TrackOperation candidate
    )
    {
        if (candidate == null)
            return false;

        if (current == null)
            return true;

        // 1. ���ȼ�����Ӯ
        if (candidate.priority > current.priority)
            return true;

        if (candidate.priority < current.priority)
            return false;

        // 2. ͬ���ȼ�ʱ��Play ������ Clear
        // ����ͬһ����ϣ�ͬ���ȼ� Clear �� Play �����
        if (candidate.type == TrackOperationType.Play &&
            current.type == TrackOperationType.Clear)
        {
            return true;
        }

        if (candidate.type == TrackOperationType.Clear &&
            current.type == TrackOperationType.Play)
        {
            return false;
        }

        // 3. ͬ���ȼ���ͬ����ʱ�������ĸ���ǰ��ġ�
        // ����ͬһ��Դ���� resolve �����Ľ�����Ը��Ǿɽ����
        return true;
    }

    public void SubmitIntent(AnimationIntent intent)
    {
        if (intent == null)
            return;

        if (characterRoot == null)
        {
            Debug.LogWarning("[AnimationComposer] characterRoot == null");
            return;
        }

        if (intent.root != characterRoot)
        {
            Debug.LogWarning(
                $"[AnimationComposer] root ��ƥ�䣬intent.root={intent.root?.name}, composer.root={characterRoot.name}"
            );
            return;
        }

        if ( intent.intentSourceStateMatine == AnimationIntentSourceStateMachine.None)
        {
            Debug.LogWarning("[AnimationComposer] AnimationIntent source Ϊ��");
            return;
        }
        //������intent != null
        //intent.root == characterRoot
        //intent.source �ǿգ�Ȼ��Ϳ�ʼClone��

        AnimationIntent copy = intent.Clone();
        /*
* �Է� oneShot �ĳ�����ͼ��ȥ�ء�
* ͬ��Դ��ͬ���塢ͬ���ȼ���ͬ replayId �ĳ�����ͼ������Ҫ�ظ� Rebuild��
*/
        if (!copy.oneShot &&
            activeIntents.TryGetValue(copy.intentSourceStateMatine, out AnimationIntent existing) &&
            IsSamePersistentIntent(existing, copy))
        {
            return;
        }

        if (copy.oneShot && copy.duration > 0f)
        {
            copy.expireTime = Time.time + copy.duration;
        }
        else
        {
            copy.expireTime = 0f;
        }

        activeIntents[copy.intentSourceStateMatine] = copy;
        //Clone �������ǣ�״̬���ύ���Ժ�Composer �����Լ��ĸ����������ⲿ���󱻸ġ�
        //��intent��oneShot���ԣ�������ǹ���


        RebuildAndDispatch();
    }

    private bool IsSamePersistentIntent(AnimationIntent a, AnimationIntent b)
    {
        if (a == null || b == null)
            return false;

        if (a.oneShot || b.oneShot)
            return false;

        return
            a.root == b.root &&
            a.intentSourceStateMatine == b.intentSourceStateMatine &&
            a.semantic == b.semantic &&
            a.layer == b.layer &&
            a.priority == b.priority &&
            a.exclusiveFullBody == b.exclusiveFullBody &&
            a.replayId == b.replayId;
    }

    public void RemoveIntent(AnimationIntentSourceStateMachine source)
    {
        if (source == AnimationIntentSourceStateMachine.None)
            return;

        if (activeIntents.Remove(source))
        {
            RebuildAndDispatch();
        }
    }

    public void ClearAllIntents()
    {
        if (activeIntents.Count <= 0)
            return;

        activeIntents.Clear();
        RebuildAndDispatch();
    }

    public AnimationIntent GetIntent(AnimationIntentSourceStateMachine source)
    {
        if (source == AnimationIntentSourceStateMachine.None)
            return null;


        activeIntents.TryGetValue(source, out AnimationIntent intent);
        return intent;
    }

    private void RemoveExpiredOneShotIntents()
    {
        if (activeIntents.Count <= 0)
            return;

        List<AnimationIntentSourceStateMachine> removeList = null;

        foreach (KeyValuePair<AnimationIntentSourceStateMachine, AnimationIntent> pair in activeIntents)
        {
            AnimationIntent intent = pair.Value;

            if (intent == null)
                continue;

            if (!intent.oneShot)
                continue;

            if (intent.expireTime <= 0f)
                continue;

            if (Time.time < intent.expireTime)
                continue;

            removeList ??= new List<AnimationIntentSourceStateMachine>();
            removeList.Add(pair.Key);
        }

        if (removeList == null)
            return;

        for (int i = 0; i < removeList.Count; i++)
        {
            activeIntents.Remove(removeList[i]);
        }

        RebuildAndDispatch();
    }

    private void RebuildAndDispatch()//����
    {
        CharacterAnimationPlan finalPlan = ComposeFinalPlan();


        if (finalPlan == null)
        {
        //    //Composer ��Ҫ��Ϊ finalPlan == null �Ͳ��ɷ�
        //    ���ٷ�һ���� plan��
            finalPlan = new CharacterAnimationPlan();
            finalPlan.characterRoot = characterRoot;
        }

        currentOutputPlan = finalPlan;

        EventCenter.Instance.EventTrigger<CharacterAnimationPlan>(
            GameEvent.AnimationPlanRequest,
            finalPlan
        );
    }

    private CharacterAnimationPlan ComposeFinalPlan()//�ѵ�ǰ���� active intents ���ºϳ�һ��
    {
        if (characterRoot == null || configManager == null)
            return null;
        AnimationComposeContext context = new AnimationComposeContext//�ȹ��������ģ���ϵ�ǰ�ֵ��д洢����������
                                                                     //��� context ��·�����á�
        {
            root = characterRoot,
            dataBus = characterRoot.characterDataBus,
            activeIntents = activeIntents
        };

        List<ResolvedAnimationPlan> resolvedPlans = new List<ResolvedAnimationPlan>();//newһ������plan

        foreach (AnimationIntent intent in activeIntents.Values)//����ͼ�����������ɹ���spine�ļ����ö����Ķ����߼�ID
        {
            //��ÿ�� intent����·�ɣ�����������Ķ����汾������Movement + Jog + HasIntent(AutoAttack)=> Jog_LowerOnly_Attack

            if (intent == null)
                continue;
            // ·�ɺ�Ľ��������ɲ��Ŷ���ID
            EAnimationLogicState routedLogicID =
             configManager.RouteIntent(intent, context);

            if (routedLogicID == EAnimationLogicState.None)
                continue;

            //�ص�ComposeFinalPlan()������ Plan����ʱ���õ��õ� ·�ɺ�ľ��嶯���߼�ID
            //������߼� ID ��ɾ���ɲ��ŵĶ������á�����״̬/����/basemap��ӳ��
            CharacterAnimationPlan rawPlan =
                configManager.ResolvePlan(routedLogicID);

            lastRoutedLogicBySource.TryGetValue(intent.intentSourceStateMatine,out EAnimationLogicState previousLogicState);

            rawPlan = transitionRouter.BuildTransitionedPlan(
    intent,
    previousLogicState,
    routedLogicID,
    rawPlan,
    context,
    configManager
            );

            if (rawPlan == null)
            {
                Debug.LogWarning(
                    $"[AnimationComposer] ResolvePlan ʧ��: source={intent.intentSourceStateMatine}, inputSemantic={intent.semantic}, routed={routedLogicID}"
                );
                continue;
            }

            // �ؼ�����Ҫֱ��ʹ�������ʲ���� Plan
            CharacterAnimationPlan plan = ClonePlan(rawPlan);

            // ReplayId ģʽ�£�����ͨ�� ClearTrack ��ǿ���ز���
            // �Ƿ��ز����� AnimationHandleSystem ���� clip.runtimeReplayId �жϡ�
            resolvedPlans.Add(new ResolvedAnimationPlan
            {
                resolvedIndex = resolvedPlans.Count,
                //������ص��ǣ�Plan �����������ã��� source / replayId / priority ������� Intent��
                sourceStateMachine = intent.intentSourceStateMatine,
                inputSemantic = intent.semantic,
                routedLogicID = routedLogicID,
                priority = intent.priority,
                replayId = intent.replayId,
                exclusiveFullBody = intent.exclusiveFullBody,
                plan = plan
            });
            lastRoutedLogicBySource[intent.intentSourceStateMatine] = routedLogicID;
        }
        if (resolvedPlans.Count <= 0)
            return null;

        lastResolvedPlans = resolvedPlans;

        CharacterAnimationPlan finalPlan = MergePlans(resolvedPlans);//�������� Plan

        if (debugCompose && debugEveryRebuild)
        {
            Debug.Log(BuildComposeDebugString(resolvedPlans, finalPlan), this);
        }

        return finalPlan;
    }
    private CharacterAnimationPlan ClonePlan(CharacterAnimationPlan source)
    {
        if (source == null)
            return null;

        CharacterAnimationPlan copy = new CharacterAnimationPlan();
        copy.characterRoot = characterRoot;

        if (source.clearTracks != null)
        {
            for (int i = 0; i < source.clearTracks.Count; i++)
            {
                copy.clearTracks.Add(source.clearTracks[i]);
            }
        }

        if (source.clips != null)
        {
            for (int i = 0; i < source.clips.Count; i++)
            {
                CharacterAnimationClip clip = source.clips[i];

                if (clip == null)
                    continue;

                CharacterAnimationClip clipCopy = clip.Clone();
                clipCopy.characterRoot = characterRoot;
                copy.clips.Add(clipCopy);
            }
        }

        if (source.queuedClips != null)
        {
            for (int i = 0; i < source.queuedClips.Count; i++)
            {
                CharacterAnimationQueuedClip queued = source.queuedClips[i];

                if (queued == null)
                    continue;

                CharacterAnimationQueuedClip queuedCopy = queued.Clone();

                if (queuedCopy.clip != null)
                    queuedCopy.clip.characterRoot = characterRoot;

                copy.queuedClips.Add(queuedCopy);
            }
        }

        return copy;
    }




    private string BuildComposeDebugString(List<ResolvedAnimationPlan> resolvedPlans,CharacterAnimationPlan finalPlan)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        sb.AppendLine("========== [AnimationComposer Debug] ==========");

        sb.AppendLine($"Character: {(characterRoot != null ? characterRoot.name : "NULL")}");
        sb.AppendLine($"Time: {Time.time:F2}");

        sb.AppendLine();
        sb.AppendLine("---- Active Intents ----");

        if (activeIntents == null || activeIntents.Count <= 0)
        {
            sb.AppendLine("  <none>");
        }
        else
        {
            foreach (KeyValuePair<AnimationIntentSourceStateMachine, AnimationIntent> pair in activeIntents)
            {
                AnimationIntent intent = pair.Value;

                if (intent == null)
                {
                    sb.AppendLine($"  {pair.Key}: NULL");
                    continue;
                }

                sb.AppendLine(
                    $"  Source={intent.intentSourceStateMatine}, Semantic={intent.semantic}, " +
                    $"Priority={intent.priority}, OneShot={intent.oneShot}, " +

                        $"ReplayId={intent.replayId}"

                );
            }
        }

        sb.AppendLine();
        sb.AppendLine("---- Routed / Resolved Plans ----");

        if (resolvedPlans == null || resolvedPlans.Count <= 0)
        {
            sb.AppendLine("  <none>");
        }
        else
        {
            for (int i = 0; i < resolvedPlans.Count; i++)
            {
                ResolvedAnimationPlan resolved = resolvedPlans[i];

                if (resolved == null)
                {
                    sb.AppendLine($"  [{i}] NULL");
                    continue;
                }

                sb.AppendLine(
                    $"  [{i}] Source={resolved.sourceStateMachine}, " +
                    $"Input={resolved.inputSemantic}, " +
                    $"Routed={resolved.routedLogicID}, " +
                    $"Priority={resolved.priority}"
                );

                CharacterAnimationPlan plan = resolved.plan;

                if (plan == null)
                {
                    sb.AppendLine("      Plan=NULL");
                    continue;
                }

                if (plan.clearTracks != null && plan.clearTracks.Count > 0)
                {
                    sb.Append("      ClearTracks: ");

                    for (int j = 0; j < plan.clearTracks.Count; j++)
                    {
                        sb.Append(plan.clearTracks[j]);

                        if (j < plan.clearTracks.Count - 1)
                            sb.Append(", ");
                    }

                    sb.AppendLine();
                }
                else
                {
                    sb.AppendLine("      ClearTracks: <empty>");
                }

                if (plan.clips != null && plan.clips.Count > 0)
                {
                    for (int j = 0; j < plan.clips.Count; j++)
                    {
                        CharacterAnimationClip clip = plan.clips[j];

                        if (clip == null)
                        {
                            sb.AppendLine($"      Clip[{j}]: NULL");
                            continue;
                        }

                        sb.AppendLine(
                            $"      Clip[{j}]: Track={clip.track}, " +
                            $"Anim={clip.animaName}, Loop={clip.loop}, " +
                            $"Mix={clip.mixDuration}, Additive={clip.isAdditive}"
                        );
                    }
                }
                else
                {
                    sb.AppendLine("      Clips: <empty>");
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine("---- Final Plan ----");


        if (finalPlan.queuedClips != null && finalPlan.queuedClips.Count > 0)
        {
            for (int i = 0; i < finalPlan.queuedClips.Count; i++)
            {
                CharacterAnimationQueuedClip queued = finalPlan.queuedClips[i];

                if (queued == null || queued.clip == null)
                {
                    sb.AppendLine($"  FinalQueuedClip[{i}]: NULL");
                    continue;
                }

                CharacterAnimationClip clip = queued.clip;

                sb.AppendLine(
                    $"  FinalQueuedClip[{i}]: Track={clip.track}, " +
                    $"Anim={clip.animaName}, Loop={clip.loop}, " +
                    $"Delay={queued.delay}, " +
                    $"Source={clip.runtimeSourceStateMachine}, ReplayId={clip.runtimeReplayId}, " +
                    $"Mix={clip.mixDuration}, Additive={clip.isAdditive}"
                );
            }
        }
        else
        {
            sb.AppendLine("  QueuedClips: <empty>");
        }






        if (finalPlan == null)
        {
            sb.AppendLine("  FinalPlan=NULL");
        }
        else
        {
            if (finalPlan.clearTracks != null && finalPlan.clearTracks.Count > 0)
            {
                sb.Append("  ClearTracks: ");

                for (int i = 0; i < finalPlan.clearTracks.Count; i++)
                {
                    sb.Append(finalPlan.clearTracks[i]);

                    if (i < finalPlan.clearTracks.Count - 1)
                        sb.Append(", ");
                }

                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("  ClearTracks: <empty>");
            }

            if (finalPlan.clips != null && finalPlan.clips.Count > 0)
            {
                for (int i = 0; i < finalPlan.clips.Count; i++)
                {
                    CharacterAnimationClip clip = finalPlan.clips[i];

                    if (clip == null)
                    {
                        sb.AppendLine($"  FinalClip[{i}]: NULL");
                        continue;
                    }

                    sb.AppendLine(
    $"  FinalClip[{i}]: Track={clip.track}, " +
    $"Anim={clip.animaName}, Loop={clip.loop}, " +
    $"Source={clip.runtimeSourceStateMachine}, ReplayId={clip.runtimeReplayId}, " +
    $"Mix={clip.mixDuration}, Additive={clip.isAdditive}"
                    );
                }
            }
            else
            {
                sb.AppendLine("  Clips: <empty>");
            }
        }

        sb.AppendLine("===============================================");

        return sb.ToString();
    }


    private CharacterAnimationPlan MergePlans(List<ResolvedAnimationPlan> resolvedPlans)
    {
        CharacterAnimationPlan finalPlan = new CharacterAnimationPlan();
        finalPlan.characterRoot = characterRoot;

        finalPlan.clips ??= new List<CharacterAnimationClip>();
        finalPlan.clearTracks ??= new List<int>();

        finalPlan.queuedClips ??= new List<CharacterAnimationQueuedClip>();


        if (resolvedPlans == null || resolvedPlans.Count <= 0)
            return finalPlan;


        //clearTracks ��׼ȷ������Ӧ���ǣ�ϣ��ĳ�� Track ���ձ�ɿա�������������һ�ֹ������,һ�����ն��������ÿ�������ʵֻ�����ֲ�����
        //1. ��һ�� Clip2. ������ Track.��������ģ����ÿ�� Track �ϣ�Play �� Clear ����Ϊ��ѡ�������뾺����
        //Clip ��һ�ֹ��������Play,ClearTrack Ҳ��һ�ֹ��������Clear.ÿ�� Track ����ֻ����һ������Ӯ��Ҫô Play��Ҫô Clear
        //�����ɿ��ƶ���ʱ��AutoAttack: Play Track 0 Attack priority 50;  Movement Idle: Play Track 0 Idle priority 10 ,  Clear Track 1 priority 10
        //���ս��Ӧ���ǣ�Track 0 Play Attack��Track 1 Clear
        // ���ı仯��
        // ÿ�� Track ���ղ���ֻ��ѡ winner clip��
        // ����ѡһ�� winner operation��
        //      Play  = ����ĳ������
        //      Clear = ���������
        Dictionary<int, TrackOperation> trackOperations =
            new Dictionary<int, TrackOperation>();

        for (int i = 0; i < resolvedPlans.Count; i++)
        {
            ResolvedAnimationPlan resolved = resolvedPlans[i];

            if (resolved == null || resolved.plan == null)
                continue;

            CharacterAnimationPlan plan = resolved.plan;

            // 1. clearTracks Ҳ��Ϊ��ѡ�����������ȼ��ٲ�
            if (plan.clearTracks != null)
            {
                for (int j = 0; j < plan.clearTracks.Count; j++)
                {
                    int track = plan.clearTracks[j];

                    if (track < 0)
                        continue;

                    TrackOperation clearOperation = new TrackOperation
                    {
                        type = TrackOperationType.Clear,
                        track = track,
                        priority = resolved.priority,

                        clip = null,

                        sourceStateMachine = resolved.sourceStateMachine,
                        replayId = resolved.replayId
                    };

                    TryApplyTrackOperation(trackOperations, clearOperation);
                }
            }

            // 2. clips ��Ϊ Play �����������ȼ��ٲ�
            if (plan.clips != null)
            {
                for (int j = 0; j < plan.clips.Count; j++)
                {
                    CharacterAnimationClip clip = plan.clips[j];

                    if (clip == null)
                        continue;

                    if (clip.track < 0)
                        continue;

                    TrackOperation playOperation = new TrackOperation
                    {
                        type = TrackOperationType.Play,
                        track = clip.track,
                        priority = resolved.priority,

                        clip = clip,

                        sourceStateMachine = resolved.sourceStateMachine,
                        replayId = resolved.replayId
                    };

                    TryApplyTrackOperation(trackOperations, playOperation);
                }
            }


            if (plan.queuedClips != null)
            {
                for (int j = 0; j < plan.queuedClips.Count; j++)
                {
                    CharacterAnimationQueuedClip queued = plan.queuedClips[j];

                    if (queued == null || queued.clip == null)
                        continue;

                    CharacterAnimationQueuedClip queuedCopy = queued.Clone();
                    queuedCopy.clip.characterRoot = characterRoot;
                    queuedCopy.clip.runtimeSourceStateMachine = resolved.sourceStateMachine;
                    queuedCopy.clip.runtimeReplayId = resolved.replayId;

                    finalPlan.queuedClips.Add(queuedCopy);
                }
            }

        }

        // ========== exclusiveFullBody suppression ==========
        // When a winning Play comes from an exclusiveFullBody source,
        // clear all other tracks so the exclusive animation owns the full body.
        // This lets GunDown stay in activeIntents (avoiding 1-2 frame gap
        // when the oneShot expires) while being visually suppressed during the attack.
        bool hasExclusiveFullBody = false;
        HashSet<int> exclusiveTracks = new HashSet<int>();

        for (int i = 0; i < resolvedPlans.Count; i++)
        {
            ResolvedAnimationPlan resolved = resolvedPlans[i];
            if (!resolved.exclusiveFullBody || resolved.plan == null)
                continue;

            if (resolved.plan.clips != null)
            {
                for (int j = 0; j < resolved.plan.clips.Count; j++)
                {
                    int track = resolved.plan.clips[j].track;
                    if (track < 0)
                        continue;

                    if (trackOperations.TryGetValue(track, out TrackOperation op)
                        && op.type == TrackOperationType.Play
                        && op.sourceStateMachine == resolved.sourceStateMachine)
                    {
                        hasExclusiveFullBody = true;
                        exclusiveTracks.Add(track);
                    }
                }
            }
        }

        if (hasExclusiveFullBody)
        {
            List<int> tracksToClear = new List<int>();
            foreach (KeyValuePair<int, TrackOperation> pair in trackOperations)
            {
                if (exclusiveTracks.Contains(pair.Key))
                    continue;

                if (pair.Value.type == TrackOperationType.Play)
                    tracksToClear.Add(pair.Key);
            }

            for (int i = 0; i < tracksToClear.Count; i++)
            {
                int track = tracksToClear[i];
                trackOperations[track] = new TrackOperation
                {
                    type = TrackOperationType.Clear,
                    track = track,
                    priority = 100,
                    clip = null,
                    sourceStateMachine = AnimationIntentSourceStateMachine.None,
                    replayId = 0
                };
            }
        }

        foreach (KeyValuePair<int, TrackOperation> pair in trackOperations)
        {
            TrackOperation operation = pair.Value;

            if (operation == null)
                continue;

            switch (operation.type)
            {
                case TrackOperationType.Clear:
                    {
                        if (!finalPlan.clearTracks.Contains(operation.track))
                        {
                            finalPlan.clearTracks.Add(operation.track);
                        }

                        break;
                    }

                case TrackOperationType.Play:
                    {
                        if (operation.clip == null)
                            break;

                        CharacterAnimationClip clipCopy = operation.clip.Clone();

                        clipCopy.characterRoot = characterRoot;
                        clipCopy.runtimeSourceStateMachine = operation.sourceStateMachine;
                        clipCopy.runtimeReplayId = operation.replayId;

                        finalPlan.clips.Add(clipCopy);

                        break;
                    }
            }
        }

        return finalPlan;
    }
    private class ResolvedAnimationPlan
    {
        public int resolvedIndex;
        public AnimationIntentSourceStateMachine sourceStateMachine;
        public EAnimSemantic inputSemantic;
        public EAnimationLogicState routedLogicID;
        public int priority;
        public int replayId;
        public bool exclusiveFullBody;
        public CharacterAnimationPlan plan;
    }

    public void ClearTracks(params int[] tracks)
    {
        CharacterAnimationPlan plan = new CharacterAnimationPlan();
        plan.characterRoot = characterRoot;

        for (int i = 0; i < tracks.Length; i++)
        {
            if (tracks[i] >= 0 && !plan.clearTracks.Contains(tracks[i]))
                plan.clearTracks.Add(tracks[i]);
        }

        currentOutputPlan = plan;

        EventCenter.Instance.EventTrigger<CharacterAnimationPlan>(
            GameEvent.AnimationPlanRequest,
            plan
        );
    }

}